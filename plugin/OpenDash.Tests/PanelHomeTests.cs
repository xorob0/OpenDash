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

        /// <summary>Every section heading and every quick-controls label but the brightness's is found, and lands
        /// on Home: the brightness's label is whichever brightness is in force, so neither is an entry.</summary>
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

        /// <summary>Main.dc.html's numbers, which the page draws and nothing else holds. Two besides: the
        /// cards' 280 floor is the brief's (the artboard's grid is repeat(3, minmax(0, 1fr))), and the empty
        /// rig's 12, which the artboard does not draw. The press's ceiling and threshold are held with the
        /// rule they serve.</summary>
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

        /// <summary>A press is a row's trailing action and sits beside its text wherever the text keeps 300 of
        /// room beside the widest press, the rail's widths included; it goes under the text only narrower.</summary>
        [Fact]
        public void The_press_sits_beside_the_text_wherever_the_text_keeps_its_room()
        {
            Assert.Equal(200, PanelHome.PressMaxWidth);
            Assert.Equal(300, PanelHome.PressTextMinWidth);
            Assert.Equal(600, PanelHome.PressBesideFrom);
            Assert.Equal(PanelHome.PressBesideFrom,
                PanelHome.IssuePaddingX * 2 + PanelHome.IconWell + PanelHome.IconGap + PanelHome.IconGap + PanelHome.PressMaxWidth + PanelHome.PressTextMinWidth);
            Assert.True(PanelHome.PressBeside(600));
            Assert.False(PanelHome.PressBeside(599));
            // The rail's content, from a control of 760 with a scroll bar, keeps its presses beside, as the
            // full sidebar's does.
            Assert.Equal(PanelLayout.Rail, PanelShell.Layout(760));
            Assert.True(PanelHome.PressBeside(PanelShell.ContentWidth(760, 17)));
            Assert.True(PanelHome.PressBeside(PanelShell.TwoColumnFrom));
            // The column has no ceiling, and a 4K window's keeps its presses beside too.
            Assert.True(PanelHome.PressBeside(PanelShell.ContentWidth(3840, 17)));
            Assert.False(PanelHome.PressBeside(400));
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
            // ink. A failed install reads the same, since Check again installs nothing. A result and its step
            // are two sentences, the ceiling, so "Checked again." goes.
            var deleted = PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.NotInstalled, null));
            Assert.Equal("Dash brow's profile is not installed. Install it on the LEDs page.", deleted.Text);
            Assert.Equal(PanelTone.Info, deleted.Tone);
            Assert.Equal(deleted.Text, PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.Failed, null)).Text);

            // SimHub's LED settings could not be read, or said nothing of the profile: the Updates page's words.
            var unavailable = new[]
            {
                PanelHome.CheckedAgain(before, none, Brow(null, true)),
                PanelHome.CheckedAgain(before, none, Brow(null, null)),
                PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.Unavailable, null)),
                PanelHome.CheckedAgain(before, none, null),
                PanelHome.CheckedAgain(null, none, null),
            };
            Assert.All(unavailable, message =>
            {
                Assert.Equal("Checked again. SimHub's LED settings are not available.", message.Text);
                Assert.Equal("Checked again. " + PanelLightRows.Unavailable, message.Text);
                Assert.Equal(PanelTone.Caution, message.Tone);
            });

            // SimHub answered, and this build carries no profile for the strip: not a read that failed, and said
            // as the LEDs page says it, whatever the device.
            var unshipped = PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.NotEmbedded, null));
            Assert.Equal("Checked again. This build ships no profile for Dash brow.", unshipped.Text);
            Assert.Equal(PanelTone.Caution, unshipped.Tone);
            Assert.Equal(unshipped.Text, PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.NotEmbedded, true, null)).Text);

            // SimHub answered, and has no such device to read the selection from: the LEDs page's one phrase for
            // it, and its step, where it is taken, never the old "LED device".
            var gone = PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.UpToDate, null, null));
            Assert.Equal("Dash brow's device is not in SimHub. Choose one on the LEDs page.", gone.Text);
            Assert.Equal(PanelTone.Caution, gone.Tone);
            // Removing the device takes its profiles with it, so the profile reads as not installed too; the
            // device comes first, since the LEDs page has no Install for a strip whose device is not listed.
            Assert.Equal(gone.Text, PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.NotInstalled, null, null)).Text);
            Assert.Equal(gone.Text, PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.Failed, null, " ")).Text);
            Assert.Equal(gone.Text, PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.UpToDate, true, null)).Text);
            // The device is there and its selection could not be read, which is logged.
            var unread = PanelHome.CheckedAgain(before, none, Brow(FlagBoxInstallState.UpToDate, null));
            Assert.Equal("Could not read whether Dash brow's profile is selected. See SimHub's log.", unread.Text);
            Assert.Equal(PanelTone.Caution, unread.Tone);
            // A read of "not selected" with no issue left says so rather than calling it fixed, in the caution's ink.
            var notSelected = PanelHome.CheckedAgain(null, none, Brow(FlagBoxInstallState.UpToDate, false));
            Assert.Equal("Checked again. Dash brow's profile is not selected.", notSelected.Text);
            Assert.Equal(PanelTone.Caution, notSelected.Tone);

            // A strip with no name is still named, capitalised only where the name starts the sentence.
            var blank = new AttentionStrip { Name = " ", Namespace = "LedBar1", DeviceName = "Wheel", Profile = FlagBoxInstallState.UpToDate, Selected = true };
            Assert.Equal("Checked again. This strip's profile is selected.", PanelHome.CheckedAgain(before, none, blank).Text);
            blank.Selected = null;
            Assert.Equal("Could not read whether this strip's profile is selected. See SimHub's log.", PanelHome.CheckedAgain(before, none, blank).Text);
            blank.Profile = FlagBoxInstallState.NotInstalled;
            Assert.Equal("This strip's profile is not installed. Install it on the LEDs page.", PanelHome.CheckedAgain(before, none, blank).Text);
            blank.DeviceName = null;
            Assert.Equal("This strip's device is not in SimHub. Choose one on the LEDs page.", PanelHome.CheckedAgain(before, none, blank).Text);
            blank.Profile = FlagBoxInstallState.NotEmbedded;
            Assert.Equal("Checked again. This build ships no profile for this strip.", PanelHome.CheckedAgain(before, none, blank).Text);
            blank.Name = " Dash brow ";
            blank.DeviceName = "Wheel";
            blank.Profile = FlagBoxInstallState.UpToDate;
            blank.Selected = true;
            Assert.Equal("Checked again. Dash brow's profile is selected.", PanelHome.CheckedAgain(before, none, blank).Text);

            // Two sentences at most, and "Checked again." only before a single one.
            foreach (var profile in new FlagBoxInstallState?[] { null }.Concat(Enum.GetValues(typeof(FlagBoxInstallState)).Cast<FlagBoxInstallState?>()))
            {
                foreach (var pick in new bool?[] { null, true, false })
                {
                    foreach (var device in new[] { "Wheel", null })
                    {
                        var text = PanelHome.CheckedAgain(before, none, Brow(profile, pick, device)).Text;
                        var sentences = Regex.Matches(text, @"\.( |$)").Count;
                        Assert.InRange(sentences, 1, 2);
                        if (text.StartsWith(PanelHome.Checked, StringComparison.Ordinal)) Assert.Equal(2, sentences);
                    }
                }
            }

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
            // With no settings to read the slots from, a card face names nothing rather than guessing.
            Assert.Equal(string.Empty, PanelHome.ScreenShows(null, Screen(Contract.KindSlots, 480, 480)));
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
            // The Screens card's own constant, not a copy: when the card's word moves, Home's moves with it. The
            // ruled word is held here too (the inventory's "Missing", scenario HM-04), so a Screens page that
            // says otherwise fails Home until the page and the ruling agree.
            Assert.Equal(PanelScreens.Missing, missing.Text);
            Assert.Equal("Missing", missing.Text);
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

        /// <summary>
        /// Check again states a device SimHub does not list and a profile the build does not carry in the LEDs
        /// page's own phrases: the moment PanelLeds carries them, they are the ones Home's sentences are built on.
        /// </summary>
        [Fact]
        public void Check_again_says_a_strips_facts_as_the_LEDs_page_does()
        {
            const string fact = "'s device is not in SimHub.";
            Assert.StartsWith(fact + " ", PanelHome.CheckedNoDevice);
            var listed = typeof(PanelLeds).GetField("DeviceNotListed");
            if (listed != null) Assert.StartsWith("This strip" + fact + " ", (string)listed.GetValue(null));
            var shipped = typeof(PanelLeds).GetField("NoProfileForStrip");
            if (shipped != null) Assert.Equal(PanelHome.CheckedNoProfile + "this strip.", (string)shipped.GetValue(null));
        }

        /// <summary>
        /// Home's strip and matrix words are copies until the LEDs and Matrix pages carry their own
        /// (PanelLeds.StateText and its words, PanelMatrix.NotShown). The moment they do, Home's must be
        /// them, state for state, so the two pages cannot say different things for one strip.
        /// </summary>
        [Fact]
        public void Home_names_a_strips_and_a_matrixs_state_as_their_cards_do()
        {
            Action<Type, string, string> same = (type, field, home) =>
            {
                var theirs = type.GetField(field);
                if (theirs != null) Assert.Equal(home, (string)theirs.GetValue(null));
            };
            same(typeof(PanelLeds), "Showing", PanelHome.StripShowing);
            same(typeof(PanelLeds), "NotSelected", PanelHome.StripNotSelected);
            same(typeof(PanelLeds), "UpdateAvailable", PanelHome.StripUpdateAvailable);
            same(typeof(PanelLeds), "Installed", PanelHome.StripInstalled);
            same(typeof(PanelLeds), "NotInstalled", PanelHome.StripNotInstalled);
            same(typeof(PanelMatrix), "NotShown", PanelHome.MatrixNotShown);

            var stateText = typeof(PanelLeds).GetMethod("StateText", new[] { typeof(FlagBoxInstallState?), typeof(bool?) });
            if (stateText == null) return;
            var states = new FlagBoxInstallState?[] { null }.Concat(Enum.GetValues(typeof(FlagBoxInstallState)).Cast<FlagBoxInstallState?>());
            foreach (var profile in states)
            {
                foreach (var selected in new bool?[] { null, true, false })
                {
                    var card = (string)stateText.Invoke(null, new object[] { profile, selected }) ?? string.Empty;
                    Assert.True(card == PanelHome.StripLine(false, null, profile, selected).Text,
                        "The LEDs card says \"" + card + "\" for " + profile + "/" + selected + ", and Home \"" + PanelHome.StripLine(false, null, profile, selected).Text + "\".");
                }
            }
        }

        /// <summary>A size nobody knows is not drawn, and nor is one the name already says.</summary>
        [Fact]
        public void A_screen_gives_its_size_unless_nobody_knows_it_or_its_name_says_it()
        {
            Assert.Equal("1280 × 480", PanelHome.ScreenSize(Screen(Contract.KindFace, 1280, 480)));
            // A migrated face whose folder named no size, or named only one side of it.
            Assert.Equal(string.Empty, PanelHome.ScreenSize(new ScreenInstance { Kind = Contract.KindFace }));
            Assert.Equal(string.Empty, PanelHome.ScreenSize(new ScreenInstance { Kind = Contract.KindFace, Width = 1280 }));
            Assert.Equal(string.Empty, PanelHome.ScreenSize(new ScreenInstance { Kind = Contract.KindFace, Height = 480 }));
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

        /// <summary>A strip's line keeps its room exactly where the strip can go live, which is where the line
        /// says the car's own rev lights once a car's tables are ready, and so where a session moves it.</summary>
        [Fact]
        public void A_strip_that_can_go_live_keeps_its_lines_room()
        {
            var car = Contract.LedRpmStyleCar;
            var rpm = Contract.LedCentres[0];
            var states = new FlagBoxInstallState?[] { null }.Concat(Enum.GetValues(typeof(FlagBoxInstallState)).Cast<FlagBoxInstallState?>());
            foreach (var profile in states)
            {
                foreach (var selected in new bool?[] { null, true, false })
                {
                    foreach (var style in new[] { car, Contract.LedRpmStyleLeftToRight })
                    {
                        foreach (var centre in new[] { rpm, "brake" })
                        {
                            var keeps = PanelHome.StripLineKeepsRoom(style, centre, profile, selected);
                            Assert.Equal(PanelHome.StripLive(true, style, centre, profile, selected), keeps);
                            // Where the line keeps its room it has something to say once the car is ready.
                            if (keeps) Assert.NotEqual(string.Empty, PanelHome.StripLine(true, "Car", profile, selected).Text);
                        }
                    }
                }
            }
            Assert.True(PanelHome.StripLineKeepsRoom(car, rpm, null, null));
            Assert.False(PanelHome.StripLineKeepsRoom(Contract.LedRpmStyleLeftToRight, rpm, null, null));
            Assert.False(PanelHome.StripLineKeepsRoom(car, rpm, FlagBoxInstallState.NotInstalled, null));
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
            // Any one device of any kind is a rig with something to show.
            Assert.False(PanelHome.RigEmpty(1, 0, 0));
            Assert.False(PanelHome.RigEmpty(0, 1, 0));
            Assert.False(PanelHome.RigEmpty(0, 0, 1));
            Assert.False(PanelHome.RigEmpty(2, 1, 3));
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
            // Branches on the brightness it was built for, never the live setting: a wheel's night-mode press
            // mid-drag would otherwise write the dragged day value into the night brightness.
            Assert.Contains("if (nightOn) { if (v == Settings.LightsNightBrightness) return; Settings.LightsNightBrightness = v; Save(); ShowLightingChange(); } else { if (v == Settings.LightsBrightness) return; Settings.LightsBrightness = v; Save(); ShowLightingChange(); }", code);
            Assert.DoesNotContain("if (Settings.LightsNightMode)", code);
            Assert.Contains("Settings.LightsNightMode = on; Save(); ShowLightingChange();", code);
            // It starts at that brightness, with its figure, which follows the drag; the switch starts where night mode is.
            Assert.Contains("var slider = Ui.Slider(value, v =>", code);
            Assert.Contains("var numeral = Ui.Text(PanelHome.Percent(value), PanelHome.QuickValueSize,", code);
            Assert.Contains("}, v => numeral.Text = PanelHome.Percent(v), false);", code);
            // A key step reaches only changed, so the figure is set there too, before an unchanged value returns.
            Assert.Contains("var slider = Ui.Slider(value, v => { numeral.Text = PanelHome.Percent(v); if (nightOn) {", code);
            Assert.Contains("var night = Ui.Switch(nightOn, on =>", code);
        }

        /// <summary>Home draws night mode and a brightness as controls, so it says so before it draws
        /// anything: without it a wheel's press leaves the slider on the other brightness's label and value. An
        /// empty rig draws neither, and does not ask to be rebuilt for them.</summary>
        [Fact]
        public void The_page_says_it_draws_lighting_first()
        {
            Assert.Contains("private FrameworkElement BuildHomePage(PanelRoute to) { var screens = Settings.RigScreens(); var strips = Settings.LedBarList(); var matrices = Settings.MatrixPanels().ToList(); var empty = PanelHome.RigEmpty(screens.Count, strips.Count, matrices.Count); if (!empty) DrawsLighting();", PageCode());
            Assert.Single(Regex.Matches(PageCode(), @"DrawsLighting\(\);"));
        }

        /// <summary>The pictures are live only where PanelHome says so: the car's run on a strip StripLive
        /// allows, the idle glyph on a matrix MatrixDrawsGlyph allows, and both repainted on the tick.</summary>
        [Fact]
        public void The_page_draws_live_only_what_PanelHome_allows()
        {
            var code = PageCode();
            // A strip's facts are the ones the attention card was drawn from, copied once at build.
            Assert.Contains("var facts = StripFacts(bar.Namespace);", code);
            Assert.Contains("Profile = facts == null ? null : facts.Profile, Selected = facts == null ? null : facts.Selected,", code);
            Assert.Contains("var profile = strip.Profile; var selected = strip.Selected; var cars = plugin.CarLights;", code);
            Assert.Equal(1, Regex.Matches(code, @"StripFacts\(").Count - Regex.Matches(code, @"StripFacts\(issue\.Subject\)").Count);
            Assert.Contains("var live = cars != null && PanelHome.StripLive(cars.Ready, Settings.BarRpmStyle(ns), Settings.BarCentre(ns), profile, selected);", code);
            Assert.Contains("var run = live ? cars.Run(strip.Centre) : null;", code);
            Assert.Contains("var line = PanelHome.StripLine(live, live ? cars.CarName : null, profile, selected);", code);
            Assert.Contains("if (runKey != strip.PaintedRun) { strip.PaintedRun = runKey; var frame = PanelEmulation.LiveFrame(run, strip.Ends, strip.Centre); if (!HomeRelight(strip.Picture, frame, StripStyle.Home.UnlitHex)) {", code);
            Assert.Contains("HomeRelightMissed(); strip.Picture = Ui.Strip(frame, StripStyle.Home, strip.Dim); strip.Host.Child = strip.Picture;", code);
            Assert.Contains("foreach (var hex in lit ?? new string[0]) leds[at++].Background = Ui.Brush(hex ?? unlitHex);", code);
            Assert.Contains("HomeSetLine(strip.Line, line, strip.KeepsRoom); HomeSetDot(strip.Dot, line.DotHex);", code);
            Assert.Contains("strip.Picture = Ui.Strip(PanelEmulation.LiveFrame(null, strip.Ends, strip.Centre), StripStyle.Home, dim);", code);
            Assert.Contains("OnTick(() => { foreach (var strip in live) HomePaintStrip(strip); });", code);
            Assert.Contains("if (live.Count > 0) OnTick(", code);
            Assert.Contains("OnTick(() => { foreach (var screen in shown) HomePaintScreen(screen); });", code);
            Assert.Contains("if (shown.Count > 0) OnTick(", code);
            Assert.Contains("var line = PanelHome.ScreenLine(Settings, screen, row.Installed, row.Restart);", code);
            Assert.Contains("var facts = MatrixFacts(slot); var shown = facts == null ? null : facts.Shown;", code);
            Assert.Contains("var line = PanelHome.MatrixLine(slot, title, Settings.MatrixRest(slot), shown); IList<string> cells = new string[64]; if (PanelHome.MatrixDrawsGlyph(shown)) { var options = new MatrixOptions { Rest = Settings.MatrixRest(slot), Side = Settings.MatrixSide(slot), Bands = Settings.MatrixGearBands(slot), CarLadder = Settings.MatrixGearCarLadder(slot), }; cells = PanelEmulation.MatrixFrame(GlyphSheet, PanelEmulation.Idle, options); }", code);
            Assert.Contains("var picture = Ui.Matrix(cells, MatrixStyle.Home, dim);", code);
            Assert.Contains("var lineText = HomeLineText(true); HomeSetLine(lineText, line);", code);
            Assert.Contains("var dot = HomeDot(line.DotHex);", code);

            // A screen's facts, copied at build; its line and dot repainted only when they move.
            Assert.Contains("var facts = ScreenFacts(screen.Namespace); var restart = PanelAttention.Has(issues, PanelAttention.ScreenRestart, screen.Namespace); var row = new HomeScreen { Namespace = screen.Namespace, Installed = facts == null ? null : facts.Installed, Restart = restart,", code);
            Assert.Contains("if (painted == row.Painted) return; row.Painted = painted; HomeSetLine(row.Line, line); HomeSetDot(row.Dot, line.DotHex);", code);
            Assert.Contains("text.Text = line.Text; text.Foreground = Ui.Brush(line.TextHex);", code);
            Assert.Contains("dot.Fill = hex == null ? null : Ui.Brush(hex); dot.Visibility = hex == null ? Visibility.Hidden : Visibility.Visible;", code);
        }

        /// <summary>An update check's answer can land while Home is showing and add or drop a row, so Home hears
        /// it and draws itself again, headline and card together; an answer that moved nothing Home drew, which
        /// can land mid-drag, leaves the page alone.</summary>
        [Fact]
        public void The_page_draws_itself_again_when_the_update_check_answers()
        {
            var code = PageCode();
            Assert.Contains("var drawn = PanelHome.DrawnFrom(issues); OnUpdate(null, manual => { if (PanelHome.DrawnFrom(issues) != drawn) RebuildPage(); });", code);
            Assert.DoesNotContain("OnUpdate(null, manual => RebuildPage());", code);
        }

        [Fact]
        public void What_the_page_was_drawn_from_moves_with_every_word_a_row_draws()
        {
            var steps = new List<string[]> { new[] { "Devices", "Wheel" }, new[] { "Select it." } };
            Func<string, string, string, string, IList<string[]>, PanelIssue> issue = (id, title, detail, press, s) =>
                new PanelIssue(id, PanelPage.Leds, "LedBar1", title, detail, s, press, PanelIssueAction.CheckAgain);
            var one = new[] { issue("a", "Title", "Detail", "Press", steps) };
            var key = PanelHome.DrawnFrom(one);
            Assert.Equal(key, PanelHome.DrawnFrom(new[] { issue("a", "Title", "Detail", "Press", new List<string[]> { new[] { "Devices", "Wheel" }, new[] { "Select it." } }) }));
            Assert.NotEqual(key, PanelHome.DrawnFrom(new[] { issue("b", "Title", "Detail", "Press", steps) }));
            Assert.NotEqual(key, PanelHome.DrawnFrom(new[] { issue("a", "Title 2", "Detail", "Press", steps) }));
            Assert.NotEqual(key, PanelHome.DrawnFrom(new[] { issue("a", "Title", "Detail 2", "Press", steps) }));
            Assert.NotEqual(key, PanelHome.DrawnFrom(new[] { issue("a", "Title", "Detail", "Press 2", steps) }));
            Assert.NotEqual(key, PanelHome.DrawnFrom(new[] { issue("a", "Title", "Detail", "Press", new List<string[]> { new[] { "Devices" } }) }));
            // A row added or dropped moves it; no issues at all is its own key.
            Assert.NotEqual(key, PanelHome.DrawnFrom(one.Concat(one).ToList()));
            Assert.NotEqual(key, PanelHome.DrawnFrom(new PanelIssue[0]));
            Assert.Equal(PanelHome.DrawnFrom(new PanelIssue[0]), PanelHome.DrawnFrom(null));
        }

        /// <summary>The attention card draws every part of each issue: the headline counts them, the well
        /// wears the issue's icon, and the title, detail, steps and press are the issue's own.</summary>
        [Fact]
        public void The_attention_card_draws_each_issue_whole()
        {
            var code = PageCode();
            Assert.Contains("head.Children.Add(Ui.PageTitle(PanelAttention.Headline(issues.Count)));", code);
            Assert.Contains("var eyebrow = Ui.Eyebrow(PanelHome.Title);", code);
            Assert.Contains("var icon = Ui.NavIcon(PanelHome.IssueIcon(issue), Theme.Caution, PanelHome.IconSize);", code);
            Assert.Contains("var title = Ui.Text(issue.Title, PanelShell.RowTitleSize, FontWeights.SemiBold, Theme.TextPrimary);", code);
            Assert.Contains("if (!string.IsNullOrEmpty(issue.Detail)) { var detail = Ui.Prose(issue.Detail, PanelHome.DetailSize);", code);
            Assert.Contains("text.Children.Add(detail);", code);
            Assert.Contains("if (hasSteps) { var steps = Ui.Steps(issue.Steps); steps.Margin = new Thickness(0, PanelHome.StepsGap, 0, 0); text.Children.Add(steps); }", code);
            Assert.Contains("var hasSteps = issue.Steps.Count > 0;", code);
            Assert.Contains("var row = HomeIssueRow(issues[i], beside);", code);
        }

        /// <summary>The page draws PanelHome's words and numbers, not the values they are made from.</summary>
        [Fact]
        public void The_page_draws_PanelHomes_words_and_numbers()
        {
            var code = PageCode();
            Assert.Contains("var text = Ui.VStack(0, HomeNameLine(screen.Name, PanelHome.ScreenSize(screen)), row.Line);", code);
            Assert.Contains("var shape = PanelHome.StripShape(bar);", code);
            Assert.Contains("var title = Settings.MatrixName(slot) ?? PanelHome.MatrixName(slot);", code);
            Assert.Contains("var name = Ui.Text(title, PanelHome.NameSize, FontWeights.SemiBold, Theme.TextPrimary);", code);
            Assert.Contains("var none = Ui.Prose(PanelHome.EmptyLine(empty), PanelHome.DetailSize);", code);
            Assert.Contains("var eyebrow = Ui.Eyebrow(PanelNav.Label(page));", code);
            Assert.Contains("var open = Ui.LinkButton(PanelHome.OpenLink);", code);
            Assert.Contains("var rig = Ui.Button(PanelHome.OpenRig, PanelButtonKind.Outline, PanelButtonSize.Small);", code);
            Assert.Contains("var dim = PanelEmulation.Dim(Settings.LightsNightMode, Settings.LightsNightBrightness);", code);
            Assert.Contains("var name = Ui.Text(bar.Name ?? string.Empty, PanelHome.NameSize, FontWeights.SemiBold, Theme.TextPrimary);", code);
            Assert.Contains("var grid = Ui.CardGrid(PanelHome.CardMinWidth, PanelHome.CardGap, PanelHome.CardMax,", code);
            Assert.Contains("return PageSection(PanelHome.RightNowTitle, grid);", code);
            Assert.Contains("return PageSection(PanelHome.QuickControlsTitle, Ui.CardBox(body, 0));", code);
            Assert.Contains("Ui.VStack(PanelHome.QuickGap, Ui.Eyebrow(PanelSettings.NightModeTitle), night),", code);
            Assert.Contains("Ui.VStack(PanelHome.QuickGap, Ui.Eyebrow(PanelHome.TryTitle), rig),", code);
            Assert.DoesNotContain("StripStyle.Card", code);
            Assert.DoesNotContain("MatrixStyle.Card", code);
        }

        /// <summary>
        /// Every control and every row is put where it is drawn: a statement that builds a part and one that
        /// adds it to the page are pinned together, so a part built and never placed (an issue with no press,
        /// a card with its head and no rows, a slider with no track) fails here.
        /// </summary>
        [Fact]
        public void Every_part_it_builds_is_placed_on_the_page()
        {
            var code = PageCode();
            // The fix rows, ruled apart, in the card.
            Assert.Contains("var row = HomeIssueRow(issues[i], beside); if (i > 0) { row.BorderBrush = Ui.Brush(Theme.Rule); row.BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0); } rows.Children.Add(row); } return Ui.CardBox(rows, 0);", code);
            // A Right now card: its head, then its page's empty state or its rows.
            Assert.Contains("stack.Children.Add(head); if (rows.Count == 0) { var none = Ui.Prose(PanelHome.EmptyLine(empty), PanelHome.DetailSize); none.Margin = new Thickness(PanelHome.RowPaddingX, 0, PanelHome.RowPaddingX, PanelHome.RowPaddingY + PanelHome.CardHeadPaddingBottom); stack.Children.Add(none); } foreach (var row in rows) stack.Children.Add(row); return Ui.CardBox(stack, 0);", code);
            // Each screen's row on its card, painted before the first tick.
            Assert.Contains("var row = HomeScreenRow(screen); shown.Add(row.Key); rows.Add(row.Value);", code);
            Assert.Contains("Restart = restart, Line = HomeLineText(false), Dot = HomeDot(null), }; HomePaintScreen(row);", code);
            Assert.Contains("DockPanel.SetDock(row.Dot, Dock.Right); dock.Children.Add(row.Dot); dock.Children.Add(text);", code);
            // Each strip's picture in its host, painted before the first tick, and repainted on it.
            // In the kit's fixed picture, which shrinks to the card and never grows past its own size.
            Assert.Contains("var host = (Viewbox)Ui.FitWidth(strip.Picture); host.Margin = new Thickness(0, PanelHome.StripRowGap, 0, 0); strip.Host = host; HomePaintStrip(strip); live.Add(strip);", code);
            Assert.DoesNotContain("new Viewbox", code);
            Assert.Contains("top.Children.Add(strip.Dot);", code);
            Assert.Contains("top.Children.Add(name);", code);
            // Each matrix's picture, dot and text.
            Assert.Contains("dock.Children.Add(picture); dock.Children.Add(dot); dock.Children.Add(text);", code);
            // The brightness cell: the label and its figure over the slider.
            Assert.Contains("DockPanel.SetDock(numeral, Dock.Right); labelLine.Children.Add(numeral); labelLine.Children.Add(label);", code);
            Assert.Contains("Ui.VStack(PanelHome.QuickGap, labelLine, slider),", code);
            // The three cells side by side on TwoColumns, stacked otherwise, each in the card.
            Assert.Contains("var beside = TwoColumns; Panel body; if (beside) { var grid = new Grid(); foreach (var share in PanelHome.QuickColumns) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(share, GridUnitType.Star) }); body = grid; } else { body = new StackPanel { Orientation = Orientation.Vertical }; }", code);
            Assert.Contains("BorderThickness = beside ? new Thickness(rule, 0, 0, 0) : new Thickness(0, rule, 0, 0), Child = cells[i], }; if (beside) Grid.SetColumn(cell, i); body.Children.Add(cell);", code);
            // The page's head over its sections.
            Assert.Contains("head.Children.Add(eyebrow); head.Children.Add(Ui.PageTitle(PanelAttention.Headline(issues.Count)));", code);
            Assert.Contains("var stack = new StackPanel { Orientation = Orientation.Vertical }; stack.Children.Add(head); foreach (var section in sections)", code);
        }

        /// <summary>What each press does: the issue's own call, and each row and link to its own page.</summary>
        [Fact]
        public void Each_press_makes_its_own_call()
        {
            var code = PageCode();
            Assert.Contains("switch (PanelHome.Press(issue))", code);
            Assert.Contains("press.Click += (sender, args) => HomeAct(issue);", code);
            Assert.Contains("row.Click += (sender, args) => click();", code);
            Assert.Contains("case HomePress.CheckAgain: CheckAgain(); Say(PanelHome.CheckedAgain(issue, issues, StripFacts(issue.Subject))); return;", code);
            Assert.Contains("case HomePress.Reinstall: var screen = Settings.ScreenByNamespace(issue.Subject); if (screen != null) { InstallScreenAgain(screen); return; } Open(issue.Page, issue.Subject, issue.Anchor); return;", code);
            Assert.Contains("case HomePress.Open: Open(issue.Page, issue.Subject, issue.Anchor); return;", code);
            Assert.Contains("default: Go(issue.Route); return;", code);
            Assert.Contains("var ns = screen.Namespace; return new KeyValuePair<HomeScreen, Button>(row, HomeRow(dock, () => Open(PanelPage.Screens, ns)));", code);
            Assert.Contains("var ns = bar.Namespace; rows.Add(HomeRow(Ui.VStack(0, top, host, strip.Line), () => Open(PanelPage.Leds, ns)));", code);
            Assert.Contains("var id = slot.ToString(System.Globalization.CultureInfo.InvariantCulture); return HomeRow(dock, () => Open(PanelPage.Matrix, id));", code);
            Assert.Contains("rig.Click += (sender, args) => Go(PanelPage.Rig);", code);
            Assert.Contains("open.Click += (sender, args) => Go(page);", code);
            // Go focuses the Screens page at Loaded; the sheet opens after, at Input, so its focus lands last.
            Assert.Contains("Ui.DashedAddCard(PanelAddScreen.SectionTitle, () => { Go(PanelPage.Screens); Dispatcher.BeginInvoke(new Action(() => ShowAddScreen()), DispatcherPriority.Input); });", code);
            // The shell's route is its own, not a hook: Home never reads it.
            Assert.DoesNotContain("route.", code);
            Assert.DoesNotContain("Go(PanelPage.Screens); ShowAddScreen();", code);
            Assert.Contains("if (issues.Count > 0) sections.Add(Ui.Anchor(HomeAttentionCard(), PanelHome.AnchorAttention));", code);
            Assert.Contains("HomeCard(PanelPage.Screens, HomeScreenRows(screens), PanelScreens.NoScreens),", code);
            Assert.Contains("HomeCard(PanelPage.Leds, HomeStripRows(strips, dim), PanelLeds.NoStrips),", code);
            Assert.Contains("HomeCard(PanelPage.Matrix, matrices.Select(m => HomeMatrixRow(m, dim)).ToList(), PanelMatrix.NoPanels));", code);
        }

        /// <summary>The empty rig is the add tile with its sentence, in place of Right now and under its anchor,
        /// and it is the only thing on the page (voice.md; the inventory's "in place of the three sections"):
        /// only an issue the headline counts (an update, the one kind an empty rig can have) is drawn above it,
        /// so the count is never over no rows. A rig with anything draws Right now and the quick controls, and
        /// each Home search entry lands on its own section's anchor.</summary>
        [Fact]
        public void The_empty_rig_stands_in_place_of_right_now()
        {
            var code = PageCode();
            var rightNow = "sections.Add(Ui.Anchor(HomeRightNow(screens, strips, matrices), PanelHome.AnchorRightNow));";
            var quick = "sections.Add(Ui.Anchor(HomeQuickControls(), PanelHome.AnchorQuickControls));";
            Assert.Contains("if (issues.Count > 0) sections.Add(Ui.Anchor(HomeAttentionCard(), PanelHome.AnchorAttention)); if (empty) { sections.Add(Ui.Anchor(HomeEmptyRig(), PanelHome.AnchorRightNow)); } else { " + rightNow + " " + quick + " }", code);
            var drawnAt = new Dictionary<string, string>
            {
                { PanelHome.AnchorRightNow, rightNow },
                { PanelHome.AnchorQuickControls, quick },
            };
            Assert.All(PanelHome.Search.Where(entry => entry.Route.Anchor != null), entry =>
            {
                Assert.True(drawnAt.ContainsKey(entry.Route.Anchor), entry.Label);
                Assert.Contains(drawnAt[entry.Route.Anchor], code);
            });
            Assert.Single(Regex.Matches(code, Regex.Escape("Ui.Anchor(HomeQuickControls(),")));
            Assert.Contains("foreach (var section in sections) { section.Margin = new Thickness(0, PanelShell.SectionGapFor(PanelPage.Home), 0, 0); stack.Children.Add(section); } return stack;", code);
            Assert.Contains("var line = Ui.Prose(PanelCopy.EmptyRig, PanelHome.DetailSize); line.Margin = new Thickness(0, PanelHome.EmptyRigGap, 0, 0); return Ui.VStack(0, Ui.CardGrid(PanelHome.CardMinWidth, PanelHome.CardGap, PanelHome.CardMax, tile), line);", code);
        }

        /// <summary>The quick controls sit side by side only on TwoColumns, never on !Narrow; an issue's press
        /// sits beside its text by the content's width; a stacked press hangs its row's icon from the top.</summary>
        [Fact]
        public void The_page_asks_TwoColumns_for_columns()
        {
            var code = PageCode();
            // An issue's press asks the content's width, since it is a row's trailing action and not a second
            // block, read once for the card and only up to PressBesideFrom, past which nothing is drawn
            // differently: a resize of a wide panel then leaves Home alone (PanelShell.RebuildsOnResize).
            Assert.Contains("var beside = PanelHome.PressBeside(ContentWidthUpTo(PanelHome.PressBesideFrom)); var rows = new StackPanel", code);
            Assert.Single(Regex.Matches(code, @"\bContentWidthUpTo\("));
            Assert.Empty(Regex.Matches(code, @"\bContentWidth\b(?!\s*\()"));
            Assert.False(PanelShell.RebuildsOnResize(3000, 3840, PanelHome.PressBesideFrom, 17));
            Assert.False(PanelShell.RebuildsOnResize(1300, 1301, PanelHome.PressBesideFrom, 17));
            Assert.Contains("var align = hasSteps || !beside ? VerticalAlignment.Top : VerticalAlignment.Center;", code);
            Assert.Contains("var beside = TwoColumns;", code);
            Assert.DoesNotContain("!Narrow", code);
            Assert.DoesNotContain("RefreshSidebar();", code);
        }

        /// <summary>A long name wraps beside its size, which is docked first and never cut, as the artboard's
        /// name does; a long press label trims, at PressMaxWidth beside the text, with no hover that repeats
        /// it; and a line with nothing to say takes its gap with it.</summary>
        [Fact]
        public void A_long_name_wraps_beside_its_size_and_an_empty_line_takes_its_gap()
        {
            var code = PageCode();
            Assert.Contains("var dock = new DockPanel { LastChildFill = true, HorizontalAlignment = HorizontalAlignment.Left };", code);
            Assert.Contains("DockPanel.SetDock(figure, Dock.Right); dock.Children.Add(figure);", code);
            Assert.Contains("title.TextWrapping = TextWrapping.Wrap; title.VerticalAlignment = VerticalAlignment.Center; dock.Children.Add(title);", code);
            Assert.DoesNotContain("title.TextTrimming", code);
            Assert.Contains("press.Content = new TextBlock { Text = issue.ActionLabel, TextTrimming = TextTrimming.CharacterEllipsis }; press.Click += (sender, args) => HomeAct(issue);", code);
            Assert.DoesNotContain("press.ToolTip", code);
            Assert.Contains("if (beside) { press.VerticalAlignment = align; press.MaxWidth = PanelHome.PressMaxWidth; press.Margin = new Thickness(PanelHome.IconGap, 0, 0, 0); DockPanel.SetDock(press, Dock.Right); dock.Children.Add(press); dock.Children.Add(text); } else { press.HorizontalAlignment = HorizontalAlignment.Left; press.Margin = new Thickness(0, PanelHome.StepsGap, 0, 0); text.Children.Add(press); dock.Children.Add(text); }", code);
            Assert.Contains("rows.Add(HomeRow(Ui.VStack(0, top, host, strip.Line), () => Open(PanelPage.Leds, ns)));", code);
            Assert.Contains("strip.Line.Margin = new Thickness(0, PanelHome.StripRowGap, 0, 0);", code);
            Assert.Contains("text.Visibility = line.Text.Length > 0 ? Visibility.Visible : keepsRoom ? Visibility.Hidden : Visibility.Collapsed;", code);
            // A strip that can go live keeps its line's room, so its card does not grow as a session starts.
            Assert.Contains("strip.KeepsRoom = PanelHome.StripLineKeepsRoom(Settings.BarRpmStyle(bar.Namespace), Settings.BarCentre(bar.Namespace), strip.Profile, strip.Selected);", code);
            Assert.Contains("HomeSetLine(strip.Line, line, strip.KeepsRoom);", code);
        }
    }
}
