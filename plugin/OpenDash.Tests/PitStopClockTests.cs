// PitStopClockTests.cs: the stop in the pit box is timed from the box, not from the pit lane, on a sim whose
// own timer counts the lane. #1130.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PitStopClockTests
    {
        private const string Raw = "DataCorePlugin.GameRawData.";
        private const string Player = Raw + "PlayerNativeScoring.";

        /// <summary>One Le Mans Ultimate frame of the player's own scoring, as SimHub publishes it: whether the car is
        /// in the pit lane, mInPits, and where its stop is, mPitState (0 none, 2 entering, 3 stopped, 4 exiting).</summary>
        private static Func<string, object> LmuFrame(bool inPits, byte pitState)
        {
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                [Player + "mInPits"] = inPits,
                [Player + "mPitState"] = pitState,
            };
            return name =>
            {
                object value;
                return map.TryGetValue(name, out value) ? value : null;
            };
        }

        /// <summary>A stretch of frames at SimHub's 60 Hz, each the same.</summary>
        private static IEnumerable<Func<string, object>> For(double seconds, bool inPits, byte pitState) =>
            Enumerable.Repeat(LmuFrame(inPits, pitState), (int)Math.Round(seconds * 60));

        /// <summary>
        /// A stop at Le Mans: 2 s on track, 25 s down the lane to the box, 30 s stopped in it, 12 s back up the lane
        /// and out. SimHub's LMU reader answers IsInPit with mInPits, so its timer would read the 67 s from the entry
        /// line to the exit line; the box is the 30 s in the middle.
        /// </summary>
        private static List<Func<string, object>> LmuStop() =>
            For(2, false, 0)
                .Concat(For(25, true, 2))
                .Concat(For(30, true, 3))
                .Concat(For(12, true, 4))
                .Concat(For(2, false, 0))
                .ToList();

        /// <summary>
        /// Plays the frames through the clock as the plugin does, one frame a sixtieth of a second after the last on
        /// its millisecond clock, and hands back what it published after each. <paramref name="first"/> is the index
        /// of the first frame, so that a clock played on is handed a time that goes on rising.
        /// </summary>
        private static List<(double? Seconds, double? Last, bool InPits)> Play(string game, IEnumerable<Func<string, object>> frames, PitStopClock clock = null, int first = 0)
        {
            clock = clock ?? new PitStopClock();
            var published = new List<(double? Seconds, double? Last, bool InPits)>();
            var i = first;
            foreach (var frame in frames)
            {
                long nowMs = (long)Math.Round(i++ * 1000.0 / 60);
                clock.Update(SimCapabilities.InPitBox(game, frame), nowMs / 1000.0);
                published.Add((clock.Seconds, clock.LastSeconds, (bool)frame(Player + "mInPits")));
            }
            return published;
        }

        [Fact]
        public void On_Le_Mans_Ultimate_the_stops_duration_is_the_time_in_the_box_and_not_the_lane()
        {
            var published = Play(SimCapabilities.LeMansUltimate, LmuStop());

            // Before the stop, and down the lane to the box: no stop under way and none timed yet.
            Assert.All(published.Take((2 + 25) * 60), p => Assert.Equal((double?)0.0, p.Seconds));
            Assert.All(published.Take((2 + 25) * 60), p => Assert.Equal((double?)0.0, p.Last));

            // In the box the stop counts from the first frame stopped, up to the 30 s it lasts.
            var box = published.Skip((2 + 25) * 60).Take(30 * 60).ToList();
            Assert.Equal(0.0, box.First().Seconds.Value, 3);
            Assert.Equal(30.0 - 1.0 / 60, box.Last().Seconds.Value, 2);
            Assert.True(box.Zip(box.Skip(1), (a, b) => b.Seconds > a.Seconds).All(rising => rising));

            // Up the lane and out, the stop is over and its duration is the 30 s in the box, not the 67 s in the lane.
            var after = published.Skip((2 + 25 + 30) * 60).ToList();
            Assert.All(after, p => Assert.Equal((double?)0.0, p.Seconds));
            Assert.All(after, p => Assert.Equal(30.0, p.Last.Value, 2));
            Assert.Contains(after, p => p.InPits);
            Assert.Contains(after, p => !p.InPits);
        }

        [Fact]
        public void The_last_stop_is_kept_through_the_next_until_that_one_ends()
        {
            // The pit view's progress is the stop under way over the last one, so the last must stand while the next
            // is timed, as SimHub's LastPitStopDuration does.
            var clock = new PitStopClock();
            var first = LmuStop();
            Play(SimCapabilities.LeMansUltimate, first, clock);
            var second = For(1, true, 2).Concat(For(10, true, 3)).Concat(For(1, true, 4)).ToList();
            var published = Play(SimCapabilities.LeMansUltimate, second, clock, first.Count);
            Assert.All(published.Take(60 + 10 * 60), p => Assert.Equal(30.0, p.Last.Value, 2));
            Assert.Equal(10.0, published.Last().Last.Value, 2);
        }

        [Fact]
        public void Where_SimHubs_own_timer_is_the_box_the_clock_publishes_nothing()
        {
            // iRacing's, Assetto Corsa's and ACC's IsInPit are the box, so their rows leave it to SimHub and the pit
            // view reads IsInPitSince and LastPitStopDuration, as it does with no plugin. The LMU frames' properties
            // are there too, to show they are not read.
            foreach (var game in new[] { SimCapabilities.IRacing, SimCapabilities.AssettoCorsa, SimCapabilities.AssettoCorsaCompetizione })
            {
                Assert.All(Play(game, LmuStop()), p =>
                {
                    Assert.Null(p.Seconds);
                    Assert.Null(p.Last);
                });
            }
            // Nor for a game with no row, nor with no game at all.
            Assert.Null(SimCapabilities.InPitBox("RFactor2", LmuFrame(true, 3)));
            Assert.Null(SimCapabilities.InPitBox(null, LmuFrame(true, 3)));
            Assert.Null(SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, null));
        }

        [Fact]
        public void A_frame_with_no_answer_forgets_every_stop()
        {
            // A game that stops, or a page not there yet, leaves nothing to time: the dashboards fall back on SimHub's
            // own timer rather than read a stop from another session.
            var clock = new PitStopClock();
            Play(SimCapabilities.LeMansUltimate, LmuStop(), clock);
            Assert.Equal(30.0, clock.LastSeconds.Value, 2);
            Func<string, object> empty = name => null;
            Assert.Null(SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, empty));
            clock.Update(SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, empty), 1000);
            Assert.Null(clock.Seconds);
            Assert.Null(clock.LastSeconds);
            // And the next answer starts from nothing timed.
            clock.Update(false, 1001);
            Assert.Equal((double?)0.0, clock.Seconds);
            Assert.Equal((double?)0.0, clock.LastSeconds);
        }

        [Fact]
        public void A_property_that_cannot_be_read_is_no_answer_and_never_an_exception()
        {
            Func<string, object> broken = name => throw new InvalidOperationException("SimHub could not evaluate " + name);
            Assert.Null(SimCapabilities.InPitBox(SimCapabilities.LeMansUltimate, broken));
        }

        [Fact]
        public void They_are_published_with_the_shared_group_after_the_track_map()
        {
            var shared = Contract.SharedPropertyNames().ToList();
            Assert.Equal("PitStopSeconds", Contract.PitStopSeconds);
            Assert.Equal("LastPitStopSeconds", Contract.LastPitStopSeconds);
            Assert.Equal(shared.IndexOf(Contract.TrackMapRecorded) + 1, shared.IndexOf(Contract.PitStopSeconds));
            // Last of the group until the sim readings followed it. #1175.
            Assert.Equal(shared.Count - 1 - Contract.Readings.Length, shared.IndexOf(Contract.LastPitStopSeconds));
            Assert.Equal(shared.IndexOf(Contract.PitStopSeconds) + 1, shared.IndexOf(Contract.LastPitStopSeconds));
        }
    }
}
