// PanelShortcutsTests.cs: the Shortcuts page's words, its cards' rows in their order, the filter, the counts,
// the clash line and the geometry it takes from Shortcuts.dc.html.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelShortcutsTests
    {
        private const string Face = "Face1920x480";

        [Fact]
        public void Shortcuts_says_what_the_artboard_says()
        {
            Assert.Equal("Shortcuts", PanelShortcuts.Title);
            // Ruling 49's intro, and nothing the rows under it already show: no touch, and no mechanism.
            Assert.Equal("A wheel button, a button box or a key.", PanelShortcuts.IntroCaption);
            Assert.Equal("Lights", PanelShortcuts.RigGroupTitle);
            Assert.Equal("Every strip and matrix", PanelShortcuts.RigGroupDetail);
            Assert.Equal("Alerts", PanelShortcuts.AlertsGroupTitle);
            Assert.Equal("Quick glance", PanelShortcuts.QuickGlanceTitle);
            Assert.Equal("Tap", PanelShortcuts.Tap);
            Assert.Equal("Hold", PanelShortcuts.Hold);
            Assert.Equal("Band D · next page", PanelShortcuts.ZoneRow("Band D", true));
            Assert.Equal("Band D · previous page", PanelShortcuts.ZoneRow("Band D", false));
            // The binder's name in SimHub's list is the row's words after the screen's name.
            Assert.Equal("Rim · Band D · previous page", PanelShortcuts.ZoneBinderName("Rim", "Band D", false));
            Assert.Equal("Rim · quick glance", PanelShortcuts.GlanceBinderName("Rim"));
        }

        [Fact]
        public void A_card_names_its_screen_by_kind_and_size_as_a_screens_card_does()
        {
            Assert.Equal("Face · 1280 × 480", PanelShortcuts.GroupDetail(Contract.KindFace, 1280, 480));
            Assert.Equal("Pit wall · 1920 × 1080", PanelShortcuts.GroupDetail(Contract.KindPitWall, 1920, 1080));
            Assert.Equal("Companion · 480 × 850", PanelShortcuts.GroupDetail(Contract.KindCompanion, 480, 850));
            // A screen whose package is gone has no size to show, and "0 × 0" is worse than the kind alone.
            Assert.Equal("Face", PanelShortcuts.GroupDetail(Contract.KindFace, 0, 0));
        }

        [Fact]
        public void A_card_counts_its_bound_rows_and_a_card_of_greyed_rows_counts_nothing()
        {
            Assert.Equal("3 of 6", PanelShortcuts.Count(3, 6));
            Assert.Equal("0 of 3", PanelShortcuts.Count(0, 3));
            Assert.Null(PanelShortcuts.Count(0, 0));
        }

        [Fact]
        public void A_face_lists_every_zone_forward_then_back_then_the_glance()
        {
            var face = Contract.FaceSizes.First(size => size.Width == 1920 && size.Height == 480);
            var order = PanelFacePlan.ZoneOrder(face);
            var rows = PanelShortcuts.FaceBindings(Face, "Rim", order);
            var zones = order.Select(PanelFacePlan.ZoneLabel).ToList();
            Assert.Equal(zones.Select(z => z + " · next page").Concat(zones.Select(z => z + " · previous page")), rows.Select(r => r.Label));
            Assert.Equal(order.Select(l => Contract.CycleZoneAction(Face, l)).Concat(order.Select(l => Contract.CycleZoneBackAction(Face, l))), rows.Select(r => r.Action));
            Assert.Equal(rows.Select(r => "Rim · " + r.Label), rows.Select(r => r.BinderName));
            // The previous page rows are this release's, and they alone carry the New tag.
            Assert.Equal(zones.Select(z => false).Concat(zones.Select(z => true)), rows.Select(r => r.IsNew));
            Assert.All(rows, r => Assert.Equal("Tap", r.Press));

            var glance = PanelShortcuts.GlanceBinding(Face, "Rim");
            Assert.Equal("Quick glance", glance.Label);
            Assert.Equal("Hold", glance.Press);
            Assert.True(glance.IsHold);
            Assert.False(glance.IsNew);
            Assert.Equal("Rim · quick glance", glance.BinderName);

            // Every action the face registers has a row, and nothing else does: nine rows on a face.
            var drawn = rows.Select(r => r.Action).Concat(new[] { glance.Action }).OrderBy(a => a, StringComparer.Ordinal);
            Assert.Equal(Contract.ScreenActionNames(Contract.KindFace, Face).OrderBy(a => a, StringComparer.Ordinal), drawn);
            Assert.Equal(9, rows.Count + 1);
        }

        [Fact]
        public void A_face_of_unknown_size_still_lists_every_zone()
        {
            var rows = PanelShortcuts.FaceBindings(Face, "Rim", Contract.FaceZoneLetters);
            Assert.Equal(2 * Contract.FaceZoneLetters.Length, rows.Count);
            Assert.Equal("Zone A · next page", rows[0].Label);
            Assert.Equal("Band D · previous page", rows[rows.Count - 1].Label);
        }

        [Fact]
        public void A_pit_wall_and_a_companion_bind_their_glance_alone()
        {
            foreach (var kind in new[] { Contract.KindPitWall, Contract.KindCompanion })
            {
                var ns = kind == Contract.KindPitWall ? Contract.PitWallPrefix : Contract.CompanionPrefix;
                Assert.Equal(Contract.ScreenActionNames(kind, ns), new[] { PanelShortcuts.GlanceBinding(ns, "Wall").Action });
            }
        }

        [Fact]
        public void The_lights_card_binds_the_rig_actions_and_brightness_is_new()
        {
            var rows = PanelShortcuts.LightsBindings();
            Assert.Equal(Contract.RigActionNames(), rows.Select(r => r.Action));
            Assert.Equal(new[] { "Night mode", "Brightness up", "Brightness down" }, rows.Select(r => r.Label));
            Assert.Equal(new[] { false, true, true }, rows.Select(r => r.IsNew));
            Assert.All(rows, r => Assert.Equal("Tap", r.Press));
            // The rig test and the alert's dismissal are coming, and are the page's two greyed rows.
            Assert.Equal(new[] { PanelSoon.RigTest, PanelSoon.AlertDismissal }, PanelShortcuts.SoonDrawn);
            Assert.Equal(511, PanelSoon.RigTest.Ticket);
            Assert.Equal(510, PanelSoon.AlertDismissal.Ticket);
        }

        [Fact]
        public void Every_rig_action_has_a_label_that_is_not_its_id()
        {
            var actions = Contract.RigActionNames().ToList();
            Assert.NotEmpty(actions);
            foreach (var action in actions)
            {
                var label = PanelShortcuts.RigActionLabel(action);
                Assert.False(string.IsNullOrWhiteSpace(label), action);
                Assert.NotEqual(action, label);
                Assert.Contains(PanelShortcuts.Search, entry => entry.Label == label);
                Assert.NotEqual(action, PanelShortcuts.RigActionDoes(action));
            }
            Assert.Equal("Night mode", PanelShortcuts.RigActionLabel(Contract.ToggleNightModeAction));
            Assert.Equal("Brightness up", PanelShortcuts.RigActionLabel(Contract.BrightnessUpAction));
            Assert.Equal("Brightness down", PanelShortcuts.RigActionLabel(Contract.BrightnessDownAction));
        }

        [Fact]
        public void The_companion_paging_path_is_drawn_as_crumbs()
        {
            Assert.Equal(new[] { "Devices", "Phone", "Controls and events", "NextScreen" }, PanelShortcuts.PagingCrumbs("Phone"));
            // The crumbs are the path PanelCopy.CompanionPaging names in its sentence.
            Assert.Contains(PanelShortcuts.ControlsAndEventsCrumb, PanelCopy.CompanionPaging);
            Assert.Contains(PanelShortcuts.NextScreenCrumb, PanelCopy.CompanionPaging);
        }

        [Fact]
        public void The_filter_shows_all_bound_or_not_bound_and_a_greyed_row_only_under_all()
        {
            Assert.Equal(new[] { "All", "Bound", "Not bound" }, PanelShortcuts.FilterLabels);
            Assert.Equal(PanelShortcuts.FilterValues.Length, PanelShortcuts.FilterLabels.Length);
            Assert.Equal(PanelBindings.NotBound, PanelShortcuts.FilterLabels[2]);

            Assert.True(PanelShortcuts.Shows(PanelShortcuts.FilterAll, true, true));
            Assert.True(PanelShortcuts.Shows(PanelShortcuts.FilterAll, true, false));
            Assert.True(PanelShortcuts.Shows(PanelShortcuts.FilterAll, false, false));
            Assert.True(PanelShortcuts.Shows(PanelShortcuts.FilterBound, true, true));
            Assert.False(PanelShortcuts.Shows(PanelShortcuts.FilterBound, true, false));
            Assert.False(PanelShortcuts.Shows(PanelShortcuts.FilterBound, false, false));
            Assert.False(PanelShortcuts.Shows(PanelShortcuts.FilterNotBound, true, true));
            Assert.True(PanelShortcuts.Shows(PanelShortcuts.FilterNotBound, true, false));
            Assert.False(PanelShortcuts.Shows(PanelShortcuts.FilterNotBound, false, false));
        }

        [Fact]
        public void The_filter_never_hides_the_row_the_page_was_opened_on_and_is_gone_when_nothing_can_be_read()
        {
            Assert.Equal(PanelShortcuts.FilterBound, PanelShortcuts.FilterFor(PanelShortcuts.FilterBound, null, true));
            Assert.Equal(PanelShortcuts.FilterAll, PanelShortcuts.FilterFor(PanelShortcuts.FilterBound, PanelBindings.Anchor("BrightnessUp"), true));
            Assert.Equal(PanelShortcuts.FilterAll, PanelShortcuts.FilterFor(PanelShortcuts.FilterNotBound, null, false));
            Assert.Equal(PanelShortcuts.FilterAll, PanelShortcuts.FilterFor(null, null, true));
            Assert.Equal(PanelShortcuts.FilterAll, PanelShortcuts.FilterFor("nonsense", null, true));

            Assert.Null(PanelShortcuts.FilterEmpty(PanelShortcuts.FilterAll));
            Assert.Equal("Nothing is bound yet.", PanelShortcuts.FilterEmpty(PanelShortcuts.FilterBound));
            Assert.Equal("Every shortcut is bound.", PanelShortcuts.FilterEmpty(PanelShortcuts.FilterNotBound));
        }

        private static PanelShortcuts.BindingUse Use(string trigger, string place, string does)
        {
            return new PanelShortcuts.BindingUse(trigger, place, does);
        }

        [Fact]
        public void A_button_bound_twice_is_named_with_what_it_does_where_and_nothing_more()
        {
            var zoneB = PanelShortcuts.ZoneDoes("Zone B", true);
            var clashes = PanelShortcuts.Clashes(new[]
            {
                Use("JoystickPlugin.CSL_Elite_B07", "Main dash", zoneB),
                Use("JoystickPlugin.CSL_Elite_B08", "Main dash", PanelShortcuts.ZoneDoes("Zone C", true)),
                Use("JoystickPlugin.CSL_Elite_B07", "Rim", zoneB),
            });
            var clash = Assert.Single(clashes);
            // The artboard's line, less its aside ("Fine if you meant it."), which voice.md deletes.
            Assert.Equal("CSL Elite · 7 cycles zone B on both Main dash and Rim.", clash.Text);
            Assert.Equal("CSL Elite · 7", clash.Lead);
            Assert.Equal("cycles zone B on both Main dash and Rim.", clash.Rest);
            Assert.Equal("JoystickPlugin.CSL_Elite_B07", clash.Trigger);
        }

        [Fact]
        public void A_clash_line_reads_for_any_pair_of_rows()
        {
            Assert.Equal("cycles band D back", PanelShortcuts.ZoneDoes("Band D", false));
            Assert.Equal("Keyboard · N toggles night mode and cycles zone A on Rim.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.N", null, PanelShortcuts.RigActionDoes(Contract.ToggleNightModeAction)),
                Use("KeyboardReaderPlugin.N", "Rim", PanelShortcuts.ZoneDoes("Zone A", true)),
            }).Single().Text);
            Assert.Equal("Keyboard · F9 cycles zone A and holds the quick glance on Rim.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true)),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.GlanceDoes),
            }).Single().Text);
            Assert.Equal("Keyboard · F9 holds the quick glance on Rim, Main dash and Pit wall.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.GlanceDoes),
                Use("KeyboardReaderPlugin.F9", "Main dash", PanelShortcuts.GlanceDoes),
                Use("KeyboardReaderPlugin.F9", "Pit wall", PanelShortcuts.GlanceDoes),
            }).Single().Text);
            Assert.Equal("Keyboard · B turns the brightness up and turns the brightness down.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.B", null, PanelShortcuts.RigActionDoes(Contract.BrightnessUpAction)),
                Use("KeyboardReaderPlugin.B", null, PanelShortcuts.RigActionDoes(Contract.BrightnessDownAction)),
            }).Single().Text);
        }

        [Fact]
        public void One_binding_per_row_is_no_clash_and_clashes_keep_the_page_order()
        {
            Assert.Empty(PanelShortcuts.Clashes(null));
            Assert.Empty(PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.GlanceDoes),
                // SimHub keeping the same trigger twice on one action is still one binding.
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.GlanceDoes),
                Use(" ", "Rim", PanelShortcuts.GlanceDoes),
                Use("KeyboardReaderPlugin.F10", "Main dash", PanelShortcuts.GlanceDoes),
            }));
            var both = PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F2", "Rim", PanelShortcuts.GlanceDoes),
                Use("KeyboardReaderPlugin.F1", "Rim", PanelShortcuts.ZoneDoes("Zone A", true)),
                Use("KeyboardReaderPlugin.F1", "Main dash", PanelShortcuts.ZoneDoes("Zone A", true)),
                Use("KeyboardReaderPlugin.F2", "Main dash", PanelShortcuts.GlanceDoes),
            });
            Assert.Equal(new[] { "KeyboardReaderPlugin.F2", "KeyboardReaderPlugin.F1" }, both.Select(c => c.Trigger));
        }

        [Fact]
        public void Its_geometry_is_the_artboards()
        {
            // .gh
            Assert.Equal(16, PanelShortcuts.HeaderPaddingX);
            Assert.Equal(14, PanelShortcuts.HeaderPaddingY);
            Assert.Equal(16, PanelShortcuts.GroupTitleSize);
            Assert.Equal(10, PanelShortcuts.GroupDetailGap);
            Assert.Equal(14, PanelShortcuts.CountSize);
            // .r
            Assert.Equal(16, PanelShortcuts.RowPaddingX);
            Assert.Equal(10, PanelShortcuts.RowPaddingY);
            Assert.Equal(16, PanelShortcuts.RowGap);
            Assert.Equal(14, PanelShortcuts.RowNameSize);
            Assert.Equal(8, PanelShortcuts.TagGap);
            Assert.Equal(90, PanelShortcuts.PressWidth);
            Assert.Equal(260, PanelShortcuts.BinderMinWidth);
            // The header: 8 from the title to the caption, the filter's 30 px bar on the caption's foot.
            Assert.Equal(8, PanelShortcuts.IntroGap);
            Assert.Equal(11, PanelShortcuts.FilterRaise);
            Assert.Equal(14, PanelKit.SegmentedPaddingShortcuts);
            // The external line and the role=status line.
            Assert.Equal(16, PanelShortcuts.LeadPaddingX);
            Assert.Equal(12, PanelShortcuts.LeadPaddingY);
            Assert.Equal(10, PanelShortcuts.LeadGap);
            Assert.Equal(16, PanelShortcuts.BannerPaddingX);
            Assert.Equal(12, PanelShortcuts.BannerPaddingY);
            Assert.Equal(12, PanelShortcuts.BannerGap);
            Assert.Equal(14, PanelShortcuts.BannerTextSize);
            Assert.Equal(16, PanelShortcuts.BannerIconSize);
        }

        [Fact]
        public void A_row_puts_its_binder_under_its_name_only_where_the_three_cannot_sit_side_by_side()
        {
            // 2 of card rules, 32 of padding, 160 of name, 16 + 90 + 16 + 260 of press and binder.
            Assert.Equal(576, PanelShortcuts.RowStackBelow);
            Assert.False(PanelShortcuts.RowStacks(PanelShell.ContentMax));
            // The rail's content at the narrowest full-sidebar width, where TwoColumns is still false.
            Assert.False(PanelShortcuts.RowStacks(679));
            Assert.False(PanelShortcuts.RowStacks(576));
            Assert.True(PanelShortcuts.RowStacks(575));
            Assert.True(PanelShortcuts.RowStacks(400));
        }

        [Fact]
        public void Search_finds_every_row_and_heading_the_page_names()
        {
            var labels = PanelShortcuts.Search.Select(entry => entry.Label).ToList();
            Assert.Equal(new[] { "Next page", "Previous page", "Quick glance", "Lights", "Night mode", "Brightness up", "Brightness down", "Alerts" }, labels);
            Assert.Equal(labels.Count, labels.Distinct().Count());
            Assert.All(PanelShortcuts.Search, entry => Assert.Equal(PanelPage.Shortcuts, entry.Route.Page));
            Assert.NotEmpty(PanelSearch.Find(PanelShortcuts.Search, "wheel buttons"));
            Assert.Equal(PanelShortcuts.AnchorAlerts, PanelSearch.Find(PanelShortcuts.Search, "dismiss").First().Route.Anchor);
        }

        /// <summary>The page's anchor ids, which search, Home's fix rows and the capture scripts route to: a
        /// renamed one sends each of them to the page's top, so every id is pinned, and a new one is added here.</summary>
        [Fact]
        public void Its_anchor_ids_are_pinned()
        {
            Assert.Equal(new[]
            {
                "AnchorAlerts = shortcuts.alerts",
                "AnchorRig = shortcuts.rig",
                "AnchorScreens = shortcuts.screens",
            }, AnchorTable.Of(typeof(PanelShortcuts)));
        }

        /// <summary>
        /// The page draws: one hold binder per kind, each with its glance caption; every greyed row through the
        /// registry; every live row anchored where another page's binding chip lands; and the filter at the
        /// artboard's padding.
        /// </summary>
        [Fact]
        public void The_page_draws_what_its_model_decides()
        {
            var code = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Shortcuts.cs"));
            Assert.Equal(3, Regex.Matches(code, @"BuildBinder\([^;]*hold: true\)").Count);
            Assert.Contains("Ui.Caption(PanelCopy.FaceGlance)", code);
            Assert.Contains("Ui.Caption(PanelCopy.PitWallGlance)", code);
            Assert.Contains("Ui.Caption(PanelCopy.CompanionGlance)", code);
            Assert.Contains("Ui.Caption(PanelCopy.CompanionPaging, BodyWidth)", code);
            Assert.Contains("Ui.Crumbs(PanelShortcuts.PagingCrumbs(", code);
            Assert.Contains("Ui.Anchor(row, PanelBindings.Anchor(binding.Action));", code);
            Assert.Contains("Ui.Soon(row, item)", code);
            Assert.Contains("ShortcutsSoon(group, PanelSoon.RigTest, stacks);", code);
            Assert.Contains("ShortcutsSoon(group, PanelSoon.AlertDismissal, stacks);", code);
            Assert.Contains("padding: PanelKit.SegmentedPaddingShortcuts", code);
            // Columns follow the room a row has, never the sidebar's state.
            Assert.DoesNotContain("!Narrow", code);
        }
    }
}
