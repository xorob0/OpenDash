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
            Assert.Equal("Flags and spotter", PanelHome.TryTitle);
            Assert.Equal("Open Rig", PanelHome.OpenRig);
            Assert.Contains(PanelHome.Search, entry => entry.Label == PanelHome.RightNowTitle && entry.Route.Anchor == PanelHome.AnchorRightNow);
            Assert.Contains(PanelHome.Search, entry => entry.Label == PanelHome.QuickControlsTitle && entry.Route.Anchor == PanelHome.AnchorQuickControls);
            // Home's headline changes with the count, so its entry is the page's own name, which it draws over it.
            Assert.Contains(PanelHome.Search, entry => entry.Label == PanelHome.Title && entry.Route.Anchor == null && System.Array.IndexOf(entry.Keywords, "things to fix") >= 0);
            Assert.DoesNotContain(PanelHome.Search, entry => entry.Label == "Things to fix");
        }

        /// <summary>The page's anchor ids, which search, Home's fix rows and the capture scripts route to: a
        /// renamed one sends each of them to the page's top, so every id is pinned, and a new one is added here.</summary>
        [Fact]
        public void Its_anchor_ids_are_pinned()
        {
            Assert.Equal(new[]
            {
                "AnchorAttention = home.attention",
                "AnchorQuickControls = home.quick-controls",
                "AnchorRightNow = home.right-now",
            }, AnchorTable.Of(typeof(PanelHome)));
        }
    }
}
