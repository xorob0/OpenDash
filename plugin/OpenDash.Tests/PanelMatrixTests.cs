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
            // Shift points, not thresholds, which is the settings model's word; and "Car's own", the
            // qualifier the LEDs page's #369 switch gives the same tables.
            Assert.Equal("Car-specific shift points", PanelMatrix.CarShiftPointsTitle);
            Assert.StartsWith("Car's own", PanelLeds.CarRevLightsTitle, StringComparison.Ordinal);
            Assert.DoesNotContain("threshold", PanelMatrix.CarShiftPointsTitle, StringComparison.OrdinalIgnoreCase);
            // Nothing about a fallback the row cannot name, and no "table", which is internal vocabulary.
            Assert.Equal("Colours change where this car's own lights do.", PanelMatrix.CarShiftPointsCaption);
            Assert.Equal("The flag box", PanelMatrix.FlagBoxTitle);
            Assert.Equal("The bar slides in from the edge.", PanelMatrix.SpotterAnimationCaption);
            Assert.Equal("Race flags", PanelMatrix.RaceFlagsTitle);
            Assert.Equal("Pit status", PanelMatrix.PitStatusTitle);
            Assert.Equal("Limiter, pit lane and speeding.", PanelMatrix.PitStatusCaption);
            Assert.Equal("Spotter", PanelMatrix.SpotterTitle);
            Assert.Equal("Warns about cars alongside.", PanelMatrix.SpotterCaption);
            Assert.Equal("Car warnings", PanelMatrix.CarWarningsTitle);
            Assert.Equal("Low fuel, oil and water.", PanelMatrix.CarWarningsCaption);
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

        /// <summary>The page's anchor ids, which search, Home's fix rows and the capture scripts route to: a
        /// renamed one sends each of them to the page's top, so every id is pinned, and a new one is added here.</summary>
        [Fact]
        public void Its_anchor_ids_are_pinned()
        {
            Assert.Equal(new[]
            {
                "AnchorPanels = matrix.panels",
                "AnchorProfile = matrix.profile",
                "AnchorSpotterAnimation = matrix.spotter-animation",
            }, AnchorTable.Of(typeof(PanelMatrix)));
        }
    }
}
