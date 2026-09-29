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
            Assert.All(PanelLeds.Search, entry => Assert.StartsWith("leds.", entry.Route.Anchor, StringComparison.Ordinal));
            Assert.Contains(PanelLeds.Search, entry => entry.Label == PanelLights.BarsTitle && entry.Route.Anchor == PanelLeds.AnchorStrips);
            Assert.Contains(PanelLeds.Search, entry => entry.Label == PanelLights.CarTablesTitle && entry.Route.Anchor == PanelLeds.AnchorCarTables);
        }
    }
}
