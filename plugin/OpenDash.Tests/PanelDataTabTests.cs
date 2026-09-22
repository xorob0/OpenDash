// PanelDataTabTests.cs: the Data tab's copy and its one spacing of its own.
//
// The Position row carries two sentences the canvas does not, which is a decision and not an oversight; a
// later reader comparing the tab against the canvas would otherwise delete them as a difference. Pinning
// them here is what makes the deletion fail rather than pass quietly.
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
            // The heading is the whole of what the section has to say, so it carries no caption
            // restating it; docs/design/voice.md is the rule.
            Assert.Equal("These apply to every screen", PanelDataTab.SectionTitle);
            Assert.Null(PanelDataTab.SectionCaption);
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
            // The three values are the control's to show; the caption says only what the row is about.
            Assert.Equal("What shows next to a blue flag.", PanelDataTab.BlueFlagCaption);
        }

        [Fact]
        public void The_position_row_keeps_the_sentences_the_canvas_does_not_carry()
        {
            // The canvas's own sentence, "Overall, or within your class", is what the segmented control
            // beside the row already says in two words. What the row keeps is what the canvas does not
            // carry and the control cannot show: that Class filters the lists as well as numbering them
            // since #212, and that a zone can ask for the same filter without the rig doing so. The
            // row used to promise an override instead, which is the direction the two settings do not
            // run in; pinning the words here is what makes a quiet return to that fail.
            Assert.Equal("Class also shows only your own class in lists. A zone can ask for that on its own.", PanelDataTab.PositionCaption);
            Assert.DoesNotContain("override", PanelDataTab.PositionCaption);
        }
    }
}
