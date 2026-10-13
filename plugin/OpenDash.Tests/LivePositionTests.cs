// LivePositionTests.cs: the race as it stands on the road, which iRacing only gives at the line. #1063.
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class LivePositionTests
    {
        private static LiveCar Car(int position, double? progress, string carClass = "GT3", bool player = false, bool connected = true)
        {
            return new LiveCar { Position = position, Progress = progress, CarClass = carClass, IsPlayer = player, IsConnected = connected };
        }

        /// <summary>The rows from first place to last, as the dashboards' lists read them.</summary>
        private static int?[] Order(LiveRace race, int count) => race.Order.Take(count).ToArray();

        [Fact]
        public void A_pass_moves_the_place_and_the_order_before_the_line_does()
        {
            // SimHub's leaderboard is the sim's order from the last time each car crossed the line; on the road
            // the player, row 5, has just gone past the car on row 4.
            var field = new List<LiveCar>
            {
                Car(1, 4.80), Car(2, 4.70), Car(3, 4.60), Car(4, 4.40),
                Car(5, 4.45, player: true),
                Car(6, 4.30),
            };
            var race = LivePosition.Rank(field);
            Assert.Equal(new int?[] { 1, 2, 3, 5, 4, 6 }, Order(race, 6));
            Assert.Equal(new int?[] { 1, 2, 3, 5, 4, 6 }, race.Place.Take(6).ToArray());
            Assert.Null(race.Order[6]);
            Assert.Null(race.Place[6]);
        }

        [Fact]
        public void A_lapped_car_ahead_on_the_road_is_still_behind()
        {
            // Progress counts laps, so a car a lap down that the player is following on track is not ahead.
            var race = LivePosition.Rank(new List<LiveCar> { Car(1, 6.20, player: true), Car(2, 5.90), Car(3, 5.30) });
            Assert.Equal(new int?[] { 1, 2, 3 }, Order(race, 3));
        }

        [Fact]
        public void Each_class_is_counted_on_its_own_and_the_players_class_has_its_order()
        {
            var field = new List<LiveCar>
            {
                Car(1, 5.90, "LMP2"),
                Car(2, 5.50, "GT3"),
                Car(3, 5.40, "GT3"),
                Car(4, 5.45, "GT3", player: true),
                Car(5, 5.10, "LMP2"),
            };
            var race = LivePosition.Rank(field);
            Assert.Equal(new int?[] { 1, 2, 4, 3, 5 }, Order(race, 5));
            Assert.Equal(new int?[] { 1, 1, 3, 2, 2 }, race.ClassPlace.Take(5).ToArray());
            // The player's class, GT3, from its first car: rows 2, 4 and 3.
            Assert.Equal(new int?[] { 2, 4, 3, null }, race.ClassOrder.Take(4).ToArray());
        }

        [Fact]
        public void A_car_that_left_keeps_the_place_the_sim_gave_it()
        {
            // Its distance stops moving when it disconnects, so it is ranked as the sim ranks it rather than by a
            // distance that would let the field pass a car that is not there.
            var field = new List<LiveCar> { Car(1, 2.10, connected: false), Car(2, 4.30, player: true), Car(3, 4.10), Car(4, 4.40) };
            var race = LivePosition.Rank(field);
            Assert.Equal(new int?[] { 1, 4, 2, 3 }, Order(race, 4));
        }

        [Fact]
        public void The_order_is_a_true_order_where_the_road_and_the_sim_disagree_about_a_stopped_car()
        {
            // Row 5 is ahead of row 3 on the road, row 3 ahead of the stopped row 4 by the sim, and row 4 ahead
            // of row 5 by the sim: a comparison mixing the two goes round in a circle. Every car still gets one
            // place and every place one car.
            var field = new List<LiveCar> { Car(1, 9.0), Car(2, 8.0), Car(3, 7.0), Car(4, 1.0, connected: false), Car(5, 7.5) };
            var race = LivePosition.Rank(field);
            var order = Order(race, 5);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, order.Select(r => r.Value).OrderBy(r => r).ToArray());
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, race.Place.Take(5).Select(p => p.Value).OrderBy(p => p).ToArray());
            for (var place = 1; place <= 5; place++) Assert.Equal(place, race.Place[order[place - 1].Value - 1]);
        }

        [Fact]
        public void A_car_with_no_distance_keeps_the_sims_order_and_a_tie_does_too()
        {
            var field = new List<LiveCar> { Car(1, null), Car(2, 3.00, player: true), Car(3, 3.00), Car(4, 3.50) };
            var race = LivePosition.Rank(field);
            Assert.Equal(new int?[] { 1, 4, 2, 3 }, Order(race, 4));
        }

        [Fact]
        public void A_car_the_sim_has_not_placed_is_in_the_order_with_no_place()
        {
            var race = LivePosition.Rank(new List<LiveCar> { Car(1, 3.0), Car(0, 3.5), Car(2, 2.0, player: true) });
            Assert.Equal(new int?[] { 2, 1, 3 }, Order(race, 3));
            Assert.Null(race.Place[1]);
            Assert.Equal(2, race.Place[0]);
        }

        [Fact]
        public void Nothing_is_published_before_the_sim_has_placed_anybody()
        {
            // The grid reads `--` (#992), which is the dashboards' fallback; a live order here would undo it.
            Assert.Null(LivePosition.Rank(new List<LiveCar> { Car(0, -0.02), Car(0, -0.05, player: true) }));
            Assert.Null(LivePosition.Rank(new List<LiveCar>()));
            Assert.Null(LivePosition.Rank(null));
        }

        [Fact]
        public void A_field_without_the_player_has_no_class_order()
        {
            var race = LivePosition.Rank(new List<LiveCar> { Car(1, 2.0), Car(2, 1.0) });
            Assert.Equal(new int?[] { 1, 2 }, Order(race, 2));
            Assert.All(race.ClassOrder, row => Assert.Null(row));
        }

        [Fact]
        public void A_field_past_the_slots_is_published_as_far_as_they_go()
        {
            var field = Enumerable.Range(1, 70).Select(p => Car(p, 100.0 - p, player: p == 70)).ToList();
            var race = LivePosition.Rank(field);
            Assert.Equal(64, race.Order[63]);
            Assert.Equal(64, race.Place[63]);
            Assert.Null(race.Read(Contract.LiveOrder, 65));
            Assert.Null(race.Read(Contract.LiveOrder, 0));
            Assert.Equal(3, race.Read(Contract.LiveOrder, 3));
            Assert.Equal(3, race.Read(Contract.LivePosition, 3));
            Assert.Equal(3, race.Read(Contract.LiveClassPosition, 3));
            Assert.Equal(3, race.Read(Contract.LiveClassOrder, 3));
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
            var names = Contract.LivePropertyNames().ToList();
            Assert.Equal(4 * LivePosition.Slots, names.Count);
            Assert.Equal(new[] { "LivePosition1", "LivePosition64", "LiveClassPosition1", "LiveOrder1", "LiveClassOrder64" },
                new[] { names[0], names[63], names[64], names[128], names[255] });
            var shared = Contract.SharedPropertyNames().ToList();
            Assert.Equal(names, shared.Skip(shared.IndexOf("LivePosition1")).Take(names.Count));
        }
    }
}
