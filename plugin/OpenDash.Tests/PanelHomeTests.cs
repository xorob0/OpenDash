// PanelHomeTests.cs: Home's own words, held to what search says about them.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelHomeTests
    {
        [Fact]
        public void Home_says_what_the_artboard_says()
        {
            Assert.Equal("Home", PanelHome.Title);
            Assert.Equal("Right now", PanelHome.RightNowTitle);
            Assert.Equal("Quick controls", PanelHome.QuickControlsTitle);
            Assert.Contains(PanelHome.Search, entry => entry.Label == PanelHome.RightNowTitle && entry.Route.Anchor == PanelHome.AnchorRightNow);
            Assert.Contains(PanelHome.Search, entry => entry.Label == PanelHome.QuickControlsTitle && entry.Route.Anchor == PanelHome.AnchorQuickControls);
        }
    }
}
