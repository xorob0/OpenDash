// HeldBackBandPagesTests.cs: band D's car page, the telltale rank, is held back from 1.0 (#969).
// Twelve outlined boxes with no pictogram read as a broken page, so no cycle, start or glance may
// land on it and the panel offers it nowhere; the page keeps its number, so a saved page of a driver
// who never chose it still means the page it meant.
//
// The other half is packages/dash/test/heldBack.test.ts, which holds every built band to drawing fuel
// in the car page's place, and ContractTests holds the two lists of held-back pages equal.
using System.Linq;
using Newtonsoft.Json;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class HeldBackBandPagesTests
    {
        private const string Porsche = "porsche";

        /// <summary>The car page's number, the eighth of band D's.</summary>
        private static readonly int Car = FacePages.BandD.Single(p => p.Id == "car").Number;

        private static ScreenInstance Read(string face, string theme = null)
        {
            var json = "{\"Namespace\":\"Face1280x480\",\"Kind\":\"face\",\"Width\":1280,\"Height\":480,"
                + (theme == null ? string.Empty : "\"Theme\":\"" + theme + "\",")
                + "\"Face\":" + face + "}";
            var screen = JsonConvert.DeserializeObject<ScreenInstance>(json);
            screen.Normalise();
            return screen;
        }

        [Fact]
        public void The_car_page_is_held_back_and_keeps_its_number()
        {
            Assert.Equal(new[] { "car" }, FacePages.HeldBack);
            Assert.Equal(7, Car);
            Assert.True(FacePages.IsHeldBack("D", Car, null));
            Assert.True(FacePages.IsHeldBack("D", Car, Porsche));
            Assert.False(FacePages.IsHeldBack("D", 8, Porsche));
            foreach (var letter in new[] { "A", "B", "C" })
            {
                Assert.Empty(FacePages.For(letter).Where(p => FacePages.IsHeldBack(letter, p.Id)));
            }

            // Eight in the catalogue still, so the Porsche row is still the ninth; seven offered, each under
            // its own number.
            Assert.Equal(8, Contract.FaceZonePageCount(3));
            Assert.Equal(Enumerable.Range(0, 7), FacePages.Offered("D").Select(p => p.Number));
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 8 }, FacePages.Offered("D", Porsche).Select(p => p.Number));
            Assert.Equal(1 << Car, FacePages.HeldBackBandMask);
            Assert.Equal(0, Contract.DefaultZoneMask(3) & (1 << Car));
            Assert.Equal(0, Contract.DefaultZoneMask(3, Porsche) & (1 << Car));
        }

        [Fact]
        public void A_rig_that_opened_band_d_on_the_car_page_opens_on_fuel()
        {
            // Everything a rig could have saved about the page before 1.0: showing it, opening on it, its bit
            // in the mask and a glance at it.
            var face = Read("{\"Zones\":[0,0,14,7],\"Masks\":[15,2097151,2097151,255],\"Starts\":[0,0,14,7],\"QuickGlance\":307}").Face;
            Assert.Equal(0, face.Zone("D"));
            Assert.Equal(0, face.Start("D"));
            Assert.Equal(127, face.Mask("D"));
            Assert.False(face.PageEnabled("D", Car));
            Assert.Equal(Contract.DefaultQuickGlance, face.QuickGlance);
            Assert.Equal("Fuel", PanelScreens.OpensOnName(face, "D"));

            // A Porsche face moves on to the page after it, which is the Porsche row it opens on anyway.
            var porsche = Read("{\"Zones\":[0,0,14,7],\"Masks\":[15,2097151,2097151,511],\"Starts\":[0,0,14,7]}", Porsche).Face;
            Assert.Equal(8, porsche.Zone("D"));
            Assert.Equal(8, porsche.Start("D"));
            Assert.Equal(511 & ~(1 << Car), porsche.Mask("D"));
        }

        [Fact]
        public void A_rig_that_had_reordered_band_d_skips_the_car_page_in_its_own_order()
        {
            var face = Read("{\"Zones\":[0,0,14,7],\"Masks\":[15,2097151,2097151,255],\"Starts\":[0,0,14,7],"
                + "\"Orders\":[null,null,null,[7,3,2,1,0,4,5,6]]}").Face;
            Assert.Equal(3, face.Start("D"));
            Assert.Equal(3, face.Zone("D"));
            // The order is the driver's and keeps the page, which a re-enabled page would want back.
            Assert.Equal(new[] { 7, 3, 2, 1, 0, 4, 5, 6 }, face.Order("D"));
        }

        [Fact]
        public void A_rig_that_never_chose_it_keeps_every_page_it_had()
        {
            var face = Read("{\"Zones\":[0,0,14,6],\"Masks\":[15,2097151,2097151,107],\"Starts\":[0,0,14,3],\"QuickGlance\":306}").Face;
            Assert.Equal(6, face.Zone("D"));
            Assert.Equal(3, face.Start("D"));
            Assert.Equal(107, face.Mask("D"));
            Assert.Equal(306, face.QuickGlance);
        }

        [Fact]
        public void The_band_never_cycles_to_it_either_way()
        {
            var face = new FaceSettings();
            face.Normalise();
            var forward = Enumerable.Range(0, 16).Select(_ => face.Cycle("D")).ToArray();
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 0 }, forward.Take(7));
            Assert.DoesNotContain(Car, forward);
            var back = Enumerable.Range(0, 16).Select(_ => face.CycleBack("D")).ToArray();
            Assert.DoesNotContain(Car, back);
            // And the counter counts seven.
            face.SetStart("D", 6);
            Assert.Equal(7, face.Position("D"));
        }

        [Fact]
        public void Nothing_can_turn_it_on_or_open_on_it()
        {
            var face = new FaceSettings();
            face.Normalise();
            face.SetPageEnabled("D", Car, true);
            Assert.False(face.PageEnabled("D", Car));
            PanelScreens.Tick(face, "D", Car, true);
            Assert.False(face.PageEnabled("D", Car));
            PanelScreens.SetEveryPage(face, "D", true);
            Assert.False(face.PageEnabled("D", Car));
            face.SetStart("D", Car);
            Assert.Equal(0, face.Start("D"));
            Assert.Equal(0, face.Zone("D"));

            face.QuickGlance = Contract.QuickGlanceValue(3, Car);
            Assert.Equal(Contract.DefaultQuickGlance, face.NormalisedQuickGlance());
            face.BeginQuickGlance();
            Assert.NotEqual(Car, face.Zone("D"));
        }

        [Fact]
        public void The_panel_offers_it_nowhere()
        {
            var face = new FaceSettings();
            face.Normalise();
            Assert.Equal("7 of 7", PanelScreens.ZoneCount(face, "D"));
            Assert.DoesNotContain("Car", PanelScreens.ZoneRows(face, "D", true).Select(r => r.Name));
            Assert.DoesNotContain("Car", PanelScreens.GlancePageLabels(3));
            Assert.DoesNotContain("Car", PanelScreens.GlancePageLabels(3, Porsche));
            Assert.Equal(PanelScreens.GlancePageLabels(3).Length, PanelScreens.GlancePages(3).Length);
            Assert.DoesNotContain(Contract.QuickGlanceValue(3, Car), PanelFacePlan.GlanceOptions());
            Assert.DoesNotContain(Contract.QuickGlanceValue(3, Car), PanelFacePlan.GlanceOptions(Porsche));
            foreach (var option in PanelFacePlan.GlanceOptions(Porsche)) Assert.Equal(option, Contract.NormaliseQuickGlance(option, Porsche));
        }
    }
}
