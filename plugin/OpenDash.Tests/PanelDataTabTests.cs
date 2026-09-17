// PanelDataTabTests.cs: the Data tab's copy and its one spacing of its own.
//
// The Position row carries a sentence the canvas does not, which is a decision and not an oversight; a
// later reader comparing the tab against the canvas would otherwise delete it as a difference. Pinning it
// here is what makes the deletion fail rather than pass quietly.
using System;
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
                "A lap time means the same thing on the rim as it does on the pit wall, so these are not per screen.",
                PanelDataTab.SectionCaption);
        }

        [Fact]
        public void The_rows_sit_wider_apart_than_a_section_elsewhere_on_the_panel()
        {
            Assert.Equal(22, PanelDataTab.RowGap);
            Assert.NotEqual(PanelMetrics.SectionGap, PanelDataTab.RowGap);
        }

        [Fact]
        public void The_position_row_keeps_the_sentence_the_canvas_does_not_carry()
        {
            Assert.StartsWith("Overall, or within your class.", PanelDataTab.PositionCaption, StringComparison.Ordinal);
            Assert.Contains(
                "A zone can still be set to list your own class on its own.",
                PanelDataTab.PositionCaption,
                StringComparison.Ordinal);
        }
    }
}
