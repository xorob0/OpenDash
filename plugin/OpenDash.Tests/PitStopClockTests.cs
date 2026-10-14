// PitStopClockTests.cs: the stop timed in the pit box rather than down the whole lane on Le Mans Ultimate. #1130.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PitStopClockTests
    {
        private const string GameData = "DataCorePlugin.GameData.";
        private const string Raw = "DataCorePlugin.GameRawData.";

        private static readonly DateTime Start = new DateTime(2026, 10, 14, 20, 0, 0, DateTimeKind.Utc);

        /// <summary>One frame of Le Mans Ultimate's player scoring, as SimHub publishes it; a null leaves the property out.</summary>
        private static Func<string, object> Lmu(bool? garage, bool? lane, byte? pitState, double? kmh)
        {
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (garage.HasValue) map[Raw + "PlayerNativeScoring.mInGarageStall"] = garage.Value;
            if (lane.HasValue) map[Raw + "PlayerNativeScoring.mInPits"] = lane.Value;
            if (pitState.HasValue) map[Raw + "PlayerNativeScoring.mPitState"] = pitState.Value;
            if (kmh.HasValue) map[GameData + "SpeedKmh"] = kmh.Value;
            return name =>
            {
                object value;
                return map.TryGetValue(name, out value) ? value : null;
            };
        }

        /// <summary>
        /// A stop at Le Mans as the frames arrive, one a second: down the lane for 25 s, 30 s stopped in the box,
        /// 10 s back out to the exit line. Each step is the frame's answers and how many seconds it lasts.
        /// </summary>
        private static readonly (Func<string, object> Frame, int Seconds, string What)[] Stop =
        {
            (Lmu(false, false, 0, 230), 5, "on track"),
            (Lmu(false, true, 1, 60), 25, "down the lane to the box, the stop requested"),
            (Lmu(false, true, 2, 4), 1, "arriving at the box, slow but not yet stopped"),
            (Lmu(false, true, 3, 0), 30, "stopped in the box"),
            (Lmu(false, true, 4, 20), 10, "leaving it, back up the lane"),
            (Lmu(false, false, 0, 120), 5, "past the exit line"),
        };

        [Fact]
        public void On_Le_Mans_Ultimate_the_stop_is_the_time_in_the_box_and_not_the_time_in_the_lane()
        {
            // SimHub's own timer runs on mInPits, which is the lane, so it reads this stop as 65 s. The clock reads 29, the box alone.
            var clock = new PitStopClock();
            var now = Start;
            var inBox = new List<double>();
            foreach (var step in Stop)
            {
                for (var s = 0; s < step.Seconds; s++)
                {
                    clock.Update(SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, step.Frame), now);
                    if (step.What == "stopped in the box") inBox.Add(clock.PitStopSeconds.Value);
                    else Assert.Equal(0.0, clock.PitStopSeconds);
                    now = now.AddSeconds(1);
                }
            }
            Assert.Equal(Enumerable.Range(0, 30).Select(s => (double)s), inBox);
            // The last in-box frame, taken as the car leaves, as SimHub takes its own: the frame rate's grain.
            Assert.Equal(29.0, clock.LastPitStopSeconds);
            Assert.Equal(0.0, clock.PitStopSeconds);
        }

        [Fact]
        public void The_last_stop_is_kept_until_the_next_and_zero_before_the_first()
        {
            var clock = new PitStopClock();
            var lane = SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, Lmu(false, true, 1, 60));
            var box = SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, Lmu(false, true, 3, 0));
            Assert.False(lane);
            Assert.True(box);

            clock.Update(lane, Start);
            Assert.Equal(0.0, clock.LastPitStopSeconds);
            clock.Update(box, Start.AddSeconds(1));
            clock.Update(box, Start.AddSeconds(23.5));
            Assert.Equal(22.5, clock.PitStopSeconds);
            clock.Update(lane, Start.AddSeconds(24));
            Assert.Equal(22.5, clock.LastPitStopSeconds);
            Assert.Equal(0.0, clock.PitStopSeconds);

            // A lap later, still 22.5 until the car stops again, and the second stop replaces it as it ends.
            clock.Update(false, Start.AddSeconds(120));
            Assert.Equal(22.5, clock.LastPitStopSeconds);
            clock.Update(box, Start.AddSeconds(200));
            clock.Update(box, Start.AddSeconds(208));
            Assert.Equal(22.5, clock.LastPitStopSeconds);
            clock.Update(lane, Start.AddSeconds(209));
            Assert.Equal(8.0, clock.LastPitStopSeconds);
        }

        [Fact]
        public void The_garage_is_the_box_as_SimHub_says_of_an_opponent()
        {
            Assert.True(SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, Lmu(true, true, 0, 0)));
            Assert.True(SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, Lmu(true, null, null, null)));
            // In the lane at walking pace but not at the box: queueing behind a team-mate is not a stop.
            Assert.False(SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, Lmu(false, true, 2, 0)));
            // Stopped at the box with the state stale from a frame ago but still rolling: not yet.
            Assert.False(SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, Lmu(false, true, 3, 5)));
            // The pit state left at "stopped" out on the track is not the box.
            Assert.False(SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, Lmu(false, false, 3, 0)));
        }

        [Fact]
        public void Where_SimHubs_own_timer_is_the_box_the_plugin_publishes_nothing_and_the_dashboards_read_it()
        {
            // iRacing's, Assetto Corsa's and ACC's IsInPit is the box. Whatever the frame holds, their rows do not
            // answer, so both readings are null and the dashboards read IsInPitSince and LastPitStopDuration.
            var everything = Lmu(true, true, 3, 0);
            foreach (var game in new[] { SimCapabilities.IRacing, SimCapabilities.AssettoCorsa, SimCapabilities.AssettoCorsaCompetizione, "RFactor2", "", null })
            {
                Assert.Null(SimCapabilities.InPitBox(game, everything));
                var clock = new PitStopClock();
                clock.Update(SimCapabilities.InPitBox(game, everything), Start);
                Assert.Null(clock.PitStopSeconds);
                Assert.Null(clock.LastPitStopSeconds);
            }
            Assert.Null(SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, null));
        }

        [Fact]
        public void A_frame_LMU_has_not_filled_is_no_answer_and_keeps_the_stop()
        {
            // The scoring page missing, or a property that throws: null, which publishes null for the frame,
            // which the dashboards answer with SimHub's own members. Never an exception on the data thread.
            Assert.Null(SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, Lmu(null, null, null, null)));
            Assert.Null(SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, Lmu(false, true, 3, null)));
            Assert.Null(SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, name => throw new InvalidOperationException(name)));

            var clock = new PitStopClock();
            var box = SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, Lmu(false, true, 3, 0));
            clock.Update(box, Start);
            clock.Update(box, Start.AddSeconds(10));
            clock.Update(false, Start.AddSeconds(11));
            Assert.Equal(10.0, clock.LastPitStopSeconds);
            clock.Update(null, Start.AddSeconds(12));
            Assert.Null(clock.PitStopSeconds);
            Assert.Null(clock.LastPitStopSeconds);
            // One frame with a reading missing says nothing about the stop: the last one is still there after it,
            clock.Update(false, Start.AddSeconds(13));
            Assert.Equal(10.0, clock.LastPitStopSeconds);
            Assert.Equal(0.0, clock.PitStopSeconds);

            // and a stop with such a frame in the middle of it is timed whole, from the frame the car stopped.
            clock.Update(box, Start.AddSeconds(100));
            clock.Update(null, Start.AddSeconds(104));
            clock.Update(box, Start.AddSeconds(105));
            Assert.Equal(5.0, clock.PitStopSeconds);
            clock.Update(false, Start.AddSeconds(106));
            Assert.Equal(5.0, clock.LastPitStopSeconds);
        }

        [Fact]
        public void A_session_restart_forgets_the_last_stop_as_SimHub_forgets_its_own()
        {
            // SimHub's RaiseSessionRestart zeroes LastPitStopDuration, so a stop from the session before is not
            // carried into the next one's Pit time, nor into the service gauge, which divides by it.
            var clock = new PitStopClock();
            clock.Update(true, Start);
            clock.Update(true, Start.AddSeconds(20));
            clock.Update(false, Start.AddSeconds(21));
            Assert.Equal(20.0, clock.LastPitStopSeconds);

            // SimHub's SessionRestart event marks it, between two frames; the next frame carries it out.
            clock.Restart();
            Assert.Equal(20.0, clock.LastPitStopSeconds);
            clock.Update(false, Start.AddSeconds(60));
            Assert.Equal(0.0, clock.LastPitStopSeconds);
            Assert.Equal(0.0, clock.PitStopSeconds);
            // Once: the stop after it is kept as any other.
            clock.Update(true, Start.AddSeconds(70));
            clock.Update(true, Start.AddSeconds(78));
            clock.Update(false, Start.AddSeconds(79));
            Assert.Equal(8.0, clock.LastPitStopSeconds);
        }

        [Fact]
        public void A_session_restart_marked_on_a_frame_with_a_reading_missing_is_carried_out_all_the_same()
        {
            var clock = new PitStopClock();
            clock.Update(true, Start);
            clock.Update(true, Start.AddSeconds(20));
            clock.Update(false, Start.AddSeconds(21));

            clock.Restart();
            clock.Update(null, Start.AddSeconds(22));
            Assert.Null(clock.LastPitStopSeconds);
            clock.Update(false, Start.AddSeconds(23));
            Assert.Equal(0.0, clock.LastPitStopSeconds);
        }

        [Fact]
        public void A_session_restart_with_the_car_in_its_box_counts_from_the_restart()
        {
            // A stop begun in the session before is none of this one's: a car restarted in its garage stall
            // counts from the restart, and the stop that ends there is that long.
            var clock = new PitStopClock();
            clock.Update(true, Start);
            clock.Update(true, Start.AddSeconds(40));
            Assert.Equal(40.0, clock.PitStopSeconds);

            clock.Restart();
            clock.Update(true, Start.AddSeconds(41));
            Assert.Equal(0.0, clock.PitStopSeconds);
            Assert.Equal(0.0, clock.LastPitStopSeconds);
            clock.Update(true, Start.AddSeconds(50));
            Assert.Equal(9.0, clock.PitStopSeconds);
            clock.Update(false, Start.AddSeconds(51));
            Assert.Equal(9.0, clock.LastPitStopSeconds);
        }

        [Fact]
        public void No_game_forgets_everything_and_publishes_nothing()
        {
            var clock = new PitStopClock();
            clock.Update(true, Start);
            clock.Update(true, Start.AddSeconds(10));
            clock.Update(false, Start.AddSeconds(11));
            clock.Reset();
            Assert.Null(clock.PitStopSeconds);
            Assert.Null(clock.LastPitStopSeconds);
            // The game back: timing again from nothing, as SimHub's own timer starts from nothing in a new session.
            clock.Update(false, Start.AddSeconds(13));
            Assert.Equal(0.0, clock.LastPitStopSeconds);
        }

        [Fact]
        public void A_frame_stamped_before_the_stop_began_never_reads_a_negative_stop()
        {
            var clock = new PitStopClock();
            clock.Update(true, Start);
            clock.Update(true, Start.AddSeconds(-1));
            Assert.Equal(0.0, clock.PitStopSeconds);
        }
    }
}
