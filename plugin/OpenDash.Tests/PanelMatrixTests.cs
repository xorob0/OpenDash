// PanelMatrixTests.cs: the Matrix page's row titles, which the page draws and search lists from the same constants.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelMatrixTests
    {
        [Fact]
        public void The_matrix_rows_say_what_the_artboard_and_the_voice_rulings_say()
        {
            Assert.Equal("Matrix", PanelMatrix.Title);
            Assert.Equal("Spotter bar animation", PanelMatrix.SpotterAnimationTitle);
            Assert.Equal("Idle display", PanelMatrix.IdleDisplayTitle);
            Assert.Equal("Mounting side", PanelMatrix.MountingSideTitle);
            Assert.Equal("Critical flags only", PanelMatrix.CriticalFlagsOnlyTitle);
            Assert.Equal("Shift colours", PanelMatrix.ShiftColoursTitle);
            Assert.Equal("Redline flash", PanelMatrix.RedlineFlashTitle);
            // V44/V46: shift points, not thresholds, which is the settings model's word.
            Assert.Equal("Car-specific shift points", PanelMatrix.CarShiftPointsTitle);
            Assert.DoesNotContain("threshold", PanelMatrix.CarShiftPointsTitle, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Search_lists_the_rows_by_the_constants_the_page_draws()
        {
            var labels = PanelMatrix.Search.Select(entry => entry.Label).ToList();
            foreach (var title in new[]
            {
                PanelMatrix.SpotterAnimationTitle, PanelMatrix.IdleDisplayTitle, PanelMatrix.MountingSideTitle,
                PanelMatrix.CriticalFlagsOnlyTitle, PanelMatrix.ShiftColoursTitle, PanelMatrix.RedlineFlashTitle,
                PanelMatrix.CarShiftPointsTitle, FlagBoxProfile.ProfileName,
            })
            {
                Assert.Contains(title, labels);
            }
        }
    }
}
