// BarAttachmentsTests.cs: a strip added while SimHub is running is attached when it is added (#565).
//
// Init attached each strip's own names -- its centre, rev style, flag animation, spotter, brightness and
// effect switches -- for the strips it found, and nothing attached any after it. A strip added from the
// LEDs page had its rewritten profile installed at once, and that profile reads only the strip's own
// names, so every one of its settings fell through to the profile's isnull() default until SimHub
// restarted. These hold the decision of which strips still need attaching and, by reading OpenDash.cs,
// that the plugin asks it when Init attaches the lights and again on every save. #636 is the same for
// screens (ScreenAttachmentsTests).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class BarAttachmentsTests
    {
        private static OpenDashSettings Rig(params string[] names)
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            foreach (var name in names) settings.AddLedBar("3-9-3", name, "arduino");
            settings.Normalise();
            return settings;
        }

        private static string[] Namespaces(IEnumerable<LedBar> bars)
        {
            return bars.Select(b => b.Namespace).ToArray();
        }

        [Fact]
        public void A_strip_added_after_the_lights_were_attached_is_handed_out_on_its_own()
        {
            var settings = Rig("Wheel rim", "Dash brow");
            var attached = new BarAttachments();
            Assert.Equal(new[] { "LedWheelRim", "LedDashBrow" }, Namespaces(attached.Take(settings.LedBarList())));

            // The LEDs page's Add: the case #565 is, which nothing attached until SimHub restarted.
            var strip = settings.AddLedBar("3-9-3", "Strip", "arduino");
            settings.Normalise();
            Assert.Equal(new[] { strip.Namespace }, Namespaces(attached.Take(settings.LedBarList())));

            // And once each: a second save has nothing left to attach.
            Assert.Empty(attached.Take(settings.LedBarList()));

            // What the delegate it is handed to reads, by namespace, follows the panel: the VM check's read.
            settings.LedBarByNamespace(strip.Namespace).Centre = "fuel";
            Assert.Equal("fuel", settings.BarCentre(strip.Namespace));
        }

        [Fact]
        public void A_rig_with_no_strips_at_startup_hands_out_its_first()
        {
            var settings = Rig();
            var attached = new BarAttachments();
            Assert.Empty(attached.Take(settings.LedBarList()));
            settings.AddLedBar("3-9-3", "Strip", "arduino");
            settings.Normalise();
            Assert.Equal(new[] { "LedStrip" }, Namespaces(attached.Take(settings.LedBarList())));
        }

        [Fact]
        public void A_strip_added_back_under_its_namespace_is_served_by_what_is_already_attached()
        {
            // Why #565 hid in testing: a strip removed and added again under its name takes its namespace
            // back, and the delegates attached for it look the strip up by namespace.
            var settings = Rig("Strip");
            var attached = new BarAttachments();
            attached.Take(settings.LedBarList());
            Assert.True(settings.RemoveLedBar("LedStrip"));
            settings.AddLedBar("3-9-3", "Strip", "arduino");
            settings.Normalise();
            Assert.Equal("LedStrip", settings.LedBarList().Single().Namespace);
            Assert.Empty(attached.Take(settings.LedBarList()));
        }

        [Fact]
        public void Case_does_not_make_a_second_strip_since_SimHub_lowercases_every_name()
        {
            var attached = new BarAttachments();
            Assert.Single(attached.Take(new[] { new LedBar { Namespace = "LedStrip" } }));
            Assert.Empty(attached.Take(new[] { new LedBar { Namespace = "LEDSTRIP" } }));
        }

        [Fact]
        public void Nothing_and_no_namespace_hand_out_nothing()
        {
            var attached = new BarAttachments();
            Assert.Empty(attached.Take(null));
            Assert.Empty(attached.Take(new LedBar[] { null, new LedBar() }));
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
        public void The_plugin_attaches_a_strip_added_after_Init_through_the_code_Init_uses()
        {
            var source = File.ReadAllText(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "OpenDash.cs"));

            // Every save attaches the strips the rig gained since the last one: the LEDs page's Add ends in one.
            Assert.Contains("AttachAddedBars();", Method(source, "public void SaveSettings()"));

            // Init's strips and an added one go through the one method, so the two cannot list different names.
            Assert.Contains("foreach (var bar in attachedBars.Take(Settings.LedBarList())) AttachBarProperties(bar.Namespace);",
                Method(source, "private void AttachLightsProperties()"));
            var added = Method(source, "private void AttachAddedBars()");
            Assert.Contains("foreach (var bar in attachedBars.Take(Settings.LedBarList()))", added);
            Assert.Contains("AttachBarProperties(bar.Namespace);", added);

            // The per-strip names are attached in that method and nowhere else.
            var bar = Method(source, "private void AttachBarProperties(string ns)");
            foreach (var setting in new[] { "Contract.LedCentre", "Contract.LedRpmStyle", "Contract.LedFlagAnimation", "Contract.LedSpotterWhole", "Contract.LedBrightness", "setting" })
            {
                var call = "this.AttachDelegate(LedBarProfile.Property(ns, " + setting + ")";
                Assert.Contains(call, bar);
                Assert.Equal(1, source.Split(new[] { call }, StringSplitOptions.None).Length - 1);
            }
        }
    }
}
