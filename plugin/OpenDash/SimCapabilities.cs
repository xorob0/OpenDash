// SimCapabilities.cs: what the running sim can report, so that a dashboard never has to name a game.
//
// Every binding OpenDash ships was written against iRacing. Most of what it reads SimHub normalises for
// every game, and most of the rest is iRacing's raw telemetry, which is simply absent on another sim, so
// an isnull() in the expression already tells "not reported" from a value. A few questions cannot be told
// apart that way: a car without a pit limiter reads PitLimiterOn 0 like a limiter switched off, Assetto
// Corsa's reader fills PitLimiterOn with the pit lane, iRacing fills CarDamage1..5 with zeros, and the
// raw reading that says the driver is in the car has another name and another shape on every sim, and Le
// Mans Ultimate's reader raises SimHub's Flag_Black for a retired car, Flag_Yellow for a yellow anywhere on
// the track, and never the chequer or the white (#1129). For
// those the plugin answers, from the table below, and publishes one OpenDash property per question
// (Contract.Capabilities); ADR 0021 is the rule and docs/research/sims.md (#1100) is where each row's
// facts were read. A game with no row gets null for every question, which is also what a dashboard reads
// with no plugin, and every dashboard reads each one with iRacing's own answer as its isnull() fallback.
//
// Pure: no SimHub types, so the net8.0 tests compile it. The plugin hands it the running game's code and
// a way to read one of SimHub's properties by name, PluginManager.GetPropertyValue.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace OpenDashPlugin
{
    /// <summary>
    /// One frame's answers, one per <see cref="Contract.Capabilities"/> name, each true, false or null where
    /// the sim does not say. Immutable and interned, so that a frame never builds a new one.
    /// </summary>
    public sealed class SimReport
    {
        /// <summary>How many questions there are, and so how many digits a report's index has.</summary>
        private static readonly int Count = Contract.Capabilities.Length;

        /// <summary>
        /// One slot per set of answers, by <see cref="Index"/>: three answers to each of the questions. Filled
        /// as each set is first asked for, since a session meets a handful of the 6561 and never the rest.
        /// </summary>
        private static readonly SimReport[] Interned = new SimReport[Power(Count)];

        /// <summary>Every answer null: no game, a game with no row, or no frame to read.</summary>
        public static readonly SimReport Unknown = Of();

        private readonly bool?[] answers;

        /// <summary>See <see cref="Contract.CarHasPitLimiter"/>.</summary>
        public bool? CarHasPitLimiter => Read(Contract.CarHasPitLimiter);

        /// <summary>See <see cref="Contract.PitLimiterOn"/>.</summary>
        public bool? PitLimiterOn => Read(Contract.PitLimiterOn);

        /// <summary>See <see cref="Contract.DamageReported"/>.</summary>
        public bool? DamageReported => Read(Contract.DamageReported);

        /// <summary>See <see cref="Contract.DriverInCar"/>.</summary>
        public bool? DriverInCar => Read(Contract.DriverInCar);

        /// <summary>See <see cref="Contract.YellowFlagShown"/>.</summary>
        public bool? YellowFlagShown => Read(Contract.YellowFlagShown);

        /// <summary>See <see cref="Contract.BlackFlagShown"/>.</summary>
        public bool? BlackFlagShown => Read(Contract.BlackFlagShown);

        /// <summary>See <see cref="Contract.WhiteFlagShown"/>.</summary>
        public bool? WhiteFlagShown => Read(Contract.WhiteFlagShown);

        /// <summary>See <see cref="Contract.ChequeredFlagShown"/>.</summary>
        public bool? ChequeredFlagShown => Read(Contract.ChequeredFlagShown);

        private SimReport(bool?[] answers)
        {
            this.answers = answers;
        }

        /// <summary>
        /// The one instance holding these answers, in the order of <see cref="Contract.Capabilities"/>; a question
        /// left off the end is answered null.
        /// </summary>
        public static SimReport Of(params bool?[] answers)
        {
            var index = 0;
            for (var i = 0; i < Count; i++) index = index * 3 + Digit(answers != null && i < answers.Length ? answers[i] : null);
            return At(index);
        }

        /// <summary>One answer, by its <see cref="Contract.Capabilities"/> name; null for a name that is not one.</summary>
        public bool? Read(string capability)
        {
            var i = Array.IndexOf(Contract.Capabilities, capability);
            return i < 0 ? null : answers[i];
        }

        public override string ToString()
        {
            return "limiter " + Word(CarHasPitLimiter) + ", on " + Word(PitLimiterOn)
                + ", damage " + Word(DamageReported) + ", in the car " + Word(DriverInCar)
                + ", yellow " + Word(YellowFlagShown) + ", black " + Word(BlackFlagShown)
                + ", white " + Word(WhiteFlagShown) + ", chequered " + Word(ChequeredFlagShown);
        }

        private static string Word(bool? answer) => answer.HasValue ? (answer.Value ? "yes" : "no") : "unknown";

        /// <summary>An answer as one digit of a report's index: unknown, no, yes.</summary>
        internal static int Digit(bool? answer) => answer.HasValue ? (answer.Value ? 2 : 1) : 0;

        /// <summary>The report at an index built from <see cref="Digit"/>s, the first question the most significant.</summary>
        internal static SimReport At(int index)
        {
            var report = Volatile.Read(ref Interned[index]);
            if (report != null) return report;
            var answers = new bool?[Count];
            for (int i = Count - 1, rest = index; i >= 0; i--, rest /= 3)
            {
                var digit = rest % 3;
                answers[i] = digit == 0 ? (bool?)null : digit == 2;
            }
            return Interlocked.CompareExchange(ref Interned[index], new SimReport(answers), null) ?? Interned[index];
        }

        private static int Power(int digits)
        {
            var n = 1;
            for (var i = 0; i < digits; i++) n *= 3;
            return n;
        }
    }

    /// <summary>The table: how each sim SimHub reads answers each question a dashboard asks of it.</summary>
    public static class SimCapabilities
    {
        /// <summary>SimHub's code for iRacing, as GameData.GameName and DataCorePlugin.CurrentGame carry it.</summary>
        public const string IRacing = "IRacing";

        /// <summary>SimHub's code for Assetto Corsa.</summary>
        public const string AssettoCorsa = "AssettoCorsa";

        /// <summary>SimHub's code for Assetto Corsa Competizione.</summary>
        public const string AssettoCorsaCompetizione = "AssettoCorsaCompetizione";

        /// <summary>SimHub's code for Le Mans Ultimate.</summary>
        public const string LeMansUltimate = "LMU";

        /// <summary>Where SimHub publishes what its reader normalised, for every game.</summary>
        public const string GameData = "DataCorePlugin.GameData.";

        /// <summary>Where SimHub publishes the running game's own data, under that game's own names.</summary>
        public const string GameRawData = "DataCorePlugin.GameRawData.";

        /// <summary>AC_LIVE in Assetto Corsa's and ACC's AC_STATUS: a session running with the player driving.</summary>
        public const int AcLive = 2;

        /// <summary>Where Le Mans Ultimate publishes the session's scoring, under rFactor 2's names.</summary>
        public const string LmuScoring = GameRawData + "LMUNativeTelemetry.scoring.scoringInfo.";

        /// <summary>Where Le Mans Ultimate publishes the player's own car's scoring.</summary>
        public const string LmuPlayer = GameRawData + "PlayerNativeScoring.";

        /// <summary>rFactor 2's mGamePhase while the race is green.</summary>
        public const int LmuGreen = 5;

        /// <summary>rFactor 2's mGamePhase under a full course yellow.</summary>
        public const int LmuFullCourseYellow = 6;

        /// <summary>rFactor 2's mGamePhase once the session is over: the chequer is out.</summary>
        public const int LmuSessionOver = 8;

        /// <summary>rFactor 2's mFinishStatus for a car that has taken the chequer.</summary>
        public const int LmuFinished = 1;

        /// <summary>rFactor 2's first race session in mSession: 10 to 13 are the races, below are practice, qualifying and warmup.</summary>
        public const int LmuFirstRace = 10;

        /// <summary>How one sim answers one question, given a way to read a property by its full name.</summary>
        public delegate bool? Answer(Func<string, object> property);

        /// <summary>How one sim answers each question. A null answer is a question the sim does not answer.</summary>
        public sealed class Row
        {
            public Answer CarHasPitLimiter { get; set; }
            public Answer PitLimiterOn { get; set; }
            public Answer DamageReported { get; set; }
            public Answer DriverInCar { get; set; }
            public Answer YellowFlagShown { get; set; }
            public Answer BlackFlagShown { get; set; }
            public Answer WhiteFlagShown { get; set; }
            public Answer ChequeredFlagShown { get; set; }

            /// <summary>The answer to one question by its <see cref="Contract.Capabilities"/> name.</summary>
            public Answer For(string capability)
            {
                switch (capability)
                {
                    case Contract.CarHasPitLimiter: return CarHasPitLimiter;
                    case Contract.PitLimiterOn: return PitLimiterOn;
                    case Contract.DamageReported: return DamageReported;
                    case Contract.DriverInCar: return DriverInCar;
                    case Contract.YellowFlagShown: return YellowFlagShown;
                    case Contract.BlackFlagShown: return BlackFlagShown;
                    case Contract.WhiteFlagShown: return WhiteFlagShown;
                    case Contract.ChequeredFlagShown: return ChequeredFlagShown;
                    default: return null;
                }
            }
        }

        /// <summary>The same answer for every car on every frame.</summary>
        public static Answer Always(bool answer) => property => answer;

        /// <summary>Whether a property is there at all: an in-car control a car without the device does not have.</summary>
        public static Answer Present(string name) => property => property(name) != null;

        /// <summary>A property read as true or false, a number being true when it is not zero; null while it is absent.</summary>
        public static Answer Truth(string name) => property => Truthy(property(name));

        /// <summary>Whether a property holds one value, an enum read as its number; null while it is absent.</summary>
        public static Answer Is(string name, long value) => property =>
        {
            var number = Number(property(name));
            return number.HasValue ? number.Value == value : (bool?)null;
        };

        /// <summary>
        /// Whether a yellow is out in the sector the player's car is in, rather than anywhere on the track. rFactor 2
        /// numbers the player's mSector 0 for the third sector, 1 for the first and 2 for the second, and orders
        /// mSectorFlag the same way, which SimHub publishes one-based as mSectorFlag01 to mSectorFlag03; a flag of 1
        /// is a yellow, as LMUManager.GD_Flag_Yellow reads it. Null while either is absent, so a dashboard falls
        /// back on SimHub's own any-sector Flag_Yellow rather than read "no yellow".
        /// </summary>
        public static bool? LmuLocalYellow(Func<string, object> property)
        {
            var sector = Number(property(LmuPlayer + "mSector"));
            if (!sector.HasValue || sector.Value < 0 || sector.Value > 2) return null;
            var flag = Number(property(LmuScoring + "mSectorFlag0" + (sector.Value + 1).ToString(CultureInfo.InvariantCulture)));
            return flag.HasValue ? flag.Value == 1 : (bool?)null;
        }

        /// <summary>
        /// Whether the chequer is out for the player: the session is over, rFactor 2's mGamePhase 8, which it becomes
        /// as the leader crosses the line and stays at for every car still to cross it, or the player has finished.
        /// Null with no session to read. SimHub's own Flag_Checkered is [NotAvailable] on Le Mans Ultimate.
        /// </summary>
        public static bool? LmuChequered(Func<string, object> property)
        {
            var phase = Number(property(LmuScoring + "mGamePhase"));
            if (!phase.HasValue) return null;
            return phase.Value == LmuSessionOver || Number(property(LmuPlayer + "mFinishStatus")) == LmuFinished;
        }

        /// <summary>
        /// Whether the player is on the last lap of a race that is still running: in a race of so many laps, the
        /// player has completed all but one of them; in a timed race, the time has run out and the player has not
        /// yet crossed the line. An approximation, since rFactor 2 publishes no white flag: a car the leader has
        /// lapped is shown it a lap late, as the leader's crossing ends that car's race too. Null with no session to
        /// read. SimHub's own Flag_White is [NotAvailable] on Le Mans Ultimate.
        /// </summary>
        public static bool? LmuLastLap(Func<string, object> property)
        {
            var session = Number(property(LmuScoring + "mSession"));
            var phase = Number(property(LmuScoring + "mGamePhase"));
            if (!session.HasValue || !phase.HasValue) return null;
            if (session.Value < LmuFirstRace || (phase.Value != LmuGreen && phase.Value != LmuFullCourseYellow)) return false;
            if (Number(property(LmuPlayer + "mFinishStatus")) > 0) return false;
            var maxLaps = Number(property(LmuScoring + "mMaxLaps"));
            if (maxLaps.HasValue && maxLaps.Value > 0 && maxLaps.Value < LmuTimedRaceLaps)
            {
                var laps = Number(property(LmuPlayer + "mTotalLaps"));
                return laps.HasValue ? laps.Value >= maxLaps.Value - 1 : (bool?)null;
            }
            var remaining = Real(property(LmuScoring + "mSessionTimeRemaining"));
            return remaining.HasValue ? remaining.Value <= 0 : (bool?)null;
        }

        /// <summary>The mMaxLaps at and above which rFactor 2's race is one of time rather than laps: it writes a huge number there.</summary>
        public const int LmuTimedRaceLaps = 10000;

        /// <summary>
        /// One row per game, by SimHub's code. Each answer cites the reader member docs/research/sims.md read it
        /// from; adding a game is adding a row here and its test, and nothing in a dashboard.
        /// </summary>
        private static readonly Dictionary<string, Row> Table = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase)
        {
            [IRacing] = new Row
            {
                // The in-car control, which a car without a limiter does not have: pitAlerts.ts's old test.
                CarHasPitLimiter = Present(GameRawData + "Telemetry.dcPitSpeedLimiterToggle"),
                // The limiter bit of EngineWarnings (IRacingManager.GD_PitLimiterOn).
                PitLimiterOn = Truth(GameData + "PitLimiterOn"),
                // GD_CarDamage1..5 are [NotAvailable] and return 0: a zero is not an undamaged car.
                DamageReported = Always(false),
                // "Car on track physics running with player in car."
                DriverInCar = Truth(GameRawData + "Telemetry.IsOnTrack"),
                // The flags are SessionFlagsDetails' bits, which a dashboard asks before any of these and which
                // iRacing publishes on every frame, so there is nothing for the plugin to answer.
                YellowFlagShown = null,
                BlackFlagShown = null,
                WhiteFlagShown = null,
                ChequeredFlagShown = null,
            },
            [AssettoCorsa] = new Row
            {
                // A feature of the game and not of the car: every car has one.
                CarHasPitLimiter = Always(true),
                // ACManager.GD_PitLimiterOn returns GD_IsInPitLane, the lane and not the limiter; the
                // physics page carries the limiter itself.
                PitLimiterOn = Truth(GameRawData + "Physics.PitLimiterOn"),
                // ACManager.GD_CarDamage1..5 read Physics.CarDamage.
                DamageReported = Always(true),
                // Graphics.Status: AC_OFF, AC_REPLAY, AC_LIVE, AC_PAUSE. Live is the driver in the car.
                DriverInCar = Is(GameRawData + "Graphics.Status", AcLive),
                // ACManager's Flag_* read Graphics.Flag, the one flag the game shows the driver, so SimHub's own
                // summary is the answer and a dashboard reads it (#551).
                YellowFlagShown = null,
                BlackFlagShown = null,
                WhiteFlagShown = null,
                ChequeredFlagShown = null,
            },
            [AssettoCorsaCompetizione] = new Row
            {
                CarHasPitLimiter = Always(true),
                // ACCManager.GD_PitLimiterOn reads Physics.PitLimiterOn, so SimHub's own is right here.
                PitLimiterOn = Truth(GameData + "PitLimiterOn"),
                // ACCManager.GD_CarDamage1..5 read Physics.CarDamage.
                DamageReported = Always(true),
                DriverInCar = Is(GameRawData + "Graphics.Status", AcLive),
                // ACCManager's Flag_* read Graphics.Flag, as Assetto Corsa's do; the global flags it also has are
                // the track's and not the driver's.
                YellowFlagShown = null,
                BlackFlagShown = null,
                WhiteFlagShown = null,
                ChequeredFlagShown = null,
            },
            [LeMansUltimate] = new Row
            {
                // LMU's own telemetry says whether the car has one.
                CarHasPitLimiter = Truth(GameRawData + "PlayerNativeTelemetry.mSpeedLimiterAvailable"),
                // LMUManager.GD_PitLimiterOn is mSpeedLimiter > 0.
                PitLimiterOn = Truth(GameData + "PitLimiterOn"),
                // GD_CarDamage1..5 are [NotAvailable]; the dents LMU has are not SimHub's five panels.
                DamageReported = Always(false),
                // mInRealtime: in the car rather than in the monitor.
                DriverInCar = Truth(GameRawData + "LMUNativeTelemetry.scoring.scoringInfo.mInRealtime"),
                // GD_Flag_Yellow is any mSectorFlag at 1: a yellow anywhere on the track. The driver's own sector.
                YellowFlagShown = LmuLocalYellow,
                // GD_Flag_Black is mFinishStatus 2 or 3, a retirement or a disqualification. A black flag on track
                // is a penalty to serve, and mNumPenalties counts those outstanding.
                BlackFlagShown = property =>
                {
                    var penalties = Number(property(LmuPlayer + "mNumPenalties"));
                    return penalties.HasValue ? penalties.Value > 0 : (bool?)null;
                },
                // GD_Flag_White is [NotAvailable].
                WhiteFlagShown = LmuLastLap,
                // GD_Flag_Checkered is [NotAvailable].
                ChequeredFlagShown = LmuChequered,
            },
        };

        /// <summary>The codes of every game the table has a row for.</summary>
        public static IEnumerable<string> Games => Table.Keys;

        /// <summary>
        /// The answers for one frame of <paramref name="gameName"/>, or <see cref="SimReport.Unknown"/> for a
        /// game the table has no row for. Never throws: a property that cannot be read is an answer not given.
        /// </summary>
        /// <param name="gameName">SimHub's code for the running game, GameData.GameName.</param>
        /// <param name="property">Reads one of SimHub's properties by its full name; null when there is no such property.</param>
        public static SimReport Read(string gameName, Func<string, object> property)
        {
            Row row;
            if (gameName == null || property == null || !Table.TryGetValue(gameName, out row)) return SimReport.Unknown;
            // The index is built answer by answer rather than through SimReport.Of, so a frame allocates nothing.
            var index = 0;
            foreach (var capability in Contract.Capabilities) index = index * 3 + SimReport.Digit(Ask(row.For(capability), property));
            return SimReport.At(index);
        }

        private static bool? Ask(Answer answer, Func<string, object> property)
        {
            if (answer == null) return null;
            try
            {
                return answer(property);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A value as true or false: a boolean as it is, a number or an enum true when it is not zero, a
        /// string by either reading; null when absent or unreadable.</summary>
        public static bool? Truthy(object value)
        {
            if (value == null) return null;
            if (value is bool b) return b;
            if (value is string s)
            {
                bool parsed;
                if (bool.TryParse(s.Trim(), out parsed)) return parsed;
            }
            var number = Real(value);
            return number.HasValue ? number.Value != 0 : (bool?)null;
        }

        /// <summary>A value as a whole number: an enum by its value, a number truncated, a string parsed; null otherwise.</summary>
        public static long? Number(object value)
        {
            var real = Real(value);
            return real.HasValue ? (long)real.Value : (long?)null;
        }

        /// <summary>A value as a number: an enum by its value, a number as it is, a string parsed; null otherwise.</summary>
        private static double? Real(object value)
        {
            if (value == null || value is bool) return null;
            try
            {
                if (value is Enum) return Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (value is string s)
                {
                    double parsed;
                    return double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : (double?)null;
                }
                if (value is IConvertible) return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                // A value no conversion understands is no answer.
            }
            return null;
        }
    }
}
