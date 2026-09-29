// ScreenActionsTests.cs: what the plugin hands SimHub's PluginManager, and not only what the contract
// lists.
//
// ContractTests already asserted that a companion owns no action, and passed for as long as
// AttachActions registered two per companion anyway, because it asserted the list and the
// registration made its own decision per kind (#435). These record what the registration passes,
// through the same delegate AttachActions hands PluginManager.AddAction, for a rig holding one screen
// of every kind.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class ScreenActionsTests
    {
        private sealed class Registered
        {
            public string Name;
            public Action Press;
            public Action Release;
        }

        private static ScreenInstance Screen(string kind, int width, int height, string folder = null)
        {
            var screen = new ScreenInstance { Kind = kind, Width = width, Height = height, Folder = folder };
            screen.Namespace = screen.StockNamespace;
            screen.Normalise();
            return screen;
        }

        /// <summary>A face, a companion, a pit wall and a round slots face: one of each kind there is.</summary>
        private static OpenDashSettings RigOfEveryKind()
        {
            var settings = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            settings.Rig.Add(Screen(Contract.KindFace, 1920, 480));
            settings.Rig.Add(Screen(Contract.KindCompanion, 850, 480));
            settings.Rig.Add(Screen(Contract.KindPitWall, 1920, 1080));
            settings.Rig.Add(Screen(Contract.KindSlots, 480, 480, "OpenDash 480 round"));
            settings.Normalise();
            return settings;
        }

        private static List<Registered> Record(OpenDashSettings settings)
        {
            var registered = new List<Registered>();
            ScreenActions.Register(() => settings, (name, press, release) => registered.Add(new Registered { Name = name, Press = press, Release = release }));
            return registered;
        }

        [Fact]
        public void Every_kind_is_on_the_rig_being_registered()
        {
            // The rig below has to hold one of each, or the assertion over it would say nothing about the
            // kind that is missing.
            var kinds = RigOfEveryKind().RigScreens().Select(s => s.Kind).ToArray();
            Assert.Equal(Contract.ScreenKinds.OrderBy(k => k, StringComparer.Ordinal), kinds.OrderBy(k => k, StringComparer.Ordinal));
        }

        [Fact]
        public void What_is_registered_is_what_the_contract_lists_screen_by_screen()
        {
            var settings = RigOfEveryKind();
            var expected = settings.RigScreens().SelectMany(s => Contract.ScreenActionNames(s.Kind, s.Namespace)).ToArray();
            Assert.Equal(expected, Record(settings).Select(r => r.Name).ToArray());
            // And that list, spelled out, so a contract that grew an action would fail here too rather
            // than being registered faithfully.
            Assert.Equal(new[]
            {
                "Face1920x480CycleZoneA",
                "Face1920x480CycleZoneB",
                "Face1920x480CycleZoneC",
                "Face1920x480CycleZoneD",
                "Face1920x480HoldQuickGlance",
                "Face1920x480CycleZoneABack",
                "Face1920x480CycleZoneBBack",
                "Face1920x480CycleZoneCBack",
                "Face1920x480CycleZoneDBack",
                "CompanionHoldQuickGlance",
                "PitWallHoldQuickGlance",
            }, expected);
        }

        [Fact]
        public void A_companion_registers_its_glance_and_nothing_to_page_with()
        {
            var registered = Record(RigOfEveryKind());
            Assert.Equal(new[] { "CompanionHoldQuickGlance" },
                registered.Where(r => r.Name.StartsWith(Contract.CompanionPrefix, StringComparison.Ordinal)).Select(r => r.Name).ToArray());
            // Paging is SimHub's NextScreen, so nothing that would be a second binding for it.
            Assert.DoesNotContain(registered, r => r.Name.EndsWith("NextModule", StringComparison.Ordinal));

            // Two companions, a glance each, under each one's own namespace, so a button glances one
            // phone and not both.
            var companions = new OpenDashSettings { Rig = new List<ScreenInstance> { Screen(Contract.KindCompanion, 850, 480), Screen(Contract.KindCompanion, 480, 850) } };
            companions.Rig[1].Namespace = "Garage";
            companions.Normalise();
            Assert.Equal(new[] { "CompanionHoldQuickGlance", "GarageHoldQuickGlance" }, Record(companions).Select(r => r.Name).ToArray());
        }

        [Fact]
        public void A_glance_carries_a_release_and_a_cycle_does_not()
        {
            // The release is the whole of a glance, and the reason AttachActions goes through the
            // PluginManager overload rather than the extension method that nulls it.
            foreach (var r in Record(RigOfEveryKind()))
            {
                Assert.NotNull(r.Press);
                if (r.Name.EndsWith("HoldQuickGlance", StringComparison.Ordinal)) Assert.NotNull(r.Release);
                else Assert.Null(r.Release);
            }
        }

        [Fact]
        public void Each_registered_action_moves_its_own_screen()
        {
            var settings = RigOfEveryKind();
            var registered = Record(settings).ToDictionary(r => r.Name, StringComparer.Ordinal);
            var face = settings.ScreenOf("Face1920x480").Face;

            var before = face.Zones[1];
            registered["Face1920x480CycleZoneB"].Press();
            Assert.NotEqual(before, face.Zones[1]);

            var glance = Contract.NormaliseQuickGlance(face.QuickGlance);
            var zone = Contract.QuickGlanceZone(glance);
            var held = face.Zones[zone];
            registered["Face1920x480HoldQuickGlance"].Press();
            Assert.Equal(Contract.QuickGlancePage(glance), face.Zones[zone]);
            registered["Face1920x480HoldQuickGlance"].Release();
            Assert.Equal(held, face.Zones[zone]);

            var towerA = settings.ScreenZone("PitWall", "TowerA");
            var wall = settings.ScreenOf("PitWall");
            var glanceZones = Contract.GlanceZoneSlots();
            wall.PitWallQuickGlance = Contract.PitWallQuickGlanceValue(glanceZones.ToList().FindIndex(slot => slot.Key == "TowerA"), 9);
            registered["PitWallHoldQuickGlance"].Press();
            Assert.Equal(9, settings.ScreenZone("PitWall", "TowerA"));
            registered["PitWallHoldQuickGlance"].Release();
            Assert.Equal(towerA, settings.ScreenZone("PitWall", "TowerA"));

            // A companion's glance is a force: its module while held, and once released the way back,
            // which lasts a moment and is timed exactly in SettingsTests. Either is "not the glance".
            registered["CompanionHoldQuickGlance"].Press();
            Assert.Equal(Contract.DefaultCompanionQuickGlance, settings.ScreenCompanionOpenOn("Companion"));
            registered["CompanionHoldQuickGlance"].Release();
            Assert.Contains(settings.ScreenCompanionOpenOn("Companion"), new[] { Contract.CompanionOpenOnBack, Contract.DefaultCompanionOpenOn });
        }

        [Fact]
        public void A_press_reads_the_settings_it_is_pressed_under()
        {
            // The panel replaces the settings object whenever the user changes something, so a callback
            // holding the one it was registered under would move a screen nobody is looking at.
            var first = RigOfEveryKind();
            var current = first;
            var registered = new Dictionary<string, Action>(StringComparer.Ordinal);
            ScreenActions.Register(() => current, (name, press, release) => registered[name] = press);
            current = RigOfEveryKind();
            var before = first.ScreenOf("Face1920x480").Face.Zones[0];
            registered["Face1920x480CycleZoneA"]();
            Assert.Equal(before, first.ScreenOf("Face1920x480").Face.Zones[0]);
            Assert.NotEqual(before, current.ScreenOf("Face1920x480").Face.Zones[0]);
        }

        [Fact]
        public void A_press_on_a_screen_the_rig_no_longer_has_does_nothing()
        {
            // An action stays bound until SimHub restarts, so a press after a remove must not throw on
            // SimHub's own thread.
            var settings = RigOfEveryKind();
            var registered = Record(settings);
            settings.Rig.Clear();
            foreach (var r in registered)
            {
                r.Press();
                if (r.Release != null) r.Release();
            }
        }

        [Fact]
        public void AttachActions_registers_through_ScreenActions_and_decides_nothing_itself()
        {
            // OpenDash.cs holds SimHub's PluginManager and cannot be compiled here, so this reads it: the
            // registration is the one call, and no per-kind name is spelled there any more.
            var source = File.ReadAllText(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "OpenDash.cs"));
            var start = source.IndexOf("private void AttachActions(PluginManager pluginManager)", StringComparison.Ordinal);
            Assert.True(start >= 0);
            var end = source.IndexOf("\n        }\n", start, StringComparison.Ordinal);
            Assert.True(end > start);
            var body = source.Substring(start, end - start);
            Assert.Contains("ScreenActions.Register(() => Settings,", body);
            Assert.Contains("pluginManager.AddAction(", body);
            // The one registration in the file, and it is inside ScreenActions.Register's delegate: a second
            // AddAction with a name spelled by hand, here or anywhere in OpenDash.cs, fails this.
            Assert.Equal(1, Occurrences(source, ".AddAction("));
            Assert.Equal(1, Occurrences(body, ".AddAction("));
            Assert.DoesNotContain("this.AddAction(", source);
            Assert.DoesNotContain("Contract.NextModuleActionFor(", source);
            Assert.DoesNotContain("Contract.HoldQuickGlanceActionFor(", source);
            Assert.DoesNotContain("Contract.CycleZoneAction(", source);
        }

        private static int Occurrences(string text, string needle)
        {
            var count = 0;
            for (var at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal)) count++;
            return count;
        }
    }
}
