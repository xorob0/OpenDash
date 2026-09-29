// PanelHome.cs: the Home page's words -- its title, its sections, and what search finds on it.
//
// Home says what needs fixing (PanelAttention decides that), what each device is showing, and the two
// controls a driver reaches for between sessions. The Home page agent owns this file and adds to it.
// Pure: no WPF.
namespace OpenDashPlugin
{
    public static class PanelHome
    {
        public const string Title = "Home";

        public const string RightNowTitle = "Right now";
        public const string QuickControlsTitle = "Quick controls";

        public const string AnchorAttention = "home.attention";
        public const string AnchorRightNow = "home.right-now";
        public const string AnchorQuickControls = "home.quick-controls";

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry("Things to fix", PanelPage.Home, AnchorAttention, "attention", "problem", "warning"),
            new PanelSearch.Entry(RightNowTitle, PanelPage.Home, AnchorRightNow, "live", "showing"),
            new PanelSearch.Entry(QuickControlsTitle, PanelPage.Home, AnchorQuickControls, "brightness", "night mode"),
        };
    }
}
