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

        // #689: a settings object that keeps its profiles per game family. SimHub's ProfileSettingsBase then draws the
        // current family's list on the device's page (AvailableProfiles finds it under PerFamilyProfiles), and at every
        // save OnSerializing clears Profiles and refills it from the families. These stand in for the three lists and
        // that save, which is the step that used to lose a profile added to Profiles alone.

        /// <summary>SimHub's save under the family filter: Profiles cleared and rebuilt from every family's list.</summary>
        private static Action MirrorFamilies(IList profiles, params IList[] families)
        {
            return () =>
            {
                profiles.Clear();
                foreach (var family in families)
                {
                    foreach (var entry in family) profiles.Add(entry);
                }
            };
        }

        private static IList<FlagBoxPlan> Install(ProfileLists lists, IList<Stand> embedded, Action<int> add, Action save, ListLog log)
        {
            return ProfileInstall.Install(lists, embedded.Cast<object>().ToList(), add, save, "RGB LED", Read, log, where: "Wheel");
        }

        [Fact]
        public void Without_the_family_filter_the_lists_are_the_saved_one_and_the_one_the_page_draws()
        {
            var profiles = new List<object>();
            var builtIn = new List<object>();

            var same = ProfileLists.Of(profiles, profiles);
            Assert.Same(profiles, same.Saved);
            Assert.Empty(same.Others);
            Assert.Null(same.Family);
            Assert.False(same.ByFamily);

            var apart = ProfileLists.Of(profiles, builtIn);
            Assert.Collection(apart.Others, list => Assert.Same(builtIn, list));
            Assert.Null(apart.Family);

            // Families handed over without the filter are not SimHub's lists for this object and are not touched.
            var ignored = ProfileLists.Of(profiles, profiles, false, new IList[] { new List<object>() });
            Assert.Empty(ignored.Others);
        }

        [Fact]
        public void Under_the_family_filter_the_add_goes_to_the_current_familys_list_and_every_family_is_swept()
        {
            var profiles = new List<object>();
            var racing = new List<object>();
            var flight = new List<object>();

            // AvailableProfiles is the racing list, and it is also among the families: held once.
            var lists = ProfileLists.Of(profiles, racing, true, new IList[] { racing, flight, null, profiles });

            Assert.Same(racing, lists.Family);
            Assert.True(lists.ByFamily);
            Assert.Collection(lists.Others, list => Assert.Same(racing, list), list => Assert.Same(flight, list));
        }

        [Fact]
        public void Under_the_family_filter_an_installed_profile_is_on_the_page_and_survives_the_save()
        {
            var theirs = Profile("Theirs", Old);
            var profiles = new List<object> { theirs };
            var racing = new List<object> { theirs };
            var flight = new List<object>();
            var lists = ProfileLists.Of(profiles, racing, true, new IList[] { racing, flight });
            var incoming = Profile("Rim", New);
            var log = new ListLog();

            var plans = Install(lists, new[] { incoming }, i => ProfileInstall.AddTo(lists, incoming), MirrorFamilies(profiles, racing, flight), log);

            // In the list the page draws, and still in Profiles after the save rebuilt it from the families, which
            // is what the census read back: a profile added to Profiles alone was counted installed until this save.
            Assert.Equal(FlagBoxInstallState.UpToDate, plans[0].State);
            Assert.Equal(new[] { "Theirs old", "Rim new" }, Names(racing));
            Assert.Equal(new[] { "Theirs old", "Rim new" }, Names(profiles));
            Assert.Empty(flight);
            Assert.DoesNotContain(log.Lines, line => line.Contains("cannot be found again"));
        }

        [Fact]
        public void A_profile_added_to_the_saved_list_alone_is_lost_at_the_save_under_the_family_filter()
        {
            // The defect itself, over the same lists: what the install did before #689.
            var profiles = new List<object>();
            var racing = new List<object>();
            var lists = ProfileLists.Of(profiles, racing, true, new IList[] { racing });
            var incoming = Profile("Rim", New);
            var log = new ListLog();

            var plans = Install(lists, new[] { incoming }, i => profiles.Add(incoming), MirrorFamilies(profiles, racing), log);

            Assert.NotEqual(FlagBoxInstallState.UpToDate, plans[0].State);
            Assert.Empty(racing);
            Assert.Empty(profiles);
        }

        [Fact]
        public void Under_the_family_filter_a_copy_installed_under_another_family_is_replaced_not_left_behind()
        {
            var old = Profile("Rim", Old);
            var theirs = Profile("Theirs", Old);
            var profiles = new List<object> { theirs, old };
            var racing = new List<object> { theirs };
            var flight = new List<object> { old };
            var lists = ProfileLists.Of(profiles, racing, true, new IList[] { racing, flight });
            var incoming = Profile("Rim", New);
            var log = new ListLog();

            var plans = Install(lists, new[] { incoming }, i => ProfileInstall.AddTo(lists, incoming), MirrorFamilies(profiles, racing, flight), log);

            Assert.Equal(FlagBoxInstallState.UpToDate, plans[0].State);
            Assert.Equal(new[] { "Theirs old", "Rim new" }, Names(profiles));
            Assert.Equal(new[] { "Theirs old", "Rim new" }, Names(racing));
            Assert.Empty(flight);
            Assert.Contains(log.Lines, line => line.StartsWith("info: Installed 1 RGB LED profile(s) into Wheel, 1 of them replacing", StringComparison.Ordinal));
        }

        [Fact]
        public void Under_the_family_filter_an_add_that_throws_puts_every_familys_list_back()
        {
            var old = Profile("Rim", Old);
            var theirs = Profile("Theirs", Old);
            var profiles = new List<object> { old, theirs };
            var racing = new List<object> { theirs };
            var flight = new List<object> { old };
            var lists = ProfileLists.Of(profiles, racing, true, new IList[] { racing, flight });
            var incoming = Profile("Rim", New);
            var saves = 0;

            var plans = Install(lists, new[] { incoming }, i =>
            {
                ProfileInstall.AddTo(lists, incoming);
                throw new InvalidOperationException("RefreshSortedProfiles threw");
            }, () => saves++, new ListLog());

            Assert.Equal(FlagBoxInstallState.Failed, plans[0].State);
            Assert.Equal(new[] { "Rim old", "Theirs old" }, Names(profiles));
            Assert.Equal(new[] { "Theirs old" }, Names(racing));
            Assert.Equal(new[] { "Rim old" }, Names(flight));
            Assert.Equal(0, saves);
        }

        [Fact]
        public void Under_the_family_filter_an_uninstall_takes_ours_out_of_every_family_and_saves_once()
        {
            var rim = Profile("Rim", Old);
            var theirs = Profile("Theirs", Old);
            var profiles = new List<object> { theirs, rim };
            var racing = new List<object> { theirs };
            var flight = new List<object> { rim };
            var lists = ProfileLists.Of(profiles, racing, true, new IList[] { racing, flight });
            var saves = 0;

            var gone = ProfileInstall.Uninstall(lists, rim.Id, () => saves++, "RGB LED", Read, new ListLog());

            Assert.True(gone);
            Assert.Equal(new[] { "Theirs old" }, Names(profiles));
            Assert.Equal(new[] { "Theirs old" }, Names(racing));
            Assert.Empty(flight);
            Assert.Equal(1, saves);
        }

        [Fact]
        public void Without_the_family_filter_the_add_goes_to_the_saved_list_alone()
        {
            var profiles = new List<object>();
            var builtIn = new List<object>();
            var lists = ProfileLists.Of(profiles, builtIn);
            var incoming = Profile("Rim", New);

            var plans = Install(lists, new[] { incoming }, i => ProfileInstall.AddTo(lists, incoming), () => { }, new ListLog());

            // The maker's built-in list is theirs and is never added to (ProfileInstall.WhyNotAddProfile).
            Assert.Equal(FlagBoxInstallState.UpToDate, plans[0].State);
            Assert.Equal(new[] { "Rim new" }, Names(profiles));
            Assert.Empty(builtIn);
        }
    }
}
