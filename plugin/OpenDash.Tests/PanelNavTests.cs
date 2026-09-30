// PanelNavTests.cs: the sidebar's items, their counts, their dots and the badge at the foot.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelNavTests
    {
        [Fact]
        public void The_sidebar_lists_the_pages_in_the_artboards_order_with_Updates_at_the_foot()
        {
            Assert.Equal(new[] { "Home", "Rig", "Screens", "LEDs", "Matrix", "Shortcuts", "Settings" }, PanelNav.Pages.Select(PanelNav.Label));
            Assert.DoesNotContain(PanelPage.Updates, PanelNav.Pages);
            Assert.Equal("Updates", PanelNav.Label(PanelPage.Updates));
            // Every page the enum has is listed or pinned, and in the enum's order.
            Assert.Equal(Enum.GetValues(typeof(PanelPage)).Cast<PanelPage>().Take(7), PanelNav.Pages);
        }

        [Fact]
        public void The_labels_are_sentence_case()
        {
            foreach (var page in Enum.GetValues(typeof(PanelPage)).Cast<PanelPage>())
            {
                var label = PanelNav.Label(page);
                // LEDs is an acronym and keeps its capitals; nothing else is shouted.
                if (page != PanelPage.Leds) Assert.Equal(label.Substring(0, 1) + label.Substring(1).ToLowerInvariant(), label);
            }
        }

        [Fact]
        public void Every_page_has_its_own_icon()
        {
            var icons = Enum.GetValues(typeof(PanelPage)).Cast<PanelPage>().Select(PanelNav.Icon).ToList();
            Assert.Equal(icons.Count, icons.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(PanelIcons.Home, PanelNav.Icon(PanelPage.Home));
            Assert.Equal(PanelIcons.Updates, PanelNav.Icon(PanelPage.Updates));
        }

        [Fact]
        public void A_rule_follows_Rig_and_Matrix()
        {
            Assert.Equal(new[] { PanelPage.Rig, PanelPage.Matrix }, PanelNav.Pages.Where(PanelNav.GapAfter));
        }

        [Fact]
        public void The_devices_are_counted_and_nothing_else_is()
        {
            Assert.Equal("5", PanelNav.Count(PanelPage.Screens, 5, 2, 3, 4));
            Assert.Equal("2", PanelNav.Count(PanelPage.Leds, 5, 2, 3, 4));
            Assert.Equal("3", PanelNav.Count(PanelPage.Matrix, 5, 2, 3, 4));
            Assert.Equal("4", PanelNav.Count(PanelPage.Shortcuts, 5, 2, 3, 4));
            Assert.Null(PanelNav.Count(PanelPage.Home, 5, 2, 3, 4));
            Assert.Null(PanelNav.Count(PanelPage.Rig, 5, 2, 3, 4));
            Assert.Null(PanelNav.Count(PanelPage.Settings, 5, 2, 3, 4));
            Assert.Null(PanelNav.Count(PanelPage.Updates, 5, 2, 3, 4));
        }

        [Fact]
        public void An_empty_count_reads_zero_and_only_an_unreadable_one_draws_nothing()
        {
            // How many the rig has, none included, and nothing bound is a count the driver can read.
            Assert.Equal("0", PanelNav.Count(PanelPage.Screens, 0, 0, 0, 0));
            Assert.Equal("0", PanelNav.Count(PanelPage.Leds, 0, 0, 0, 0));
            Assert.Equal("0", PanelNav.Count(PanelPage.Matrix, 0, 0, 0, 0));
            Assert.Equal("0", PanelNav.Count(PanelPage.Shortcuts, 0, 0, 0, 0));
            // The bindings could not be read: hidden, never guessed, and not the same as none bound.
            Assert.Null(PanelNav.Count(PanelPage.Shortcuts, 1, 1, 1, null));
        }

        [Fact]
        public void A_rail_item_says_what_its_dot_means()
        {
            Assert.Equal("Screens · 5", PanelNav.RailTooltip(PanelPage.Screens, "5", null, false));
            Assert.Equal("Screens · 5 · Needs attention", PanelNav.RailTooltip(PanelPage.Screens, "5", null, true));
            Assert.Equal("Updates · Restart · Needs attention", PanelNav.RailTooltip(PanelPage.Updates, null, PanelNav.RestartBadge, true));
            Assert.Equal("Home", PanelNav.RailTooltip(PanelPage.Home, null, null, false));
        }

        [Fact]
        public void An_item_wears_the_dot_only_when_its_page_has_something_to_fix()
        {
            var issues = new List<PanelIssue>
            {
                new PanelIssue("a", PanelPage.Leds, null, "t", null, null, "Check again", PanelIssueAction.CheckAgain),
            };
            Assert.True(PanelNav.Warns(PanelPage.Leds, issues));
            Assert.False(PanelNav.Warns(PanelPage.Screens, issues));
            Assert.False(PanelNav.Warns(PanelPage.Leds, null));
        }

        [Fact]
        public void Updates_carries_the_restart_before_the_offer()
        {
            Assert.Equal("Restart", PanelNav.UpdatesBadge(true, true, "0.5.1"));
            Assert.Equal("0.5.1", PanelNav.UpdatesBadge(false, true, " 0.5.1 "));
            Assert.Null(PanelNav.UpdatesBadge(false, false, "0.5.1"));
            Assert.Null(PanelNav.UpdatesBadge(false, true, null));
        }
    }
}
