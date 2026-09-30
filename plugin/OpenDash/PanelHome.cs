// PanelHome.cs: the Home page's words -- its title, its sections, and what search finds on it.
//
// Home says what needs fixing (PanelAttention decides that), what each device is showing, and the two
// controls a driver reaches for between sessions. The Home page agent owns this file and adds to it.
// Pure: no WPF.
using System.Collections.Generic;

namespace OpenDashPlugin
{
    public static class PanelHome
    {
        public const string Title = "Home";

        public const string RightNowTitle = "Right now";
        public const string QuickControlsTitle = "Quick controls";

        /// <summary>The quick controls' third cell: what the Rig page tries, and the press that opens it.</summary>
        public const string TryTitle = "Flags and spotter";
        public static readonly string OpenRig = PanelAttention.Open(PanelRigMap.Title);

        public const string AnchorAttention = "home.attention";
        public const string AnchorRightNow = "home.right-now";
        public const string AnchorQuickControls = "home.quick-controls";

        public static readonly PanelSearch.Entry[] Search =
        {
            // By the page's name, which it draws over its headline: the headline itself is "Nothing to fix", "1
            // thing to fix" or "3 things to fix" (PanelAttention.Headline), so no one label names it.
            new PanelSearch.Entry(Title, PanelPage.Home, null, "things to fix", "nothing to fix", "attention", "problem", "warning"),
            new PanelSearch.Entry(RightNowTitle, PanelPage.Home, AnchorRightNow, "live", "showing"),
            new PanelSearch.Entry(QuickControlsTitle, PanelPage.Home, AnchorQuickControls, "brightness", "night mode"),
        };

        /// <summary>The greyed rows this page draws (PanelSoon's named entries), which search lists unless one
        /// is InSheetOnly. PanelSoonTests holds the list to this page's own sources: draw a row, add it here.</summary>
        public static readonly SoonItem[] SoonDrawn = new SoonItem[0];

        /// <summary>Search labels this page draws through something other than the constant: none.</summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>();
    }
}
