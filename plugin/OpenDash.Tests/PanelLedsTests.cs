// PanelLedsTests.cs: the LEDs page's title and where search sends a driver on it.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelLedsTests
    {
        [Fact]
        public void The_leds_page_is_titled_and_its_rows_are_found_where_they_are()
        {
            Assert.Equal("LEDs", PanelLeds.Title);
            // #369: one switch where there was a four-way chooser.
            Assert.Equal("Car's own rev lights", PanelLeds.CarRevLightsTitle);
            Assert.Equal("Off fills the strip left to right.", PanelLeds.CarRevLightsCaption);
            Assert.Equal("Centre display", PanelLeds.CentreDisplayTitle);
            Assert.Equal("Flag animation", PanelLeds.FlagAnimationTitle);
            Assert.Equal("Full-strip spotter", PanelLeds.SpotterTitle);
            Assert.Equal("Car shift light width", PanelLeds.MirrorFitTitle);
            Assert.Equal("Only for a strip using the car's own rev lights.", PanelLeds.MirrorFitCaption);
            Assert.Equal("Every strip", PanelLeds.EveryStripTitle);
            foreach (var title in new[] { PanelLeds.CarRevLightsTitle, PanelLeds.CentreDisplayTitle, PanelLeds.FlagAnimationTitle, PanelLeds.SpotterTitle, PanelLeds.MirrorFitTitle })
            {
                Assert.Contains(PanelLeds.Search, entry => entry.Label == title);
            }
            Assert.All(PanelLeds.Search, entry => Assert.StartsWith("leds.", entry.Route.Anchor, StringComparison.Ordinal));
            Assert.Contains(PanelLeds.Search, entry => entry.Label == PanelLights.BarsTitle && entry.Route.Anchor == PanelLeds.AnchorStrips);
            Assert.Contains(PanelLeds.Search, entry => entry.Label == PanelLights.CarTablesTitle && entry.Route.Anchor == PanelLeds.AnchorCarTables);
        }
    }
}
