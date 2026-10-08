// PanelCarPlaylist.cs: which per-car playlist entries the plugin owes SimHub's screen devices for the themed screens
// on the rig, which entries are its own, and what the panel says about them (#199).
//
// SimHub switches a display's dashboard by car through a playlist that belongs to the display device: a list of
// entries, each naming cars by the id the game reader gives (the CarPath on iRacing) and the dashboards to show in
// them (docs/research/simhub-dash-format.md, "Per-car playlists"). A themed screen is drawn for some cars, so the
// plugin writes one entry per car into each display that shows a face of the screen's size, and takes it out again
// when the screen goes. Everything that decides what to write is here and pure, so that the rule "our entries only,
// and never over the driver's own" is under test; CarPlaylists.cs only reads SimHub's objects into the shapes below
// and applies a plan.
//
// An entry is ours when its id carries OurPrefix, which every id the plugin writes does, and which no id SimHub
// draws at random is likely to: one chance in four billion per entry. The id is otherwise derived from the theme and
// the car, so an entry is recognised as ours whatever later build reads it, a theme since dropped from the catalogue
// included, which is the entry that matters most to remove, since a playlist naming a dashboard SimHub does not list
// blanks the display.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OpenDashPlugin
{
    /// <summary>One per-car entry of a display's iRacing playlist, as read.</summary>
    public sealed class CarPlaylistEntry
    {
        public string Id { get; set; }

        /// <summary>The cars it is for, by the id SimHub matches on.</summary>
        public IList<string> Cars { get; set; } = new List<string>();

        public IList<CarPlaylistItem> Items { get; set; } = new List<CarPlaylistItem>();
    }

    /// <summary>One dashboard of an entry.</summary>
    public sealed class CarPlaylistItem
    {
        public string Id { get; set; }

        /// <summary>The dashboard's code: its folder under DashTemplates.</summary>
        public string Dashboard { get; set; }
    }

    /// <summary>One display SimHub can switch by car, as read.</summary>
    public sealed class CarPlaylistDevice
    {
        /// <summary>The device instance's id, which is what a plan names it by.</summary>
        public string Id { get; set; }

        /// <summary>The device's name in SimHub's Devices list.</summary>
        public string Name { get; set; }

        /// <summary>The dashboard it shows now.</summary>
        public string Dashboard { get; set; }

        /// <summary>The dashboard its iRacing playlist loads in a car with no entry, or null when it loads none, in
        /// which case such a car keeps whatever was showing.</summary>
        public string DefaultForOtherCars { get; set; }

        public IList<CarPlaylistEntry> Entries { get; set; } = new List<CarPlaylistEntry>();
    }

    public enum CarPlaylistChangeKind
    {
        Add,
        Remove,

        /// <summary>Our entry for a car stays and shows another dashboard: the screen moved folder.</summary>
        Retarget,
    }

    /// <summary>One change to one display's playlist.</summary>
    public sealed class CarPlaylistChange
    {
        public CarPlaylistChangeKind Kind { get; set; }

        public string DeviceId { get; set; }

        /// <summary>The entry added, removed or retargeted.</summary>
        public string EntryId { get; set; }

        /// <summary>The added entry's one item; null for any other change.</summary>
        public string ItemId { get; set; }

        public string Car { get; set; }

        /// <summary>The dashboard an added or retargeted entry shows.</summary>
        public string Dashboard { get; set; }
    }

    /// <summary>What a themed screen's cars come to on the rig's displays, for the panel to say.</summary>
    public sealed class CarPlaylistScreenReport
    {
        /// <summary>The displays that switch to the screen in its cars.</summary>
        public IList<string> Devices { get; } = new List<string>();

        /// <summary>The displays of <see cref="Devices"/> that load no dashboard in a car without an entry.</summary>
        public IList<string> KeepLastFace { get; } = new List<string>();

        /// <summary>Display and car pairs left to an entry the driver made, which the plugin does not touch.</summary>
        public IList<KeyValuePair<string, string>> LeftToTheDriver { get; } = new List<KeyValuePair<string, string>>();

        /// <summary>Whether SimHub does not list the screen's dashboard yet, so nothing can switch to it before a restart.</summary>
        public bool WaitsForRestart { get; set; }
    }

    /// <summary>Every change a sync makes, and what each themed screen comes to.</summary>
    public sealed class CarPlaylistPlan
    {
        public IList<CarPlaylistChange> Changes { get; } = new List<CarPlaylistChange>();

        /// <summary>By screen namespace, one per themed screen on the rig.</summary>
        public IDictionary<string, CarPlaylistScreenReport> Screens { get; } = new Dictionary<string, CarPlaylistScreenReport>(StringComparer.Ordinal);
    }

    public static class PanelCarPlaylist
    {
        /// <summary>The game code SimHub keys a device's playlists by for iRacing, which is the one game the theme
        /// catalogue names cars for.</summary>
        public const string IRacing = "IRacing";

        /// <summary>The first eight hex digits of every id the plugin writes into a playlist.</summary>
        public const string OurPrefix = "0de7da5b";

        /// <summary>The id of our entry for a car in a theme.</summary>
        public static string EntryId(string theme, string car)
        {
            return OurId("entry", theme, car);
        }

        /// <summary>The id of the one item of our entry for a car in a theme.</summary>
        public static string ItemId(string theme, string car)
        {
            return OurId("item", theme, car);
        }

        /// <summary>
        /// A name-based id in the shape of RFC 4122's version 5, its first group replaced by <see cref="OurPrefix"/>.
        /// </summary>
        private static string OurId(string role, string theme, string car)
        {
            byte[] hash;
            using (var sha1 = SHA1.Create())
            {
                hash = sha1.ComputeHash(Encoding.UTF8.GetBytes("OpenDash car playlist\n" + role + "\n" + (theme ?? string.Empty) + "\n" + (car ?? string.Empty)));
            }
            hash[6] = (byte)((hash[6] & 0x0f) | 0x50);
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
            var hex = new StringBuilder(32);
            for (var i = 4; i < 16; i++) hex.Append(hash[i].ToString("x2"));
            var rest = hex.ToString();
            return OurPrefix + "-" + rest.Substring(0, 4) + "-" + rest.Substring(4, 4) + "-" + rest.Substring(8, 4) + "-" + rest.Substring(12, 12);
        }

        public static bool IsOurs(string id)
        {
            return id != null && id.StartsWith(OurPrefix + "-", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether an entry is ours and still as we wrote it: one car, one dashboard, both ids ours.
        /// </summary>
        /// <remarks>
        /// An entry of ours that the driver has opened in SimHub and changed, a car added to it or a dashboard put in
        /// it, is theirs from then on. SimHub keeps the entry's id through an edit, so the id alone would let a later
        /// sync take back what they arranged, which #199 names the worst thing this could do.
        /// </remarks>
        public static bool IsOursAsWritten(CarPlaylistEntry entry)
        {
            return entry != null
                && IsOurs(entry.Id)
                && entry.Cars != null && entry.Cars.Count == 1
                && entry.Items != null && entry.Items.Count == 1
                && entry.Items[0] != null && IsOurs(entry.Items[0].Id);
        }

        /// <summary>
        /// The changes that bring every display's playlist to what the rig's themed screens want, and what each of
        /// those screens comes to.
        /// </summary>
        /// <remarks>
        /// <para>A display is bound for a size when it shows, or is set to fall back to, or already switches by our
        /// entries to, the dashboard of a screen of that size on the rig. A themed face is drawn for one display at
        /// one size, and the only thing SimHub says of a display that is also a fact about OpenDash is which of its
        /// dashboards that display shows. The last of the three keeps a display bound once it has switched: in a car
        /// with no entry it keeps the themed face, or falls back to whatever the driver chose, and neither should
        /// unbind it.</para>
        ///
        /// <para>A themed screen is wanted while its package is carried (<paramref name="carried"/>), its theme is
        /// catalogued and SimHub lists its dashboard (<paramref name="listed"/>). An entry naming a dashboard SimHub
        /// does not list blanks the display when the car loads, so a screen added since SimHub started, whose folder
        /// SimHub reads only at the next start, is bound at that start rather than now.</para>
        ///
        /// <para>Our entry for a car is removed whenever the driver has one of their own for it, since SimHub takes
        /// the first entry naming a car and ours would hide theirs.</para>
        /// </remarks>
        /// <param name="rig">The screens on the rig.</param>
        /// <param name="carried">Whether a screen's package is in this build.</param>
        /// <param name="themes">The theme catalogue.</param>
        /// <param name="listed">Whether SimHub lists a dashboard by its code.</param>
        /// <param name="devices">Every display, as read.</param>
        public static CarPlaylistPlan Plan(
            IEnumerable<ScreenInstance> rig,
            Func<ScreenInstance, bool> carried,
            IEnumerable<Contract.ThemeEntry> themes,
            Func<string, bool> listed,
            IEnumerable<CarPlaylistDevice> devices)
        {
            var plan = new CarPlaylistPlan();
            var screens = (rig ?? Enumerable.Empty<ScreenInstance>()).Where(s => s != null && !string.IsNullOrEmpty(s.Folder)).ToList();
            var catalogue = (themes ?? Enumerable.Empty<Contract.ThemeEntry>()).ToList();

            var themed = new List<KeyValuePair<ScreenInstance, Contract.ThemeEntry>>();
            foreach (var screen in screens)
            {
                if (string.IsNullOrEmpty(screen.Theme) || screen.Namespace == null) continue;
                var report = new CarPlaylistScreenReport();
                plan.Screens[screen.Namespace] = report;
                var theme = catalogue.FirstOrDefault(t => !t.IsDefault && string.Equals(t.Id, screen.Theme, StringComparison.Ordinal));
                if (theme == null || carried == null || !carried(screen)) continue;
                if (listed == null || !listed(screen.Folder))
                {
                    report.WaitsForRestart = true;
                    continue;
                }
                themed.Add(new KeyValuePair<ScreenInstance, Contract.ThemeEntry>(screen, theme));
            }

            foreach (var device in (devices ?? Enumerable.Empty<CarPlaylistDevice>()).Where(d => d != null))
            {
                var entries = (device.Entries ?? new List<CarPlaylistEntry>()).Where(e => e != null).ToList();
                var ours = entries.Where(IsOursAsWritten).ToList();
                var theirs = entries.Where(e => !IsOursAsWritten(e)).ToList();

                // The cars each themed screen of the display's size wants, the first screen on the rig winning a car
                // two of them claim.
                var wanted = new List<Wanted>();
                var size = SizeOf(device, ours, screens);
                if (size != null)
                {
                    foreach (var pair in themed)
                    {
                        if (pair.Key.Width != size.Width || pair.Key.Height != size.Height) continue;
                        foreach (var car in pair.Value.IracingCarPaths ?? new string[0])
                        {
                            if (string.IsNullOrEmpty(car) || wanted.Any(w => string.Equals(w.Car, car, StringComparison.Ordinal))) continue;
                            wanted.Add(new Wanted { Screen = pair.Key, Theme = pair.Value, Car = car });
                        }
                    }
                }

                var kept = new HashSet<CarPlaylistEntry>();
                foreach (var want in wanted)
                {
                    var report = plan.Screens[want.Screen.Namespace];
                    var entryId = EntryId(want.Theme.Id, want.Car);
                    if (theirs.Any(e => e.Cars != null && e.Cars.Contains(want.Car)))
                    {
                        report.LeftToTheDriver.Add(new KeyValuePair<string, string>(device.Name, want.Car));
                        continue;
                    }
                    var existing = ours.FirstOrDefault(e => !kept.Contains(e) && string.Equals(e.Id, entryId, StringComparison.OrdinalIgnoreCase) && e.Cars[0] == want.Car);
                    if (existing == null)
                    {
                        plan.Changes.Add(new CarPlaylistChange
                        {
                            Kind = CarPlaylistChangeKind.Add,
                            DeviceId = device.Id,
                            EntryId = entryId,
                            ItemId = ItemId(want.Theme.Id, want.Car),
                            Car = want.Car,
                            Dashboard = want.Screen.Folder,
                        });
                    }
                    else
                    {
                        kept.Add(existing);
                        if (!string.Equals(existing.Items[0].Dashboard, want.Screen.Folder, StringComparison.Ordinal))
                        {
                            plan.Changes.Add(new CarPlaylistChange
                            {
                                Kind = CarPlaylistChangeKind.Retarget,
                                DeviceId = device.Id,
                                EntryId = existing.Id,
                                Car = want.Car,
                                Dashboard = want.Screen.Folder,
                            });
                        }
                    }
                    if (!report.Devices.Contains(device.Name))
                    {
                        report.Devices.Add(device.Name);
                        if (string.IsNullOrEmpty(device.DefaultForOtherCars)) report.KeepLastFace.Add(device.Name);
                    }
                }
                foreach (var entry in ours.Where(e => !kept.Contains(e)))
                {
                    plan.Changes.Add(new CarPlaylistChange { Kind = CarPlaylistChangeKind.Remove, DeviceId = device.Id, EntryId = entry.Id, Car = entry.Cars[0] });
                }
            }
            return plan;
        }

        private sealed class Wanted
        {
            public ScreenInstance Screen;
            public Contract.ThemeEntry Theme;
            public string Car;
        }

        /// <summary>The size of the rig's screen a display shows, falls back to, or switches to by our entries; null
        /// when it is none of the rig's.</summary>
        private static ScreenInstance SizeOf(CarPlaylistDevice device, IEnumerable<CarPlaylistEntry> ours, IList<ScreenInstance> screens)
        {
            var shown = new List<string> { device.Dashboard, device.DefaultForOtherCars };
            shown.AddRange(ours.Select(e => e.Items[0].Dashboard));
            foreach (var dashboard in shown)
            {
                if (string.IsNullOrEmpty(dashboard)) continue;
                var screen = screens.FirstOrDefault(s => string.Equals(s.Folder, dashboard, StringComparison.OrdinalIgnoreCase));
                if (screen != null && screen.Width > 0 && screen.Height > 0) return screen;
            }
            return null;
        }

        // --- What SimHub has to have -------------------------------------------------------------------

        /// <summary>
        /// Every type and public member of SimHub's that CarPlaylists reaches, as read from the 9.12.6 decompile and
        /// listed in docs/research/simhub-dash-format.md.
        /// </summary>
        /// <remarks>
        /// Looked up before anything is read or written, so that a SimHub update that moves one is reported by name
        /// rather than as a null reference, and held to plugin/lib/SimHub.Plugins.dll by CarPlaylistsTests, so that
        /// the list cannot fall behind the code that uses it without a test saying so.
        /// </remarks>
        public static readonly string[][] Reached =
        {
            new[] { "SimHub.Plugins.Devices.DevicesPlugin", "DevicesPluginSettings" },
            new[] { "SimHub.Plugins.Devices.DevicesPlugin", "SaveSettings" },
            new[] { "SimHub.Plugins.Devices.DevicesPluginSettings", "Devices" },
            new[] { "SimHub.Plugins.Devices.DeviceInstance", "GetInstances" },
            new[] { "SimHub.Plugins.Devices.DeviceInstance", "GetSettingsControls" },
            new[] { "SimHub.Plugins.Devices.DeviceInstance", "MainDisplayName" },
            new[] { "SimHub.Plugins.Devices.DeviceInstance", "InstanceId" },
            new[] { "SimHub.Plugins.Devices.DeviceSettingControl", "Control" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.BitmapDisplay.IBitmapDisplayDevice", "GetDashboard" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.BitmapDisplay.BitmapDisplayDevice`1", "Settings" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.BitmapDisplaySettings", "DashPlaylistSettings" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.MonitorDevice.DashMonitorDeviceSettingsControl", "DashMonitorSettings" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.MonitorDevice.DashMonitorSettings", "DashPlaylistSettings" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DeviceDashWeb.WebDashDeviceSettingsControl", "DashMonitorSettings" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DeviceDashWeb.WebDashDeviceSettings", "DashPlaylistSettings" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.DashPlaylistSettings", "UpdateCurrentGame" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.DashPlaylistSettings", "CurrentGameCode" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.DashPlaylistSettings", "CurrentGamePlaylist" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.GameDashPlaylist", "CarDashes" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.GameDashPlaylist", "DefaultMainDash" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.GameDashPlaylist", "UseDefaultMainDashForUnkownCars" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.DefaultDashOption", "Enabled" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.DefaultDashOption", "Selection" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.CarDash", "Cars" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.DashPlaylist", "Items" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.DashPlaylist", "Id" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.CarSelection", "CarId" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.CarSelection", "CarName" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.PlaylistItem", "Id" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.PlaylistItem", "DashboardSelection" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashboardSelection", "Dashboard" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.GraphicalDashPlugin", "GetSettings" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.GraphicalDashPluginListModel", "Items" },
            new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.GraphicalDashItem", "Code" },
        };

        /// <summary>The members of <see cref="Reached"/> that <paramref name="has"/> does not find, as "Type.Member".</summary>
        public static IList<string> Missing(Func<string, string, bool> has)
        {
            var missing = new List<string>();
            foreach (var pair in Reached)
            {
                if (has == null || !has(pair[0], pair[1])) missing.Add(pair[0] + "." + pair[1]);
            }
            return missing;
        }

        /// <summary>The line SimHub's log carries when this SimHub has moved what the playlists are reached through.</summary>
        public static string MissingLog(IList<string> missing)
        {
            return "The per-car playlists were not synchronised: this SimHub has no "
                + string.Join(", ", missing ?? new string[0])
                + ", which OpenDash reaches as SimHub 9.12.6 has them. Nothing was written.";
        }

        // --- What the panel says ------------------------------------------------------------------------

        /// <summary>A themed screen's line when the playlists could not be reached at all.</summary>
        public const string Failed = "Your displays could not be switched to this screen by car. See SimHub's log.";

        /// <summary>The row on a themed screen's page.</summary>
        public const string RowTitle = "Car switching";

        /// <summary>
        /// The lines under a themed screen's details: which displays switch to it, which cars are left to the
        /// driver's own playlist, and what a display does in another car.
        /// </summary>
        public static IList<string> Lines(CarPlaylistScreenReport report, ScreenInstance screen)
        {
            var lines = new List<string>();
            if (report == null || screen == null) return lines;
            if (report.WaitsForRestart)
            {
                lines.Add(WaitsForRestart);
                return lines;
            }
            if (report.Devices.Count == 0 && report.LeftToTheDriver.Count == 0)
            {
                lines.Add(NoDisplay(screen.SizeLabel));
                return lines;
            }
            if (report.Devices.Count > 0) lines.Add(Switches(report.Devices));
            foreach (var pair in report.LeftToTheDriver) lines.Add(LeftToTheDriver(pair.Key, pair.Value));
            foreach (var device in report.KeepLastFace) lines.Add(KeepsLastFace(device));
            return lines;
        }

        public static CarPlaylistScreenReport ReportFor(CarPlaylistPlan plan, string screenNamespace)
        {
            CarPlaylistScreenReport report;
            return plan != null && screenNamespace != null && plan.Screens.TryGetValue(screenNamespace, out report) ? report : null;
        }

        public const string WaitsForRestart = "Restart SimHub to switch your displays to this screen by car.";

        /// <summary>No display shows a face of the screen's size, so none is bound: the install happened, and the
        /// step left is the driver's.</summary>
        public static string NoDisplay(string size)
        {
            return "No display switches to this screen by car. Show a " + size + " face on a screen under Devices in SimHub, then restart SimHub.";
        }

        public static string Switches(IList<string> devices)
        {
            return Join(devices) + " switches to this screen in the cars it is drawn for.";
        }

        /// <summary>A car the driver has their own playlist entry for on a display, which the plugin leaves alone.</summary>
        public static string LeftToTheDriver(string device, string car)
        {
            return "Your own playlist for " + car + " on " + device + " is kept, so that car does not switch here.";
        }

        /// <summary>A bound display that loads no dashboard in a car without an entry: SimHub then leaves the
        /// themed face on it, and the setting is the driver's, so the line says where it is.</summary>
        public static string KeepsLastFace(string device)
        {
            return "In any other car, " + device + " keeps the last face it showed. Choose the dashboard for those cars in its playlist in SimHub.";
        }

        private static string Join(IList<string> names)
        {
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
        }
    }
}
