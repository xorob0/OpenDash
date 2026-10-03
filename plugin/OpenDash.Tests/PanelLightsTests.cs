// PanelLightsTests.cs: the LEDs page hands its controls two arrays and reads them by index, so the
// labels and the values they name have to stay the same length; and the strip words the page draws.
//
// This is the failure the design audit found rather than an invented one: the centre drop-down went on
// offering a label for a value the contract had retired, and nothing said so. A segmented bar is worse
// than silent, since BuildSegmented indexes the labels with the values and throws while the page is being
// drawn.
using System;
using System.Globalization;
using System.IO;
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
            // voice.md's own example beside "No strips yet", departing from the artboard's "Add LEDs", as
            // docs/design/plugin.md's departures table records (PanelCopyTests reads the row).
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
            // The hover in the label's own verb, Download or Update.
            Assert.Equal("Downloads Lovely Car Data.", PanelLights.CarTablesButtonTooltip(0));
            Assert.Equal("Updates Lovely Car Data.", PanelLights.CarTablesButtonTooltip(84));
            Assert.StartsWith(PanelLights.CarTablesButton(0) + "s ", PanelLights.CarTablesButtonTooltip(0));
            Assert.StartsWith(PanelLights.CarTablesButton(84) + "s ", PanelLights.CarTablesButtonTooltip(84));
            Assert.Equal("Not downloaded yet.", PanelLights.CarTablesNone);
            Assert.Equal("Downloading…", PanelLights.CarTablesDownloading);
            // A chooser's value names what the code knows: SimHub does not list the device.
            Assert.Equal("Device not in SimHub", PanelLights.DeviceGone);
            Assert.Equal(" · Not connected", PanelLights.DeviceOffline);
            Assert.Equal("Goes to Arduino RGB LEDs.", PanelLights.OneDevice("Arduino RGB LEDs"));
            Assert.Equal("Goes to Button hub, which is not connected.", PanelLights.OneDevice("Button hub", false));
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
            // The Matrix page's own words for its name box, in PanelMatrix.cs: PanelLights keeps no matrix copy.
            Assert.Contains("Not shown in SimHub", PanelMatrix.NameCaption);
            foreach (var gone in new[] { "PanelsTitle", "PanelsCaption", "AddPanel", "PanelNameTitle", "PanelNameCaption", "AddPanelCaption", "PanelAdded", "PanelNeedsInstall", "BoxCaption", "PanelSlot" })
            {
                Assert.Empty(typeof(PanelLights).GetMember(gone));
            }
        }

        /// <summary>The flag box profile's row on Updates says what it is, since its name does not.</summary>
        [Fact]
        public void The_flag_box_row_says_what_the_profile_paints()
        {
            Assert.Equal("8 × 8 matrix", PanelLightRows.FlagBoxCaption);
        }

        /// <summary>A strip whose profile could not be installed is sent to SimHub's log (voice.md's failure
        /// form): Updates offers no press for a profile that is not in SimHub, so "See the Updates page" led
        /// nowhere. A failed move is PanelLeds.MovedNotInstalled's, pinned with the LEDs page. Rename installs the profile again only where SimHub
        /// holds it, so its hover promises only the rename, and its sheet promises no "Install the strip
        /// again".</summary>
        [Fact]
        public void A_strip_whose_profile_failed_is_sent_to_the_log()
        {
            // The log, then the step left: the header's Install. No restart, since the strip's settings are
            // attached when it is saved (#565).
            Assert.Equal("Added Rim, but its profile could not be installed. See SimHub's log, then install it here.", PanelLights.BarAddFailed("Rim"));
            // Rename reinstalls only where SimHub holds the profile, so the hover promises only the rename.
            Assert.Equal("Renames this strip.", PanelLights.RenameBarTooltip);
            // Nor does the Rename sheet one click later: its footer has no note, since saving reinstalls.
            var leds = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            var rename = leds.Substring(leds.IndexOf("ShowSheet(\"Rename \"", System.StringComparison.Ordinal));
            Assert.StartsWith("SheetFooter(null, cancel, save)", rename.Substring(rename.IndexOf("SheetFooter(", System.StringComparison.Ordinal)));
            Assert.DoesNotContain("Install the strip again", leds);
        }

        /// <summary>A strip added is told the steps OpenDash does not take, in the order they are done, as
        /// voice.md's own example has them: the select on the device it went to. A note the install returned
        /// comes first, since nothing is listed to select until it is done. No restart: the plugin attaches the
        /// strip's own settings when it is saved, where it used to publish them only for the strips it held at
        /// start (#565).</summary>
        [Fact]
        public void A_strip_added_says_where_to_select_it_and_asks_for_no_restart()
        {
            Assert.Equal("Added Rim. Select \"Rim\" on Fanatec CSL Elite in SimHub to use it.",
                PanelLights.BarAdded("Rim", "Fanatec CSL Elite"));
            Assert.Equal("Added Rim. Select \"Rim\" in SimHub to use it.",
                PanelLights.BarAdded("Rim", null));
            Assert.Equal("Added Rim. " + FlagBoxInstallPlan.BuiltInModeNote + " Select \"Rim\" on Wheel in SimHub to use it.",
                PanelLights.BarAdded("Rim", "Wheel", FlagBoxInstallPlan.BuiltInModeNote));
        }

        /// <summary>
        /// The status line is CarLightService's status, with "updated over a week ago" in place of its age where the copy
        /// is, so the age is never given twice and "updated just now" never sits beside the note. The
        /// page works the note out as it draws the row, since the status's own age is written only when the
        /// tables are read.
        /// </summary>
        [Fact]
        public void The_stale_note_takes_the_place_of_the_age()
        {
            Assert.Equal(", updated over a week ago", PanelLights.CarTablesStaleAge);
            Assert.Equal("Press Update for a newer copy.", PanelLights.CarTablesUpdateStep);
            // Every state of the line ends in a full stop, the plain one included.
            Assert.Equal("84 cars, updated 9 days ago.", PanelLights.CarTablesLine("84 cars, updated 9 days ago", false));
            Assert.Equal("84 cars.", PanelLights.CarTablesLine("84 cars", false));
            Assert.Equal("84 cars, updated over a week ago. Press Update for a newer copy.", PanelLights.CarTablesLine("84 cars, updated 9 days ago", true));
            // Frozen at "just now" by a SimHub left running a week: the note replaces it rather than contradicting it.
            Assert.Equal("84 cars, updated over a week ago. Press Update for a newer copy.", PanelLights.CarTablesLine("84 cars, updated just now", true));
            // Tables with no fetch stamp give no age, and are stale by CarLightLibrary.IsStale only because nothing
            // says when they came: offered the Update, never called over a week old.
            Assert.Equal("84 cars. Press Update for a newer copy.", PanelLights.CarTablesLine("84 cars", true));
            Assert.Equal("1 car, updated over a week ago. Press Update for a newer copy.", PanelLights.CarTablesLine("1 car, updated yesterday", true));
            Assert.Equal("Not downloaded yet.", PanelLights.CarTablesLine("Not downloaded yet.", false));
            Assert.Equal(string.Empty, PanelLights.CarTablesLine(null, true));
            Assert.Equal(string.Empty, PanelLights.CarTablesLine(null, false));
            // Held to what CarLightService.Describe writes, so a reword there moves this.
            var now = new System.DateTime(2026, 9, 30, 12, 0, 0, System.DateTimeKind.Utc);
            Assert.Equal("84 cars, updated over a week ago. Press Update for a newer copy.", PanelLights.CarTablesLine(CarLightService.Describe(84, now.AddDays(-9), now, null), true));
            Assert.Equal("84 cars. Press Update for a newer copy.", PanelLights.CarTablesLine(CarLightService.Describe(84, null, now, null), true));
        }

        /// <summary>
        /// A download that did not answer is said in voice.md's failure form, never with the fetch's own message in
        /// a bracketed aside or after a colon: with no copy, what failed and the log, where the page writes the
        /// reason; beside a copy that works, what failed and what the driver still has, as "Could not reach
        /// GitHub. You have 0.3.0-rc.4." has it.
        /// </summary>
        [Fact]
        public void A_download_that_did_not_answer_is_said_in_the_rows_words()
        {
            Assert.Equal("Could not download Lovely Car Data. See SimHub's log.", PanelLights.CarTablesDownloadFailed);
            Assert.Equal("Could not download a newer copy.", PanelLights.CarTablesNewerFailed);
            var now = new System.DateTime(2026, 9, 30, 12, 0, 0, System.DateTimeKind.Utc);
            var failed = CarLightRefresh.Failed("Unable to connect to the remote server", 84);
            var none = CarLightService.Describe(0, null, now, CarLightRefresh.Failed("The remote name could not be resolved: 'codeload.github.com'", 0));
            Assert.Equal(PanelLights.CarTablesDownloadFailed, PanelLights.CarTablesLine(none, false));
            // The count is the copy's, not the driver's.
            Assert.Equal("Could not download a newer copy. Your copy has 84 cars, updated 9 days ago.",
                PanelLights.CarTablesLine(CarLightService.Describe(84, now.AddDays(-9), now, failed), false));
            Assert.Equal("Could not download a newer copy. Your copy has 84 cars, updated over a week ago.",
                PanelLights.CarTablesLine(CarLightService.Describe(84, now.AddDays(-9), now, failed), true));
            foreach (var status in new[] { none, CarLightService.Describe(84, now.AddDays(-9), now, failed) })
            {
                Assert.True(PanelLights.CarTablesDownloadDidNotAnswer(status));
                Assert.DoesNotContain("remote", PanelLights.CarTablesLine(status, false));
                Assert.DoesNotContain("(", PanelLights.CarTablesLine(status, true));
            }
            Assert.False(PanelLights.CarTablesDownloadDidNotAnswer(PanelLights.CarTablesNone));
            Assert.False(PanelLights.CarTablesDownloadDidNotAnswer("84 cars, updated just now"));
            Assert.False(PanelLights.CarTablesDownloadDidNotAnswer(null));
            // The words matched are CarLightService's own: a reword there has to move them here.
            var service = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "CarLightService.cs"));
            Assert.Contains("line += \"" + PanelLights.ServiceAge, service);
            Assert.Contains("line += \"" + PanelLights.ServiceFailedTail, service);
            // The reason goes to the log, since the row no longer shows it.
            var leds = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.Contains("if (PanelLights.CarTablesDownloadDidNotAnswer(status)) Log.Warn(\"Lovely Car Data could not be downloaded: \" + status);", leds);
        }

        [Fact]
        public void The_car_tables_button_and_caption_are_pinned()
        {
            Assert.Equal("Update", PanelLights.CarTablesButton(84));
            Assert.Equal("Download", PanelLights.CarTablesButton(0));
            // The page works the age out from what the service holds as it draws the row, with the clock of the
            // moment, and never from the folder: a tick draws the row, and nothing is read from disk on a tick.
            var leds = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.Contains("var stale = PanelLights.CarTablesStale(service.CarCount, service.FetchedAt, DateTime.UtcNow);", leds);
            Assert.Contains("var line = PanelLights.CarTablesLine(status, stale);", leds);
            Assert.DoesNotContain(".Stale(", leds);
            Assert.DoesNotContain("CarLightLibrary.", leds);
            // The stamp is read after the status, which the service's read writes after it.
            var refresh = leds.Substring(leds.IndexOf("private void RefreshCarTables()", StringComparison.Ordinal));
            Assert.InRange(refresh.IndexOf("var status = service.Status;", StringComparison.Ordinal), 0, refresh.IndexOf("service.FetchedAt", StringComparison.Ordinal));
            // The start reads the tables on a thread of its own: a tick draws the row again once the status or the
            // count it was drawn from moves, and only then.
            Assert.Contains("carTablesDrawn = CarTablesKey();", leds);
            Assert.Contains("if (carTablesLine == null || carTablesDownloading) return;", leds);
            Assert.Contains("if (!string.Equals(CarTablesKey(), carTablesDrawn, StringComparison.Ordinal)) RefreshCarTables();", leds);
            // The one row that fetches the tables names both things that read them.
            Assert.Equal("Needed for the car's own rev lights and car-specific shift points. Every car is downloaded at once, about 400 KB, so your car is never disclosed.",
                PanelLights.CarTablesCaption);
        }

        /// <summary>
        /// The age the row works out from the service's stamp in memory is CarLightLibrary.IsStale's over the same
        /// stamp on disk: none, a week, and a stamp in the future, each on both sides of its edge. With no cars
        /// there is no copy to call old, as CarLightService.Stale has it.
        /// </summary>
        [Fact]
        public void The_rows_age_is_the_librarys_from_the_stamp_in_memory()
        {
            var now = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
            var root = Path.Combine(Path.GetTempPath(), "opendash-stale-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                Assert.True(CarLightLibrary.IsStale(root, now));
                Assert.True(PanelLights.CarTablesStale(84, null, now));
                foreach (var age in new[] { TimeSpan.Zero, TimeSpan.FromDays(6), TimeSpan.FromDays(7) - TimeSpan.FromTicks(1), TimeSpan.FromDays(7), TimeSpan.FromDays(30), TimeSpan.FromDays(-1), TimeSpan.FromTicks(-1) })
                {
                    var stamp = now - age;
                    File.WriteAllText(Path.Combine(root, CarLightLibrary.StampFile), stamp.Ticks.ToString(CultureInfo.InvariantCulture));
                    Assert.Equal(CarLightLibrary.IsStale(root, now), PanelLights.CarTablesStale(84, stamp, now));
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
            Assert.False(PanelLights.CarTablesStale(0, null, now));
            Assert.False(PanelLights.CarTablesStale(0, now.AddDays(-30), now));
            Assert.False(PanelLights.CarTablesStale(1, now.AddDays(-6), now));
            Assert.True(PanelLights.CarTablesStale(1, now.AddDays(-7), now));
            Assert.True(PanelLights.CarTablesStale(1, now.AddMinutes(1), now));
        }

        /// <summary>
        /// CarLightService's lowercase states, which name the tables by the retired noun and one of which carries
        /// an exception's message, are said in the row's words, and a failure points at the log the page writes.
        /// </summary>
        [Fact]
        public void The_services_own_states_are_said_in_the_rows_words()
        {
            Assert.Equal("Loading…", PanelLights.CarTablesLoading);
            // The service says a failed read in the panel's failure form (#523), and the row says it as is.
            Assert.Equal("Lovely Car Data could not be read. See SimHub's log.", CarLightService.Unreadable);
            Assert.Equal(CarLightService.Unreadable, PanelLights.CarTablesUnreadable);
            Assert.Equal(PanelLights.CarTablesLoading, PanelLights.CarTablesLine(new CarLightService(null, System.IO.Path.GetTempPath()).Status, false));
            Assert.Equal(PanelLights.CarTablesUnreadable, PanelLights.CarTablesLine(CarLightService.Unreadable, true));
            Assert.Equal(PanelLights.CarTablesUnreadable, PanelLights.CarTablesLine(CarLightService.Unreadable, false));
            Assert.True(PanelLights.CarTablesUnread(CarLightService.Unreadable));
            Assert.False(PanelLights.CarTablesUnread("could not read the car light tables: boom"));
            Assert.False(PanelLights.CarTablesUnread("84 cars"));
            Assert.False(PanelLights.CarTablesUnread(null));
            // The words matched are CarLightService's own: a reword there has to move them here.
            var service = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "CarLightService.cs"));
            Assert.Contains("status = \"" + PanelLights.ServiceNotLoaded + "\";", service);
            // Both failures set the one status and keep the exception's message for the log.
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(service, @"error = e\.Message;\s+status = Unreadable;").Count);
            Assert.DoesNotContain("car light tables", service);
            // The reason is written to the log, once for each failure, so the line has something behind it.
            var leds = RepoPaths.Code(System.IO.Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "SettingsControl.Lights.cs"));
            Assert.Contains("Log.Warn(\"Lovely Car Data could not be read: \" + error);", leds);
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

            // The one device, named with its state: "not Rim" follows the device it contrasts with, and a device
            // SimHub is not talking to keeps that fact beside its own name rather than under a second "which".
            Assert.Equal("Goes to Arduino RGB LEDs.", PanelLights.OneDeviceCaption("Arduino RGB LEDs", true, null));
            Assert.Equal("Goes to Button hub, which is not connected.", PanelLights.OneDeviceCaption("Button hub", false, new string[0]));
            Assert.Equal("Goes to Arduino RGB LEDs, not Rim, which has no LEDs OpenDash can reach. See SimHub's log.",
                PanelLights.OneDeviceCaption("Arduino RGB LEDs", true, new[] { "Rim" }));
            Assert.Equal("Goes to Button hub, which is not connected, and Rim has no LEDs OpenDash can reach. See SimHub's log.",
                PanelLights.OneDeviceCaption("Button hub", false, new[] { "Rim" }));
            Assert.Equal("Goes to Button hub, which is not connected, and Rim and Hub have no LEDs OpenDash can reach. See SimHub's log.",
                PanelLights.OneDeviceCaption("Button hub", false, new[] { "Rim", "Hub" }));
            // The fact alone, for a line that goes on to the steps after it.
            Assert.Null(PanelLights.Unreached(null));
            Assert.Equal("Rim has no LEDs OpenDash can reach", PanelLights.Unreached(new[] { "Rim" }));
            Assert.Equal(PanelLights.Unreached(new[] { "Rim", "Hub" }) + ". See SimHub's log.", PanelLights.NotOffered(new[] { "Rim", "Hub" }));
        }
    }
}
