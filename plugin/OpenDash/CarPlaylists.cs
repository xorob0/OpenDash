// CarPlaylists.cs: SimHub's per-car playlists, read and written on the running SimHub's own objects (#199).
//
// What to write is PanelCarPlaylist's, which is pure and tested; this file only reads every display device into
// its shapes and applies the plan it returns. The members it reaches were read from the 9.12.6 decompile and are
// listed in docs/research/simhub-dash-format.md ("Per-car playlists"); PanelCarPlaylist.Reached names them again,
// CarPlaylistsTests holds that list to plugin/lib/SimHub.Plugins.dll, and Sync looks each of them up before it
// touches anything, so that a SimHub that has moved one is reported by name in the log and the panel rather than
// written into half-way.
//
// The chain:
//
//     PluginManager.GetInstance().GetPlugin<DevicesPlugin>()
//       .DevicesPluginSettings.Devices[*].GetInstances()          every device, composites flattened, as LedTargets
//       .OfType<IBitmapDisplayDevice>()                           the three kinds that carry a playlist
//       → DashPlaylistSettings                                    per kind, see PlaylistSettingsOf
//       .UpdateCurrentGame("IRacing"); .CurrentGamePlaylist       the public way to a game's playlist, the
//                                                                 dictionary behind it being private; the
//                                                                 game it pointed at before is put back
//       .CarDashes                                                public ObservableCollection<CarDash>
//     DevicesPlugin.SaveSettings()                                writes every device's settings.json now, so the
//                                                                 entry outlives a SimHub that is killed
//
// Never the files while SimHub runs: DevicesPlugin reads each device's settings.json once at startup and rewrites
// it from memory on every save and at a clean exit, so an edit to the file would be lost.
//
// A new entry is built by deserialising it, as SimHub builds every entry it reads from a device's file: its id and
// its car's id have private setters, and Newtonsoft fills them from [JsonProperty] exactly as it does at startup.
//
// Needs SimHub types, so it is NOT compiled into OpenDash.Tests.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using Newtonsoft.Json.Linq;
using SimHub.Plugins;
using SimHub.Plugins.Devices;
using SimHub.Plugins.OutputPlugins.GraphicalDash;
using SimHub.Plugins.OutputPlugins.GraphicalDash.BitmapDisplay;
using SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist;
using SimHub.Plugins.OutputPlugins.GraphicalDash.DeviceDashWeb;
using SimHub.Plugins.OutputPlugins.GraphicalDash.MonitorDevice;

namespace OpenDashPlugin
{
    public static class CarPlaylists
    {
        /// <summary>
        /// Brings every display's iRacing playlist to what the rig's themed screens want, saves the devices when
        /// anything changed, and returns the plan, or null when SimHub could not be reached.
        /// </summary>
        /// <remarks>
        /// On the interface thread only, which is where SimHub's own playlist editor changes these collections and
        /// where the settings pages of two of the three kinds are built. Never throws: a sync that fails leaves the
        /// playlists as they were and says why in the log.
        /// </remarks>
        public static CarPlaylistPlan Sync(IReadOnlyList<ScreenInstance> rig, IEnumerable<PackageEntry> catalogue)
        {
            var application = Application.Current;
            if (application != null && !application.Dispatcher.CheckAccess())
            {
                Log.Warn("The per-car playlists were not synchronised: asked off the interface thread.");
                return null;
            }
            var missing = PanelCarPlaylist.Missing(Has);
            if (missing.Count > 0)
            {
                Log.Warn(PanelCarPlaylist.MissingLog(missing));
                return null;
            }
            try
            {
                return Apply(rig, catalogue);
            }
            catch (Exception e)
            {
                Log.Warn("The per-car playlists were not synchronised: " + e);
                return null;
            }
        }

        /// <summary>Whether SimHub's assembly has a type with a public member of that name.</summary>
        private static bool Has(string type, string member)
        {
            try
            {
                var found = typeof(DevicesPlugin).Assembly.GetType(type, false);
                return found != null && found.GetMember(member, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private sealed class Live
        {
            public CarPlaylistDevice Read;
            public GameDashPlaylist Playlist;
        }

        private static CarPlaylistPlan Apply(IReadOnlyList<ScreenInstance> rig, IEnumerable<PackageEntry> catalogue)
        {
            var entries = (catalogue ?? Enumerable.Empty<PackageEntry>()).ToList();
            var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dashboards = GraphicalDashPlugin.GetSettings();
            if (dashboards != null && dashboards.Items != null)
            {
                foreach (var item in dashboards.Items.ToList())
                {
                    if (item != null && item.Code != null) listed.Add(item.Code);
                }
            }

            var live = Devices();
            var plan = PanelCarPlaylist.Plan(
                rig,
                screen => PackageCatalogue.EntryFor(entries, screen) != null,
                Contract.Themes,
                listed.Contains,
                live.Select(d => d.Read));

            foreach (var change in plan.Changes)
            {
                var device = live.FirstOrDefault(d => string.Equals(d.Read.Id, change.DeviceId, StringComparison.Ordinal));
                if (device == null) continue;
                var dashes = device.Playlist.CarDashes;
                var existing = dashes.FirstOrDefault(c => string.Equals(c.Id.ToString(), change.EntryId, StringComparison.OrdinalIgnoreCase));
                switch (change.Kind)
                {
                    case CarPlaylistChangeKind.Add:
                        dashes.Add(NewEntry(change));
                        Log.Info("Per-car playlist of " + device.Read.Name + ": " + change.Car + " shows " + change.Dashboard + ".");
                        break;
                    case CarPlaylistChangeKind.Retarget:
                        if (existing == null || existing.Items.Count == 0) break;
                        existing.Items[0].DashboardSelection.Dashboard = change.Dashboard;
                        Log.Info("Per-car playlist of " + device.Read.Name + ": " + change.Car + " now shows " + change.Dashboard + ".");
                        break;
                    case CarPlaylistChangeKind.Remove:
                        if (existing == null) break;
                        dashes.Remove(existing);
                        Log.Info("Per-car playlist of " + device.Read.Name + ": removed OpenDash's entry for " + change.Car + ".");
                        break;
                }
            }
            if (plan.Changes.Count > 0)
            {
                var manager = PluginManager.GetInstance();
                var devices = manager == null ? null : manager.GetPlugin<DevicesPlugin>();
                if (devices != null) devices.SaveSettings();
            }
            return plan;
        }

        /// <summary>
        /// Our entry for one car, in the shape a device's settings.json holds it, read as SimHub reads that file.
        /// </summary>
        /// <remarks>
        /// The car's display name is its id: SimHub fills it from the car's own settings when a driver adds a car
        /// in its editor, and the catalogue knows a car's path without knowing which of its names SimHub would use.
        /// </remarks>
        private static CarDash NewEntry(CarPlaylistChange change)
        {
            var json = new JObject
            {
                ["Cars"] = new JArray(new JObject { ["CarId"] = change.Car, ["CarName"] = change.Car }),
                ["Items"] = new JArray(new JObject { ["Id"] = change.ItemId, ["DashboardSelection"] = change.Dashboard }),
                ["Id"] = change.EntryId,
            };
            var entry = json.ToObject<CarDash>();
            if (entry == null || !string.Equals(entry.Id.ToString(), change.EntryId, StringComparison.OrdinalIgnoreCase)
                || entry.Cars.Count != 1 || entry.Cars[0].CarId != change.Car
                || entry.Items.Count != 1 || !string.Equals(entry.Items[0].Id.ToString(), change.ItemId, StringComparison.OrdinalIgnoreCase)
                || entry.Items[0].DashboardSelection.Dashboard != change.Dashboard)
            {
                throw new InvalidOperationException("SimHub read OpenDash's playlist entry for " + change.Car + " back differently from how it was written");
            }
            return entry;
        }

        /// <summary>Every display device SimHub has, with its iRacing playlist.</summary>
        private static List<Live> Devices()
        {
            var found = new List<Live>();
            var manager = PluginManager.GetInstance();
            var plugin = manager == null ? null : manager.GetPlugin<DevicesPlugin>();
            var settings = plugin == null ? null : plugin.DevicesPluginSettings;
            if (settings == null || settings.Devices == null) return found;
            foreach (var root in settings.Devices.Where(d => d != null).ToList())
            {
                foreach (var display in (root.GetInstances() ?? Enumerable.Empty<DeviceInstance>()).OfType<IBitmapDisplayDevice>())
                {
                    try
                    {
                        var playlists = PlaylistSettingsOf(display);
                        if (playlists == null) continue;
                        var game = IRacingPlaylist(playlists);
                        if (game == null) continue;
                        found.Add(new Live { Read = Read(root, display, game), Playlist = game });
                    }
                    catch (Exception e)
                    {
                        string name = null;
                        try { name = root.MainDisplayName; } catch (Exception) { }
                        Log.Warn("The playlist of \"" + (name ?? "a display") + "\" could not be read: " + e.Message);
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// A display's playlist settings, by the public route each kind has.
        /// </summary>
        /// <remarks>
        /// A USB screen's settings are public on the device (<c>BitmapDisplayDevice&lt;T&gt;.Settings</c>), reached
        /// by name only because the device is generic over a type the plugin does not know. A monitor and a web dash
        /// keep theirs private, and hand the same live object to the first tab of their settings page, which is the
        /// route #686 takes for a wheel's LEDs; the tab is built, read and dropped.
        /// </remarks>
        private static DashPlaylistSettings PlaylistSettingsOf(IBitmapDisplayDevice display)
        {
            if (IsBitmapDisplayDevice(display.GetType()))
            {
                var property = display.GetType().GetProperty("Settings", BindingFlags.Public | BindingFlags.Instance);
                var bitmap = property == null ? null : property.GetValue(display) as BitmapDisplaySettings;
                return bitmap == null ? null : bitmap.DashPlaylistSettings;
            }
            var device = display as DeviceInstance;
            if (!(display is DashMonitorDevice) && !(display is WebDashDevice) || device == null) return null;
            var tab = (device.GetSettingsControls() ?? Enumerable.Empty<DeviceSettingControl>()).FirstOrDefault();
            var control = tab == null ? null : tab.Control;
            var monitor = control as DashMonitorDeviceSettingsControl;
            if (monitor != null) return monitor.DashMonitorSettings == null ? null : monitor.DashMonitorSettings.DashPlaylistSettings;
            var web = control as WebDashDeviceSettingsControl;
            if (web != null) return web.DashMonitorSettings == null ? null : web.DashMonitorSettings.DashPlaylistSettings;
            return null;
        }

        private static bool IsBitmapDisplayDevice(Type type)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(BitmapDisplayDevice<>)) return true;
            }
            return false;
        }

        /// <summary>
        /// The display's iRacing playlist, through the one public way to a game's playlist, with the game it was
        /// pointing at put back.
        /// </summary>
        /// <remarks>
        /// UpdateCurrentGame also moves what the running playlist consults, so a SimHub running another game is
        /// pointed back at it at once. The display's playlist manager re-reads it only when the game or the car
        /// changes, and nothing here changes either.
        /// </remarks>
        private static GameDashPlaylist IRacingPlaylist(DashPlaylistSettings playlists)
        {
            var before = playlists.CurrentGameCode;
            if (string.Equals(before, PanelCarPlaylist.IRacing, StringComparison.OrdinalIgnoreCase)) return playlists.CurrentGamePlaylist;
            playlists.UpdateCurrentGame(PanelCarPlaylist.IRacing);
            var game = playlists.CurrentGamePlaylist;
            if (!string.IsNullOrEmpty(before)) playlists.UpdateCurrentGame(before);
            return game;
        }

        private static CarPlaylistDevice Read(DeviceInstance root, IBitmapDisplayDevice display, GameDashPlaylist game)
        {
            var instance = display as DeviceInstance;
            var read = new CarPlaylistDevice
            {
                Id = (instance ?? root).InstanceId.ToString(),
                Name = root.MainDisplayName,
                Dashboard = display.GetDashboard(),
                DefaultForOtherCars = game.UseDefaultMainDashForUnkownCars && game.DefaultMainDash != null && game.DefaultMainDash.Enabled && game.DefaultMainDash.Selection != null
                    ? game.DefaultMainDash.Selection.Dashboard
                    : null,
            };
            foreach (var dash in game.CarDashes.ToList())
            {
                if (dash == null) continue;
                read.Entries.Add(new CarPlaylistEntry
                {
                    Id = dash.Id.ToString(),
                    Cars = dash.Cars.Where(c => c != null).Select(c => c.CarId).ToList(),
                    Items = dash.Items.Where(i => i != null).Select(i => new CarPlaylistItem
                    {
                        Id = i.Id.ToString(),
                        Dashboard = i.DashboardSelection == null ? null : i.DashboardSelection.Dashboard,
                    }).ToList(),
                });
            }
            return read;
        }
    }
}
