// FlagBoxInstaller.cs: installing OpenDash's light profiles through SimHub's own object model.
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
// it acts on is in FlagBoxInstallPlan.cs and what it does to SimHub's lists is in
// ProfileInstall.Core.cs, which are. The reflection-free chain is asserted by the
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
using LedsSettings = SimHub.Plugins.DataPlugins.RGBDriver.Settings.LedsSettings;

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
                index => AddToSaved(settings, embedded),
                driver.SaveSettings,
                "matrix",
                FlagBoxInstallPlan.BuiltInModeOf(settings.HasBuiltInProfiles, settings.UseBuiltInProfiles))[0];
        }

        /// <summary>Adds to the list SimHub saves. See <see cref="ProfileInstall.WhyNotAddProfile"/>.</summary>
        private static void AddToSaved(MatrixSettings settings, RGBMatrixProfile profile)
        {
            profile.Settings = settings;
            settings.InitProfile(profile);
            settings.Profiles.Add(profile);
            settings.RefreshSortedProfiles();
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
    /// touched -- so the only difference is which driver the chain ends at. What is installed is never
    /// the embedded profile itself but one bar's copy of it, under the id that bar derives
    /// (<see cref="LedBarProfile.For"/>), so what SimHub holds is asked about by that id as well
    /// (<see cref="LedBarProfile.Plan"/>) and never by the embedded one.
    /// </summary>
    public static class StripInstaller
    {
        /// <summary>
        /// The profiles one LED device currently holds, or null when it cannot be reached.
        /// </summary>
        /// <remarks>
        /// Per device, because SimHub keeps one list per device and not one list. Null is a state a
        /// driver can do nothing about rather than an error: the panel says the settings are
        /// unavailable, exactly as it does for the matrix.
        /// </remarks>
        public static List<InstalledProfile> Installed(string device)
        {
            var target = LedTargets.Find(device);
            return target == null ? null : ProfileInstall.Census(target.Settings.Profiles, "RGB LED");
        }

        /// <summary>
        /// What every LED device on the rig holds, one list per device, off one walk of SimHub's devices.
        /// </summary>
        /// <remarks>
        /// What the Install tab's strip rows are drawn from, and read once for all of them rather than
        /// once per row or per bar: the rows are redrawn whenever the tab is opened. An entry is null for
        /// a device whose list could not be read, and the list is empty on a rig with no LED device at
        /// all, which the panel reports as unavailable.
        /// </remarks>
        public static List<List<InstalledProfile>> InstalledEverywhere()
        {
            return InstalledEverywhere(LedTargets.All());
        }

        /// <summary><see cref="InstalledEverywhere()"/> over devices already read (#611).</summary>
        public static List<List<InstalledProfile>> InstalledEverywhere(IEnumerable<LedTarget> targets)
        {
            return (targets ?? Enumerable.Empty<LedTarget>()).Select(target => ProfileInstall.Census(target.Settings.Profiles, "RGB LED")).ToList();
        }

        /// <summary>One shape, installed on one device. The list form with a single member.</summary>
        public static FlagBoxPlan Install(string embeddedJson, string device)
        {
            return Install(new[] { embeddedJson }, device)[0];
        }

        /// <summary>One shape, installed on one of devices already read (#611).</summary>
        public static FlagBoxPlan Install(string embeddedJson, string device, IEnumerable<LedTarget> targets)
        {
            return Install(new[] { embeddedJson }, device, targets)[0];
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
        public static IList<FlagBoxPlan> Install(IEnumerable<string> embeddedJsons, string device)
        {
            return Install(embeddedJsons, device, LedTargets.All());
        }

        /// <summary><see cref="Install(IEnumerable{string}, string)"/> into one of devices already read, so an install
        /// the panel makes does not walk SimHub's devices again to find the one it already has (#611).</summary>
        public static IList<FlagBoxPlan> Install(IEnumerable<string> embeddedJsons, string device, IEnumerable<LedTarget> targets)
        {
            var jsons = (embeddedJsons ?? Enumerable.Empty<string>()).ToList();
            var parsed = jsons.Select(Parse).ToList();

            var target = LedTargets.Find(device, targets);
            var settings = target?.Settings;
            if (settings == null)
            {
                return parsed
                    .Select(p => new FlagBoxPlan
                    {
                        State = p == null ? FlagBoxInstallState.NotEmbedded : FlagBoxInstallState.Unavailable,
                    })
                    .ToList();
            }

            var results = ProfileInstall.Install(
                settings.Profiles,
                settings.AvailableProfiles,
                parsed.Cast<object>().ToList(),
                index => AddToSaved(settings, parsed[index]),
                target.Save,
                "RGB LED",
                FlagBoxInstallPlan.BuiltInModeOf(settings.HasBuiltInProfiles, settings.UseBuiltInProfiles),
                target.Name);
            if (target.IndividualOnly) InstallIndividual(jsons, target, results);
            return results;
        }

        /// <summary>
        /// The second half of an install on a device set to "Individual profile only", which leaves the list
        /// just installed into off its page and draws nothing from it (#690).
        /// </summary>
        /// <remarks>
        /// Where the individual list addresses the same LEDs, every member installed above goes into it too,
        /// as a copy of its own: a profile carries a back-reference to the one list that holds it, so the
        /// object already added cannot be shared. Where it does not, or that second install fails, the member
        /// carries the note that names the switch, since it is installed and nobody can see it.
        /// </remarks>
        private static void InstallIndividual(IList<string> jsons, LedTarget target, IList<FlagBoxPlan> results)
        {
            var individual = target.Individual;
            var installed = Enumerable.Range(0, results.Count)
                .Where(i => results[i] != null && results[i].State == FlagBoxInstallState.UpToDate)
                .ToList();
            if (installed.Count == 0) return;

            if (!target.InstallsIntoIndividual || individual == null)
            {
                Log.Info(target.Name + " is set to Individual profile only, which hides and does not draw its Telemetry LEDs list,"
                    + " and its individual LEDs are not the strip's LEDs, so the profile was not put there: "
                    + FlagBoxInstallPlan.IndividualOnlyNote);
                foreach (var i in installed) results[i].Note = results[i].Note ?? FlagBoxInstallPlan.IndividualOnlyNote;
                return;
            }

            var copies = installed.Select(i => Parse(jsons[i])).ToList();
            var mirrored = ProfileInstall.Install(
                individual.Profiles,
                individual.AvailableProfiles,
                copies.Cast<object>().ToList(),
                index => AddToSaved(individual, copies[index]),
                target.Save,
                "individual LED",
                false,
                target.Name);
            for (var k = 0; k < installed.Count; k++)
            {
                if (mirrored[k] != null && mirrored[k].State == FlagBoxInstallState.UpToDate) continue;
                var i = installed[k];
                results[i].Note = results[i].Note ?? FlagBoxInstallPlan.IndividualOnlyNote;
            }
        }

        /// <summary>Adds to the list SimHub saves. See <see cref="ProfileInstall.WhyNotAddProfile"/>.</summary>
        private static void AddToSaved(LedsSettings settings, LedsProfile profile)
        {
            profile.Settings = settings;
            settings.InitProfile(profile);
            settings.Profiles.Add(profile);
            settings.RefreshSortedProfiles();
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
        public static bool Uninstall(Guid profileId, string device)
        {
            var target = LedTargets.Find(device);
            var settings = target?.Settings;
            if (settings == null) return false;
            var gone = ProfileInstall.Uninstall(settings.Profiles, settings.AvailableProfiles, profileId, target.Save, "RGB LED");
            return UninstallIndividual(target, profileId) || gone;
        }

        /// <summary>Takes our copy out of the device's individual list as well, whatever its mode is now: a copy
        /// installed under "Individual profile only" stays there after the mode is changed back (#690).</summary>
        private static bool UninstallIndividual(LedTarget target, Guid profileId)
        {
            var individual = target.Individual;
            if (individual == null) return false;
            return ProfileInstall.Uninstall(individual.Profiles, individual.AvailableProfiles, profileId, target.Save, "individual LED");
        }

        /// <summary>
        /// Takes one profile out of every LED device that holds it.
        /// </summary>
        /// <remarks>
        /// What a bar that is moving from one device to another asks, and what removing a bar asks when
        /// the device it names is gone. Nothing is left behind on the device it used to be on: a profile
        /// whose settings nothing attaches any more is a row in somebody's list that lights nothing.
        /// </remarks>
        public static bool UninstallEverywhere(Guid profileId)
        {
            return UninstallEverywhere(profileId, LedTargets.All());
        }

        /// <summary><see cref="UninstallEverywhere(Guid)"/> over devices already read (#611).</summary>
        public static bool UninstallEverywhere(Guid profileId, IEnumerable<LedTarget> targets)
        {
            var gone = false;
            foreach (var target in targets ?? Enumerable.Empty<LedTarget>())
            {
                if (ProfileInstall.Uninstall(target.Settings.Profiles, target.Settings.AvailableProfiles, profileId, target.Save, "RGB LED")) gone = true;
                if (UninstallIndividual(target, profileId)) gone = true;
            }
            return gone;
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
    ///
    /// This half is what reaches SimHub: the driver, the parse, and the reading of one list entry as a
    /// profile. The lists themselves -- what is removed, added, put back and counted -- are
    /// ProfileInstall.Core.cs, which names no SimHub type and is compiled into OpenDash.Tests.
    /// </summary>
    internal static partial class ProfileInstall
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
        /// read. The rule is in ProfileInstall.Core.cs; this is it over SimHub's own profile type.</summary>
        internal static List<InstalledProfile> Census(IEnumerable profiles, string what)
        {
            return Census(profiles, Read, what, SimHubLog);
        }

        /// <summary>
        /// Why neither installer calls SimHub's own `AddProfile`.
        /// </summary>
        /// <remarks>
        /// `settings.AddProfile(p)` adds to `AvailableProfiles`, and that is a *computed* property:
        /// `Profiles` normally, but `BuiltInProfiles` whenever the device ships built-in profiles and the
        /// user has them switched on (ProfileSettingsBase.cs:422-438). A profile appended there is not
        /// what `SaveSettings` serialises, so it is gone at the next start -- while the verification
        /// below, which reads `Profiles`, reports the install as failed. Two symptoms, one line.
        ///
        /// A wheel that ships its own profiles is exactly such a device, which is why this matters more
        /// now than when it was written: until <see cref="LedTargets"/> existed, the only settings object
        /// either installer ever touched was the Arduino RGB LEDs device's, which ships none. The
        /// separate bug that a profile installed into the Arduino's list is invisible on a wheel is that
        /// file's, and is not this.
        ///
        /// Each installer therefore does by hand what the protected `AddProfile(target, p)` does
        /// (ProfileSettingsBase.cs:842-858), against `Profiles`: the back-reference, SimHub's own
        /// per-driver initialisation, the add, and the sorted-list refresh the device's dropdown binds to.
        /// Every step is public. Two four-line methods rather than one generic helper, because a type
        /// parameter constrained to `ProfileBase` makes the compiler resolve every interface the concrete
        /// profile implements, one of which lives in an assembly SimHub does not ship to plugin authors.
        ///
        /// The one step left out is that method's de-duplication pass, which renumbers any profile whose
        /// id already appears: the caller has just removed ours by id, so there is nothing to renumber,
        /// and letting it run would hand SimHub the chance to renumber the very profile we then go looking
        /// for. `TargetGameFamily` is left alone for the same kind of reason -- it is set only under
        /// `FilterByGameFamily`, which neither lighting driver overrides to true.
        /// </remarks>
        internal const string WhyNotAddProfile =
            "SimHub's AddProfile targets AvailableProfiles, which is BuiltInProfiles on a device with "
            + "built-in profiles switched on; the saved list is Profiles.";

        /// <summary>The group install of ProfileInstall.Core.cs over SimHub's own profile type and log.</summary>
        internal static IList<FlagBoxPlan> Install(
            IList profiles, IList available, IReadOnlyList<object> embedded, Action<int> add, Action save, string what, bool builtInMode = false, string where = null)
        {
            return Install(profiles, available, embedded, add, save, what, Read, SimHubLog, builtInMode, where);
        }

        /// <summary>The uninstall of ProfileInstall.Core.cs over SimHub's own profile type and log.</summary>
        internal static bool Uninstall(IList profiles, IList available, Guid id, Action save, string what)
        {
            return Uninstall(profiles, available, id, save, what, Read, SimHubLog);
        }

        private static readonly IInstallLog SimHubLog = new SimHubInstallLog();

        /// <summary>One entry of a SimHub profile list as the decision sees it, or null when it is not a
        /// profile at all. The one place the core's lists meet SimHub's `IProfile`.</summary>
        private static InstalledProfile Read(object entry)
        {
            var profile = entry as IProfile;
            if (profile == null) return null;
            return new InstalledProfile
            {
                ProfileId = profile.ProfileId,
                Name = profile.Name,
                Description = profile.Description,
            };
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
    }
}
