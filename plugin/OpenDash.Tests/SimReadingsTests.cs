// SimReadingsTests.cs: each sim's own track wetness, wind and time of day, mapped onto iRacing's scale. #1175.
using System;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class SimReadingsTests
    {
        /// <summary>The shape SimHub hands ACC's Graphics.trackGripStatus in: the reader's own enum, boxed.</summary>
        private enum AccGrip
        {
            ACC_GREEN,
            ACC_FAST,
            ACC_OPTIMUM,
            ACC_GREASY,
            ACC_DAMP,
            ACC_WET,
            ACC_FLOODED,
        }

        private const string Graphics = SimCapabilities.GameRawData + "Graphics.";
        private const string Lmu = SimCapabilities.LmuScoring;

        /// <summary>SimHub's properties for one frame, by full name; anything not listed is absent, as GetPropertyValue says.</summary>
        private static Func<string, object> Frame(params (string Name, object Value)[] properties)
        {
            var map = properties.ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);
            return name =>
            {
                object value;
                return map.TryGetValue(name, out value) ? value : null;
            };
        }

        private static double? Read(string game, string reading, Func<string, object> frame) => SimReadings.Read(game, reading, frame);

        [Fact]
        public void Every_reading_has_a_name_the_row_knows()
        {
            Assert.Equal(new[] { "TrackWetness", "WindSpeed", "WindDirection", "SimTimeOfDay" }, Contract.Readings);
            Assert.Equal(
                new[] { SimCapabilities.IRacing, SimCapabilities.AssettoCorsa, SimCapabilities.AssettoCorsaCompetizione, SimCapabilities.LeMansUltimate }.OrderBy(g => g),
                SimReadings.Games.OrderBy(g => g));
        }

        [Fact]
        public void On_iRacing_nothing_is_published_because_its_raw_properties_are_the_answer()
        {
            var frame = Frame(
                (SimCapabilities.GameRawData + "Telemetry.TrackWetness", 3),
                (SimCapabilities.GameRawData + "Telemetry.WindVel", 2.1f),
                (SimCapabilities.GameRawData + "Telemetry.SessionTimeOfDay", 51732.58));
            foreach (var reading in Contract.Readings) Assert.Null(Read(SimCapabilities.IRacing, reading, frame));
        }

        [Fact]
        public void On_Assetto_Corsa_any_surface_grip_is_a_dry_track_and_there_is_no_wind_or_clock()
        {
            // 0.98 is what the emulator's race scenario publishes; 0.86 the lowest of Kunos's own presets.
            Assert.Equal(SimReadings.Dry, Read(SimCapabilities.AssettoCorsa, Contract.TrackWetness, Frame((Graphics + "surfaceGrip", 0.98f))));
            Assert.Equal(SimReadings.Dry, Read(SimCapabilities.AssettoCorsa, Contract.TrackWetness, Frame((Graphics + "surfaceGrip", 0.86f))));
            Assert.Equal(SimReadings.Dry, Read(SimCapabilities.AssettoCorsa, Contract.TrackWetness, Frame((Graphics + "surfaceGrip", 1.0))));
            // 0 before the game has a session, and absent with no game: no reading, not a dry track.
            Assert.Null(Read(SimCapabilities.AssettoCorsa, Contract.TrackWetness, Frame((Graphics + "surfaceGrip", 0f))));
            Assert.Null(Read(SimCapabilities.AssettoCorsa, Contract.TrackWetness, Frame()));
            var full = Frame((Graphics + "surfaceGrip", 0.98f), (Graphics + "WindSpeed", 3f), (Graphics + "clock", 3600f));
            Assert.Null(Read(SimCapabilities.AssettoCorsa, Contract.WindSpeed, full));
            Assert.Null(Read(SimCapabilities.AssettoCorsa, Contract.WindDirection, full));
            Assert.Null(Read(SimCapabilities.AssettoCorsa, Contract.SimTimeOfDay, full));
        }

        [Fact]
        public void On_ACC_the_seven_grip_statuses_land_on_iRacings_seven_wetnesses()
        {
            var expected = new double[] { 1, 1, 1, 2, 3, 5, 7 };
            var statuses = Enum.GetValues(typeof(AccGrip)).Cast<AccGrip>().ToArray();
            Assert.Equal(expected.Length, statuses.Length);
            for (var i = 0; i < statuses.Length; i++)
            {
                // The enum boxed, its number, and its name with and without the prefix all read the same.
                Assert.Equal(expected[i], Read(SimCapabilities.AssettoCorsaCompetizione, Contract.TrackWetness, Frame((Graphics + "trackGripStatus", statuses[i]))));
                Assert.Equal(expected[i], Read(SimCapabilities.AssettoCorsaCompetizione, Contract.TrackWetness, Frame((Graphics + "trackGripStatus", i))));
                Assert.Equal(expected[i], Read(SimCapabilities.AssettoCorsaCompetizione, Contract.TrackWetness, Frame((Graphics + "trackGripStatus", statuses[i].ToString()))));
                Assert.Equal(expected[i], Read(SimCapabilities.AssettoCorsaCompetizione, Contract.TrackWetness, Frame((Graphics + "trackGripStatus", SimReadings.AccGripStatuses[i].ToLowerInvariant()))));
            }
            Assert.Null(Read(SimCapabilities.AssettoCorsaCompetizione, Contract.TrackWetness, Frame((Graphics + "trackGripStatus", 7))));
            Assert.Null(Read(SimCapabilities.AssettoCorsaCompetizione, Contract.TrackWetness, Frame((Graphics + "trackGripStatus", "ACC_SNOW"))));
            Assert.Null(Read(SimCapabilities.AssettoCorsaCompetizione, Contract.TrackWetness, Frame()));
        }

        [Fact]
        public void On_ACC_the_wind_and_the_clock_are_already_in_iRacings_units()
        {
            var frame = Frame((Graphics + "WindSpeed", 2.5f), (Graphics + "WindDirection", 1.25f), (Graphics + "clock", 51732.5f));
            Assert.Equal(2.5, Read(SimCapabilities.AssettoCorsaCompetizione, Contract.WindSpeed, frame));
            Assert.Equal(1.25, Read(SimCapabilities.AssettoCorsaCompetizione, Contract.WindDirection, frame));
            Assert.Equal(51732.5, Read(SimCapabilities.AssettoCorsaCompetizione, Contract.SimTimeOfDay, frame));
            Assert.Null(Read(SimCapabilities.AssettoCorsaCompetizione, Contract.WindSpeed, Frame()));
            Assert.Null(Read(SimCapabilities.AssettoCorsaCompetizione, Contract.SimTimeOfDay, Frame()));
        }

        [Theory]
        [InlineData(0.0, 1)]
        [InlineData(-0.1, 1)]
        [InlineData(0.01, 2)]
        [InlineData(1.0 / 6, 2)]
        [InlineData(0.3, 3)]
        [InlineData(0.5, 4)]
        [InlineData(0.7, 6)]
        [InlineData(0.9, 7)]
        [InlineData(1.0, 7)]
        [InlineData(1.5, 7)]
        public void On_LMU_the_path_wetness_is_spread_over_iRacings_six_wet_steps(double wetness, double expected)
        {
            Assert.Equal(expected, Read(SimCapabilities.LeMansUltimate, Contract.TrackWetness, Frame((Lmu + "mAvgPathWetness", wetness))));
        }

        [Fact]
        public void On_LMU_the_wind_is_the_horizontal_part_of_its_vector_and_the_clock_is_its_time_of_day()
        {
            var frame = Frame((Lmu + "mWind.x", 3.0), (Lmu + "mWind.y", 9.0), (Lmu + "mWind.z", 4.0), (Lmu + "mTimeOfDay", 30600f));
            Assert.Equal(5.0, Read(SimCapabilities.LeMansUltimate, Contract.WindSpeed, frame).Value, 9);
            Assert.Equal(Math.Atan2(3, 4), Read(SimCapabilities.LeMansUltimate, Contract.WindDirection, frame).Value, 9);
            Assert.Equal(30600.0, Read(SimCapabilities.LeMansUltimate, Contract.SimTimeOfDay, frame));
            // A wind blowing back along z and towards negative x is past half a turn, not a negative angle.
            var back = Read(SimCapabilities.LeMansUltimate, Contract.WindDirection, Frame((Lmu + "mWind.x", -1.0), (Lmu + "mWind.z", -1.0))).Value;
            Assert.Equal(1.25 * Math.PI, back, 9);
            // Half a vector is no reading.
            Assert.Null(Read(SimCapabilities.LeMansUltimate, Contract.WindSpeed, Frame((Lmu + "mWind.x", 3.0))));
            Assert.Null(Read(SimCapabilities.LeMansUltimate, Contract.WindDirection, Frame((Lmu + "mWind.z", 3.0))));
            Assert.Null(Read(SimCapabilities.LeMansUltimate, Contract.TrackWetness, Frame()));
        }

        [Fact]
        public void An_unknown_game_a_broken_property_and_a_reading_that_is_not_a_number_read_null()
        {
            var frame = Frame((Graphics + "surfaceGrip", 0.98f));
            Assert.Null(Read("RFactor2", Contract.TrackWetness, frame));
            Assert.Null(Read(null, Contract.TrackWetness, frame));
            Assert.Null(Read(SimCapabilities.AssettoCorsa, "NoSuchReading", frame));
            Assert.Null(Read(SimCapabilities.AssettoCorsa, Contract.TrackWetness, null));
            Assert.Null(Read(SimCapabilities.AssettoCorsa, Contract.TrackWetness, name => throw new InvalidOperationException()));
            Assert.Null(Read(SimCapabilities.AssettoCorsaCompetizione, Contract.WindSpeed, Frame((Graphics + "WindSpeed", float.NaN))));
            Assert.Null(Read(SimCapabilities.AssettoCorsaCompetizione, Contract.SimTimeOfDay, Frame((Graphics + "clock", double.PositiveInfinity))));
        }
    }
}
