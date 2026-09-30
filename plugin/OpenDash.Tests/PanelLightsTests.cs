// PanelLightsTests.cs: the LEDs page hands its controls two arrays and reads them by index, so the
// labels and the values they name have to stay the same length; and the strip words the page draws.
//
// This is the failure the design audit found rather than an invented one: the centre drop-down went on
// offering a label for a value the contract had retired, and nothing said so. A segmented bar is worse
// than silent, since BuildSegmented indexes the labels with the values and throws while the page is being
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
            // The shape as the cards write it; a bare run is its total alone, never the id's "0/15/0".
            Assert.Equal("15 LEDs in all, as 3 · 9 · 3.", PanelLights.BarShapeNote(3, 9));
            Assert.Equal("15 LEDs in all.", PanelLights.BarShapeNote(0, 15));
            Assert.Equal("1 LED in all.", PanelLights.BarShapeNote(0, 1));
            Assert.Equal("3-9-3", PanelLights.BarShapeId(3, 9));
            Assert.Equal("0-15-0", PanelLights.BarShapeId(0, 15));
            // The two rows of the sheet's shape step, in the page's word for the middle: "centre", as Centre
            // display says it.
            Assert.Equal("LEDs at each end", PanelLights.BarEndsTitle);
            Assert.Equal("LEDs in the centre", PanelLights.BarCentreTitle);
            // The two captions, in a driver's words rather than the generator's ("lamps", "the rev ladder"), as
            // docs/design/voice.md has them. Not drawn, as the artboard draws none; pinned as the reasons the
            // choices are what they are.
            Assert.Equal("Flags, warnings and cars alongside. Pick None for one continuous run.", PanelLights.BarEndsCaption);
            Assert.Equal("Count your LEDs and subtract the ends.", PanelLights.BarCentreCaption);
            var leds = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.DoesNotContain("PanelLights.BarEndsCaption", leds);
            Assert.DoesNotContain("PanelLights.BarCentreCaption", leds);
        }

        /// <summary>The strip words the LEDs page draws, as the artboards and docs/design/voice.md have them.</summary>
        [Fact]
        public void The_strip_words_are_the_pages()
        {
            // voice.md's own example beside "No strips yet", departing from the artboard's "Add LEDs", which
            // docs/design/plugin.md has yet to record.
            Assert.Equal("Add an LED strip", PanelLights.AddBar);
            // The Centre display chooser, in Contract.LedCentres' order.
            Assert.Equal(new[] { "RPM", "Brake", "Throttle and brake", "Fuel" }, PanelLights.CentreLabels);
            Assert.Equal(new[] { "rpm", "brake", "throttleBrake", "fuel" }, Contract.LedCentres);
            // The Rename sheet's caption: the fact nothing on the panel shows, that the name is SimHub's too.
            Assert.Equal("Also shown in SimHub's LED profile list.", PanelLights.BarNameCaption);
            Assert.Equal("Your LED strips", PanelLights.BarsTitle);
            // SimHub's word for it, since SimHub's Devices list is where the driver finds it.
            Assert.Equal("SimHub device", PanelLights.BarDeviceTitle);
            Assert.Equal(new[] { "Stretch to fit", "Actual size" }, PanelLights.MirrorFitLabels);
            // The source by its own name, which is how a driver who met it at Lovely Sim Racing knows it.
            Assert.Equal("Lovely Car Data", PanelLights.CarTablesTitle);
            Assert.Equal("Downloads Lovely Car Data.", PanelLights.CarTablesButtonTooltip);
            Assert.Equal("Not downloaded yet.", PanelLights.CarTablesNone);
            Assert.Equal("Downloading…", PanelLights.CarTablesDownloading);
            // A chooser's value names what the code knows: SimHub does not list the device.
            Assert.Equal("Device not in SimHub", PanelLights.DeviceGone);
            Assert.Equal(" (not connected)", PanelLights.DeviceOffline);
            Assert.Equal("Goes to Arduino RGB LEDs.", PanelLights.OneDevice("Arduino RGB LEDs"));
            Assert.Equal("No SimHub device has LEDs. Add your wheel or Arduino in SimHub first.", PanelLights.NoDevices);
            // No word of the old tabs survives in the words the page draws.
            foreach (var words in new[] { PanelLights.CarTablesCaption, PanelLights.BarAddFailed("Rim"), PanelLights.BarNameCaption, PanelLights.RenameBarTooltip })
            {
                Assert.DoesNotContain(" tab", words);
            }
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

            // The tabs went with #503: the profile is installed from its own row, directly above this caption
            // on the Matrix page, so the caption says nothing of where.
            // And the profile row above it already says "8 × 8 matrix", so the caption does not.
            Assert.Equal(
                "\"OpenDash Flag box\" is the one profile that paints every panel below.",
                PanelLights.BoxCaption("OpenDash Flag box"));
            Assert.Equal("8 × 8 matrix", PanelLightRows.FlagBoxCaption);
        }

        /// <summary>
        /// A panel added on a rig where SimHub has no profile of ours is sent to what the top of the page it
        /// was added on, the Matrix page, can do in that state: its Install when SimHub lacks the profile or
        /// installing failed, the copy to import by hand when SimHub's matrix settings could not be reached
        /// and Install is disabled, and nowhere when this build has no profile, where the page has nothing to
        /// install.
        /// </summary>
        /// <remarks>
        /// Outdated counts as installed: an old copy paints the box, so the driver is told to select it
        /// rather than told SimHub has nothing. The Matrix page's header is what offers the update.
        /// </remarks>
        [Theory]
        [InlineData(FlagBoxInstallState.NotInstalled, "and SimHub has not got it: install it at the top of this page.")]
        [InlineData(FlagBoxInstallState.Failed, "and SimHub has not got it: install it at the top of this page.")]
        [InlineData(FlagBoxInstallState.Unavailable, "and SimHub's matrix settings could not be reached: import it by hand from the top of this page.")]
        [InlineData(FlagBoxInstallState.NotEmbedded, "and this build has none to install, so the panel stays dark.")]
        public void A_panel_added_without_the_profile_is_sent_where_the_Matrix_page_can_help(FlagBoxInstallState state, string tail)
        {
            Assert.True(PanelLights.PanelNeedsInstall(state));
            var said = PanelLights.PanelAdded("Matrix 1", 1, "OpenDash Flag box", state);
            Assert.EndsWith(". \"OpenDash Flag box\" is the profile that paints it, " + tail, said);
            Assert.DoesNotContain("Matrix page", said);
        }

        /// <summary>A strip whose profile could not be installed is sent to SimHub's log (voice.md's failure
        /// form): Updates offers no press for a profile that is not in SimHub, so "See the Updates page" led
        /// nowhere. A failed move says "move", not "Added". Rename installs the profile again only where SimHub
        /// holds it, so its hover promises only the rename, and its sheet promises no "Install the strip
        /// again".</summary>
        [Fact]
        public void A_strip_whose_profile_failed_is_sent_to_the_log()
        {
            Assert.Equal("Added Rim, but its profile could not be installed. See SimHub's log.", PanelLights.BarAddFailed("Rim"));
            Assert.Equal("Could not move Rim's profile. See SimHub's log.", PanelLights.BarMoveFailed("Rim"));
            // Rename reinstalls only where SimHub holds the profile, so the hover promises only the rename.
            Assert.Equal("Renames this strip.", PanelLights.RenameBarTooltip);
            // Nor does the Rename sheet one click later: its footer has no note, since saving reinstalls.
            var leds = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            var rename = leds.Substring(leds.IndexOf("ShowSheet(\"Rename \"", System.StringComparison.Ordinal));
            Assert.StartsWith("SheetFooter(null, cancel, save)", rename.Substring(rename.IndexOf("SheetFooter(", System.StringComparison.Ordinal)));
            Assert.DoesNotContain("Install the strip again", leds);
        }

        /// <summary>A strip added is told the steps OpenDash does not take, in the order they are done, as
        /// voice.md's own example has them: the restart its own settings wait on (the plugin publishes them only
        /// for the strips it held at start), then the select on the device it went to. A note the install
        /// returned comes first, since nothing is listed to select until it is done.</summary>
        [Fact]
        public void A_strip_added_says_where_to_select_it_and_that_its_settings_wait_for_a_restart()
        {
            Assert.Equal("Added Rim. Restart SimHub, then select \"Rim\" on Fanatec CSL Elite.",
                PanelLights.BarAdded("Rim", "Fanatec CSL Elite"));
            Assert.Equal("Added Rim. Restart SimHub, then select \"Rim\" in SimHub.",
                PanelLights.BarAdded("Rim", null));
            Assert.Equal("Added Rim. " + FlagBoxInstallPlan.BuiltInModeNote + " Restart SimHub, then select \"Rim\" on Wheel.",
                PanelLights.BarAdded("Rim", "Wheel", FlagBoxInstallPlan.BuiltInModeNote));
        }

        /// <summary>
        /// The status line is CarLightService's status, and the stale note after it as a sentence of its own
        /// where the copy is over a week old (ruling 51). The page works the note out as it draws the row, since
        /// the status's own age is written only when the tables are read.
        /// </summary>
        [Fact]
        public void The_stale_note_follows_the_status_as_a_sentence_of_its_own()
        {
            Assert.Equal("Over a week old. Press Update for a newer copy.", PanelLights.CarTablesStale);
            Assert.Equal("84 cars, updated 9 days ago", PanelLights.CarTablesLine("84 cars, updated 9 days ago", false));
            Assert.Equal("84 cars, updated 9 days ago. Over a week old. Press Update for a newer copy.",
                PanelLights.CarTablesLine("84 cars, updated 9 days ago", true));
            // Frozen at "just now" by a SimHub left running a week: the note is what says so.
            Assert.Equal("84 cars, updated just now. Over a week old. Press Update for a newer copy.",
                PanelLights.CarTablesLine("84 cars, updated just now", true));
            // Tables with no fetch stamp give no age at all, and are stale by CarLightLibrary.IsStale.
            Assert.Equal("84 cars. Over a week old. Press Update for a newer copy.", PanelLights.CarTablesLine("84 cars", true));
            Assert.Equal("84 cars, updated 9 days ago (last download failed: timeout). Over a week old. Press Update for a newer copy.",
                PanelLights.CarTablesLine("84 cars, updated 9 days ago (last download failed: timeout) ", true));
            Assert.Equal("Not downloaded yet.", PanelLights.CarTablesLine("Not downloaded yet.", false));
            Assert.Equal(PanelLights.CarTablesStale, PanelLights.CarTablesLine(null, true));
            Assert.Equal(string.Empty, PanelLights.CarTablesLine(null, false));
            Assert.Equal("Update", PanelLights.CarTablesButton(84));
            Assert.Equal("Download", PanelLights.CarTablesButton(0));
            // The page asks the service as it draws the row, with the clock of the moment.
            var leds = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.Contains("PanelLights.CarTablesLine(status, service.Stale(DateTime.UtcNow))", leds);
            // The one row that fetches the tables names both things that read them.
            Assert.Equal("Needed for the car's own rev lights and car-specific shift points. Every car is downloaded at once, about 400 KB, so your car is never disclosed.",
                PanelLights.CarTablesCaption);
        }

        /// <summary>
        /// CarLightService's lowercase states, which name the tables by the retired noun and one of which carries
        /// an exception's message, are said in the row's words, and a failure points at the log the page writes.
        /// </summary>
        [Fact]
        public void The_services_own_states_are_said_in_the_rows_words()
        {
            Assert.Equal("Loading…", PanelLights.CarTablesLoading);
            Assert.Equal("Could not read Lovely Car Data. See SimHub's log.", PanelLights.CarTablesUnreadable);
            Assert.Equal(PanelLights.CarTablesLoading, PanelLights.CarTablesLine(new CarLightService(null, System.IO.Path.GetTempPath()).Status, false));
            Assert.Equal(PanelLights.CarTablesUnreadable, PanelLights.CarTablesLine("could not read the car light tables: Access to the path is denied.", true));
            Assert.Equal(PanelLights.CarTablesUnreadable, PanelLights.CarTablesLine("the car light tables could not be loaded", false));
            Assert.True(PanelLights.CarTablesUnread("could not read the car light tables: boom"));
            Assert.False(PanelLights.CarTablesUnread("84 cars"));
            Assert.False(PanelLights.CarTablesUnread(null));
            // The words matched are CarLightService's own: a reword there has to move them here.
            var service = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "CarLightService.cs"));
            Assert.Contains("status = \"" + PanelLights.ServiceNotLoaded + "\";", service);
            Assert.Contains("status = \"" + PanelLights.ServiceUnreadPrefix + ": \" + e.Message;", service);
            Assert.Contains("status = \"" + PanelLights.ServiceUnloaded + "\";", service);
            // The reason is written to the log, once for each failure, so the line has something behind it.
            var leds = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.Contains("Log.Warn(\"Lovely Car Data could not be read: \" + status);", leds);
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
            Assert.Equal("Rim has no LEDs OpenDash can reach. See SimHub's log.", PanelLights.NotOffered(new[] { "Rim" }));
            Assert.Equal("Rim and Formula rim have no LEDs OpenDash can reach. See SimHub's log.",
                PanelLights.NotOffered(new[] { "Rim", "Formula rim" }));
            Assert.Equal("Rim, Formula rim, Hub and 2 others have no LEDs OpenDash can reach. See SimHub's log.",
                PanelLights.NotOffered(new[] { "Rim", "Formula rim", "Hub", "Button box", "GT rim" }));
            Assert.Equal("Rim, Formula rim, Hub and 1 other have no LEDs OpenDash can reach. See SimHub's log.",
                PanelLights.NotOffered(new[] { "Rim", "Formula rim", "Hub", "Button box" }));

            // Nothing passed over: the row reads exactly as it did.
            Assert.Equal(PanelLights.NoDevices, PanelLights.DeviceRowCaption(0, null, new string[0]));
            Assert.Equal("Goes to Rim.", PanelLights.DeviceRowCaption(1, "Goes to Rim.", null));
            Assert.Null(PanelLights.DeviceRowCaption(2, PanelLights.BarDeviceCaption, new string[0]));

            // Something passed over and nothing offered: the device is named in place of "No LED device".
            var none = PanelLights.DeviceRowCaption(0, null, new[] { "Rim" });
            Assert.Equal("Rim has no LEDs OpenDash can reach. See SimHub's log.", none);
            Assert.DoesNotContain(PanelLights.NoDevices, none);

            // Something offered as well: one device and the ones passed over are one sentence, so the caption
            // stays at voice.md's ceiling of two; beside a picker the name stands alone.
            Assert.Equal("Goes to Arduino RGB LEDs, not Rim, which has no LEDs OpenDash can reach. See SimHub's log.",
                PanelLights.DeviceRowCaption(1, "Goes to Arduino RGB LEDs.", new[] { "Rim" }));
            Assert.Equal("Goes to Arduino RGB LEDs, not Rim and Hub, which have no LEDs OpenDash can reach. See SimHub's log.",
                PanelLights.DeviceRowCaption(1, "Goes to Arduino RGB LEDs.", new[] { "Rim", "Hub" }));
            Assert.Equal("Rim has no LEDs OpenDash can reach. See SimHub's log.",
                PanelLights.DeviceRowCaption(2, PanelLights.BarDeviceCaption, new[] { "Rim" }));
        }
    }
}
