// PanelNavTitlesTests.cs: every sidebar item is the title of the page it opens.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelNavTitlesTests
    {
        /// <summary>The sidebar item and the heading of the page it opens are one string.</summary>
        [Fact]
        public void Every_sidebar_item_is_its_pages_title()
        {
            var titles = new Dictionary<PanelPage, string>
            {
                { PanelPage.Home, PanelHome.Title },
                { PanelPage.Rig, PanelRigMap.Title },
                { PanelPage.Screens, PanelScreens.Title },
                { PanelPage.Leds, PanelLeds.Title },
                { PanelPage.Matrix, PanelMatrix.Title },
                { PanelPage.Shortcuts, PanelShortcuts.Title },
                { PanelPage.Settings, PanelSettings.Title },
                { PanelPage.Updates, PanelUpdates.Title },
            };
            foreach (var page in Enum.GetValues(typeof(PanelPage)).Cast<PanelPage>())
            {
                Assert.Equal(titles[page], PanelNav.Label(page));
            }
        }

        /// <summary>The sidebar's and the search's own words, which no page draws.</summary>
        [Fact]
        public void The_sidebar_says_what_it_says_in_one_place()
        {
            Assert.Equal("Needs attention", PanelNav.WarnTooltip);
            Assert.Equal("Restart", PanelNav.RestartBadge);
            // The artboard's "Search settings" is "Search", which docs/design/plugin.md records.
            Assert.Equal("Search", PanelSearch.Placeholder);
            Assert.Equal("Searches every setting.", PanelSearch.RailTooltip);
            Assert.Equal("No setting matches.", PanelSearch.NoMatch);
            Assert.Equal("This page could not be drawn. See SimHub's log.", PanelShell.PageFailed);
            Assert.Equal("Not bound", PanelBindings.NotBound);
            Assert.Equal("Opens the Shortcuts page.", PanelBindings.ChipTooltip);
        }
    }
}
