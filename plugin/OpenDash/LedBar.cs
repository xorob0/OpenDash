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

        /// <summary>How the rev ladder fills. One of <see cref="Contract.LedRpmStyles"/>.</summary>
        public string RpmStyle { get; set; }

        /// <summary>Whether a flag animates on this bar or simply holds.</summary>
        public bool FlagAnimation { get; set; } = Contract.DefaultLedFlagAnimation;

        /// <summary>Whether a car alongside lights this whole bar rather than the lamp at that end.
        /// Per bar, because a brow above a monitor has no ends to speak of and a rim does.</summary>
        public bool SpotterWhole { get; set; } = Contract.DefaultLedSpotterWhole;

        /// <summary>Repairs the bar: a namespace that is spellable and three settings that are legal
        /// values, so a hand-edited file cannot reach a profile as itself.</summary>
        public void Normalise()
        {
            if (string.IsNullOrWhiteSpace(Name)) Name = Shape ?? "Strip";
            if (string.IsNullOrWhiteSpace(Namespace)) Namespace = "Led" + Contract.Slug(Name);
            Centre = Contract.NormaliseLedCentre(Centre);
            RpmStyle = Contract.NormaliseChoice(RpmStyle, Contract.LedRpmStyles, Contract.DefaultLedRpmStyle);
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
            };
        }
    }
}
