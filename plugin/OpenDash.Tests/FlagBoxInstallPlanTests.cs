// FlagBoxInstallPlanTests: what the Install button decides to do, and what it says it will do.
//
// The act itself (FlagBoxInstaller.cs) needs SimHub's own types and cannot run here. What can run is
// every branch of the decision, which is where the damage would be: replacing a profile the user made,
// or offering an update that is not one.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class FlagBoxInstallPlanTests
    {
        private static readonly Guid Ours = new Guid("11111111-1111-5111-8111-111111111111");
        private static readonly Guid Theirs = new Guid("22222222-2222-5222-8222-222222222222");

        private const string V1 = "The alert catalogue. Built by openDash 0.2.0-rc.1; do not edit here.";
        private const string V2 = "The alert catalogue. Built by openDash 0.3.0; do not edit here.";

        private static InstalledProfile Profile(Guid id, string description, string name = "x")
        {
            return new InstalledProfile { ProfileId = id, Name = name, Description = description };
        }

        [Fact]
        public void Reads_the_version_the_build_stamped_in()
        {
            Assert.Equal("0.2.0-rc.1", FlagBoxInstallPlan.VersionOf(V1));
            Assert.Equal("0.3.0", FlagBoxInstallPlan.VersionOf(V2));
            Assert.Equal("1.0.0+build.7", FlagBoxInstallPlan.VersionOf("Built by openDash 1.0.0+build.7, honest."));
        }

        [Fact]
        public void A_description_with_no_version_reads_as_none_rather_than_as_a_guess()
        {
            Assert.Null(FlagBoxInstallPlan.VersionOf(null));
            Assert.Null(FlagBoxInstallPlan.VersionOf(""));
            Assert.Null(FlagBoxInstallPlan.VersionOf("A profile somebody made by hand."));
            Assert.Null(FlagBoxInstallPlan.VersionOf("Built by openDash "));
        }

        [Fact]
        public void Nothing_installed_means_install()
        {
            var plan = FlagBoxInstallPlan.Decide(Ours, V1, new List<InstalledProfile>());
            Assert.Equal(FlagBoxInstallState.NotInstalled, plan.State);
            Assert.True(plan.WouldChange);
            Assert.Equal("Install into SimHub", FlagBoxInstallPlan.ButtonLabel(plan));
        }

        [Fact]
        public void The_same_version_installed_means_nothing_to_do()
        {
            var plan = FlagBoxInstallPlan.Decide(Ours, V1, new[] { Profile(Ours, V1) });
            Assert.Equal(FlagBoxInstallState.UpToDate, plan.State);
            Assert.False(plan.WouldChange);
            Assert.Equal("0.2.0-rc.1", plan.InstalledVersion);
            // Still pressable, because a user who has broken it in SimHub needs a way back.
            Assert.Equal("Reinstall in SimHub", FlagBoxInstallPlan.ButtonLabel(plan));
        }

        [Fact]
        public void An_older_version_installed_means_update()
        {
            var plan = FlagBoxInstallPlan.Decide(Ours, V2, new[] { Profile(Ours, V1) });
            Assert.Equal(FlagBoxInstallState.Outdated, plan.State);
            Assert.True(plan.WouldChange);
            Assert.Equal("0.2.0-rc.1", plan.InstalledVersion);
            Assert.Equal("0.3.0", plan.EmbeddedVersion);
            Assert.Equal("Update in SimHub", FlagBoxInstallPlan.ButtonLabel(plan));
        }

        [Fact]
        public void A_profile_the_user_made_is_never_ours_to_replace()
        {
            // The whole safety of this: we match on OUR ProfileId and nothing else. Somebody else's
            // profile, even one named the same or carrying a version-looking description, is invisible.
            var theirs = new[]
            {
                Profile(Theirs, "My own flag box", "openDash Flag box"),
                Profile(Theirs, V1, "Built by openDash 0.2.0-rc.1"),
            };
            var plan = FlagBoxInstallPlan.Decide(Ours, V1, theirs);
            Assert.Equal(FlagBoxInstallState.NotInstalled, plan.State);
            Assert.Null(plan.Existing);
        }

        [Fact]
        public void Ours_is_found_among_the_users_own_profiles()
        {
            var mixed = new[] { Profile(Theirs, "mine"), Profile(Ours, V1), Profile(Theirs, "also mine") };
            var plan = FlagBoxInstallPlan.Decide(Ours, V1, mixed);
            Assert.Equal(FlagBoxInstallState.UpToDate, plan.State);
            Assert.Equal(Ours, plan.Existing);
        }

        [Fact]
        public void An_installed_copy_with_no_version_counts_as_out_of_date()
        {
            // Somebody imported the file by hand from an older build, or edited the description.
            // Offering the update is right; claiming it is current is not.
            var plan = FlagBoxInstallPlan.Decide(Ours, V1, new[] { Profile(Ours, "openDash flag box") });
            Assert.Equal(FlagBoxInstallState.Outdated, plan.State);
            Assert.Null(plan.InstalledVersion);
        }

        [Fact]
        public void No_profile_embedded_and_no_SimHub_are_different_answers()
        {
            Assert.Equal(FlagBoxInstallState.NotEmbedded, FlagBoxInstallPlan.Decide(Guid.Empty, V1, new List<InstalledProfile>()).State);
            // null means "SimHub's matrix settings could not be reached", which is not the same as
            // "SimHub has no profiles" and must not read as "not installed".
            Assert.Equal(FlagBoxInstallState.Unavailable, FlagBoxInstallPlan.Decide(Ours, V1, null).State);
        }

        [Fact]
        public void A_null_entry_in_SimHubs_list_does_not_throw()
        {
            var plan = FlagBoxInstallPlan.Decide(Ours, V1, new InstalledProfile[] { null, Profile(Ours, V1) });
            Assert.Equal(FlagBoxInstallState.UpToDate, plan.State);
        }

        [Fact]
        public void The_summary_warns_before_an_update_overwrites_edits()
        {
            // Matching by ProfileId means a copy the user has edited in SimHub keeps our id, so an
            // update replaces their work. They have to be told that before they press it.
            var outdated = FlagBoxInstallPlan.Decide(Ours, V2, new[] { Profile(Ours, V1) });
            var text = FlagBoxInstallPlan.Summary(outdated, null);
            Assert.Contains("replaces", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("0.2.0-rc.1", text, StringComparison.Ordinal);
            Assert.Contains("0.3.0", text, StringComparison.Ordinal);
        }

        [Fact]
        public void The_summary_says_that_installing_is_not_selecting()
        {
            // AddProfile appends to the list; SimHub picks the current profile from its own persisted
            // activeProfileId. A user who presses the button and sees nothing on the box has been told
            // half the job.
            var installed = FlagBoxInstallPlan.Summary(FlagBoxInstallPlan.Decide(Ours, V1, new[] { Profile(Ours, V1) }), null);
            Assert.Contains("Select it", installed, StringComparison.OrdinalIgnoreCase);
            var fresh = FlagBoxInstallPlan.Summary(FlagBoxInstallPlan.Decide(Ours, V1, new List<InstalledProfile>()), null);
            Assert.Contains("pick it", fresh, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void The_summary_falls_back_to_the_file_when_SimHub_cannot_be_reached()
        {
            var text = FlagBoxInstallPlan.Summary(FlagBoxInstallPlan.Decide(Ours, V1, null), @"C:\SimHub\OpenDash\x.ledsprofile");
            Assert.Contains("by hand", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(@"C:\SimHub\OpenDash\x.ledsprofile", text, StringComparison.Ordinal);
        }

        [Fact]
        public void The_version_marker_matches_the_one_the_build_writes()
        {
            // The two halves of one contract: flagBoxVersion() in packages/dash/src/leds/profile.ts
            // writes it, this reads it. A rename on either side breaks installing silently, so the
            // built profile itself is the fixture whenever one has been built.
            var built = Path.Combine(RepoPaths.Root(), "build");
            if (!Directory.Exists(built)) return;
            var profile = Directory.GetFiles(built, "*.ledsprofile").FirstOrDefault();
            if (profile == null) return;

            var json = File.ReadAllText(profile);
            var description = FlagBoxProfile.DescriptionOf(json);
            Assert.NotNull(description);
            var version = FlagBoxInstallPlan.VersionOf(description);
            Assert.False(string.IsNullOrEmpty(version));
            Assert.Matches(@"^\d+\.\d+\.\d+", version);
            Assert.Equal(FlagBoxInstallPlan.Author, FlagBoxProfile.AuthorOf(json));
        }
    }
}
