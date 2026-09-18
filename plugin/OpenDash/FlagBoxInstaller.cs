// FlagBoxInstaller.cs: installing openDash's light profiles through SimHub's own object model.
//
// ADR 0013 originally said the plugin could not install this, because matrix profiles live inside
// PluginsData/Common/ArduinoRGBMatrixSettings.json and SimHub rewrites that file itself. That was
// true about the FILE and wrong about the conclusion: SimHub exposes the whole chain publicly, so we
// never touch the file at all -- we hand SimHub a profile object and SimHub writes its own settings.
//
//     PluginManager.GetInstance()
//       .GetPlugin<SerialDashPlugin>()          SimHub.Plugins.dll, public
//       .Settings.RGBMatrixDriver               public        .Settings.RGBLedsDriver
//       .Settings                               public        MatrixSettings / LedsSettings
//       .AddProfile(profile)                    public, from ProfileSettingsBase on both
//     driver.SaveSettings()                     public -- SimHub serialises its own collection
//
// Two drivers, not one. SimHub's strips are RGBLedsDriver and its 8x8 matrix is RGBMatrixDriver; they
// are different types holding different profile types and they share the ".ledsprofile" extension,
// which is exactly why FlagBoxProfile picks a profile out by its file name and never by position. The
// remove-add-save rule is the part that must not be allowed to differ between them, so ProfileInstall
// below holds it once and each installer supplies its own driver.
//
// It is written over `IProfile` and the NON-generic `IList` rather than over
// `IProfileSettings<TProfile>`, which would read better and does not compile: constraining a type
// parameter to `IProfile` makes the compiler enumerate the interfaces of SimHub's two profile classes,
// and the strip's implements IDragSource and IDropTarget from GongSolutions.WPF.DragDrop, which SimHub
// ships and plugin/lib does not carry. Nothing here needs the concrete types, so nothing here names
// them; the two callers do, and they only ever touch members that resolve.
//
// No reflection and no file merge, so the clobbering objection is gone. What remains is consent, and
// that is why this runs from a button rather than at startup: a profile paints hardware the user owns.
//
// Everything here needs SimHub types, so this file is NOT compiled into OpenDash.Tests; the decision
// it acts on is in FlagBoxInstallPlan.cs, which is. The reflection-free chain is asserted by the
// plugin building at all, and the two drivers are exercised on the VM (docs/dev-loop.md).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SimHub.Plugins;
using SimHub.Plugins.DataPlugins.RGBMatrixDriver;
using SimHub.Plugins.DataPlugins.RGBMatrixDriver.Settings;
using SimHub.Plugins.OutputPlugins.Dash;
using SimHub.Plugins.ProfilesCommon;
using LedsDriver = SimHub.Plugins.DataPlugins.RGBDriver.RGBLedsDriver;
using LedsProfile = SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile;

namespace OpenDashPlugin
{
    public static class FlagBoxInstaller
    {
        /// <summary>
        /// SimHub's matrix driver for a plain Arduino matrix, or null when it cannot be reached --
        /// an older SimHub, a build without the serial dash plugin, or the plugin not yet loaded.
        /// Every caller treats null as "say so and fall back to the file on disk".
        /// </summary>
        public static RGBMatrixDriver Driver()
        {
            return ProfileInstall.Driver(s => s.RGBMatrixDriver, "matrix");
        }

        /// <summary>The profiles SimHub currently holds, or null when the driver is unreachable.</summary>
        public static List<InstalledProfile> Installed()
        {
            var settings = Driver()?.Settings;
            return settings == null ? null : ProfileInstall.Census(settings.Profiles, "matrix");
        }

        /// <summary>What the panel shows: the embedded profile compared with what SimHub holds.</summary>
        public static FlagBoxPlan Plan(string embeddedJson)
        {
            var embedded = Parse(embeddedJson);
            if (embedded == null) return new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded };
            return FlagBoxInstallPlan.Decide(embedded.ProfileId, embedded.Description, Installed());
        }

        /// <summary>
        /// Adds the profile to SimHub, replacing an older copy of our own, and asks SimHub to save.
        ///
        /// Runs on the UI thread: `Profiles` is an ObservableCollection and WPF bindings are watching
        /// it, so mutating it from a worker would throw. The button's click handler is already there.
        /// </summary>
        public static FlagBoxPlan Install(string embeddedJson)
        {
            var embedded = Parse(embeddedJson);
            if (embedded == null) return new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded };

            var driver = Driver();
            var settings = driver?.Settings;
            if (settings == null) return new FlagBoxPlan { State = FlagBoxInstallState.Unavailable };

            return ProfileInstall.Install(
                settings.Profiles,
                settings.AvailableProfiles,
                new object[] { embedded },
                index => settings.AddProfile(embedded),
                driver.SaveSettings,
                "matrix")[0];
        }

        /// <summary>
        /// The embedded JSON as SimHub's own profile object. Deserialised with Newtonsoft's defaults,
        /// which is what SimHub's own import uses (JsonExtensions.FromJsonFile is a bare
        /// JsonSerializer), so the container converter on MatrixContainerBase resolves the same way.
        /// </summary>
        public static RGBMatrixProfile Parse(string json)
        {
            return ProfileInstall.Parse<RGBMatrixProfile>(json, "flag box");
        }
    }

    /// <summary>
    /// The same install, over SimHub's OTHER lighting driver: the RGB LED strips and brows.
    ///
    /// Everything that made the matrix safe holds here unchanged -- a profile is matched by its
    /// ProfileId, ours is removed and re-added, anything else in either list is the user's and is not
    /// touched -- so the only difference is which driver the chain ends at. The panel offers one row
    /// per embedded shape; a grouped row installs its members under a single save and reads its state
    /// back off them with <see cref="FlagBoxInstallPlan.Combine"/>.
    /// </summary>
    public static class StripInstaller
    {
        /// <summary>SimHub's RGB LED driver, or null when it cannot be reached. Null is a state the
        /// driver can do nothing about rather than an error: the panel says the settings are
        /// unavailable and offers the file to import by hand, exactly as it does for the matrix.</summary>
        public static LedsDriver Driver()
        {
            return ProfileInstall.Driver(s => s.RGBLedsDriver, "RGB LED");
        }

        /// <summary>The strip profiles SimHub currently holds, or null when the driver is unreachable.</summary>
        public static List<InstalledProfile> Installed()
        {
            var settings = Driver()?.Settings;
            return settings == null ? null : ProfileInstall.Census(settings.Profiles, "RGB LED");
        }

        /// <summary>One shape's row: the embedded profile compared with what SimHub holds.</summary>
        public static FlagBoxPlan Plan(string embeddedJson)
        {
            return Plan(new[] { embeddedJson })[0];
        }

        /// <summary>
        /// A plan per member, in the order given, off ONE read of SimHub's profile list.
        ///
        /// The panel redraws its rows whenever the tab is opened, and there are nineteen of them, so
        /// asking the driver once and deciding nineteen times is the difference between one traversal
        /// of the user's profiles and nineteen. <see cref="FlagBoxInstallPlan.Combine"/> turns these
        /// into the grouped row's own state.
        /// </summary>
        public static IList<FlagBoxPlan> Plan(IEnumerable<string> embeddedJsons)
        {
            var parsed = (embeddedJsons ?? Enumerable.Empty<string>()).Select(Parse).ToList();
            var installed = Installed();
            return parsed
                .Select(p => p == null
                    ? new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded }
                    : FlagBoxInstallPlan.Decide(p.ProfileId, p.Description, installed))
                .ToList();
        }

        /// <summary>One shape, installed. The list form with a single member.</summary>
        public static FlagBoxPlan Install(string embeddedJson)
        {
            return Install(new[] { embeddedJson })[0];
        }

        /// <summary>
        /// Every member of a group, installed under one save, and a plan per member in the order given.
        ///
        /// Per member rather than one verdict, because a row that installs several profiles and reports
        /// a single boolean cannot say which one failed. Everything is parsed before anything is
        /// touched, and SimHub is asked to save once at the end, so a member that cannot be read costs
        /// the others nothing and a failure part way through cannot leave the settings file holding
        /// half a group.
        ///
        /// Runs on the UI thread for the same reason the matrix install does.
        /// </summary>
        public static IList<FlagBoxPlan> Install(IEnumerable<string> embeddedJsons)
        {
            var parsed = (embeddedJsons ?? Enumerable.Empty<string>()).Select(Parse).ToList();

            var driver = Driver();
            var settings = driver?.Settings;
            if (settings == null)
            {
                return parsed
                    .Select(p => new FlagBoxPlan
                    {
                        State = p == null ? FlagBoxInstallState.NotEmbedded : FlagBoxInstallState.Unavailable,
                    })
                    .ToList();
            }

            return ProfileInstall.Install(
                settings.Profiles,
                settings.AvailableProfiles,
                parsed.Cast<object>().ToList(),
                index => settings.AddProfile(parsed[index]),
                driver.SaveSettings,
                "RGB LED");
        }

        /// <summary>
        /// Takes one of our profiles out of SimHub, by the id the bar it belonged to derives.
        /// </summary>
        /// <remarks>
        /// The removal half of an install and nothing else: a bar that has been taken off the rig has no
        /// settings attached any more, so a profile left behind would be a row in SimHub's list reading
        /// properties nothing fills. Only the id given goes -- anything else in either list is the
        /// user's, which is the same promise the install makes.
        /// </remarks>
        public static bool Uninstall(Guid profileId)
        {
            var driver = Driver();
            var settings = driver?.Settings;
            if (settings == null) return false;
            return ProfileInstall.Uninstall(settings.Profiles, settings.AvailableProfiles, profileId, driver.SaveSettings, "RGB LED");
        }

        /// <summary>The embedded JSON as SimHub's own strip profile. The same bare Newtonsoft defaults
        /// the matrix uses, so RGBDriver's own LedContainerJsonConverter -- which is attached to
        /// LedsContainerBase by attribute rather than by serialiser settings -- resolves every
        /// ContainerType exactly as SimHub's own import does.</summary>
        public static LedsProfile Parse(string json)
        {
            return ProfileInstall.Parse<LedsProfile>(json, "strip");
        }
    }

    /// <summary>
    /// The half of an install that is the same for both lighting drivers.
    ///
    /// The second consumer is what earns the abstraction, and this is the rule that must not be allowed
    /// to drift between the two: match by ProfileId, remove ours from both lists, add the embedded one,
    /// save once, read the list back. See the file header for why it is written over `IProfile` and the
    /// non-generic `IList` rather than over SimHub's own generic settings interface.
    /// </summary>
    internal static class ProfileInstall
    {
        /// <summary>One lighting driver off the serial dash plugin's settings, or null when the chain is
        /// broken anywhere along it.</summary>
        internal static TDriver Driver<TDriver>(Func<SerialDashPluginSettings, TDriver> pick, string what)
            where TDriver : class
        {
            try
            {
                var manager = PluginManager.GetInstance();
                if (manager == null) return null;
                var serial = manager.GetPlugin<SerialDashPlugin>();
                var settings = serial?.Settings;
                return settings == null ? null : pick(settings);
            }
            catch (Exception e)
            {
                Log.Warn("SimHub's " + what + " driver could not be reached: " + e.Message);
                return null;
            }
        }

        /// <summary>What SimHub holds, reduced to what the decision needs, or null when it cannot be
        /// read. Null is "could not be reached", which the plan turns into Unavailable; an empty list is
        /// "SimHub has none of ours", which is NotInstalled, and the two must not be confused.</summary>
        internal static List<InstalledProfile> Census(IEnumerable profiles, string what)
        {
            if (profiles == null) return null;
            try
            {
                var census = new List<InstalledProfile>();
                foreach (var entry in profiles)
                {
                    var profile = entry as IProfile;
                    if (profile == null) continue;
                    census.Add(new InstalledProfile
                    {
                        ProfileId = profile.ProfileId,
                        Name = profile.Name,
                        Description = profile.Description,
                    });
                }
                return census;
            }
            catch (Exception e)
            {
                Log.Warn("SimHub's " + what + " profiles could not be read: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Removes our copies, adds the embedded ones and asks SimHub to save once, returning a plan per
        /// member in the order given. A null member is one that could not be parsed: it is reported
        /// NotEmbedded and nothing is touched on its behalf.
        /// </summary>
        internal static IList<FlagBoxPlan> Install(
            IList profiles, IList available, IReadOnlyList<object> embedded, Action<int> add, Action save, string what)
        {
            var results = new FlagBoxPlan[embedded.Count];
            var replaced = 0;
            var changed = false;
            for (var i = 0; i < embedded.Count; i++)
            {
                var profile = embedded[i] as IProfile;
                if (profile == null)
                {
                    results[i] = new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded };
                    continue;
                }
                try
                {
                    // Ours is the one carrying our ProfileId. Anything else in either list is the user's
                    // and is not touched, which is the whole reason this is safer than merging the file.
                    //
                    // Both lists, because AddProfile appends to AvailableProfiles while the file is
                    // serialised from Profiles. For a plain Arduino matrix those are the same collection
                    // -- AvailableProfiles returns Profiles unless the settings filter by game family or
                    // have a built-in profiles path (ProfileSettingsBase.cs:422) -- but the LED driver
                    // does have built-in profiles, the ones a device maker ships under
                    // DevicesDefinitions/.../BuiltInLedsProfiles, so there the two genuinely part company
                    // and removing from only one would leave a duplicate. BuiltInProfiles itself is never
                    // touched: those profiles are the device maker's, not ours and not the user's.
                    replaced += Remove(profiles, profile.ProfileId);
                    if (!ReferenceEquals(available, profiles))
                    {
                        replaced += Remove(available, profile.ProfileId);
                    }

                    add(i);
                    changed = true;
                }
                catch (Exception e)
                {
                    Log.Error("Installing the " + Name(profile) + " profile into SimHub failed", e);
                    results[i] = new FlagBoxPlan { State = FlagBoxInstallState.Failed };
                }
            }

            // One save for the whole group, and only when something actually changed. SimHub serialises
            // its own collection in one call, so the file on disk either holds the list as it now stands
            // or is untouched; there is no state in which it holds half a group.
            if (changed && !Save(save, what))
            {
                for (var i = 0; i < results.Length; i++)
                {
                    if (results[i] == null) results[i] = new FlagBoxPlan { State = FlagBoxInstallState.Failed };
                }
                return results;
            }

            // AddProfile re-GUIDs any profile whose id already exists in the target list
            // (ProfileSettingsBase.cs:853-858), and the newcomer is the one it renames. That cannot
            // happen while the removal above works, but if it ever stops working the symptom is silent:
            // our profile becomes unrecognisable and the next install adds a second copy. Reading the
            // list back costs one comparison per member and turns that into a log line.
            var installed = Census(profiles, what);
            var added = 0;
            for (var i = 0; i < results.Length; i++)
            {
                if (results[i] != null) continue;
                var profile = (IProfile)embedded[i];
                var plan = FlagBoxInstallPlan.Decide(profile.ProfileId, profile.Description, installed);
                if (plan.State == FlagBoxInstallState.UpToDate)
                {
                    added++;
                }
                else if (installed != null)
                {
                    // Only when the list could be read at all. A list that could not be read says
                    // nothing about whether the profile is in it, and Census has already logged why.
                    Log.Warn("The " + Name(profile) + " profile was added but cannot be found again by its id;"
                        + " SimHub may have renumbered it because a copy was already present. Check SimHub's "
                        + what + " profile list.");
                }
                results[i] = plan;
            }

            if (added > 0)
            {
                Log.Info("Installed " + added + " " + what + " profile(s) into SimHub, " + replaced
                    + " of them replacing a copy already there. Select one on the device to use it:"
                    + " installing adds a profile, it does not switch to one.");
            }
            return results;
        }

        /// <summary>Removes one profile id from both lists and saves once. False when nothing went,
        /// which is also what a profile that was never there gives.</summary>
        internal static bool Uninstall(IList profiles, IList available, Guid id, Action save, string what)
        {
            var gone = Remove(profiles, id);
            if (!ReferenceEquals(available, profiles)) gone += Remove(available, id);
            if (gone == 0) return false;
            if (!Save(save, what)) return false;
            Log.Info("Removed " + gone + " " + what + " profile(s) from SimHub.");
            return true;
        }

        private static bool Save(Action save, string what)
        {
            try
            {
                save();
                return true;
            }
            catch (Exception e)
            {
                Log.Error("SimHub could not save its " + what + " settings after installing", e);
                return false;
            }
        }

        /// <summary>Drops every copy of one profile id from a collection; returns how many went.</summary>
        private static int Remove(IList list, Guid id)
        {
            if (list == null) return 0;
            var mine = new List<object>();
            foreach (var entry in list)
            {
                var profile = entry as IProfile;
                if (profile != null && profile.ProfileId == id) mine.Add(entry);
            }
            foreach (var old in mine) list.Remove(old);
            return mine.Count;
        }

        /// <summary>The embedded JSON as SimHub's own profile object, or null when it cannot be read.</summary>
        internal static TProfile Parse<TProfile>(string json, string what) where TProfile : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonConvert.DeserializeObject<TProfile>(json);
            }
            catch (Exception e)
            {
                Log.Error("The embedded " + what + " profile could not be read: " + e.Message);
                return null;
            }
        }

        private static string Name(IProfile profile)
        {
            return string.IsNullOrEmpty(profile.Name) ? "light" : profile.Name;
        }
    }
}
