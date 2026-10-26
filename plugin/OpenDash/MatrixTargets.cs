// MatrixTargets.cs: every RGB matrix SimHub can install the flag box profile into, and how to save each one (#363).
//
// **This file exists for the reason LedTargets.cs does, one device along.** The flag box profile went into
// `SerialDashPlugin.Settings.RGBMatrixDriver`, which is `new RGBMatrixDriver("PluginsData\Common\
// ArduinoRGBMatrixSettings.json")` -- the Arduino RGB matrix and nothing else. A matrix built into a wheel or a
// button plate is not that device: it is the `MatrixDriver` of a `LedModuleSettings` under SimHub's Devices
// plugin, with its own `MatrixSettings`, its own profile list and its own place on disk, and a profile installed
// into the Arduino's list is invisible there (ADR 0013, the 2026-09-18 amendment).
//
// The chain is the strips' with the last link changed, all public, no reflection:
//
//     PluginManager.GetInstance()
//       .GetPlugin<DevicesPlugin>().DevicesPluginSettings.Devices      every root device, as LedTargets walks them
//       .GetInstances().OfType<LedModuleDevice>()                      composites flattened
//       .ledModuleSettings.MatrixDriver.Settings                       public field, property, property
//
// and a device that is not an LED module is read through its settings page, exactly as LedTargets reads it
// (#686): the page's LED editor holds the live `LedModuleSettings`, matrix driver and all.
//
// Saving is the strips' two calls for the strips' reason: a driver built with a file name writes it in
// `SaveSettings()`, and one built without -- which is every device's, `AddMatrixDriver` builds it from a null
// token -- is serialised into its device's settings by `DevicesPlugin.SaveSettings()`.
//
// Only an 8x8 is offered: the driver also drives 4x4, 5x4, 5x7 and PSP-shaped panels, and the flag box profile is
// drawn for an 8x8 (PanelMatrixDevices.Takes). A device whose matrix is another kind is named in SimHub's log.
//
// Needs SimHub types, so it is NOT compiled into OpenDash.Tests. What it decides is PanelMatrixDevices, which is.
using System;
using System.Collections.Generic;
using System.Linq;
using SimHub.Plugins;
using SimHub.Plugins.DataPlugins.RGBMatrixDriver;
using SimHub.Plugins.DataPlugins.RGBMatrixDriver.Settings;
using SimHub.Plugins.Devices;
using SimHub.Plugins.OutputPlugins.GraphicalDash.LedModules;

namespace OpenDashPlugin
{
    /// <summary>One RGB matrix the flag box profile can be installed into: what to call it, and where it goes.</summary>
    public sealed class MatrixTarget
    {
        /// <summary>What a matrix records: <see cref="LedBar.ArduinoDevice"/>, or a device instance's own id, the
        /// same id a strip on that device records.</summary>
        public string Id { get; internal set; }

        /// <summary>What the panel calls it: the device's name as SimHub's own Devices list shows it.</summary>
        public string Name { get; internal set; }

        /// <summary>Whether SimHub has the hardware in front of it. Installing does not need it.</summary>
        public bool Connected { get; internal set; }

        /// <summary>The profile list the flag box profile is added to.</summary>
        internal MatrixSettings Settings { get; set; }

        /// <summary>Everything SimHub does to put this device's matrix profiles on disk.</summary>
        internal Action Save { get; set; }
    }

    public static class MatrixTargets
    {
        /// <summary>Every 8x8 SimHub can install into, the Arduino's first. Never null and never throws.</summary>
        public static List<MatrixTarget> All()
        {
            IList<string> declined;
            return All(out declined);
        }

        /// <summary>
        /// <see cref="All()"/>, and the names of the devices with a matrix that is not an 8x8, in the shape
        /// <see cref="PanelLedDevices{T}"/> reads, so the panel reads them once for each time it asks SimHub again.
        /// </summary>
        public static List<MatrixTarget> All(out IList<string> declined)
        {
            var targets = new List<MatrixTarget>();
            var passed = new List<string>();
            declined = passed;
            var arduino = Arduino();
            if (arduino != null) targets.Add(arduino);
            foreach (var root in LedTargets.Roots())
            {
                try
                {
                    var target = Judge(root, passed);
                    // Two matrices under one device would take one id; the first is the one a matrix lands on.
                    if (target != null && Find(target.Id, targets) == null) targets.Add(target);
                }
                catch (Exception e)
                {
                    string name = null;
                    try { name = root.MainDisplayName; } catch (Exception) { }
                    Report((name ?? "A device") + " could not be read for a matrix: " + e.GetType().Name + ": " + e.Message);
                }
            }
            return targets;
        }

        /// <summary>The target a matrix is pointed at among devices already read, or null when SimHub no longer
        /// has it. Ids are normalised as a strip's are, so a matrix with none recorded finds the Arduino's.</summary>
        public static MatrixTarget Find(string id, IEnumerable<MatrixTarget> targets)
        {
            if (targets == null) return null;
            var wanted = LedBar.NormaliseDevice(id);
            return targets.FirstOrDefault(t => t != null && string.Equals(t.Id, wanted, StringComparison.Ordinal));
        }

        /// <summary>The targets as the device row and the pure decisions read them.</summary>
        public static IList<LedDeviceEntry> Entries(IEnumerable<MatrixTarget> targets)
        {
            return (targets ?? Enumerable.Empty<MatrixTarget>()).Where(t => t != null).Select(t => new LedDeviceEntry(t.Id, t.Name, t.Connected)).ToList();
        }

        /// <summary>What a new matrix opens on; see <see cref="PanelMatrixDevices.Preferred"/>.</summary>
        public static string Preferred(IEnumerable<MatrixTarget> targets)
        {
            return PanelMatrixDevices.Preferred(Entries(targets));
        }

        /// <summary>SimHub's Arduino RGB matrix, or null when the serial dash plugin cannot be reached.</summary>
        private static MatrixTarget Arduino()
        {
            var driver = FlagBoxInstaller.Driver();
            var settings = driver == null ? null : driver.Settings;
            if (settings == null) return null;
            return new MatrixTarget
            {
                Id = LedBar.ArduinoDevice,
                Name = PanelMatrixDevices.ArduinoName,
                Connected = true,
                Settings = settings,
                Save = driver.SaveSettings,
            };
        }

        /// <summary>One root device's 8x8, or null when it has none; a matrix of another kind is named in
        /// <paramref name="declined"/> and the log.</summary>
        private static MatrixTarget Judge(DeviceInstance root, IList<string> declined)
        {
            var instances = (root.GetInstances() ?? Enumerable.Empty<DeviceInstance>()).Where(i => i != null).ToList();
            var modules = instances.OfType<LedModuleDevice>().ToList();
            var module = modules.FirstOrDefault(m => MatrixOf(m.ledModuleSettings) != null);
            // A device with no LED module may still carry one on its settings page (#686), as LedTargets reads it.
            var settings = module != null ? module.ledModuleSettings : (modules.Count == 0 ? LedTargets.EditorModule(root) : null);
            var driver = MatrixOf(settings);
            if (driver == null) return null;

            var owner = module == null ? root : module.RootInstance ?? module;
            var name = LedTargets.NameOf(owner, settings);
            // Compared by value, not by name: `Default` and `Matrix8x8` are one value, and which name ToString gives
            // a value two names share is not defined.
            var kind = driver.DeviceKind == DeviceKind.Matrix8x8 ? PanelMatrixDevices.FlagBoxKind : driver.DeviceKind.ToString();
            if (!PanelMatrixDevices.Takes(kind))
            {
                declined.Add(name);
                Report(name + " has a " + kind + " matrix; the flag box profile is drawn for an 8x8, so it is not offered.");
                return null;
            }
            return new MatrixTarget
            {
                Id = LedBar.DeviceId(owner.InstanceId),
                Name = name,
                Connected = module == null ? root.IsConnected : module.IsConnected,
                Settings = driver.Settings,
                Save = () => SaveDevice(driver),
            };
        }

        private static RGBMatrixDriver MatrixOf(LedModuleSettings settings)
        {
            var driver = settings == null ? null : settings.MatrixDriver;
            return driver == null || driver.Settings == null ? null : driver;
        }

        /// <summary>Both of the ways SimHub stores a device's matrix profiles. See the head of this file.</summary>
        private static void SaveDevice(RGBMatrixDriver driver)
        {
            if (driver != null) driver.SaveSettings();
            var manager = PluginManager.GetInstance();
            var devices = manager == null ? null : manager.GetPlugin<DevicesPlugin>();
            if (devices != null) devices.SaveSettings();
        }

        private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Writes a line once a session: the panel walks the devices on every redraw.</summary>
        private static void Report(string line)
        {
            lock (Reported)
            {
                if (!Reported.Add(line)) return;
            }
            Log.Info(line);
        }
    }
}
