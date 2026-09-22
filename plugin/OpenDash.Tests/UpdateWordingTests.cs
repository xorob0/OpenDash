// UpdateWordingTests.cs: the copy, as a table. The sentence a driver reads is the deliverable of #82 as much
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
            Assert.Equal("Could not reach GitHub. You have 0.1.0-rc.2.", line);
            Assert.DoesNotContain("error", line, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("fail", line, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Every_state_says_something_or_deliberately_nothing()
        {
            Assert.Equal("Checking for updates…", UpdateWording.Line(Status(UpdateState.Checking)));
            Assert.Equal("You have the newest release, 0.1.0-rc.2.", UpdateWording.Line(Status(UpdateState.UpToDate)));
            Assert.Equal("Update checks are off.", UpdateWording.Line(Status(UpdateState.Disabled)));
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
            Assert.Contains("Close and reopen the dashboard", UpdateWording.Reopen);
            // It no longer says "SimHub does not need restarting" either, which was a reassurance about
            // a thing the sentence never raised. What matters is that it does not ask for a restart.
            Assert.DoesNotContain("restart", UpdateWording.Reopen, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The plugin's own half, which says the opposite about the one point that matters.
        /// </summary>
        /// <remarks>
        /// A loaded assembly cannot be replaced under a running process, so the new openDash is put in
        /// place once SimHub closes. The sentence used to be past tense -- "was updated too; it takes
        /// effect the next time you start SimHub" -- under a pill that had already flipped to up to date
        /// and a version number that had already moved, and a rig read all of that as "done" and never
        /// restarted. It names the restart, and the dialog asks.
        ///
        /// What it does not do any more is say why the restart is needed. A user does not act on the
        /// fact that a process cannot replace its own assembly, so the sentence names the outcome and
        /// the action and stops; see docs/design/voice.md.
        /// </remarks>
        [Fact]
        public void The_plugins_own_sentence_says_SimHub_has_to_close()
        {
            Assert.Contains("Restart SimHub", UpdateWording.Restart);
            Assert.DoesNotContain("does not need restarting", UpdateWording.Restart);
            // Not past tense: the swap has not happened yet and saying it has is the whole bug.
            Assert.DoesNotContain("was updated", UpdateWording.Restart);

            var question = UpdateWording.RestartQuestion("0.3.0-rc.5");
            Assert.Contains("0.3.0-rc.5", question);
            Assert.Contains("Close SimHub now", question);
            // The consequence of saying no, which is the one fact that makes the dialog worth the
            // interruption: the pill and the version have both already moved.
            Assert.Contains("you are running the old version", question);
            // And no account of why, in the dialog any more than in the caption.
            Assert.DoesNotContain("cannot replace its own code", question);
            // It survives not knowing which version it is offering.
            Assert.Contains("openDash itself", UpdateWording.RestartQuestion(null));

            Assert.Contains("SimHub", UpdateWording.RestartTitle);
            Assert.Contains("Restart SimHub", UpdateWording.RestartLater);
            Assert.Contains("Close SimHub yourself", UpdateWording.RestartFailed);
        }

        /// <summary>
        /// The sentence beside the switch. It is the author's, from design/canvas/Plugin.dc.html, and it is the
        /// only place a user is told in full what leaves their machine, so it says both halves: what is asked for,
        /// and that nothing else goes.
        /// </summary>
        [Fact]
        public void The_setting_says_in_one_sentence_what_leaves_the_machine()
        {
            Assert.Equal("Asks GitHub for the newest release once a day. Nothing else leaves your machine.", UpdateWording.CheckCaption);
        }

        /// <summary>
        /// What a press with nothing behind it says. The sentence has to name the button that would fetch an
        /// answer, since the whole complaint it settles is that the press left the user with nothing to do next.
        /// </summary>
        [Fact]
        public void A_press_with_no_release_behind_it_names_the_button_that_would_find_one()
        {
            Assert.Contains("release to install", UpdateWording.NothingToApply);
            Assert.Contains("Check now", UpdateWording.NothingToApply);
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
