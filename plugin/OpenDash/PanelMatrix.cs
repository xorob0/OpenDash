// PanelMatrix.cs: the Matrix page's words -- its title and what search finds on it.
//
// One card per panel with the flag box profile's state in the page header, the family switches, the idle
// display and a live 8x8 preview (PanelEmulation.MatrixFrame). The matrix copy that already existed stays in
// PanelLights; the Matrix page agent owns this file and adds the rest. Pure: no WPF.
namespace OpenDashPlugin
{
    public static class PanelMatrix
    {
        public const string Title = "Matrix";

        public const string AnchorProfile = "matrix.profile";
        public const string AnchorPanels = "matrix.panels";
        public const string AnchorLowFuel = "matrix.low-fuel";
        public const string AnchorSpotterAnimation = "matrix.spotter-animation";

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry("Flag box profile", PanelPage.Matrix, AnchorProfile, "install", "matrix profile", "8x8"),
            new PanelSearch.Entry(PanelLights.PanelsTitle, PanelPage.Matrix, AnchorPanels, "8x8", "flag box", "pillar"),
            new PanelSearch.Entry(PanelLights.AddPanel, PanelPage.Matrix, AnchorPanels, "new", "8x8"),
            new PanelSearch.Entry("Low fuel warning", PanelPage.Matrix, AnchorLowFuel, "laps", "fuel"),
            new PanelSearch.Entry("Spotter bar animation", PanelPage.Matrix, AnchorSpotterAnimation, "slide in"),
            new PanelSearch.Entry("Idle display", PanelPage.Matrix, AnchorPanels, "at rest", "gear", "dark"),
            new PanelSearch.Entry("Mounting side", PanelPage.Matrix, AnchorPanels, "left", "right", "both"),
            new PanelSearch.Entry("Critical flags only", PanelPage.Matrix, AnchorPanels, "chequer", "white", "green"),
            new PanelSearch.Entry("Shift colours", PanelPage.Matrix, AnchorPanels, "gear", "revs"),
            new PanelSearch.Entry("Redline flash", PanelPage.Matrix, AnchorPanels, "gear", "shift"),
            new PanelSearch.Entry("Car-specific thresholds", PanelPage.Matrix, AnchorPanels, "lovely", "car data"),
        };
    }
}
