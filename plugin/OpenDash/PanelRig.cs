// PanelRig.cs: the line the Rig tab puts over its cards for a rig the migration made, and when it does.
//
// Apart from the WPF file for the reason PanelCopy.cs is apart from Widgets.cs: the panel is net48 and the
// net8.0 test project cannot compile a line of it, so the decision and its wording live where
// PanelRigTests can hold them against a rig built the way the plugin builds one. Pure: no WPF types.
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    public static class PanelRig
    {
        /// <summary>The line an upgrading user meets over the cards, and nobody else.</summary>
        public const string UnclaimedNote = "Remove the dashboards you have no screen for.";

        /// <summary>
        /// Whether the rig holds a screen the migration made that the driver has neither kept nor removed.
        /// </summary>
        /// <remarks>
        /// It used to be whether the rig held more than four screens, which says nothing about where a
        /// screen came from (#478). The screen carries that fact now (ScreenInstance.Unclaimed), so the line
        /// is shown however few screens the migration left and never to screens added from the Rig tab,
        /// however many. It goes on its own once the last such screen is kept or removed, which is when it
        /// stops being true, and there is nothing to dismiss.
        /// </remarks>
        public static bool ShowsUnclaimedNote(IEnumerable<ScreenInstance> rig)
        {
            return rig != null && rig.Any(screen => screen != null && screen.Unclaimed == true);
        }
    }
}
