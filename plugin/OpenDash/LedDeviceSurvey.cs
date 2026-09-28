// LedDeviceSurvey.cs: what OpenDash saw of each device in SimHub's Devices plugin, whether it offered the
// device as a place for a strip's profile, and why not when it did not.
//
// **This file exists because a device OpenDash declined used to vanish without a word.** `LedTargets`
// walked SimHub's devices for LED modules and skipped, silently, both a device that is not an LED module
// at all and a module whose LED driver is null. The picker said nothing about a device it had not seen,
// and `PanelLights.NoDevices` fired only when the whole list was empty, so a wheel made in FanaBridge's
// wizard that was plainly in SimHub's Devices view was simply absent from OpenDash's picker, and the
// report could not say which of the two skips it had fallen through (#437). Now every device is judged
// here, the judgement is written to SimHub's log with what was seen, a device that throws while being
// read is judged as unreadable rather than dropped, and the names of the devices that were not offered
// and show some sign of LEDs are said beside the picker.
//
// Pure, and compiled into OpenDash.Tests: `LedTargets` reads SimHub's types into a `LedDeviceSeen` and
// this decides what to make of it, so the verdicts and the lines they produce are pinned without SimHub.
// Nothing here widens what is offered. Whether a strip profile installed into a module's other drivers
// (its button, encoder or individual-LED lists) would light a wheel's rev LEDs is a question for a rig,
// and the log line names which of those drivers a declined module carries so that the next report
// answers it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>Whether a device is offered as a place for a strip's profile, and if not, which skip it fell
    /// through.</summary>
    public enum LedDeviceVerdict
    {
        /// <summary>An LED module with a telemetry LED driver and a profile list: a target.</summary>
        Offered,

        /// <summary>Nothing under the device is an LED module SimHub's `GetDevices&lt;LedModuleDevice&gt;`
        /// returns, which is cause one of #437.</summary>
        NotLedModule,

        /// <summary>An LED module whose telemetry LED driver is null, which is cause two of #437.</summary>
        NoLedsDriver,

        /// <summary>An LED module whose driver has no settings, so no profile list to install into.</summary>
        NoLedsSettings,

        /// <summary>Reading the device threw, so nothing is known of its LEDs but that it has a name and an
        /// id. A plugin's own device is where that is likeliest, which is why it is not dropped.</summary>
        Unreadable,
    }

    /// <summary>One root device in SimHub's Devices plugin, as <see cref="LedTargets"/> read it. Plain data
    /// with no SimHub types, so a test can build one.</summary>
    public sealed class LedDeviceSeen
    {
        /// <summary>What SimHub's Devices view calls it.</summary>
        public string Name { get; set; }

        /// <summary>The id a bar would record, <see cref="LedBar.DeviceId"/> of its instance id. Logged so
        /// that two logs from either side of a restart show whether it held (cause four of #437).</summary>
        public string Id { get; set; }

        public bool Connected { get; set; }

        /// <summary>The device's CLR type and the assembly it comes from, which is how a device a third-party
        /// plugin registers is told from one SimHub builds.</summary>
        public string Kind { get; set; }

        /// <summary>The type of every instance under the device, itself included.</summary>
        public IList<string> Instances { get; set; } = new List<string>();

        /// <summary>Whether any instance is an LED module.</summary>
        public bool LedModule { get; set; }

        /// <summary>Whether that module carries a telemetry LED driver, the one a strip profile goes to.</summary>
        public bool LedsDriver { get; set; }

        /// <summary>Whether that driver carries settings, which is where the profile list lives.</summary>
        public bool LedsSettings { get; set; }

        /// <summary>The module's own LED count, as its descriptor gives it.</summary>
        public int? LedCount { get; set; }

        /// <summary>The module's other drivers by what SimHub's Devices view calls them: "buttons",
        /// "encoders", "individual LEDs", "matrix". Not offered; logged so that a module whose LEDs sit
        /// in one of them is recognisable from its log line.</summary>
        public IList<string> OtherDrivers { get; set; } = new List<string>();

        /// <summary>How many profiles the driver's saved list holds, when there is one.</summary>
        public int? Profiles { get; set; }

        /// <summary>Whether the device ships built-in profiles, and whether they are switched on, when
        /// there is a driver to ask. Cause three of #437 is the pair being true together.</summary>
        public bool? HasBuiltInProfiles { get; set; }

        public bool? UseBuiltInProfiles { get; set; }

        /// <summary>Public properties of an RGB LED driver type found on an instance that is not an LED
        /// module, as "Type.Property". Only ever read to be logged: a driver held somewhere OpenDash does
        /// not look is what cause one would look like, and this says where it is.</summary>
        public IList<string> ForeignDrivers { get; set; } = new List<string>();

        /// <summary>What reading the device threw, when it did. Everything else past the name and the id is
        /// then unknown.</summary>
        public string Unreadable { get; set; }
    }

    public static class LedDeviceSurvey
    {
        /// <summary>What to make of one device.</summary>
        public static LedDeviceVerdict Judge(LedDeviceSeen seen)
        {
            if (seen != null && seen.Unreadable != null) return LedDeviceVerdict.Unreadable;
            if (seen == null || !seen.LedModule) return LedDeviceVerdict.NotLedModule;
            if (!seen.LedsDriver) return LedDeviceVerdict.NoLedsDriver;
            if (!seen.LedsSettings) return LedDeviceVerdict.NoLedsSettings;
            return LedDeviceVerdict.Offered;
        }

        /// <summary>Why a device was not offered, in a contributor's words, for the log. Null when it was.</summary>
        public static string Reason(LedDeviceSeen seen)
        {
            switch (Judge(seen))
            {
                case LedDeviceVerdict.NotLedModule:
                    return "it is not an LED module, so SimHub's GetDevices<LedModuleDevice>() does not return it";
                case LedDeviceVerdict.NoLedsDriver:
                    return "its LED module has no telemetry LED driver (LedsDriver is null)";
                case LedDeviceVerdict.NoLedsSettings:
                    return "its LED driver has no settings, so there is no profile list to install into";
                case LedDeviceVerdict.Unreadable:
                    return "reading it threw: " + Or(seen.Unreadable, "no message");
                default:
                    return null;
            }
        }

        /// <summary>
        /// The line written to SimHub's log for one device: what it is, the verdict, and everything that
        /// tells the four causes of #437 apart.
        /// </summary>
        /// <remarks>
        /// One line per device rather than one per verdict, because the report that prompted this had a
        /// wheel in SimHub and nothing in OpenDash, and what was needed was exactly this line for that
        /// wheel. It is long on purpose: the log is addressed to whoever reads the report, not to the
        /// driver, and every field in it is one a report would otherwise have to go back and ask for.
        /// </remarks>
        public static string LogLine(LedDeviceSeen seen)
        {
            if (seen == null) return "LED device survey: a null device.";
            var parts = new List<string>();
            parts.Add("id " + Or(seen.Id, "none"));
            if (seen.Unreadable == null) parts.Add(seen.Connected ? "connected" : "not connected");
            parts.Add("type " + Or(seen.Kind, "unknown"));
            if (seen.Instances != null && seen.Instances.Count > 0)
            {
                parts.Add("instances " + string.Join(", ", seen.Instances));
            }
            if (seen.LedModule)
            {
                if (seen.LedCount.HasValue) parts.Add(seen.LedCount.Value.ToString(CultureInfo.InvariantCulture) + " LEDs");
                if (seen.Profiles.HasValue) parts.Add(seen.Profiles.Value.ToString(CultureInfo.InvariantCulture) + " saved profiles");
                if (seen.HasBuiltInProfiles.HasValue)
                {
                    parts.Add(seen.HasBuiltInProfiles.Value
                        ? "built-in profiles " + (seen.UseBuiltInProfiles == true ? "switched on" : "switched off")
                        : "no built-in profiles");
                }
                if (seen.OtherDrivers != null && seen.OtherDrivers.Count > 0)
                {
                    parts.Add("other drivers: " + string.Join(", ", seen.OtherDrivers));
                }
            }
            if (seen.ForeignDrivers != null && seen.ForeignDrivers.Count > 0)
            {
                parts.Add("RGB LED drivers outside an LED module: " + string.Join(", ", seen.ForeignDrivers));
            }

            var reason = Reason(seen);
            var head = reason == null
                ? "LED device offered: \"" + Or(seen.Name, "unnamed") + "\""
                : "LED device not offered: \"" + Or(seen.Name, "unnamed") + "\", because " + reason;
            return head + ". " + string.Join("; ", parts) + ".";
        }

        /// <summary>
        /// Whether a device shows any sign of having LEDs: an LED module, an RGB LED driver held outside
        /// one, or a device that could not be read far enough to tell.
        /// </summary>
        /// <remarks>
        /// SimHub's Devices view holds pedals, shifters, screens and bass shakers too, and a pedal set has
        /// no LEDs to reach. Naming it under every strip's device row would be a sentence the reader can do
        /// nothing with, which docs/design/voice.md keeps off the panel. It is still judged and still
        /// logged; only the panel leaves it out.
        /// </remarks>
        public static bool ShowsLeds(LedDeviceSeen seen)
        {
            if (seen == null) return false;
            return seen.LedModule
                || seen.Unreadable != null
                || (seen.ForeignDrivers != null && seen.ForeignDrivers.Count > 0);
        }

        /// <summary>
        /// The names of the devices seen, not offered and showing a sign of LEDs, in the order seen, for
        /// the picker to name. Every declined device is in the log whether it is named here or not.
        /// </summary>
        public static IList<string> Declined(IEnumerable<LedDeviceSeen> seen)
        {
            return (seen ?? Enumerable.Empty<LedDeviceSeen>())
                .Where(s => s != null && Judge(s) != LedDeviceVerdict.Offered && ShowsLeds(s))
                .Select(s => Or(s.Name, "A device"))
                .ToList();
        }

        private static string Or(string value, string otherwise)
        {
            return string.IsNullOrWhiteSpace(value) ? otherwise : value.Trim();
        }
    }
}
