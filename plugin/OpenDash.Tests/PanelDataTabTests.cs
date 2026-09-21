// PanelDataTabTests.cs: the Data tab's copy and its one spacing of its own.
//
// The Position row carries a sentence the canvas does not, which is a decision and not an oversight; a
// later reader comparing the tab against the canvas would otherwise delete it as a difference. Pinning it
// here is what makes the deletion fail rather than pass quietly.
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelDataTabTests
    {
        [Fact]
        public void The_section_says_what_the_canvas_says()
        {
            Assert.Equal("These apply to every screen", PanelDataTab.SectionTitle);
            Assert.Equal(
                "Change one of these and every screen on your rig follows.",
                PanelDataTab.SectionCaption);
        }

        [Fact]
        public void The_rows_sit_wider_apart_than_a_section_elsewhere_on_the_panel()
        {
            Assert.Equal(22, PanelDataTab.RowGap);
            Assert.NotEqual(PanelMetrics.SectionGap, PanelDataTab.RowGap);
        }

        /// <remarks>
        /// Two settings have shipped declared, mirrored and attached with no way to reach them: the
        /// flag format, which made a whole format unreachable, and the blue flag detail. Both were
        /// found by reading the code rather than by a red suite, because declaring a property and
        /// drawing a control for it are two edits and nothing held them together. This is the thing
        /// that holds them together.
        /// </remarks>
        [Fact]
        public void Every_rig_setting_can_be_reached_from_the_panel()
        {
            var sources = string.Concat(RepoPaths.SettingsControlSources().Select(File.ReadAllText));
            var unreachable = Contract.SharedPropertyNames()
                // The twelve slots are the card face's own and sit on that screen's pane, which writes
                // them through Contract.SlotProperty rather than by name. ShiftLights has no control
                // because RevBar supersedes it and SetRevBar writes both; the two cannot disagree.
                .Where(name => !name.StartsWith("Slot", StringComparison.Ordinal) && name != Contract.ShiftLights)
                .Where(name => !sources.Contains("Settings." + name) && !sources.Contains("Set" + name))
                .ToArray();
            Assert.Equal(Array.Empty<string>(), unreachable);
        }

        [Fact]
        public void The_blue_flag_row_offers_the_three_the_contract_declares()
        {
            Assert.Equal(new[] { "none", "class", "positionClass" }, Contract.BlueFlagDetails);
            Assert.Equal("Blue flag detail", PanelDataTab.BlueFlagTitle);
            Assert.Contains("the car behind", PanelDataTab.BlueFlagCaption, StringComparison.Ordinal);
        }

        [Fact]
        public void The_position_row_keeps_the_sentence_the_canvas_does_not_carry()
        {
            Assert.StartsWith("Overall, or within your class.", PanelDataTab.PositionCaption, StringComparison.Ordinal);
            Assert.Contains(
                "Individual zones can still be set to your class on their own.",
                PanelDataTab.PositionCaption,
                StringComparison.Ordinal);
        }
    }
}
