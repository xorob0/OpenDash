// FaceSettingsTests.cs: the order a zone cycles in, which became the driver's with #503.
//
// The cycling tests in SettingsTests predate the order and run under the catalogue's own, which is the
// order every zone had before it could be chosen. The first tests here run the same presses with that
// order written out, so a zone nobody has reordered is held to exactly what it did before; the rest are
// what an order of the driver's own changes, and what it leaves alone.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class FaceSettingsTests
    {
        private static FaceSettings Fresh()
        {
            var face = new FaceSettings();
            face.Normalise();
            face.OpenOnStartPages();
            return face;
        }

        private static int[] Identity(string letter)
        {
            return Enumerable.Range(0, Contract.FaceZonePageCounts[Array.IndexOf(Contract.FaceZoneLetters, letter)]).ToArray();
        }

        [Fact]
        public void A_zone_opens_in_the_catalogue_order()
        {
            var face = Fresh();
            foreach (var letter in Contract.FaceZoneLetters) Assert.Equal(Identity(letter), face.Order(letter));
            // And a face read from a file that predates the order reads the same.
            var old = new FaceSettings { Orders = null };
            foreach (var letter in Contract.FaceZoneLetters) Assert.Equal(Identity(letter), old.Order(letter));
            old.Normalise();
            Assert.Equal(Contract.FaceZoneLetters.Length, old.Orders.Length);
        }

        [Fact]
        public void The_catalogue_order_cycles_exactly_as_every_zone_always_did()
        {
            // Cycling_steps_through_the_enabled_pages_and_wraps, with the order written out.
            var face = Fresh();
            face.SetOrder("A", Identity("A"));
            Assert.Equal(0, face.Zone("A"));
            Assert.Equal(new[] { 1, 2, 3, 0 }, new[] { face.Cycle("A"), face.Cycle("A"), face.Cycle("A"), face.Cycle("A") });

            // Cycling_skips_the_pages_the_mask_turns_off.
            face = Fresh();
            face.SetOrder("A", Identity("A"));
            face.SetPageEnabled("A", 1, false);
            face.SetPageEnabled("A", 2, false);
            face.OpenOnStartPages();
            Assert.Equal(3, face.Cycle("A"));
            Assert.Equal(0, face.Cycle("A"));

            // A_zone_with_one_page_left_stays_where_it_is.
            face = Fresh();
            for (var page = 1; page < 4; page++) face.SetPageEnabled("A", page, false);
            face.OpenOnStartPages();
            Assert.Equal(0, face.Cycle("A"));
            Assert.Equal(0, face.Cycle("A"));
            Assert.Equal(0, face.CycleBack("A"));

            // A_mask_of_any_length_cycles_through_exactly_the_pages_it_leaves_on, in catalogue order.
            var masks = new[] { Contract.DefaultZoneMask(1), 1 << 4, (1 << 0) | (1 << 4) | (1 << 14), (1 << 19) | (1 << 20), 0x155555, (1 << 0) | (1 << 20) };
            foreach (var mask in masks)
            {
                face = new FaceSettings { Masks = new[] { Contract.DefaultZoneMask(0), mask, Contract.DefaultZoneMask(2), Contract.DefaultZoneMask(3) } };
                face.Normalise();
                face.SetOrder("B", Identity("B"));
                face.OpenOnStartPages();
                var expected = Enumerable.Range(0, Modules.Count).Where(page => (mask & (1 << page)) != 0).ToList();
                var first = face.Zone("B");
                var seen = new List<int> { first };
                for (var press = 1; press < expected.Count; press++) seen.Add(face.Cycle("B"));
                // In order, from wherever it started, and back to the start.
                var start = expected.IndexOf(first);
                Assert.Equal(expected.Skip(start).Concat(expected.Take(start)), seen);
                Assert.Equal(first, face.Cycle("B"));
            }
        }

        [Fact]
        public void The_way_back_is_the_way_forward_reversed()
        {
            var face = Fresh();
            Assert.Equal(new[] { 3, 2, 1, 0 }, new[] { face.CycleBack("A"), face.CycleBack("A"), face.CycleBack("A"), face.CycleBack("A") });
            face.SetPageEnabled("A", 2, false);
            face.OpenOnStartPages();
            Assert.Equal(3, face.CycleBack("A"));
            Assert.Equal(1, face.CycleBack("A"));
            Assert.Equal(3, face.Cycle("A"));
        }

        [Fact]
        public void A_custom_order_cycles_forwards_and_back_through_it()
        {
            var face = Fresh();
            face.SetOrder("A", new[] { 2, 0, 3, 1 });
            Assert.Equal(new[] { 2, 0, 3, 1 }, face.Order("A"));
            // Reordering does not move what is on the screen.
            Assert.Equal(0, face.Zone("A"));
            Assert.Equal(3, face.Cycle("A"));
            Assert.Equal(1, face.Cycle("A"));
            Assert.Equal(2, face.Cycle("A"));
            Assert.Equal(0, face.Cycle("A"));
            Assert.Equal(2, face.CycleBack("A"));
            Assert.Equal(1, face.CycleBack("A"));

            // Past what is turned off, in the zone's order and not the catalogue's.
            face.SetPageEnabled("A", 3, false);
            face.SetStart("A", 0);
            Assert.Equal(1, face.Cycle("A"));
            Assert.Equal(0, face.CycleBack("A"));
            Assert.Equal(2, face.CycleBack("A"));
        }

        [Fact]
        public void A_stored_order_is_repaired_rather_than_refused()
        {
            // A page missing from the file is appended, which is what a catalogue that grows a page does
            // to every stored order; a repeated or unknown page is dropped.
            var face = new FaceSettings { Orders = new[] { new[] { 3, 1 }, new[] { 20, 20, 99, -1, 0 }, null, new[] { 7, 6, 5, 4, 3, 2, 1, 0 } } };
            face.Normalise();
            Assert.Equal(new[] { 3, 1, 0, 2 }, face.Order("A"));
            Assert.Equal(new[] { 20, 0 }.Concat(Enumerable.Range(1, 19)), face.Order("B"));
            Assert.Equal(Identity("C"), face.Order("C"));
            Assert.Equal(new[] { 7, 6, 5, 4, 3, 2, 1, 0 }, face.Order("D"));

            // Too few zones is the rest read in catalogue order.
            var short_ = new FaceSettings { Orders = new[] { new[] { 1, 0, 2, 3 } } };
            short_.Normalise();
            Assert.Equal(new[] { 1, 0, 2, 3 }, short_.Order("A"));
            Assert.Equal(Identity("D"), short_.Order("D"));

            // And SetOrder takes whatever the panel hands it and keeps a whole order.
            face.SetOrder("A", new[] { 2, 2, 9 });
            Assert.Equal(new[] { 2, 0, 1, 3 }, face.Order("A"));
            Assert.Throws<ArgumentOutOfRangeException>(() => face.SetOrder("E", new[] { 0 }));

            // A zone whose order is not whole is repaired before a press steps through it.
            face.Orders[0] = new[] { 1 };
            face.OpenOnStartPages();
            Assert.Equal(4, face.Orders[0].Length);

            // So is one of the right length that holds a page twice: [3, 3, 0, 2] would step 3, 3, 3
            // and never reach page 1. Repaired, it is 3, 0, 2 and then 1, both ways.
            face = Fresh();
            for (var page = 0; page < Contract.FaceZonePageCounts[0]; page++) face.SetPageEnabled("A", page, true);
            face.Orders[0] = new[] { 3, 3, 0, 2 };
            face.Zones[0] = 3;
            Assert.Equal(new[] { 0, 2, 1, 3 }, Enumerable.Range(0, 4).Select(_ => face.Cycle("A")).ToArray());
            Assert.Equal(new[] { 3, 0, 2, 1 }, face.Orders[0]);

            face.Orders[0] = new[] { 3, 3, 0, 2 };
            face.Zones[0] = 3;
            Assert.Equal(new[] { 1, 2, 0, 3 }, Enumerable.Range(0, 4).Select(_ => face.CycleBack("A")).ToArray());
        }

        [Fact]
        public void Order_hands_back_a_copy()
        {
            var face = Fresh();
            var order = face.Order("A");
            order[0] = 3;
            Assert.Equal(Identity("A"), face.Order("A"));
        }

        [Fact]
        public void Position_counts_the_enabled_pages_before_the_one_showing_plus_itself()
        {
            // Under the catalogue order it is what zoneCyclePosition in contract.ts has always derived
            // from the mask: one plus the enabled pages below the page.
            var face = Fresh();
            face.SetPageEnabled("B", 2, false);
            face.SetPageEnabled("B", 5, false);
            for (var page = 0; page < Modules.Count; page++)
            {
                face.Zones[1] = page;
                var below = Enumerable.Range(0, page).Count(p => (face.Mask("B") & (1 << p)) != 0);
                Assert.Equal(1 + below, face.Position("B"));
            }

            // Under an order of the driver's own it counts in that order.
            face = Fresh();
            face.SetOrder("A", new[] { 3, 1, 0, 2 });
            face.Zones[0] = 3;
            Assert.Equal(1, face.Position("A"));
            face.Zones[0] = 0;
            Assert.Equal(3, face.Position("A"));
            face.SetPageEnabled("A", 1, false);
            face.Zones[0] = 0;
            Assert.Equal(2, face.Position("A"));
            // A page the mask has turned off, which a held glance can show, counts itself after the
            // enabled pages before it.
            face.Zones[0] = 1;
            Assert.Equal(2, face.Position("A"));
            face.Zones[0] = 2;
            Assert.Equal(3, face.Position("A"));

            // An order a hand has broken -- a page twice, one missing -- is counted as Order() repairs
            // it, not as it lies: [3, 3, 0, 2] is 3, 0, 2 and then 1.
            face = Fresh();
            face.Orders[0] = new[] { 3, 3, 0, 2 };
            face.Zones[0] = 1;
            Assert.Equal(4, face.Position("A"));
        }

        [Fact]
        public void Position_costs_nothing_to_read_on_a_normalised_face()
        {
            // The zone header's "N / M" reads it for every zone of every face each dashboard frame.
            var face = Fresh();
            face.SetOrder("A", new[] { 3, 1, 0, 2 });
            face.SetPageEnabled("B", 2, false);
            foreach (var letter in Contract.FaceZoneLetters) face.Position(letter);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var frame = 0; frame < 100; frame++)
            {
                foreach (var letter in Contract.FaceZoneLetters) face.Position(letter);
            }
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Fact]
        public void Turning_off_the_page_showing_moves_on_in_the_zones_order()
        {
            var face = Fresh();
            face.SetOrder("A", new[] { 0, 3, 1, 2 });
            face.SetStart("A", 0);
            face.SetPageEnabled("A", 0, false);
            // Forward, which is where the button would have gone next: 3 in this order, not 1.
            Assert.Equal(3, face.Zone("A"));
            Assert.Equal(3, face.Start("A"));
        }

        [Fact]
        public void Normalise_lands_a_disabled_page_forward_in_the_zones_order()
        {
            var face = new FaceSettings
            {
                Orders = new[] { new[] { 2, 3, 0, 1 }, null, null, null },
                Masks = new[] { (1 << 0) | (1 << 1), Contract.DefaultZoneMask(1), Contract.DefaultZoneMask(2), Contract.DefaultZoneMask(3) },
                Starts = new[] { 2, 0, 0, 0 },
                Zones = new[] { 3, 0, 0, 0 },
            };
            face.Normalise();
            Assert.Equal(0, face.Start("A"));
            Assert.Equal(0, face.Zone("A"));
        }

        [Fact]
        public void The_orders_survive_a_clone_and_a_round_trip_through_JsonNET()
        {
            var face = Fresh();
            face.SetOrder("B", Enumerable.Reverse(Enumerable.Range(0, Modules.Count)).ToArray());
            face.SetOrder("D", new[] { 4, 5, 6, 7, 0, 1, 2, 3 });

            var clone = face.Clone();
            Assert.Equal(face.Order("B"), clone.Order("B"));
            clone.SetOrder("B", Identity("B"));
            Assert.NotEqual(face.Order("B"), clone.Order("B"));

            // SimHub reads and writes the settings with Json.NET, and the jagged array is the one shape
            // here it has not carried before.
            var json = JsonConvert.SerializeObject(face);
            var back = JsonConvert.DeserializeObject<FaceSettings>(json);
            back.Normalise();
            foreach (var letter in Contract.FaceZoneLetters) Assert.Equal(face.Order(letter), back.Order(letter));
            Assert.Equal(new[] { 4, 5, 6, 7, 0, 1, 2, 3 }, back.Orders[3]);
        }
    }
}
