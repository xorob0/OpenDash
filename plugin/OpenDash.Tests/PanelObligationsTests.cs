// PanelObligationsTests.cs: what the settings' #503 changes oblige the panel to do, read off its source.
//
// The contract half of #503 moved two answers under the panel's feet. The temperature thresholds are
// the rig's now, and Normalise writes them into every panel's entry on each save, so a row that writes
// one panel's entry and saves has its edit overwritten straight away. And a reversed strip is its plain
// shape with a switch on, so its profile is found by ProfileShapeId: a lookup by Shape finds the plain
// wiring for a strip wired from the far end, and installs it. Neither shows in a test of the settings,
// which are right; both show only in the panel, which these read the way PanelDataTabTests does.
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelObligationsTests
    {
        private static string Sources()
        {
            return string.Concat(RepoPaths.SettingsControlCode());
        }

        [Fact]
        public void The_temperature_rows_set_the_rigs_thresholds_and_not_one_panels()
        {
            var sources = Sources();
            Assert.Contains("SetLightsOilTemp(", sources);
            Assert.Contains("SetLightsWaterTemp(", sources);
            // An index into the per-panel arrays is a write Normalise undoes on the save that follows it.
            Assert.DoesNotContain("FlagBoxMatrixOilTemp[", sources);
            Assert.DoesNotContain("FlagBoxMatrixWaterTemp[", sources);
        }

        [Fact]
        public void A_strip_is_installed_by_the_profile_it_names()
        {
            var sources = Sources();
            Assert.Contains(".ProfileShapeId", sources);
            // A strip migrated from the 4/14/4 reversed shape is Shape "4-14-4" with Reversed on: looked up
            // by its shape, it finds the plain profile and installs it on a strip wired the other way.
            Assert.DoesNotContain("TryGetValue(bar.Shape", sources);
            Assert.DoesNotContain("entry.Id, bar.Shape", sources);
        }

        /// <summary>
        /// Every reader of the panel's sources reads code alone. A comment that named an anchor --
        /// "(Settings.SetRevBar)", "writes Contract.LedRpmStyleCar" -- kept a pin green with the code that did
        /// it deleted, so comments are dropped and literals kept.
        /// </summary>
        [Fact]
        public void The_panels_sources_are_read_as_code_alone()
        {
            var source = string.Join("\n",
                "// Settings.SetRevBar in a comment",
                "var url = \"https://example.org/a\"; /* Contract.LedRpmStyles */ var c = '/';",
                "/// <summary>Contract.LedRpmStyleCar</summary>",
                "var s = $\"{(x ? \"a//b\" : \"c\")} //kept\"; var v = @\"C:\\x \"\"//\"\" y\";");
            var code = RepoPaths.StripComments(source);
            Assert.DoesNotContain("SetRevBar", code);
            Assert.DoesNotContain("LedRpmStyles", code);
            Assert.DoesNotContain("LedRpmStyleCar", code);
            Assert.Contains("\"https://example.org/a\"", code);
            Assert.Contains("'/'", code);
            Assert.Contains("$\"{(x ? \"a//b\" : \"c\")} //kept\"", code);
            Assert.Contains("@\"C:\\x \"\"//\"\" y\"", code);
        }

        /// <summary>
        /// A wheel's night-mode or brightness press rebuilds only a page that drew the switch or a brightness
        /// as a control. LEDs walks SimHub's LED devices and Matrix reads its matrix profiles while they are
        /// built, so both re-dim their pictures in place through OnLighting, and neither asks to be rebuilt.
        /// </summary>
        [Fact]
        public void A_page_that_asks_simhub_while_it_is_built_repaints_its_lighting_in_place()
        {
            foreach (var name in new[] { "SettingsControl.Lights.cs", "SettingsControl.Matrix.cs" })
            {
                var code = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == name));
                Assert.DoesNotContain("DrawsLighting()", code);
                Assert.Contains("OnLighting(() => Ui.Redim(preview,", code);
            }
            var shell = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.cs"));
            // Cleared by every build, and run in place before any rebuild.
            Assert.Contains("lightingActions.Clear();", shell);
            Assert.Contains("Run(lightingActions.ToList(),", shell);
        }

        /// <summary>
        /// A glance re-bound through SimHub's Change command is corrected too. Change edits the mapping in
        /// place (trigger.PressType = pressType in 9.12.6) and the Triggers collection does not move, so the
        /// correction watches each mapping's own PropertyChanged, and the model's for a replaced collection.
        /// </summary>
        [Fact]
        public void A_glance_rebound_in_place_is_held_again()
        {
            var code = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.cs"));
            var start = code.IndexOf("private static void HoldWhilePressed(", StringComparison.Ordinal);
            Assert.True(start >= 0, "HoldWhilePressed is in the shell");
            var body = code.Substring(start, code.IndexOf("private static Button BuildLink(", start, StringComparison.Ordinal) - start);
            Assert.Contains("mapping.PropertyChanged += pressTypeChanged;", body);
            Assert.Contains("mapping.PropertyChanged -= pressTypeChanged;", body);
            Assert.Contains("args.PropertyName == \"PressType\"", body);
            Assert.Contains("watched.PropertyChanged += modelChanged;", body);
            Assert.Contains("watchedTriggers.CollectionChanged += collectionChanged;", body);
        }

        /// <summary>
        /// An undo a page registers while it is built runs once on Go, however many times the page was built
        /// in place since: OnLeave takes a key, and a key registered again replaces its action.
        /// </summary>
        [Fact]
        public void An_undo_registered_by_every_build_runs_once()
        {
            var code = string.Concat(RepoPaths.SettingsControlCode());
            Assert.Contains("private void OnLeave(string key, Action undo)", code);
            Assert.Contains("leaveActions.RemoveAll(pair => string.Equals(pair.Key, key, StringComparison.Ordinal));", code);
            Assert.DoesNotContain("OnLeave(() =>", code);
        }

        /// <summary>
        /// A press that changes what needs fixing asks again and redraws the sidebar, as the Matrix header's
        /// does: the Updates page's strip and flag box Update presses and a strip's device move redrew only
        /// their own row, and the Matrix and Updates dots and Home's list kept the old state until a page change.
        /// </summary>
        [Fact]
        public void A_press_that_moves_what_needs_fixing_refreshes_it()
        {
            var updates = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Updates.Lights.cs"));
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(updates, @"draw\((update|press)\(\)\);\s*RefreshAttention\(\);\s*RefreshSidebar\(\);").Count);
            var leds = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Lights.cs"));
            var move = leds.Substring(leds.IndexOf("private void MoveLedBar(", StringComparison.Ordinal));
            move = move.Substring(0, move.IndexOf("private FrameworkElement BuildLedBarActions(", StringComparison.Ordinal));
            Assert.Contains("RefreshAttention();", move);
            Assert.Contains("RefreshSidebar();", move);
        }
    }
}
