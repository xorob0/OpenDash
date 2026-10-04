// LedDeviceSurveyTests.cs: a device OpenDash sees and does not offer says so, with the reason, rather
// than being missing from a list.
//
// #437 was a wheel made in FanaBridge's wizard that was in SimHub's Devices view and not in OpenDash's
// picker, with nothing to say whether it had fallen through the "not an LED module" skip or the "no LED
// driver" skip. These pin the verdict for each shape a device can have, the log line that tells the four
// causes of that ticket apart, and the sentence the picker says.
using System.Collections.Generic;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class LedDeviceSurveyTests
    {
        private static LedDeviceSeen Wheel()
        {
            return new LedDeviceSeen
            {
                Name = "Rim",
                Id = "device:3f2504e0-4f89-11d3-9a0c-0305e82c3301",
                Connected = true,
                Kind = "SimHub.Plugins.Devices.CompositeDeviceInstance (SimHub.Plugins)",
                Instances = new List<string> { "CompositeDeviceInstance", "LedModuleDevice" },
                LedModule = true,
                LedsDriver = true,
                LedsSettings = true,
                LedCount = 13,
                Profiles = 4,
                HasBuiltInProfiles = false,
            };
        }

        [Fact]
        public void A_module_with_a_driver_and_a_profile_list_is_offered()
        {
            Assert.Equal(LedDeviceVerdict.Offered, LedDeviceSurvey.Judge(Wheel()));
            Assert.Null(LedDeviceSurvey.Reason(Wheel()));
        }

        /// <summary>#686: a device a plugin registers as a type of its own, whose settings page carries SimHub's
        /// LED editor, is judged as the module that editor holds, and the line says how it was reached.</summary>
        [Fact]
        public void A_device_whose_settings_page_carries_an_LED_editor_is_judged_as_its_module()
        {
            var seen = new LedDeviceSeen
            {
                Name = "Fanatec ClubSport Formula V2.5",
                Id = "device:f4a7103f-67eb-4890-ad5d-081ea20af228",
                Kind = "FanaBridge.Adapters.FanatecWheelDeviceInstance (FanaBridge)",
                Instances = new List<string> { "FanatecWheelDeviceInstance" },
                EditorModule = true,
                LedsDriver = true,
                LedsSettings = true,
                LedCount = 15,
                Profiles = 1,
                HasBuiltInProfiles = false,
            };
            Assert.Equal(LedDeviceVerdict.Offered, LedDeviceSurvey.Judge(seen));
            Assert.Null(LedDeviceSurvey.Reason(seen));
            var line = LedDeviceSurvey.LogLine(seen);
            Assert.StartsWith("LED device offered: \"Fanatec ClubSport Formula V2.5\".", line);
            Assert.Contains("LED module reached through its settings page", line);
            Assert.Contains("15 LEDs", line);
            Assert.Contains("1 saved profiles", line);
            Assert.Contains("no built-in profiles", line);
            Assert.True(LedDeviceSurvey.ShowsLeds(seen));
            Assert.Empty(LedDeviceSurvey.Declined(new[] { seen }));
            // The module's own three verdicts apply to it: a driver without settings is declined as such.
            seen.LedsSettings = false;
            Assert.Equal(LedDeviceVerdict.NoLedsSettings, LedDeviceSurvey.Judge(seen));
            // And a page with no editor is cause one still, with the reason saying both.
            seen.EditorModule = false;
            Assert.Equal(LedDeviceVerdict.NotLedModule, LedDeviceSurvey.Judge(seen));
            Assert.Contains("and its settings page carries no LED editor", LedDeviceSurvey.Reason(seen));
            Assert.DoesNotContain("settings page", LedDeviceSurvey.LogLine(seen).Split(new[] { ". id " }, System.StringSplitOptions.None)[1]);
        }

        /// <summary>Cause one of #437: a device a plugin registers as a type of its own.</summary>
        [Fact]
        public void A_device_with_no_LED_module_is_declined_and_says_it_is_not_one()
        {
            var seen = new LedDeviceSeen
            {
                Name = "Fanatec wheel",
                Id = "device:00000000-0000-0000-0000-000000000001",
                Kind = "FanaBridge.WheelDevice (FanaBridge)",
                Instances = new List<string> { "WheelDevice" },
                ForeignDrivers = new List<string> { "WheelDevice.Leds" },
            };
            Assert.Equal(LedDeviceVerdict.NotLedModule, LedDeviceSurvey.Judge(seen));
            var line = LedDeviceSurvey.LogLine(seen);
            Assert.StartsWith("LED device not offered: \"Fanatec wheel\", because it is not an LED module", line);
            // The type and its assembly are what tell a plugin's device from SimHub's own.
            Assert.Contains("type FanaBridge.WheelDevice (FanaBridge)", line);
            Assert.Contains("instances WheelDevice", line);
            Assert.Contains("RGB LED drivers outside an LED module: WheelDevice.Leds", line);
        }

        /// <summary>Cause two of #437: an LED module whose telemetry driver is null. The other drivers it
        /// carries are named, because a wheel whose LEDs are all in the individual-LED list would read
        /// exactly like this and is the next thing to ask.</summary>
        [Fact]
        public void A_module_without_a_driver_is_declined_and_names_the_drivers_it_has()
        {
            var seen = Wheel();
            seen.LedsDriver = false;
            seen.LedsSettings = false;
            seen.LedCount = 0;
            seen.Profiles = null;
            seen.HasBuiltInProfiles = null;
            seen.OtherDrivers = new List<string> { "buttons", "individual LEDs" };
            Assert.Equal(LedDeviceVerdict.NoLedsDriver, LedDeviceSurvey.Judge(seen));
            var line = LedDeviceSurvey.LogLine(seen);
            Assert.Contains("because its LED module has no telemetry LED driver", line);
            Assert.Contains("0 LEDs", line);
            Assert.Contains("other drivers: buttons, individual LEDs", line);
        }

        [Fact]
        public void A_driver_without_settings_is_declined()
        {
            var seen = Wheel();
            seen.LedsSettings = false;
            Assert.Equal(LedDeviceVerdict.NoLedsSettings, LedDeviceSurvey.Judge(seen));
            Assert.Contains("no profile list", LedDeviceSurvey.Reason(seen));
        }

        /// <summary>Causes three and four are about a device that is offered, so its line carries the
        /// built-in switch and the id: two logs either side of a restart show whether the id held.</summary>
        [Fact]
        public void An_offered_device_logs_its_id_and_its_built_in_profiles()
        {
            var seen = Wheel();
            seen.HasBuiltInProfiles = true;
            seen.UseBuiltInProfiles = true;
            var line = LedDeviceSurvey.LogLine(seen);
            Assert.StartsWith("LED device offered: \"Rim\".", line);
            Assert.Contains("id device:3f2504e0-4f89-11d3-9a0c-0305e82c3301", line);
            Assert.Contains("connected", line);
            Assert.Contains("13 LEDs", line);
            Assert.Contains("4 saved profiles", line);
            Assert.Contains("built-in profiles switched on", line);

            seen.UseBuiltInProfiles = false;
            Assert.Contains("built-in profiles switched off", LedDeviceSurvey.LogLine(seen));
            seen.HasBuiltInProfiles = false;
            Assert.Contains("no built-in profiles", LedDeviceSurvey.LogLine(seen));
        }

        /// <summary>
        /// Only a declined device with some sign of LEDs is named for the picker, in the order seen. A pedal
        /// set is declined and logged like any other device, and naming it under every strip would be a
        /// sentence nobody can act on.
        /// </summary>
        [Fact]
        public void Only_the_declined_with_a_sign_of_LEDs_are_named_for_the_picker_in_the_order_seen()
        {
            var pedals = new LedDeviceSeen { Name = "Pedals", Kind = "X" };
            var foreign = new LedDeviceSeen { Name = "Fanatec wheel", ForeignDrivers = new List<string> { "WheelDevice.Leds" } };
            var broken = new LedDeviceSeen { Name = "Button box", Unreadable = "NullReferenceException: boom" };
            var bare = Wheel();
            bare.Name = "  ";
            bare.LedsDriver = false;
            var declined = LedDeviceSurvey.Declined(new[] { Wheel(), pedals, foreign, null, broken, bare });
            Assert.Equal(new[] { "Fanatec wheel", "Button box", "A device" }, declined);
            Assert.Empty(LedDeviceSurvey.Declined(null));

            Assert.False(LedDeviceSurvey.ShowsLeds(pedals));
            Assert.False(LedDeviceSurvey.ShowsLeds(null));
            Assert.True(LedDeviceSurvey.ShowsLeds(foreign));
            Assert.True(LedDeviceSurvey.ShowsLeds(broken));
            Assert.True(LedDeviceSurvey.ShowsLeds(bare));
            // Still judged, and still logged with its reason.
            Assert.StartsWith("LED device not offered: \"Pedals\", because it is not an LED module",
                LedDeviceSurvey.LogLine(pedals));
        }

        /// <summary>
        /// A device that throws while being read is judged, logged under its name and id with what it
        /// threw, and named for the picker, rather than dropped: a plugin's own device is where that is
        /// likeliest, and it is the device #437 asks after.
        /// </summary>
        [Fact]
        public void A_device_that_could_not_be_read_is_declined_and_says_what_it_threw()
        {
            var seen = new LedDeviceSeen
            {
                Name = "Fanatec wheel",
                Id = "device:00000000-0000-0000-0000-000000000002",
                Kind = "FanaBridge.WheelDevice (FanaBridge)",
                Unreadable = "NullReferenceException: Object reference not set to an instance of an object.",
                // Whatever else is filled in, an unreadable device is judged unreadable.
                LedModule = true,
                LedsDriver = true,
                LedsSettings = true,
            };
            Assert.Equal(LedDeviceVerdict.Unreadable, LedDeviceSurvey.Judge(seen));
            var line = LedDeviceSurvey.LogLine(seen);
            Assert.StartsWith("LED device not offered: \"Fanatec wheel\", because reading it threw: NullReferenceException", line);
            Assert.Contains("id device:00000000-0000-0000-0000-000000000002", line);
            Assert.Contains("type FanaBridge.WheelDevice (FanaBridge)", line);
            // Whether it is connected was not read, so the line does not claim either.
            Assert.DoesNotContain("connected", line);
            Assert.Equal(new[] { "Fanatec wheel" }, LedDeviceSurvey.Declined(new[] { seen }));

            var nameless = new LedDeviceSeen { Unreadable = "" };
            Assert.Equal(LedDeviceVerdict.Unreadable, LedDeviceSurvey.Judge(nameless));
            Assert.Contains("reading it threw: no message", LedDeviceSurvey.LogLine(nameless));
        }

        [Fact]
        public void A_line_is_written_for_a_device_with_nothing_filled_in()
        {
            var line = LedDeviceSurvey.LogLine(new LedDeviceSeen());
            Assert.StartsWith("LED device not offered: \"unnamed\"", line);
            Assert.Contains("id none", line);
            Assert.Contains("type unknown", line);
            Assert.Contains("null", LedDeviceSurvey.LogLine(null));
        }

        /// <summary>A FanaBridge wheel as the rig reported it for #690: reached through its settings page, 15
        /// telemetry LEDs and an individual-LEDs driver over the same 15.</summary>
        private static LedDeviceSeen FanaBridgeWheel(string mode)
        {
            return new LedDeviceSeen
            {
                Name = "Fanatec ClubSport Formula V2.5",
                Id = "device:d6be8e96-0674-423a-97b5-67234bb81724",
                Kind = "FanaBridge.Adapters.FanatecWheelDeviceInstance (FanaBridge)",
                Instances = new List<string> { "FanatecWheelDeviceInstance" },
                EditorModule = true,
                LedsDriver = true,
                LedsSettings = true,
                LedCount = 15,
                Profiles = 2,
                HasBuiltInProfiles = false,
                OtherDrivers = new List<string> { "individual LEDs" },
                IndividualLeds = mode,
                IndividualLedCount = 15,
                IndividualLedsSettings = true,
            };
        }

        /// <summary>#690: the line names the "Individual leds profiles" choice in the page's own words, and
        /// says what that choice does to the list a strip's profile goes into.</summary>
        [Fact]
        public void The_line_names_the_individual_leds_mode_in_the_pages_words()
        {
            var disabled = LedDeviceSurvey.LogLine(FanaBridgeWheel("Disabled"));
            Assert.Contains("individual leds profiles Disabled over 15 LEDs.", disabled);

            var combined = LedDeviceSurvey.LogLine(FanaBridgeWheel("Combined"));
            Assert.Contains("individual leds profiles Combined over 15 LEDs.", combined);

            var exclusive = LedDeviceSurvey.LogLine(FanaBridgeWheel("Exclusive"));
            Assert.Contains("individual leds profiles Individual profile only over 15 LEDs, so a strip's profile goes into that list as well", exclusive);

            var unequal = FanaBridgeWheel("Exclusive");
            unequal.IndividualLedCount = 27;
            Assert.Contains("Individual profile only over 27 LEDs, which hides and does not draw the Telemetry LEDs list", LedDeviceSurvey.LogLine(unequal));
        }

        /// <summary>A module with no individual-LEDs driver has no such choice on its page, and its line says
        /// nothing about one.</summary>
        [Fact]
        public void A_module_without_individual_leds_says_nothing_of_the_mode()
        {
            Assert.DoesNotContain("individual leds profiles", LedDeviceSurvey.LogLine(Wheel()));
            Assert.False(LedDeviceSurvey.IsIndividualOnly(Wheel()));
            Assert.False(LedDeviceSurvey.InstallsIntoIndividual(Wheel()));
        }

        /// <summary>#690: the profile goes into the individual list as well only under "Individual profile
        /// only", where the telemetry list is neither shown nor drawn, and only over the same LEDs. Combined
        /// draws the individual list on top of the telemetry one, so a second copy there would draw twice.</summary>
        [Fact]
        public void The_individual_list_takes_the_profile_only_when_it_alone_is_drawn_over_the_same_leds()
        {
            Assert.True(LedDeviceSurvey.InstallsIntoIndividual(FanaBridgeWheel("Exclusive")));
            Assert.True(LedDeviceSurvey.IsIndividualOnly(FanaBridgeWheel("Exclusive")));

            Assert.False(LedDeviceSurvey.InstallsIntoIndividual(FanaBridgeWheel("Disabled")));
            Assert.False(LedDeviceSurvey.InstallsIntoIndividual(FanaBridgeWheel("Combined")));
            Assert.False(LedDeviceSurvey.IsIndividualOnly(FanaBridgeWheel("Combined")));

            var buttons = FanaBridgeWheel("Exclusive");
            buttons.IndividualLedCount = 27;
            Assert.False(LedDeviceSurvey.InstallsIntoIndividual(buttons));
            Assert.True(LedDeviceSurvey.IsIndividualOnly(buttons));

            var noList = FanaBridgeWheel("Exclusive");
            noList.IndividualLedsSettings = false;
            Assert.False(LedDeviceSurvey.InstallsIntoIndividual(noList));

            var unknown = FanaBridgeWheel("Exclusive");
            unknown.IndividualLedCount = null;
            Assert.False(LedDeviceSurvey.InstallsIntoIndividual(unknown));

            var empty = FanaBridgeWheel("Exclusive");
            empty.LedCount = 0;
            empty.IndividualLedCount = 0;
            Assert.False(LedDeviceSurvey.InstallsIntoIndividual(empty));
        }
    }
}
