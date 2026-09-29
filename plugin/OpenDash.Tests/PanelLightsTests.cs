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
            Assert.Equal(Contract.LedMirrorFits.Length, PanelLights.MirrorFitLabels.Length);
            Assert.Equal(Contract.FlagBoxRests.Length, PanelLights.RestLabels.Length);
            Assert.Equal(Contract.FlagBoxSides.Length, PanelLights.SideLabels.Length);
        }

        [Fact]
        public void No_label_is_blank()
        {
            var every = PanelLights.CentreLabels
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
            Assert.DoesNotContain("Matrix page", installed);

            // The tabs went with #503: the profile is installed from the Matrix page's own header.
            Assert.Equal(
                "An 8x8 LED matrix. \"OpenDash Flag box\" is the one profile that paints every panel below;"
                    + " install it at the top of the Matrix page.",
                PanelLights.BoxCaption("OpenDash Flag box"));
        }

        /// <summary>
        /// A panel added on a rig where SimHub has no profile of ours is sent to the Matrix page's header,
        /// and every state that is not a profile in SimHub says the same thing.
        /// </summary>
        /// <remarks>
        /// Outdated counts as installed: an old copy paints the box, so the driver is told to select it
        /// rather than told SimHub has nothing. The Matrix page's header is what offers the update.
        /// </remarks>
        [Theory]
        [InlineData(FlagBoxInstallState.NotInstalled)]
        [InlineData(FlagBoxInstallState.Unavailable)]
        [InlineData(FlagBoxInstallState.NotEmbedded)]
        [InlineData(FlagBoxInstallState.Failed)]
        public void A_panel_added_without_the_profile_is_sent_to_the_Matrix_page(FlagBoxInstallState state)
        {
            Assert.True(PanelLights.PanelNeedsInstall(state));
            Assert.Contains("install it at the top of the Matrix page.", PanelLights.PanelAdded("Matrix 1", 1, "OpenDash Flag box", state));
        }

        /// <summary>A strip whose profile could not be installed is sent to Updates, which lists what
        /// OpenDash has written into SimHub; the tab it used to name is gone (#503).</summary>
        [Fact]
        public void A_strip_whose_profile_failed_is_sent_to_Updates()
        {
            Assert.Equal("Added Rim, but its profile could not be installed. See Updates.", PanelLights.BarAddFailed("Rim"));
        }

        [Theory]
        [InlineData(FlagBoxInstallState.UpToDate)]
        [InlineData(FlagBoxInstallState.Outdated)]
        public void A_panel_added_beside_a_profile_SimHub_holds_is_told_to_select_it(FlagBoxInstallState state)
        {
            Assert.False(PanelLights.PanelNeedsInstall(state));
            Assert.Contains("select", PanelLights.PanelAdded("Matrix 1", 1, "OpenDash Flag box", state));
        }

        /// <summary>
        /// A device SimHub has and OpenDash did not offer is named beside the picker, in every shape the
        /// row takes.
        /// </summary>
        /// <remarks>
        /// #437 was a wheel in SimHub's Devices view and absent from the picker with nothing said, and a
        /// row reading "No LED device in SimHub" while the wheel sits in SimHub's list is worse than
        /// nothing. The sentence is one sentence whatever it follows, which docs/design/voice.md holds
        /// a caption to.
        /// </remarks>
        [Fact]
        public void A_device_passed_over_is_named_beside_the_picker()
        {
            Assert.Null(PanelLights.NotOffered(null));
            Assert.Null(PanelLights.NotOffered(new string[0]));
            Assert.Equal("Rim has no LEDs OpenDash can reach; see SimHub's log.", PanelLights.NotOffered(new[] { "Rim" }));
            Assert.Equal("Rim and Formula rim have no LEDs OpenDash can reach; see SimHub's log.",
                PanelLights.NotOffered(new[] { "Rim", "Formula rim" }));
            Assert.Equal("Rim, Formula rim, Hub and 2 others have no LEDs OpenDash can reach; see SimHub's log.",
                PanelLights.NotOffered(new[] { "Rim", "Formula rim", "Hub", "Button box", "GT rim" }));
            Assert.Equal("Rim, Formula rim, Hub and 1 other have no LEDs OpenDash can reach; see SimHub's log.",
                PanelLights.NotOffered(new[] { "Rim", "Formula rim", "Hub", "Button box" }));

            // Nothing passed over: the row reads exactly as it did.
            Assert.Equal(PanelLights.NoDevices, PanelLights.DeviceRowCaption(0, null, new string[0]));
            Assert.Equal("Goes to Rim.", PanelLights.DeviceRowCaption(1, "Goes to Rim.", null));
            Assert.Null(PanelLights.DeviceRowCaption(2, PanelLights.BarDeviceCaption, new string[0]));

            // Something passed over and nothing offered: the device is named in place of "No LED device".
            var none = PanelLights.DeviceRowCaption(0, null, new[] { "Rim" });
            Assert.Equal("Rim has no LEDs OpenDash can reach; see SimHub's log.", none);
            Assert.DoesNotContain(PanelLights.NoDevices, none);

            // Something offered as well: the name follows what the row said, or stands alone.
            Assert.Equal("Goes to Arduino RGB LEDs. Rim has no LEDs OpenDash can reach; see SimHub's log.",
                PanelLights.DeviceRowCaption(1, "Goes to Arduino RGB LEDs.", new[] { "Rim" }));
            Assert.Equal("Rim has no LEDs OpenDash can reach; see SimHub's log.",
                PanelLights.DeviceRowCaption(2, PanelLights.BarDeviceCaption, new[] { "Rim" }));
        }
    }
}
