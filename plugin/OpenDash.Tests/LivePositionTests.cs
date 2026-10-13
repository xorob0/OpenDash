// LivePositionTests.cs: the player's place as it stands on the road, which iRacing only gives at the line. #1063.
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class LivePositionTests
    {
        private static LiveCar Car(int position, double? progress, string carClass = "GT3", int classPosition = 0, bool player = false, bool connected = true)
        {
            return new LiveCar
            {
                Position = position,
                ClassPosition = classPosition == 0 ? position : classPosition,
                Progress = progress,
                CarClass = carClass,
                IsPlayer = player,
                IsConnected = connected,
            };
        }

        [Fact]
        public void A_pass_moves_the_place_before_the_line_does()
        {
            // The sim still has the player fifth from the last time they crossed the line; on the road they
            // have just gone past the car it calls fourth.
            var field = new List<LiveCar>
            {
                Car(1, 4.80), Car(2, 4.70), Car(3, 4.60), Car(4, 4.40),
                Car(5, 4.45, player: true),
                Car(6, 4.30),
            };
            Assert.Equal(4, LivePosition.Overall(field));
        }

        [Fact]
        public void Being_passed_moves_it_the_other_way()
        {
            var field = new List<LiveCar> { Car(1, 3.50, player: true), Car(2, 3.52), Car(3, 3.10) };
            Assert.Equal(2, LivePosition.Overall(field));
        }

        [Fact]
        public void A_lapped_car_ahead_on_the_road_is_still_behind()
        {
            // Progress counts laps, so a car a lap down that the player is following on track is not ahead.
            var field = new List<LiveCar> { Car(1, 6.20, player: true), Car(2, 5.90), Car(3, 5.30) };
            Assert.Equal(1, LivePosition.Overall(field));
        }

        [Fact]
        public void The_class_place_counts_only_the_players_class()
        {
            var field = new List<LiveCar>
            {
                Car(1, 5.90, "LMP2", 1),
                Car(2, 5.50, "GT3", 1),
                Car(3, 5.40, "GT3", 2),
                Car(4, 5.45, "GT3", 3, player: true),
                Car(5, 5.10, "LMP2", 2),
            };
            Assert.Equal(2, LivePosition.InClass(field));
            Assert.Equal(3, LivePosition.Overall(field));
        }

        [Fact]
        public void A_car_that_left_keeps_the_place_the_sim_gave_it()
        {
            // Its distance stops moving when it disconnects, so it is ranked as the sim ranks it rather than by
            // a distance that would let the player pass a car that is not there.
            var field = new List<LiveCar> { Car(1, 2.10, connected: false), Car(2, 4.30, player: true), Car(3, 4.10) };
            Assert.Equal(2, LivePosition.Overall(field));
            var behind = new List<LiveCar> { Car(1, 4.30, player: true), Car(2, 6.00, connected: false) };
            Assert.Equal(1, LivePosition.Overall(behind));
        }

        [Fact]
        public void A_car_with_no_distance_keeps_the_sims_order_and_a_tie_does_too()
        {
            var field = new List<LiveCar> { Car(1, null), Car(2, 3.00, player: true), Car(3, 3.00), Car(0, 3.50) };
            // Car 1 ahead by the sim, car 3 behind on the tie, and the unplaced car ahead on the road.
            Assert.Equal(3, LivePosition.Overall(field));
        }

        [Fact]
        public void Nothing_is_published_before_the_sim_has_placed_the_player()
        {
            // The grid reads `--` (#992), which is the dashboard's fallback; a live place here would undo it.
            var grid = new List<LiveCar> { Car(0, -0.02), Car(0, -0.05, player: true) };
            Assert.Null(LivePosition.Overall(grid));
            Assert.Null(LivePosition.InClass(grid));
        }

        [Fact]
        public void Nothing_is_published_without_the_player_or_their_distance()
        {
            Assert.Null(LivePosition.Overall(null));
            Assert.Null(LivePosition.Overall(new List<LiveCar>()));
            Assert.Null(LivePosition.Overall(new List<LiveCar> { Car(1, 2.0), Car(2, 1.0) }));
            Assert.Null(LivePosition.Overall(new List<LiveCar> { Car(1, 2.0), Car(2, null, player: true) }));
            Assert.Null(LivePosition.Overall(new List<LiveCar> { Car(1, 2.0), Car(2, 1.0, player: true, connected: false) }));
        }

        [Theory]
        [InlineData("IRacing", "Race", false, true)]
        [InlineData("IRacing", "race", false, true)]
        [InlineData("IRacing", "Race", true, false)]
        [InlineData("IRacing", "Practice", false, false)]
        [InlineData("IRacing", "Lone Qualify", false, false)]
        [InlineData("LMU", "Race", false, false)]
        [InlineData(null, null, false, false)]
        public void It_applies_to_an_iracing_race_until_the_chequered_flag(string game, string session, bool chequered, bool applies)
        {
            // A practice or qualifying place is a lap time, not a distance; after the flag cars stop racing; and
            // the other sims SimHub reads already move the place as it happens.
            Assert.Equal(applies, LivePosition.Applies(game, session, chequered));
        }

        [Fact]
        public void It_is_published_with_the_shared_group_and_not_offered_as_a_setting()
        {
            var shared = Contract.SharedPropertyNames().ToList();
            Assert.Contains(Contract.LivePosition, shared);
            Assert.Equal(shared.IndexOf(Contract.LivePosition) + 1, shared.IndexOf(Contract.LiveClassPosition));
            Assert.Equal("LivePosition", Contract.LivePosition);
            Assert.Equal("LiveClassPosition", Contract.LiveClassPosition);
        }
    }
}
