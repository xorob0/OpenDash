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
            return new AttentionScreen { Name = name, Namespace = name.Replace(" ", string.Empty), Installed = installed, WrittenSinceStart = written, Unclaimed = unclaimed };
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
            Assert.Equal("Restart SimHub, then assign \"Rim\" to this display in Dash Studio.", issue.Detail);
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
            Assert.Equal("Its profile is installed but not selected on the device.", issue.Detail);
            Assert.Equal(PanelIssueAction.CheckAgain, issue.Action);
            Assert.Equal("Check again", issue.ActionLabel);
            Assert.Equal(PanelPage.Leds, issue.Page);
            Assert.Equal(3, issue.Steps.Count);
            Assert.Equal(new[] { "Devices", "Arduino RGB LEDs", "Telemetry LEDs" }, issue.Steps[0]);
            Assert.Equal(new[] { "Select \"Dash brow\"" }, issue.Steps[1]);
            Assert.Equal(new[] { "Set automatic profile switching to Disabled" }, issue.Steps[2]);
        }

        [Fact]
        public void A_strip_that_is_not_installed_is_not_asked_to_be_selected()
        {
            var input = new AttentionInput();
            input.Strips.Add(Strip("Dash brow", FlagBoxInstallState.NotInstalled, false));
            Assert.Empty(PanelAttention.Find(input));
        }

        [Fact]
        public void A_matrix_slot_no_device_shows_is_dark()
        {
            var input = new AttentionInput();
            input.Matrices.Add(new AttentionMatrix { Slot = 2, Name = "Left pillar", Shown = false });
            input.Matrices.Add(new AttentionMatrix { Slot = 3, Name = null, Shown = false });
            var issues = PanelAttention.Find(input);
            Assert.Equal("Left pillar is dark", issues[0].Title);
            Assert.Equal("No matrix device in SimHub is set to matrix 2.", issues[0].Detail);
            Assert.Equal("Open Left pillar", issues[0].ActionLabel);
            Assert.Equal("2", issues[0].Subject);
            Assert.Equal("Matrix 3 is dark", issues[1].Title);
        }

        [Fact]
        public void Unclaimed_screens_are_one_issue_whatever_their_number()
        {
            var input = new AttentionInput();
            input.Screens.Add(Screen("A", unclaimed: true));
            input.Screens.Add(Screen("B", unclaimed: true));
            var issue = PanelAttention.Find(input).Single();
            Assert.Equal("2 screens came with an older OpenDash", issue.Title);
            Assert.Equal(PanelScreens.UnclaimedNote, issue.Detail);
        }

        [Fact]
        public void An_outdated_profile_is_named_and_the_flag_box_only_with_a_matrix()
        {
            var input = new AttentionInput();
            input.Strips.Add(Strip("Wheel rim", FlagBoxInstallState.Outdated, true));
            input.FlagBox = FlagBoxInstallState.Outdated;
            Assert.Equal(new[] { "Wheel rim's profile is out of date" }, PanelAttention.Find(input).Select(i => i.Title));
            input.Matrices.Add(new AttentionMatrix { Slot = 1, Name = "Flag box", Shown = true });
            Assert.Equal("OpenDash Flag box is out of date", PanelAttention.Find(input).Last().Title);
        }

        [Fact]
        public void A_waiting_restart_outranks_the_offer_that_caused_it()
        {
            var input = new AttentionInput { RestartPending = true, UpdateAvailable = true, OfferedVersion = "0.5.1" };
            var issue = PanelAttention.Find(input).Single();
            Assert.Equal("Restart SimHub to finish updating", issue.Title);
            Assert.Equal(PanelPage.Updates, issue.Page);
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
