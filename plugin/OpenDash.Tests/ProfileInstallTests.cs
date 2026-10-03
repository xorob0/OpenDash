// ProfileInstallTests: what the lighting install does to SimHub's profile lists, over plain lists.
//
// SimHub's Profiles and AvailableProfiles are ObservableCollections the installer only ever touches as a
// non-generic IList, so a List<object> of stand-in profiles is the whole of what it sees. The cases that
// matter are the ones SimHub cannot be made to produce on demand: an add that throws, part way through a
// group or part way through itself, and a device whose two lists are different collections.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class ProfileInstallTests
    {
        private const string Old = "Built by OpenDash 0.2.0; do not edit here.";
        private const string New = "Built by OpenDash 0.3.0; do not edit here.";

        /// <summary>A profile as the installer sees one: an id, a name and the description the version lives in.</summary>
        private sealed class Stand
        {
            public Guid Id;
            public string Name;
            public string Description;

            public override string ToString() => Name + " " + (Description == New ? "new" : "old");
        }

        private static InstalledProfile Read(object entry)
        {
            var stand = entry as Stand;
            return stand == null ? null : new InstalledProfile { ProfileId = stand.Id, Name = stand.Name, Description = stand.Description };
        }

        private static Stand Profile(string name, string description)
        {
            return new Stand { Id = new Guid(name.GetHashCode(), 0, 0, new byte[8]), Name = name, Description = description };
        }

        private static string[] Names(IList list) => list.Cast<object>().Select(e => e.ToString()).ToArray();

        private static IList<FlagBoxPlan> Install(
            IList profiles, IList available, IList<Stand> embedded, Action<int> add, Action save, ListLog log)
        {
            return ProfileInstall.Install(profiles, available, embedded.Cast<object>().ToList(), add, save, "RGB LED", Read, log, where: "Wheel");
        }

        [Fact]
        public void An_add_that_throws_leaves_the_old_copy_in_both_lists()
        {
            var mine = Profile("Rim", Old);
            var theirs = Profile("Theirs", Old);
            var profiles = new List<object> { mine, theirs };
            var available = new List<object> { theirs, mine };
            var saves = 0;

            var plans = Install(profiles, available, new[] { Profile("Rim", New) }, i => throw new InvalidOperationException("SimHub said no"), () => saves++, new ListLog());

            Assert.Equal(FlagBoxInstallState.Failed, plans[0].State);
            Assert.Equal(new[] { "Rim old", "Theirs old" }, Names(profiles));
            Assert.Equal(new[] { "Theirs old", "Rim old" }, Names(available));
            Assert.Equal(0, saves);
        }

        [Fact]
        public void An_add_that_throws_half_way_takes_back_what_it_added_and_puts_the_old_copy_where_it_was()
        {
            var profiles = new List<object> { Profile("Rim", Old), Profile("Theirs", Old) };
            var incoming = Profile("Rim", New);

            var plans = Install(profiles, profiles, new[] { incoming }, i =>
            {
                profiles.Add(incoming);
                throw new InvalidOperationException("RefreshSortedProfiles threw");
            }, () => { }, new ListLog());

            Assert.Equal(FlagBoxInstallState.Failed, plans[0].State);
            Assert.Equal(new[] { "Rim old", "Theirs old" }, Names(profiles));
        }

        [Fact]
        public void A_group_with_one_failed_member_saves_that_member_as_it_was()
        {
            var profiles = new List<object> { Profile("Rim", Old), Profile("Brow", Old) };
            var rim = Profile("Rim", New);
            var brow = Profile("Brow", New);
            string[] saved = null;

            var plans = Install(profiles, profiles, new[] { rim, brow }, i =>
            {
                if (i == 1) throw new InvalidOperationException("SimHub said no");
                profiles.Add(rim);
            }, () => saved = Names(profiles), new ListLog());

            Assert.Equal(FlagBoxInstallState.UpToDate, plans[0].State);
            Assert.Equal(FlagBoxInstallState.Failed, plans[1].State);
            // What SimHub serialised: the new Rim, and the old Brow rather than no Brow at all.
            Assert.Equal(new[] { "Brow old", "Rim new" }, saved);
        }

        [Fact]
        public void A_replaced_profile_is_counted_once_when_the_two_lists_are_different_collections()
        {
            var mine = Profile("Rim", Old);
            var profiles = new List<object> { mine };
            var available = new List<object> { mine };
            var incoming = Profile("Rim", New);
            var log = new ListLog();

            var plans = Install(profiles, available, new[] { incoming }, i => profiles.Add(incoming), () => { }, log);

            Assert.Equal(FlagBoxInstallState.UpToDate, plans[0].State);
            Assert.Empty(available);
            Assert.Contains(
                "info: Installed 1 RGB LED profile(s) into Wheel, 1 of them replacing a copy already there."
                + " Select one on the device to use it: installing adds a profile, it does not switch to one.",
                log.Lines);
        }

        [Fact]
        public void A_failed_member_is_not_counted_as_a_replacement()
        {
            var profiles = new List<object> { Profile("Rim", Old), Profile("Brow", Old) };
            var rim = Profile("Rim", New);
            var log = new ListLog();

            Install(profiles, profiles, new[] { rim, Profile("Brow", New) }, i =>
            {
                if (i == 1) throw new InvalidOperationException("SimHub said no");
                profiles.Add(rim);
            }, () => { }, log);

            Assert.Contains(log.Lines, line => line.StartsWith("info: Installed 1 RGB LED profile(s) into Wheel, 1 of them replacing", StringComparison.Ordinal));
        }

        [Fact]
        public void An_install_replaces_ours_and_leaves_everything_else_where_it_was()
        {
            var profiles = new List<object> { Profile("Theirs", Old), Profile("Rim", Old), Profile("Other", Old) };
            var incoming = Profile("Rim", New);
            var saves = 0;
            var log = new ListLog();

            var plans = Install(profiles, profiles, new[] { incoming }, i => profiles.Add(incoming), () => saves++, log);

            Assert.Equal(FlagBoxInstallState.UpToDate, plans[0].State);
            Assert.Equal(new[] { "Theirs old", "Other old", "Rim new" }, Names(profiles));
            Assert.Equal(1, saves);
            Assert.Contains(log.Lines, line => line.StartsWith("info: Installed 1 RGB LED profile(s) into Wheel, 1 of them replacing", StringComparison.Ordinal));
        }

        [Fact]
        public void A_first_install_replaces_nothing()
        {
            var profiles = new List<object> { Profile("Theirs", Old) };
            var incoming = Profile("Rim", New);
            var log = new ListLog();

            var plans = Install(profiles, profiles, new[] { incoming }, i => profiles.Add(incoming), () => { }, log);

            Assert.Equal(FlagBoxInstallState.UpToDate, plans[0].State);
            Assert.Contains(log.Lines, line => line.StartsWith("info: Installed 1 RGB LED profile(s) into Wheel, 0 of them replacing", StringComparison.Ordinal));
        }
    }
}
