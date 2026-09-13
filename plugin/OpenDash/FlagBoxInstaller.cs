// FlagBoxInstaller.cs: installing the flag box profile through SimHub's own object model.
//
// ADR 0013 originally said the plugin could not install this, because matrix profiles live inside
// PluginsData/Common/ArduinoRGBMatrixSettings.json and SimHub rewrites that file itself. That was
// true about the FILE and wrong about the conclusion: SimHub exposes the whole chain publicly, so we
// never touch the file at all -- we hand SimHub a profile object and SimHub writes its own settings.
//
//     PluginManager.GetInstance()
//       .GetPlugin<SerialDashPlugin>()          SimHub.Plugins.dll, public
//       .Settings.RGBMatrixDriver               public
//       .Settings                               MatrixSettings, public
//       .AddProfile(profile)                    public, from ProfileSettingsBase
//     driver.SaveSettings()                     public -- SimHub serialises its own collection
//
// No reflection and no file merge, so the clobbering objection is gone. What remains is consent, and
// that is why this runs from a button rather than at startup: a profile paints hardware the user owns.
//
// Everything here needs SimHub types, so this file is NOT compiled into OpenDash.Tests; the decision
// it acts on is in FlagBoxInstallPlan.cs, which is. The reflection-free chain is asserted by the
// plugin building at all.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SimHub.Plugins;
using SimHub.Plugins.DataPlugins.RGBMatrixDriver;
using SimHub.Plugins.DataPlugins.RGBMatrixDriver.Settings;
using SimHub.Plugins.OutputPlugins.Dash;

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
            try
            {
                var manager = PluginManager.GetInstance();
                if (manager == null) return null;
                var serial = manager.GetPlugin<SerialDashPlugin>();
                var settings = serial?.Settings;
                return settings?.RGBMatrixDriver;
            }
            catch (Exception e)
            {
                Log.Warn("SimHub's matrix driver could not be reached: " + e.Message);
                return null;
            }
        }

        /// <summary>The profiles SimHub currently holds, or null when the driver is unreachable.</summary>
        public static List<InstalledProfile> Installed()
        {
            var settings = Driver()?.Settings;
            if (settings == null) return null;
            try
            {
                return settings.Profiles
                    .Where(p => p != null)
                    .Select(p => new InstalledProfile { ProfileId = p.ProfileId, Name = p.Name, Description = p.Description })
                    .ToList();
            }
            catch (Exception e)
            {
                Log.Warn("SimHub's matrix profiles could not be read: " + e.Message);
                return null;
            }
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

            try
            {
                // Ours is the one carrying our ProfileId. Anything else in the list is the user's and
                // is not touched, which is the whole reason this is safer than merging the file.
                var mine = settings.Profiles.Where(p => p != null && p.ProfileId == embedded.ProfileId).ToList();
                foreach (var old in mine) settings.Profiles.Remove(old);

                settings.AddProfile(embedded);
                driver.SaveSettings();

                Log.Info("Installed the flag box profile into SimHub (" + (mine.Count > 0 ? "replaced" : "added") + ").");
                return FlagBoxInstallPlan.Decide(embedded.ProfileId, embedded.Description, Installed());
            }
            catch (Exception e)
            {
                Log.Error("Installing the flag box profile into SimHub failed", e);
                return new FlagBoxPlan { State = FlagBoxInstallState.Failed };
            }
        }

        /// <summary>
        /// The embedded JSON as SimHub's own profile object. Deserialised with Newtonsoft's defaults,
        /// which is what SimHub's own import uses (JsonExtensions.FromJsonFile is a bare
        /// JsonSerializer), so the container converter on MatrixContainerBase resolves the same way.
        /// </summary>
        public static RGBMatrixProfile Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonConvert.DeserializeObject<RGBMatrixProfile>(json);
            }
            catch (Exception e)
            {
                Log.Error("The embedded flag box profile could not be read: " + e.Message);
                return null;
            }
        }
    }
}
