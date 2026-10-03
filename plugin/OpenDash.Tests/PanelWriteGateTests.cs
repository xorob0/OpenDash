// PanelWriteGateTests.cs: one gate for every press that writes SimHub's dashboards (#606). A press made while an
// update is running, or while another press is still writing, is refused with a line rather than queued behind
// the run or written over it, and refused before it has changed anything.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    [Collection(UpdateServiceWriters.Name)]
    public class PanelWriteGateTests
    {
        [Fact]
        public void Nothing_running_lets_a_press_write()
        {
            Assert.Null(PanelWriteGate.Refusal(updating: false, writing: false));
        }

        [Fact]
        public void A_press_is_refused_while_an_update_runs_or_another_press_writes()
        {
            Assert.Equal(PanelWriteGate.WhileUpdating, PanelWriteGate.Refusal(updating: true, writing: false));
            Assert.Equal(PanelWriteGate.WhileWriting, PanelWriteGate.Refusal(updating: false, writing: true));
            // An update that has reached its install is both, and the update is the one the driver started.
            Assert.Equal(PanelWriteGate.WhileUpdating, PanelWriteGate.Refusal(updating: true, writing: true));
        }

        /// <summary>voice.md: say what to do, never how it works.</summary>
        [Fact]
        public void The_refusals_say_what_to_do()
        {
            Assert.Equal("An update is installing. Try again when it has finished.", PanelWriteGate.WhileUpdating);
            Assert.Equal("A dashboard is still being written. Try again in a moment.", PanelWriteGate.WhileWriting);
        }

        /// <summary>
        /// The gate reads the service's state, which every panel the plugin builds shares: a press that writes is
        /// refused while a write the panel handed over is still running.
        /// </summary>
        [Fact]
        public void A_press_is_refused_while_a_handed_over_write_is_still_running()
        {
            var service = new UpdateService(new NoReleases());
            var release = new System.Threading.ManualResetEventSlim();
            var running = new System.Threading.ManualResetEventSlim();
            var finished = new System.Threading.ManualResetEventSlim();

            Assert.True(service.WriteInBackground(() => { running.Set(); release.Wait(TimeSpan.FromSeconds(10)); }, failure => finished.Set()));
            Assert.True(running.Wait(TimeSpan.FromSeconds(5)));

            Assert.Equal(PanelWriteGate.WhileWriting, PanelWriteGate.Refusal(service.Applying, UpdateService.Busy));

            release.Set();
            Assert.True(finished.Wait(TimeSpan.FromSeconds(10)));
            Assert.Null(PanelWriteGate.Refusal(service.Applying, UpdateService.Busy));
        }

        private sealed class NoReleases : IReleaseSource
        {
            public FetchResult GetString(string url) => FetchResult.Failed("not asked");
            public FetchResult GetBytes(string url, Action<double> progress = null, System.Threading.CancellationToken cancel = default(System.Threading.CancellationToken)) => FetchResult.Failed("not asked");
        }

        /// <summary>The methods that write and are only ever called from a press that has already asked.</summary>
        private static readonly string[] Helpers =
        {
            "private void WriteThen(",
            "private void WriteScreenThen(",
            "private void UpdatesReadThen(",
        };

        /// <summary>What a press changes before it writes, which a refused press must not have changed.</summary>
        private static readonly string[] Changes =
        {
            "Settings.AddScreen(", "Settings.ResizeScreen(", "Settings.DuplicateScreen(", "Settings.RemoveScreen(",
            "screen.Keep()", "RenameScreen(", "CloseSheet()", "Save(",
        };

        /// <summary>
        /// Every press that writes, puts back or removes a dashboard folder, and Download, asks WriteRefused before
        /// it hands anything over and before it has changed the settings or closed its sheet: the Screens page's add,
        /// duplicate, rename, resize, reinstall and remove, Home's install again, Reinstall everything, Put mine back
        /// and the update itself. The panel is WPF and is read rather than run.
        /// </summary>
        [Fact]
        public void Every_press_that_writes_asks_the_gate_before_it_changes_anything()
        {
            var writes = new[] { "WriteScreenThen(", "WriteThen(", "UpdatesReadThen(", "plugin.ApplyUpdate(" };
            var method = new Regex(@"(private|public|internal|protected)[^;{}=]*\(");
            var ungated = new List<string>();
            var presses = new HashSet<string>();
            foreach (var path in RepoPaths.SettingsControlSources())
            {
                var code = RepoPaths.Code(path);
                foreach (var write in writes)
                {
                    for (var at = code.IndexOf(write, StringComparison.Ordinal); at >= 0; at = code.IndexOf(write, at + 1, StringComparison.Ordinal))
                    {
                        // WriteScreenThen( contains WriteThen(, and a declaration is not a call.
                        if (write == "WriteThen(" && at >= 6 && code.Substring(at - 6, 6) == "Screen") continue;
                        var before = code.Substring(0, at);
                        if (before.TrimEnd().EndsWith("void", StringComparison.Ordinal)) continue;
                        var declaration = method.Matches(before).Cast<Match>().LastOrDefault();
                        if (declaration == null) continue;
                        var head = code.Substring(declaration.Index, Math.Min(120, code.Length - declaration.Index));
                        if (Helpers.Any(h => head.StartsWith(h, StringComparison.Ordinal))) continue;
                        var name = head.Split('(')[0];
                        presses.Add(name);
                        var body = code.Substring(declaration.Index, at - declaration.Index);
                        var gate = body.IndexOf("WriteRefused()) return;", StringComparison.Ordinal);
                        var changed = Changes.Select(c => body.IndexOf(c, StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(int.MaxValue).Min();
                        if (gate < 0 || gate > changed) ungated.Add(Path.GetFileName(path) + ": " + name + " -> " + write);
                    }
                }
            }
            Assert.True(presses.Count >= 10, "found only " + presses.Count + " presses that write; the patterns no longer match the panel:\n" + string.Join("\n", presses));
            Assert.True(ungated.Count == 0, "Writes without asking the gate first:\n" + string.Join("\n", ungated.Distinct()));
        }
    }

    /// <summary>
    /// The tests that start writers on an UpdateService: UpdateService.Busy is static, so one class's write in
    /// flight is another's Busy, and the two run one after the other rather than beside each other.
    /// </summary>
    [CollectionDefinition(Name)]
    public class UpdateServiceWriters
    {
        public const string Name = "UpdateService writers";
    }
}
