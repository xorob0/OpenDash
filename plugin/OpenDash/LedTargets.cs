// LedTargets.cs: every set of RGB LEDs SimHub can install a profile into, and how to save each one.
//
// **This file exists because "install" meant one device and said "SimHub".** OpenDash installed strip
// profiles into `SerialDashPlugin.Settings.RGBLedsDriver`, which is `new RGBLedsDriver(
// "PluginsData\Common\ArduinoRGBLedsSettings.json")` -- the Arduino RGB LEDs device and nothing else.
// A wheel with LEDs in its rim is not that device. It is a `LedModuleDevice` under SimHub's Devices
// plugin, holding its own `LedModuleSettings` with its own `RGBLedsDriver`, its own `LedsSettings` and
// its own profile list, saved into its own file. So a bar added for a wheel was written correctly, saved
// correctly and verified correctly -- into a list the wheel does not read. Reported from a rig as a
// 3-9-3 bar named "wheel" simply not being in the wheel's profile list, which is exactly what it was.
//
// There is no single list of LED profiles in SimHub and there never was. Every LED device has its own,
// so an installer has to be told which one, and a bar has to remember its answer. {@link LedBar.Device}
// is that answer and this is what resolves it.
//
// The chain, all public, no reflection:
//
//     PluginManager.GetInstance()
//       .GetPlugin<DevicesPlugin>()                       SimHub.Plugins.dll, public
//       .GetDevices<LedModuleDevice>()                    public, and it flattens composites: a wheel
//                                                         that is an LCD *and* a LED module is one
//                                                         CompositeDeviceInstance whose GetInstances()
//                                                         yields both, so the LEDs are reached without
//                                                         knowing what else the device is
//       .ledModuleSettings.LedsDriver.Settings            public field, public property, public property
//
// Saving is two calls rather than one because SimHub stores these two ways. A module built with a file
// name has one (`RGBLedsDriver.SettingsFileName`) and `SaveSettings()` writes it; a module built without
// one is serialised into its device's own settings by `DevicesPlugin.SaveSettings()`. Which of the two a
// given device is cannot be read off the public surface, both are public, and both are what SimHub does
// itself, so both are called.
//
// A device that is walked and not offered is not dropped in silence any more: every root device is
// judged by LedDeviceSurvey, which is pure and tested, and what was seen of it is written to SimHub's log
// with the reason (#437). The one use of reflection in this file is there, and only to be logged.
//
// Needs SimHub types, so it is NOT compiled into OpenDash.Tests. The id vocabulary a settings file holds
// is on LedBar, which is, and is pinned there.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimHub.Plugins;
using SimHub.Plugins.Devices;
using SimHub.Plugins.OutputPlugins.Dash;
using SimHub.Plugins.OutputPlugins.GraphicalDash.LedModules;
using LedsSettings = SimHub.Plugins.DataPlugins.RGBDriver.Settings.LedsSettings;
using RGBLedsDriver = SimHub.Plugins.DataPlugins.RGBDriver.RGBLedsDriver;

namespace OpenDashPlugin
{
    /// <summary>One set of RGB LEDs a profile can be installed into: what to call it, and where it goes.</summary>
    public sealed class LedTarget
    {
        /// <summary>What a bar records. <see cref="LedBar.ArduinoDevice"/>, or a device instance's own id.</summary>
        public string Id { get; internal set; }

        /// <summary>What the panel calls it: the device's name as SimHub's own Devices list shows it.</summary>
        public string Name { get; internal set; }

        /// <summary>Whether SimHub has the hardware in front of it. A profile installs into a device that
        /// is unplugged -- the settings are SimHub's, not the hardware's -- so this only says so.</summary>
        public bool Connected { get; internal set; }

        /// <summary>The profile list a profile is added to and taken out of.</summary>
        internal LedsSettings Settings { get; set; }

        /// <summary>Everything SimHub does to put this device's profiles on disk.</summary>
        internal Action Save { get; set; }
    }

    public static class LedTargets
    {
        /// <summary>
        /// Every LED device SimHub can install into, the Arduino one first.
        /// </summary>
        /// <remarks>
        /// Never null and never throws: a broken chain is an empty list or a list without that device,
        /// which the panel reads as "there is nowhere to put this" and says so. A device whose LED module
        /// carries no driver is left out rather than offered as a target that cannot hold anything, and
        /// the log says so; <see cref="NotOffered"/> gives the panel its name.
        /// </remarks>
        public static List<LedTarget> All()
        {
            var targets = new List<LedTarget>();
            var arduino = Arduino();
            if (arduino != null) targets.Add(arduino);
            targets.AddRange(Devices());
            return targets;
        }

        /// <summary>The target a bar is pointed at, or null when SimHub no longer has it -- the device was
        /// unplugged and removed, or the settings file came from another rig.</summary>
        public static LedTarget Find(string id)
        {
            var wanted = LedBar.NormaliseDevice(id);
            return All().FirstOrDefault(t => string.Equals(t.Id, wanted, StringComparison.Ordinal));
        }

        /// <summary>
        /// What a new bar opens on: the one LED device there is, and the Arduino only when it is that one.
        /// </summary>
        /// <remarks>
        /// A rig with a wheel and nothing else should not have to find a drop-down to say so, and a rig
        /// with an Arduino and nothing else should not either. With more than one, the first *device* wins
        /// over the Arduino: somebody who has wired a strip to an Arduino has done that deliberately and
        /// will look, whereas a wheel is where LEDs are unless you know otherwise. The panel shows the
        /// choice either way.
        /// </remarks>
        public static LedTarget Preferred()
        {
            var targets = All();
            if (targets.Count == 0) return null;
            var device = targets.FirstOrDefault(t => !string.Equals(t.Id, LedBar.ArduinoDevice, StringComparison.Ordinal));
            return device ?? targets[0];
        }

        /// <summary>SimHub's Arduino RGB LEDs, or null when the serial dash plugin cannot be reached.</summary>
        private static LedTarget Arduino()
        {
            var driver = ProfileInstall.Driver(s => s.RGBLedsDriver, "RGB LED");
            var settings = driver == null ? null : driver.Settings;
            if (settings == null) return null;
            return new LedTarget
            {
                Id = LedBar.ArduinoDevice,
                Name = ArduinoName,
                Connected = true,
                Settings = settings,
                Save = driver.SaveSettings,
            };
        }

        /// <summary>What the Arduino device is called here. SimHub's own menu calls the plugin "Arduino"
        /// and its LED page "RGB Leds"; this is the two, so a list holding a wheel and this one reads as
        /// two devices rather than as a device and a category.</summary>
        public const string ArduinoName = "Arduino RGB LEDs";

        /// <summary>The names of the devices SimHub has, this list does not offer, and that show some sign of
        /// LEDs, for the picker to say. A pedal set or a screen is left to the log. The reasons are in
        /// SimHub's log, one line for every device; see <see cref="LedDeviceSurvey"/>.</summary>
        public static IList<string> NotOffered()
        {
            return LedDeviceSurvey.Declined(Survey().Select(entry => entry.Seen));
        }

        /// <summary>Every LED module of every device SimHub has, connected or not.</summary>
        /// <remarks>
        /// Every root device is judged, not only the LED modules, and what was seen of each is logged by
        /// <see cref="Survey"/>: a device passed over used to be passed over in silence, which is how a
        /// FanaBridge wheel went missing from the picker with nothing to say whether it was not an LED
        /// module or was one with no driver (#437).
        /// </remarks>
        private static IEnumerable<LedTarget> Devices()
        {
            var targets = Survey().Where(entry => entry.Target != null).Select(entry => entry.Target).ToList();
            // Two LED modules under one device would take one id and overwrite each other, so only the
            // first of each is offered. Nothing in SimHub's registry builds such a device today; if one
            // appears, a bar pointed at it lands on its first module rather than at random.
            return targets.GroupBy(t => t.Id, StringComparer.Ordinal).Select(g => g.First());
        }

        /// <summary>One root device: what was seen of it, and the target it gives when it is offered.</summary>
        private sealed class Surveyed
        {
            public LedDeviceSeen Seen;
            public LedTarget Target;
        }

        /// <summary>
        /// Every root device in SimHub's Devices plugin, judged, with the judgement logged when it changes.
        /// </summary>
        /// <remarks>
        /// The walk is the one `GetDevices&lt;LedModuleDevice&gt;()` makes -- in 9.12.6 it is
        /// `DevicesPluginSettings.Devices.SelectMany(GetInstances).OfType&lt;T&gt;()`, all public --
        /// written out so that the devices it drops are seen too. A device's first LED module with a usable
        /// driver is its target; failing that, its first LED module is what the verdict describes.
        ///
        /// The panel asks for the list on every redraw, so each device's line is written once and again
        /// only when it changes: a wheel plugged in, a driver appearing, built-in profiles switched off.
        /// </remarks>
        private static List<Surveyed> Survey()
        {
            var surveyed = new List<Surveyed>();
            foreach (var root in Roots())
            {
                try
                {
                    var entry = Judge(root);
                    surveyed.Add(entry);
                    Report(entry.Seen);
                }
                catch (Exception e)
                {
                    // Dropping it here would be the silence #437 is about, for the device likeliest to
                    // be the one asked after: a plugin's own. It is judged unreadable and logged like the
                    // rest, under whatever of its name and id can still be read.
                    var seen = Unreadable(root, e);
                    surveyed.Add(new Surveyed { Seen = seen });
                    Report(seen);
                }
            }
            return surveyed;
        }

        /// <summary>What can still be said of a device that threw while being read.</summary>
        private static LedDeviceSeen Unreadable(DeviceInstance root, Exception e)
        {
            var seen = new LedDeviceSeen
            {
                Unreadable = e.GetType().Name + ": " + e.Message,
                Kind = KindOf(root),
            };
            try { seen.Name = root.MainDisplayName; } catch (Exception) { }
            try { seen.Id = LedBar.DeviceId(root.InstanceId); } catch (Exception) { }
            return seen;
        }

        private static IList<DeviceInstance> Roots()
        {
            try
            {
                var manager = PluginManager.GetInstance();
                var devices = manager == null ? null : manager.GetPlugin<DevicesPlugin>();
                var settings = devices == null ? null : devices.DevicesPluginSettings;
                if (settings == null || settings.Devices == null) return new List<DeviceInstance>();
                return settings.Devices.Where(d => d != null).ToList();
            }
            catch (Exception e)
            {
                Log.Warn("SimHub's devices could not be read for their LEDs: " + e.Message);
                return new List<DeviceInstance>();
            }
        }

        private static Surveyed Judge(DeviceInstance root)
        {
            var instances = (root.GetInstances() ?? Enumerable.Empty<DeviceInstance>()).Where(i => i != null).ToList();
            var modules = instances.OfType<LedModuleDevice>().ToList();
            var usable = modules.FirstOrDefault(m => m.ledModuleSettings != null
                && m.ledModuleSettings.LedsDriver != null
                && m.ledModuleSettings.LedsDriver.Settings != null);
            var module = usable ?? modules.FirstOrDefault();

            var seen = new LedDeviceSeen
            {
                Name = root.MainDisplayName,
                Id = LedBar.DeviceId(root.InstanceId),
                Connected = root.IsConnected,
                Kind = KindOf(root),
                Instances = instances.Select(i => i.GetType().Name).ToList(),
                LedModule = module != null,
            };

            if (module == null)
            {
                seen.ForeignDrivers = ForeignDrivers(instances);
                return new Surveyed { Seen = seen };
            }

            var settings = module.ledModuleSettings;
            var driver = settings == null ? null : settings.LedsDriver;
            var leds = driver == null ? null : driver.Settings;
            seen.LedsDriver = driver != null;
            seen.LedsSettings = leds != null;
            if (settings != null)
            {
                seen.LedCount = settings.Ledcount;
                if (settings.ButtonsDriver != null) seen.OtherDrivers.Add("buttons");
                if (settings.EncodersDriver != null) seen.OtherDrivers.Add("encoders");
                if (settings.RawDriver != null) seen.OtherDrivers.Add("individual LEDs");
                if (settings.MatrixDriver != null) seen.OtherDrivers.Add("matrix");
            }
            if (leds != null)
            {
                seen.Profiles = leds.Profiles == null ? (int?)null : leds.Profiles.Count;
                seen.HasBuiltInProfiles = leds.HasBuiltInProfiles;
                seen.UseBuiltInProfiles = leds.UseBuiltInProfiles;
            }
            if (LedDeviceSurvey.Judge(seen) != LedDeviceVerdict.Offered) return new Surveyed { Seen = seen };

            var owner = module.RootInstance ?? module;
            var captured = module;
            return new Surveyed
            {
                Seen = seen,
                Target = new LedTarget
                {
                    Id = LedBar.DeviceId(owner.InstanceId),
                    Name = NameOf(owner, settings),
                    Connected = module.IsConnected,
                    Settings = leds,
                    Save = () => SaveDevice(captured),
                },
            };
        }

        /// <summary>The device's type and the assembly it is from: SimHub's own, or a plugin's.</summary>
        private static string KindOf(DeviceInstance root)
        {
            var type = root.GetType();
            return type.FullName + " (" + type.Assembly.GetName().Name + ")";
        }

        /// <summary>
        /// Every public property or field typed as SimHub's RGB LED driver, or as an LED module's settings,
        /// on the instances of a device that has no LED module. Fields too, because SimHub's own module
        /// holds its settings in one (`LedModuleDevice.ledModuleSettings`).
        /// </summary>
        /// <remarks>
        /// Read to be logged and for nothing else, and the one place this file uses reflection. A device a
        /// plugin registers as a type of its own, holding its LEDs somewhere OpenDash does not look, is
        /// cause one of #437, and this names where they are held; whether installing there would light
        /// the device is a question for a rig and not something to act on from a type name.
        /// </remarks>
        private static IList<string> ForeignDrivers(IEnumerable<DeviceInstance> instances)
        {
            var found = new List<string>();
            foreach (var instance in instances)
            {
                var type = instance.GetType();
                try
                {
                    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (HoldsLeds(property.PropertyType)) found.Add(type.Name + "." + property.Name);
                    }
                    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (HoldsLeds(field.FieldType)) found.Add(type.Name + "." + field.Name);
                    }
                }
                catch (Exception e)
                {
                    found.Add(type.Name + " (unreadable: " + e.Message + ")");
                }
            }
            return found;
        }

        private static bool HoldsLeds(Type type)
        {
            return typeof(RGBLedsDriver).IsAssignableFrom(type) || typeof(LedModuleSettings).IsAssignableFrom(type);
        }

        private static readonly object ReportLock = new object();
        private static readonly Dictionary<string, string> Reported = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Writes a device's line when it is new or has changed since it was last written.</summary>
        private static void Report(LedDeviceSeen seen)
        {
            var line = LedDeviceSurvey.LogLine(seen);
            var key = seen.Id ?? seen.Name ?? seen.Kind ?? string.Empty;
            lock (ReportLock)
            {
                string previous;
                if (Reported.TryGetValue(key, out previous) && string.Equals(previous, line, StringComparison.Ordinal)) return;
                Reported[key] = line;
            }
            Log.Info(line);
        }

        /// <summary>The device's name, with the module's own behind it when the two differ: a wheel the
        /// user renamed reads as they named it, and one they did not reads as its model.</summary>
        private static string NameOf(DeviceInstance root, LedModuleSettings settings)
        {
            var given = root == null ? null : root.MainDisplayName;
            var model = settings == null ? null : settings.DeviceName;
            if (string.IsNullOrWhiteSpace(given)) return string.IsNullOrWhiteSpace(model) ? "A device" : model;
            if (string.IsNullOrWhiteSpace(model) || string.Equals(given.Trim(), model.Trim(), StringComparison.OrdinalIgnoreCase)) return given;
            return given + " (" + model + ")";
        }

        /// <summary>Both of the ways SimHub stores a device's LED profiles. See the head of this file.</summary>
        private static void SaveDevice(LedModuleDevice module)
        {
            var driver = module.ledModuleSettings == null ? null : module.ledModuleSettings.LedsDriver;
            if (driver != null) driver.SaveSettings();
            var manager = PluginManager.GetInstance();
            var devices = manager == null ? null : manager.GetPlugin<DevicesPlugin>();
            if (devices != null) devices.SaveSettings();
        }
    }
}
