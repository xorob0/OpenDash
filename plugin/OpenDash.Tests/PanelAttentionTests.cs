// PanelAttentionTests.cs: what Home says needs fixing -- each rule, the order, and the silence of the unknown.
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelAttentionTests
    {
        private static AttentionScreen Screen(string name, bool? installed = true, bool? written = false, bool unclaimed = false)
        {
            return new AttentionScreen { Name = name, Namespace = name.Replace(" ", string.Empty), Installed = installed, AddedSinceStart = written, Unclaimed = unclaimed };
        }

        private static AttentionStrip Strip(string name, FlagBoxInstallState? profile, bool? selected, string device = "Arduino RGB LEDs")
        {
            return new AttentionStrip { Name = name, Namespace = "Bar" + name.Replace(" ", string.Empty), DeviceName = device, Profile = profile, Selected = selected };
        }

        [Fact]
        public void A_rig_with_nothing_wrong_has_nothing_to_fix()
        {
            var input = new AttentionInput();
            input.Screens.Add(Screen("Main dash"));
            input.Strips.Add(Strip("Wheel rim", FlagBoxInstallState.UpToDate, true));
            input.Matrices.Add(new AttentionMatrix { Slot = 1, Name = "Flag box", Shown = true });
            input.FlagBox = FlagBoxInstallState.UpToDate;
            input.RestartPending = false;
            input.UpdateAvailable = false;
            Assert.Empty(PanelAttention.Find(input));
            Assert.Empty(PanelAttention.Find(null));
        }

        [Fact]
        public void A_fact_nobody_could_read_produces_nothing()
        {
            var input = new AttentionInput();
            input.Screens.Add(Screen("Rim", installed: null, written: null));
            input.Strips.Add(Strip("Dash brow", null, null));
            input.Strips.Add(Strip("Wheel rim", FlagBoxInstallState.UpToDate, null));
            input.Matrices.Add(new AttentionMatrix { Slot = 2, Name = "Left pillar", Shown = null });
            Assert.Empty(PanelAttention.Find(input));
        }

        [Fact]
        public void A_missing_folder_is_installed_again()
        {
            var input = new AttentionInput();
            input.Screens.Add(Screen("Rim", installed: false));
            var issue = PanelAttention.Find(input).Single();
            Assert.Equal("Rim's dashboard is missing from SimHub", issue.Title);
            // Only what the press does not say, and the words the Screens page's fix box says too.
            Assert.Equal("Its settings are kept.", issue.Detail);
            Assert.Equal(PanelAttention.MissingDetail, issue.Detail);
            Assert.Equal("Install it again", issue.ActionLabel);
            Assert.Equal(PanelIssueAction.Reinstall, issue.Action);
            Assert.Equal(PanelPage.Screens, issue.Page);
            Assert.Equal("Rim", issue.Subject);
        }

        [Fact]
        public void A_screen_written_since_SimHub_started_says_the_restart_and_the_assignment()
        {
            var input = new AttentionInput();
            input.Screens.Add(Screen("Rim", written: true));
            var issue = PanelAttention.Find(input).Single();
            Assert.Equal("Rim is not in SimHub yet", issue.Title);
            Assert.Equal("Restart SimHub to load it. Then assign \"Rim\" to its display in Dash Studio.", issue.Detail);
            Assert.Equal("Open Rim", issue.ActionLabel);
            Assert.Equal(PanelIssueAction.Navigate, issue.Action);
            // A folder that is gone is not also waiting for a restart.
            input.Screens[0] = Screen("Rim", installed: false, written: true);
            Assert.Equal(PanelIssueAction.Reinstall, PanelAttention.Find(input).Single().Action);
        }

        [Fact]
        public void A_strip_installed_and_not_selected_gets_the_steps_in_SimHub()
        {
            var input = new AttentionInput();
            input.Strips.Add(Strip("Dash brow", FlagBoxInstallState.UpToDate, false));
            var issue = PanelAttention.Find(input).Single();
            Assert.Equal("Dash brow's profile is not selected", issue.Title);
            Assert.Equal("Installed, but not selected in SimHub.", issue.Detail);
            Assert.Equal(PanelAttention.UnselectedDetail, issue.Detail);
            Assert.Equal(PanelIssueAction.CheckAgain, issue.Action);
            Assert.Equal("Check again", issue.ActionLabel);
            Assert.Equal(PanelPage.Leds, issue.Page);
            Assert.Equal(3, issue.Steps.Count);
            Assert.Equal(new[] { "Devices", "Arduino RGB LEDs", "Telemetry LEDs" }, issue.Steps[0]);
            Assert.Equal(new[] { "Select \"Dash brow\"" }, issue.Steps[1]);
            Assert.Equal(new[] { "Set automatic profile switching to Disabled" }, issue.Steps[2]);
        }

        /// <summary>With no device name, the crumb says where the device is, in SimHub, as PanelLeds.SelectIt
        /// does, and not "your LED device" (#523).</summary>
        [Fact]
        public void A_strip_whose_device_is_unknown_is_sent_to_its_device_in_SimHub()
        {
            var input = new AttentionInput();
            input.Strips.Add(Strip("Dash brow", FlagBoxInstallState.UpToDate, false, device: null));
            var issue = PanelAttention.Find(input).Single();
            Assert.Equal(new[] { "Devices", "the strip's device in SimHub", "Telemetry LEDs" }, issue.Steps[0]);
            Assert.Equal(PanelAttention.UnnamedDeviceCrumb, issue.Steps[0][1]);
            Assert.EndsWith(" in SimHub", PanelAttention.UnnamedDeviceCrumb);
            Assert.EndsWith(" in SimHub to use it.", PanelLeds.SelectIt("Dash brow", null));
        }

        [Fact]
        public void A_strip_that_is_not_installed_is_not_asked_to_be_selected()
        {
            var input = new AttentionInput();
            input.Strips.Add(Strip("Dash brow", FlagBoxInstallState.NotInstalled, false));
            Assert.Empty(PanelAttention.Find(input));
        }

        [Fact]
        public void A_matrix_slot_no_device_shows_is_not_shown_in_SimHub()
        {
            var input = new AttentionInput();
            input.Matrices.Add(new AttentionMatrix { Slot = 2, Name = "Left pillar", Shown = false });
            input.Matrices.Add(new AttentionMatrix { Slot = 3, Name = null, Shown = false });
            var issues = PanelAttention.Find(input);
            Assert.Equal("Left pillar is not shown in SimHub", issues[0].Title);
            Assert.Equal("No matrix device in SimHub is set to matrix 2.", issues[0].Detail);
            Assert.Equal("Open Left pillar", issues[0].ActionLabel);
            Assert.Equal("2", issues[0].Subject);
            Assert.Equal("Matrix 3 is not shown in SimHub", issues[1].Title);
        }

        [Fact]
        public void Unclaimed_screens_are_one_issue_whatever_their_number()
        {
            var input = new AttentionInput();
            input.Screens.Add(Screen("A", unclaimed: true));
            input.Screens.Add(Screen("B", unclaimed: true));
            var issue = PanelAttention.Find(input).Single();
            Assert.Equal("2 screens came with an older OpenDash", issue.Title);
            Assert.Equal("Keep or remove each one on the Screens page.", issue.Detail);
            Assert.Equal("Open Screens", issue.ActionLabel);
            Assert.Equal("1 screen came with an older OpenDash", PanelAttention.UnclaimedTitle(1));
            // One noun and one pair of verbs for these screens on both pages.
            Assert.Equal("Keep or remove each screen an older OpenDash made.", PanelScreens.UnclaimedNote);
            Assert.Contains("Keep or remove each", PanelScreens.UnclaimedNote);
        }

        [Fact]
        public void An_outdated_profile_is_named_and_the_flag_box_only_with_a_matrix()
        {
            var input = new AttentionInput();
            input.Strips.Add(Strip("Wheel rim", FlagBoxInstallState.Outdated, true));
            input.FlagBox = FlagBoxInstallState.Outdated;
            Assert.Equal(new[] { "Wheel rim's profile has an update" }, PanelAttention.Find(input).Select(i => i.Title));
            // Filed where the LEDs page says its press is (PanelLeds.StripUpdateRoute): the strip's header on
            // LEDs, which carries Update (#523), so the LEDs item wears the dot and the Updates item does not.
            // The route is the LEDs page's constant, so moving the press moves the issue and the dot.
            Assert.Equal(new PanelRoute(PanelPage.Leds, PanelLeds.AnchorStrips), PanelLeds.StripUpdateRoute);
            var outdated = PanelAttention.Find(input).Single();
            Assert.Equal(PanelLeds.StripUpdateRoute.Page, outdated.Page);
            Assert.Equal(PanelLeds.StripUpdateRoute.Anchor, outdated.Anchor);
            // The title and the press say it all; the detail described the plugin's mechanism.
            Assert.Null(outdated.Detail);
            Assert.Equal("Open " + PanelNav.Label(PanelLeds.StripUpdateRoute.Page), outdated.ActionLabel);
            Assert.Equal("Open LEDs", outdated.ActionLabel);
            Assert.False(PanelNav.UpdatesWarns(PanelAttention.Find(input)));
            Assert.True(PanelNav.Warns(PanelPage.Leds, PanelAttention.Find(input)));
            input.Matrices.Add(new AttentionMatrix { Slot = 1, Name = "Flag box", Shown = true });
            Assert.Equal("OpenDash Flag box has an update", PanelAttention.Find(input).Last().Title);
            Assert.Equal("Open Matrix", PanelAttention.Find(input).Last().ActionLabel);
            Assert.Null(PanelAttention.Find(input).Last().Detail);
        }

        /// <summary>#642: a profile newer than this build's has nothing to update to, so Home files no item for it,
        /// on a strip or on the flag box; one that is not selected is still named, as a current one is.</summary>
        [Fact]
        public void A_newer_profile_is_not_an_update()
        {
            var input = new AttentionInput { FlagBox = FlagBoxInstallState.Newer };
            input.Strips.Add(Strip("Wheel rim", FlagBoxInstallState.Newer, true));
            input.Matrices.Add(new AttentionMatrix { Slot = 1, Name = "Flag box", Shown = true });
            Assert.Empty(PanelAttention.Find(input));
            Assert.False(PanelNav.Warns(PanelPage.Leds, PanelAttention.Find(input)));
            Assert.False(PanelNav.Warns(PanelPage.Matrix, PanelAttention.Find(input)));
            input.Strips[0].Selected = false;
            Assert.Equal(new[] { PanelAttention.StripUnselected + input.Strips[0].Namespace }, PanelAttention.Find(input).Select(i => i.Id));
        }

        [Fact]
        public void A_waiting_restart_outranks_the_offer_that_caused_it()
        {
            var input = new AttentionInput { RestartPending = true, UpdateAvailable = true, OfferedVersion = "0.5.1" };
            var issue = PanelAttention.Find(input).Single();
            Assert.Equal("Restart SimHub to finish updating", issue.Title);
            Assert.Equal("Until then you are running the old version.", issue.Detail);
            Assert.Equal("Open Updates", issue.ActionLabel);
            Assert.Equal(PanelPage.Updates, issue.Page);
            // The badge says it, so the Updates item wears no dot as well.
            Assert.False(PanelNav.UpdatesWarns(PanelAttention.Find(input)));
            input.RestartPending = false;
            Assert.Equal("OpenDash 0.5.1 is available", PanelAttention.Find(input).Single().Title);
            input.RestartPending = null;
            input.UpdateAvailable = null;
            Assert.Empty(PanelAttention.Find(input));
        }

        [Fact]
        public void The_issues_come_in_the_order_a_driver_acts_in()
        {
            var input = new AttentionInput { RestartPending = true, FlagBox = FlagBoxInstallState.Outdated };
            input.Screens.Add(Screen("Old", unclaimed: true));
            input.Screens.Add(Screen("Rim", written: true));
            input.Screens.Add(Screen("Gone", installed: false));
            input.Strips.Add(Strip("Wheel rim", FlagBoxInstallState.Outdated, true));
            input.Strips.Add(Strip("Dash brow", FlagBoxInstallState.UpToDate, false));
            input.Matrices.Add(new AttentionMatrix { Slot = 2, Name = "Left pillar", Shown = false });
            var ids = PanelAttention.Find(input).Select(i => i.Id.Split(':')[0]).ToList();
            Assert.Equal(new[]
            {
                "screen-missing", "screen-restart", "strip-unselected", "matrix-dark", "screens-unclaimed",
                "strip-outdated", "flagbox-outdated", "update-restart",
            }, ids);
        }

        [Fact]
        public void A_page_asks_after_an_issue_by_its_rule_and_its_subject()
        {
            var input = new AttentionInput();
            input.Screens.Add(Screen("Rim", written: true));
            var issues = PanelAttention.Find(input);
            Assert.Equal(PanelAttention.ScreenRestart + "Rim", issues.Single().Id);
            Assert.True(PanelAttention.Has(issues, PanelAttention.ScreenRestart, "Rim"));
            Assert.False(PanelAttention.Has(issues, PanelAttention.ScreenRestart, "Brow"));
            Assert.False(PanelAttention.Has(issues, PanelAttention.ScreenMissing, "Rim"));
            Assert.Same(issues.Single(), PanelAttention.Of(issues, PanelAttention.ScreenRestart, "Rim"));
            Assert.Null(PanelAttention.Of(null, PanelAttention.ScreenRestart, "Rim"));
        }

        [Theory]
        [InlineData(0, "Nothing to fix")]
        [InlineData(1, "1 thing to fix")]
        [InlineData(3, "3 things to fix")]
        public void The_headline_counts_them(int count, string headline)
        {
            Assert.Equal(headline, PanelAttention.Headline(count));
        }

        [Fact]
        public void No_sentence_is_contracted_or_spells_the_wordmark()
        {
            var input = new AttentionInput { RestartPending = true };
            input.Screens.Add(Screen("Rim", written: true));
            input.Screens.Add(Screen("Gone", installed: false));
            input.Screens.Add(Screen("Old", unclaimed: true));
            input.Strips.Add(Strip("Brow", FlagBoxInstallState.UpToDate, false));
            input.Matrices.Add(new AttentionMatrix { Slot = 2, Name = "Pillar", Shown = false });
            foreach (var issue in PanelAttention.Find(input))
            {
                var text = issue.Title + " " + issue.Detail + " " + string.Join(" ", issue.Steps.SelectMany(s => s));
                Assert.DoesNotContain("n't", text);
                Assert.DoesNotContain("openDash", text);
            }
        }
    }
}
