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

        /// <summary>
        /// The two name boxes look identical and answer different questions, so both have to say which.
        /// </summary>
        /// <remarks>
        /// Reported from the rig: a panel was added, named, and then looked for on SimHub's Arduino page,
        /// where the matrix list held an older OpenDash profile and nothing carrying the name just typed.
        /// Nothing was broken -- a bar is a profile and a panel is a content number inside the one flag
        /// box profile -- and nothing in the panel said so.
        /// </remarks>
        [Fact]
        public void A_bar_name_reaches_SimHub_and_a_panel_name_does_not_and_both_say_so()
        {
            Assert.Contains("SimHub", PanelLights.BarNameCaption);
            Assert.Contains("not shown in SimHub", PanelLights.PanelNameCaption);
        }

        /// <summary>The add screen and the line after the press both name the profile a driver selects on
        /// the device, since the panel's own name is not it.</summary>
        [Fact]
        public void Adding_a_panel_names_the_profile_that_paints_it()
        {
            var caption = PanelLights.AddPanelCaption(2, "OpenDash Flag box");
            Assert.Contains("SimHub matrix 2", caption);
            Assert.Contains("OpenDash Flag box", caption);

            var installed = PanelLights.PanelAdded("by the wheel", 2, "OpenDash Flag box", FlagBoxInstallState.UpToDate);
            Assert.Contains("by the wheel", installed);
            Assert.Contains("SimHub matrix 2", installed);
            Assert.Contains("OpenDash Flag box", installed);
            // The clause that answers the report: the list carries the profile's name, not the panel's.
            Assert.Contains("rather than yours", installed);
            Assert.DoesNotContain("Install tab", installed);

            Assert.Equal(
                "An 8x8 LED matrix. \"OpenDash Flag box\" is the one profile that paints every panel below;"
                    + " install it from the Install tab.",
                PanelLights.BoxCaption("OpenDash Flag box"));
        }

        /// <summary>
        /// A panel added on a rig where SimHub has no profile of ours is sent to the Install tab, and
        /// every state that is not a profile in SimHub says the same thing.
        /// </summary>
        /// <remarks>
        /// Outdated counts as installed: an old copy paints the box, so the driver is told to select it
        /// rather than told SimHub has nothing. The Install tab's own row is what offers the update.
        /// </remarks>
        [Theory]
        [InlineData(FlagBoxInstallState.NotInstalled)]
        [InlineData(FlagBoxInstallState.Unavailable)]
        [InlineData(FlagBoxInstallState.NotEmbedded)]
        [InlineData(FlagBoxInstallState.Failed)]
        public void A_panel_added_without_the_profile_is_sent_to_the_Install_tab(FlagBoxInstallState state)
        {
            Assert.True(PanelLights.PanelNeedsInstall(state));
            Assert.Contains("Install tab", PanelLights.PanelAdded("Matrix 1", 1, "OpenDash Flag box", state));
        }

        [Theory]
        [InlineData(FlagBoxInstallState.UpToDate)]
        [InlineData(FlagBoxInstallState.Outdated)]
        public void A_panel_added_beside_a_profile_SimHub_holds_is_told_to_select_it(FlagBoxInstallState state)
        {
            Assert.False(PanelLights.PanelNeedsInstall(state));
            Assert.Contains("select", PanelLights.PanelAdded("Matrix 1", 1, "OpenDash Flag box", state));
        }
    }
}
