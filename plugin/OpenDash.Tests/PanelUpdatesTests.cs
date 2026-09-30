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
            Assert.Equal("Check for updates", PanelUpdates.CheckTitle);
            Assert.Equal("Put mine back", PanelUpdates.PutMineBack);
            Assert.Equal("Documentation", PanelUpdates.DocumentationLink);
            Assert.Equal("Report an issue", PanelUpdates.ReportIssueLink);
            Assert.Equal("Reinstall", PanelConfirmation.ReinstallLabel);
            Assert.Equal("Update", PanelConfirmation.UpdateLabel);
            foreach (var title in new[] { PanelUpdates.PluginTitle, PanelUpdates.PackagesTitle, PanelUpdates.LightsTitle })
            {
                Assert.Contains(PanelUpdates.Search, entry => entry.Label == title);
            }
        }

        /// <summary>The page's anchor ids, which search, Home's fix rows and the capture scripts route to: a
        /// renamed one sends each of them to the page's top, so every id is pinned, and a new one is added here.</summary>
        [Fact]
        public void Its_anchor_ids_are_pinned()
        {
            Assert.Equal(new[]
            {
                "AnchorCheck = updates.check",
                "AnchorLights = updates.lights",
                "AnchorLinks = updates.links",
                "AnchorPackages = updates.packages",
                "AnchorPlugin = updates.plugin",
            }, AnchorTable.Of(typeof(PanelUpdates)));
        }
    }
}
