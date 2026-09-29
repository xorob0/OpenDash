// PanelLeds.cs: the LEDs page's words -- its title and what search finds on it.
//
// One card per strip: the SimHub device it is on, whether it uses the car's own rev lights (#369), what the
// centre shows, a brightness of its own, its wiring direction, and a switch for every effect (#370). The
// strip copy that already existed stays in PanelLights and PanelLightRows; the LEDs page agent owns this
// file and adds the rest. Pure: no WPF.
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

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(PanelLights.BarsTitle, PanelPage.Leds, AnchorStrips, "strip", "wheel", "rim", "brow"),
            new PanelSearch.Entry(PanelLights.AddBar, PanelPage.Leds, AnchorStrips, "strip", "new"),
            new PanelSearch.Entry(PanelLights.BarDeviceTitle, PanelPage.Leds, AnchorDevice, "device", "arduino", "wheel"),
            new PanelSearch.Entry("Centre display", PanelPage.Leds, AnchorCentre, "middle", "brake", "throttle", "fuel"),
            new PanelSearch.Entry("Rev light style", PanelPage.Leds, AnchorRevStyle, "car-specific", "shift lights", "rpm"),
            new PanelSearch.Entry("Flag animation", PanelPage.Leds, AnchorFlagAnimation, "flags"),
            new PanelSearch.Entry("Full-strip spotter", PanelPage.Leds, AnchorSpotter, "car alongside"),
            new PanelSearch.Entry("Car shift light width", PanelPage.Leds, AnchorMirrorFit, "stretch", "true size"),
            new PanelSearch.Entry(PanelLights.CarTablesTitle, PanelPage.Leds, AnchorCarTables, "lovely", "car data", "download"),
        };
    }
}
