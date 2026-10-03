// PanelLedDevicesTests.cs: SimHub's LED devices are read once each time the panel asks SimHub again, and everything
// a page build and its presses need comes off that one read (#611).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelLedDevicesTests
    {
        /// <summary>A device source that counts how often it is walked, standing in for LedTargets.All.</summary>
        private sealed class Source
        {
            public int Reads;
            public List<string> Devices = new List<string> { "arduino", "wheel" };
            public List<string> Passed = new List<string> { "pedals" };

            public List<string> Read(out IList<string> declined)
            {
                Reads++;
                declined = Passed;
                return Devices;
            }
        }

        /// <summary>
        /// What the LEDs page asks of SimHub's devices as it builds and as a strip is installed -- the census of the
        /// rig's strips, Home's facts, the strip's header, its device row, then the install's uninstall from every
        /// device, its find and its line -- is one walk, where it used to be three for the build and two more for every
        /// strip an install wrote.
        /// </summary>
        [Fact]
        public void A_page_build_and_its_presses_walk_SimHub_s_devices_once()
        {
            var source = new Source();
            var devices = new PanelLedDevices<string>(source.Read);

            // RefreshAttention: forgets, then the census and Home's facts.
            devices.Forget();
            var census = devices.Targets;
            var facts = devices.Targets;
            // The strip's section: its header and device row.
            var header = devices.Targets;
            var declined = devices.Declined;
            // A press on each of two strips: the uninstall from every device, the install into one, and the line.
            for (var strip = 0; strip < 2; strip++)
            {
                Assert.Equal(2, devices.Targets.Count);
                Assert.Contains("wheel", devices.Targets);
                Assert.Equal(new[] { "pedals" }, devices.Declined);
            }

            Assert.Equal(1, source.Reads);
            Assert.Same(census, header);
            Assert.Same(facts, header);
            Assert.Equal(new[] { "pedals" }, declined);
        }

        /// <summary>The next ask of SimHub -- a Go, a Redraw, a return to the panel -- reads them again, so a wheel
        /// plugged in on SimHub's Devices page is there when the panel comes back.</summary>
        [Fact]
        public void Asking_SimHub_again_reads_the_devices_again()
        {
            var source = new Source();
            var devices = new PanelLedDevices<string>(source.Read);
            Assert.Equal(2, devices.Targets.Count);

            source.Devices = new List<string> { "arduino", "wheel", "rim" };
            Assert.Equal(2, devices.Targets.Count);
            devices.Forget();

            Assert.Equal(3, devices.Targets.Count);
            Assert.Equal(2, source.Reads);
        }

        /// <summary>A rig with no LED device is an empty list, read once like any other, not a walk on every ask; and
        /// a source that answers nothing is read as nothing rather than as a null a page would trip over.</summary>
        [Fact]
        public void No_devices_is_an_empty_answer_read_once()
        {
            var reads = 0;
            var devices = new PanelLedDevices<string>((out IList<string> declined) =>
            {
                reads++;
                declined = null;
                return null;
            });

            Assert.Empty(devices.Targets);
            Assert.Empty(devices.Declined);
            Assert.Empty(devices.Targets);
            Assert.Equal(1, reads);
        }

        /// <summary>
        /// The panel's sources reach SimHub's devices only through the one read: nothing in a page walks them for
        /// itself, and every install, uninstall, census and find is handed the list the page already has. The
        /// overloads without the list walk SimHub's devices again, which is what they are for outside the panel.
        /// </summary>
        [Fact]
        public void The_panel_walks_SimHub_s_devices_only_through_its_one_read()
        {
            var found = new List<string>();
            foreach (var path in RepoPaths.SettingsControlSources())
            {
                var code = RepoPaths.Code(path);
                var name = Path.GetFileName(path);
                foreach (Match all in Regex.Matches(code, @"LedTargets\.All\b[^;]*"))
                {
                    if (all.Value != "LedTargets.All)") found.Add(name + ": " + all.Value);
                }
                foreach (var call in new Dictionary<string, int>
                {
                    { "StripInstaller.InstalledEverywhere(", 1 },
                    { "StripInstaller.UninstallEverywhere(", 2 },
                    { "StripInstaller.Install(", 3 },
                    { "LedTargets.Find(", 2 },
                    { "LedTargets.Preferred(", 1 },
                })
                {
                    for (var at = code.IndexOf(call.Key, StringComparison.Ordinal); at >= 0; at = code.IndexOf(call.Key, at + 1, StringComparison.Ordinal))
                    {
                        var count = Arguments(code, at + call.Key.Length);
                        if (count != call.Value) found.Add(name + ": " + call.Key + " with " + count + " arguments");
                    }
                }
            }
            Assert.True(found.Count == 0, "Walks SimHub's devices for itself:\n" + string.Join("\n", found));

            var status = Regex.Replace(RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => p.EndsWith("SettingsControl.Status.cs", StringComparison.Ordinal))), @"\s+", " ");
            Assert.Contains("new PanelLedDevices<LedTarget>(LedTargets.All)", status);
            var refresh = status.Substring(status.IndexOf("private void RefreshAttention()", StringComparison.Ordinal));
            var forget = refresh.IndexOf("ledDevices.Forget();", StringComparison.Ordinal);
            var ask = refresh.IndexOf("attentionFacts = Attention();", StringComparison.Ordinal);
            Assert.True(forget >= 0 && ask > forget, "RefreshAttention lets the last read go before it asks SimHub again");
        }

        /// <summary>How many arguments a call takes, read from just after its opening parenthesis.</summary>
        private static int Arguments(string code, int from)
        {
            var depth = 0;
            var commas = 0;
            var any = false;
            for (var i = from; i < code.Length; i++)
            {
                var c = code[i];
                if (depth == 0 && c == ')') return any ? commas + 1 : 0;
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == ',' && depth == 0) commas++;
                if (!char.IsWhiteSpace(c)) any = true;
            }
            return -1;
        }
    }
}
