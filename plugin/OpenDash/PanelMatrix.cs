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
        public const string AnchorSpotterAnimation = "matrix.spotter-animation";

        // The rows' titles, which the page draws and search lists, so the two cannot drift apart.
        public const string SpotterAnimationTitle = "Spotter bar animation";
        public const string IdleDisplayTitle = "Idle display";
        public const string MountingSideTitle = "Mounting side";
        public const string CriticalFlagsOnlyTitle = "Critical flags only";
        public const string ShiftColoursTitle = "Shift colours";
        public const string RedlineFlashTitle = "Redline flash";

        /// <summary>The digit's colours following the car's own shift lights: "shift points", as voice.md's
        /// rulings name them; "thresholds" is the settings model's word, not the driver's.</summary>
        public const string CarShiftPointsTitle = "Car-specific shift points";

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(FlagBoxProfile.ProfileName, PanelPage.Matrix, AnchorProfile, "flag box profile", "install", "matrix profile", "8x8"),
            new PanelSearch.Entry(PanelLights.PanelsTitle, PanelPage.Matrix, AnchorPanels, "8x8", "flag box", "pillar"),
            new PanelSearch.Entry(PanelLights.AddPanel, PanelPage.Matrix, AnchorPanels, "new", "8x8"),
            new PanelSearch.Entry(SpotterAnimationTitle, PanelPage.Matrix, AnchorSpotterAnimation, "slide in"),
            new PanelSearch.Entry(IdleDisplayTitle, PanelPage.Matrix, AnchorPanels, "at rest", "gear", "dark"),
            new PanelSearch.Entry(MountingSideTitle, PanelPage.Matrix, AnchorPanels, "left", "right", "both"),
            new PanelSearch.Entry(CriticalFlagsOnlyTitle, PanelPage.Matrix, AnchorPanels, "chequer", "white", "green"),
            new PanelSearch.Entry(ShiftColoursTitle, PanelPage.Matrix, AnchorPanels, "gear", "revs"),
            new PanelSearch.Entry(RedlineFlashTitle, PanelPage.Matrix, AnchorPanels, "gear", "shift"),
            new PanelSearch.Entry(CarShiftPointsTitle, PanelPage.Matrix, AnchorPanels, "lovely", "car data", "thresholds"),
        };
    }
}
