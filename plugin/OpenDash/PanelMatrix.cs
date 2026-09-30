// PanelMatrix.cs: the Matrix page's words -- its title and what search finds on it.
//
// One card per panel with the flag box profile's state in the page header, the family switches, the idle
// display and a live 8x8 preview (PanelEmulation.MatrixFrame). The matrix copy that already existed stays in
// PanelLights; the Matrix page agent owns this file and adds the rest. Pure: no WPF.
using System.Collections.Generic;

namespace OpenDashPlugin
{
    public static class PanelMatrix
    {
        public const string Title = "Matrix";

        /// <summary>The empty state, here and under Home's Matrix eyebrow: the Matrix page's own, as NoScreens
        /// is Screens', so ruling 59's one noun ("No matrices yet") is the Matrix agent's to apply. It was
        /// PanelLights.NoPanels, shared, which Home's read froze.</summary>
        public const string NoPanels = "No panels yet.";

        /// <summary>
        /// What the flag box profile's row in the Matrix page's header says and offers. The Matrix page's
        /// own table: it was PanelCopy.LightRow, which the Updates page's rows read too, so neither page could
        /// reword a state without changing the other's. Today it says what LightRow says.
        /// </summary>
        public static RowAction ProfileRow(FlagBoxInstallState state, string installedVersion)
        {
            switch (state)
            {
                case FlagBoxInstallState.Outdated:
                    return new RowAction(PanelCopy.InstalledAt(installedVersion), Theme.StatusUpToDate, "Update", PanelButton.Primary);
                case FlagBoxInstallState.UpToDate:
                    return new RowAction(PanelCopy.InstalledAt(installedVersion), Theme.StatusUpToDate, "Reinstall", PanelButton.Outline);
                case FlagBoxInstallState.Failed:
                    return new RowAction(PanelCopy.InstallFailed, Theme.StatusFailed, "Install", PanelButton.Outline);
                default:
                    return new RowAction(PanelCopy.NotInstalled, Theme.TextLabel, "Install", PanelButton.Outline);
            }
        }

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
        /// rulings name them, since "thresholds" is the settings model's word and not the driver's; and
        /// "Car-specific", the name voice.md gives the car's own tables, which is also what Lovely Sim Racing
        /// calls the tables behind them, so a driver who met them there recognises the word.</summary>
        public const string CarShiftPointsTitle = "Car-specific shift points";
        public const string CarShiftPointsCaption = "Colours change where this car's own lights do.";

        /// <summary>The section over the profile's panels, and the rows each panel has for what it shows.</summary>
        public const string FlagBoxTitle = "The flag box";
        public const string SpotterAnimationCaption = "The bar slides in from the edge.";
        public const string RaceFlagsTitle = "Race flags";
        public const string PitStatusTitle = "Pit status";
        public const string PitStatusCaption = "Limiter, pit lane and speeding.";
        public const string SpotterTitle = "Spotter";
        public const string SpotterCaption = "Warns about cars alongside.";
        public const string CarWarningsTitle = "Car warnings";
        public const string CarWarningsCaption = "Low fuel, oil and water.";

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
            new PanelSearch.Entry(RaceFlagsTitle, PanelPage.Matrix, AnchorPanels, "flags"),
            new PanelSearch.Entry(PitStatusTitle, PanelPage.Matrix, AnchorPanels, "limiter", "pit lane", "speeding"),
            new PanelSearch.Entry(CarWarningsTitle, PanelPage.Matrix, AnchorPanels, "fuel", "oil", "water"),
            new PanelSearch.Entry(CarShiftPointsTitle, PanelPage.Matrix, AnchorPanels, "lovely", "car data", "car-specific", "thresholds"),
        };

        /// <summary>The greyed rows this page draws (PanelSoon's named entries), which search lists unless one
        /// is InSheetOnly. PanelSoonTests holds the list to this page's own sources: draw a row, add it here.</summary>
        public static readonly SoonItem[] SoonDrawn = new SoonItem[0];

        /// <summary>
        /// Search labels this page draws through something other than the constant, each with the text its
        /// sources draw it by, so the list is a record rather than a way round PanelSearchTests.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            // The flag box row names the profile as SimHub lists it, FlagBoxName(), whose fallback is this.
            { FlagBoxProfile.ProfileName, "FlagBoxName()" },
        };
    }
}
