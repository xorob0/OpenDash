// PanelPreviewScreenTests.cs: the preview shows the screen SimHub's own window would. #1085.
//
// The shapes below are the ones the generator writes: a face is its rev bar arrangements, of which the
// settings enable one, and then its idle screen last; the companion is its module screens and then its
// idle screen; and a dashboard made before #763 has no idle screen at all. Every racing screen declares
// `idle: false` and the idle screen `inGame: false, pit: false` (packages/dash/src/idle.ts).
using System.Collections.Generic;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelPreviewScreenTests
    {
        private static PreviewScreenRoles Racing(bool enabled = true)
        {
            return new PreviewScreenRoles { InGame = true, Idle = false, Pit = true, Enabled = enabled };
        }

        private static PreviewScreenRoles IdleScreen()
        {
            return new PreviewScreenRoles { InGame = false, Idle = true, Pit = false, Enabled = true };
        }

        /// <summary>Rev bar on, rev bar off, rev bar on top: the settings enable the second, and the idle
        /// screen is last, as every face is generated.</summary>
        private static List<PreviewScreenRoles> Face()
        {
            return new List<PreviewScreenRoles> { Racing(false), Racing(true), Racing(false), IdleScreen() };
        }

        private static List<PreviewScreenRoles> Companion()
        {
            return new List<PreviewScreenRoles> { Racing(), Racing(), Racing(), Racing(), Racing(), IdleScreen() };
        }

        [Fact]
        public void With_no_game_running_a_face_shows_its_idle_screen_although_it_is_last()
        {
            Assert.Equal(PreviewMode.Idle, PanelPreviewScreen.Mode(Face(), false));
            Assert.Equal(3, PanelPreviewScreen.Choose(Face(), false, -1));
            // Nor does a racing screen the preview was already showing hold its place.
            Assert.Equal(3, PanelPreviewScreen.Choose(Face(), false, 1));
        }

        [Fact]
        public void With_a_game_running_a_face_shows_its_enabled_racing_screen()
        {
            Assert.Equal(PreviewMode.Game, PanelPreviewScreen.Mode(Face(), true));
            Assert.Equal(1, PanelPreviewScreen.Choose(Face(), true, -1));
            Assert.Equal(1, PanelPreviewScreen.Choose(Face(), true, 3));
        }

        [Fact]
        public void The_preview_follows_the_game_starting_and_stopping()
        {
            var shown = PanelPreviewScreen.Choose(Face(), false, -1);
            Assert.Equal(3, shown);
            shown = PanelPreviewScreen.Choose(Face(), true, shown);
            Assert.Equal(1, shown);
            shown = PanelPreviewScreen.Choose(Face(), false, shown);
            Assert.Equal(3, shown);
        }

        [Fact]
        public void A_disabled_screen_is_never_chosen_even_when_it_is_the_one_showing()
        {
            Assert.Equal(1, PanelPreviewScreen.Choose(Face(), true, 0));
            Assert.Equal(3, PanelPreviewScreen.Choose(Face(), false, 0));
        }

        /// <summary>A dashboard made before #763, or someone else's, with no idle screen: SimHub's mode is
        /// Indeterminate at rest and takes the first candidate, so the preview does too rather than
        /// drawing nothing.</summary>
        [Fact]
        public void A_package_with_no_idle_screen_falls_through_as_SimHub_does()
        {
            var screens = new List<PreviewScreenRoles> { Racing(false), Racing(true) };
            Assert.Equal(PreviewMode.Indeterminate, PanelPreviewScreen.Mode(screens, false));
            Assert.Equal(1, PanelPreviewScreen.Choose(screens, false, -1));
            Assert.Equal(PreviewMode.Game, PanelPreviewScreen.Mode(screens, true));
            Assert.Equal(1, PanelPreviewScreen.Choose(screens, true, -1));
        }

        /// <summary>A game running but no in-game screen at all is not Game mode: SimHub falls through to
        /// the idle screen, and so does the preview.</summary>
        [Fact]
        public void A_running_game_with_no_in_game_screen_still_shows_the_idle_screen()
        {
            var screens = new List<PreviewScreenRoles> { IdleScreen() };
            Assert.Equal(PreviewMode.Idle, PanelPreviewScreen.Mode(screens, true));
            Assert.Equal(0, PanelPreviewScreen.Choose(screens, true, -1));
        }

        [Fact]
        public void The_companion_shows_its_idle_screen_at_rest()
        {
            Assert.Equal(5, PanelPreviewScreen.Choose(Companion(), false, -1));
            Assert.Equal(5, PanelPreviewScreen.Choose(Companion(), false, 2));
        }

        /// <summary>The companion pages between module screens that all carry the in-game role, and SimHub
        /// remembers which one it was on. Putting the preview back on the first module every second would
        /// undo every page turn.</summary>
        [Fact]
        public void The_companion_keeps_the_module_it_is_on_while_a_game_runs()
        {
            Assert.Equal(3, PanelPreviewScreen.Choose(Companion(), true, 3));
            Assert.Equal(0, PanelPreviewScreen.Choose(Companion(), true, 5));
            Assert.Equal(0, PanelPreviewScreen.Choose(Companion(), true, -1));
        }

        /// <summary>The panel cannot tell that the car is in the pit lane, so a pit screen SimHub has chosen
        /// while a game runs is left alone rather than fought over once a second.</summary>
        [Fact]
        public void A_pit_screen_SimHub_chose_is_kept_while_a_game_runs()
        {
            var pit = new PreviewScreenRoles { InGame = false, Idle = false, Pit = true, Enabled = true };
            var screens = new List<PreviewScreenRoles> { Racing(), pit, IdleScreen() };
            Assert.Equal(1, PanelPreviewScreen.Choose(screens, true, 1));
            Assert.Equal(0, PanelPreviewScreen.Choose(screens, true, -1));
            Assert.Equal(2, PanelPreviewScreen.Choose(screens, false, 1));
        }

        [Fact]
        public void A_layer_is_never_chosen()
        {
            var layer = new PreviewScreenRoles { InGame = true, Idle = true, Pit = true, Enabled = true, Layer = true };
            var screens = new List<PreviewScreenRoles> { layer, Racing(), IdleScreen() };
            Assert.Equal(2, PanelPreviewScreen.Choose(screens, false, 0));
            Assert.Equal(1, PanelPreviewScreen.Choose(screens, true, 0));
        }

        /// <summary>ScreenEnabled starts false and only SimHub's frame loop writes it, so before the first
        /// frame no screen is enabled. The roles still decide, over every screen that is not a layer.</summary>
        [Fact]
        public void Before_any_screen_is_found_enabled_the_roles_still_decide()
        {
            var screens = new List<PreviewScreenRoles> { Racing(false), Racing(false), new PreviewScreenRoles { Idle = true } };
            Assert.Equal(2, PanelPreviewScreen.Choose(screens, false, -1));
            Assert.Equal(0, PanelPreviewScreen.Choose(screens, true, -1));
        }

        [Fact]
        public void A_dashboard_with_no_screens_has_nothing_to_show()
        {
            Assert.Equal(PreviewMode.None, PanelPreviewScreen.Mode(new List<PreviewScreenRoles>(), false));
            Assert.Equal(-1, PanelPreviewScreen.Choose(new List<PreviewScreenRoles>(), false, -1));
            Assert.Equal(-1, PanelPreviewScreen.Choose(null, true, -1));
        }
    }
}
