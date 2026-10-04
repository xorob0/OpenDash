// SlipEstimateTests.cs: when the estimate says the wheels spin or lock, and when it keeps quiet.
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class SlipEstimateTests
    {
        /// <summary>Sixty frames a second, which is SimHub's default rate.</summary>
        private const long Frame = 16;

        private sealed class Drive
        {
            public readonly SlipEstimate Estimate = new SlipEstimate();
            public long Now = 1000;
            public double Speed = 120, Rpm = 6000, Throttle = 100, Brake, Clutch, Tc = 3;
            public string Gear = "4";

            public Drive Step(int frames = 1)
            {
                for (var i = 0; i < frames; i++)
                {
                    Estimate.Update(true, Speed, Rpm, Gear, Throttle, Brake, Clutch, Tc, Now);
                    Now += Frame;
                }
                return this;
            }
        }

        [Fact]
        public void A_car_accelerating_on_grip_keeps_its_ratio_and_lights_nothing()
        {
            var drive = new Drive().Step(40);
            for (var i = 0; i < 60; i++)
            {
                // Speed and RPM rise together: the ratio does not move.
                drive.Speed *= 1.004;
                drive.Rpm *= 1.004;
                drive.Step();
                Assert.False(drive.Estimate.Spin);
                Assert.False(drive.Estimate.Lock);
            }
        }

        [Fact]
        public void The_engine_running_away_from_the_road_under_full_throttle_is_a_spin()
        {
            var drive = new Drive().Step(40);
            drive.Rpm += 250;
            drive.Step();
            Assert.True(drive.Estimate.Spin);
            Assert.False(drive.Estimate.Lock);
            Assert.True(drive.Estimate.Slip > SlipEstimate.Threshold);
        }

        [Fact]
        public void A_spin_with_the_traction_control_on_is_traction_control_and_with_it_off_is_not()
        {
            var on = new Drive().Step(40);
            on.Rpm += 250;
            on.Step();
            Assert.True(on.Estimate.TcInferred);

            var off = new Drive { Tc = 0 }.Step(40);
            off.Rpm += 250;
            off.Step();
            Assert.True(off.Estimate.Spin);
            Assert.False(off.Estimate.TcInferred);
        }

        [Fact]
        public void The_engine_dropping_faster_than_the_car_under_heavy_braking_is_a_lock()
        {
            var drive = new Drive { Throttle = 0, Brake = 95 }.Step(40);
            drive.Rpm -= 400;
            drive.Speed -= 1;
            drive.Step();
            Assert.True(drive.Estimate.Lock);
            Assert.False(drive.Estimate.Spin);
            Assert.False(drive.Estimate.TcInferred);
        }

        [Fact]
        public void Part_throttle_and_light_braking_count_for_nothing_as_ShakeIt_weights_them()
        {
            // Past the gate but short of where ShakeIt starts to count: 70 % throttle and 55 % brake.
            var lift = new Drive { Throttle = 60 }.Step(40);
            lift.Rpm += 250;
            lift.Step();
            Assert.False(lift.Estimate.Spin);

            var trail = new Drive { Throttle = 0, Brake = 50 }.Step(40);
            trail.Rpm -= 400;
            trail.Step();
            Assert.False(trail.Estimate.Lock);
        }

        [Fact]
        public void A_gear_change_moves_the_ratio_by_itself_and_is_ignored_for_half_a_second()
        {
            var drive = new Drive().Step(40);
            drive.Gear = "5";
            drive.Rpm = 5000;
            drive.Step();
            Assert.False(drive.Estimate.Spin);

            // Still inside the quiet half second, a real jump is not counted either.
            drive.Rpm += 250;
            drive.Step();
            Assert.False(drive.Estimate.Spin);

            drive.Step((int)(SlipEstimate.GearChangeQuietMs / Frame));
            drive.Rpm += 250;
            drive.Step();
            Assert.True(drive.Estimate.Spin);
        }

        [Fact]
        public void Neutral_a_pressed_clutch_and_a_car_at_rest_light_nothing()
        {
            var neutral = new Drive { Gear = "N" }.Step(40);
            neutral.Rpm += 1000;
            neutral.Step();
            Assert.False(neutral.Estimate.Spin);

            var launch = new Drive { Clutch = 50 }.Step(40);
            launch.Rpm += 1000;
            launch.Step();
            Assert.False(launch.Estimate.Spin);

            var still = new Drive { Speed = 0.5 }.Step(40);
            still.Rpm += 1000;
            still.Step();
            Assert.False(still.Estimate.Spin);
        }

        [Fact]
        public void A_lamp_holds_through_a_spin_and_goes_out_a_moment_after_it()
        {
            var drive = new Drive().Step(40);
            drive.Rpm += 250;
            drive.Step();
            Assert.True(drive.Estimate.Spin);

            // Grip again: the ratio holds still, and the lamp holds for HoldMs and no longer.
            drive.Step((int)(SlipEstimate.HoldMs / Frame) - 1);
            Assert.True(drive.Estimate.Spin);
            drive.Step(2);
            Assert.False(drive.Estimate.Spin);
            Assert.False(drive.Estimate.TcInferred);
        }

        [Fact]
        public void A_game_that_stops_turns_everything_off_and_starts_again_from_nothing()
        {
            var drive = new Drive().Step(40);
            drive.Rpm += 250;
            drive.Step();
            Assert.True(drive.Estimate.Spin);

            drive.Estimate.Update(false, 0, 0, null, 0, 0, 0, 0, drive.Now);
            Assert.False(drive.Estimate.Spin);
            Assert.False(drive.Estimate.TcInferred);
            Assert.Equal(0, drive.Estimate.Slip);

            // The first frame back has nothing to compare with, however far it is from the last one.
            drive.Rpm = 9000;
            drive.Step();
            Assert.False(drive.Estimate.Spin);
        }

        [Fact]
        public void Offset_is_the_clamped_position_between_the_two()
        {
            Assert.Equal(0, SlipEstimate.Offset(40, 70, 100));
            Assert.Equal(0.5, SlipEstimate.Offset(85, 70, 100), 6);
            Assert.Equal(1, SlipEstimate.Offset(100, 70, 100));
            Assert.Equal(1, SlipEstimate.Offset(120, 70, 100));
        }
    }
}
