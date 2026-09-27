// CarLightService.cs: the tables in memory, the car on screen, and the strings the profile reads.
//
// This is the part ADR 0018 reopened ADR 0009 for. It is the only place in OpenDash that computes
// from telemetry, and it computes exactly one thing: what colour each LED of a strip should be, for
// the car the driver is sitting in, in the gear they are in, right now. There is no SimHub property
// to derive that from, no expression that could hold an 85-car table, and no NCalc clock to flash
// with -- which is the test ADR 0009 set for reopening itself.
//
// **A run is published as one string, not as one property per LED.** Ten run lengths at up to 25
// LEDs each would be 147 property names in a contract that has 259 of everything else. Instead each
// run is a fixed-width string of nine-character colours and the profile slices LED k out of it with
// `left(value, k * 9, 9)`, which is the whole reason CarLightTable normalises every colour to
// #AARRGGBB. Twelve names instead of a hundred and forty-nine.
//
// Nothing here throws. An unobserved exception on the thread pool takes SimHub down on .NET
// Framework, and the fallback for every failure is the same and is already built: the strip goes
// back to the ladder iRacing publishes.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace OpenDashPlugin
{
    public sealed class CarLightService
    {
        private readonly IReleaseSource source;
        private readonly string folder;
        private readonly object fetchLock = new object();

        private volatile Dictionary<string, CarLightTable> cars = new Dictionary<string, CarLightTable>(StringComparer.Ordinal);
        private volatile Frame frame = Frame.Dark();
        private volatile string status = "not loaded";
        private DateTime? fetchedAt;

        /// <summary>The car last looked up, so that a lookup happens on a car change and not every frame.</summary>
        private string lastCarId;
        private CarLightTable lastTable;

        public CarLightService(IReleaseSource source, string folder)
        {
            this.source = source;
            this.folder = folder;
        }

        /// <summary>How many cars are readable. Zero before the first fetch, and on a machine that never reaches it.</summary>
        public int CarCount
        {
            get { return cars.Count; }
        }

        /// <summary>When the tables were last fetched, or null if they never have been.</summary>
        public DateTime? FetchedAt
        {
            get { return fetchedAt; }
        }

        /// <summary>Whether a mirror is being published this frame, which is what the profile switches on.</summary>
        public bool Ready
        {
            get { return frame.Ready; }
        }

        /// <summary>
        /// How many of the car's own three shift bands the engine has entered, or -1 when no table is
        /// being read. What the flag box's digit is coloured by when a panel is set to the car's own.
        /// </summary>
        public int Stage
        {
            get { return frame.Ladder.Stage; }
        }

        /// <summary>Whether the car is past its own redline for the gear it is in. False whenever
        /// <see cref="Stage"/> is -1, and false for the 47 cars in 85 that publish no flash.</summary>
        public bool OverRev
        {
            get { return frame.OverRev; }
        }

        /// <summary>
        /// Whether this car and gear have a flash to give at all, which is not the same question as
        /// whether they are giving one now.
        /// </summary>
        /// <remarks>
        /// 47 of the 85 measured cars publish no flash, and a strip mirroring one of them simply does
        /// not blink. A screen draws OpenDash's own bar, whose top band has flashed at redline since
        /// ADR 0004, so it needs to tell "not over-revving" from "never says so" and fall back to the
        /// published threshold for the second (#353). False whenever no table is being read.
        /// </remarks>
        public bool Flashes
        {
            get { return frame.Flashes; }
        }

        /// <summary>
        /// How many of the car's own LEDs are lit this frame, out of <see cref="Lamps"/>: what a rev bar
        /// of any segment count fills itself from (#353).
        ///
        /// <para>A count and a total rather than a fraction, because a screen compares them by
        /// cross-multiplication, band by band -- <c>lit * 3m &gt; (band * m + local) * lamps</c> for a
        /// band of m segments -- exactly as the published ladder's bands do. So it lights a segment on
        /// the frame the car lights its own LED rather than a rounding either side of it, and it changes
        /// band where <see cref="Stage"/> does, at any segment count.</para>
        /// </summary>
        public int Lit
        {
            get { return frame.Ladder.Lit; }
        }

        /// <summary>How many LEDs the car's ladder has in the gear it is in. Zero when no table is being read,
        /// which is what makes a screen fall back to the ladder the sim publishes.</summary>
        public int Lamps
        {
            get { return frame.Ladder.Lamps; }
        }

        /// <summary>The RPM the top third of the car's bar lights at, which is the number a readout beside a
        /// rev bar prints. Zero when no table is being read.</summary>
        public int TopRpm
        {
            get { return frame.Ladder.TopRpm; }
        }

        /// <summary>The measured name of the car being mirrored, for the panel. Null when none is.</summary>
        public string CarName
        {
            get { return frame.CarName; }
        }

        /// <summary>One line for the lights page. Said plainly, because every failure here is silent by design.</summary>
        public string Status
        {
            get { return status; }
        }

        /// <summary>
        /// Reads whatever is on disk, and asks for nothing.
        ///
        /// <para>This is the startup path and the whole of it: OpenDash never fetches the tables on its
        /// own (#366). A rig that has them uses them, offline and every time; a rig that has not has no
        /// mirror, which is the fallback every car had before the tables existed.</para>
        ///
        /// <para>Synchronous and returning what happened, the way UpdateService's entry points are, so
        /// that every branch of it is tested.</para>
        /// </summary>
        public CarLightRefresh Load(DateTime nowUtc)
        {
            lock (fetchLock)
            {
                return Read(null, nowUtc);
            }
        }

        /// <summary>
        /// Fetches the archive because the user pressed the button, then reads what arrived.
        ///
        /// <para>Unconditional: a press is a person asking, so the week <see cref="CarLightLibrary.MaxAge"/>
        /// describes is not consulted. It is what makes the copy the user's own — made when they asked
        /// for it, from a panel that had named the project, the licence and the size (ADR 0018).</para>
        /// </summary>
        public CarLightRefresh Download(DateTime nowUtc)
        {
            lock (fetchLock)
            {
                return Read(CarLightLibrary.Fetch(source, folder, nowUtc), nowUtc);
            }
        }

        /// <summary>The half both share: take the folder as it now stands and say what is in it.</summary>
        private CarLightRefresh Read(CarLightRefresh fetch, DateTime nowUtc)
        {
            Dictionary<string, CarLightTable> loaded;
            try
            {
                loaded = CarLightLibrary.ReadFolder(folder);
            }
            catch (Exception e)
            {
                status = "could not read the car light tables: " + e.Message;
                return CarLightRefresh.Failed(e.Message, cars.Count);
            }

            cars = loaded;
            fetchedAt = CarLightLibrary.FetchedAt(folder);
            // The car on screen may now have a table, or may have lost one.
            lastCarId = null;
            lastTable = null;

            status = Describe(loaded.Count, fetchedAt, nowUtc, fetch);
            if (loaded.Count == 0) return CarLightRefresh.Failed(status, 0);
            return new CarLightRefresh { Ok = true, Fetched = fetch != null && fetch.Ok, Cars = loaded.Count };
        }

        /// <summary>What the panel says about the tables. Pure, so the wording is pinned like UpdateWording's.</summary>
        public static string Describe(int cars, DateTime? fetchedAt, DateTime nowUtc, CarLightRefresh fetch)
        {
            // The state a fresh install is in and stays in until somebody presses the button, so it is a
            // sentence rather than a count of zero. A download that was tried and did not answer says so:
            // the reason is the only thing the panel can offer a person on hotel wifi.
            if (cars == 0)
            {
                return fetch != null && !fetch.Ok
                    ? PanelLights.CarTablesNone + " " + PanelLights.CarTablesFailed(fetch.Reason)
                    : PanelLights.CarTablesNone;
            }
            var line = cars == 1 ? "1 car" : cars + " cars";
            if (fetchedAt == null) return line;
            var age = nowUtc - fetchedAt.Value;
            var when = age < TimeSpan.FromMinutes(2) ? "just now"
                : age < TimeSpan.FromHours(1) ? (int)age.TotalMinutes + " minutes ago"
                : age < TimeSpan.FromHours(2) ? "an hour ago"
                : age < TimeSpan.FromDays(1) ? (int)age.TotalHours + " hours ago"
                : (int)age.TotalDays == 1 ? "yesterday"
                : (int)age.TotalDays + " days ago";
            line += ", updated " + when;
            // A failed refresh does not hide the copy that is working; it explains why it is not newer.
            if (fetch != null && !fetch.Ok) line += " (last download failed: " + fetch.Reason + ")";
            return line;
        }

        /// <summary>
        /// The same, off the calling thread, for startup. Swallows everything: an unobserved exception
        /// on the thread pool ends the SimHub process on .NET Framework.
        /// </summary>
        public void LoadInBackground()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Load(DateTime.UtcNow);
                }
                catch (Exception)
                {
                    status = "the car light tables could not be loaded";
                }
            });
        }

        /// <summary>Whether the copy on disk is old enough to be worth offering a refresh for.</summary>
        /// <remarks>
        /// It decides a sentence now rather than a request. Nothing refetches on its own, so a stale copy
        /// is a thing the panel mentions beside the button and the driver ignores if they like.
        /// The clock is the caller's, as it is for <see cref="Load"/> and <see cref="Download"/>: as a
        /// property reading <see cref="DateTime.UtcNow"/> itself, this went stale for real one week after
        /// the date a test had pinned, and took CI with it.
        /// </remarks>
        public bool Stale(DateTime nowUtc)
        {
            return cars.Count > 0 && CarLightLibrary.IsStale(folder, nowUtc);
        }

        /// <summary>The table for a car, or null when there is none. Keyed the folded way, so spelling does not matter.</summary>
        public CarLightTable For(string carId)
        {
            CarLightTable table;
            return cars.TryGetValue(CarLightLibrary.Key(carId), out table) ? table : null;
        }

        /// <summary>
        /// One frame: work out every run's colours for the car, gear and RPM now.
        ///
        /// <para>Ten runs of at most 25 LEDs is a few hundred comparisons, which is nothing beside the
        /// frame SimHub is already drawing — so there is no caching on RPM here, and the flash stays
        /// exact.</para>
        /// </summary>
        public void Update(string carId, string gear, double rpm, MirrorFit fit, bool enabled, long clockMs)
        {
            if (!enabled)
            {
                frame = Frame.Dark();
                return;
            }
            // Looked up on a car change rather than per frame: the fold allocates and this runs at 60 Hz.
            if (!string.Equals(carId, lastCarId, StringComparison.Ordinal))
            {
                lastCarId = carId;
                lastTable = For(carId);
            }
            var table = lastTable;
            if (table == null)
            {
                frame = Frame.Dark();
                return;
            }

            var runs = new string[Contract.MirrorRunLengths.Length];
            for (var i = 0; i < Contract.MirrorRunLengths.Length; i++)
            {
                var length = Contract.MirrorRunLengths[i];
                var colors = CarLightMirror.Colors(table, gear, rpm, length, fit, clockMs);
                runs[i] = colors == null ? Packed(CarLightMirror.Dark(length)) : Packed(colors);
            }
            // The ladder as numbers, for the digit on a flag box and for the bar on a screen. One more
            // lookup of a row Colors() has already found ten times this frame, and the alternative is a
            // second entry point walking the same table.
            var row = CarLightMirror.GearFor(table, gear);
            frame = new Frame
            {
                Ready = true,
                CarName = table.CarName,
                Runs = runs,
                Ladder = row == null ? CarLightMirror.CarLadder.None : CarLightMirror.Ladder(row, rpm),
                OverRev = row != null && CarLightMirror.OverRev(table, row, rpm),
                Flashes = CarLightMirror.CanOverRev(table, row),
            };
        }

        /// <summary>
        /// A run as the profile reads it: fixed-width colours end to end, sliced with <c>left()</c>.
        /// </summary>
        public static string Packed(string[] colors)
        {
            return string.Concat(colors);
        }

        /// <summary>What the property for a run of this length carries. Dark, at the right width, when there is no mirror.</summary>
        public string Run(int length)
        {
            var at = Array.IndexOf(Contract.MirrorRunLengths, length);
            if (at < 0) return string.Empty;
            var current = frame;
            return current.Runs != null ? current.Runs[at] : Packed(CarLightMirror.Dark(length));
        }

        /// <summary>One frame's answer, swapped in whole so that a reader never sees half of one.</summary>
        private sealed class Frame
        {
            public bool Ready;
            public string CarName;
            public string[] Runs;
            public CarLightMirror.CarLadder Ladder;
            public bool OverRev;
            public bool Flashes;

            /// <summary>
            /// No mirror, shared rather than made.
            ///
            /// <para>It is returned on every frame the mirror is off, which at SimHub's data rate is
            /// sixty allocations a second for the whole time somebody is using one of OpenDash's own
            /// styles. It never changes, so there is one of it.</para>
            /// </summary>
            public static readonly Frame None = new Frame { Ready = false, CarName = null, Runs = null, Ladder = CarLightMirror.CarLadder.None, OverRev = false, Flashes = false };

            public static Frame Dark()
            {
                return None;
            }
        }
    }
}
