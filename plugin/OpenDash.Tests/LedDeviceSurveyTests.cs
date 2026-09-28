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

        [Fact]
        public void Only_the_declined_are_named_for_the_picker_in_the_order_seen()
        {
            var pedals = new LedDeviceSeen { Name = "Pedals", Kind = "X" };
            var bare = Wheel();
            bare.Name = "  ";
            bare.LedsDriver = false;
            var declined = LedDeviceSurvey.Declined(new[] { Wheel(), pedals, null, bare });
            Assert.Equal(new[] { "Pedals", "A device" }, declined);
            Assert.Empty(LedDeviceSurvey.Declined(null));
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
    }
}
