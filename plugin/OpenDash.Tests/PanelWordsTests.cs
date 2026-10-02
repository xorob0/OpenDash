// PanelWordsTests.cs: the words two or more pages draw for one thing, held to one constant (#524).
//
// Each ruling of #524 puts a word on the page that owns it and has every other page read it. A page that types
// its own copy, or rewords the one it reads, fails here rather than drifting: voice.md's "One word per thing"
// across the whole panel, where each page's own tests hold only its own words. The restart phrase (ruling 1)
// is held by PanelScreensTests.A_screen_waiting_for_the_restart_is_said_one_way_on_every_page, and the Matrix
// link's plural of the Settings column (ruling 7) by PanelMatrixTests beside its route.
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelWordsTests
    {
        private static string Source(string file)
        {
            var path = System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", file);
            return System.Text.RegularExpressions.Regex.Replace(RepoPaths.Code(path), @"\s+", " ");
        }

        /// <summary>Ruling 2: a strip whose profile this build does not ship is "Not installed" on the LEDs
        /// card, on Home and in the Updates table, which no longer says "Unknown" for it.</summary>
        [Fact]
        public void A_strip_whose_profile_the_build_does_not_ship_is_not_installed_on_every_page()
        {
            foreach (var selected in new bool?[] { null, true, false })
            {
                Assert.Equal(PanelCopy.NotInstalled, PanelLeds.StateText(FlagBoxInstallState.NotEmbedded, selected));
                Assert.Equal(PanelCopy.NotInstalled, PanelHome.StripLine(false, null, FlagBoxInstallState.NotEmbedded, selected).Text);
            }
            var row = PanelUpdates.StripRow("Rim", new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded });
            Assert.Equal(PanelCopy.NotInstalled, row.State);
            Assert.NotEqual(PanelUpdates.Unknown, row.State);
            // The hover still says why no press here installs it, in the LEDs page's words.
            Assert.Equal(PanelLeds.NoProfileForStrip, row.Tooltip);
        }

        /// <summary>Ruling 6: the scenario chips' section is "Preview" on Rig, Matrix, LEDs and Settings, the Rig
        /// page's constant, and Rig's "Emulation" is gone.</summary>
        [Fact]
        public void The_scenario_chips_are_called_preview_on_every_page()
        {
            Assert.Equal("Preview", PanelRigMap.ScenariosName);
            Assert.Equal(PanelRigMap.ScenariosName, PanelSettings.PreviewTitle);
            Assert.Equal(PanelRigMap.ScenariosName, PanelMatrix.PreviewChipsName);
            Assert.Equal(PanelRigMap.ScenariosName, PanelLeds.PreviewChipsName);
            // Each page draws the name it reads.
            Assert.Contains("AutomationProperties.SetName(host, PanelRigMap.ScenariosName);", Source("SettingsControl.Rig.cs"));
            Assert.Contains("AutomationProperties.SetName(chipGroup, PanelMatrix.PreviewChipsName);", Source("SettingsControl.Matrix.cs"));
            Assert.Contains("var chipGroup = new GroupBorder { Child = chips }; AutomationProperties.SetName(chipGroup, PanelLeds.PreviewChipsName); top.Children.Add(chipGroup);", Source("SettingsControl.Lights.cs"));
            Assert.Contains("Ui.Eyebrow(PanelSettings.PreviewTitle)", Source("SettingsControl.Settings.cs"));
        }

        /// <summary>Ruling 7: the alert table's second column is "Trigger", and the Matrix page's link to it is
        /// that word's plural.</summary>
        [Fact]
        public void The_alert_tables_second_column_is_trigger_and_the_matrix_links_to_it_by_that_word()
        {
            Assert.Equal("Trigger", PanelSettings.ThresholdColumn);
            Assert.Equal(PanelSettings.ThresholdColumn + "s", PanelMatrix.ThresholdsLink);
            Assert.Equal(PanelSettings.AnchorAlerts, PanelMatrix.ThresholdsRoute.Anchor);
        }

        /// <summary>Ruling 8: a matrix no device shows is "Not shown in SimHub" on the Matrix card, on Home's line,
        /// as the Matrix fix box's title and in Home's issue; the matrix number is in the steps under the box.</summary>
        [Fact]
        public void A_matrix_no_device_shows_is_not_shown_in_SimHub_on_every_page()
        {
            const string state = "Not shown in SimHub";
            Assert.Equal(state, PanelMatrix.NotShown);
            Assert.Equal(state, PanelMatrix.CardLine("Left pillar", 2, Contract.DefaultFlagBoxSide, false));
            Assert.Equal(state, PanelMatrix.FixTitle);
            Assert.Equal(state, PanelHome.MatrixLine(2, "Matrix 2", Contract.FlagBoxRests[0], false).Text);
            Assert.EndsWith(PanelHome.Separator + state, PanelHome.MatrixLine(2, "Left pillar", Contract.FlagBoxRests[0], false).Text);
            Assert.Contains("Ui.FixBox(PanelMatrix.FixTitle, null, PanelMatrix.FixSteps(m, FlagBoxName()), check);", Source("SettingsControl.Matrix.cs"));
            Assert.Contains(PanelMatrix.FixSteps(2, "OpenDash Flag box"), step => step.Length == 1 && step[0].EndsWith(": 2", System.StringComparison.Ordinal));

            var input = new AttentionInput();
            input.Matrices.Add(new AttentionMatrix { Slot = 2, Name = "Left pillar", Shown = false });
            var issue = PanelAttention.Find(input).Single();
            Assert.Equal("Left pillar is not shown in SimHub", issue.Title);
            Assert.Contains("matrix 2", issue.Detail);
        }

        /// <summary>Ruling 12: the Update press over an older profile says what it costs in one sentence,
        /// FlagBoxInstallPlan.Replaces, on the LEDs strip header, the Matrix flag box and the Updates table.</summary>
        [Fact]
        public void The_update_press_says_what_it_replaces_on_every_page()
        {
            Assert.Equal(FlagBoxInstallPlan.Replaces, PanelLeds.UpdateTooltip);
            Assert.Equal(FlagBoxInstallPlan.Replaces, PanelMatrix.ProfileTooltip(FlagBoxInstallState.Outdated, FlagBoxInstallState.Outdated));
            Assert.Equal(FlagBoxInstallPlan.Replaces, PanelUpdates.RowUpdateTooltip);
            Assert.Contains("install.ToolTip = action == PanelLeds.UpdateProfile ? PanelLeds.UpdateTooltip : PanelLeds.InstallTooltip;", Source("SettingsControl.Lights.cs"));
        }

        /// <summary>plugin.md's departure notes describe the build that is drawn: the Matrix layer is "Warnings",
        /// the inherited words the rebuild already changed are not listed as still to change, the artboard's
        /// "Width" is placed under Rev lights where the build draws it, and the dead constants that kept the stale
        /// words in the source are gone.</summary>
        [Fact]
        public void Plugin_md_describes_the_words_the_build_draws()
        {
            var doc = System.Text.RegularExpressions.Regex.Replace(
                System.IO.File.ReadAllText(System.IO.Path.Combine(RepoPaths.Root(), "docs", "design", "plugin.md")), @"\s+", " ");
            Assert.Equal("Warnings", PanelMatrix.WarningsTitle);
            Assert.DoesNotContain("\"Car warnings\" on a Matrix", doc);
            Assert.Contains("\"Warnings\" on Rig (its scenario group) and on a Matrix panel", doc);
            foreach (var changed in new[] { "\"Fill the strip | True size\"", "\"Add a matrix panel\" for", "\"Revbar\" for", "\"LED device\" for" })
            {
                Assert.DoesNotContain(changed, doc);
            }
            Assert.Equal(new[] { "Stretch to fit", "Actual size" }, PanelLights.MirrorFitLabels);
            Assert.Equal("SimHub device", PanelLights.BarDeviceTitle);
            Assert.Equal("Add a matrix", PanelMatrix.AddPanel);
            Assert.Equal("Rev bar", PanelScreens.RevBarTitle);
            Assert.Contains("| LEDs: \"Width\", under \"Rev lights\" | \"" + PanelLeds.MirrorFitTitle + "\" |", doc);
            Assert.Null(typeof(PanelDataTab).GetField("RevBarTitle"));
            Assert.Null(typeof(PanelDataTab).GetField("RevBarCaption"));
        }

        /// <summary>Ruling 3: a strip is the device and an LED profile is the file SimHub loads. The empty build is
        /// said in one sentence, the Updates table's, on the LEDs page's Add sheet too; the device's words name a
        /// strip; and no constant a page draws calls the file a "strip profile".</summary>
        [Fact]
        public void A_strip_is_the_device_and_an_LED_profile_is_the_file_on_every_page()
        {
            // One sentence for the empty build, naming the file as Updates names its rows' kind.
            Assert.Contains("Ui.Prose(PanelLightRows.NoProfiles)", Source("SettingsControl.Lights.cs"));
            Assert.Null(typeof(PanelLeds).GetField("NoProfiles"));
            Assert.Contains(PanelUpdates.StripKind, PanelLightRows.NoProfiles);
            Assert.Equal("LED profile", PanelUpdates.StripKind);
            // The device is a strip.
            foreach (var device in new[] { PanelLights.AddBar, PanelLeds.NoStrips, PanelLights.BarsTitle })
            {
                Assert.Contains("strip", device, System.StringComparison.OrdinalIgnoreCase);
            }
            // No constant pairs the device's noun with the file's.
            var pairing = new System.Text.RegularExpressions.Regex(@"\bstrip profiles?\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var offenders = typeof(PanelSearch).Assembly.GetTypes()
                .Where(type => type.Namespace == "OpenDashPlugin" && type.IsAbstract && type.IsSealed && type.Name.StartsWith("Panel", System.StringComparison.Ordinal))
                .SelectMany(type => type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    .Where(field => field.FieldType == typeof(string) && (field.IsLiteral || field.IsInitOnly))
                    .Select(field => new { Name = type.Name + "." + field.Name, Value = (string)field.GetValue(null) }))
                .Where(constant => constant.Value != null && pairing.IsMatch(constant.Value))
                .Select(constant => constant.Name + " = " + constant.Value)
                .ToList();
            Assert.True(offenders.Count == 0, "constants that call the file a strip profile: " + string.Join("; ", offenders));
        }
    }
}
