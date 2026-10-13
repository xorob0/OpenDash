// ThemeBandPagesTests.cs: a theme's band pages are part of band D's catalogue on a face of the theme
// (#718). The Porsche's foot was the ninth screen of its band dashboard while every count here said
// eight, so the plugin clamped index 8 back to fuel and no rig could open the page.
//
// The other half is packages/dash/test/themeBandPages.test.ts, and ContractTests.Themes_agree_with_contract_ts_when_present
// holds the two catalogues' band pages equal. What is held here is the per-screen reading: nine pages
// and the Porsche row first on a Porsche face, and a default face, and the file a default rig wrote
// before this, exactly as they were.
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class ThemeBandPagesTests
    {
        private const string Porsche = "porsche";

        private static ScreenInstance PorscheScreen()
        {
            var screen = new ScreenInstance { Kind = Contract.KindFace, Width = 1280, Height = 480, Namespace = "Face1280x480", Theme = Porsche };
            screen.Normalise();
            screen.Face.OpenOnStartPages();
            return screen;
        }

        [Fact]
        public void Band_d_on_a_porsche_face_is_the_house_eight_and_then_the_porsche_row()
        {
            var pages = FacePages.For("D", Porsche);
            Assert.Equal(FacePages.BandD.Select(p => p.Id).Concat(new[] { "porscheFoot" }), pages.Select(p => p.Id));
            Assert.Equal(FacePages.BandD.Select(p => p.Name).Concat(new[] { "Porsche" }), pages.Select(p => p.Name));
            for (var page = 0; page < pages.Count; page++) Assert.Equal(page, pages[page].Number);
            Assert.Equal(9, Contract.FaceZonePageCount(3, Porsche));
            // Every page but the held-back car page, the eighth (#969).
            Assert.Equal(511 & ~(1 << 7), Contract.DefaultZoneMask(3, Porsche));
            Assert.Equal(8, Contract.DefaultFaceZonePage(3, Porsche));
            Assert.Equal(new[] { 0, 0, 14, 8 }, Contract.DefaultFaceZones(Porsche));

            // No other zone moves, and no other theme: the default look, no theme and an id nobody knows
            // are all the house's eight, opening on fuel.
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal(Contract.FaceZonePageCounts[i], Contract.FaceZonePageCount(i, Porsche));
                Assert.Equal(Contract.DefaultFaceZonePages[i], Contract.DefaultFaceZonePage(i, Porsche));
            }
            foreach (var theme in new[] { null, Contract.DefaultThemeId, "nope" })
            {
                Assert.Same(FacePages.BandD, FacePages.For("D", theme));
                Assert.Equal(8, Contract.FaceZonePageCount(3, theme));
                Assert.Equal(127, Contract.DefaultZoneMask(3, theme));
                Assert.Equal(0, Contract.DefaultFaceZonePage(3, theme));
            }
        }

        [Fact]
        public void A_porsche_screen_opens_band_d_on_the_porsche_row_and_its_button_cycles_eight_pages()
        {
            // Nine in the catalogue, and the car page, the eighth, held back (#969).
            var face = PorscheScreen().Face;
            Assert.Equal(Porsche, face.ThemeId());
            Assert.Equal(8, face.Start("D"));
            Assert.Equal(8, face.Zone("D"));
            Assert.Equal(511 & ~(1 << 7), face.Mask("D"));
            Assert.Equal(Enumerable.Range(0, 9), face.Order("D"));
            Assert.Equal(8, face.Position("D"));

            var seen = Enumerable.Range(0, 8).Select(_ => face.Cycle("D")).ToArray();
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 8 }, seen);
            Assert.Equal(6, face.CycleBack("D"));
            // The other zones open where a default face's do.
            Assert.Equal(new[] { 0, 0, 14 }, new[] { face.Start("A"), face.Start("B"), face.Start("C") });
        }

        [Fact]
        public void A_porsche_screen_added_from_the_add_sheet_opens_on_the_porsche_row()
        {
            var entry = new PackageEntry { Package = "OpenDash Porsche 1280x480.simhubdash", Folder = "OpenDash Porsche 1280x480", Kind = Contract.KindFace, Width = 1280, Height = 480, Theme = Porsche };
            var settings = new OpenDashSettings();
            var screen = settings.AddScreen(entry, null);
            screen.Normalise();
            Assert.Equal(8, screen.Face.Start("D"));
            Assert.Equal("Porsche", PanelScreens.OpensOnName(screen.Face, "D"));
        }

        [Fact]
        public void The_theme_reaches_the_face_whichever_json_sets_first()
        {
            // Json.NET sets properties in the file's order, and a screen's Face may come before its Theme.
            var json = "{\"Face\":{\"Zones\":[0,0,14,8],\"Masks\":[15,2097151,2097151,511],\"Starts\":[0,0,14,8]},"
                + "\"Namespace\":\"Face1280x480\",\"Kind\":\"face\",\"Width\":1280,\"Height\":480,\"Theme\":\"porsche\"}";
            var screen = JsonConvert.DeserializeObject<ScreenInstance>(json);
            screen.Normalise();
            Assert.Equal(8, screen.Face.Zone("D"));
            Assert.Equal(8, screen.Face.Start("D"));
            // The ninth bit stands; the eighth, the held-back car page's, is trimmed (#969).
            Assert.Equal(511 & ~(1 << 7), screen.Face.Mask("D"));

            // And a copy keeps it.
            Assert.Equal(Porsche, screen.Copy().Face.ThemeId());
            Assert.Equal(Porsche, screen.Face.Clone().ThemeId());
        }

        [Fact]
        public void The_theme_is_not_written_into_the_face()
        {
            // The screen carries it already; a face that wrote it too would change every rig's file. The
            // keys are the ones a face has written since #791, GlanceHeld being a read-only property Json.NET
            // has always written and never read back.
            var written = JObject.Parse(JsonConvert.SerializeObject(PorscheScreen()));
            Assert.Equal(Porsche, (string)written["Theme"]);
            Assert.Equal(
                new[] { "Zones", "Masks", "Starts", "ClassOnly", "BarFields", "QuickGlance", "Orders", "GlanceHeld" },
                ((JObject)written["Face"]).Properties().Select(p => p.Name));
        }

        [Fact]
        public void A_default_face_written_before_718_normalises_exactly_as_it_did()
        {
            // A band D page 8, a ninth mask bit, a ninth page in the order and a glance at band D page 8:
            // everything a default face cannot carry, which is clamped as it always was.
            var json = "{\"Namespace\":\"Face1280x480\",\"Name\":\"1280 × 480\",\"Kind\":\"face\",\"Width\":1280,\"Height\":480,"
                + "\"Face\":{\"Zones\":[3,9,14,8],\"Masks\":[11,2097115,2097151,767],\"Starts\":[0,0,14,8],"
                + "\"ClassOnly\":[false,true,false,true],\"BarFields\":[0,1,5,6],\"QuickGlance\":308,"
                + "\"Orders\":[[3,1,0,2],null,null,[4,5,6,7,0,1,2,3,8]]}}";
            var screen = JsonConvert.DeserializeObject<ScreenInstance>(json);
            screen.Normalise();
            var face = screen.Face;
            Assert.Null(face.ThemeId());
            Assert.Equal(new[] { 3, 9, 14, 0 }, face.Zones);
            Assert.Equal(new[] { 11, 2097115, 2097151, 127 }, face.Masks);
            Assert.Equal(new[] { 0, 0, 14, 0 }, face.Starts);
            Assert.Equal(new[] { 3, 1, 0, 2 }, face.Orders[0]);
            Assert.Equal(Enumerable.Range(0, Modules.Count), face.Orders[1]);
            Assert.Equal(new[] { 4, 5, 6, 7, 0, 1, 2, 3 }, face.Orders[3]);
            Assert.Equal(Contract.DefaultQuickGlance, face.QuickGlance);
            Assert.Equal("7 of 7", PanelScreens.ZoneCount(face, "D"));
        }

        [Fact]
        public void A_porsche_face_written_before_718_keeps_what_it_had()
        {
            // A Porsche screen written before #718 has the house's eight in its band mask and fuel as its
            // start, which were the only answers it could hold. Neither is read as a choice about a page that
            // did not exist: the mask is the driver's, so the Porsche row is offered unticked rather than
            // switched on behind their back, and the start stays where it was.
            var json = "{\"Namespace\":\"Face1280x480\",\"Kind\":\"face\",\"Width\":1280,\"Height\":480,\"Theme\":\"porsche\","
                + "\"Face\":{\"Zones\":[0,0,14,0],\"Masks\":[15,2097151,2097151,255],\"Starts\":[0,0,14,0]}}";
            var screen = JsonConvert.DeserializeObject<ScreenInstance>(json);
            screen.Normalise();
            Assert.Equal(0, screen.Face.Start("D"));
            Assert.Equal(127, screen.Face.Mask("D"));
            Assert.False(screen.Face.PageEnabled("D", 8));
            Assert.Equal("7 of 8", PanelScreens.ZoneCount(screen.Face, "D"));
            // Ticking it is all it takes.
            PanelScreens.Tick(screen.Face, "D", 8, true);
            Assert.True(screen.Face.PageEnabled("D", 8));
        }

        [Fact]
        public void The_panel_lists_the_porsche_row_on_a_porsche_screen_and_only_there()
        {
            var face = PorscheScreen().Face;
            Assert.Equal("8 of 8", PanelScreens.ZoneCount(face, "D"));
            Assert.Equal("Porsche", PanelScreens.OpensOnName(face, "D"));
            // The aside draws the cycle from the page the band opens on, which is the Porsche row, then the
            // house's seven in their order, the held-back car page among none of them.
            var offered = FacePages.BandD.Where(p => p.Id != "car").Select(p => p.Name).ToArray();
            var rows = PanelScreens.ZoneRows(face, "D", true);
            Assert.Equal(new[] { "Porsche" }.Concat(offered), rows.Select(r => r.Name));
            // The glance's page picker lists it after the house's seven, as page 8.
            Assert.Equal(offered.Concat(new[] { "Porsche" }), PanelScreens.GlancePageLabels(3, Porsche));
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 8 }, PanelScreens.GlancePages(3, Porsche));
            Assert.Equal(offered, PanelScreens.GlancePageLabels(3));
            Assert.Equal("Band D · Porsche", PanelFacePlan.GlanceLabel(Contract.QuickGlanceValue(3, 8), Porsche));
            Assert.Equal(PanelFacePlan.GlanceOptions().Length + 1, PanelFacePlan.GlanceOptions(Porsche).Length);

            var house = new FaceSettings();
            house.Normalise();
            Assert.Equal("7 of 7", PanelScreens.ZoneCount(house, "D"));
            Assert.Equal(7, PanelScreens.ZoneRows(house, "D", true).Count);
        }

        [Fact]
        public void A_porsche_face_can_glance_at_the_porsche_row()
        {
            var face = PorscheScreen().Face;
            face.QuickGlance = Contract.QuickGlanceValue(3, 8);
            face.Normalise();
            Assert.Equal(308, face.QuickGlance);
            Assert.Equal(308, face.NormalisedQuickGlance());
            face.Cycle("D");
            face.BeginQuickGlance();
            Assert.Equal(8, face.Zone("D"));
            face.EndQuickGlance();
            Assert.Equal(0, face.Zone("D"));
            // On a default face the same value is half a glance, and the default stands in for it.
            Assert.Equal(Contract.DefaultQuickGlance, Contract.NormaliseQuickGlance(308));
            Assert.Equal(308, Contract.NormaliseQuickGlance(308, Porsche));
        }

        [Fact]
        public void A_page_clash_names_the_porsche_row()
        {
            var face = PorscheScreen().Face;
            face.QuickGlance = Contract.QuickGlanceValue(3, 8);
            var clash = Assert.Single(face.Clashes());
            Assert.Equal("porscheFoot", clash.PageId);
            Assert.Equal("Porsche", clash.PageName);
            Assert.Equal(new[] { "D" }, clash.Zones);
            Assert.True(clash.Glance);
        }
    }
}
