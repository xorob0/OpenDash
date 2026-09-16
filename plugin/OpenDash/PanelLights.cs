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

        /// <summary>One label per <see cref="Contract.LedRpmStyles"/> value, in its order.</summary>
        public static readonly string[] RpmStyleLabels = { "Left to right", "Meet in middle", "F1" };

        /// <summary>One label per <see cref="Contract.FlagBoxRests"/> value, in its order.</summary>
        public static readonly string[] RestLabels = { "Dark", "Gear" };

        /// <summary>One label per <see cref="Contract.FlagBoxSides"/> value, in its order.</summary>
        public static readonly string[] SideLabels = { "Both", "Left", "Right" };

        /// <summary>The section the three rig-wide settings sit in, at the foot of the tab: they are not
        /// the flag box's, and a driver who owns a strip as well as a box sets them once.</summary>
        public const string RigWideTitle = "For every light";

        public const string RigWideCaption = "Brightness is one answer for the rig.";
    }
}
