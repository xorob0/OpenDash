// PanelCarPlaylistTests.cs: the per-car playlist entries the plugin writes for a themed screen (#199), and the ones
// it must leave alone. Our entries only, by an id no random one carries; never over an entry the driver made for the
// same car, which SimHub would let ours hide; removed with the screen, with its package, and before SimHub lists the
// dashboard, since an entry naming a dashboard SimHub does not list blanks the display; and a display is bound only
// while it shows a face of the screen's size. Also the members CarPlaylists reaches, held to the SimHub assembly the
// plugin is built against, so that the list in docs/research/simhub-dash-format.md cannot drift from the code.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelCarPlaylistTests
    {
        private const string Themed = "OpenDash Porsche 1280x480";
        private const string Default = "OpenDash 1280x480";

        private static ScreenInstance Screen(string folder, string theme, string ns, int width = 1280, int height = 480)
        {
            return new ScreenInstance { Kind = Contract.KindFace, Width = width, Height = height, Folder = folder, Namespace = ns, Name = folder, Theme = theme };
        }

        private static readonly ScreenInstance Rim = Screen(Default, null, "Face1280x480");
        private static readonly ScreenInstance Porsche = Screen(Themed, "porsche", "Porsche");

        private static CarPlaylistDevice Device(string dashboard, params CarPlaylistEntry[] entries)
        {
            return new CarPlaylistDevice { Id = "sd43", Name = "SD43", Dashboard = dashboard, DefaultForOtherCars = Default, Entries = entries.ToList() };
        }

        private static CarPlaylistEntry Ours(string car, string dashboard, string theme = "porsche")
        {
            return new CarPlaylistEntry
            {
                Id = PanelCarPlaylist.EntryId(theme, car),
                Cars = new List<string> { car },
                Items = new List<CarPlaylistItem> { new CarPlaylistItem { Id = PanelCarPlaylist.ItemId(theme, car), Dashboard = dashboard } },
            };
        }

        private static CarPlaylistEntry Theirs(string car, string dashboard)
        {
            return new CarPlaylistEntry
            {
                Id = Guid.NewGuid().ToString(),
                Cars = new List<string> { car },
                Items = new List<CarPlaylistItem> { new CarPlaylistItem { Id = Guid.NewGuid().ToString(), Dashboard = dashboard } },
            };
        }

        private static CarPlaylistPlan Plan(IEnumerable<ScreenInstance> rig, params CarPlaylistDevice[] devices)
        {
            return PanelCarPlaylist.Plan(rig, s => true, Contract.Themes, d => true, devices);
        }

        private static readonly string[] PorscheCars = Contract.Themes.Single(t => t.Id == "porsche").IracingCarPaths;

        [Fact]
        public void Our_ids_are_the_same_from_one_build_to_the_next_and_carry_our_prefix()
        {
            var id = PanelCarPlaylist.EntryId("porsche", "porsche992rgt3");
            Assert.Equal(id, PanelCarPlaylist.EntryId("porsche", "porsche992rgt3"));
            Assert.NotEqual(id, PanelCarPlaylist.EntryId("porsche", "porsche992cup"));
            Assert.NotEqual(id, PanelCarPlaylist.ItemId("porsche", "porsche992rgt3"));
            Assert.Equal(id, Guid.Parse(id).ToString());
            Assert.StartsWith(PanelCarPlaylist.OurPrefix + "-", id);
            Assert.Equal('5', id[14]);
            Assert.True(PanelCarPlaylist.IsOurs(id.ToUpperInvariant()));
            Assert.False(PanelCarPlaylist.IsOurs(Guid.NewGuid().ToString().Replace(Guid.NewGuid().ToString().Substring(0, 8), "12345678")));
        }

        /// <summary>The case the feature is for: a display shows the 1280 x 480 face and the Porsche is added at that size,
        /// so the display gets one entry per car the theme names, each showing the themed face.</summary>
        [Fact]
        public void A_display_showing_a_face_of_the_size_gets_one_entry_per_car_of_the_theme()
        {
            var plan = Plan(new[] { Rim, Porsche }, Device(Default));
            Assert.Equal(PorscheCars, plan.Changes.Select(c => c.Car));
            Assert.All(plan.Changes, c =>
            {
                Assert.Equal(CarPlaylistChangeKind.Add, c.Kind);
                Assert.Equal(Themed, c.Dashboard);
                Assert.Equal(PanelCarPlaylist.EntryId("porsche", c.Car), c.EntryId);
                Assert.Equal(PanelCarPlaylist.ItemId("porsche", c.Car), c.ItemId);
            });
            Assert.Equal(new[] { "SD43" }, plan.Screens["Porsche"].Devices);
            Assert.False(plan.Screens.ContainsKey("Face1280x480"));
        }

        [Fact]
        public void A_display_showing_anything_else_is_not_touched()
        {
            var other = Device("AIM GS-DASH");
            other.DefaultForOtherCars = null;
            var smaller = Device("OpenDash 850x480");
            smaller.Id = "rim";
            smaller.DefaultForOtherCars = "OpenDash 850x480";
            var plan = Plan(new[] { Rim, Porsche, Screen("OpenDash 850x480", null, "Face850x480", 850, 480) }, other, smaller);
            Assert.Empty(plan.Changes);
            Assert.Empty(plan.Screens["Porsche"].Devices);
            Assert.Equal(new[] { PanelCarPlaylist.NoDisplay("1280 × 480") }, PanelCarPlaylist.Lines(plan.Screens["Porsche"], Porsche));
        }

        /// <summary>A display that switched to the themed face in a Porsche and kept it is still bound, whether it shows
        /// it now or only falls back to it.</summary>
        [Fact]
        public void A_display_already_switching_by_our_entries_stays_bound()
        {
            var shown = Device(Themed, PorscheCars.Select(car => Ours(car, Themed)).ToArray());
            shown.DefaultForOtherCars = null;
            Assert.Empty(Plan(new[] { Porsche }, shown).Changes);
            var elsewhere = Device("AIM GS-DASH", PorscheCars.Select(car => Ours(car, Themed)).ToArray());
            elsewhere.DefaultForOtherCars = "AIM GS-DASH";
            Assert.Empty(Plan(new[] { Porsche }, elsewhere).Changes);
        }

        /// <summary>#199's worst case: the driver has their own entry for the car. It is never overridden, ours is taken
        /// out where it would hide it (SimHub takes the first entry naming a car), and the panel says so.</summary>
        [Fact]
        public void An_entry_the_driver_made_for_the_car_is_left_alone_and_ours_gives_way_to_it()
        {
            var car = PorscheCars[0];
            var mine = Theirs(car, "AIM GS-DASH");
            var plan = Plan(new[] { Rim, Porsche }, Device(Default, Ours(car, Themed), mine));
            var forCar = plan.Changes.Where(c => c.Car == car).ToList();
            Assert.Single(forCar);
            Assert.Equal(CarPlaylistChangeKind.Remove, forCar[0].Kind);
            Assert.Equal(PanelCarPlaylist.EntryId("porsche", car), forCar[0].EntryId);
            Assert.DoesNotContain(plan.Changes, c => c.EntryId == mine.Id);
            Assert.Contains(new KeyValuePair<string, string>("SD43", car), plan.Screens["Porsche"].LeftToTheDriver);
            Assert.Contains(PanelCarPlaylist.LeftToTheDriver("SD43", car), PanelCarPlaylist.Lines(plan.Screens["Porsche"], Porsche));
        }

        /// <summary>An entry of ours the driver has since edited in SimHub, a car added to it or a dashboard put in, is
        /// theirs: it keeps its id through the edit, and taking it back would undo what they arranged.</summary>
        [Fact]
        public void An_entry_of_ours_the_driver_edited_is_theirs()
        {
            var car = PorscheCars[0];
            var edited = Ours(car, Themed);
            edited.Cars.Add("ferrari296gt3");
            Assert.False(PanelCarPlaylist.IsOursAsWritten(edited));
            var plan = Plan(new ScreenInstance[0], Device(Default, edited));
            Assert.Empty(plan.Changes);

            var redrawn = Ours(car, Themed);
            redrawn.Items[0].Id = Guid.NewGuid().ToString();
            Assert.False(PanelCarPlaylist.IsOursAsWritten(redrawn));
            Assert.True(PanelCarPlaylist.IsOursAsWritten(Ours(car, Themed)));
        }

        /// <summary>Our entries go with the screen, and with a theme a later catalogue no longer has, and with a package
        /// the build no longer carries: an entry naming a dashboard nobody answers for is what blanks a display.</summary>
        [Fact]
        public void Our_entries_go_with_the_screen_its_theme_and_its_package()
        {
            var device = Device(Default, Ours(PorscheCars[0], Themed), Ours(PorscheCars[1], Themed), Ours("bmwm4gt4", "OpenDash Bmw 1280x480", "bmw"), Theirs("mx5", Default));
            var removed = Plan(new[] { Rim }, device);
            Assert.Equal(3, removed.Changes.Count);
            Assert.All(removed.Changes, c => Assert.Equal(CarPlaylistChangeKind.Remove, c.Kind));
            Assert.DoesNotContain(removed.Changes, c => c.Car == "mx5");

            var notCarried = PanelCarPlaylist.Plan(new[] { Rim, Porsche }, s => s.Theme == null, Contract.Themes, d => true, new[] { Device(Default, Ours(PorscheCars[0], Themed)) });
            Assert.Equal(new[] { CarPlaylistChangeKind.Remove }, notCarried.Changes.Select(c => c.Kind));
        }

        /// <summary>A screen added in this session is written to DashTemplates, but SimHub lists it only from its next
        /// start, and an entry naming it before then would blank the display in the car. It is bound at that start.</summary>
        [Fact]
        public void A_screen_simhub_does_not_list_yet_is_bound_at_the_next_start_and_not_before()
        {
            var plan = PanelCarPlaylist.Plan(new[] { Rim, Porsche }, s => true, Contract.Themes, d => d != Themed, new[] { Device(Default) });
            Assert.Empty(plan.Changes);
            Assert.True(plan.Screens["Porsche"].WaitsForRestart);
            Assert.Equal(new[] { PanelCarPlaylist.WaitsForRestart }, PanelCarPlaylist.Lines(plan.Screens["Porsche"], Porsche));
        }

        [Fact]
        public void A_screen_that_moved_folder_takes_our_entries_with_it()
        {
            var moved = Screen("OpenDash Porsche", "porsche", "Porsche");
            var plan = Plan(new[] { Rim, moved }, Device(Default, PorscheCars.Select(car => Ours(car, Themed)).ToArray()));
            Assert.Equal(PorscheCars.Length, plan.Changes.Count);
            Assert.All(plan.Changes, c =>
            {
                Assert.Equal(CarPlaylistChangeKind.Retarget, c.Kind);
                Assert.Equal("OpenDash Porsche", c.Dashboard);
            });
        }

        /// <summary>The fallback is the driver's: a display set to load no dashboard in a car with no entry keeps the
        /// themed face there, and the panel says so and where the setting is, and writes nothing to it.</summary>
        [Fact]
        public void A_display_with_no_dashboard_for_other_cars_is_said_to_keep_the_last_face()
        {
            var device = Device(Default);
            device.DefaultForOtherCars = null;
            var plan = Plan(new[] { Rim, Porsche }, device);
            Assert.Equal(new[] { "SD43" }, plan.Screens["Porsche"].KeepLastFace);
            Assert.Equal(new[]
            {
                "SD43 switches to this screen in the cars it is drawn for.",
                "In any other car, SD43 keeps the last face it showed. Choose the dashboard for those cars in its playlist in SimHub.",
            }, PanelCarPlaylist.Lines(plan.Screens["Porsche"], Porsche));
            Assert.Empty(Plan(new[] { Rim, Porsche }, Device(Default)).Screens["Porsche"].KeepLastFace);
        }

        [Fact]
        public void Two_themed_screens_of_a_size_do_not_both_claim_a_car_and_other_sizes_are_not_asked()
        {
            var second = Screen("OpenDash Porsche 2", "porsche", "Porsche2");
            var wide = Screen("OpenDash Porsche 1920x480", "porsche", "PorscheWide", 1920, 480);
            var plan = Plan(new[] { Rim, Porsche, second, wide }, Device(Default));
            Assert.Equal(PorscheCars.Length, plan.Changes.Count);
            Assert.All(plan.Changes, c => Assert.Equal(Themed, c.Dashboard));
            Assert.Empty(plan.Screens["Porsche2"].Devices);
            Assert.Empty(plan.Screens["PorscheWide"].Devices);
        }

        // --- What SimHub has to have ------------------------------------------------------------------

        /// <summary>
        /// Every member CarPlaylists reaches is in the SimHub assembly the plugin is built against, by the name the list
        /// gives it, so that the list in docs/research/simhub-dash-format.md is the list the code uses.
        /// </summary>
        /// <remarks>
        /// Read with System.Reflection.Metadata rather than loaded, since SimHub.Plugins.dll is a net48 WPF assembly this
        /// runtime cannot load. A member that is gone fails by its name.
        /// </remarks>
        [Fact]
        public void Every_simhub_member_the_playlists_reach_is_in_the_assembly_the_plugin_is_built_against()
        {
            var path = Path.Combine(RepoPaths.Root(), "plugin", "lib", "SimHub.Plugins.dll");
            var members = MembersOf(path);
            var missing = PanelCarPlaylist.Missing((type, member) => members.Contains(type + "." + member));
            Assert.True(missing.Count == 0, PanelCarPlaylist.MissingLog(missing));
        }

        [Fact]
        public void A_simhub_without_one_of_them_is_reported_by_name_and_nothing_is_written()
        {
            var missing = PanelCarPlaylist.Missing((type, member) => !(type.EndsWith(".DashPlaylistSettings", StringComparison.Ordinal) && member == "UpdateCurrentGame"));
            Assert.Equal(new[] { "SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.DashPlaylistSettings.UpdateCurrentGame" }, missing);
            Assert.Equal("The per-car playlists were not synchronised: this SimHub has no SimHub.Plugins.OutputPlugins.GraphicalDash.DashPlaylist.DashPlaylistSettings.UpdateCurrentGame, which OpenDash reaches as SimHub 9.12.6 has them. Nothing was written.",
                PanelCarPlaylist.MissingLog(missing));
        }

        /// <summary>Every public type's public methods, properties and fields, as "Namespace.Type.Member".</summary>
        private static HashSet<string> MembersOf(string path)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            using (var stream = File.OpenRead(path))
            using (var pe = new PEReader(stream))
            {
                var reader = pe.GetMetadataReader();
                foreach (var handle in reader.TypeDefinitions)
                {
                    var type = reader.GetTypeDefinition(handle);
                    var name = reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
                    foreach (var m in type.GetMethods())
                    {
                        var method = reader.GetMethodDefinition(m);
                        if ((method.Attributes & System.Reflection.MethodAttributes.MemberAccessMask) == System.Reflection.MethodAttributes.Public) found.Add(name + "." + reader.GetString(method.Name));
                    }
                    foreach (var p in type.GetProperties())
                    {
                        var property = reader.GetPropertyDefinition(p);
                        var accessors = property.GetAccessors();
                        var getter = accessors.Getter.IsNil ? default(MethodDefinition?) : reader.GetMethodDefinition(accessors.Getter);
                        if (getter.HasValue && (getter.Value.Attributes & System.Reflection.MethodAttributes.MemberAccessMask) == System.Reflection.MethodAttributes.Public) found.Add(name + "." + reader.GetString(property.Name));
                    }
                    foreach (var f in type.GetFields())
                    {
                        var field = reader.GetFieldDefinition(f);
                        if ((field.Attributes & System.Reflection.FieldAttributes.FieldAccessMask) == System.Reflection.FieldAttributes.Public) found.Add(name + "." + reader.GetString(field.Name));
                    }
                }
            }
            return found;
        }
    }
}
