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
// Needs SimHub types, so it is NOT compiled into OpenDash.Tests. The id vocabulary a settings file holds
// is on LedBar, which is, and is pinned there.
using System;
using System.Collections.Generic;
using System.Linq;
using SimHub.Plugins;
using SimHub.Plugins.Devices;
using SimHub.Plugins.OutputPlugins.Dash;
using SimHub.Plugins.OutputPlugins.GraphicalDash.LedModules;
using LedsSettings = SimHub.Plugins.DataPlugins.RGBDriver.Settings.LedsSettings;

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
        /// carries no driver is left out rather than offered as a target that cannot hold anything.
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

        /// <summary>Every LED module of every device SimHub has, connected or not.</summary>
        private static IEnumerable<LedTarget> Devices()
        {
            var modules = Modules();
            var targets = new List<LedTarget>();
            foreach (var module in modules)
            {
                var settings = module.ledModuleSettings;
                var driver = settings == null ? null : settings.LedsDriver;
                if (driver == null || driver.Settings == null) continue;
                var root = module.RootInstance ?? module;
                var captured = module;
                targets.Add(new LedTarget
                {
                    Id = LedBar.DeviceId(root.InstanceId),
                    Name = NameOf(root, settings),
                    Connected = module.IsConnected,
                    Settings = driver.Settings,
                    Save = () => SaveDevice(captured),
                });
            }
            // Two LED modules under one device would take one id and overwrite each other, so only the
            // first of each is offered. Nothing in SimHub's registry builds such a device today; if one
            // appears, a bar pointed at it lands on its first module rather than at random.
            return targets.GroupBy(t => t.Id, StringComparer.Ordinal).Select(g => g.First());
        }

        private static IList<LedModuleDevice> Modules()
        {
            try
            {
                var manager = PluginManager.GetInstance();
                var devices = manager == null ? null : manager.GetPlugin<DevicesPlugin>();
                if (devices == null) return new List<LedModuleDevice>();
                return devices.GetDevices<LedModuleDevice>().Where(d => d != null).ToList();
            }
            catch (Exception e)
            {
                Log.Warn("SimHub's devices could not be read for their LEDs: " + e.Message);
                return new List<LedModuleDevice>();
            }
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
