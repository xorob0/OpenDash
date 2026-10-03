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
