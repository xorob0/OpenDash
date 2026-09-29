// PanelUpdatesTests.cs: the Updates page's headings.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelUpdatesTests
    {
        [Fact]
        public void Updates_says_what_the_artboard_says()
        {
            Assert.Equal("Updates", PanelUpdates.Title);
            Assert.Equal("This plugin", PanelUpdates.PluginTitle);
            Assert.Equal("Screens OpenDash can install", PanelUpdates.PackagesTitle);
            Assert.Equal("Lights OpenDash can install", PanelUpdates.LightsTitle);
            Assert.Equal("Support", PanelUpdates.LinksTitle);
            foreach (var title in new[] { PanelUpdates.PluginTitle, PanelUpdates.PackagesTitle, PanelUpdates.LightsTitle })
            {
                Assert.Contains(PanelUpdates.Search, entry => entry.Label == title);
            }
        }
    }
}
