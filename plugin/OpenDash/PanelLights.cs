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
