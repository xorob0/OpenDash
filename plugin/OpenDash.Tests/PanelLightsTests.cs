// PanelLightsTests.cs: the Lights tab hands its controls two arrays and reads them by index, so the
// labels and the values they name have to stay the same length.
//
// This is the failure the design audit found rather than an invented one: the centre drop-down went on
// offering a label for a value the contract had retired, and nothing said so. A segmented bar is worse
// than silent, since BuildSegmented indexes the labels with the values and throws while the tab is being
// drawn.
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelLightsTests
    {
        [Fact]
        public void Every_value_set_the_tab_draws_has_one_label_per_value()
        {
            Assert.Equal(Contract.LedCentres.Length, PanelLights.CentreLabels.Length);
            Assert.Equal(Contract.LedRpmStyles.Length, PanelLights.RpmStyleLabels.Length);
            Assert.Equal(Contract.LedMirrorFits.Length, PanelLights.MirrorFitLabels.Length);
            Assert.Equal(Contract.FlagBoxRests.Length, PanelLights.RestLabels.Length);
            Assert.Equal(Contract.FlagBoxSides.Length, PanelLights.SideLabels.Length);
        }

        [Fact]
        public void No_label_is_blank()
        {
            var every = PanelLights.CentreLabels
                .Concat(PanelLights.RpmStyleLabels)
                .Concat(PanelLights.MirrorFitLabels)
                .Concat(PanelLights.RestLabels)
                .Concat(PanelLights.SideLabels);
            Assert.All(every, label => Assert.False(string.IsNullOrWhiteSpace(label)));
        }

        /// <summary>
        /// The retired fifth centre by name, because it is the one a label could quietly come back for:
        /// Contract keeps it so that a settings file carrying it can be migrated, and a reader who sees
        /// the name there could take it for a value the panel is owed a row of.
        /// </summary>
        [Fact]
        public void The_retired_centre_is_not_offered()
        {
            Assert.DoesNotContain(Contract.RetiredLedCentre, Contract.LedCentres);
            Assert.DoesNotContain("RPM only", PanelLights.CentreLabels);
        }

        /// <summary>
        /// The two numbers a driver is asked for, and the line that checks them against the strip they
        /// are holding.
        /// </summary>
        /// <remarks>
        /// A list of sixty-three geometries asked somebody to find "3/9/3" among every other one and to
        /// know that is what their wheel is called. Two numbers is what they can count.
        /// </remarks>
        [Fact]
        public void The_shape_note_says_what_the_two_numbers_add_up_to()
        {
            Assert.Equal("15 LEDs in all, as 3/9/3.", PanelLights.BarShapeNote(3, 9));
            Assert.Equal("15 LEDs in all, as 0/15/0.", PanelLights.BarShapeNote(0, 15));
            Assert.Equal("3-9-3", PanelLights.BarShapeId(3, 9));
            Assert.Equal("0-15-0", PanelLights.BarShapeId(0, 15));
            // The one thing the two captions have to get the right way round, said in a driver's words
            // rather than the generator's ("lamps", "the rev ladder"); docs/design/voice.md is the rule.
            Assert.Contains("Flags", PanelLights.BarEndsCaption);
            Assert.Contains("Count your LEDs", PanelLights.BarCentreCaption);
        }
    }
}
