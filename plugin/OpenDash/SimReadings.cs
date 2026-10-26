// SimReadings.cs: the readings every sim publishes in a unit and on a scale of its own, mapped onto iRacing's. #1175.
//
// The track state, the wind and the sim's clock were written against iRacing's raw TrackWetness, WindVel, WindDir and
// SessionTimeOfDay, which no other reader publishes (#1123, #1128). Assetto Corsa, ACC and Le Mans Ultimate each
// publish a reading of their own under another name, in another unit and on another scale (docs/research/sims.md).
// Under ADR 0021 a dashboard never names a game, so the plugin answers instead: it reads the running sim's own
// reading from the table below, maps it onto iRacing's unit and scale, and publishes it as one OpenDash property
// (Contract.Readings), which every dashboard reads with iRacing's raw property as its isnull() fallback. iRacing's
// row is empty, its raw property being the answer; a game with no row gets null for every reading, which is also
// what a dashboard reads with no plugin.
//
// Pure: no SimHub types, so the net8.0 tests compile it. The plugin hands it the running game's code and a way to
// read one of SimHub's properties by name, PluginManager.GetPropertyValue, as SimCapabilities.cs is handed.
using System;
using System.Collections.Generic;
using System.Globalization;
using static OpenDashPlugin.SimCapabilities;

namespace OpenDashPlugin
{
    /// <summary>The table: how each sim SimHub reads gives each reading a dashboard draws, on iRacing's scale.</summary>
    public static class SimReadings
    {
        /// <summary>iRacing's irsdk_TrackWetness for a dry track, the driest of its seven.</summary>
        public const int Dry = 1;

        /// <summary>iRacing's irsdk_TrackWetness for an extremely wet track, the wettest of its seven.</summary>
        public const int ExtremelyWet = 7;

        /// <summary>How one sim gives one reading, given a way to read a property by its full name; null where it has none.</summary>
        public delegate double? Reading(Func<string, object> property);

        /// <summary>How one sim gives each reading. A null reading is one the sim does not publish.</summary>
        public sealed class Row
        {
            /// <summary>See <see cref="Contract.TrackWetness"/>: 1 dry to 7 extremely wet.</summary>
            public Reading TrackWetness { get; set; }

            /// <summary>See <see cref="Contract.WindSpeed"/>: metres per second.</summary>
            public Reading WindSpeed { get; set; }

            /// <summary>See <see cref="Contract.WindDirection"/>: radians.</summary>
            public Reading WindDirection { get; set; }

            /// <summary>See <see cref="Contract.SimTimeOfDay"/>: seconds since midnight.</summary>
            public Reading SimTimeOfDay { get; set; }

            /// <summary>One reading by its <see cref="Contract.Readings"/> name.</summary>
            public Reading For(string reading)
            {
                switch (reading)
                {
                    case Contract.TrackWetness: return TrackWetness;
                    case Contract.WindSpeed: return WindSpeed;
                    case Contract.WindDirection: return WindDirection;
                    case Contract.SimTimeOfDay: return SimTimeOfDay;
                    default: return null;
                }
            }
        }

        /// <summary>A property read as the number it is, already in iRacing's unit; null while it is absent.</summary>
        public static Reading Plain(string name) => property => Real(property(name));

        /// <summary>
        /// Assetto Corsa's Graphics.surfaceGrip as a wetness. The game has no rain: surfaceGrip is the grip of a dry
        /// track as a fraction of its full grip, which a server starts at its session preset (0.86 to 1.00 among
        /// Kunos's own) and which climbs towards 1 as the cars lay rubber down. Every value of it is a dry track, so
        /// any positive grip reads <see cref="Dry"/>. It is 0 before the game has a session, which is no reading.
        /// docs/research/sims.md records the range.
        /// </summary>
        public static double? AcWetness(Func<string, object> property)
        {
            var grip = Real(property(GameRawData + "Graphics.surfaceGrip"));
            return grip.HasValue && grip.Value > 0 ? Dry : (double?)null;
        }

        /// <summary>ACC_TRACK_GRIP_STATUS's members without their ACC_ prefix, in the order of their values.</summary>
        public static readonly string[] AccGripStatuses = { "GREEN", "FAST", "OPTIMUM", "GREASY", "DAMP", "WET", "FLOODED" };

        /// <summary>The wetness each of <see cref="AccGripStatuses"/> reads as, on iRacing's scale.</summary>
        private static readonly double[] AccWetnesses = { Dry, Dry, Dry, 2, 3, 5, ExtremelyWet };

        /// <summary>
        /// ACC's Graphics.trackGripStatus, ACC_TRACK_GRIP_STATUS, as a wetness. SimHub publishes the enum, which may reach
        /// the plugin as its number or by its name. Green, fast and optimum are a dry track at three stages of rubber and
        /// read dry; greasy is mostly dry; damp is very lightly wet; wet is moderately wet; flooded is extremely wet. So
        /// ACC's seven land on iRacing's seven as 1, 1, 1, 2, 3, 5 and 7.
        /// </summary>
        public static double? AccWetness(Func<string, object> property)
        {
            var value = property(GameRawData + "Graphics.trackGripStatus");
            if (value == null || value is bool) return null;
            long status;
            var name = value is Enum ? null : value as string;
            if (name != null && !double.TryParse(name.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                var trimmed = name.Trim();
                if (trimmed.StartsWith("ACC_", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed.Substring(4);
                status = Array.FindIndex(AccGripStatuses, s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                var number = Number(value);
                if (!number.HasValue) return null;
                status = number.Value;
            }
            return status >= 0 && status < AccWetnesses.Length ? AccWetnesses[status] : (double?)null;
        }

        /// <summary>
        /// Le Mans Ultimate's scoringInfo.mAvgPathWetness, rFactor 2's average wetness of the racing line from 0.0 dry to
        /// 1.0 soaked, as a wetness: 0 is dry, and the rest is spread evenly over iRacing's six wet steps, so that any
        /// water at all on the line reads mostly dry and a soaked one extremely wet. mTrackGripLevel is not read: it is a
        /// byte the reader leaves undescribed, and nothing says it is the water rather than the rubber.
        /// </summary>
        public static double? LmuWetness(Func<string, object> property)
        {
            var wetness = Real(property(LmuScoring + "mAvgPathWetness"));
            if (!wetness.HasValue || double.IsNaN(wetness.Value)) return null;
            var w = Math.Max(0, Math.Min(1, wetness.Value));
            return w <= 0 ? Dry : Math.Min(ExtremelyWet, Dry + Math.Ceiling(w * (ExtremelyWet - Dry)));
        }

        /// <summary>
        /// Le Mans Ultimate's scoringInfo.mWind, rFactor 2's wind as a vector in metres per second in the world's axes,
        /// y up: its speed over the ground, from the horizontal x and z. Null while either is absent.
        /// </summary>
        public static double? LmuWindSpeed(Func<string, object> property)
        {
            var x = Real(property(LmuScoring + "mWind.x"));
            var z = Real(property(LmuScoring + "mWind.z"));
            return x.HasValue && z.HasValue ? Math.Sqrt(x.Value * x.Value + z.Value * z.Value) : (double?)null;
        }

        /// <summary>
        /// The direction of <see cref="LmuWindSpeed"/>'s vector in radians, 0 up to 2π, from the world's z axis towards its
        /// x axis. rFactor 2's world has no north, so this is the track's own frame and not a compass bearing.
        /// </summary>
        public static double? LmuWindDirection(Func<string, object> property)
        {
            var x = Real(property(LmuScoring + "mWind.x"));
            var z = Real(property(LmuScoring + "mWind.z"));
            if (!x.HasValue || !z.HasValue) return null;
            var angle = Math.Atan2(x.Value, z.Value);
            return angle < 0 ? angle + 2 * Math.PI : angle;
        }

        /// <summary>
        /// One row per game, by SimHub's code. Each reading cites the raw member docs/research/sims.md read it from;
        /// adding a game is adding a row here and its test, and nothing in a dashboard.
        /// </summary>
        private static readonly Dictionary<string, Row> Table = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase)
        {
            // iRacing's raw TrackWetness, WindVel, WindDir and SessionTimeOfDay are every dashboard's fallback, so there
            // is nothing for the plugin to publish.
            [IRacing] = new Row(),
            [AssettoCorsa] = new Row
            {
                // Graphics.surfaceGrip: a dry track's grip. The game publishes no wind and no time of day.
                TrackWetness = AcWetness,
            },
            [AssettoCorsaCompetizione] = new Row
            {
                TrackWetness = AccWetness,
                // Graphics.WindSpeed in metres per second and Graphics.WindDirection in radians, as Kunos documents
                // them: iRacing's units already.
                WindSpeed = Plain(GameRawData + "Graphics.WindSpeed"),
                WindDirection = Plain(GameRawData + "Graphics.WindDirection"),
                // Graphics.clock, the session's time of day in seconds.
                SimTimeOfDay = Plain(GameRawData + "Graphics.clock"),
            },
            [LeMansUltimate] = new Row
            {
                TrackWetness = LmuWetness,
                WindSpeed = LmuWindSpeed,
                WindDirection = LmuWindDirection,
                // scoringInfo.mTimeOfDay, the session's time of day in seconds.
                SimTimeOfDay = Plain(LmuScoring + "mTimeOfDay"),
            },
        };

        /// <summary>The codes of every game the table has a row for.</summary>
        public static IEnumerable<string> Games => Table.Keys;

        /// <summary>
        /// One reading for one frame of <paramref name="gameName"/>, by its <see cref="Contract.Readings"/> name: null for a
        /// game with no row, a reading the row does not give, and a property that cannot be read. Never throws.
        /// </summary>
        /// <param name="gameName">SimHub's code for the running game, GameData.GameName.</param>
        /// <param name="reading">One of <see cref="Contract.Readings"/>.</param>
        /// <param name="property">Reads one of SimHub's properties by its full name; null when there is no such property.</param>
        public static double? Read(string gameName, string reading, Func<string, object> property)
        {
            Row row;
            if (gameName == null || property == null || !Table.TryGetValue(gameName, out row)) return null;
            var read = row.For(reading);
            if (read == null) return null;
            try
            {
                var value = read(property);
                return value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) ? value : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
