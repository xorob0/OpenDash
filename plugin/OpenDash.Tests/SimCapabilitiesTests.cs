// SimCapabilitiesTests.cs: what each sim answers to the questions a dashboard may not ask by the game's name. #1101.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class SimCapabilitiesTests
    {
        /// <summary>The shape SimHub hands Assetto Corsa's and ACC's Graphics.Status in: the reader's own enum, boxed.</summary>
        private enum AcStatus
        {
            AC_OFF,
            AC_REPLAY,
            AC_LIVE,
            AC_PAUSE,
        }

        private const string GameData = "DataCorePlugin.GameData.";
        private const string Raw = "DataCorePlugin.GameRawData.";

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

        [Fact]
        public void On_iRacing_the_limiter_is_the_cars_own_control_and_damage_is_not_reported()
        {
            // A car with the control, switched off: it has a limiter, and it is not on.
            var gt3 = SimCapabilities.Read(SimCapabilities.IRacing, Frame(
                (Raw + "Telemetry.dcPitSpeedLimiterToggle", false),
                (GameData + "PitLimiterOn", 0),
                (Raw + "Telemetry.IsOnTrack", true)));
            Assert.True(gt3.CarHasPitLimiter);
            Assert.False(gt3.PitLimiterOn);
            Assert.False(gt3.DamageReported);
            Assert.True(gt3.DriverInCar);

            // A car without the control has no limiter, and a driver in the garage is not in the car.
            var mx5 = SimCapabilities.Read(SimCapabilities.IRacing, Frame(
                (GameData + "PitLimiterOn", 0),
                (Raw + "Telemetry.IsOnTrack", false)));
            Assert.False(mx5.CarHasPitLimiter);
            Assert.False(mx5.DriverInCar);

            // The limiter bit, as SimHub's int.
            var engaged = SimCapabilities.Read(SimCapabilities.IRacing, Frame(
                (Raw + "Telemetry.dcPitSpeedLimiterToggle", true),
                (GameData + "PitLimiterOn", 1)));
            Assert.True(engaged.PitLimiterOn);
        }

        [Fact]
        public void On_iRacing_every_answer_is_the_one_the_dashboards_fall_back_on()
        {
            // The fallbacks in values.ts are iRacing's readings, so the plugin there says what a package with no
            // plugin already reads: the control's presence, the limiter bit, no damage, and IsOnTrack.
            var frame = Frame(
                (Raw + "Telemetry.dcPitSpeedLimiterToggle", false),
                (GameData + "PitLimiterOn", 1),
                (Raw + "Telemetry.IsOnTrack", true));
            Assert.Same(SimReport.Of(true, true, false, true), SimCapabilities.Read("IRacing", frame));
        }

        [Fact]
        public void On_Assetto_Corsa_every_car_has_a_limiter_and_its_state_is_the_physics_pages()
        {
            // SimHub's own PitLimiterOn is the pit lane on AC (ACManager.GD_PitLimiterOn returns GD_IsInPitLane): in
            // the lane with the limiter off it reads 1, and the plugin answers from Physics.PitLimiterOn instead.
            var inTheLane = SimCapabilities.Read(SimCapabilities.AssettoCorsa, Frame(
                (GameData + "PitLimiterOn", 1),
                (Raw + "Physics.PitLimiterOn", 0),
                (Raw + "Graphics.Status", AcStatus.AC_LIVE)));
            Assert.True(inTheLane.CarHasPitLimiter);
            Assert.False(inTheLane.PitLimiterOn);
            Assert.True(inTheLane.DamageReported);
            Assert.True(inTheLane.DriverInCar);

            var engaged = SimCapabilities.Read(SimCapabilities.AssettoCorsa, Frame(
                (GameData + "PitLimiterOn", 0),
                (Raw + "Physics.PitLimiterOn", 1)));
            Assert.True(engaged.PitLimiterOn);
            Assert.True(engaged.CarHasPitLimiter);
        }

        [Fact]
        public void On_Assetto_Corsa_and_ACC_only_a_live_session_is_the_driver_in_the_car()
        {
            foreach (var game in new[] { SimCapabilities.AssettoCorsa, SimCapabilities.AssettoCorsaCompetizione })
            {
                Assert.True(SimCapabilities.Read(game, Frame((Raw + "Graphics.Status", AcStatus.AC_LIVE))).DriverInCar);
                Assert.False(SimCapabilities.Read(game, Frame((Raw + "Graphics.Status", AcStatus.AC_PAUSE))).DriverInCar);
                Assert.False(SimCapabilities.Read(game, Frame((Raw + "Graphics.Status", AcStatus.AC_REPLAY))).DriverInCar);
                Assert.False(SimCapabilities.Read(game, Frame((Raw + "Graphics.Status", AcStatus.AC_OFF))).DriverInCar);
                // A number or the enum's value written out, should SimHub ever hand it on that way.
                Assert.True(SimCapabilities.Read(game, Frame((Raw + "Graphics.Status", 2))).DriverInCar);
                Assert.Null(SimCapabilities.Read(game, Frame()).DriverInCar);
            }
        }

        [Fact]
        public void On_ACC_SimHubs_limiter_is_the_limiter_and_damage_is_reported()
        {
            // ACCManager.GD_PitLimiterOn reads Physics.PitLimiterOn, so SimHub's member is right on ACC.
            var acc = SimCapabilities.Read(SimCapabilities.AssettoCorsaCompetizione, Frame(
                (GameData + "PitLimiterOn", 1),
                (Raw + "Graphics.Status", AcStatus.AC_LIVE)));
            Assert.True(acc.CarHasPitLimiter);
            Assert.True(acc.PitLimiterOn);
            Assert.True(acc.DamageReported);
            Assert.True(acc.DriverInCar);
        }

        [Fact]
        public void On_Le_Mans_Ultimate_the_car_says_whether_it_has_a_limiter_and_damage_is_not_reported()
        {
            var hypercar = SimCapabilities.Read(SimCapabilities.LeMansUltimate, Frame(
                (Raw + "PlayerNativeTelemetry.mSpeedLimiterAvailable", (byte)1),
                (GameData + "PitLimiterOn", 0),
                (Raw + "LMUNativeTelemetry.scoring.scoringInfo.mInRealtime", true)));
            Assert.True(hypercar.CarHasPitLimiter);
            Assert.False(hypercar.PitLimiterOn);
            Assert.False(hypercar.DamageReported);
            Assert.True(hypercar.DriverInCar);

            var none = SimCapabilities.Read(SimCapabilities.LeMansUltimate, Frame(
                (Raw + "PlayerNativeTelemetry.mSpeedLimiterAvailable", (byte)0),
                (Raw + "LMUNativeTelemetry.scoring.scoringInfo.mInRealtime", false)));
            Assert.False(none.CarHasPitLimiter);
            Assert.False(none.DriverInCar);

            // LMU's page not there yet: no answer, so the dashboards fall back rather than read "no limiter".
            Assert.Null(SimCapabilities.Read(SimCapabilities.LeMansUltimate, Frame()).CarHasPitLimiter);
        }

        [Fact]
        public void A_game_with_no_row_answers_nothing_so_the_dashboards_read_what_they_read_without_the_plugin()
        {
            var frame = Frame((GameData + "PitLimiterOn", 1), (Raw + "Telemetry.IsOnTrack", true));
            Assert.Same(SimReport.Unknown, SimCapabilities.Read("RFactor2", frame));
            Assert.Same(SimReport.Unknown, SimCapabilities.Read("", frame));
            Assert.Same(SimReport.Unknown, SimCapabilities.Read(null, frame));
            Assert.Same(SimReport.Unknown, SimCapabilities.Read(SimCapabilities.IRacing, null));
            foreach (var capability in Contract.Capabilities) Assert.Null(SimReport.Unknown.Read(capability));
        }

        [Fact]
        public void Every_claimed_sim_has_a_row_that_answers_every_question()
        {
            // 1.0 claims iRacing, Assetto Corsa, ACC and Le Mans Ultimate (#479). A row with a question left out
            // would publish null for it, which reads as iRacing's answer on a sim that is not iRacing.
            var claimed = new[] { "IRacing", "AssettoCorsa", "AssettoCorsaCompetizione", "LMU" };
            Assert.Equal(claimed.OrderBy(g => g), SimCapabilities.Games.OrderBy(g => g));
            var damage = claimed.ToDictionary(g => g, g => SimCapabilities.Read(g, Frame()).DamageReported);
            Assert.Equal(new bool?[] { false, true, true, false }, claimed.Select(g => damage[g]).ToArray());
        }

        [Fact]
        public void A_property_that_cannot_be_read_is_an_answer_not_given_and_never_an_exception()
        {
            Func<string, object> broken = name => throw new InvalidOperationException("SimHub could not evaluate " + name);
            var report = SimCapabilities.Read(SimCapabilities.AssettoCorsa, broken);
            // The constant answers stand; the two read from a property are unknown.
            Assert.True(report.CarHasPitLimiter);
            Assert.True(report.DamageReported);
            Assert.Null(report.PitLimiterOn);
            Assert.Null(report.DriverInCar);
        }

        [Fact]
        public void A_value_reads_as_true_or_false_whatever_shape_SimHub_hands_it_in()
        {
            Assert.True(SimCapabilities.Truthy(true));
            Assert.False(SimCapabilities.Truthy(false));
            Assert.True(SimCapabilities.Truthy(1));
            Assert.False(SimCapabilities.Truthy(0));
            Assert.True(SimCapabilities.Truthy((byte)1));
            Assert.True(SimCapabilities.Truthy(0.5f));
            Assert.False(SimCapabilities.Truthy(0.0));
            Assert.True(SimCapabilities.Truthy("True"));
            Assert.False(SimCapabilities.Truthy("0"));
            Assert.True(SimCapabilities.Truthy(AcStatus.AC_REPLAY));
            Assert.Null(SimCapabilities.Truthy(null));
            Assert.Null(SimCapabilities.Truthy("on"));
            Assert.Null(SimCapabilities.Truthy(new object()));

            Assert.Equal(2L, SimCapabilities.Number(AcStatus.AC_LIVE));
            Assert.Equal(2L, SimCapabilities.Number("2"));
            Assert.Equal(2L, SimCapabilities.Number(2.0f));
            Assert.Null(SimCapabilities.Number(true));
            Assert.Null(SimCapabilities.Number("AC_LIVE"));
        }

        [Fact]
        public void A_report_is_one_interned_instance_per_set_of_answers_and_reads_by_name()
        {
            var a = SimReport.Of(true, null, false, true);
            Assert.Same(a, SimReport.Of(true, null, false, true));
            Assert.NotSame(a, SimReport.Of(true, null, false, false));
            Assert.Equal(true, a.Read(Contract.CarHasPitLimiter));
            Assert.Null(a.Read(Contract.PitLimiterOn));
            Assert.Equal(false, a.Read(Contract.DamageReported));
            Assert.Equal(true, a.Read(Contract.DriverInCar));
            Assert.Null(a.Read("ShiftLights"));
            Assert.Equal("limiter yes, on unknown, damage no, in the car yes", a.ToString());
        }

        [Fact]
        public void They_are_published_with_the_shared_group_after_the_live_order()
        {
            // Appended to the shared group, which is pinned in order on both sides of the contract, and named for
            // the question a dashboard asks rather than for a game.
            Assert.Equal(new[] { "CarHasPitLimiter", "PitLimiterOn", "DamageReported", "DriverInCar" }, Contract.Capabilities);
            var shared = Contract.SharedPropertyNames().ToList();
            // Followed only by the pit stop as the plugin times it, which is a reading and not a question. #1130.
            Assert.Equal(Contract.Capabilities.Concat(Contract.PitStopTimes), shared.Skip(shared.Count - Contract.Capabilities.Length - Contract.PitStopTimes.Length));
            Assert.Equal(shared.IndexOf("LiveClassOrder" + LivePosition.Slots) + 1, shared.IndexOf(Contract.CarHasPitLimiter));
        }
    }
}
