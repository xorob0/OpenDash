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
            // The binder's name in BuildBinder's fallback text is the row's words after the screen's name.
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
        public void A_row_is_greyed_unread_bound_or_not_bound()
        {
            Assert.Equal(PanelShortcuts.RowState.Greyed, PanelShortcuts.StateOf(false, null));
            Assert.Equal(PanelShortcuts.RowState.Greyed, PanelShortcuts.StateOf(false, 2));
            Assert.Equal(PanelShortcuts.RowState.Unread, PanelShortcuts.StateOf(true, null));
            Assert.Equal(PanelShortcuts.RowState.NotBound, PanelShortcuts.StateOf(true, 0));
            Assert.Equal(PanelShortcuts.RowState.Bound, PanelShortcuts.StateOf(true, 1));
        }

        private static readonly PanelShortcuts.RowState Greyed = PanelShortcuts.RowState.Greyed;
        private static readonly PanelShortcuts.RowState Unread = PanelShortcuts.RowState.Unread;
        private static readonly PanelShortcuts.RowState NotBound = PanelShortcuts.RowState.NotBound;
        private static readonly PanelShortcuts.RowState Bound = PanelShortcuts.RowState.Bound;

        [Fact]
        public void A_card_counts_its_bound_rows_out_of_those_it_can_bind()
        {
            Assert.Equal("3 of 6", PanelShortcuts.CardCount(new[] { Bound, Bound, Bound, NotBound, NotBound, NotBound }, true, false));
            // Greyed rows are left out of the total: Lights with night mode bound reads "1 of 3", not the
            // artboard's "1 of 4" (a departure awaiting a ruling), and Alerts, all greyed, has no count.
            Assert.Equal("1 of 3", PanelShortcuts.CardCount(new[] { Bound, NotBound, NotBound, Greyed }, true, false));
            Assert.Null(PanelShortcuts.CardCount(new[] { Greyed }, true, false));
            Assert.Null(PanelShortcuts.CardCount(new PanelShortcuts.RowState[0], true, false));
        }

        [Fact]
        public void A_card_has_no_count_when_simhub_pages_it_or_nothing_can_be_read()
        {
            // The companion's card, whose paging SimHub binds: the artboard's external card has no count.
            Assert.Null(PanelShortcuts.CardCount(new[] { Bound }, true, true));
            Assert.Null(PanelShortcuts.CardCount(new[] { NotBound }, true, true));
            // Ruling 60: when any row cannot be read, no card counts, and neither the filter nor the clash
            // line is drawn.
            Assert.Null(PanelShortcuts.CardCount(new[] { Bound, NotBound }, false, false));
            Assert.True(PanelShortcuts.Readable(new[] { Bound, NotBound, Greyed }));
            Assert.True(PanelShortcuts.Readable(new PanelShortcuts.RowState[0]));
            Assert.False(PanelShortcuts.Readable(new[] { Bound, Unread, Greyed }));
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
            // What each row does in the clash line: a previous-page row cycles its zone back, never forward.
            Assert.Equal(zones.Select(z => PanelShortcuts.ZoneDoes(z, true)).Concat(zones.Select(z => PanelShortcuts.ZoneDoes(z, false))), rows.Select(r => r.Does));
            Assert.All(rows.Skip(zones.Count), r => Assert.EndsWith(" back", r.Does));
            // The previous page rows are this release's, and they alone carry the New tag.
            Assert.Equal(zones.Select(z => false).Concat(zones.Select(z => true)), rows.Select(r => r.IsNew));
            Assert.All(rows, r => Assert.Equal("Tap", r.Press));

            var glance = PanelShortcuts.GlanceBinding(Face, "Rim");
            Assert.Equal("Quick glance", glance.Label);
            Assert.Equal("Hold", glance.Press);
            Assert.True(glance.IsHold);
            Assert.False(glance.IsNew);
            Assert.Equal("Rim · quick glance", glance.BinderName);
            Assert.Equal(PanelShortcuts.GlanceDoes, glance.Does);
            Assert.Equal("holds the quick glance", glance.Does);

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
            Assert.Equal(new[] { "Night mode", "Brightness up", "Brightness down" }, rows.Select(r => r.BinderName));
            Assert.Equal(Contract.RigActionNames().Select(PanelShortcuts.RigActionDoes), rows.Select(r => r.Does));
            Assert.Equal(new[] { "toggles night mode", "turns the brightness up", "turns the brightness down" }, rows.Select(r => r.Does));
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
            // Only what SimHub names: the screen's name is OpenDash's, not a SimHub device's, and a companion may
            // run in a window, so the device is left to the sentence above the crumbs.
            Assert.Equal(new[] { "Controls and events", "NextScreen" }, PanelShortcuts.PagingCrumbs());
            Assert.Contains("device or window the companion runs on", PanelCopy.CompanionPaging);
            // The crumbs are the path PanelCopy.CompanionPaging names in its sentence.
            Assert.Contains(PanelShortcuts.ControlsAndEventsCrumb, PanelCopy.CompanionPaging);
            Assert.Contains(PanelShortcuts.NextScreenCrumb, PanelCopy.CompanionPaging);
        }

        [Fact]
        public void The_filter_shows_all_bound_or_not_bound_and_a_greyed_row_is_not_bound()
        {
            Assert.Equal(new[] { "All", "Bound", "Not bound" }, PanelShortcuts.FilterLabels);
            Assert.Equal(PanelShortcuts.FilterValues.Length, PanelShortcuts.FilterLabels.Length);
            Assert.Equal(PanelBindings.NotBound, PanelShortcuts.FilterLabels[2]);

            foreach (var row in new[] { Greyed, Unread, NotBound, Bound }) Assert.True(PanelShortcuts.Shows(PanelShortcuts.FilterAll, row));
            Assert.True(PanelShortcuts.Shows(PanelShortcuts.FilterBound, Bound));
            Assert.False(PanelShortcuts.Shows(PanelShortcuts.FilterBound, NotBound));
            Assert.False(PanelShortcuts.Shows(PanelShortcuts.FilterBound, Greyed));
            Assert.False(PanelShortcuts.Shows(PanelShortcuts.FilterBound, Unread));
            Assert.False(PanelShortcuts.Shows(PanelShortcuts.FilterNotBound, Bound));
            Assert.True(PanelShortcuts.Shows(PanelShortcuts.FilterNotBound, NotBound));
            // A greyed row is not bound, and its chip says so: Not bound shows it, so that "Every shortcut is
            // bound." cannot be said while the Rig test and the alert's dismissal are still to come.
            Assert.True(PanelShortcuts.Shows(PanelShortcuts.FilterNotBound, Greyed));
            Assert.False(PanelShortcuts.Shows(PanelShortcuts.FilterNotBound, Unread));
        }

        [Fact]
        public void The_filter_goes_to_all_for_a_row_the_page_was_opened_on_and_when_nothing_can_be_read()
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

        private static PanelShortcuts.BindingUse Use(string trigger, string place, string does, PanelShortcuts.Fires fires = PanelShortcuts.Fires.OnEveryPress)
        {
            return new PanelShortcuts.BindingUse(trigger, place, does, fires);
        }

        [Fact]
        public void A_binding_answers_the_presses_its_press_type_names()
        {
            // SimHub 9.12.6's PressType names, as the view reads them off each mapping.
            Assert.Equal(PanelShortcuts.Fires.OnShortPress, PanelShortcuts.FiresOn("ShortPress"));
            Assert.Equal(PanelShortcuts.Fires.OnLongPress, PanelShortcuts.FiresOn("LongPress"));
            Assert.Equal(PanelShortcuts.Fires.OnLongPress, PanelShortcuts.FiresOn("LongPressNoAutoRepeat"));
            foreach (var any in new[] { "Default", "During", "ShortAndLongPress", "Pressed", "Released", null })
            {
                Assert.Equal(PanelShortcuts.Fires.OnEveryPress, PanelShortcuts.FiresOn(any));
            }
        }

        [Fact]
        public void A_short_press_and_a_long_press_of_one_button_are_two_gestures_and_no_clash()
        {
            var shortPress = PanelShortcuts.Fires.OnShortPress;
            var longPress = PanelShortcuts.Fires.OnLongPress;
            // The pairing this release's previous-page and brightness rows invite on a rim with few buttons.
            Assert.Empty(PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), shortPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", false), longPress),
            }));
            Assert.Empty(PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.B", null, PanelShortcuts.RigActionDoes(Contract.BrightnessUpAction), shortPress),
                Use("KeyboardReaderPlugin.B", null, PanelShortcuts.RigActionDoes(Contract.BrightnessDownAction), longPress),
            }));
            // The glance is held (During), which answers every press, so it doubles whatever shares its button.
            Assert.Equal("Keyboard · F9 holds the quick glance and cycles zone A on Rim.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.GlanceDoes, PanelShortcuts.FiresOn("During")),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), shortPress),
            }).Single().Text);
            // Two short presses clash; ShortAndLongPress, SimHub's default in its picker, clashes with either.
            Assert.Single(PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F1", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), shortPress),
                Use("KeyboardReaderPlugin.F1", "Main dash", PanelShortcuts.ZoneDoes("Zone A", true), shortPress),
            }));
            Assert.Single(PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F1", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), PanelShortcuts.FiresOn("ShortAndLongPress")),
                Use("KeyboardReaderPlugin.F1", "Rim", PanelShortcuts.ZoneDoes("Zone A", false), longPress),
            }));
        }

        [Fact]
        public void A_clash_names_every_row_doubled_on_either_gesture_and_no_other()
        {
            var shortPress = PanelShortcuts.Fires.OnShortPress;
            var longPress = PanelShortcuts.Fires.OnLongPress;
            // Short on zone A, long on zone A back and on band D back: only the two long presses are doubled.
            Assert.Equal("Keyboard · F9 cycles zone A back and cycles band D back on Rim.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), shortPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", false), longPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Band D", false), longPress),
            }).Single().Text);
            // One row bound on both gestures of a button answers both, so a long press elsewhere doubles it.
            Assert.Equal("Keyboard · F9 cycles zone A and cycles zone A back on Rim.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), shortPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), longPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", false), longPress),
            }).Single().Text);
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
            // The glance's caption 4 under its name, and a stacked binder 8 under the name and the press.
            Assert.Equal(4, PanelShortcuts.CaptionGap);
            Assert.Equal(8, PanelShortcuts.StackGap);
            Assert.Equal(160, PanelShortcuts.NameMinWidth);
            Assert.Equal(260, PanelShortcuts.BinderMinWidth);
            Assert.Equal(300, PanelShortcuts.BinderWidth);
            // The header: 8 from the title to the caption, the filter's 30 px bar on the caption's foot.
            Assert.Equal(8, PanelShortcuts.IntroGap);
            Assert.Equal(11, PanelShortcuts.FilterRaise);
            // Under the caption when the header stacks: the artboard's 12 px header gap.
            Assert.Equal(12, PanelShortcuts.FilterGapStacked);
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
            // Two clash lines, 8 apart.
            Assert.Equal(8, PanelShortcuts.BannerStackGap);
        }

        [Fact]
        public void A_row_puts_its_binder_under_its_name_only_where_the_three_cannot_sit_side_by_side()
        {
            // 2 of card rules, 32 of padding, 160 of name, 16 + 90 + 16 + 300 of press and binder.
            Assert.Equal(616, PanelShortcuts.RowStackBelow);
            Assert.Equal(PanelShortcuts.RowStackBelow, 2 + 2 * PanelShortcuts.RowPaddingX + PanelShortcuts.NameMinWidth
                + PanelShortcuts.RowGap + PanelShortcuts.PressWidth + PanelShortcuts.RowGap + PanelShortcuts.BinderWidth);
            Assert.False(PanelShortcuts.RowStacks(PanelShell.ContentMax));
            // The rail's content at the narrowest full-sidebar width, where TwoColumns is still false.
            Assert.False(PanelShortcuts.RowStacks(679));
            Assert.False(PanelShortcuts.RowStacks(616));
            Assert.True(PanelShortcuts.RowStacks(615));
            Assert.True(PanelShortcuts.RowStacks(400));
        }

        [Fact]
        public void Every_row_gives_simhubs_editor_one_slot_of_fixed_width()
        {
            // SimHub's editor is never given less than BuildBinder's floor, so the slot is no narrower.
            Assert.True(PanelShortcuts.BinderWidth >= PanelShortcuts.BinderMinWidth);
            var shell = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.cs"));
            Assert.Contains("MinWidth = " + PanelShortcuts.BinderMinWidth.ToString(System.Globalization.CultureInfo.InvariantCulture) + " }", shell);
            // Beside the name, the same slot on every row, whatever the content's width.
            Assert.Equal(PanelShortcuts.BinderWidth, PanelShortcuts.BinderSlot(PanelShortcuts.RowStackBelow, false));
            Assert.Equal(PanelShortcuts.BinderWidth, PanelShortcuts.BinderSlot(PanelShell.ContentMax, false));
            // Under it, no wider than the row inside its padding.
            Assert.Equal(PanelShortcuts.BinderWidth, PanelShortcuts.BinderSlot(600, true));
            Assert.Equal(266, PanelShortcuts.BinderSlot(300, true));
            Assert.Equal(0, PanelShortcuts.BinderSlot(20, true));
            // SimHub's editor draws no name of its own: the row's beside it says what it binds.
            Assert.Empty(PanelShortcuts.EditorName);
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
        /// The watch the page puts on every live row's editor is let go of, as HoldWhilePressed's is
        /// (PanelObligationsTests.A_glance_rebound_in_place_is_held_again): SimHub's mappings live for the
        /// session and hold their handlers strongly, and each handler here leads through the page's evaluation
        /// to every card it drew, so one left behind keeps every discarded Shortcuts build alive.
        /// </summary>
        [Fact]
        public void The_bindings_watch_lets_go_when_the_editor_leaves_and_the_page_is_dropped()
        {
            var code = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Shortcuts.cs"));
            var start = code.IndexOf("private void ShortcutsWatch(", StringComparison.Ordinal);
            Assert.True(start >= 0, "ShortcutsWatch is on the page");
            var body = code.Substring(start);
            Assert.Contains("mapping.PropertyChanged += mappingChanged;", body);
            Assert.Contains("watchedTriggers.CollectionChanged += collectionChanged;", body);
            Assert.Contains("watched.PropertyChanged += modelChanged;", body);

            var rewatch = Between(body, "Action rewatchMappings = () =>", "};");
            Assert.Contains("mapping.PropertyChanged -= mappingChanged;", rewatch);
            Assert.Contains("watchedMappings.Clear();", rewatch);

            var detach = Between(body, "Action detach = () =>", "};");
            Assert.Contains("mapping.PropertyChanged -= mappingChanged;", detach);
            Assert.Contains("watchedMappings.Clear();", detach);
            Assert.Contains("watchedTriggers.CollectionChanged -= collectionChanged;", detach);
            Assert.Contains("watchedTriggers = null;", detach);
            Assert.Contains("watched.PropertyChanged -= modelChanged;", detach);
            Assert.Contains("watched = null;", detach);

            // Let go of when the editor leaves the tree, and when the build is replaced or the page left.
            Assert.Contains("editor.Unloaded += (sender, args) => detach();", body);
            Assert.Contains("OnDrop(detach);", body);
        }

        private static string Between(string text, string from, string to)
        {
            var start = text.IndexOf(from, StringComparison.Ordinal);
            Assert.True(start >= 0, from);
            var end = text.IndexOf(to, start, StringComparison.Ordinal);
            Assert.True(end > start, to);
            return text.Substring(start, end - start);
        }

        /// <summary>
        /// The page draws what PanelShortcuts decides, through PanelShortcuts: one hold binder per kind, each with
        /// its glance caption; every greyed row through the registry; every live row anchored where another
        /// page's binding chip lands and every card where search lands; the filter, the counts, the clash line
        /// and the empty line from the model's evaluation; and the rows at the model's geometry.
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
            Assert.Contains("Ui.Crumbs(PanelShortcuts.PagingCrumbs())", code);
            Assert.Contains("Ui.Anchor(row, PanelBindings.Anchor(binding.Action));", code);
            Assert.Contains("Ui.Soon(row, item)", code);
            Assert.Contains("ShortcutsSoon(group, PanelSoon.RigTest, layout);", code);
            Assert.Contains("ShortcutsSoon(group, PanelSoon.AlertDismissal, layout);", code);
            Assert.Contains("padding: PanelKit.SegmentedPaddingShortcuts", code);
            // Columns follow the room a row has, never the sidebar's state.
            Assert.DoesNotContain("!Narrow", code);

            // The page's frame: its intro, the filter's labels and its hook into the evaluation, the empty line,
            // the header's two columns only where TwoColumns says, and every anchor id AnchorTable pins drawn.
            Assert.Contains("Ui.Caption(PanelShortcuts.IntroCaption, BodyWidth)", code);
            Assert.Contains("BuildSegmented(PanelShortcuts.FilterValues, PanelShortcuts.FilterLabels, shortcutsFilter,", code);
            Assert.Contains("evaluate = () => ShortcutsEvaluate(groups, filter, banner, empty);", code);
            Assert.Contains("if (editor != null) ShortcutsWatch(editor, changed);", code);
            Assert.Contains("            evaluate();\n", code.Replace("\r\n", "\n"));
            Assert.Contains("var line = ShortcutsClashLine(clash);", code);
            Assert.Contains("empty.Text = emptyText ?? string.Empty;", code);
            Assert.Contains("if (TwoColumns)", code);
            Assert.Contains("if (screens.Count > 0) Ui.Anchor(screens[0].Card, PanelShortcuts.AnchorScreens);", code);
            Assert.Contains("Ui.Anchor(lights.Card, PanelShortcuts.AnchorRig);", code);
            Assert.Contains("Ui.Anchor(alerts.Card, PanelShortcuts.AnchorAlerts);", code);
            foreach (var id in AnchorTable.Of(typeof(PanelShortcuts)))
            {
                Assert.Contains("PanelShortcuts." + id.Substring(0, id.IndexOf(" =", StringComparison.Ordinal)) + ");", code);
            }

            // The rows as the model gives them: its label and press, the New tag where the model says, the
            // greyed rows by their registry title, and each card's line beside its name.
            Assert.Contains("ShortcutsRow(binding.Label, binding.Press, editor, layout, caption, tags);", code);
            Assert.Contains("var tags = binding.IsNew ? new FrameworkElement[] { Ui.NewTag() } : new FrameworkElement[0];", code);
            Assert.Contains("ShortcutsRow(item.Title, PanelShortcuts.Tap, chip, layout, null);", code);
            Assert.Equal(3, Regex.Matches(code, Regex.Escape("PanelShortcuts.GroupDetail(screen.Kind, screen.Width, screen.Height)")).Count);
            Assert.Contains("ShortcutsCard(PanelShortcuts.RigGroupTitle, PanelShortcuts.RigGroupDetail, null);", code);
            Assert.Contains("ShortcutsCard(PanelShortcuts.AlertsGroupTitle, null, null);", code);

            // Every row's binder in one slot of the model's width, the gap on the slot, never on SimHub's editor,
            // whose template draws its own Margin a second time; the editor names nothing of its own.
            Assert.Contains("var layout = new ShortcutsLayout(ContentWidth);", code);
            Assert.Contains("Stacks = PanelShortcuts.RowStacks(contentWidth);", code);
            Assert.Contains("Binder = PanelShortcuts.BinderSlot(contentWidth, Stacks);", code);
            Assert.Contains("control.Margin = new Thickness(0);", code);
            Assert.DoesNotContain("control.Margin = new Thickness(PanelShortcuts", code);
            Assert.Contains("var slot = new Border { Child = control, Width = layout.Binder,", code);
            Assert.Contains("new ColumnDefinition { Width = new GridLength(PanelShortcuts.RowGap + layout.Binder) }", code);
            Assert.DoesNotContain("GridLength.Auto });\n                    control", code.Replace("\r\n", "\n"));
            Assert.Contains("control.FriendlyName = PanelShortcuts.EditorName;", code);
            // A card's name is measured at the header's width, so a long one ends in an ellipsis.
            Assert.Contains("var titleLine = new WrapPanel", code);

            // What the model decides from the bindings read, each drawn through it: the row's state, the
            // page's readability, the filter, the counts (and none for the companion), and ruling 60's three
            // hides when a row cannot be read.
            Assert.Contains("states[row] = PanelShortcuts.StateOf(row.Bindable, read == null ? (int?)null : read.Count);", code);
            Assert.Contains("var readable = PanelShortcuts.Readable(states.Values);", code);
            Assert.Contains("var chosen = PanelShortcuts.FilterFor(shortcutsFilter, null, readable);", code);
            Assert.Contains("filter.Visibility = readable ? Visibility.Visible : Visibility.Collapsed;", code);
            Assert.Contains("PanelShortcuts.Shows(chosen, states[row])", code);
            Assert.Contains("var count = PanelShortcuts.CardCount(group.Rows.Select(row => states[row]), readable, group.HasLead);", code);
            Assert.Contains("if (readable)\n", code.Replace("\r\n", "\n"));
            Assert.Contains("foreach (var clash in PanelShortcuts.Clashes(all))", code);
            Assert.Contains("PanelShortcuts.FilterEmpty(chosen)", code);
            // A binding's presses come off SimHub's mapping by the name of its press type.
            Assert.Contains("PanelShortcuts.FiresOn(mapping.PressType.ToString())", code);

            // The anchor puts the filter back to All on the first build after the page is opened, and never on
            // a rebuild in place, which keeps the route and its anchor: OnLeave runs on Go alone.
            var landing = Regex.Match(code, @"if \(shortcutsLanding\)\s*\{\s*shortcutsFilter = PanelShortcuts\.FilterFor\(shortcutsFilter, to == null \? null : to\.Anchor, true\);\s*shortcutsLanding = false;\s*\}\s*OnLeave\(""Shortcuts\.filterLanding"", \(\) => shortcutsLanding = true\);");
            Assert.True(landing.Success, "the filter's reset for an anchor is not gated on the page's first build");
            Assert.Single(Regex.Matches(code, @"PanelShortcuts\.FilterFor\(shortcutsFilter, to"));
        }
    }
}
