// PanelNavTitlesTests.cs: every sidebar item is the title of the page it opens.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelPageTitlesTests
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
    }
}
