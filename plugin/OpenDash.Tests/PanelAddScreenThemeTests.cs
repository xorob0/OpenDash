// PanelAddScreenThemeTests.cs: the Add sheet's third question (#198). A theme is asked after the size, only of a
// size some carried theme is drawn at, with the default look picked; the name proposed for a themed screen carries
// the theme's name, because SimHub lists the dashboard under it beside the default face of its size (ADR 0016); and
// the kinds and sizes the sheet offers are the default look's, so a themed package is never a second tile of its size.
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelAddScreenThemeTests
    {
        private static PackageEntry Package(string folder, int width, int height, string theme = null, string kind = Contract.KindFace)
        {
            return new PackageEntry
            {
                Package = "OpenDashPlugin.Resources." + folder + ".simhubdash",
                Folder = folder,
                Kind = kind,
                Width = width,
                Height = height,
                Theme = theme,
            };
        }

        private static readonly PackageEntry Default1280 = Package("OpenDash 1280x480", 1280, 480);
        private static readonly PackageEntry Default850 = Package("OpenDash 850x480", 850, 480);
        private static readonly PackageEntry Porsche1280 = Package("OpenDash Porsche 1280x480", 1280, 480, "porsche");

        private static IReadOnlyList<PackageEntry> Catalogue()
        {
            return new[] { Default850, Porsche1280, Default1280, Package("OpenDash Pit wall", 1920, 1080, null, Contract.KindPitWall) };
        }

        [Fact]
        public void The_sizes_offered_are_the_default_looks_and_a_theme_is_never_a_second_tile()
        {
            var face = PanelAddScreen.Types(Catalogue()).Single(t => t.Kind == Contract.KindFace);
            Assert.DoesNotContain(Porsche1280, face.Entries);
            Assert.Equal(2, face.Entries.Count);
        }

        [Fact]
        public void A_size_with_a_theme_asks_it_with_the_default_first_and_one_without_asks_nothing()
        {
            var at1280 = PanelAddScreen.Themes(Catalogue(), Default1280);
            Assert.Equal(new[] { Default1280, Porsche1280 }, at1280);
            Assert.True(PanelAddScreen.AsksTheme(at1280));
            Assert.Equal("OpenDash", PanelAddScreen.ThemeName(at1280[0]));
            Assert.Equal("Porsche", PanelAddScreen.ThemeName(at1280[1]));
            Assert.Equal(4, PanelAddScreen.NameStepNumber(true));

            var at850 = PanelAddScreen.Themes(Catalogue(), Default850);
            Assert.Equal(new[] { Default850 }, at850);
            Assert.False(PanelAddScreen.AsksTheme(at850));
            Assert.Equal(3, PanelAddScreen.NameStepNumber(false));
            Assert.Equal("Theme", PanelAddScreen.ThemeStep);
        }

        /// <summary>The default look is picked until the driver picks another, and a theme picked is kept across a
        /// change of size only where the new size is drawn in it.</summary>
        [Fact]
        public void A_theme_picked_is_kept_where_the_new_size_has_it_and_the_default_otherwise()
        {
            Assert.Same(Default1280, PanelAddScreen.ThemeKept(PanelAddScreen.Themes(Catalogue(), Default1280), null));
            Assert.Same(Porsche1280, PanelAddScreen.ThemeKept(PanelAddScreen.Themes(Catalogue(), Default1280), "porsche"));
            Assert.Same(Default850, PanelAddScreen.ThemeKept(PanelAddScreen.Themes(Catalogue(), Default850), "porsche"));
        }

        /// <summary>ADR 0016 owes this to #198: SimHub lists a dashboard by the screen's name, so the name proposed for
        /// a themed screen says the theme, and two of them on one rig are told apart as any two names are.</summary>
        [Fact]
        public void The_name_proposed_for_a_themed_screen_carries_the_themes_name()
        {
            Assert.Equal("Porsche 1280 × 480", PanelAddScreen.DefaultName(Porsche1280));
            Assert.Equal("Porsche 1280 × 480 (2)", PanelAddScreen.FilledName(string.Empty, false, Porsche1280, new[] { "Porsche 1280 × 480" }));
            Assert.Equal("1280 × 480", PanelAddScreen.DefaultName(Default1280));
        }

        [Fact]
        public void A_themed_screen_is_not_assigned_by_hand_and_its_lines_say_what_is_left()
        {
            Assert.Equal("Restart SimHub. A display showing a 1280 × 480 face then switches to \"Porsche 1280 × 480\" in the cars it is drawn for.",
                PanelAddScreen.NextStep("Porsche 1280 × 480", Porsche1280));
            Assert.Equal(PanelAddScreen.NextStep("Rim"), PanelAddScreen.NextStep("Rim", Default850));

            var themed = PackageCatalogue.NewScreen(Porsche1280, "Porsche 1280 × 480", new string[0]);
            Assert.Equal("porsche", themed.Theme);
            Assert.Equal("Added Porsche 1280 × 480. Restart SimHub, and a display showing a 1280 × 480 face switches to it in the cars it is drawn for.",
                PanelAddScreen.Added(themed));
            var plain = PackageCatalogue.NewScreen(Default850, "Rim", new string[0]);
            Assert.Null(plain.Theme);
            Assert.Equal(PanelAddScreen.Added("Rim", "Rim"), PanelAddScreen.Added(plain));
        }

        /// <summary>A themed screen's edit sheet offers the sizes its theme is drawn at, so a resize keeps the theme
        /// and takes the theme with it into the screen.</summary>
        [Fact]
        public void A_themed_screens_edit_sheet_offers_only_its_themes_sizes_and_a_resize_keeps_the_theme()
        {
            var porscheAt1920 = Package("OpenDash Porsche 1920x480", 1920, 480, "porsche");
            var catalogue = Catalogue().Concat(new[] { porscheAt1920 }).ToList();
            var faces = PanelAddScreen.Types(catalogue, "porsche").Single(t => t.Kind == Contract.KindFace);
            Assert.Equal(new[] { Porsche1280, porscheAt1920 }.OrderBy(e => e.Width), faces.Entries.OrderBy(e => e.Width));

            var settings = new OpenDashSettings();
            var screen = settings.AddScreen(Porsche1280, "Porsche");
            settings.ResizeScreen(screen, porscheAt1920);
            Assert.Equal("porsche", screen.Theme);
            Assert.Equal(porscheAt1920.Package, screen.Package);
            Assert.Equal("porsche", screen.Copy().Theme);
        }

        [Fact]
        public void Removing_a_themed_screen_says_its_displays_stop_switching_and_the_drivers_playlists_stay()
        {
            var themed = PackageCatalogue.NewScreen(Porsche1280, "Porsche", new string[0]);
            Assert.EndsWith(" Your displays stop switching to it by car; playlists you made in SimHub are kept.", PanelScreens.RemoveBody(themed));
            Assert.DoesNotContain("by car", PanelScreens.RemoveBody(PackageCatalogue.NewScreen(Default1280, "Rim", new string[0])));
        }
    }
}
