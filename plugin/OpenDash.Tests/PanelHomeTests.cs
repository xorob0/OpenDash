// PanelHomeTests.cs: Home's words, numbers and rules -- what each device's row says, which pictures are live,
// what each issue's press does -- held to Main.dc.html, voice.md and what search says about them.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelHomeTests
    {
        [Fact]
        public void Home_says_what_the_artboard_says()
        {
            Assert.Equal("Home", PanelHome.Title);
            Assert.Equal("Right now", PanelHome.RightNowTitle);
            Assert.Equal("Quick controls", PanelHome.QuickControlsTitle);
            // A noun phrase where the artboard asks "Try a flag or the spotter": the button says what to do.
            Assert.Equal("Flags and spotter", PanelHome.TryTitle);
            Assert.Equal("Open Rig", PanelHome.OpenRig);
            Assert.Equal("Open", PanelHome.OpenLink);
            Assert.Equal("Open Screens", PanelHome.OpenScreens);
            Assert.Contains(PanelHome.Search, entry => entry.Label == PanelHome.RightNowTitle && entry.Route.Anchor == PanelHome.AnchorRightNow);
            Assert.Contains(PanelHome.Search, entry => entry.Label == PanelHome.QuickControlsTitle && entry.Route.Anchor == PanelHome.AnchorQuickControls);
            // Home's headline changes with the count, so its entry is the page's own name, which it draws over it.
            Assert.Contains(PanelHome.Search, entry => entry.Label == PanelHome.Title && entry.Route.Anchor == null && System.Array.IndexOf(entry.Keywords, "things to fix") >= 0);
            Assert.DoesNotContain(PanelHome.Search, entry => entry.Label == "Things to fix");
        }

        /// <summary>Every section heading and every label in the quick controls is found, and lands on Home.</summary>
        [Fact]
        public void Every_heading_and_label_it_draws_is_searchable()
        {
            var labels = PanelHome.Search.Select(entry => entry.Label).ToList();
            Assert.Equal(new[]
            {
                PanelHome.Title,
                PanelHome.RightNowTitle,
                PanelHome.QuickControlsTitle,
                PanelSettings.BrightnessTitle,
                PanelSettings.NightBrightnessTitle,
                PanelSettings.NightModeTitle,
                PanelHome.TryTitle,
            }, labels);
            Assert.Equal(labels.Count, labels.Distinct().Count());
            Assert.All(PanelHome.Search.Skip(3), entry => Assert.Equal(PanelHome.AnchorQuickControls, entry.Route.Anchor));
            // The slider's label is whichever brightness is in force, so it is drawn through BrightnessLabel.
            Assert.Equal("PanelHome.BrightnessLabel(", PanelHome.SearchDrawnOtherwise[PanelSettings.BrightnessTitle]);
            Assert.Equal("PanelHome.BrightnessLabel(", PanelHome.SearchDrawnOtherwise[PanelSettings.NightBrightnessTitle]);
            Assert.Empty(PanelHome.SoonDrawn);
        }

        /// <summary>The page's anchor ids, which search, Home's fix rows and the capture scripts route to: a
        /// renamed one sends each of them to the page's top, so every id is pinned, and a new one is added here.</summary>
        [Fact]
        public void Its_anchor_ids_are_pinned()
        {
            Assert.Equal(new[]
            {
                "AnchorAttention = home.attention",
                "AnchorQuickControls = home.quick-controls",
                "AnchorRightNow = home.right-now",
            }, AnchorTable.Of(typeof(PanelHome)));
        }

        /// <summary>Main.dc.html's numbers, which the page draws and nothing else holds.</summary>
        [Fact]
        public void Its_geometry_is_the_artboards()
        {
            Assert.Equal(10, PanelHome.HeaderGap);
            Assert.Equal(new[] { 18.0, 16, 32, 18, 16, 0.12, 3, 10, 13 }, new[]
            {
                PanelHome.IssuePaddingX, PanelHome.IssuePaddingY, PanelHome.IconWell, PanelHome.IconSize, PanelHome.IconGap,
                PanelHome.IconTint, PanelHome.DetailGap, PanelHome.StepsGap, PanelHome.DetailSize,
            });
            Assert.Equal(280, PanelHome.CardMinWidth);
            Assert.Equal(16, PanelHome.CardGap);
            Assert.Equal(3, PanelHome.CardMax);
            Assert.Equal(new[] { 14.0, 14, 12, 13 }, new[] { PanelHome.CardHeadPaddingX, PanelHome.CardHeadPaddingTop, PanelHome.CardHeadPaddingBottom, PanelHome.OpenLinkSize });
            Assert.Equal(new[] { 14.0, 12, 14, 14, 13, 8, 12, 3, 8, 10 }, new[]
            {
                PanelHome.RowPaddingX, PanelHome.RowPaddingY, PanelHome.RowGap, PanelHome.NameSize, PanelHome.MetaSize,
                PanelHome.MetaGap, PanelHome.LineSize, PanelHome.LineGap, PanelHome.DotSize, PanelHome.StripRowGap,
            });
            Assert.Equal(new[] { 18.0, 16, 12, 15, 14 }, new[] { PanelHome.QuickPaddingX, PanelHome.QuickPaddingY, PanelHome.QuickGap, PanelHome.QuickValueSize, PanelHome.QuickRigPaddingX });
            Assert.Equal(new[] { 1.6, 1, 1.2 }, PanelHome.QuickColumns);
        }

        /// <summary>Three cards of 280 fit the artboard's 896 of content, and fewer fit a narrower panel.</summary>
        [Fact]
        public void Right_now_lays_three_cards_only_where_three_fit()
        {
            Assert.Equal(3, PanelShell.Columns(896, PanelHome.CardMinWidth, PanelHome.CardGap, PanelHome.CardMax));
            Assert.Equal(2, PanelShell.Columns(700, PanelHome.CardMinWidth, PanelHome.CardGap, PanelHome.CardMax));
            Assert.Equal(1, PanelShell.Columns(400, PanelHome.CardMinWidth, PanelHome.CardGap, PanelHome.CardMax));
        }

        // --- The attention card ------------------------------------------------------------------------------

        private static PanelIssue Issue(string id, PanelPage page, string subject, PanelIssueAction action)
        {
            return new PanelIssue(id, page, subject, "Title", null, null, "Press", action);
        }

        [Fact]
        public void Each_issue_wears_the_icon_of_what_it_is_about()
        {
            Assert.Equal(PanelIcons.Warning, PanelHome.IssueIcon(Issue(PanelAttention.ScreenMissing + "Face1280x480", PanelPage.Screens, "Face1280x480", PanelIssueAction.Reinstall)));
            Assert.Equal(PanelIcons.Restart, PanelHome.IssueIcon(Issue(PanelAttention.ScreenRestart + "Face1280x480", PanelPage.Screens, "Face1280x480", PanelIssueAction.Navigate)));
            Assert.Equal(PanelIcons.Leds, PanelHome.IssueIcon(Issue(PanelAttention.StripUnselected + "LedBar1", PanelPage.Leds, "LedBar1", PanelIssueAction.CheckAgain)));
            Assert.Equal(PanelIcons.Leds, PanelHome.IssueIcon(Issue(PanelAttention.StripOutdated + "LedBar1", PanelPage.Updates, "LedBar1", PanelIssueAction.Navigate)));
            Assert.Equal(PanelIcons.Matrix, PanelHome.IssueIcon(Issue(PanelAttention.MatrixDark + "2", PanelPage.Matrix, "2", PanelIssueAction.Navigate)));
            Assert.Equal(PanelIcons.Matrix, PanelHome.IssueIcon(Issue(PanelAttention.FlagBoxOutdated, PanelPage.Matrix, null, PanelIssueAction.Navigate)));
            Assert.Equal(PanelIcons.Screens, PanelHome.IssueIcon(Issue(PanelAttention.ScreensUnclaimed, PanelPage.Screens, null, PanelIssueAction.Navigate)));
            Assert.Equal(PanelIcons.Restart, PanelHome.IssueIcon(Issue(PanelAttention.UpdateRestart, PanelPage.Updates, null, PanelIssueAction.Navigate)));
            Assert.Equal(PanelIcons.Updates, PanelHome.IssueIcon(Issue(PanelAttention.UpdateAvailable, PanelPage.Updates, null, PanelIssueAction.Navigate)));
            Assert.Equal(PanelIcons.Warning, PanelHome.IssueIcon(null));
        }

        /// <summary>The artboard's three rows: Open Rim and Open Left pillar open their page with the thing
        /// selected, Check again asks again, and Install it again writes the dashboard back. An issue about a
        /// page as a whole goes there and leaves its selection alone.</summary>
        [Fact]
        public void Each_press_does_what_its_issue_asks()
        {
            Assert.Equal(HomePress.Open, PanelHome.Press(Issue(PanelAttention.ScreenRestart + "Face850x480", PanelPage.Screens, "Face850x480", PanelIssueAction.Navigate)));
            Assert.Equal(HomePress.Open, PanelHome.Press(Issue(PanelAttention.MatrixDark + "2", PanelPage.Matrix, "2", PanelIssueAction.Navigate)));
            Assert.Equal(HomePress.CheckAgain, PanelHome.Press(Issue(PanelAttention.StripUnselected + "LedBar1", PanelPage.Leds, "LedBar1", PanelIssueAction.CheckAgain)));
            Assert.Equal(HomePress.Reinstall, PanelHome.Press(Issue(PanelAttention.ScreenMissing + "Face850x480", PanelPage.Screens, "Face850x480", PanelIssueAction.Reinstall)));
            Assert.Equal(HomePress.Go, PanelHome.Press(Issue(PanelAttention.ScreensUnclaimed, PanelPage.Screens, null, PanelIssueAction.Navigate)));
            Assert.Equal(HomePress.Go, PanelHome.Press(Issue(PanelAttention.UpdateRestart, PanelPage.Updates, null, PanelIssueAction.Navigate)));
            Assert.Equal(HomePress.Go, PanelHome.Press(null));
        }

        [Fact]
        public void The_press_sits_beside_the_text_only_where_two_blocks_fit()
        {
            Assert.True(PanelHome.PressBeside(true));
            Assert.False(PanelHome.PressBeside(false));
        }

        [Fact]
        public void Check_again_says_whether_the_thing_is_fixed()
        {
            var before = new PanelIssue(PanelAttention.StripUnselected + "LedBar1", PanelPage.Leds, "LedBar1", "Dash brow's profile is not selected", PanelAttention.UnselectedDetail, null, PanelAttention.CheckAgain, PanelIssueAction.CheckAgain);
            var fixedNow = PanelHome.CheckedAgain(before, new List<PanelIssue>());
            Assert.Equal("Checked again. That is fixed.", fixedNow.Text);
            Assert.Equal(PanelTone.Info, fixedNow.Tone);
            var still = PanelHome.CheckedAgain(before, new[] { before });
            Assert.Equal("Checked again. Dash brow's profile is not selected.", still.Text);
            Assert.Equal(PanelTone.Caution, still.Tone);
        }

        // --- Right now: screens ------------------------------------------------------------------------------

        private static ScreenInstance Screen(string kind, int width, int height)
        {
            var screen = new ScreenInstance { Namespace = "Test", Name = "Main dash", Kind = kind, Width = width, Height = height };
            screen.Normalise();
            if (screen.IsFace && screen.Face == null) screen.Face = new FaceSettings();
            return screen;
        }

        /// <summary>A face says the page each zone is on, in the panel's zone order: the body as it draws, then
        /// band D. The artboard's "Lap times · Gear · Relative" is the body of the same line.</summary>
        [Fact]
        public void A_face_says_the_page_each_zone_is_on()
        {
            var face = Screen(Contract.KindFace, 1280, 480);
            Assert.Equal("Lap times · Gear, speed, revs · Relative · Fuel", PanelHome.ScreenShows(new OpenDashSettings(), face));
            face.Face.Zones[Array.IndexOf(Contract.FaceZoneLetters, "A")] = 1;
            Assert.Equal("Lap times · Gear alone · Relative · Fuel", PanelHome.ScreenShows(new OpenDashSettings(), face));
            // A portrait face draws its body as a column, A first.
            var portrait = Screen(Contract.KindFace, 600, 686);
            Assert.StartsWith("Gear, speed, revs · Lap times · ", PanelHome.ScreenShows(new OpenDashSettings(), portrait));
        }

        [Fact]
        public void A_pit_wall_says_its_page_a_companion_its_rotation_and_a_card_face_its_cards()
        {
            var wall = Screen(Contract.KindPitWall, 1920, 1080);
            Assert.Equal("Race page", PanelHome.ScreenShows(new OpenDashSettings(), wall));
            wall.PitWallPage = 1;
            Assert.Equal("Tower page", PanelHome.ScreenShows(new OpenDashSettings(), wall));
            Assert.Equal("Telemetry page", PanelHome.PitWallPage(2));
            Assert.Equal("Race page", PanelHome.PitWallPage(9));

            Assert.Equal("21 of 21 modules", PanelHome.ModulesLine(null));
            Assert.Equal("21 of 21 modules", PanelHome.ModulesLine(new bool[Modules.Count]));
            var some = new bool[Modules.Count];
            some[0] = some[4] = some[16] = true;
            Assert.Equal("3 of 21 modules", PanelHome.ModulesLine(some));
            var phone = Screen(Contract.KindCompanion, 1080, 2400);
            Assert.Matches(@"^\d+ of 21 modules$", PanelHome.ScreenShows(new OpenDashSettings(), phone));

            var round = Screen(Contract.KindSlots, 480, 480);
            Assert.Equal("Speed · Current lap · Last lap · Best lap · Delta · Position · Session · Fuel · Fuel laps · TC · ABS · Tyre temps",
                PanelHome.ScreenShows(new OpenDashSettings(), round));
            Assert.Equal(string.Empty, PanelHome.ScreenShows(new OpenDashSettings(), null));
        }

        /// <summary>What SimHub is missing outranks what the screen shows, in the Screens cards' own words and
        /// inks; a state nobody could read has no dot rather than a green one.</summary>
        [Fact]
        public void A_screen_says_what_SimHub_is_missing_before_what_it_shows()
        {
            var face = Screen(Contract.KindFace, 1280, 480);
            var settings = new OpenDashSettings();
            var missing = PanelHome.ScreenLine(settings, face, false, true);
            Assert.Equal("Missing", missing.Text);
            Assert.Equal(Theme.StatusFailed, missing.TextHex);
            Assert.Equal(Theme.StatusFailed, missing.DotHex);
            var restart = PanelHome.ScreenLine(settings, face, true, true);
            Assert.Equal("Not in SimHub yet", restart.Text);
            Assert.Equal(Theme.Caution, restart.TextHex);
            Assert.Equal(Theme.Caution, restart.DotHex);
            var fine = PanelHome.ScreenLine(settings, face, true, false);
            Assert.Equal(PanelHome.ScreenShows(settings, face), fine.Text);
            Assert.Equal(Theme.TextSecondary, fine.TextHex);
            Assert.Equal(Theme.StatusUpToDate, fine.DotHex);
            Assert.Null(PanelHome.ScreenLine(settings, face, null, false).DotHex);
        }

        [Fact]
        public void A_screen_gives_its_size_unless_its_package_is_gone()
        {
            Assert.Equal("1280 × 480", PanelHome.ScreenSize(Screen(Contract.KindFace, 1280, 480)));
            Assert.Equal(string.Empty, PanelHome.ScreenSize(new ScreenInstance { Kind = Contract.KindFace }));
            Assert.Equal(string.Empty, PanelHome.ScreenSize(null));
        }

        // --- Right now: LEDs ---------------------------------------------------------------------------------

        [Fact]
        public void A_strip_is_drawn_with_its_own_shape()
        {
            Assert.Equal(new[] { 3, 9 }, PanelHome.StripSpan("3-9-3"));
            Assert.Equal(new[] { 0, 15 }, PanelHome.StripSpan("0-15-0"));
            Assert.Equal(new[] { 3, 9 }, PanelHome.StripSpan("nonsense"));
            Assert.Equal("3/9/3", PanelHome.StripShape(new LedBar { Shape = "3-9-3" }));
            Assert.Equal(string.Empty, PanelHome.StripShape(new LedBar()));
        }

        /// <summary>
        /// The strip's picture is the car's own run only where that is what the strip shows: the tables have a
        /// car, the strip's centre shows the revs in the car's own style, and SimHub is not known to be missing
        /// or not showing its profile. Everything else is drawn dark, never faked.
        /// </summary>
        [Fact]
        public void A_strip_is_live_only_where_it_shows_the_cars_own_lights()
        {
            var car = Contract.LedRpmStyleCar;
            var rpm = Contract.LedCentres[0];
            Assert.True(PanelHome.StripLive(true, car, rpm, FlagBoxInstallState.UpToDate, true));
            Assert.True(PanelHome.StripLive(true, car, rpm, null, null));
            Assert.False(PanelHome.StripLive(false, car, rpm, FlagBoxInstallState.UpToDate, true));
            Assert.False(PanelHome.StripLive(true, Contract.LedRpmStyleLeftToRight, rpm, FlagBoxInstallState.UpToDate, true));
            Assert.False(PanelHome.StripLive(true, car, "brake", FlagBoxInstallState.UpToDate, true));
            Assert.False(PanelHome.StripLive(true, car, rpm, FlagBoxInstallState.UpToDate, false));
            Assert.False(PanelHome.StripLive(true, car, rpm, FlagBoxInstallState.NotInstalled, null));
            Assert.False(PanelHome.StripLive(true, car, rpm, FlagBoxInstallState.Failed, null));
        }

        [Fact]
        public void A_strip_says_what_SimHub_lacks_then_the_car_then_the_profile()
        {
            var live = PanelHome.StripLine(true, "Porsche 911 GT3 R (992)", FlagBoxInstallState.UpToDate, true);
            Assert.Equal("Car's own lights · Porsche 911 GT3 R (992)", live.Text);
            Assert.Equal(Theme.TextSecondary, live.TextHex);
            Assert.Equal(Theme.StatusUpToDate, live.DotHex);
            Assert.Equal("Car's own lights", PanelHome.StripLine(true, " ", FlagBoxInstallState.UpToDate, true).Text);

            var unselected = PanelHome.StripLine(false, null, FlagBoxInstallState.UpToDate, false);
            Assert.Equal("Not selected in SimHub", unselected.Text);
            Assert.Equal(Theme.Caution, unselected.TextHex);
            Assert.Equal(Theme.Caution, unselected.DotHex);
            Assert.Equal("Not installed in SimHub", PanelHome.StripLine(false, null, FlagBoxInstallState.NotInstalled, null).Text);
            Assert.Equal("Not installed in SimHub", PanelHome.StripLine(true, "Car", FlagBoxInstallState.Failed, null).Text);
            Assert.Equal("Profile has an update", PanelHome.StripLine(true, "Car", FlagBoxInstallState.Outdated, true).Text);
            Assert.Equal("Selected in SimHub", PanelHome.StripLine(false, null, FlagBoxInstallState.UpToDate, true).Text);
            Assert.Equal("Installed in SimHub", PanelHome.StripLine(false, null, FlagBoxInstallState.UpToDate, null).Text);

            // SimHub could not be asked: nothing is said, and no dot.
            var unknown = PanelHome.StripLine(false, null, null, null);
            Assert.Equal(string.Empty, unknown.Text);
            Assert.Null(unknown.DotHex);
            Assert.Null(PanelHome.StripLine(true, "Car", null, null).DotHex);
        }

        // --- Right now: matrix -------------------------------------------------------------------------------

        [Fact]
        public void A_matrix_says_its_content_number_and_idle_display()
        {
            var gear = PanelHome.MatrixLine(1, "gear", null, false);
            Assert.Equal("Matrix 1 · Gear", gear.Text);
            Assert.Equal(Theme.TextSecondary, gear.TextHex);
            Assert.Null(gear.DotHex);
            Assert.Equal("Matrix 3 · Dark", PanelHome.MatrixLine(3, "dark", null, false).Text);
            Assert.Equal(Theme.StatusUpToDate, PanelHome.MatrixLine(1, "gear", true, false).DotHex);
            Assert.Equal(Theme.Caution, PanelHome.MatrixLine(1, "gear", true, true).DotHex);
            var dark = PanelHome.MatrixLine(2, "gear", false, false);
            Assert.Equal("Matrix 2 · Not set", dark.Text);
            Assert.Equal(Theme.Caution, dark.TextHex);
            Assert.Equal(Theme.Caution, dark.DotHex);
            Assert.Equal("Matrix 4", PanelHome.MatrixName(4));
            Assert.Equal("Gear", PanelHome.RestLabel("GEAR"));
            Assert.Equal("Dark", PanelHome.RestLabel(null));
        }

        [Fact]
        public void A_matrix_no_device_shows_is_drawn_dark()
        {
            Assert.True(PanelHome.MatrixDrawsGlyph(null));
            Assert.True(PanelHome.MatrixDrawsGlyph(true));
            Assert.False(PanelHome.MatrixDrawsGlyph(false));
        }

        [Fact]
        public void Each_card_says_its_pages_own_empty_state_as_a_sentence()
        {
            Assert.Equal("No screens yet.", PanelHome.EmptyLine(PanelScreens.NoScreens));
            Assert.Equal(PanelLeds.NoStrips, PanelHome.EmptyLine(PanelLeds.NoStrips));
            Assert.Equal(PanelMatrix.NoPanels, PanelHome.EmptyLine(PanelMatrix.NoPanels));
            Assert.Equal(string.Empty, PanelHome.EmptyLine(null));
            Assert.True(PanelHome.RigEmpty(0, 0, 0));
            Assert.False(PanelHome.RigEmpty(0, 1, 0));
        }

        // --- Quick controls ----------------------------------------------------------------------------------

        /// <summary>The slider edits the brightness in force, labelled for it, as the wheel's buttons do.</summary>
        [Fact]
        public void The_slider_edits_the_brightness_in_force()
        {
            Assert.Equal("Brightness", PanelHome.BrightnessLabel(false));
            Assert.Equal("Night brightness", PanelHome.BrightnessLabel(true));
            Assert.Equal(80, PanelHome.BrightnessInForce(false, 80, 30));
            Assert.Equal(30, PanelHome.BrightnessInForce(true, 80, 30));
            Assert.Equal("80%", PanelHome.Percent(80));
            Assert.Equal("0%", PanelHome.Percent(-4));
            Assert.Equal("100%", PanelHome.Percent(140));
        }

        /// <summary>The page file writes each lighting setting then shows it, and lays the quick controls and the
        /// issue presses side by side only on TwoColumns, never on !Narrow.</summary>
        [Fact]
        public void The_page_shows_each_lighting_write_and_asks_TwoColumns_for_columns()
        {
            var code = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Home.cs"));
            Assert.Contains("Settings.LightsNightBrightness = v; Save(); ShowLightingChange();", code);
            Assert.Contains("Settings.LightsBrightness = v; Save(); ShowLightingChange();", code);
            Assert.Contains("if (v == Settings.LightsNightBrightness) return;", code);
            Assert.Contains("if (v == Settings.LightsBrightness) return;", code);
            Assert.Contains("OnTick(", code);
            Assert.Contains("PanelEmulation.LiveFrame(", code);
            Assert.Contains("TwoColumns", code);
            Assert.DoesNotContain("!Narrow", code);
            Assert.DoesNotContain("RefreshSidebar();", code);
        }
    }
}
