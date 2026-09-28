// UpdateMarkTests.cs: what the idle screen is told about a newer release (#755). The ticket's acceptance is
// written in terms of silence -- up to date, off, no network, no plugin -- so most of these pin a silence.
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class UpdateMarkTests
    {
        private static UpdateStatus Answer(UpdateState state, string latest = null) =>
            new UpdateStatus { State = state, InstalledVersion = "0.3.0", LatestVersion = latest };

        // Remember

        [Fact]
        public void An_offer_is_remembered_and_an_up_to_date_answer_forgets_it()
        {
            Assert.Equal("0.4.0", UpdateMark.Remember(null, Answer(UpdateState.UpdateAvailable, "0.4.0")));
            Assert.Equal("0.4.1", UpdateMark.Remember("0.4.0", Answer(UpdateState.UpdateAvailable, "0.4.1")));
            Assert.Null(UpdateMark.Remember("0.4.0", Answer(UpdateState.UpToDate)));
        }

        [Fact]
        public void No_network_changes_nothing_in_either_direction()
        {
            // Unreachable is neither "up to date" nor "no longer available": a rig that heard of 0.4.0 yesterday
            // and has no network today has still not installed it, and a rig that never heard of it learns nothing.
            Assert.Equal("0.4.0", UpdateMark.Remember("0.4.0", Answer(UpdateState.Unreachable)));
            Assert.Null(UpdateMark.Remember(null, Answer(UpdateState.Unreachable)));
        }

        [Fact]
        public void A_check_that_did_not_answer_is_not_an_answer()
        {
            foreach (var state in new[] { UpdateState.Disabled, UpdateState.Idle, UpdateState.Checking })
            {
                Assert.Equal("0.4.0", UpdateMark.Remember("0.4.0", Answer(state)));
            }
            Assert.Equal("0.4.0", UpdateMark.Remember("0.4.0", null));
            // An offer that names nothing is not worth forgetting a named one for.
            Assert.Equal("0.4.0", UpdateMark.Remember("0.4.0", Answer(UpdateState.UpdateAvailable, " ")));
        }

        // Offered

        [Fact]
        public void A_rig_that_has_caught_up_is_offered_nothing()
        {
            Assert.Equal("0.4.0", UpdateMark.Offered("0.4.0", "0.3.0", pluginStaged: false));
            Assert.Null(UpdateMark.Offered("0.4.0", "0.4.0", pluginStaged: false));
            Assert.Null(UpdateMark.Offered("0.4.0", "0.4.1", pluginStaged: false));
            // Pre-release ordering is Versioning's, so a candidate is behind its own release.
            Assert.Equal("0.4.0", UpdateMark.Offered("0.4.0", "0.4.0-rc.3", pluginStaged: false));
        }

        [Fact]
        public void Nothing_remembered_or_nothing_readable_is_silence()
        {
            Assert.Null(UpdateMark.Offered(null, "0.3.0", pluginStaged: false));
            Assert.Null(UpdateMark.Offered("", "0.3.0", pluginStaged: false));
            Assert.Null(UpdateMark.Offered("0.4.0", null, pluginStaged: false));
            Assert.Null(UpdateMark.Offered("0.4.0", Versioning.UnknownVersion, pluginStaged: false));
        }

        [Fact]
        public void The_older_half_is_what_is_compared_until_the_new_plugin_is_staged()
        {
            // The same reduction the check makes: dashboards current and the plugin a release behind is a rig that
            // still has an update to take.
            var running = UpdateCheck.RigVersion(new[] { new PackageStatus { FolderName = "OpenDash 850x480", InstalledVersion = "0.4.0" } }, new[] { "OpenDash 850x480" }, "0.3.0");
            Assert.Equal("0.3.0", running);
            Assert.Equal("0.4.0", UpdateMark.Offered("0.4.0", running, pluginStaged: false));
            // Once the new assembly is staged, what is left is the restart the panel asks for, not a second download:
            // after an update that wrote the dashboards before staging the plugin, as one did while a release
            // attached them...
            Assert.Null(UpdateMark.Offered("0.4.0", running, pluginStaged: true));
            // ...and after one whose dashboards come inside the plugin (#438), which leaves them on the old version
            // until the new plugin starts. Comparing the dashboards alone put the mark back up for exactly that.
            Assert.Null(UpdateMark.Offered("0.4.0", UpdateCheck.ComparableInstalled("0.3.0", "0.3.0"), pluginStaged: true));
        }

        // Available and Shown

        [Fact]
        public void The_switch_silences_the_mark_whatever_was_remembered()
        {
            Assert.True(UpdateMark.Available(true, "0.4.0"));
            Assert.False(UpdateMark.Available(false, "0.4.0"));
            Assert.Equal(string.Empty, UpdateMark.Shown(false, "0.4.0"));
            Assert.False(UpdateMark.Available(true, null));
            Assert.Equal(string.Empty, UpdateMark.Shown(true, null));
        }

        [Fact]
        public void The_version_is_published_only_when_the_mark_can_draw_it_whole()
        {
            Assert.Equal("0.4.0", UpdateMark.Shown(true, "0.4.0"));
            Assert.Equal("0.10.0-rc.10", UpdateMark.Shown(true, "0.10.0-rc.10"));
            Assert.Equal("0.4.0", UpdateMark.Shown(true, "v0.4.0"));
            // Longer than the box was measured for, or in a character it was not measured in: the mark still says
            // an update is available, and names no version rather than half of one.
            var tooLong = new string('1', Contract.UpdateVersionMaxLength + 1);
            Assert.Equal(string.Empty, UpdateMark.Shown(true, tooLong));
            Assert.True(UpdateMark.Available(true, tooLong));
            Assert.Equal(string.Empty, UpdateMark.Shown(true, "0.4.0+build"));
            Assert.Equal(string.Empty, UpdateMark.Shown(true, "0.4.0 beta"));
            Assert.Equal(string.Empty, UpdateMark.Shown(true, "v"));
        }

        [Fact]
        public void The_bound_holds_the_version_this_repository_writes()
        {
            // VERSION is what every release is cut from, so the version a rig is offered is one written in that
            // file's form; the bound that could not carry it would name no release at all.
            var version = System.IO.File.ReadAllText(System.IO.Path.Combine(RepoPaths.Root(), "VERSION")).Trim();
            Assert.Equal(version, UpdateMark.Shown(true, version));
            Assert.All(version, c => Assert.Contains(c, Contract.UpdateVersionCharacters));
        }

        // Opening

        [Fact]
        public void The_panel_opens_on_what_the_mark_says()
        {
            // The second start of the day: no check ran in this session, and the remembered offer is what the idle
            // screen draws, so it is what the panel draws too.
            var opening = UpdateMark.Opening(true, null, "0.4.0", "0.3.0");
            Assert.Equal(UpdateState.UpdateAvailable, opening.State);
            Assert.Equal("0.4.0", opening.LatestVersion);
            Assert.Equal("0.3.0", opening.InstalledVersion);
            Assert.True(opening.IsVisible);

            // This session's own answer, with its notes and link, when it offers the same release.
            var answered = new UpdateStatus { State = UpdateState.UpdateAvailable, LatestVersion = "0.4.0", Notes = "Notes", Url = "u" };
            Assert.Same(answered, UpdateMark.Opening(true, answered, "0.4.0", "0.3.0"));
        }

        [Fact]
        public void The_panel_does_not_offer_again_what_the_rig_has_caught_up_with()
        {
            var answered = new UpdateStatus { State = UpdateState.UpdateAvailable, LatestVersion = "0.4.0" };
            var opening = UpdateMark.Opening(true, answered, null, "0.4.0");
            Assert.Equal(UpdateState.Idle, opening.State);
            Assert.False(opening.IsVisible);
        }

        [Fact]
        public void The_panel_opens_silent_with_the_check_off_or_nothing_to_say()
        {
            Assert.Equal(UpdateState.Idle, UpdateMark.Opening(false, null, "0.4.0", "0.3.0").State);
            Assert.False(UpdateMark.Opening(true, null, null, "0.3.0").IsVisible);
            // An answer that offered nothing stands as it was, silent unless a press asked for it.
            var upToDate = new UpdateStatus { State = UpdateState.UpToDate, InstalledVersion = "0.4.0" };
            Assert.Same(upToDate, UpdateMark.Opening(true, upToDate, null, "0.4.0"));
            Assert.False(upToDate.IsVisible);
        }

        // Applied

        /// <summary>
        /// After an update, the line says up to date only once what the rig runs has reached the release.
        /// </summary>
        /// <remarks>
        /// It said so whenever a dashboard had been replaced, and a run that wrote the dashboards and staged the
        /// plugin leaves the old plugin running until SimHub restarts. Measured against the rig that is still a
        /// release behind, so the offer stands and the panel goes on asking for the restart.
        /// </remarks>
        [Fact]
        public void An_update_is_up_to_date_only_when_the_rig_has_reached_the_release()
        {
            var offer = new UpdateStatus { State = UpdateState.UpdateAvailable, InstalledVersion = "0.3.0", LatestVersion = "0.4.0" };

            // The plugin staged, so the rig still runs 0.3.0 whatever the dashboards say.
            Assert.Same(offer, UpdateMark.Applied(offer, true, "0.4.0", "0.3.0"));
            // A run that did not finish changes nothing it could be wrong about.
            Assert.Same(offer, UpdateMark.Applied(offer, false, "0.4.0", "0.4.0"));
            Assert.Same(offer, UpdateMark.Applied(offer, true, null, "0.4.0"));

            // The dashboards were all that was behind, and they have moved.
            var caughtUp = UpdateMark.Applied(offer, true, "0.4.0", "0.4.0");
            Assert.Equal(UpdateState.UpToDate, caughtUp.State);
            Assert.Equal("You have the newest release, 0.4.0.", caughtUp.Line);
            Assert.True(caughtUp.IsVisible);
        }

        [Fact]
        public void A_press_answered_by_a_check_already_running_is_still_a_press()
        {
            var background = new UpdateStatus { State = UpdateState.UpToDate, InstalledVersion = "0.4.0", Manual = false };
            Assert.False(background.IsVisible);
            var pressed = background.AsManual();
            Assert.True(pressed.IsVisible);
            Assert.Equal(background.InstalledVersion, pressed.InstalledVersion);
            Assert.Equal(background.State, pressed.State);
            Assert.False(background.Manual);
        }

        // The contract

        [Fact]
        public void The_mark_reads_two_shared_properties()
        {
            var shared = Contract.SharedPropertyNames().ToList();
            Assert.Contains(Contract.UpdateAvailable, shared);
            Assert.Contains(Contract.UpdateVersion, shared);
            // Appended together, so every name that shipped before them keeps its index.
            var at = shared.IndexOf(Contract.UpdateAvailable);
            Assert.Equal(new[] { Contract.UpdateAvailable, Contract.UpdateVersion }, shared.Skip(at).Take(2));
            Assert.Equal(shared.Count - 3, at);
        }
    }
}
