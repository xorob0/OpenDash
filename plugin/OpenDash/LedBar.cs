// LedBar.cs: one RGB strip the user added -- a name they chose, the shape it is, and the settings that
// decide what it shows.
//
// A strip used to be a shape and nothing else. The build emits one `.ledsprofile` per shape, the panel
// offered a row per shape, and every one of them read the same three rig-wide properties -- so two strips
// on one rig could be installed separately and could not be *configured* separately, which is the whole
// of what somebody with a wheel and a brow wants. ADR 0017 answered exactly this question for screens and
// the answer is the same here: the unit is an instance, its namespace is frozen at creation, and the
// profile installed for it is the embedded one with that namespace written through it.
//
// What stays rig-wide stays rig-wide, and the line is the same as the flag box's: brightness, night mode
// and the low-fuel threshold are one answer for the rig, and so is the car's own shift pattern, which is
// the car's rather than the strip's. What a bar owns is what its LEDs do with all that.
using System;
using System.Globalization;

namespace OpenDashPlugin
{
    /// <summary>One strip on the rig, with the settings its own profile reads.</summary>
    public sealed class LedBar
    {
        /// <summary>
        /// What this bar's properties are called: a slug of the name it was created with.
        /// </summary>
        /// <remarks>
        /// Frozen at creation and never moved by a rename, for the reason ADR 0017 freezes a screen's: a
        /// property name is a public interface (ADR 0003), the installed profile carries these names as
        /// literals, and a rename that re-pointed them would leave the profile reading properties nothing
        /// attaches.
        /// </remarks>
        public string Namespace { get; set; }

        /// <summary>The user's name for it. It names the row here and the profile in SimHub's own list.</summary>
        public string Name { get; set; }

        /// <summary>The generator's own shape id: "3-9-3", "brow-15", "4-14-4-reversed".</summary>
        public string Shape { get; set; }

        /// <summary>What the middle of the run shows. One of <see cref="Contract.LedCentres"/>.</summary>
        public string Centre { get; set; }

        /// <summary>How the rev ladder fills. One of <see cref="Contract.LedRpmStyles"/>. Deprecated by
        /// <see cref="CarRevBar"/> (#369) and kept because it is what an rc.4 settings file holds.</summary>
        public string RpmStyle { get; set; }

        /// <summary>
        /// Whether this bar's rev bar is the car's own measured one, or SimHub's.
        /// </summary>
        /// <remarks>
        /// Nullable, and that is what carries an rc.4 rig across: null means nobody has answered this
        /// question on this bar, so the answer is read off <see cref="RpmStyle"/> instead, where "car"
        /// is on and the three retired styles are off. A driver who touches the switch writes a value
        /// here and the old style stops deciding anything.
        /// </remarks>
        public bool? CarRevBar { get; set; }

        /// <summary>Whether a flag animates on this bar or simply holds.</summary>
        public bool FlagAnimation { get; set; } = Contract.DefaultLedFlagAnimation;

        /// <summary>Whether a car alongside lights this whole bar rather than the lamp at that end.
        /// Per bar, because a brow above a monitor has no ends to speak of and a rim does.</summary>
        public bool SpotterWhole { get; set; } = Contract.DefaultLedSpotterWhole;

        /// <summary>
        /// Which of SimHub's LED devices this bar's profile is installed into.
        /// </summary>
        /// <remarks>
        /// **There is no such thing as "SimHub's LED profiles".** Every LED device holds its own list:
        /// the Arduino RGB LEDs device has one, and each wheel, button plate or brow that SimHub knows
        /// as a device has one of its own, in its own file. A profile added to one is invisible to all
        /// the others, so a bar that did not say which device it was for was installed into whichever
        /// one openDash happened to name -- the Arduino's -- and a driver whose LEDs are in their wheel
        /// went looking for it in the wheel and found nothing. <see cref="LedTargets"/> is the list.
        ///
        /// <see cref="ArduinoDevice"/> is what a bar written before this existed is read as, because
        /// that is where those bars were actually installed. It is a statement about the past rather
        /// than a preference: a new bar takes whatever the panel offered.
        /// </remarks>
        public string Device { get; set; }

        /// <summary>SimHub's Arduino RGB LEDs, the device openDash installed into when it only knew one.</summary>
        public const string ArduinoDevice = "arduino";

        /// <summary>One device of SimHub's Devices plugin, by the instance id SimHub persists for it.</summary>
        public const string DevicePrefix = "device:";

        /// <summary>The id a bar records for a device instance. Round-tripped by <see cref="DeviceInstanceOf"/>.</summary>
        public static string DeviceId(Guid instanceId)
        {
            return DevicePrefix + instanceId.ToString("D", CultureInfo.InvariantCulture);
        }

        /// <summary>The instance behind a device id, or null when the id names something else or nothing.</summary>
        public static Guid? DeviceInstanceOf(string id)
        {
            if (id == null || !id.StartsWith(DevicePrefix, StringComparison.Ordinal)) return null;
            Guid parsed;
            return Guid.TryParseExact(id.Substring(DevicePrefix.Length), "D", out parsed) ? parsed : (Guid?)null;
        }

        /// <summary>A spellable device id: the Arduino's unless it is one openDash can recognise. An id
        /// it cannot read is not kept, because a bar pointed at nothing would silently install nowhere.</summary>
        public static string NormaliseDevice(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return ArduinoDevice;
            var trimmed = id.Trim();
            if (string.Equals(trimmed, ArduinoDevice, StringComparison.Ordinal)) return ArduinoDevice;
            return DeviceInstanceOf(trimmed) == null ? ArduinoDevice : trimmed;
        }

        /// <summary>Repairs the bar: a namespace that is spellable and three settings that are legal
        /// values, so a hand-edited file cannot reach a profile as itself.</summary>
        public void Normalise()
        {
            if (string.IsNullOrWhiteSpace(Name)) Name = Shape ?? "Strip";
            if (string.IsNullOrWhiteSpace(Namespace)) Namespace = "Led" + Contract.Slug(Name);
            Centre = Contract.NormaliseLedCentre(Centre);
            RpmStyle = Contract.NormaliseChoice(RpmStyle, Contract.LedRpmStyles, Contract.DefaultLedRpmStyle);
            Device = NormaliseDevice(Device);
        }

        public LedBar Copy()
        {
            return new LedBar
            {
                Namespace = Namespace,
                Name = Name,
                Shape = Shape,
                Centre = Centre,
                RpmStyle = RpmStyle,
                CarRevBar = CarRevBar,
                FlagAnimation = FlagAnimation,
                SpotterWhole = SpotterWhole,
                Device = Device,
            };
        }
    }
}
