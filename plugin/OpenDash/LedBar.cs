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
// What stays rig-wide stays rig-wide. Night mode and the low-fuel threshold are one answer for the rig,
// and so is the car's own shift pattern, which is the car's rather than the strip's. Brightness moved in
// #503: a strip in the driver's eyeline and a brow above the monitor are not comfortable at one level, so
// a bar may carry its own and falls back to the rig's when it does not -- and night mode still wins over a
// bar turned up for daylight. What a bar owns is what its LEDs do with all that: how bright, which way
// round it is wired, whether it is wired in a Fanatec wheel's order, and which of the things it can draw it
// draws.
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// The generator's id for the plain shape: "3-9-3", "0-18-0". Never a `-reversed` or `-fanatec`
        /// twin once Normalise has run: how the strip is wired is <see cref="Reversed"/> and
        /// <see cref="Fanatec"/>, and the profile it installs is <see cref="ProfileShapeId"/>.
        /// </summary>
        public string Shape { get; set; }

        /// <summary>What the middle of the run shows. One of <see cref="Contract.LedCentres"/>.</summary>
        public string Centre { get; set; }

        /// <summary>How the rev ladder fills. One of <see cref="Contract.LedRpmStyles"/>.</summary>
        public string RpmStyle { get; set; }

        /// <summary>Whether a flag animates on this bar or simply holds.</summary>
        public bool FlagAnimation { get; set; } = Contract.DefaultLedFlagAnimation;

        /// <summary>Whether a car alongside lights this whole bar rather than the lamp at that end.
        /// Per bar, because a brow above a monitor has no ends to speak of and a rim does.</summary>
        public bool SpotterWhole { get; set; } = Contract.DefaultLedSpotterWhole;

        /// <summary>Whether this bar's traction control and ABS lamps also light on the plugin's estimate of
        /// wheelspin and lock-up (<see cref="Contract.LedInferSlip"/>). On by default, and a bar saved before
        /// the switch existed loads with it on.</summary>
        public bool InferSlip { get; set; } = Contract.DefaultLedInferSlip;

        /// <summary>
        /// Whether the bar is wired from the far end, so its profile is the shape's reversed twin.
        /// </summary>
        /// <remarks>
        /// A wiring and not a shape: a 4/14/4 wired from either end is the same strip, and the LEDs page
        /// says so with one switch rather than two rows. The generator writes a `-reversed` twin of every
        /// plain shape, and <see cref="ProfileShapeId"/> is which of the two a bar installs. A bar in the
        /// Fanatec wiring cannot be reversed: that order is the device's own. #503.
        /// </remarks>
        public bool Reversed { get; set; }

        /// <summary>
        /// Whether the bar is a Fanatec wheel's LEDs in the order SimHub's Fanatec device presents them, so
        /// its profile is the shape's `-fanatec` twin: the panel's Fanatec compatibility mode.
        /// </summary>
        /// <remarks>
        /// A wiring and not a shape, as <see cref="Reversed"/> is. The device presents a rim's rev LEDs first
        /// and its flag LEDs after, so the plain profile on a Fanatec wheel lights only some of its LEDs and
        /// starts the bar from the middle of the rim (#436). The generator writes the Fanatec twin of every
        /// plain A/B/A with ends; the Add LEDs sheet ticks this for a device whose name says Fanatec and
        /// leaves it to the driver otherwise, and the strip's own settings turn it on and off. A bar written
        /// before the switch carried the wiring in its shape, `3-9-3-fanatec`, which Normalise reads off.
        /// </remarks>
        public bool Fanatec { get; set; }

        /// <summary>The bar's own brightness in percent, or null to follow the rig's.</summary>
        public int? Brightness { get; set; }

        /// <summary>
        /// Where the Rig page's canvas draws this strip's tile, as ScreenInstance.LayoutX and LayoutY are
        /// for a screen: the panel's to read and write, never a property. Null, or a negative position the
        /// canvas cannot draw, is a tile the page lays out itself.
        /// </summary>
        public int? LayoutX { get; set; }

        public int? LayoutY { get; set; }

        /// <summary>
        /// The effects this bar does not draw, by effect id; everything else it draws. A switch is stored
        /// under the first id its setting answers for (<see cref="Contract.LedEffectPrimaryId"/>), so the
        /// ten flag rows are one entry. Null or empty is every effect on.
        /// </summary>
        public List<string> EffectsOff { get; set; }

        /// <summary>
        /// The colours this bar draws in place of the defaults, by <see cref="Contract.LedColours"/> key, as
        /// "#RRGGBB" (#694). A key it leaves out is drawn in its default, so null or empty is every default.
        /// </summary>
        /// <remarks>
        /// Not a property the profile reads: SimHub binds no colour on an LED container that blinks, so the
        /// colours are written into the profile the bar installs (<see cref="LedBarProfile.For"/>), and choosing
        /// one installs it again.
        /// </remarks>
        public Dictionary<string, string> Colours { get; set; }

        /// <summary>Whether this bar's shape has a reversed twin it may install: a plain shape of the grid,
        /// not one already carrying a wiring suffix, and not a pre-grid `brow-N`, which the generator never
        /// wrote a twin of (a bare run's twin is `0-N-0-reversed`); and not while the bar is in the Fanatec
        /// wiring, whose order is the device's and has no far end.</summary>
        public bool SupportsReversal
        {
            get { return PlainShape && !(Fanatec && SupportsFanatec); }
        }

        /// <summary>
        /// Whether this bar's shape can be in the Fanatec wiring: a plain A/B/A with ends, the shapes the
        /// generator writes a `-fanatec` twin of. A bare run has no flag LEDs for the order to move.
        /// </summary>
        /// <remarks>
        /// Read off the id alone, so the bar can say it without the build. The one geometry this admits that
        /// the generator gives no twin is the GridSim 3/10/3, whose extra runs are no Fanatec device's; the
        /// panel offers the switch only where the build embedded the twin (PanelLights.HasFanatecTwin).
        /// </remarks>
        public bool SupportsFanatec
        {
            get
            {
                if (!PlainShape) return false;
                var shape = LightShape.Parse(Shape);
                return shape != null && shape.Placement == PanelLightRows.Wheel && shape.Left > 0 && shape.Left == shape.Right;
            }
        }

        /// <summary>A shape id with no wiring suffix of its own and not a pre-grid `brow-N`.</summary>
        private bool PlainShape
        {
            get
            {
                if (string.IsNullOrEmpty(Shape)) return false;
                return !Shape.EndsWith("-" + PanelLightRows.ReversedSuffix, StringComparison.Ordinal)
                    && !Shape.EndsWith("-" + PanelLightRows.FanatecSuffix, StringComparison.Ordinal)
                    && !Shape.StartsWith(PanelLightRows.BrowPrefix, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// The shape id of the profile this bar installs: the Fanatec twin while <see cref="Fanatec"/> is on
        /// and the shape has one, else the reversed twin while <see cref="Reversed"/> is on and the shape has
        /// one, else the shape itself. What the embedded profile is looked up by.
        /// </summary>
        public string ProfileShapeId
        {
            get
            {
                if (Fanatec && SupportsFanatec) return Shape + "-" + PanelLightRows.FanatecSuffix;
                return Reversed && SupportsReversal ? Shape + "-" + PanelLightRows.ReversedSuffix : Shape;
            }
        }

        /// <summary>Whether this bar draws an effect. An id no switch answers for is drawn.</summary>
        public bool EffectEnabled(string effectId)
        {
            if (EffectsOff == null || EffectsOff.Count == 0 || !Contract.IsLedEffect(effectId)) return true;
            var setting = Contract.LedEffectSetting(effectId);
            foreach (var off in EffectsOff)
            {
                if (Contract.IsLedEffect(off) && string.Equals(Contract.LedEffectSetting(off), setting, StringComparison.Ordinal)) return false;
            }
            return true;
        }

        /// <summary>The colour this bar draws <paramref name="key"/> in: its own, or the default. Null for a key
        /// no colour setting has.</summary>
        public string ColourOf(string key)
        {
            var colour = Contract.FindLedColour(key);
            if (colour == null) return null;
            string own;
            if (Colours != null && Colours.TryGetValue(key, out own))
            {
                var hex = Contract.NormaliseLedHex(own);
                if (hex != null) return hex;
            }
            return colour.DefaultHex;
        }

        /// <summary>Whether this bar draws <paramref name="key"/> in a colour of its own rather than the default.</summary>
        public bool HasOwnColour(string key)
        {
            var colour = Contract.FindLedColour(key);
            return colour != null && !string.Equals(ColourOf(key), colour.DefaultHex, StringComparison.Ordinal);
        }

        /// <summary>
        /// Gives <paramref name="key"/> a colour of this bar's own, or takes it back to the default where
        /// <paramref name="hex"/> is null or is the default. Returns whether what the bar draws changed, which is
        /// whether its profile wants installing again. A key no colour setting has, or a colour that is not one,
        /// changes nothing.
        /// </summary>
        public bool SetColour(string key, string hex)
        {
            var colour = Contract.FindLedColour(key);
            if (colour == null) return false;
            var normal = hex == null ? null : Contract.NormaliseLedHex(hex);
            if (hex != null && normal == null) return false;
            var before = ColourOf(key);
            var colours = Colours ?? new Dictionary<string, string>(StringComparer.Ordinal);
            if (normal == null || string.Equals(normal, colour.DefaultHex, StringComparison.Ordinal)) colours.Remove(key);
            else colours[key] = normal;
            Colours = colours;
            return !string.Equals(before, ColourOf(key), StringComparison.Ordinal);
        }

        /// <summary>Turns one effect's switch on or off; for a flag row, every flag row's.</summary>
        public void SetEffect(string effectId, bool enabled)
        {
            var setting = Contract.LedEffectSetting(effectId);
            var kept = new List<string>();
            foreach (var off in EffectsOff ?? new List<string>())
            {
                if (!Contract.IsLedEffect(off) || string.Equals(Contract.LedEffectSetting(off), setting, StringComparison.Ordinal)) continue;
                kept.Add(off);
            }
            if (!enabled) kept.Add(Contract.LedEffectPrimaryId(setting));
            EffectsOff = kept;
        }

        /// <summary>
        /// Which of SimHub's LED devices this bar's profile is installed into.
        /// </summary>
        /// <remarks>
        /// **There is no such thing as "SimHub's LED profiles".** Every LED device holds its own list:
        /// the Arduino RGB LEDs device has one, and each wheel, button plate or brow that SimHub knows
        /// as a device has one of its own, in its own file. A profile added to one is invisible to all
        /// the others, so a bar that did not say which device it was for was installed into whichever
        /// one OpenDash happened to name -- the Arduino's -- and a driver whose LEDs are in their wheel
        /// went looking for it in the wheel and found nothing. <see cref="LedTargets"/> is the list.
        ///
        /// <see cref="ArduinoDevice"/> is what a bar written before this existed is read as, because
        /// that is where those bars were actually installed. It is a statement about the past rather
        /// than a preference: a new bar takes whatever the panel offered.
        /// </remarks>
        public string Device { get; set; }

        /// <summary>SimHub's Arduino RGB LEDs, the device OpenDash installed into when it only knew one.</summary>
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

        /// <summary>A spellable device id: the Arduino's unless it is one OpenDash can recognise. An id
        /// it cannot read is not kept, because a bar pointed at nothing would silently install nowhere.</summary>
        public static string NormaliseDevice(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return ArduinoDevice;
            var trimmed = id.Trim();
            if (string.Equals(trimmed, ArduinoDevice, StringComparison.Ordinal)) return ArduinoDevice;
            return DeviceInstanceOf(trimmed) == null ? ArduinoDevice : trimmed;
        }

        /// <summary>Repairs the bar: a namespace that is spellable and settings that are legal values,
        /// so a hand-edited file cannot reach a profile as itself.</summary>
        public void Normalise()
        {
            // A reversed shape is the plain one wired from the far end, which is a switch on the bar now:
            // the 4/14/4 that shipped as a shape of its own migrates onto its sibling with the switch on.
            var reversedSuffix = "-" + PanelLightRows.ReversedSuffix;
            if (Shape != null && Shape.EndsWith(reversedSuffix, StringComparison.Ordinal) && Shape.Length > reversedSuffix.Length)
            {
                Shape = Shape.Substring(0, Shape.Length - reversedSuffix.Length);
                Reversed = true;
            }
            // And the Fanatec wiring, which a bar written before the switch carried in its shape: `3-9-3-fanatec`
            // is the 3/9/3 with the switch on, and installs the same profile it always did.
            var fanatecSuffix = "-" + PanelLightRows.FanatecSuffix;
            if (Shape != null && Shape.EndsWith(fanatecSuffix, StringComparison.Ordinal) && Shape.Length > fanatecSuffix.Length)
            {
                Shape = Shape.Substring(0, Shape.Length - fanatecSuffix.Length);
                Fanatec = true;
            }
            if (!SupportsFanatec) Fanatec = false;
            if (!SupportsReversal) Reversed = false;
            if (string.IsNullOrWhiteSpace(Name)) Name = Shape ?? "Strip";
            if (string.IsNullOrWhiteSpace(Namespace)) Namespace = "Led" + Contract.Slug(Name);
            Centre = Contract.NormaliseLedCentre(Centre);
            RpmStyle = Contract.NormaliseLedRpmStyle(RpmStyle);
            Device = NormaliseDevice(Device);
            if (Brightness.HasValue) Brightness = Contract.NormaliseBrightness(Brightness.Value);
            if (LayoutX < 0) LayoutX = null;
            if (LayoutY < 0) LayoutY = null;
            var off = new List<string>();
            if (EffectsOff != null)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var id in EffectsOff)
                {
                    if (!Contract.IsLedEffect(id)) continue;
                    var setting = Contract.LedEffectSetting(id);
                    if (seen.Add(setting)) off.Add(Contract.LedEffectPrimaryId(setting));
                }
            }
            EffectsOff = off;
            // Only keys a colour setting has, in a colour that is one and is not the default: a hand-edited
            // file cannot reach a profile with a colour SimHub cannot read.
            var colours = new Dictionary<string, string>(StringComparer.Ordinal);
            if (Colours != null)
            {
                foreach (var pair in Colours)
                {
                    var colour = Contract.FindLedColour(pair.Key);
                    var hex = Contract.NormaliseLedHex(pair.Value);
                    if (colour == null || hex == null || string.Equals(hex, colour.DefaultHex, StringComparison.Ordinal)) continue;
                    colours[pair.Key] = hex;
                }
            }
            Colours = colours;
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
                FlagAnimation = FlagAnimation,
                SpotterWhole = SpotterWhole,
                InferSlip = InferSlip,
                Device = Device,
                Reversed = Reversed,
                Fanatec = Fanatec,
                Brightness = Brightness,
                LayoutX = LayoutX,
                LayoutY = LayoutY,
                EffectsOff = EffectsOff == null ? null : new List<string>(EffectsOff),
                Colours = Colours == null ? null : new Dictionary<string, string>(Colours, StringComparer.Ordinal),
            };
        }
    }
}
