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
            // Shift points, not thresholds, which is the settings model's word; and "Car-specific", the name
            // voice.md gives the car's own tables.
            Assert.Equal("Car-specific shift points", PanelMatrix.CarShiftPointsTitle);
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

        /// <summary>
        /// The Matrix page's empty state and its header's profile row are its own, so its agent can reword
        /// them without touching what Home or Updates owns: Home reads NoPanels as it reads NoScreens, and
        /// the Updates page's rows read PanelCopy.LightRow.
        /// </summary>
        [Fact]
        public void The_empty_state_and_the_profile_row_are_the_Matrix_pages_own()
        {
            Assert.Equal("No panels yet.", PanelMatrix.NoPanels);
            Assert.Equal("No strips yet.", PanelLeds.NoStrips);
            // Its own table, pinned here and not held to Updates' LightRow, so either page's agent rewords
            // its row without the other's test moving.
            var outdated = PanelMatrix.ProfileRow(FlagBoxInstallState.Outdated, "0.4.0");
            Assert.Equal("Installed · 0.4.0", outdated.State);
            Assert.Equal("Update", outdated.Button);
            Assert.Equal(PanelButton.Primary, outdated.Style);
            Assert.Equal("Reinstall", PanelMatrix.ProfileRow(FlagBoxInstallState.UpToDate, "0.5.0").Button);
            Assert.Equal("Install failed", PanelMatrix.ProfileRow(FlagBoxInstallState.Failed, null).State);
            Assert.Equal(Theme.StatusFailed, PanelMatrix.ProfileRow(FlagBoxInstallState.Failed, null).StateHex);
            foreach (var state in new[] { FlagBoxInstallState.NotInstalled, FlagBoxInstallState.NotEmbedded, FlagBoxInstallState.Unavailable })
            {
                Assert.Equal("Not installed", PanelMatrix.ProfileRow(state, null).State);
                Assert.Equal("Install", PanelMatrix.ProfileRow(state, null).Button);
                Assert.Equal(PanelButton.Outline, PanelMatrix.ProfileRow(state, null).Style);
            }
            var matrix = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Matrix.cs"));
            Assert.Contains("PanelMatrix.ProfileRow(", matrix);
            Assert.DoesNotContain("PanelCopy.LightRow(", matrix);
            var home = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Home.cs"));
            Assert.Contains("PanelLeds.NoStrips", home);
            Assert.Contains("PanelMatrix.NoPanels", home);
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
