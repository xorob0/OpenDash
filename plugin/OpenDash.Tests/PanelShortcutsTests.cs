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
        /// <summary>The content beside the full sidebar at a control that wide, with a 17 px scroll bar: 879 at
        /// the artboard's 1200 frame, 3519 at 3840. It is PanelShell.ContentWidth(control, 17) once the column
        /// has no ceiling, spelled out so the pin reads the same on either side of that change; the column
        /// has no widest width to pin against.</summary>
        private static double Column(double control) => control - PanelShell.SidebarWidth - 2 * PanelShell.MainPaddingX(PanelLayout.Full) - 17;

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
        public void A_cards_line_gives_its_screens_kind_and_size_less_what_its_name_says()
        {
            // A renamed screen keeps both, in a Screens card's words.
            Assert.Equal("Face · 1280 × 480", PanelShortcuts.GroupDetail("Main dash", Contract.KindFace, 1280, 480));
            Assert.Equal("Pit wall · 1920 × 1080", PanelShortcuts.GroupDetail("Garage", Contract.KindPitWall, 1920, 1080));
            Assert.Equal("Companion · 480 × 850", PanelShortcuts.GroupDetail("Phone", Contract.KindCompanion, 480, 850));
            // A default name says the kind or the size already, so the line leaves it out, as the artboard's
            // "Pit wall" card reads "1920×1080": nothing said twice.
            Assert.Equal("1920 × 1080", PanelShortcuts.GroupDetail("Pit wall", Contract.KindPitWall, 1920, 1080));
            Assert.Equal("480 × 850", PanelShortcuts.GroupDetail("Companion", Contract.KindCompanion, 480, 850));
            Assert.Equal("Face", PanelShortcuts.GroupDetail("1920 × 480", Contract.KindFace, 1920, 480));
            Assert.Null(PanelShortcuts.GroupDetail("Companion", Contract.KindCompanion, 0, 0));
            // A screen whose package is gone has no size to show, and "0 × 0" or "1280 × 0" is worse than the
            // kind alone.
            Assert.Equal("Face", PanelShortcuts.GroupDetail("Rim", Contract.KindFace, 0, 0));
            Assert.Equal("Face", PanelShortcuts.GroupDetail("Rim", Contract.KindFace, 1280, 0));
            Assert.Equal("Face", PanelShortcuts.GroupDetail("Rim", Contract.KindFace, 0, 480));
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
            Assert.Equal("3 of 6", PanelShortcuts.CardCount(new[] { Bound, Bound, Bound, NotBound, NotBound, NotBound }, true));
            // Nothing bound yet, which is every card on a new rig: still counted.
            Assert.Equal("0 of 9", PanelShortcuts.CardCount(Enumerable.Repeat(NotBound, 9), true));
            // Greyed rows are left out of the total, as ruled: Lights with night mode bound reads "1 of 3", not
            // the artboard's "1 of 4", and Alerts, all greyed, has no count.
            Assert.Equal("1 of 3", PanelShortcuts.CardCount(new[] { Bound, NotBound, NotBound, Greyed }, true));
            Assert.Equal("0 of 3", PanelShortcuts.CardCount(new[] { NotBound, NotBound, NotBound, Greyed }, true));
            Assert.Null(PanelShortcuts.CardCount(new[] { Greyed }, true));
            Assert.Null(PanelShortcuts.CardCount(new PanelShortcuts.RowState[0], true));
        }

        [Fact]
        public void A_card_with_one_row_counts_it_and_none_counts_when_nothing_can_be_read()
        {
            // A pit wall's card and a companion's alike: the companion's paging line is no row, and its glance
            // is counted, so the cards add up to the sidebar's total. The artboard blanks the external card's
            // count although it has that row; the departure is listed for the author.
            Assert.Equal("1 of 1", PanelShortcuts.CardCount(new[] { Bound }, true));
            Assert.Equal("0 of 1", PanelShortcuts.CardCount(new[] { NotBound }, true));
            // Ruling 60: when any row cannot be read, no card counts, and neither the filter nor the clash
            // line is drawn.
            Assert.Null(PanelShortcuts.CardCount(new[] { Bound, NotBound }, false));
            Assert.True(PanelShortcuts.Readable(new[] { Bound, NotBound, Greyed }));
            Assert.True(PanelShortcuts.Readable(new PanelShortcuts.RowState[0]));
            Assert.False(PanelShortcuts.Readable(new[] { Bound, Unread, Greyed }));
        }

        [Fact]
        public void A_face_lists_every_zone_forward_then_back_then_the_glance()
        {
            var order = Contract.FaceZoneLetters;
            var rows = PanelShortcuts.FaceBindings(Face, "Rim");
            var zones = order.Select(PanelFacePlan.ZoneLabel).ToList();
            // Shortcuts.dc.html's order, by letter: not the picture's, which draws a Row face's zones B, A, C.
            Assert.Equal("Zone A · next page", rows[0].Label);
            Assert.Equal("Zone B · next page", rows[1].Label);
            Assert.Equal("Band D · next page", rows[3].Label);
            Assert.Equal("Zone A · previous page", rows[4].Label);
            Assert.Equal("Band D · previous page", rows[7].Label);
            Assert.Equal(zones.Select(z => z + " · next page").Concat(zones.Select(z => z + " · previous page")), rows.Select(r => r.Label));
            Assert.Equal(order.Select(l => Contract.CycleZoneAction(Face, l)).Concat(order.Select(l => Contract.CycleZoneBackAction(Face, l))), rows.Select(r => r.Action));
            Assert.Equal(rows.Select(r => "Rim · " + r.Label), rows.Select(r => r.BinderName));
            // What each row does in the clash line: a previous-page row takes its zone to its previous page.
            Assert.Equal(zones.Select(z => PanelShortcuts.ZoneDoes(z, true)).Concat(zones.Select(z => PanelShortcuts.ZoneDoes(z, false))), rows.Select(r => r.Does));
            Assert.All(rows.Skip(zones.Count), r => Assert.EndsWith(" to its previous page", r.Does));
            // The previous page rows are this release's, and they alone carry the New tag.
            Assert.Equal(zones.Select(z => false).Concat(zones.Select(z => true)), rows.Select(r => r.IsNew));
            Assert.All(rows, r => Assert.Equal("Tap", r.Press));

            var glance = PanelShortcuts.GlanceBinding(Contract.KindFace, Face, "Rim");
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
        public void Every_face_lists_its_zones_alike_whatever_its_picture_draws()
        {
            // A Row face's picture draws B, A, C; its card still reads A, B, C, D, as a face of unknown size does.
            var row = Contract.FaceSizes.First(size => size.Width == 1920 && size.Height == 480);
            Assert.Equal(new[] { "B", "A", "C", "D" }, PanelFacePlan.ZoneOrder(row));
            Assert.Equal(new[] { "A", "B", "C", "D" }, Contract.FaceZoneLetters);
            Assert.Equal(PanelShortcuts.FaceBindings("Face1280x480", "Main dash").Select(r => r.Label), PanelShortcuts.FaceBindings(Face, "Rim").Select(r => r.Label));
        }

        [Fact]
        public void A_pit_wall_and_a_companion_bind_their_glance_alone()
        {
            foreach (var kind in new[] { Contract.KindPitWall, Contract.KindCompanion })
            {
                var ns = kind == Contract.KindPitWall ? Contract.PitWallPrefix : Contract.CompanionPrefix;
                Assert.Equal(Contract.ScreenActionNames(kind, ns), new[] { PanelShortcuts.GlanceBinding(kind, ns, "Wall").Action });
            }
        }

        [Fact]
        public void A_portrait_pit_wall_has_no_glance_row_and_so_no_card()
        {
            // The glance borrows a landscape zone; the portrait package draws only its own four, so a held
            // key there would change nothing, and the page offers no binding for it.
            Assert.All(Contract.GlanceZoneSlots(), slot => Assert.True(slot.Landscape));
            Assert.True(PanelShortcuts.PitWallGlances(1920, 1080));
            Assert.False(PanelShortcuts.PitWallGlances(1080, 1920));
            // Square is not taller than wide, as the Screens page's Height > Width test reads it.
            Assert.True(PanelShortcuts.PitWallGlances(1080, 1080));
            // A wall whose package is gone has no size, and keeps the row it had.
            Assert.True(PanelShortcuts.PitWallGlances(0, 0));
            Assert.True(PanelShortcuts.PitWallGlances(0, 1920));
            Assert.True(PanelShortcuts.PitWallGlances(1080, 0));
        }

        [Fact]
        public void A_companions_glance_is_new_and_a_face_or_pit_walls_is_not()
        {
            // v0.3.0-rc.7 registers no companion action (its CompanionActionNames is empty); the glance came
            // back with #362 after that cut, so it carries New for this release, as night mode does.
            Assert.True(PanelShortcuts.GlanceBinding(Contract.KindCompanion, Contract.CompanionPrefix, "Phone").IsNew);
            Assert.False(PanelShortcuts.GlanceBinding(Contract.KindPitWall, Contract.PitWallPrefix, "Pit wall").IsNew);
            Assert.False(PanelShortcuts.GlanceBinding(Contract.KindFace, Face, "Rim").IsNew);
            // The one flag the kind decides: everything else about the row is the same on every kind.
            var phone = PanelShortcuts.GlanceBinding(Contract.KindCompanion, Contract.CompanionPrefix, "Phone");
            Assert.Equal("Quick glance", phone.Label);
            Assert.Equal("Hold", phone.Press);
            Assert.Equal("Phone · quick glance", phone.BinderName);
        }

        [Fact]
        public void The_lights_card_binds_the_rig_actions_and_every_one_is_new()
        {
            var rows = PanelShortcuts.LightsBindings();
            Assert.Equal(Contract.RigActionNames(), rows.Select(r => r.Action));
            Assert.Equal(new[] { "Night mode", "Brightness up", "Brightness down" }, rows.Select(r => r.Label));
            // No released plugin registers any of the three (v0.3.0-rc.7 has none), so all three carry New,
            // Night mode too, where the artboard draws it untagged.
            Assert.Equal(new[] { true, true, true }, rows.Select(r => r.IsNew));
            Assert.Equal(new[] { "Night mode", "Brightness up", "Brightness down" }, rows.Select(r => r.BinderName));
            Assert.Equal(Contract.RigActionNames().Select(PanelShortcuts.RigActionDoes), rows.Select(r => r.Does));
            Assert.Equal(new[] { "toggles night mode", "turns the brightness up", "turns the brightness down" }, rows.Select(r => r.Does));
            Assert.All(rows, r => Assert.Equal("Tap", r.Press));
            // The rig test and the alert's dismissal are coming, and are the page's two greyed rows, each
            // read by the registry's title: a noun phrase, as plugin.md's voice replacements and every other
            // page's greyed rows have it, and the words search lists the row by and its automation name says.
            // Rulings 7 and 62 name which rows exist, not their copy.
            Assert.Equal(new[] { PanelSoon.RigTest, PanelSoon.AlertDismissal }, PanelShortcuts.SoonDrawn);
            Assert.Equal("Rig test", PanelSoon.RigTest.Title);
            Assert.Equal("Alert dismissal", PanelSoon.AlertDismissal.Title);
            var code = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Shortcuts.cs"));
            Assert.DoesNotContain("Run the Rig test", code);
            Assert.DoesNotContain("Dismiss the alert", code);
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
            // The brief's four crumbs, the screen's name second, as the artboard's "Devices › Phone › ..."
            // names its card; a companion with no name reads its kind rather than an empty crumb.
            Assert.Equal(new[] { "Devices", "Phone", "Controls and events", "NextScreen" }, PanelShortcuts.PagingCrumbs("Phone"));
            Assert.Equal(new[] { "Devices", "Garage tablet", "Controls and events", "NextScreen" }, PanelShortcuts.PagingCrumbs(" Garage tablet "));
            Assert.Equal(new[] { "Devices", "Companion", "Controls and events", "NextScreen" }, PanelShortcuts.PagingCrumbs("  "));
            Assert.Equal(new[] { "Devices", "Companion", "Controls and events", "NextScreen" }, PanelShortcuts.PagingCrumbs(null));
            Assert.Equal(PanelAttention.DevicesCrumb, PanelShortcuts.PagingCrumbs("Phone")[0]);
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

        [Fact]
        public void The_filter_leaves_a_card_only_with_a_row_and_says_so_only_when_the_page_is_empty()
        {
            // The screens' cards, then Lights, then Alerts.
            Assert.Equal(new[] { "Main dash", "Rim", "Lights", "Alerts" }, PanelShortcuts.CardOrder(new[] { "Main dash", "Rim" }, "Lights", "Alerts"));
            Assert.Equal(new[] { "Lights", "Alerts" }, PanelShortcuts.CardOrder(new string[0], "Lights", "Alerts"));

            // A card with no row to show is not drawn as a header alone.
            Assert.True(PanelShortcuts.CardShows(new[] { NotBound, Bound }, PanelShortcuts.FilterBound));
            Assert.False(PanelShortcuts.CardShows(new[] { NotBound, NotBound }, PanelShortcuts.FilterBound));
            Assert.False(PanelShortcuts.CardShows(new[] { Greyed }, PanelShortcuts.FilterBound));
            Assert.True(PanelShortcuts.CardShows(new[] { Greyed }, PanelShortcuts.FilterNotBound));
            Assert.False(PanelShortcuts.CardShows(new[] { Bound }, PanelShortcuts.FilterNotBound));
            Assert.False(PanelShortcuts.CardShows(new PanelShortcuts.RowState[0], PanelShortcuts.FilterAll));

            // The header rules itself off, so the first row under it draws no rule, unless the companion's
            // paging line sits between them; every later row draws one.
            Assert.False(PanelShortcuts.RuleAbove(true, false));
            Assert.True(PanelShortcuts.RuleAbove(true, true));
            Assert.True(PanelShortcuts.RuleAbove(false, false));
            Assert.True(PanelShortcuts.RuleAbove(false, true));

            // The empty line only when no row shows: never under rows it would contradict.
            Assert.Null(PanelShortcuts.EmptyLine(PanelShortcuts.FilterBound, true));
            Assert.Null(PanelShortcuts.EmptyLine(PanelShortcuts.FilterNotBound, true));
            Assert.Null(PanelShortcuts.EmptyLine(PanelShortcuts.FilterAll, false));
            Assert.Equal("Nothing is bound yet.", PanelShortcuts.EmptyLine(PanelShortcuts.FilterBound, false));
            Assert.Equal("Every shortcut is bound.", PanelShortcuts.EmptyLine(PanelShortcuts.FilterNotBound, false));
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
            // A short press on Zone A's next page, long presses on Zone A's and Band D's previous page: only the
            // two long presses are doubled.
            Assert.Equal("Keyboard · F9 takes zone A to its previous page and band D to its previous page on Rim.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), shortPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", false), longPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Band D", false), longPress),
            }).Single().Text);
            // One row bound on both gestures of a button answers both, so a long press elsewhere doubles it.
            Assert.Equal("Keyboard · F9 cycles zone A and takes zone A to its previous page on Rim.", PanelShortcuts.Clashes(new[]
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
            // The artboard's line, less its aside ("Fine if you meant it."), which voice.md deletes: the zone in
            // its running-text form, "zone B", as the artboard writes it.
            Assert.Equal("CSL Elite · 7 cycles zone B on both Main dash and Rim.", clash.Text);
            Assert.Equal("CSL Elite · 7", clash.Lead);
            Assert.Equal("cycles zone B on both Main dash and Rim.", clash.Rest);
            Assert.Equal("JoystickPlugin.CSL_Elite_B07", clash.Trigger);
        }

        [Fact]
        public void A_clash_line_reads_for_any_pair_of_rows()
        {
            Assert.Equal("cycles band D", PanelShortcuts.ZoneDoes("Band D", true));
            Assert.Equal("takes band D to its previous page", PanelShortcuts.ZoneDoes("Band D", false));
            // A screen's uses come before the rig's, so "on Rim" is said of them alone.
            Assert.Equal("Keyboard · N cycles zone A on Rim and toggles night mode.", PanelShortcuts.Clashes(new[]
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
            // A verb the rows share is said once.
            Assert.Equal("Keyboard · B turns the brightness up and down.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.B", null, PanelShortcuts.RigActionDoes(Contract.BrightnessUpAction)),
                Use("KeyboardReaderPlugin.B", null, PanelShortcuts.RigActionDoes(Contract.BrightnessDownAction)),
            }).Single().Text);
        }

        [Fact]
        public void A_clash_line_says_each_screens_uses_then_the_rigs()
        {
            Assert.Equal("Keyboard · N cycles zone A and zone C on Rim, and toggles night mode.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.N", null, PanelShortcuts.RigActionDoes(Contract.ToggleNightModeAction)),
                Use("KeyboardReaderPlugin.N", "Rim", PanelShortcuts.ZoneDoes("Zone A", true)),
                Use("KeyboardReaderPlugin.N", "Rim", PanelShortcuts.ZoneDoes("Zone C", true)),
            }).Single().Text);
            Assert.Equal("Keyboard · F9 cycles zone A on Rim and holds the quick glance on Main dash.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true)),
                Use("KeyboardReaderPlugin.F9", "Main dash", PanelShortcuts.GlanceDoes),
            }).Single().Text);
        }

        [Fact]
        public void A_use_is_told_apart_by_its_screen()
        {
            // The place is what keeps two screens' uses of one action apart: without it, Main dash's and Rim's
            // zone B would be one use, and the artboard's line would never be drawn.
            var zoneB = PanelShortcuts.ZoneDoes("Zone B", true);
            Assert.Single(PanelShortcuts.Clashes(new[] { Use("JoystickPlugin.CSL_Elite_B07", "Main dash", zoneB), Use("JoystickPlugin.CSL_Elite_B07", "Rim", zoneB) }));
            Assert.Empty(PanelShortcuts.Clashes(new[] { Use("JoystickPlugin.CSL_Elite_B07", null, zoneB), Use("JoystickPlugin.CSL_Elite_B07", null, zoneB) }));
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
            // The binder's slot: the artboard's 260 binder, 16 of gap and 120 of buttons after it.
            Assert.Equal(260 + 16 + 120, PanelShortcuts.BinderWidth);
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
            // Two clash lines, 8 apart.
            Assert.Equal(8, PanelShortcuts.BannerStackGap);
        }

        /// <summary>The page's own numbers, where the artboard has none: it draws no stacked row, no glance
        /// caption, no stacked header and no floor under SimHub's editor.</summary>
        [Fact]
        public void Its_own_geometry_where_the_artboard_has_none()
        {
            // The glance's caption 4 under its name, and a stacked binder 8 under the name and the press.
            Assert.Equal(4, PanelShortcuts.CaptionGap);
            Assert.Equal(8, PanelShortcuts.StackGap);
            // The least a name keeps beside the press and the binder before the binder moves under it.
            Assert.Equal(160, PanelShortcuts.NameMinWidth);
            // BuildBinder's floor under SimHub's editor, wherever it is drawn.
            Assert.Equal(260, PanelShortcuts.BinderMinWidth);
            // The filter's gap under the caption when the header stacks, which the artboard's never does (its
            // header's own gap, beside the caption, is 24).
            Assert.Equal(12, PanelShortcuts.FilterGapStacked);
        }

        [Fact]
        public void A_row_puts_its_binder_under_its_name_only_where_the_three_cannot_sit_side_by_side()
        {
            // 2 of card rules, 32 of padding, 160 of name, 16 + 90 + 16 + 396 of press and binder.
            Assert.Equal(712, PanelShortcuts.RowStackBelow);
            Assert.Equal(PanelShortcuts.RowStackBelow, 2 + 2 * PanelShortcuts.RowPaddingX + PanelShortcuts.NameMinWidth
                + PanelShortcuts.RowGap + PanelShortcuts.PressWidth + PanelShortcuts.RowGap + PanelShortcuts.BinderWidth);
            Assert.False(PanelShortcuts.RowStacks(Column(1200)));
            Assert.False(PanelShortcuts.RowStacks(Column(3840)));
            Assert.False(PanelShortcuts.RowStacks(712));
            Assert.True(PanelShortcuts.RowStacks(711));
            // Nothing a row draws moves from RowStackBelow up, which is why the page reads the width only
            // that far: a row there is laid out as one in a 4K column.
            Assert.Equal(PanelShortcuts.RowStacks(Column(3840)), PanelShortcuts.RowStacks(PanelShortcuts.RowStackBelow));
            Assert.Equal(PanelShortcuts.BinderSlot(Column(3840), PanelShortcuts.RowStacks(Column(3840))), PanelShortcuts.BinderSlot(PanelShortcuts.RowStackBelow, PanelShortcuts.RowStacks(PanelShortcuts.RowStackBelow)));
            // The rail's content at the narrowest full-sidebar width: the binder goes under the name there.
            Assert.True(PanelShortcuts.RowStacks(679));
            Assert.True(PanelShortcuts.RowStacks(400));
        }

        [Fact]
        public void Every_row_gives_simhubs_editor_one_slot_of_fixed_width()
        {
            // Beside the name the slot is never narrower than BuildBinder's floor.
            Assert.True(PanelShortcuts.BinderWidth >= PanelShortcuts.BinderMinWidth);
            var shell = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.cs"));
            Assert.Contains("MinWidth = " + PanelShortcuts.BinderMinWidth.ToString(System.Globalization.CultureInfo.InvariantCulture) + " }", shell);
            // Beside the name, the same slot on every row, whatever the content's width.
            Assert.Equal(PanelShortcuts.BinderWidth, PanelShortcuts.BinderSlot(PanelShortcuts.RowStackBelow, false));
            Assert.Equal(PanelShortcuts.BinderWidth, PanelShortcuts.BinderSlot(Column(1200), false));
            Assert.Equal(PanelShortcuts.BinderWidth, PanelShortcuts.BinderSlot(Column(3840), false));
            // Under it, the whole row inside its padding, which may be less than the floor: the row lowers the
            // editor's MinWidth to the slot there, so SimHub's template is laid out in it rather than clipped.
            Assert.Equal(566, PanelShortcuts.BinderSlot(600, true));
            Assert.Equal(677, PanelShortcuts.BinderSlot(711, true));
            Assert.Equal(266, PanelShortcuts.BinderSlot(300, true));
            Assert.Equal(0, PanelShortcuts.BinderSlot(20, true));
            var code = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Shortcuts.cs"));
            Assert.Contains("control.MinWidth = Math.Min(control.MinWidth, layout.Binder);", code);
            // SimHub's editor draws no name of its own: the row's beside it says what it binds.
            Assert.Empty(PanelShortcuts.EditorName);
        }

        /// <summary>
        /// SimHub's editor loses its 2* name column, so its bindings have the whole slot: its template's shape
        /// is checked before it is touched, and the name is collapsed with its column, so its line no longer
        /// sets the row's height.
        /// </summary>
        [Fact]
        public void Simhubs_editor_gives_its_bindings_the_whole_slot()
        {
            var code = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Shortcuts.cs"));
            Assert.Contains("control.Loaded += (sender, args) => ShortcutsDropNameColumn(control);", code);
            var drop = Between(code, "private static void ShortcutsDropNameColumn(", "catch (Exception ex)");
            Assert.Contains("editor.Template.FindName(\"brd\", editor) as Border;", drop);
            Assert.Contains("if (grid == null || grid.ColumnDefinitions.Count != 2) return;", drop);
            Assert.Contains("if (!grid.ColumnDefinitions[0].Width.IsStar || !grid.ColumnDefinitions[1].Width.IsStar) return;", drop);
            Assert.Contains("foreach (var child in named) child.Visibility = Visibility.Collapsed;", drop);
            Assert.Contains("grid.ColumnDefinitions[0].Width = new GridLength(0);", drop);
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

        /// <summary>
        /// The half of the watch that takes it up: a mapping's trigger or press type changed in place (the
        /// short and long press split turns on the press type), a model whose Triggers is replaced, and a model
        /// replaced, which SimHub 9.12.6's ControlsEditor_Loaded does before the driver can press Add, so the
        /// watch is taken up again on Loaded and on Model. The names are SimHub.Plugins.dll's, and
        /// PanelObligationsTests.A_glance_rebound_in_place_is_held_again pins the same for HoldWhilePressed.
        /// Every live row on every card is watched.
        /// </summary>
        [Fact]
        public void The_bindings_watch_is_taken_up_on_every_row_whenever_the_editor_or_its_model_moves()
        {
            var code = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Shortcuts.cs"));
            var body = code.Substring(code.IndexOf("private void ShortcutsWatch(", StringComparison.Ordinal));
            Assert.Contains("args.PropertyName == \"Trigger\" || args.PropertyName == \"PressType\"", Between(body, "mappingChanged = (sender, args) =>", "};"));
            Assert.Contains("args.PropertyName != \"Triggers\"", Between(body, "modelChanged = (sender, args) =>", "};"));
            Assert.Contains("args.PropertyName == \"Model\") attach();", body);
            Assert.Contains("editor.Loaded += (sender, args) => attach();", body);
            var page = Between(code, "private FrameworkElement BuildShortcutsPage(", "var page = ShortcutsTitleTagged(");
            Assert.Matches(@"foreach \(var row in groups\.SelectMany\(group => group\.Rows\)\)\s*\{\s*var editor = row\.Editor as ControlsEditor;\s*if \(editor != null\) ShortcutsWatch\(editor, changed\);\s*if \(editor != null\) editors\.Add\(editor\);", page);
        }

        /// <summary>
        /// Every editor follows SimHub's own list, not only what it was given when it loaded: SimHub's picker
        /// can delete another action's mapping from that list alone, and SimHub 9.12.6's editor never hears of
        /// it. On each change SimHub reports, once settled, each model is brought back into line in place and
        /// the page reads again; the static event is held only while the page is on screen.
        /// </summary>
        [Fact]
        public void The_page_reads_again_whenever_simhub_reports_a_mapping_change()
        {
            var code = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Shortcuts.cs"));
            Assert.Contains("ShortcutsFollowSimHub(page, editors, changed, () => dropped);", code);
            var follow = Between(code, "private void ShortcutsFollowSimHub(", "private static void ShortcutsReconcile(");
            Assert.Matches(@"Dispatcher\.BeginInvoke\(DispatcherPriority\.Background, new Action\(\(\) =>\s*\{\s*pending = false;\s*if \(dropped\(\)\) return;\s*foreach \(var editor in editors\) ShortcutsReconcile\(editor\);\s*changed\(\);", follow);
            Assert.Contains("Action release = () => PluginManager.InputMappingsChanged -= mappingsChanged;", follow);
            Assert.Matches(@"page\.Loaded \+= \(sender, args\) =>\s*\{\s*release\(\);\s*if \(!dropped\(\)\) PluginManager\.InputMappingsChanged \+= mappingsChanged;", follow);
            Assert.Contains("page.Unloaded += (sender, args) => release();", follow);
            Assert.Contains("OnDrop(release);", follow);
            Assert.Single(Regex.Matches(code, @"InputMappingsChanged \+="));

            var reconcile = Between(code, "private static void ShortcutsReconcile(", "catch (Exception ex)");
            Assert.Contains("if (PluginManager.GetInstance() == null) return;", reconcile);
            Assert.Contains("var simhub = new ControlsEditorModel(editor.ActionName, null).Triggers;", reconcile);
            Assert.Contains("model.Triggers.Where(mapping => !simhub.Any(kept => ReferenceEquals(kept, mapping))).ToList()", reconcile);
            Assert.Contains("model.Triggers.Remove(gone);", reconcile);
            Assert.Contains("simhub.Where(mapping => !model.Triggers.Any(held => ReferenceEquals(held, mapping))).ToList()", reconcile);
            Assert.Contains("model.Triggers.Add(added);", reconcile);
            // In place: never a new model, which would leave HoldWhilePressed watching the old one.
            Assert.DoesNotContain("ModelProperty", code);
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
        /// Each card's builder wires its rows as the model gives them: the screen's name as the place, so that two
        /// screens' uses of one action stay apart in the clash line; SimHub's editor for every live row, which
        /// is what the counts, the filter and the clash line read; each kind's own glance caption; and the kind
        /// dispatch, which alone gives a round screen no card.
        /// </summary>
        [Fact]
        public void Each_card_wires_its_rows_to_its_screen_and_its_own_glance()
        {
            var code = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Shortcuts.cs"));
            var face = Between(code, "private ShortcutsGroupState BuildShortcutsFace(", "return group;");
            Assert.Contains("foreach (var binding in PanelShortcuts.FaceBindings(screen.Namespace, screen.Name))", face);
            Assert.Contains("ShortcutsBinding(group, screen.Name, binding, BuildBinder(binding.Action, binding.BinderName), null, layout);", face);
            Assert.Contains("ShortcutsBinding(group, screen.Name, glance, BuildBinder(glance.Action, glance.BinderName, hold: true), Ui.Caption(PanelCopy.FaceGlance), layout);", face);
            var wall = Between(code, "private ShortcutsGroupState BuildShortcutsPitWall(", "return group;");
            Assert.Contains("ShortcutsBinding(group, screen.Name, glance, BuildBinder(glance.Action, glance.BinderName, hold: true), Ui.Caption(PanelCopy.PitWallGlance), layout);", wall);
            var companion = Between(code, "private ShortcutsGroupState BuildShortcutsCompanion(", "return group;");
            Assert.Contains("ShortcutsBinding(group, screen.Name, glance, BuildBinder(glance.Action, glance.BinderName, hold: true), Ui.Caption(PanelCopy.CompanionGlance), layout);", companion);
            var lights = Between(code, "private ShortcutsGroupState BuildShortcutsLights(", "return group;");
            Assert.Contains("foreach (var binding in PanelShortcuts.LightsBindings())", lights);
            Assert.Contains("ShortcutsBinding(group, null, binding, BuildBinder(binding.Action, binding.BinderName), null, layout);", lights);

            var page = Between(code, "private FrameworkElement BuildShortcutsPage(", "var lights = BuildShortcutsLights(layout);");
            Assert.Matches(@"if \(screen\.IsFace\) screens\.Add\(BuildShortcutsFace\(screen, layout\)\);\s*else if \(screen\.IsPitWall && PanelShortcuts\.PitWallGlances\(screen\.Width, screen\.Height\)\) screens\.Add\(BuildShortcutsPitWall\(screen, layout\)\);\s*else if \(screen\.IsCompanion\) screens\.Add\(BuildShortcutsCompanion\(screen, layout\)\);", page);
            Assert.Equal(3, Regex.Matches(page, @"screens\.Add\(").Count);
        }

        /// <summary>
        /// A row the filter hides while it holds keyboard focus (bound under Not bound, cleared under Bound)
        /// hands focus to a row still shown, or the filter, rather than letting it leave the panel.
        /// </summary>
        [Fact]
        public void A_row_the_filter_hides_hands_its_focus_on()
        {
            var code = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Shortcuts.cs"));
            var evaluate = Between(code, "private void ShortcutsEvaluate(", "banner.Children.Clear();");
            Assert.Contains("var focused = groups.SelectMany(group => group.Rows).FirstOrDefault(row => row.Shown.IsKeyboardFocusWithin);", evaluate);
            Assert.Contains("if (focused != null && focused.Shown.Visibility != Visibility.Visible) ShortcutsKeepFocus(groups, focused, filter);", evaluate);
            Assert.True(evaluate.IndexOf("var focused =", StringComparison.Ordinal) < evaluate.IndexOf("row.Shown.Visibility = shows", StringComparison.Ordinal), "focus is read before the rows are hidden");
            var keep = Between(code, "private static void ShortcutsKeepFocus(", "\n        }");
            Assert.Contains("row.Bindable && row.Shown.Visibility == Visibility.Visible", keep);
            Assert.Contains("filter.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));", keep);
        }

        /// <summary>The model's geometry is what the view draws with: the header's gaps, the crumbs' gap, the
        /// card name wrapping at the header's width and the editor filling its slot.</summary>
        [Fact]
        public void The_page_draws_at_the_models_geometry()
        {
            var code = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Shortcuts.cs"));
            Assert.Contains("filter.Margin = new Thickness(PanelShell.RowGap, -PanelShortcuts.FilterRaise, 0, 0);", code);
            Assert.Contains("filter.Margin = new Thickness(0, PanelShortcuts.FilterGapStacked, 0, 0);", code);
            Assert.Contains("header.Margin = new Thickness(0, PanelShortcuts.IntroGap - PanelShell.SectionGapFor(PanelPage.Shortcuts), 0, 0);", code);
            Assert.Contains("crumbs.Margin = new Thickness(0, PanelShortcuts.LeadGap, 0, 0);", code);
            var card = Between(code, "private static ShortcutsGroupState ShortcutsCard(", "return new ShortcutsGroupState");
            Assert.Contains("name.TextWrapping = TextWrapping.Wrap;", card);
            Assert.DoesNotContain("TextTrimming", card);
            Assert.Contains("if (editor != null) editor.HorizontalAlignment = HorizontalAlignment.Stretch;", code);
            Assert.Contains("slot.Margin = new Thickness(0, PanelShortcuts.StackGap, 0, 0);", code);
            Assert.Contains("caption.Margin = new Thickness(0, PanelShortcuts.CaptionGap, 0, 0);", code);
            // The clash sentence keeps a message line's measure in a column with no ceiling.
            var clash = Between(code, "private static Border ShortcutsClashLine(", "return line;");
            Assert.Contains("text.MaxWidth = BodyWidth;", clash);
            Assert.Contains("text.HorizontalAlignment = HorizontalAlignment.Left;", clash);
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
            Assert.Contains("Ui.Crumbs(PanelShortcuts.PagingCrumbs(screen.Name))", Between(code, "private ShortcutsGroupState BuildShortcutsCompanion(", "return group;"));
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
            // The Map tags "Every button in one list" New, so the page's title carries the tag, as Rig's does.
            Assert.Contains("var page = ShortcutsTitleTagged(PageLayout(PanelShortcuts.Title, null, sections.ToArray()));", code);
            Assert.Contains("stack.Children.Insert(0, Ui.HStack(12, title, Ui.NewTag()));", code);
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
            // greyed rows by the registry's titles, and each card's line beside its name.
            Assert.Contains("ShortcutsRow(binding.Label, binding.Press, editor, layout, caption, tags);", code);
            Assert.Contains("var tags = binding.IsNew ? new FrameworkElement[] { Ui.NewTag() } : new FrameworkElement[0];", code);
            Assert.Contains("ShortcutsRow(item.Title, PanelShortcuts.Tap, chip, layout, null);", code);
            Assert.Equal(3, Regex.Matches(code, Regex.Escape("PanelShortcuts.GroupDetail(screen.Name, screen.Kind, screen.Width, screen.Height)")).Count);
            Assert.Contains("ShortcutsCard(PanelShortcuts.RigGroupTitle, PanelShortcuts.RigGroupDetail, null);", code);
            Assert.Contains("ShortcutsCard(PanelShortcuts.AlertsGroupTitle, null, null);", code);

            // Every row's binder in one slot of the model's width, the gap on the slot, never on SimHub's editor,
            // whose template draws its own Margin a second time; the editor names nothing of its own.
            // The width is read only up to RowStackBelow, where the drawing stops changing (the shell's rule
            // 11): past it a resize would re-create every SimHub editor on the page for nothing.
            Assert.Contains("var layout = new ShortcutsLayout(ContentWidthUpTo(PanelShortcuts.RowStackBelow));", code);
            Assert.DoesNotMatch(@"\bContentWidth\b", code);
            Assert.Contains("Stacks = PanelShortcuts.RowStacks(contentWidth);", code);
            Assert.Contains("Binder = PanelShortcuts.BinderSlot(contentWidth, Stacks);", code);
            Assert.Contains("control.Margin = new Thickness(0);", code);
            Assert.DoesNotContain("control.Margin = new Thickness(PanelShortcuts", code);
            Assert.Contains("var slot = new Border { Child = control, Width = layout.Binder,", code);
            Assert.Contains("new ColumnDefinition { Width = new GridLength(PanelShortcuts.RowGap + layout.Binder) }", code);
            Assert.DoesNotContain("GridLength.Auto });\n                    control", code.Replace("\r\n", "\n"));
            Assert.Contains("control.FriendlyName = PanelShortcuts.EditorName;", code);
            // A card's name is measured at the header's width, so a long one wraps there.
            Assert.Contains("var titleLine = new WrapPanel", code);

            // What the model decides from the bindings read, each drawn through it: the row's state, the
            // page's readability, the filter, the counts, and ruling 60's three
            // hides when a row cannot be read.
            Assert.Contains("states[row] = PanelShortcuts.StateOf(row.Bindable, read == null ? (int?)null : read.Count);", code);
            Assert.Contains("var readable = PanelShortcuts.Readable(states.Values);", code);
            Assert.Contains("var chosen = PanelShortcuts.FilterFor(shortcutsFilter, null, readable);", code);
            Assert.Contains("filter.Visibility = readable ? Visibility.Visible : Visibility.Collapsed;", code);
            Assert.Contains("PanelShortcuts.Shows(chosen, states[row])", code);
            Assert.Contains("var count = PanelShortcuts.CardCount(group.Rows.Select(row => states[row]), readable);", code);
            Assert.Contains("if (readable)\n", code.Replace("\r\n", "\n"));
            Assert.Contains("foreach (var clash in PanelShortcuts.Clashes(all))", code);
            // A binding's presses come off SimHub's mapping by the name of its press type.
            Assert.Contains("PanelShortcuts.FiresOn(mapping.PressType.ToString())", code);

            // What the filter leaves on screen, each the model's: the cards' order, a card only with a row, the
            // rule above a row, and the empty line only when no row shows.
            Assert.Contains("var groups = PanelShortcuts.CardOrder(screens, lights, alerts);", code);
            Assert.Contains("var groupShown = PanelShortcuts.CardShows(group.Rows.Select(row => states[row]), chosen);", code);
            Assert.Contains("group.Card.Visibility = groupShown ? Visibility.Visible : Visibility.Collapsed;", code);
            Assert.Contains("row.Row.BorderThickness = new Thickness(0, PanelShortcuts.RuleAbove(firstShown, group.HasLead) ? PanelMetrics.BorderWeight : 0, 0, 0);", code);
            Assert.Contains("var emptyText = PanelShortcuts.EmptyLine(chosen, anyShown);", code);

            // The anchor puts the filter back to All on the first build after the page is opened, and never on
            // a rebuild in place, which keeps the route and its anchor: OnLeave runs on Go alone.
            var landing = Regex.Match(code, @"if \(shortcutsLanding\)\s*\{\s*shortcutsFilter = PanelShortcuts\.FilterFor\(shortcutsFilter, to == null \? null : to\.Anchor, true\);\s*shortcutsLanding = false;\s*\}\s*OnLeave\(""Shortcuts\.filterLanding"", \(\) => shortcutsLanding = true\);");
            Assert.True(landing.Success, "the filter's reset for an anchor is not gated on the page's first build");
            Assert.Single(Regex.Matches(code, @"PanelShortcuts\.FilterFor\(shortcutsFilter, to"));
        }
    }
}
