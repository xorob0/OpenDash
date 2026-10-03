// ScreenAttachmentsTests.cs: a screen added while SimHub is running is attached when it is added (#636).
//
// Init attached the properties and actions of the rig it found and nothing attached any after it, so a
// screen added from the panel read the literal default of every binding until SimHub restarted, although
// SimHub listed its dashboard and drew it. These hold the decision of which screens still need attaching,
// and, by reading OpenDash.cs, that the plugin asks it when Init attaches the rig and again on every save.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class ScreenAttachmentsTests
    {
        private static readonly PackageEntry Rim = new PackageEntry { Package = "p", Folder = "OpenDash 850x480", Kind = Contract.KindFace, Width = 850, Height = 480 };
        private static readonly PackageEntry Phone = new PackageEntry { Package = "c", Folder = "OpenDash Companion", Kind = Contract.KindCompanion, Width = 850, Height = 480 };

        private static OpenDashSettings Rig(params PackageEntry[] entries)
        {
            var settings = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            foreach (var entry in entries) settings.AddScreen(entry, null);
            settings.Normalise();
            return settings;
        }

        private static string[] Namespaces(IEnumerable<ScreenInstance> screens)
        {
            return screens.Select(s => s.Namespace).ToArray();
        }

        [Fact]
        public void A_screen_added_after_the_rig_was_attached_is_handed_out_on_its_own()
        {
            var settings = Rig(Rim);
            var attached = new ScreenAttachments();
            Assert.Equal(new[] { "Face850x480" }, Namespaces(attached.Take(settings.RigScreens())));

            // The panel's Add: the case #636 is, which nothing attached until SimHub restarted.
            var second = settings.AddScreen(Rim, "Rim");
            var phone = settings.AddScreen(Phone, "Phone");
            settings.Normalise();
            Assert.Equal(new[] { second.Namespace, phone.Namespace }, Namespaces(attached.Take(settings.RigScreens())));

            // And once each: SimHub keeps the first action registered under a name, and a second save has
            // nothing left to attach.
            Assert.Empty(attached.Take(settings.RigScreens()));
        }

        [Fact]
        public void A_rig_that_was_empty_at_startup_hands_out_its_first_screen()
        {
            // The reproduction on the VM: `rig.ts clear`, SimHub started, a Rim added from the panel.
            var settings = Rig();
            var attached = new ScreenAttachments();
            Assert.Empty(attached.Take(settings.RigScreens()));
            settings.AddScreen(Rim, "Rim");
            settings.Normalise();
            Assert.Equal(new[] { "Face850x480" }, Namespaces(attached.Take(settings.RigScreens())));
        }

        [Fact]
        public void A_screen_added_back_under_its_namespace_is_served_by_what_is_already_attached()
        {
            // Why #636 looked intermittent: the first Rim of a session removed and added again takes the
            // stock namespace back, and the delegates Init attached for it look the screen up by namespace.
            var settings = Rig(Rim);
            var attached = new ScreenAttachments();
            attached.Take(settings.RigScreens());
            Assert.True(settings.RemoveScreen("Face850x480"));
            settings.AddScreen(Rim, "Rim");
            settings.Normalise();
            Assert.Equal("Face850x480", settings.RigScreens().Single().Namespace);
            Assert.Empty(attached.Take(settings.RigScreens()));
        }

        [Fact]
        public void A_namespace_reused_by_another_kind_is_attached_again()
        {
            // A face called Rim removed and a companion called Rim added share a namespace and none of its
            // names: the companion's page and modules were never attached.
            var face = new ScreenInstance { Kind = Contract.KindFace, Width = 850, Height = 480, Namespace = "Rim" };
            face.Normalise();
            var companion = new ScreenInstance { Kind = Contract.KindCompanion, Width = 850, Height = 480, Namespace = "Rim" };
            companion.Normalise();
            var attached = new ScreenAttachments();
            Assert.Single(attached.Take(new[] { face }));
            Assert.Single(attached.Take(new[] { companion }));
        }

        [Fact]
        public void Case_does_not_make_a_second_screen_since_SimHub_lowercases_every_name()
        {
            var first = new ScreenInstance { Kind = Contract.KindFace, Width = 850, Height = 480, Namespace = "Rim" };
            var second = new ScreenInstance { Kind = Contract.KindFace, Width = 850, Height = 480, Namespace = "RIM" };
            var attached = new ScreenAttachments();
            Assert.Single(attached.Take(new[] { first }));
            Assert.Empty(attached.Take(new[] { second }));
        }

        [Fact]
        public void Nothing_and_no_namespace_hand_out_nothing()
        {
            var attached = new ScreenAttachments();
            Assert.Empty(attached.Take(null));
            Assert.Empty(attached.Take(new ScreenInstance[] { null, new ScreenInstance { Kind = Contract.KindFace } }));
        }

        [Fact]
        public void An_added_screen_registers_the_actions_the_contract_lists_for_it_and_they_move_it()
        {
            var settings = Rig(Rim);
            var added = settings.AddScreen(Rim, "Dash");
            settings.Normalise();
            var registered = new List<string>();
            var presses = new Dictionary<string, Action>();
            ScreenActions.RegisterScreen(() => settings, added, (name, press, release) =>
            {
                registered.Add(name);
                presses[name] = press;
            });
            Assert.Equal(Contract.ScreenActionNames(added.Kind, added.Namespace).ToArray(), registered.ToArray());

            var rim = settings.ScreenByNamespace("Face850x480");
            var before = added.Face.Zone("B");
            var rimBefore = rim.Face.Zone("B");
            presses[added.Namespace + "CycleZoneB"]();
            Assert.NotEqual(before, added.Face.Zone("B"));
            // And only the screen it names: the Rim beside it is where it was.
            Assert.Equal(rimBefore, rim.Face.Zone("B"));
        }

        /// <summary>The body of one method of OpenDash.cs, which the net8.0 project cannot compile.</summary>
        private static string Method(string source, string signature)
        {
            var start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(start >= 0, "OpenDash.cs has no " + signature);
            var end = source.IndexOf("\n        }\n", start, StringComparison.Ordinal);
            Assert.True(end > start);
            return source.Substring(start, end - start);
        }

        [Fact]
        public void The_plugin_attaches_a_screen_added_after_Init_through_the_code_Init_uses()
        {
            var source = File.ReadAllText(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "OpenDash.cs"));

            // Every save attaches what the rig gained since the last one: Add and Duplicate both end in a save.
            Assert.Contains("AttachAddedScreens();", Method(source, "public void SaveSettings()"));

            // Init's rig and an added screen go through the one method, so the two cannot list different names.
            var properties = Method(source, "private void AttachProperties()");
            Assert.Contains("foreach (var screen in attached.Take(Settings.RigScreens())) AttachScreenProperties(screen);", properties);
            var added = Method(source, "private void AttachAddedScreens()");
            Assert.Contains("foreach (var screen in attached.Take(Settings.RigScreens()))", added);
            Assert.Contains("AttachScreenProperties(screen);", added);
            // And the same registration AttachActions hands SimHub, kept rather than spelled a second time.
            Assert.Contains("ScreenActions.RegisterScreen(() => Settings, screen, registerAction);", added);
            Assert.Contains("ScreenActions.Register(() => Settings, registerAction,", Method(source, "private void AttachActions(PluginManager pluginManager)"));

            // The per-screen names are attached in that method and nowhere else.
            var screen = Method(source, "private void AttachScreenProperties(ScreenInstance s)");
            foreach (var call in new[] { "Contract.ZoneStartProperty(s.Namespace", "Contract.CompanionPageProperty(s.Namespace", "Contract.PitWallPageProperty(s.Namespace" })
            {
                Assert.Contains(call, screen);
                Assert.Equal(1, source.Split(new[] { call }, StringSplitOptions.None).Length - 1);
            }
        }
    }
}
