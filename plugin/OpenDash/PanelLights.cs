// PanelLights.cs: the words the Lights tab puts beside a value, and the section that answers for the
// whole rig.
//
// Apart from SettingsControl.Lights.cs for the reason PanelCopy.cs is apart from Widgets.cs: the tab is
// WPF and the net8.0 test project cannot compile a line of it, so what a test can hold has to live where
// it can reach. What it holds here is the pairing. BuildChoice and BuildSegmented are handed the values
// and the labels as two arrays and read them by index, so a value set that gains or loses one leaves the
// labels beside it wrong without saying so: the centre drop-down carried a label for a retired fifth
// value that way, and a segmented bar one label short throws while a tab is being drawn. Pure: no WPF
// types.
namespace OpenDashPlugin
{
    public static class PanelLights
    {
        /// <summary>One label per <see cref="Contract.LedCentres"/> value, in its order.</summary>
        public static readonly string[] CentreLabels = { "RPM", "Brake", "Throttle and brake", "Fuel" };

        /// <summary>One label per <see cref="Contract.LedRpmStyles"/> value, in its order. The car's own
        /// heads the list because the value does, and it is the one a driver is offered first.</summary>
        public static readonly string[] RpmStyleLabels = { "The car's own", "Left to right", "Meet in middle", "F1" };

        /// <summary>One label per <see cref="Contract.LedMirrorFits"/> value, in its order.</summary>
        public static readonly string[] MirrorFitLabels = { "Fill the strip", "True size" };

        /// <summary>One label per <see cref="Contract.FlagBoxRests"/> value, in its order.</summary>
        public static readonly string[] RestLabels = { "Dark", "Gear" };

        /// <summary>One label per <see cref="Contract.FlagBoxSides"/> value, in its order.</summary>
        public static readonly string[] SideLabels = { "Both", "Left", "Right" };

        /// <summary>The heading over the strips a driver has added.</summary>
        public const string BarsTitle = "Your LED bars";

        /// <summary>
        /// The line under it.
        /// </summary>
        /// <remarks>
        /// A strip used to be a shape the Install tab offered and nothing more, so two strips on one rig
        /// could be installed separately and not configured separately -- a wheel and a brow shared one
        /// answer to what their middles show. A bar is an instance now, the way a screen is: it has a
        /// name, a shape and settings of its own, and installing it is what puts a profile of that name
        /// into SimHub.
        /// </remarks>
        public const string BarsCaption =
            "An RGB strip on the wheel, the rim or above the monitor. Add one for each strip you have and give it a name; "
            + "openDash installs a profile under that name, and the settings under it are that bar's alone.";

        public const string NoBars = "No bars yet. Add one and openDash installs its profile into SimHub, ready to pick on the device.";

        public const string AddBar = "Add an LED bar";

        public const string BarNameTitle = "Call it";

        public const string BarNameCaption = "Yours. It names the group here and the profile in SimHub's own LED profile list.";

        public const string BarShapeTitle = "What shape is it";

        public const string BarShapeCaption = "How many LEDs, and how they are grouped: the run in the middle carries the revs and the groups at the ends are lamps.";

        /// <summary>What is said once a bar exists, which is the step SimHub does not take for you:
        /// installing adds a profile, it does not select one on the device.</summary>
        public static string BarAdded(string name)
        {
            return "Added " + name + " and installed its profile. Open your LED device in SimHub and select \"" + name + "\" on it.";
        }

        public static string BarAddFailed(string name)
        {
            return "Added " + name + ", but its profile could not be installed into SimHub. The Install tab says why.";
        }

        /// <summary>The heading over the panels a driver has added.</summary>
        public const string PanelsTitle = "Your matrix panels";

        /// <summary>
        /// The line under it, and what it says when there are none.
        /// </summary>
        /// <remarks>
        /// A rig starts with no panels, the way it starts with no screens. Four numbered groups of eleven
        /// settings, one of them switched on because it was first, is a page for hardware most people own
        /// none of; a panel exists because somebody said they have one, and it carries the name they gave
        /// it rather than its slot number.
        /// </remarks>
        public const string PanelsCaption =
            "SimHub composes up to four contents onto one matrix device. Add one for each panel you actually have, and "
            + "give it a name you will recognise — \"top left\", \"by the wheel\".";

        public const string NoPanels = "No panels yet. The flag box profile is installed and will draw nothing until you add one.";

        public const string AddPanel = "Add a matrix panel";

        public const string PanelNameTitle = "Call it";

        public const string PanelNameCaption = "Yours. It names the group here; the panel itself is whichever of SimHub's four contents this is.";

        /// <summary>What a panel's group says under its name: which of SimHub's four contents it is, since
        /// that is the number a driver has to match on the device itself.</summary>
        public static string PanelSlot(int matrix)
        {
            return "SimHub matrix " + matrix;
        }

        /// <summary>The section the three rig-wide settings sit in, at the foot of the tab: they are not
        /// the flag box's, and a driver who owns a strip as well as a box sets them once.</summary>
        public const string RigWideTitle = "For every light";

        public const string RigWideCaption = "Brightness is one answer for the rig.";
    }
}
