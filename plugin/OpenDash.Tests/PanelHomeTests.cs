// PanelHomeTests.cs: Home's words, numbers and rules -- what each device's row says, which pictures are live,
// what each issue's press does -- held to Main.dc.html, voice.md and what search says about them.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
                PanelSettings.NightModeTitle,
                PanelHome.TryTitle,
            }, labels);
            Assert.Equal(labels.Count, labels.Distinct().Count());
            Assert.All(PanelHome.Search.Skip(3), entry => Assert.Equal(PanelHome.AnchorQuickControls, entry.Route.Anchor));
            // The slider's label is whichever brightness is in force, so neither brightness is a Home entry: a
            // fixed one would land on the other brightness's slider half the time. Settings lists both, and
            // Quick controls carries the word.
            Assert.DoesNotContain(PanelHome.Search, entry => entry.Label == PanelSettings.BrightnessTitle || entry.Label == PanelSettings.NightBrightnessTitle);
            Assert.Contains(PanelSettings.Search, entry => entry.Label == PanelSettings.BrightnessTitle);
            Assert.Contains(PanelSettings.Search, entry => entry.Label == PanelSettings.NightBrightnessTitle);
            Assert.Empty(PanelHome.SearchDrawnOtherwise);
            Assert.Empty(PanelHome.SoonDrawn);
        }

        /// <summary>Each entry's keywords, which are what a driver types who does not know the label.</summary>
        [Fact]
        public void Each_entry_is_found_by_its_keywords()
        {
            Func<string, string[]> keywords = label => PanelHome.Search.Single(entry => entry.Label == label).Keywords;
            Assert.Equal(new[] { "things to fix", "nothing to fix", "attention", "problem", "warning" }, keywords(PanelHome.Title));
            Assert.Equal(new[] { "live", "showing" }, keywords(PanelHome.RightNowTitle));
            Assert.Equal(new[] { "brightness", "night mode" }, keywords(PanelHome.QuickControlsTitle));
            Assert.Equal(new[] { "dark", "dim" }, keywords(PanelSettings.NightModeTitle));
            Assert.Equal(new[] { "try", "emulate", "rig" }, keywords(PanelHome.TryTitle));
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

        /// <summary>Main.dc.html's numbers, which the page draws and nothing else holds. Three besides: the
        /// cards' 280 floor is the brief's (the artboard's grid is repeat(3, minmax(0, 1fr))), and the empty
        /// rig's 12, which the artboard does not draw.</summary>
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
            Assert.Equal(12, PanelHome.EmptyRigGap);
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
            Assert.Equal(HomePress.Go, PanelHome.Press(Issue(PanelAttention.ScreenMissing + "x", PanelPage.Screens, null, PanelIssueAction.Reinstall)));
            Assert.Equal(HomePress.Go, PanelHome.Press(Issue(PanelAttention.ScreensUnclaimed, PanelPage.Screens, null, PanelIssueAction.Navigate)));
            Assert.Equal(HomePress.Go, PanelHome.Press(Issue(PanelAttention.UpdateRestart, PanelPage.Updates, null, PanelIssueAction.Navigate)));
            Assert.Equal(HomePress.Go, PanelHome.Press(null));
            // Check again asks SimHub about the whole rig, so it needs no subject.
            Assert.Equal(HomePress.CheckAgain, PanelHome.Press(Issue(PanelAttention.StripUnselected + "x", PanelPage.Leds, null, PanelIssueAction.CheckAgain)));
        }

        [Fact]
        public void The_press_sits_beside_the_text_only_where_two_blocks_fit()
        {
            Assert.True(PanelHome.PressBeside(true));
            Assert.False(PanelHome.PressBeside(false));
        }

        private static AttentionStrip Brow(FlagBoxInstallState? profile, bool? selected, string device = "Wheel")
        {
            return new AttentionStrip { Name = "Dash brow", Namespace = "LedBar1", DeviceName = device, Profile = profile, Selected = selected };
        }

        /// <summary>
        /// Check again says the profile is selected only when SimHub said so. An issue also leaves the list when
        /// a fact could not be read, and then nothing is claimed fixed: each unread fact is said in the words the
        /// other pages use for it, and the log is pointed at only where a warning was written.
        /// </summary>
        [Fact]
        public void Check_again_says_fixed_only_when_SimHub_says_it_is()
        {
            var before = new PanelIssue(PanelAttention.StripUnselected + "LedBar1", PanelPage.Leds, "LedBar1", "Dash brow's profile is not selected", PanelAttention.UnselectedDetail, null, PanelAttention.CheckAgain, PanelIssueAction.CheckAgain);
            var none = new List<PanelIssue>();

            var still = PanelHome.CheckedAgain(before, new[] { before }, Brow(FlagBoxInstallState.UpToDate, false));
            Assert.Equal("Checked again. Dash brow's profile is not selected.", still.Text);
            Assert.Equal(PanelTone.Caution, still.Tone);

            var selected = PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.UpToDate, true));
            Assert.Equal("Checked again. Dash brow's profile is selected.", selected.Text);
            Assert.Equal(PanelTone.Info, selected.Tone);
            Assert.Equal(PanelTone.Info, PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.Outdated, true)).Tone);

            // The profile is gone from SimHub: the step that is left, where it is taken, and not in the caution's
            // ink. A failed install reads the same, since Check again installs nothing.
            var deleted = PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.NotInstalled, null));
            Assert.Equal("Checked again. Dash brow's profile is not installed. Install it on the LEDs page.", deleted.Text);
            Assert.Equal(PanelTone.Info, deleted.Tone);
            Assert.Equal(deleted.Text, PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.Failed, null)).Text);

            // SimHub's LED settings could not be read, or said nothing of the profile: the Updates page's words.
            var unavailable = new[]
            {
                PanelHome.CheckedAgain(before, none, Brow(null, true)),
                PanelHome.CheckedAgain(before, none, Brow(null, null)),
                PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.Unavailable, null)),
                PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.NotEmbedded, null)),
                PanelHome.CheckedAgain(before, none, null),
                PanelHome.CheckedAgain(null, none, null),
            };
            Assert.All(unavailable, message =>
            {
                Assert.Equal("Checked again. SimHub's LED settings are not available.", message.Text);
                Assert.Equal("Checked again. " + PanelLightRows.Unavailable, message.Text);
                Assert.Equal(PanelTone.Caution, message.Tone);
            });

            // SimHub answered, and has no such device to read the selection from.
            var gone = PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.UpToDate, null, null));
            Assert.Equal("Checked again. Dash brow's LED device is not in SimHub.", gone.Text);
            Assert.Equal(PanelTone.Caution, gone.Tone);
            // The device is there and its selection could not be read, which is logged.
            var unread = PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.UpToDate, null));
            Assert.Equal("Checked again. SimHub could not say whether Dash brow's profile is selected. See SimHub's log.", unread.Text);
            Assert.Equal(PanelTone.Caution, unread.Tone);
            // A read of "not selected" with no issue left says so rather than calling it fixed.
            Assert.Equal("Checked again. Dash brow's profile is not selected.", PanelHome.CheckedAgain(null, none, Brow(FlagBoxInstallState.UpToDate, false)).Text);

            // A strip with no name is still named.
            var blank = new AttentionStrip { Name = " ", Namespace = "LedBar1", DeviceName = "Wheel", Profile = FlagBoxInstallState.UpToDate, Selected = true };
            Assert.Equal("Checked again. This strip's profile is selected.", PanelHome.CheckedAgain(before, none, blank).Text);
            blank.Name = " Dash brow ";
            Assert.Equal("Checked again. Dash brow's profile is selected.", PanelHome.CheckedAgain(before, none, blank).Text);

            // No sentence says "failed", which is for something that failed.
            foreach (var profile in Enum.GetValues(typeof(FlagBoxInstallState)).Cast<FlagBoxInstallState?>())
            {
                foreach (var pick in new bool?[] { null, true, false })
                {
                    Assert.DoesNotContain("fail", PanelHome.CheckedAgain(before, none, Brow(profile, pick)).Text, StringComparison.OrdinalIgnoreCase);
                }
            }
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
        /// band D. The artboard's "Lap times · Gear · Relative" is the same line, shortened, where the panel's
        /// body reads "Lap times · Gear, speed, revs · Relative".</summary>
        [Fact]
        public void A_face_says_the_page_each_zone_is_on()
        {
            var face = Screen(Contract.KindFace, 1280, 480);
            Assert.Equal("Lap times · Gear, speed, revs · Relative · Fuel", PanelHome.ScreenShows(new OpenDashSettings(), face));
            face.Face.Zones[Array.IndexOf(Contract.FaceZoneLetters, "A")] = 1;
            Assert.Equal("Lap times · Gear alone · Relative · Fuel", PanelHome.ScreenShows(new OpenDashSettings(), face));
            // A copy of a screen with no zones of its own reads the rig's screen of the same namespace, and one
            // at a size no face ships is drawn at the reference face.
            var settings = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            var held = Screen(Contract.KindFace, 1280, 480);
            held.Face.Zones[Array.IndexOf(Contract.FaceZoneLetters, "A")] = 1;
            settings.Rig.Add(held);
            var copy = Screen(Contract.KindFace, 1280, 480);
            copy.Face = null;
            Assert.Equal("Lap times · Gear alone · Relative · Fuel", PanelHome.ScreenShows(settings, copy));
            Assert.Equal(string.Empty, PanelHome.ScreenShows(null, copy));
            var odd = Screen(Contract.KindFace, 1000, 500);
            Assert.Null(odd.FaceSize);
            Assert.Equal(string.Join(PanelHome.Separator, PanelFacePlan.ZoneOrder(Contract.ReferenceFace).Select(letter => FacePages.NameOf(letter, odd.Face.Zone(letter)))),
                PanelHome.ScreenShows(new OpenDashSettings(), odd));
            Assert.NotEqual(string.Empty, PanelHome.ScreenShows(new OpenDashSettings(), odd));
            // A portrait face draws its body as a column, A first.
            var portrait = Screen(Contract.KindFace, 600, 686);
            Assert.StartsWith("Gear, speed, revs · Lap times · ", PanelHome.ScreenShows(new OpenDashSettings(), portrait));
        }

        /// <summary>The line follows the live zone pages a wheel's cycle press writes, since the tick reads it
        /// again rather than keeping the line the build drew.</summary>
        [Fact]
        public void A_face_line_follows_a_zone_the_wheel_cycles()
        {
            var face = Screen(Contract.KindFace, 1280, 480);
            var settings = new OpenDashSettings();
            var before = PanelHome.ScreenLine(settings, face, true, false).Text;
            face.Face.Cycle("C");
            var after = PanelHome.ScreenLine(settings, face, true, false).Text;
            Assert.NotEqual(before, after);
            Assert.Equal(PanelHome.ScreenShows(settings, face), after);
            face.Face.CycleBack("C");
            Assert.Equal(before, PanelHome.ScreenLine(settings, face, true, false).Text);
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
            Assert.False(PanelHome.PitWallPortrait(wall));

            Assert.Equal("21 of 21 modules", PanelHome.ModulesLine(null));
            Assert.Equal("21 of 21 modules", PanelHome.ModulesLine(new bool[Modules.Count]));
            var some = new bool[Modules.Count];
            some[0] = some[4] = some[16] = true;
            Assert.Equal("3 of 21 modules", PanelHome.ModulesLine(some));
            // A rotation longer than the catalogue, as an older settings file can carry, counts the catalogue's.
            var longer = Enumerable.Repeat(true, Modules.Count + 4).ToArray();
            Assert.Equal("21 of 21 modules", PanelHome.ModulesLine(longer));
            var phone = Screen(Contract.KindCompanion, 1080, 2400);
            // A new companion's rotation is Contract.DefaultModules: the catalogue without the three it leaves off.
            Assert.Equal("18 of 21 modules", PanelHome.ScreenShows(new OpenDashSettings(), phone));
            Assert.Equal(18, Contract.DefaultModules().Count(on => on));

            // A card face names only the cards its package reads, which are the first ones.
            Assert.Equal("Speed · Current lap", PanelHome.ScreenShows(new OpenDashSettings(), Screen(Contract.KindSlots, 480, 480)));
            Assert.Equal("Speed · Current lap · Last lap · Best lap · Delta · Position",
                PanelHome.ScreenShows(new OpenDashSettings(), Screen(Contract.KindSlots, 800, 800)));
            Assert.Equal("Speed · Current lap · Last lap · Best lap · Delta · Position · Session · Fuel",
                PanelHome.ScreenShows(new OpenDashSettings(), Screen(Contract.KindSlots, 1280, 480)));
            Assert.Equal("Speed · Current lap · Last lap · Best lap · Delta · Position · Session · Fuel · Fuel laps · TC · ABS · Tyre temps",
                PanelHome.ScreenShows(new OpenDashSettings(), Screen(Contract.KindSlots, 1920, 480)));
            // A size no package is drawn at names nothing rather than twelve cards it may not draw.
            Assert.Equal(string.Empty, PanelHome.ScreenShows(new OpenDashSettings(), Screen(Contract.KindSlots, 1024, 600)));
            Assert.Equal(string.Empty, PanelHome.ScreenShows(new OpenDashSettings(), null));
        }

        /// <summary>
        /// The portrait pit wall ("OpenDash Pit wall portrait", 1080 × 1920) is its own package, one page of
        /// four zones, with no Race, Tower or Telemetry page: its line is its zones' pages, never a landscape
        /// page, whatever PitWallPage a resize from landscape left behind.
        /// </summary>
        [Fact]
        public void A_portrait_pit_wall_says_its_zones_never_a_landscape_page()
        {
            var portrait = Screen(Contract.KindPitWall, 1080, 1920);
            Assert.True(PanelHome.PitWallPortrait(portrait));
            Assert.Equal("Fuel · Tyres · Relative · Opponents", PanelHome.ScreenShows(new OpenDashSettings(), portrait));
            portrait.PitWallPage = 1;
            Assert.Equal("Fuel · Tyres · Relative · Opponents", PanelHome.ScreenShows(new OpenDashSettings(), portrait));
            portrait.SetZonePage("PortraitC", 5);
            Assert.Equal("Fuel · Tyres · Leaderboard · Opponents", PanelHome.ScreenShows(new OpenDashSettings(), portrait));
            foreach (var page in Contract.PitWallPageNames) Assert.DoesNotContain(page + " page", PanelHome.ScreenShows(new OpenDashSettings(), portrait));
            // The line a Right now row draws for it, dot and all.
            Assert.Equal("Fuel · Tyres · Leaderboard · Opponents", PanelHome.ScreenLine(new OpenDashSettings(), portrait, true, false).Text);
            Assert.False(PanelHome.PitWallPortrait(Screen(Contract.KindFace, 600, 686)));
            Assert.False(PanelHome.PitWallPortrait(null));
            Assert.Equal(string.Empty, PanelHome.PortraitZones(null));
        }

        /// <summary>The slots each card face reads are the manifest's, which e2e.test.ts pins: a package that
        /// gains or loses a slot fails here too, rather than Home naming a card its screen does not draw.</summary>
        [Fact]
        public void Each_card_face_reads_the_slots_the_manifest_gives_it()
        {
            var e2e = System.IO.File.ReadAllText(System.IO.Path.Combine(RepoPaths.Root(), "packages", "dash", "test", "e2e.test.ts"));
            var pinned = Regex.Matches(e2e, @"width: (\d+), height: (\d+), slots: ([1-9]\d*)")
                .Cast<Match>()
                .Select(m => new[] { int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value) })
                .ToList();
            Assert.NotEmpty(pinned);
            foreach (var face in pinned) Assert.Equal(face[2], PanelHome.SlotsRead(face[0], face[1]));
            Assert.Equal(2, PanelHome.SlotsRead(480, 480));
            Assert.Equal(6, PanelHome.SlotsRead(800, 800));
            Assert.Equal(0, PanelHome.SlotsRead(0, 0));
            Assert.All(PanelHome.SlotFaces, face => Assert.InRange(face[2], 1, Contract.SlotCount));
            // And the other way: no row here that the manifest does not pin.
            Assert.All(PanelHome.SlotFaces, face => Assert.Contains(pinned, p => p[0] == face[0] && p[1] == face[1] && p[2] == face[2]));
        }

        /// <summary>What SimHub is missing outranks what the screen shows, in the Screens cards' own words and
        /// inks; a state nobody could read has no dot rather than a green one.</summary>
        [Fact]
        public void A_screen_says_what_SimHub_is_missing_before_what_it_shows()
        {
            var face = Screen(Contract.KindFace, 1280, 480);
            var settings = new OpenDashSettings();
            var missing = PanelHome.ScreenLine(settings, face, false, true);
            // The Screens card's own constant, not a copy: when the card's word moves, Home's moves with it.
            Assert.Equal(PanelScreens.Missing, missing.Text);
            Assert.Equal(Theme.StatusFailed, missing.TextHex);
            Assert.Equal(Theme.StatusFailed, missing.DotHex);
            var restart = PanelHome.ScreenLine(settings, face, true, true);
            // The ruled phrase for a dashboard SimHub has not read (voice ruling 23, scenario HM-03).
            Assert.Equal("Restart SimHub to load it", restart.Text);
            Assert.Equal(PanelHome.RestartToLoad, restart.Text);
            Assert.Equal(Theme.Caution, restart.TextHex);
            Assert.Equal(Theme.Caution, restart.DotHex);
            var fine = PanelHome.ScreenLine(settings, face, true, false);
            Assert.Equal(PanelHome.ScreenShows(settings, face), fine.Text);
            Assert.Equal(Theme.TextSecondary, fine.TextHex);
            Assert.Equal(Theme.StatusUpToDate, fine.DotHex);
            Assert.Null(PanelHome.ScreenLine(settings, face, null, false).DotHex);
        }

        /// <summary>
        /// One phrase for the one state on both pages: the moment PanelScreens carries the restart state's
        /// phrase (the Screens branch's RestartToLoad), Home's must be it, and Home names no state with a
        /// constant of its own besides that one.
        /// </summary>
        [Fact]
        public void Home_names_a_screens_state_as_its_card_does()
        {
            var cards = typeof(PanelScreens).GetField("RestartToLoad");
            if (cards != null) Assert.Equal(PanelHome.RestartToLoad, (string)cards.GetValue(null));
            Assert.Null(typeof(PanelHome).GetField("Missing"));
            Assert.Null(typeof(PanelHome).GetField("NotInSimHubYet"));
        }

        /// <summary>A size nobody knows is not drawn, and nor is one the name already says.</summary>
        [Fact]
        public void A_screen_gives_its_size_unless_nobody_knows_it_or_its_name_says_it()
        {
            Assert.Equal("1280 × 480", PanelHome.ScreenSize(Screen(Contract.KindFace, 1280, 480)));
            // A migrated face whose folder named no size.
            Assert.Equal(string.Empty, PanelHome.ScreenSize(new ScreenInstance { Kind = Contract.KindFace }));
            Assert.Equal(string.Empty, PanelHome.ScreenSize(null));
            // An unnamed package's screen is named by its size, and would say it twice.
            var bySize = new ScreenInstance { Namespace = "Face1280x480", Kind = Contract.KindFace, Width = 1280, Height = 480 };
            bySize.Normalise();
            Assert.Equal("1280 × 480", bySize.Name);
            Assert.Equal(string.Empty, PanelHome.ScreenSize(bySize));
        }

        // --- Right now: LEDs ---------------------------------------------------------------------------------

        [Fact]
        public void A_strip_is_drawn_with_its_own_shape()
        {
            Assert.Equal(new[] { 3, 9 }, PanelHome.StripSpan("3-9-3"));
            Assert.Equal(new[] { 0, 15 }, PanelHome.StripSpan("0-15-0"));
            Assert.Equal(new[] { 3, 9 }, PanelHome.StripSpan("nonsense"));
            // As Main.dc.html and the LEDs cards write a shape: the counts dotted, a bare run its one count.
            Assert.Equal("3 · 9 · 3", PanelHome.StripShape(new LedBar { Shape = "3-9-3" }));
            Assert.Equal("15", PanelHome.StripShape(new LedBar { Shape = "0-15-0" }));
            Assert.Equal("3 · 9 · 3", PanelHome.StripShape(new LedBar { Shape = PanelLights.FanatecShapeId }));
            Assert.Equal(string.Empty, PanelHome.StripShape(new LedBar()));
            // An id the panel cannot read is written as the Updates page writes it, never dropped.
            Assert.Equal(PanelLightRows.ShapeLabel("x"), PanelHome.StripShape(new LedBar { Shape = "x" }));
            Assert.NotEqual(string.Empty, PanelHome.StripShape(new LedBar { Shape = "x" }));
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
            // An update waiting, or SimHub not saying, does not stop the car's own lights.
            Assert.True(PanelHome.StripLive(true, car, rpm, FlagBoxInstallState.Outdated, true));
            Assert.True(PanelHome.StripLive(true, car, rpm, FlagBoxInstallState.Unavailable, true));
            Assert.True(PanelHome.StripLive(true, car, rpm, FlagBoxInstallState.NotEmbedded, true));
        }

        private static void Line(HomeLine line, string text, string textHex, string dotHex)
        {
            Assert.Equal(text, line.Text);
            Assert.Equal(textHex, line.TextHex);
            Assert.Equal(dotHex, line.DotHex);
        }

        /// <summary>
        /// Every state a strip's line can be in, in the LEDs cards' words and with its ink and its dot: amber
        /// only where the attention card has a row, and green only for a selection SimHub reported.
        /// </summary>
        [Fact]
        public void A_strip_says_what_SimHub_lacks_then_the_car_then_the_profile()
        {
            Line(PanelHome.StripLine(true, "Porsche 911 GT3 R (992)", FlagBoxInstallState.UpToDate, true), "Car's own rev lights · Porsche 911 GT3 R (992)", Theme.TextSecondary, Theme.StatusUpToDate);
            Line(PanelHome.StripLine(true, " ", FlagBoxInstallState.UpToDate, true), "Car's own rev lights", Theme.TextSecondary, Theme.StatusUpToDate);
            Line(PanelHome.StripLine(true, " Porsche ", FlagBoxInstallState.UpToDate, true), "Car's own rev lights · Porsche", Theme.TextSecondary, Theme.StatusUpToDate);
            // The #369 switch's own noun, read from the switch.
            Assert.Equal(PanelLeds.CarRevLightsTitle, PanelHome.CarLightsLine);
            // Live, but nobody could read the selection: the car's lights, and no dot.
            Line(PanelHome.StripLine(true, "Car", FlagBoxInstallState.UpToDate, null), "Car's own rev lights · Car", Theme.TextSecondary, null);
            Line(PanelHome.StripLine(true, "Car", null, null), "Car's own rev lights · Car", Theme.TextSecondary, null);

            Line(PanelHome.StripLine(false, null, FlagBoxInstallState.UpToDate, false), "Not selected in SimHub", Theme.Caution, Theme.Caution);
            // Not selected outranks the update: nothing shows until it is selected.
            Line(PanelHome.StripLine(false, null, FlagBoxInstallState.Outdated, false), "Not selected in SimHub", Theme.Caution, Theme.Caution);
            Line(PanelHome.StripLine(true, "Car", FlagBoxInstallState.Outdated, true), "Update available", Theme.StatusUpdateAvailable, Theme.StatusUpdateAvailable);
            Line(PanelHome.StripLine(false, null, FlagBoxInstallState.Outdated, null), "Update available", Theme.StatusUpdateAvailable, Theme.StatusUpdateAvailable);
            Line(PanelHome.StripLine(false, null, FlagBoxInstallState.UpToDate, true), "Showing", Theme.TextSecondary, Theme.StatusUpToDate);
            Line(PanelHome.StripLine(false, null, FlagBoxInstallState.UpToDate, null), "Installed", Theme.TextSecondary, null);

            // Not there: the LEDs card's Install press, in the secondary ink. A failed install is one that is not
            // installed, as the LEDs card says it: nothing Home reads ever reports Failed.
            Line(PanelHome.StripLine(false, null, FlagBoxInstallState.NotInstalled, null), "Not installed", Theme.TextSecondary, null);
            Line(PanelHome.StripLine(false, null, FlagBoxInstallState.NotInstalled, false), "Not installed", Theme.TextSecondary, null);
            Line(PanelHome.StripLine(true, "Car", FlagBoxInstallState.Failed, null), "Not installed", Theme.TextSecondary, null);

            // SimHub could not be asked, or this build carries no profile: nothing is said, and no dot.
            Line(PanelHome.StripLine(false, null, null, null), string.Empty, Theme.TextSecondary, null);
            Line(PanelHome.StripLine(false, null, FlagBoxInstallState.Unavailable, true), string.Empty, Theme.TextSecondary, null);
            Line(PanelHome.StripLine(false, null, FlagBoxInstallState.NotEmbedded, null), string.Empty, Theme.TextSecondary, null);
            Assert.Null(PanelHome.StripLine(true, "Car", FlagBoxInstallState.Unavailable, true).DotHex);
            Assert.Null(PanelHome.StripLine(true, "Car", FlagBoxInstallState.NotEmbedded, true).DotHex);

            Assert.Equal(PanelCopy.Installed, PanelHome.StripInstalled);
            Assert.Equal(PanelCopy.NotInstalled, PanelHome.StripNotInstalled);
            Assert.Null(typeof(PanelHome).GetField("StripInstallFailed"));
        }

        /// <summary>
        /// An amber line under "Nothing to fix" is a warning nobody can act on. Every strip line Home draws in
        /// the caution's ink, or with its dot, has the attention card's row for the same strip.
        /// </summary>
        [Fact]
        public void Every_amber_strip_line_has_its_row_in_the_attention_card()
        {
            var states = new FlagBoxInstallState?[] { null }.Concat(Enum.GetValues(typeof(FlagBoxInstallState)).Cast<FlagBoxInstallState?>()).ToList();
            var selections = new bool?[] { null, true, false };
            foreach (var profile in states)
            {
                foreach (var selected in selections)
                {
                    foreach (var live in new[] { false, true })
                    {
                        var line = PanelHome.StripLine(live, "Car", profile, selected);
                        var amber = line.TextHex == Theme.Caution || line.DotHex == Theme.Caution;
                        var input = new AttentionInput();
                        input.Strips.Add(new AttentionStrip { Name = "Dash brow", Namespace = "LedBar1", DeviceName = "Wheel", Profile = profile, Selected = selected });
                        var issues = PanelAttention.Find(input);
                        var rowed = issues.Any(issue => issue.Subject == "LedBar1");
                        Assert.True(!amber || rowed, "An amber \"" + line.Text + "\" for " + profile + "/" + selected + " has no attention row.");
                    }
                }
            }
        }

        // --- Right now: matrix -------------------------------------------------------------------------------

        /// <summary>A renamed matrix says its content number and idle display; one still called by its number
        /// says the idle display alone. A slot no device shows is in the Matrix card's words, and that is the
        /// only amber a matrix row carries, since the attention card has its row.</summary>
        [Fact]
        public void A_matrix_says_its_content_number_and_idle_display()
        {
            Line(PanelHome.MatrixLine(1, "Wheel matrix", "gear", null), "Matrix 1 · Gear", Theme.TextSecondary, null);
            Line(PanelHome.MatrixLine(1, "Matrix 1", "gear", null), "Gear", Theme.TextSecondary, null);
            Line(PanelHome.MatrixLine(1, null, "gear", null), "Matrix 1 · Gear", Theme.TextSecondary, null);
            Assert.Equal("Matrix 3 · Dark", PanelHome.MatrixLine(3, "Pit board", "dark", null).Text);
            Assert.Equal("Dark", PanelHome.MatrixLine(3, "Matrix 3", "dark", null).Text);
            // Only its own name, exactly: a name that differs in case is the driver's, and keeps the number.
            Assert.Equal("Matrix 3 · Dark", PanelHome.MatrixLine(3, "matrix 3", "dark", null).Text);
            Assert.Equal("Dark", PanelHome.MatrixLine(3, " Matrix 3 ", "dark", null).Text);
            Assert.Equal(Theme.StatusUpToDate, PanelHome.MatrixLine(1, "Matrix 1", "gear", true).DotHex);
            Line(PanelHome.MatrixLine(2, "Pit board", "gear", false), "Matrix 2 · Not shown in SimHub", Theme.Caution, Theme.Caution);
            Line(PanelHome.MatrixLine(2, "Matrix 2", "gear", false), "Not shown in SimHub", Theme.Caution, Theme.Caution);
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
            Assert.Equal("No x.", PanelHome.EmptyLine(" No x "));
            Assert.Equal("No x.", PanelHome.EmptyLine("No x."));
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

        /// <summary>The page's source as code, with every run of whitespace one space, so a pin is on whole
        /// statements rather than on fragments a wrong branch still contains.</summary>
        private static string PageCode()
        {
            var code = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Home.cs"));
            return Regex.Replace(code, @"\s+", " ");
        }

        /// <summary>The slider writes the brightness in force, under that brightness's label and value, and
        /// never saves a drag that lands where it started.</summary>
        [Fact]
        public void The_slider_writes_the_brightness_its_label_names()
        {
            var code = PageCode();
            Assert.Contains("var nightOn = Settings.LightsNightMode;", code);
            Assert.Contains("PanelHome.BrightnessInForce(nightOn, Settings.LightsBrightness, Settings.LightsNightBrightness)", code);
            Assert.Contains("Ui.Eyebrow(PanelHome.BrightnessLabel(nightOn))", code);
            Assert.Contains("if (Settings.LightsNightMode) { if (v == Settings.LightsNightBrightness) return; Settings.LightsNightBrightness = v; Save(); ShowLightingChange(); } else { if (v == Settings.LightsBrightness) return; Settings.LightsBrightness = v; Save(); ShowLightingChange(); }", code);
            Assert.Contains("Settings.LightsNightMode = on; Save(); ShowLightingChange();", code);
        }

        /// <summary>Home draws night mode and a brightness as controls, so it says so before anything else:
        /// without it a wheel's press leaves the slider on the other brightness's label and value.</summary>
        [Fact]
        public void The_page_says_it_draws_lighting_first()
        {
            Assert.Contains("private FrameworkElement BuildHomePage(PanelRoute to) { DrawsLighting();", PageCode());
        }

        /// <summary>The pictures are live only where PanelHome says so: the car's run on a strip StripLive
        /// allows, the idle glyph on a matrix MatrixDrawsGlyph allows, and both repainted on the tick.</summary>
        [Fact]
        public void The_page_draws_live_only_what_PanelHome_allows()
        {
            var code = PageCode();
            Assert.Contains("var live = cars != null && PanelHome.StripLive(cars.Ready, Settings.BarRpmStyle(ns), Settings.BarCentre(ns), profile, selected);", code);
            Assert.Contains("var run = live ? cars.Run(strip.Centre) : null;", code);
            Assert.Contains("PanelEmulation.LiveFrame(run, strip.Ends, strip.Centre)", code);
            Assert.Contains("PanelEmulation.LiveFrame(null, strip.Ends, strip.Centre)", code);
            Assert.Contains("OnTick(() => { foreach (var strip in live) HomePaintStrip(strip); });", code);
            Assert.Contains("if (live.Count > 0) OnTick(", code);
            Assert.Contains("OnTick(() => { foreach (var screen in shown) HomePaintScreen(screen); });", code);
            Assert.Contains("if (shown.Count > 0) OnTick(", code);
            Assert.Contains("var line = PanelHome.ScreenLine(Settings, screen, row.Installed, row.Restart);", code);
            Assert.Contains("var shown = facts == null ? null : facts.Shown;", code);
            Assert.Contains("if (PanelHome.MatrixDrawsGlyph(shown))", code);
            Assert.Contains("PanelEmulation.MatrixFrame(GlyphSheet, PanelEmulation.Idle, options)", code);
            Assert.Contains("PanelHome.MatrixLine(slot, title, Settings.MatrixRest(slot), shown)", code);
        }

        /// <summary>What each press does: the issue's own call, and each row and link to its own page.</summary>
        [Fact]
        public void Each_press_makes_its_own_call()
        {
            var code = PageCode();
            Assert.Contains("case HomePress.CheckAgain: CheckAgain(); Say(PanelHome.CheckedAgain(issue, issues, StripFacts(issue.Subject))); return;", code);
            Assert.Contains("InstallScreenAgain(screen); return;", code);
            Assert.Contains("case HomePress.Open: Open(issue.Page, issue.Subject, issue.Anchor); return;", code);
            Assert.Contains("default: Go(issue.Route); return;", code);
            Assert.Contains("HomeRow(dock, () => Open(PanelPage.Screens, ns))", code);
            Assert.Contains("() => Open(PanelPage.Leds, ns)", code);
            Assert.Contains("HomeRow(dock, () => Open(PanelPage.Matrix, id))", code);
            Assert.Contains("rig.Click += (sender, args) => Go(PanelPage.Rig);", code);
            Assert.Contains("open.Click += (sender, args) => Go(page);", code);
            Assert.Contains("Ui.DashedAddCard(PanelAddScreen.SectionTitle, () => { Go(PanelPage.Screens); ShowAddScreen(); })", code);
            Assert.Contains("if (issues.Count > 0) sections.Add(Ui.Anchor(HomeAttentionCard(), PanelHome.AnchorAttention));", code);
            Assert.Contains("PanelAttention.Has(issues, PanelAttention.ScreenRestart, screen.Namespace)", code);
            Assert.Contains("Installed = facts == null ? null : facts.Installed,", code);
            Assert.Contains("HomeCard(PanelPage.Screens, HomeScreenRows(screens), PanelScreens.NoScreens)", code);
            Assert.Contains("HomeCard(PanelPage.Leds, HomeStripRows(strips, dim), PanelLeds.NoStrips)", code);
            Assert.Contains("PanelMatrix.NoPanels)", code);
        }

        /// <summary>The empty rig is the add tile with its sentence, in place of Right now and the quick
        /// controls, which have nothing to show or act on. Only an issue the headline counts (an update, the
        /// one kind an empty rig can have) is drawn above it, so the count is never over no rows.</summary>
        [Fact]
        public void The_empty_rig_stands_in_place_of_right_now_and_the_quick_controls()
        {
            var code = PageCode();
            Assert.Contains("if (issues.Count > 0) sections.Add(Ui.Anchor(HomeAttentionCard(), PanelHome.AnchorAttention)); if (PanelHome.RigEmpty(screens.Count, strips.Count, matrices.Count)) { sections.Add(HomeEmptyRig()); } else { sections.Add(Ui.Anchor(HomeRightNow(screens, strips, matrices), PanelHome.AnchorRightNow)); sections.Add(Ui.Anchor(HomeQuickControls(), PanelHome.AnchorQuickControls)); }", code);
            Assert.Contains("Ui.Prose(PanelCopy.EmptyRig, PanelHome.DetailSize)", code);
            Assert.Contains("new Thickness(0, PanelHome.EmptyRigGap, 0, 0)", code);
        }

        /// <summary>The presses sit beside their text and the quick controls side by side only on TwoColumns,
        /// never on !Narrow; a stacked press hangs its row's icon from the top.</summary>
        [Fact]
        public void The_page_asks_TwoColumns_for_columns()
        {
            var code = PageCode();
            Assert.Contains("var beside = PanelHome.PressBeside(TwoColumns);", code);
            Assert.Contains("var align = hasSteps || !beside ? VerticalAlignment.Top : VerticalAlignment.Center;", code);
            Assert.Contains("var beside = TwoColumns;", code);
            Assert.DoesNotContain("!Narrow", code);
            Assert.DoesNotContain("RefreshSidebar();", code);
        }

        /// <summary>A long name trims before the size after it is cut, and a line with nothing to say takes its
        /// gap with it.</summary>
        [Fact]
        public void A_long_name_trims_before_its_size_and_an_empty_line_takes_its_gap()
        {
            var code = PageCode();
            Assert.Contains("var dock = new DockPanel { LastChildFill = true, HorizontalAlignment = HorizontalAlignment.Left };", code);
            Assert.Contains("DockPanel.SetDock(figure, Dock.Right);", code);
            Assert.Contains("rows.Add(HomeRow(Ui.VStack(0, top, host, strip.Line), () => Open(PanelPage.Leds, ns)));", code);
            Assert.Contains("strip.Line.Margin = new Thickness(0, PanelHome.StripRowGap, 0, 0);", code);
            Assert.Contains("text.Visibility = line.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;", code);
        }
    }
}
