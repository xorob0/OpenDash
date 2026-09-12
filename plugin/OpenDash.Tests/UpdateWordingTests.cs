// UpdateWordingTests.cs: the copy, as a table. The sentence a driver reads is the deliverable of XOR-31 as much
// as the request is, so it is pinned rather than left to drift.
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class UpdateWordingTests
    {
        private static UpdateStatus Status(UpdateState state, string installed = "0.1.0-rc.2", string latest = null) =>
            new UpdateStatus { State = state, InstalledVersion = installed, LatestVersion = latest };

        [Fact]
        public void An_offer_names_both_versions()
        {
            Assert.Equal(
                "Version 0.2.0 is available. You have 0.1.0-rc.2.",
                UpdateWording.Line(Status(UpdateState.UpdateAvailable, latest: "0.2.0")));
        }

        [Fact]
        public void Not_reaching_GitHub_is_not_reported_as_an_error()
        {
            // A rig with no network is a normal rig. The line says what is true and asks nothing of the user.
            var line = UpdateWording.Line(Status(UpdateState.Unreachable));
            Assert.Equal("Could not reach GitHub, so there is nothing to report. You have 0.1.0-rc.2.", line);
            Assert.DoesNotContain("error", line, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("fail", line, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Every_state_says_something_or_deliberately_nothing()
        {
            Assert.Equal("Checking for updates…", UpdateWording.Line(Status(UpdateState.Checking)));
            Assert.Equal("You have the newest release, 0.1.0-rc.2.", UpdateWording.Line(Status(UpdateState.UpToDate)));
            Assert.Equal("Update checks are off, so nothing is fetched.", UpdateWording.Line(Status(UpdateState.Disabled)));
            Assert.Null(UpdateWording.Line(Status(UpdateState.Idle)));
            Assert.Null(UpdateWording.Line(null));
        }

        [Fact]
        public void A_missing_version_is_named_rather_than_left_blank()
        {
            Assert.Equal("You have the newest release, an unknown version.", UpdateWording.Line(Status(UpdateState.UpToDate, installed: null)));
        }

        /// <summary>
        /// The most load-bearing string in the feature. It was measured on the VM: replacing a package leaves an
        /// open dash window alone, and closing and reopening the dashboard picks the change up with SimHub still
        /// running. Telling a driver to restart SimHub would cost them a session for no reason.
        /// </summary>
        [Fact]
        public void The_sentence_after_an_update_says_reopen_and_not_restart()
        {
            Assert.Contains("Close the dashboard and start it again", UpdateWording.Reopen);
            Assert.Contains("SimHub does not need restarting", UpdateWording.Reopen);
        }

        [Fact]
        public void The_notes_drop_the_heading_and_show_the_first_real_line()
        {
            // GitHub's generated body, which is what the release carries rather than CHANGELOG.md.
            const string notes = "## What's Changed\n* README: point at the releases page by @xorob0 in https://example.invalid/4\n* More";
            Assert.Equal("README: point at the releases page by @xorob0 in https://example.invalid/4", UpdateWording.Summarise(notes));
        }

        [Fact]
        public void A_long_line_is_cut_and_an_empty_one_is_nothing()
        {
            var summary = UpdateWording.Summarise("## Heading\n" + new string('x', 400));
            Assert.Equal(140, summary.Length);
            Assert.EndsWith("…", summary);

            Assert.Null(UpdateWording.Summarise(null));
            Assert.Null(UpdateWording.Summarise("   "));
            Assert.Null(UpdateWording.Summarise("## Only a heading"));
        }
    }
}
