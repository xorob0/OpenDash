// PanelLeds.cs: the LEDs page's words -- its title and what search finds on it.
//
// One card per strip: the SimHub device it is on, whether it uses the car's own rev lights (#369), what the
// centre shows, a brightness of its own, its wiring direction, and a switch for every effect (#370). The
// strip copy that already existed stays in PanelLights and PanelLightRows; the LEDs page agent owns this
// file and adds the rest. Pure: no WPF.
using System.Collections.Generic;

namespace OpenDashPlugin
{
    public static class PanelLeds
    {
        public const string Title = "LEDs";

        public const string AnchorStrips = "leds.strips";
        public const string AnchorDevice = "leds.device";
        public const string AnchorCentre = "leds.centre";
        public const string AnchorRevStyle = "leds.rev-style";
        public const string AnchorFlagAnimation = "leds.flag-animation";
        public const string AnchorSpotter = "leds.spotter";
        public const string AnchorMirrorFit = "leds.mirror-fit";
        public const string AnchorCarTables = "leds.car-tables";

        // The rows' titles, which the page draws and search lists, so the two cannot drift apart.
        public const string CentreDisplayTitle = "Centre display";
        public const string FlagAnimationTitle = "Flag animation";
        public const string SpotterTitle = "Full-strip spotter";
        public const string MirrorFitTitle = "Car shift light width";

        /// <summary>The #369 switch: the car's own rev lights, or the plain ladder.</summary>
        public const string CarRevLightsTitle = "Car's own rev lights";
        public const string CarRevLightsCaption = "Off fills the strip left to right.";

        /// <summary>The section of what is the same for every strip, and the width row's caption in it.</summary>
        public const string EveryStripTitle = "Every strip";
        public const string MirrorFitCaption = "Only for a strip using the car's own rev lights.";

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(PanelLights.BarsTitle, PanelPage.Leds, AnchorStrips, "strip", "wheel", "rim", "brow"),
            new PanelSearch.Entry(PanelLights.AddBar, PanelPage.Leds, AnchorStrips, "strip", "new"),
            new PanelSearch.Entry(PanelLights.BarDeviceTitle, PanelPage.Leds, AnchorDevice, "device", "arduino", "wheel"),
            new PanelSearch.Entry(CentreDisplayTitle, PanelPage.Leds, AnchorCentre, "middle", "brake", "throttle", "fuel"),
            new PanelSearch.Entry(CarRevLightsTitle, PanelPage.Leds, AnchorRevStyle, "rev light style", "car-specific", "shift lights", "rpm"),
            new PanelSearch.Entry(FlagAnimationTitle, PanelPage.Leds, AnchorFlagAnimation, "flags"),
            new PanelSearch.Entry(SpotterTitle, PanelPage.Leds, AnchorSpotter, "car alongside"),
            new PanelSearch.Entry(MirrorFitTitle, PanelPage.Leds, AnchorMirrorFit, "stretch", "true size"),
            new PanelSearch.Entry(PanelLights.CarTablesTitle, PanelPage.Leds, AnchorCarTables, "lovely", "car data", "download"),
        };

        /// <summary>The greyed rows this page draws (PanelSoon's named entries), which search lists unless one
        /// is InSheetOnly. PanelSoonTests holds the list to this page's own sources: draw a row, add it here.</summary>
        public static readonly SoonItem[] SoonDrawn = new SoonItem[0];

        /// <summary>Search labels this page draws through something other than the constant: none.</summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>();
    }
}
