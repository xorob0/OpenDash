// CarLightLibrary.cs: getting the measured tables onto the user's machine, and finding one by car.
//
// ADR 0018 decided OpenDash carries none of this data: it is CC BY-NC-SA 4.0, this repository is
// MIT, and data/shift-points.json says OpenDash does not ship measurements it has not made. So the
// tables are fetched, once, and everything after that is local.
//
// **The whole set is fetched in one request rather than one car at a time**, and that is a privacy
// decision rather than an optimisation. A per-car fetch would tell a CDN which car the user got into
// and when, every time they tried a new one -- a session detail leaving the machine, which is the
// line ADR 0012 was written to keep. One archive of every car asks the same question every other
// user asks, discloses nothing about this one, and has the better failure mode besides: a car works
// the first time it is driven, offline, in a session that never reaches the network at all.
//
// It costs 386 KB, once, and only when the driver asks: the tables are fetched by a button on the
// Lights tab and by nothing else (#366). Starting SimHub never reaches out, and neither does the
// update check, which used to carry this along with its own. A copy on disk is the user's own, made
// when they pressed a button that had named the project, the licence, the size and the host -- which
// is what lets OpenDash carry none of the data and still light a wheel with it.
//
// No SimHub or WPF types -- file and zip handling is fine, PackageExtractor does the same and is
// compiled into the tests too. Nothing here throws: a car with no table is a car on the published
// ladder, which is where every car was before this file existed.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace OpenDashPlugin
{
    /// <summary>What a refresh did, so that the panel can say it and the tests can assert it.</summary>
    public sealed class CarLightRefresh
    {
        public bool Ok { get; set; }

        /// <summary>True when the network was actually reached this time.</summary>
        public bool Fetched { get; set; }

        /// <summary>How many cars are readable afterwards.</summary>
        public int Cars { get; set; }

        /// <summary>Why it did not work, for the log and the panel. Null when it did.</summary>
        public string Reason { get; set; }

        public static CarLightRefresh Failed(string reason, int cars)
        {
            return new CarLightRefresh { Ok = false, Reason = reason, Cars = cars };
        }
    }

    public static class CarLightLibrary
    {
        /// <summary>
        /// The archive of the whole database, from the branch its own README points a consumer at.
        ///
        /// <para>codeload rather than the API: it needs no token, answers one request with one file,
        /// and sends nothing but the User-Agent <see cref="ReleaseClient"/> puts on it (ADR 0012).</para>
        /// </summary>
        public const string ArchiveUrl = "https://codeload.github.com/Lovely-Sim-Racing/lovely-car-data/zip/refs/heads/main";

        /// <summary>
        /// Who measured all this. Shown on the lights page, and not decoration: CC BY-NC-SA 4.0 asks
        /// for attribution, and a user is entitled to know whose numbers are lighting their wheel.
        /// </summary>
        public const string Attribution = "Shift lights come from Lovely Car Data, by Lovely Sim Racing, ATSR and Gomez Sim Industries, under CC BY-NC-SA 4.0.";

        /// <summary>Where the attribution points.</summary>
        public const string ProjectUrl = "https://github.com/Lovely-Sim-Racing/lovely-car-data";

        /// <summary>Under SimHub/OpenDash/, beside the profiles the user picks up. Not PluginsData: that is SimHub's.</summary>
        public const string FolderName = "CarLights";

        /// <summary>Written after a successful fetch, and read to decide whether another is due.</summary>
        public const string StampFile = "fetched.txt";

        /// <summary>
        /// How old a copy has to be before the panel offers a refresh. A week: the database gains a car
        /// every few weeks, and nothing about a wheel's lights is urgent enough to mention sooner.
        ///
        /// <para>It decides a sentence and not a request. Nothing refetches on its own since #366, so a
        /// copy older than this is one the Lights tab mentions beside the button; pressing is the
        /// driver's business and a rig that never presses again keeps the cars it has.</para>
        /// </summary>
        public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

        /// <summary>Only iRacing's, out of the ten games the archive carries. OpenDash supports one sim.</summary>
        private const string ArchiveFolder = "data/iracing/";

        public static string FolderPath(string simHubRoot)
        {
            return Path.Combine(simHubRoot, FlagBoxProfile.FolderName, FolderName);
        }

        /// <summary>
        /// The key a car is looked up by: lower case, and everything that is not a letter or a digit
        /// folded to a hyphen.
        ///
        /// <para>The files carry the car's iRacing CarPath verbatim, spaces and all
        /// ("stockcars chevycamarozl12022"), while the archive names the file for the hyphenated form.
        /// Folding both makes the lookup indifferent to which one it was given.</para>
        ///
        /// <para>What hands it over matters as much as the folding. The upstream README says the key
        /// is <c>DataCorePlugin.CarId</c> and <b>that property is empty on iRacing</b>: the CarPath is
        /// on <c>GameData.CarId</c> — <c>StatusDataBase.CarId</c>, which is what the plugin passes in.
        /// Measured on the VM, in a Porsche 992R GT3: GameData.CarId was "porsche992rgt3" and
        /// DataCorePlugin.CarId was null. Reading the documented one finds no car, ever, in silence.</para>
        /// </summary>
        public static string Key(string carId)
        {
            if (string.IsNullOrWhiteSpace(carId)) return string.Empty;
            var text = new StringBuilder();
            var lastWasSeparator = true;
            foreach (var c in carId.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    text.Append(c);
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator)
                {
                    text.Append('-');
                    lastWasSeparator = true;
                }
            }
            return text.ToString().Trim('-');
        }

        /// <summary>Every readable table in a folder, keyed by <see cref="Key"/>. Empty when there is no folder.</summary>
        public static Dictionary<string, CarLightTable> ReadFolder(string folder)
        {
            var cars = new Dictionary<string, CarLightTable>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return cars;
            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                CarLightTable table;
                try
                {
                    table = CarLightTable.Parse(File.ReadAllText(file));
                }
                catch (Exception)
                {
                    // A file that cannot even be read off the disk is one car on the published ladder.
                    continue;
                }
                if (table == null) continue;
                cars[Key(table.CarId)] = table;
            }
            return cars;
        }

        /// <summary>
        /// Writes iRacing's car files out of the archive, replacing whatever was there, and returns how
        /// many landed.
        ///
        /// <para>Entries are taken by suffix rather than by position, because the archive's top folder
        /// is named for the branch and that is upstream's business. Anything outside
        /// <c>data/iracing/</c> is skipped, and so is any entry whose name would escape the folder --
        /// a zip is a file from the internet, and PackageExtractor takes the same precaution.</para>
        /// </summary>
        public static int Extract(byte[] archive, string folder)
        {
            if (archive == null || archive.Length == 0) return 0;
            Directory.CreateDirectory(folder);
            var written = 0;
            using (var stream = new MemoryStream(archive))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    var name = entry.FullName.Replace('\\', '/');
                    var at = name.IndexOf(ArchiveFolder, StringComparison.OrdinalIgnoreCase);
                    if (at < 0) continue;
                    var leaf = name.Substring(at + ArchiveFolder.Length);
                    if (leaf.Length == 0 || leaf.Contains("/") || !leaf.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                    var target = Path.Combine(folder, leaf);
                    // Belt and braces over the check above: the written path must still be inside.
                    if (!Path.GetFullPath(target).StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        entry.ExtractToFile(target, true);
                        written++;
                    }
                    catch (Exception)
                    {
                        // One file that will not write is one car on the published ladder.
                    }
                }
            }
            return written;
        }

        /// <summary>When the folder was last filled from upstream, or null if it never was.</summary>
        public static DateTime? FetchedAt(string folder)
        {
            try
            {
                var stamp = Path.Combine(folder, StampFile);
                if (!File.Exists(stamp)) return null;
                long ticks;
                if (!long.TryParse(File.ReadAllText(stamp).Trim(), out ticks)) return null;
                return new DateTime(ticks, DateTimeKind.Utc);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether the copy is old enough to be worth mentioning: never fetched, or fetched longer ago
        /// than <see cref="MaxAge"/>. Read by the panel, and by nothing that makes a request.
        /// </summary>
        public static bool IsStale(string folder, DateTime nowUtc)
        {
            var fetched = FetchedAt(folder);
            if (fetched == null) return true;
            // A stamp in the future is a clock that moved, not a fresh copy.
            return nowUtc - fetched.Value >= MaxAge || fetched.Value > nowUtc;
        }

        private static void Stamp(string folder, DateTime nowUtc)
        {
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, StampFile), nowUtc.Ticks.ToString());
            }
            catch (Exception)
            {
                // A stamp that will not write costs one extra fetch next time and nothing else.
            }
        }

        /// <summary>
        /// Fetches the archive and writes it out, synchronously. The whole decision is here so that it
        /// is testable; <see cref="CarLightService"/> is what puts it on a thread.
        /// </summary>
        public static CarLightRefresh Fetch(IReleaseSource source, string folder, DateTime nowUtc)
        {
            if (source == null) return CarLightRefresh.Failed("no source", 0);
            FetchResult result;
            try
            {
                result = source.GetBytes(ArchiveUrl);
            }
            catch (Exception e)
            {
                return CarLightRefresh.Failed(e.Message, 0);
            }
            if (result == null || !result.Ok || result.Bytes == null) return CarLightRefresh.Failed(result?.Reason ?? "no answer", 0);

            int written;
            try
            {
                written = Extract(result.Bytes, folder);
            }
            catch (Exception e)
            {
                // Not a zip, a truncated one, or a disk that said no. The copy already on disk stands.
                return CarLightRefresh.Failed(e.Message, 0);
            }
            if (written == 0) return CarLightRefresh.Failed("the archive carried no iRacing cars", 0);
            Stamp(folder, nowUtc);
            return new CarLightRefresh { Ok = true, Fetched = true, Cars = written };
        }
    }
}
