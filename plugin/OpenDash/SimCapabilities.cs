// SimCapabilities.cs: what the running sim can report, so that a dashboard never has to name a game.
//
// Every binding OpenDash ships was written against iRacing. Most of what it reads SimHub normalises for
// every game, and most of the rest is iRacing's raw telemetry, which is simply absent on another sim, so
// an isnull() in the expression already tells "not reported" from a value. A few questions cannot be told
// apart that way: a car without a pit limiter reads PitLimiterOn 0 like a limiter switched off, Assetto
// Corsa's reader fills PitLimiterOn with the pit lane, iRacing fills CarDamage1..5 with zeros, and the
// raw reading that says the driver is in the car has another name and another shape on every sim. For
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

namespace OpenDashPlugin
{
    /// <summary>
    /// One frame's answers, one per <see cref="Contract.Capabilities"/> name, each true, false or null where
    /// the sim does not say. Immutable and interned, so that a frame never builds a new one.
    /// </summary>
    public sealed class SimReport
    {
        private static readonly SimReport[] Interned = Build();

        /// <summary>Every answer null: no game, a game with no row, or no frame to read.</summary>
        public static readonly SimReport Unknown = Of(null, null, null, null);

        /// <summary>See <see cref="Contract.CarHasPitLimiter"/>.</summary>
        public bool? CarHasPitLimiter { get; }

        /// <summary>See <see cref="Contract.PitLimiterOn"/>.</summary>
        public bool? PitLimiterOn { get; }

        /// <summary>See <see cref="Contract.DamageReported"/>.</summary>
        public bool? DamageReported { get; }

        /// <summary>See <see cref="Contract.DriverInCar"/>.</summary>
        public bool? DriverInCar { get; }

        private SimReport(bool? carHasPitLimiter, bool? pitLimiterOn, bool? damageReported, bool? driverInCar)
        {
            CarHasPitLimiter = carHasPitLimiter;
            PitLimiterOn = pitLimiterOn;
            DamageReported = damageReported;
            DriverInCar = driverInCar;
        }

        /// <summary>The one instance holding these four answers.</summary>
        public static SimReport Of(bool? carHasPitLimiter, bool? pitLimiterOn, bool? damageReported, bool? driverInCar)
        {
            return Interned[Index(carHasPitLimiter, pitLimiterOn, damageReported, driverInCar)];
        }

        /// <summary>One answer, by its <see cref="Contract.Capabilities"/> name; null for a name that is not one.</summary>
        public bool? Read(string capability)
        {
            switch (capability)
            {
                case Contract.CarHasPitLimiter: return CarHasPitLimiter;
                case Contract.PitLimiterOn: return PitLimiterOn;
                case Contract.DamageReported: return DamageReported;
                case Contract.DriverInCar: return DriverInCar;
                default: return null;
            }
        }

        public override string ToString()
        {
            return "limiter " + Word(CarHasPitLimiter) + ", on " + Word(PitLimiterOn)
                + ", damage " + Word(DamageReported) + ", in the car " + Word(DriverInCar);
        }

        private static string Word(bool? answer) => answer.HasValue ? (answer.Value ? "yes" : "no") : "unknown";

        private static int Digit(bool? answer) => answer.HasValue ? (answer.Value ? 2 : 1) : 0;

        private static int Index(bool? a, bool? b, bool? c, bool? d) => ((Digit(a) * 3 + Digit(b)) * 3 + Digit(c)) * 3 + Digit(d);

        private static SimReport[] Build()
        {
            var answers = new bool?[] { null, false, true };
            var all = new SimReport[81];
            foreach (var a in answers)
                foreach (var b in answers)
                    foreach (var c in answers)
                        foreach (var d in answers)
                            all[Index(a, b, c, d)] = new SimReport(a, b, c, d);
            return all;
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

        /// <summary>PSS_STOPPED in rFactor 2's and Le Mans Ultimate's mPitState: the car stopped at its box.</summary>
        public const int PitStateStopped = 3;

        /// <summary>The speed under which SimHub counts an opponent of Le Mans Ultimate as stopped in its box, in km/h.</summary>
        public const double StoppedKmh = 5.0;

        /// <summary>How one sim answers one question, given a way to read a property by its full name.</summary>
        public delegate bool? Answer(Func<string, object> property);

        /// <summary>How one sim answers each question. A null answer is a question the sim does not answer.</summary>
        public sealed class Row
        {
            public Answer CarHasPitLimiter { get; set; }
            public Answer PitLimiterOn { get; set; }
            public Answer DamageReported { get; set; }
            public Answer DriverInCar { get; set; }

            /// <summary>
            /// Whether the car stands in its pit box, which is what PitStopClock times. Left null where SimHub's
            /// own IsInPit is the box, so its timer stands; answered where IsInPit is something else. #1130.
            /// </summary>
            public Answer InPitBox { get; set; }
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
            },
            [AssettoCorsaCompetizione] = new Row
            {
                CarHasPitLimiter = Always(true),
                // ACCManager.GD_PitLimiterOn reads Physics.PitLimiterOn, so SimHub's own is right here.
                PitLimiterOn = Truth(GameData + "PitLimiterOn"),
                // ACCManager.GD_CarDamage1..5 read Physics.CarDamage.
                DamageReported = Always(true),
                DriverInCar = Is(GameRawData + "Graphics.Status", AcLive),
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
                // LMUManager.GD_IsInPit is mInPits, the whole lane, so SimHub's timer counts the lane. This is
                // the test SimHub itself puts to an opponent (IsCarInPit): in the garage, or in the lane with the
                // pit state stopped and under 5 km/h. #1130.
                InPitBox = property =>
                {
                    var garage = Truthy(property(GameRawData + "PlayerNativeScoring.mInGarageStall"));
                    var lane = Truthy(property(GameRawData + "PlayerNativeScoring.mInPits"));
                    var state = Number(property(GameRawData + "PlayerNativeScoring.mPitState"));
                    var speed = Real(property(GameData + "SpeedKmh"));
                    if (garage == true) return true;
                    if (!garage.HasValue || !lane.HasValue || !state.HasValue || !speed.HasValue) return null;
                    return lane.Value && state.Value == PitStateStopped && speed.Value < StoppedKmh;
                },
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
            return SimReport.Of(Ask(row.CarHasPitLimiter, property), Ask(row.PitLimiterOn, property),
                Ask(row.DamageReported, property), Ask(row.DriverInCar, property));
        }

        /// <summary>
        /// Whether the car stands in its pit box on this frame of <paramref name="gameName"/>, for
        /// <see cref="PitStopClock"/>: null for a game whose row leaves it to SimHub's own timer, or has no row,
        /// and wherever the answer cannot be read. Never throws.
        /// </summary>
        public static bool? InPitBox(string gameName, Func<string, object> property)
        {
            Row row;
            if (gameName == null || property == null || !Table.TryGetValue(gameName, out row)) return null;
            return Ask(row.InPitBox, property);
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
        public static double? Real(object value)
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
