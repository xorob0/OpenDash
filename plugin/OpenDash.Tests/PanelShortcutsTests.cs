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
            Assert.Equal("Rim · Quick glance", PanelShortcuts.GlanceBinderName("Rim"));
            Assert.Equal("Rim · " + PanelShortcuts.QuickGlanceTitle, PanelShortcuts.GlanceBinderName("Rim"));
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
            // Whatever the name's case: "pit wall" says the kind as well as "Pit wall" does.
            Assert.Equal("1920 × 1080", PanelShortcuts.GroupDetail("pit wall", Contract.KindPitWall, 1920, 1080));
            Assert.Equal("1920 × 1080", PanelShortcuts.GroupDetail(" PIT WALL ", Contract.KindPitWall, 1920, 1080));
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
        public void A_row_says_its_press_until_simhubs_editor_says_it_and_a_hold_always()
        {
            // Not bound, greyed or not read: the row's own "Tap" is the only word on the press.
            foreach (var state in new[] { PanelShortcuts.RowState.NotBound, PanelShortcuts.RowState.Greyed, PanelShortcuts.RowState.Unread })
            {
                Assert.True(PanelShortcuts.ShowsPress(state, false));
                Assert.True(PanelShortcuts.ShowsPress(state, true));
            }
            // Bound: SimHub's editor prints the binding's press type, which a tapped row's word could contradict.
            Assert.False(PanelShortcuts.ShowsPress(PanelShortcuts.RowState.Bound, false));
            // A held glance is held whatever press type SimHub shows (#435), so "Hold" stays.
            Assert.True(PanelShortcuts.ShowsPress(PanelShortcuts.RowState.Bound, true));
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
            Assert.Equal("Rim · Quick glance", glance.BinderName);
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
        public void A_portrait_pit_wall_has_a_card_only_while_its_glance_is_bound()
        {
            // A landscape wall always has its card; a portrait wall only while its glance is still bound (or
            // cannot be read), so that the binding, live in SimHub and in the sidebar's count, can be cleared.
            Assert.True(PanelShortcuts.PitWallCard(1920, 1080, false));
            Assert.True(PanelShortcuts.PitWallCard(1920, 1080, true));
            Assert.False(PanelShortcuts.PitWallCard(1080, 1920, false));
            Assert.True(PanelShortcuts.PitWallCard(1080, 1920, true));
            // Contract registers the glance on every pit wall, whatever its orientation: that is why a bound
            // one has to stay in sight.
            Assert.Equal(new[] { Contract.HoldQuickGlanceActionFor(Contract.PitWallPrefix) }, Contract.ScreenActionNames(Contract.KindPitWall, Contract.PitWallPrefix));
            // Its caption says the glance does nothing there, where PanelCopy.PitWallGlance would say it puts
            // a zone back, and still ends on the hold sentence every glance row's caption ends on (#435): the
            // row is a hold binder, which turns a picked press type back into a hold.
            Assert.Equal("Does nothing on a portrait pit wall. Bound as a hold, whatever press type you pick.", PanelShortcuts.PortraitGlanceCaption);
            Assert.EndsWith(PanelCopy.GlanceBoundAsHold, PanelShortcuts.PortraitGlanceCaption);
            Assert.EndsWith(PanelCopy.GlanceBoundAsHold, PanelCopy.PitWallGlance);
        }

        /// <summary>
        /// The cards' bound rows add up to the sidebar's Shortcuts count, which counts every bound action of
        /// every screen (ScreenInstance.ActionNames) and the rig's: each kind's card draws exactly its screen's
        /// actions, the companion's glance included, less a portrait wall's glance only while nothing is bound
        /// on it, which the sidebar does not count either.
        /// </summary>
        [Fact]
        public void The_cards_draw_every_action_the_sidebar_counts()
        {
            var face = PanelShortcuts.FaceBindings(Face, "Rim").Select(r => r.Action).Concat(new[] { PanelShortcuts.GlanceBinding(Contract.KindFace, Face, "Rim").Action });
            Assert.Equal(Contract.ScreenActionNames(Contract.KindFace, Face).OrderBy(a => a, StringComparer.Ordinal), face.OrderBy(a => a, StringComparer.Ordinal));
            Assert.Equal(Contract.ScreenActionNames(Contract.KindCompanion, Contract.CompanionPrefix), new[] { PanelShortcuts.GlanceBinding(Contract.KindCompanion, Contract.CompanionPrefix, "Phone").Action });
            Assert.Equal(Contract.RigActionNames(), PanelShortcuts.LightsBindings().Select(r => r.Action));
            // A pit wall's one action is drawn unless the wall is portrait and nothing is bound on it.
            foreach (var size in new[] { new[] { 1920, 1080 }, new[] { 1080, 1920 }, new[] { 0, 0 } })
            {
                foreach (var bound in new[] { false, true })
                {
                    var drawn = PanelShortcuts.PitWallCard(size[0], size[1], bound);
                    var counted = bound;
                    // A card not drawn is never one whose binding the sidebar counts.
                    Assert.True(drawn || !counted, size[0] + "x" + size[1] + (bound ? " bound" : " not bound"));
                }
            }
        }

        [Fact]
        public void A_pit_wall_glances_only_in_landscape()
        {
            // The glance borrows a landscape zone; the portrait package draws only its own four, so a held
            // key there would change nothing.
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
            // v0.3.0-rc.7 registered the companion's glance, but it moved a property no package read (#435)
            // and that release's panel had no binder for it; it works from #362, after that cut, so this
            // release is the first where it does anything, and it carries New, as night mode does.
            Assert.True(PanelShortcuts.GlanceBinding(Contract.KindCompanion, Contract.CompanionPrefix, "Phone").IsNew);
            Assert.False(PanelShortcuts.GlanceBinding(Contract.KindPitWall, Contract.PitWallPrefix, "Pit wall").IsNew);
            Assert.False(PanelShortcuts.GlanceBinding(Contract.KindFace, Face, "Rim").IsNew);
            // The one flag the kind decides: everything else about the row is the same on every kind.
            var phone = PanelShortcuts.GlanceBinding(Contract.KindCompanion, Contract.CompanionPrefix, "Phone");
            Assert.Equal("Quick glance", phone.Label);
            Assert.Equal("Hold", phone.Press);
            Assert.Equal("Phone · Quick glance", phone.BinderName);
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
            // Only the names SimHub gives, as the Screens page draws them: the screen's name is OpenDash's, not
            // a device SimHub lists, and a companion in a window is under no Devices, so the sentence above
            // the crumbs says where to start and the crumbs carry on from there.
            Assert.Equal(new[] { "Controls and events", "NextScreen" }, PanelShortcuts.PagingCrumbs);
            Assert.DoesNotContain(PanelAttention.DevicesCrumb, PanelShortcuts.PagingCrumbs);
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
            // Each value under its own label, as BuildSegmented pairs them by position: swapped, "Bound" would
            // show the rows not bound.
            Assert.Equal(new[] { PanelShortcuts.FilterAll, PanelShortcuts.FilterBound, PanelShortcuts.FilterNotBound }, PanelShortcuts.FilterValues);
            Assert.Equal(new[] { "all", "bound", "not-bound" }, PanelShortcuts.FilterValues);
            Assert.False(PanelShortcuts.Shows(PanelShortcuts.FilterValues[1], NotBound));
            Assert.False(PanelShortcuts.Shows(PanelShortcuts.FilterValues[2], Bound));
            // The group's name, which a screen reader alone reads: a noun, as voice.md has every label, where
            // the artboard's aria-label is the bare verb "Show".
            Assert.Equal("Filter", PanelShortcuts.FilterTitle);

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
            // An empty anchor is no row to land on, and keeps the choice.
            Assert.Equal(PanelShortcuts.FilterBound, PanelShortcuts.FilterFor(PanelShortcuts.FilterBound, "", true));
            Assert.Equal(PanelShortcuts.FilterNotBound, PanelShortcuts.FilterFor(PanelShortcuts.FilterNotBound, "", true));

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
            Assert.Equal("Keyboard · F9 takes zone A and zone D to their previous pages on Rim.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), shortPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", false), longPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Band D", false), longPress),
            }).Single().Text);
            // One row bound on both gestures of a button answers both, so a long press elsewhere doubles it,
            // and so does a short press, whichever of its two bindings SimHub lists first.
            Assert.Equal("Keyboard · F9 cycles zone A and takes zone A to its previous page on Rim.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), shortPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), longPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", false), longPress),
            }).Single().Text);
            Assert.Equal("Keyboard · F9 cycles zone A and takes zone A to its previous page on Rim.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), longPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), shortPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", false), longPress),
            }).Single().Text);
            Assert.Equal("Keyboard · F9 cycles zone A and zone D on Rim.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), shortPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), longPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Band D", true), shortPress),
            }).Single().Text);
            Assert.Equal("Keyboard · F9 cycles zone A and zone D on Rim.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), longPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), shortPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Band D", true), shortPress),
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

            // The artboard's "Row bound with clash": both zone B rows it names are marked, Main dash's zone C,
            // on another button, is not, and nor is a row the line does not name.
            Assert.Equal(new[] { "Main dash", "Rim" }, clash.Uses.Select(use => use.Place));
            Assert.True(PanelShortcuts.Marks(clashes, "Main dash", zoneB));
            Assert.True(PanelShortcuts.Marks(clashes, "Rim", zoneB));
            Assert.False(PanelShortcuts.Marks(clashes, "Main dash", PanelShortcuts.ZoneDoes("Zone C", true)));
            Assert.False(PanelShortcuts.Marks(clashes, "Pit wall", zoneB));
            Assert.False(PanelShortcuts.Marks(clashes, null, zoneB));
            Assert.False(PanelShortcuts.Marks(null, "Rim", zoneB));
            Assert.False(PanelShortcuts.Marks(new PanelShortcuts.Clash[] { null }, "Rim", zoneB));
        }

        [Fact]
        public void A_row_is_marked_only_on_the_gesture_that_doubles_it()
        {
            // A short press on Zone A's next page beside two long presses: only the two long presses clash, so
            // only their rows are marked.
            var clashes = PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true), PanelShortcuts.Fires.OnShortPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", false), PanelShortcuts.Fires.OnLongPress),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Band D", false), PanelShortcuts.Fires.OnLongPress),
            });
            Assert.False(PanelShortcuts.Marks(clashes, "Rim", PanelShortcuts.ZoneDoes("Zone A", true)));
            Assert.True(PanelShortcuts.Marks(clashes, "Rim", PanelShortcuts.ZoneDoes("Zone A", false)));
            Assert.True(PanelShortcuts.Marks(clashes, "Rim", PanelShortcuts.ZoneDoes("Band D", false)));
            // The rig's own rows are placed nowhere and marked by what they do.
            var rig = PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.N", null, PanelShortcuts.RigActionDoes(Contract.ToggleNightModeAction)),
                Use("KeyboardReaderPlugin.N", "Rim", PanelShortcuts.ZoneDoes("Zone A", true)),
            });
            Assert.True(PanelShortcuts.Marks(rig, null, PanelShortcuts.RigActionDoes(Contract.ToggleNightModeAction)));
            Assert.False(PanelShortcuts.Marks(rig, null, PanelShortcuts.RigActionDoes(Contract.BrightnessUpAction)));
            // No clash, no mark.
            Assert.False(PanelShortcuts.Marks(PanelShortcuts.Clashes(new[] { Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.GlanceDoes) }), "Rim", PanelShortcuts.GlanceDoes));
        }

        [Fact]
        public void A_clash_line_reads_for_any_pair_of_rows()
        {
            // Every zone mid-sentence is "zone" and its letter, D included, as the Screens page's warning
            // writes it ("Zone A and zone D both show the relative."): "band D" is the panel's own word.
            Assert.Equal("cycles zone D", PanelShortcuts.ZoneDoes("Band D", true));
            Assert.Equal("takes zone D to its previous page", PanelShortcuts.ZoneDoes("Band D", false));
            Assert.Equal("cycles zone B", PanelShortcuts.ZoneDoes("Zone B", true));
            Assert.Equal(new[] { "zone A", "zone B", "zone C", "zone D" }, Contract.FaceZoneLetters.Select(l => PanelShortcuts.ZoneDoes(PanelFacePlan.ZoneLabel(l), true).Substring("cycles ".Length)));
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

        /// <summary>A third use never makes the line say twice what two uses share: one thing done on two
        /// screens, a verb two rows share and a previous page taken on several zones are each said once.</summary>
        [Fact]
        public void A_clash_line_says_once_what_its_uses_share_however_many_there_are()
        {
            // The artboard's doubling, and night mode on the same button.
            Assert.Equal("CSL Elite · 7 cycles zone B on both Main dash and Rim, and toggles night mode.", PanelShortcuts.Clashes(new[]
            {
                Use("JoystickPlugin.CSL_Elite_B07", "Main dash", PanelShortcuts.ZoneDoes("Zone B", true)),
                Use("JoystickPlugin.CSL_Elite_B07", "Rim", PanelShortcuts.ZoneDoes("Zone B", true)),
                Use("JoystickPlugin.CSL_Elite_B07", null, PanelShortcuts.RigActionDoes(Contract.ToggleNightModeAction)),
            }).Single().Text);
            // The rig's three rows: the verb the two brightness rows share is said once beside night mode's.
            Assert.Equal("CSL Elite · 7 toggles night mode and turns the brightness up and down.", PanelShortcuts.Clashes(new[]
            {
                Use("JoystickPlugin.CSL_Elite_B07", null, PanelShortcuts.RigActionDoes(Contract.ToggleNightModeAction)),
                Use("JoystickPlugin.CSL_Elite_B07", null, PanelShortcuts.RigActionDoes(Contract.BrightnessUpAction)),
                Use("JoystickPlugin.CSL_Elite_B07", null, PanelShortcuts.RigActionDoes(Contract.BrightnessDownAction)),
            }).Single().Text);
            // Two screens' glances and a third screen's zone.
            Assert.Equal("Keyboard · F9 holds the quick glance on both Pit wall and Main dash, and cycles zone A on Rim.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Pit wall", PanelShortcuts.GlanceDoes),
                Use("KeyboardReaderPlugin.F9", "Main dash", PanelShortcuts.GlanceDoes),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true)),
            }).Single().Text);
            // Every zone's previous page on one button: the tail is said once, for them all.
            Assert.Equal("Keyboard · F9 takes zone A, zone B, zone C and zone D to their previous pages on Rim.", PanelShortcuts.Clashes(
                Contract.FaceZoneLetters.Select(l => Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes(PanelFacePlan.ZoneLabel(l), false)))).Single().Text);
            // A clause whose screens are some of a later one's is not merged into it: Main dash is still named.
            Assert.Equal("Keyboard · F9 cycles zone A on Rim, and holds the quick glance on both Rim and Main dash.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true)),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.GlanceDoes),
                Use("KeyboardReaderPlugin.F9", "Main dash", PanelShortcuts.GlanceDoes),
            }).Single().Text);
            // And the other way round, a later clause on some of an earlier one's screens.
            Assert.Equal("Keyboard · F9 holds the quick glance on both Rim and Main dash, and cycles zone A on Rim.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.GlanceDoes),
                Use("KeyboardReaderPlugin.F9", "Main dash", PanelShortcuts.GlanceDoes),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true)),
            }).Single().Text);
            // And beside a next page on the same screen, each verb once.
            Assert.Equal("Keyboard · F9 cycles zone A and takes zone B and zone C to their previous pages on Rim.", PanelShortcuts.Clashes(new[]
            {
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone A", true)),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone B", false)),
                Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.ZoneDoes("Zone C", false)),
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
            // A missing use is passed over, and blank triggers are no binding, so two of them never clash.
            Assert.Empty(PanelShortcuts.Clashes(new PanelShortcuts.BindingUse[] { null, Use("KeyboardReaderPlugin.F9", "Rim", PanelShortcuts.GlanceDoes) }));
            Assert.Empty(PanelShortcuts.Clashes(new[] { Use(" ", "Rim", PanelShortcuts.GlanceDoes), Use(" ", "Main dash", PanelShortcuts.GlanceDoes) }));
            Assert.Empty(PanelShortcuts.Clashes(new[] { Use(null, "Rim", PanelShortcuts.GlanceDoes), Use(null, "Main dash", PanelShortcuts.GlanceDoes) }));
            Assert.Empty(PanelShortcuts.Clashes(new[] { Use("", "Rim", PanelShortcuts.GlanceDoes), Use("", "Main dash", PanelShortcuts.GlanceDoes) }));
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
            // Two clash lines, 8 apart, on the caution colour at the artboard's rgba(255,179,0,0.06).
            Assert.Equal(8, PanelShortcuts.BannerStackGap);
            Assert.Equal(0.06, PanelShortcuts.BannerTint);
        }

        /// <summary>The page's own numbers, where the artboard has none: it draws no stacked row, no glance
        /// caption, no stacked header, no floor under SimHub's editor and no binder of SimHub's.</summary>
        [Fact]
        public void Its_own_geometry_where_the_artboard_has_none()
        {
            // The glance's caption 4 under its name, and a stacked binder 8 under the name and the press.
            Assert.Equal(4, PanelShortcuts.CaptionGap);
            Assert.Equal(8, PanelShortcuts.StackGap);
            // The least a name keeps beside the press and the binder before the binder moves under it: the
            // widest label with its tag (A_rows_name_column_holds_its_widest_label_with_its_tag measures it).
            Assert.Equal(184, PanelShortcuts.NameMinWidth);
            // The slot SimHub's editor is given beside the name, which is not the artboard's 260 + 16 + 120:
            // SimHub's template prints a long joystick binding on one line wider than that.
            Assert.Equal(520, PanelShortcuts.BinderWidth);
            // BuildBinder's floor under SimHub's editor, wherever it is drawn.
            Assert.Equal(260, PanelShortcuts.BinderMinWidth);
            // The filter's gap under the caption when the header stacks, which the artboard's never does (its
            // header's own gap, beside the caption, is 24).
            Assert.Equal(12, PanelShortcuts.FilterGapStacked);
        }

        [Fact]
        public void A_row_puts_its_binder_under_its_name_only_where_the_three_cannot_sit_side_by_side()
        {
            // Inside the row's padding: 184 of name, 16 + 90 + 16 + 520 of press and binder.
            Assert.Equal(826, PanelShortcuts.RowFlatFrom);
            Assert.Equal(PanelShortcuts.RowFlatFrom, PanelShortcuts.NameMinWidth + PanelShortcuts.RowGap + PanelShortcuts.PressWidth + PanelShortcuts.RowGap + PanelShortcuts.BinderWidth);
            // And on the page, with 2 of card rules and 32 of padding round it.
            Assert.Equal(860, PanelShortcuts.RowStackBelow);
            Assert.Equal(PanelShortcuts.RowStackBelow, 2 + 2 * PanelShortcuts.RowPaddingX + PanelShortcuts.RowFlatFrom);
            Assert.Equal(PanelShortcuts.RowFlatFrom, PanelShortcuts.RowInside(PanelShortcuts.RowStackBelow));
            Assert.Equal(0, PanelShortcuts.RowInside(20));
            // The artboard's 1200 frame (879 of content) and a 4K column lie flat; one pixel under the
            // threshold stacks, inside the padding as on the page.
            Assert.False(PanelShortcuts.RowStacks(Column(1200)));
            Assert.False(PanelShortcuts.RowStacks(Column(3840)));
            Assert.False(PanelShortcuts.RowStacks(860));
            Assert.True(PanelShortcuts.RowStacks(859));
            Assert.False(PanelShortcuts.RowStacksInside(826));
            Assert.True(PanelShortcuts.RowStacksInside(825));
            // The rail's content at the narrowest full-sidebar width: the binder goes under the name there.
            Assert.True(PanelShortcuts.RowStacks(679));
            Assert.True(PanelShortcuts.RowStacks(400));
        }

        [Fact]
        public void Every_row_gives_simhubs_editor_one_slot_wide_enough_for_a_long_binding()
        {
            // Beside the name the slot is never narrower than BuildBinder's floor, and this page lowers that
            // floor, so under the name, where the slot is the row's whole width, SimHub's template is laid
            // out in the room it has rather than clipped at the slot's edge.
            Assert.True(PanelShortcuts.BinderWidth >= PanelShortcuts.BinderMinWidth);
            var shell = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.cs"));
            Assert.Contains("MinWidth = " + PanelShortcuts.BinderMinWidth.ToString(System.Globalization.CultureInfo.InvariantCulture) + " }", shell);
            var code = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Shortcuts.cs"));
            Assert.Matches(@"control\.VerticalAlignment = VerticalAlignment\.Center;\s*control\.MinWidth = 0;\s*slot = new Border", code);

            // SimHub 9.12.6 prints a binding on one line at 10 px: a 58 px press type, then the plugin 4 after
            // it and the input, each a Label padded 5 a side. "JoystickPlugin" and
            // "Logitech_G923_Racing_Wheel_for_PlayStation_4_and_PC_B05" are 63.4 and 288.0 px of Arial
            // (Liberation Sans' advances, which have Arial's metrics), and the slot's two rules and the
            // template's margins take 10 more: the line's last word, the button's number, is drawn whole only
            // in a slot of 443 px or more, and Segoe UI, which SimHub draws in, measures wider than Arial.
            const double pressType = 58, pluginGap = 4, labelPadding = 2 * 5, plugin = 63.4, input = 288.0, frame = 10;
            var longBinding = pressType + pluginGap + labelPadding + plugin + labelPadding + input + frame;
            Assert.True(PanelShortcuts.BinderWidth >= longBinding + 0.15 * (plugin + input), "the slot holds a long joystick binding with room for Segoe UI");

            // Beside the name, one slot of that width in every row, wherever the row lies flat, so press and
            // binder line up down a card; the name column takes the rest.
            foreach (var content in new[] { PanelShortcuts.RowStackBelow, Column(1200), 1500, Column(3840) })
            {
                var inside = PanelShortcuts.RowInside(content);
                Assert.Equal(PanelShortcuts.BinderWidth, PanelShortcuts.BinderSlot(inside, false));
                Assert.Equal(inside - PanelShortcuts.RowGap - PanelShortcuts.PressWidth - PanelShortcuts.RowGap - PanelShortcuts.BinderWidth, PanelShortcuts.NameColumn(inside, false));
                Assert.True(PanelShortcuts.NameColumn(inside, false) >= PanelShortcuts.NameMinWidth, content.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            // At the artboard's frame the name has 203, and the threshold gives it its floor exactly.
            Assert.Equal(203, PanelShortcuts.NameColumn(PanelShortcuts.RowInside(Column(1200)), false));
            Assert.Equal(PanelShortcuts.NameMinWidth, PanelShortcuts.NameColumn(PanelShortcuts.RowInside(PanelShortcuts.RowStackBelow), false));
            // Under the name the slot is the whole row inside its padding, wider than the flat slot just under
            // the threshold, so a row never has less room for its bindings stacked than flat; and the name
            // has the row past the press.
            Assert.Equal(PanelShortcuts.RowFlatFrom - 1, PanelShortcuts.BinderSlot(PanelShortcuts.RowFlatFrom - 1, true));
            Assert.True(PanelShortcuts.BinderSlot(PanelShortcuts.RowFlatFrom - 1, true) > PanelShortcuts.BinderWidth);
            Assert.Equal(566, PanelShortcuts.BinderSlot(566, true));
            Assert.Equal(0, PanelShortcuts.BinderSlot(-4, true));
            Assert.Equal(566 - 16 - 90, PanelShortcuts.NameColumn(566, true));
            Assert.Equal(0, PanelShortcuts.NameColumn(50, true));
            Assert.Equal(0, PanelShortcuts.NameColumn(300, false));
            // SimHub's editor draws no name of its own: the row's beside it says what it binds.
            Assert.Empty(PanelShortcuts.EditorName);
        }

        /// <summary>
        /// The name column's floor holds what a flat row puts on its name's line: the widest label any card
        /// draws, with the New or Soon tag 8 after it, measured in the faces the panel draws them in (Barlow
        /// Regular at 14 for a name, Barlow SemiBold at 10 tracked 0.12 for a tag) from the font files the
        /// plugin embeds. Narrower, the WrapPanel drops the tag onto a second line at the width a row first
        /// lies flat. Kerning is left out, as a tag's letters are drawn one by one (Ui.Tracked).
        /// </summary>
        [Fact]
        public void A_rows_name_column_holds_its_widest_label_with_its_tag()
        {
            var fonts = Path.Combine(RepoPaths.Root(), "packages", "dash", "fonts");
            var regular = new Advances(Path.Combine(fonts, "Barlow-Regular.ttf"));
            var semiBold = new Advances(Path.Combine(fonts, "Barlow-SemiBold.ttf"));
            Func<string, double> tag = text => 2 * PanelMetrics.BorderWeight + PanelShell.TagPaddingX + (PanelShell.TagPaddingX - 1)
                + semiBold.Width(text, PanelShell.TagTextSize, Math.Round(PanelShell.TagTextSize * 0.12, 2));
            var rows = PanelShortcuts.FaceBindings(Face, "Rim").Concat(PanelShortcuts.LightsBindings())
                .Concat(new[] { PanelShortcuts.GlanceBinding(Contract.KindCompanion, Contract.CompanionPrefix, "Phone") })
                .Select(row => regular.Width(row.Label, PanelShortcuts.RowNameSize) + (row.IsNew ? PanelShortcuts.TagGap + tag(PanelSoon.NewTag) : 0))
                .Concat(PanelShortcuts.SoonDrawn.Select(item => regular.Width(item.Title, PanelShortcuts.RowNameSize) + PanelShortcuts.TagGap + tag(PanelSoon.Tag)))
                .ToList();
            var widest = rows.Max();
            // "Band D · previous page" and its New tag, 180.4.
            Assert.InRange(widest, 178, 183);
            Assert.True(PanelShortcuts.NameMinWidth >= widest, "the name's floor is " + PanelShortcuts.NameMinWidth + ", its widest line " + widest.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            // So at the artboard's frame and where a row first lies flat, every name line fits beside the press
            // and the binder.
            Assert.True(PanelShortcuts.NameColumn(PanelShortcuts.RowInside(Column(1200)), false) >= widest);
            Assert.True(PanelShortcuts.NameColumn(PanelShortcuts.RowInside(PanelShortcuts.RowStackBelow), false) >= widest);
        }

        /// <summary>Advances from a TrueType file's hmtx, through its Windows Unicode cmap: enough to measure a
        /// line of Latin text as WPF lays it, less kerning.</summary>
        private sealed class Advances
        {
            private readonly byte[] font;
            private readonly int unitsPerEm;
            private readonly int metrics;
            private readonly int hmtx;
            private readonly int cmap;

            public Advances(string path)
            {
                font = File.ReadAllBytes(path);
                var tables = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var i = 0; i < U16(4); i++)
                {
                    var record = 12 + 16 * i;
                    tables[System.Text.Encoding.ASCII.GetString(font, record, 4)] = (int)U32(record + 8);
                }
                unitsPerEm = U16(tables["head"] + 18);
                metrics = U16(tables["hhea"] + 34);
                hmtx = tables["hmtx"];
                var map = tables["cmap"];
                for (var i = 0; i < U16(map + 2); i++)
                {
                    var record = map + 4 + 8 * i;
                    if (U16(record) == 3 && U16(record + 2) == 1) cmap = map + (int)U32(record + 4);
                }
                Assert.True(cmap > 0 && U16(cmap) == 4, path + " has a Windows Unicode cmap of format 4");
            }

            public double Width(string text, double size, double tracking = 0)
            {
                return text.Sum(c => (double)U16(hmtx + 4 * Math.Min(Glyph(c), metrics - 1)) / unitsPerEm * size + tracking);
            }

            private int Glyph(char c)
            {
                var segments = U16(cmap + 6) / 2;
                var ends = cmap + 14;
                var starts = ends + 2 * segments + 2;
                var deltas = starts + 2 * segments;
                var ranges = deltas + 2 * segments;
                for (var i = 0; i < segments; i++)
                {
                    if (c > U16(ends + 2 * i)) continue;
                    if (c < U16(starts + 2 * i)) return 0;
                    var range = U16(ranges + 2 * i);
                    if (range == 0) return (c + U16(deltas + 2 * i)) & 0xFFFF;
                    var glyph = U16(ranges + 2 * i + range + 2 * (c - U16(starts + 2 * i)));
                    return glyph == 0 ? 0 : (glyph + U16(deltas + 2 * i)) & 0xFFFF;
                }
                return 0;
            }

            private int U16(int at) => (font[at] << 8) | font[at + 1];

            private uint U32(int at) => ((uint)U16(at) << 16) | (uint)U16(at + 2);
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
            // Only SimHub's editor is told its name and has its column dropped: BuildBinder's fallback text,
            // when the editor could not be made, is a TextBlock, which wraps in the slot instead.
            var binding = Between(code, "private static void ShortcutsBinding(", "var tags =");
            Assert.Matches(@"var control = editor as ControlsEditor;\s*if \(control != null\)\s*\{\s*control\.FriendlyName = PanelShortcuts\.EditorName;\s*control\.Loaded \+= \(sender, args\) => ShortcutsDropNameColumn\(control\);\s*\}", binding);
            Assert.Matches(@"var fallback = editor as TextBlock;\s*if \(fallback != null\) fallback\.TextWrapping = TextWrapping\.Wrap;", binding);
            var drop = Between(code, "private static void ShortcutsDropNameColumn(", "private static FrameworkElement ShortcutsMenuOwner(");
            // It runs from a Loaded handler, so a surprise from SimHub's template is caught and logged here
            // rather than thrown onto SimHub's UI thread: the whole body is the try, and its catch is its own.
            Assert.Matches(@"^private static void ShortcutsDropNameColumn\(ControlsEditor editor\)\s*\{\s*try\s*\{\s*editor\.ApplyTemplate\(\);", drop);
            Assert.Matches(@"\}\s*catch \(Exception ex\)\s*\{\s*Log\.Warn\([^;]*\);\s*\}\s*\}\s*$", drop);
            Assert.Contains("editor.Template.FindName(\"brd\", editor) as Border;", drop);
            Assert.Contains("if (grid == null || grid.ColumnDefinitions.Count != 2) return;", drop);
            Assert.Contains("if (!grid.ColumnDefinitions[0].Width.IsStar || !grid.ColumnDefinitions[1].Width.IsStar) return;", drop);
            Assert.Contains("foreach (var child in named) child.Visibility = Visibility.Collapsed;", drop);
            Assert.Contains("grid.ColumnDefinitions[0].Width = new GridLength(0);", drop);
            // Only a name column holding text: a template of another shape keeps its bindings where they are.
            Assert.Contains("if (named.Any(child => !(child is TextBlock) && !(child is Label))) return;", drop);
            Assert.Contains("if (named.Any(child => Grid.GetColumnSpan(child) != 1)) return;", drop);
            // SimHub's "Trigger now" rides on the name; it moves to the editor's border before the name goes.
            Assert.Contains("var menuOwner = named.Select(ShortcutsMenuOwner).FirstOrDefault(owner => owner != null);", drop);
            Assert.Matches(@"if \(menuOwner != null && border\.ContextMenu == null\)\s*\{\s*var menu = menuOwner\.ContextMenu;\s*menuOwner\.ContextMenu = null;\s*border\.ContextMenu = menu;", drop);
            TextOrder.Before(drop, "border.ContextMenu = menu;", "child.Visibility = Visibility.Collapsed;", "the menu moves before the name is collapsed");
            var owner = Between(code, "private static FrameworkElement ShortcutsMenuOwner(", "\n        }");
            Assert.Contains("if (element != null && element.ContextMenu != null) return element;", owner);
            Assert.Contains("var content = label == null ? null : label.Content as FrameworkElement;", owner);
            Assert.Contains("return content != null && content.ContextMenu != null ? content : null;", owner);
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
            // Each lands on its own card: a screen's rows on the first screen's, the lights' on Lights, the
            // alerts' on Alerts.
            Assert.Equal(new[]
            {
                PanelShortcuts.AnchorScreens, PanelShortcuts.AnchorScreens, PanelShortcuts.AnchorScreens,
                PanelShortcuts.AnchorRig, PanelShortcuts.AnchorRig, PanelShortcuts.AnchorRig, PanelShortcuts.AnchorRig,
                PanelShortcuts.AnchorAlerts,
            }, PanelShortcuts.Search.Select(entry => entry.Route.Anchor));
            // And by the words a driver would type for it.
            Assert.Equal(new[]
            {
                "wheel buttons|bind|button|key|zone",
                "back|zone|bind",
                "hold|bind|button",
                "night|brightness|strip|matrix|bind",
                "night mode button|bind|toggle",
                "brightness buttons|brighter|bind",
                "brightness buttons|dimmer|bind",
                "dismiss|bind",
            }, PanelShortcuts.Search.Select(entry => string.Join("|", entry.Keywords)));
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
            // The page's watch is the shell's WatchBindings, which HoldWhilePressed stands on too, let go of
            // when the build is dropped as well as when the editor leaves the tree.
            Assert.Matches(@"private void ShortcutsWatch\(ControlsEditor editor, Action changed\)\s*\{\s*OnDrop\(WatchBindings\(editor, null, changed\)\);\s*\}", code.Substring(start));
            var bindings = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Bindings.cs"));
            var body = bindings.Substring(bindings.IndexOf("private static Action WatchBindings(", StringComparison.Ordinal));
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

            // Let go of when the editor leaves the tree; the caller lets go of it when the build is replaced.
            Assert.Contains("editor.Unloaded += (sender, args) => detach();", body);

            // What each step acts on, not only its += and -=: every mapping watched is remembered, so rewatch
            // and detach can let go of it; the collection is swapped before it is watched, and the mappings
            // in it are watched every time; the model is remembered between its -= and +=; and the watch is
            // taken up at once, not only on Loaded.
            Assert.Matches(@"Action rewatchMappings = \(\) =>\s*\{\s*foreach \(var mapping in watchedMappings\) mapping\.PropertyChanged -= mappingChanged;\s*watchedMappings\.Clear\(\);\s*if \(watchedTriggers == null\) return;\s*foreach \(var mapping in watchedTriggers\)\s*\{\s*if \(mapping == null\) continue;\s*mapping\.PropertyChanged \+= mappingChanged;\s*watchedMappings\.Add\(mapping\);\s*each\(mapping\);\s*\}\s*\};", body);
            Assert.Matches(@"Action rewatchTriggers = \(\) =>\s*\{\s*var triggers = watched == null \? null : watched\.Triggers;\s*if \(!ReferenceEquals\(triggers, watchedTriggers\)\)\s*\{\s*if \(watchedTriggers != null\) watchedTriggers\.CollectionChanged -= collectionChanged;\s*watchedTriggers = triggers;\s*if \(watchedTriggers != null\) watchedTriggers\.CollectionChanged \+= collectionChanged;\s*\}\s*rewatchMappings\(\);\s*\};", body);
            Assert.Matches(@"Action attach = \(\) =>\s*\{\s*var model = editor\.Model;\s*if \(ReferenceEquals\(model, watched\)\) return;\s*if \(watched != null\) watched\.PropertyChanged -= modelChanged;\s*watched = model;\s*if \(watched != null\) watched\.PropertyChanged \+= modelChanged;\s*rewatchTriggers\(\);\s*told\(\);\s*\};", body);
            Assert.Matches(@"attach\(\);\s*return detach;\s*\}", body);
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
            var bindings = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Bindings.cs"));
            var body = bindings.Substring(bindings.IndexOf("private static Action WatchBindings(", StringComparison.Ordinal));
            // A notification that names no property (all of them changed) reads again too.
            Assert.Matches(@"if \(args\.PropertyName != null && args\.PropertyName != ""Trigger"" && args\.PropertyName != ""PressType""\) return;\s*each\(sender as InputMapping\);\s*told\(\);", Between(body, "mappingChanged = (sender, args) =>", "};"));
            Assert.Contains("args.PropertyName != \"Triggers\"", Between(body, "modelChanged = (sender, args) =>", "};"));
            // Each re-reads the page. A binding changed in place (SimHub's Change sets Trigger and PressType on
            // the mapping already in the list) raises neither a collection change nor InputMappingsChanged,
            // so the mapping's own watch is the only way the clash line hears of it; an Add or a Clear
            // raises the collection's; a model replaced or taken up again reads at once.
            Assert.Matches(@"Action told = \(\) =>\s*\{\s*if \(changed != null\) changed\(\);\s*\};", body);
            Assert.Matches(@"collectionChanged = \(sender, args\) =>\s*\{\s*rewatchMappings\(\);\s*told\(\);\s*\};", body);
            Assert.Matches(@"modelChanged = \(sender, args\) =>\s*\{\s*if \(args\.PropertyName != null && args\.PropertyName != ""Triggers""\) return;\s*rewatchTriggers\(\);\s*told\(\);\s*\};", body);
            Assert.Matches(@"Action attach = \(\) =>\s*\{[^}]*rewatchTriggers\(\);\s*told\(\);\s*\};", body);
            Assert.Contains("if (args.PropertyName == null || args.PropertyName == \"Model\") attach();", body);
            Assert.Contains("editor.Loaded += (sender, args) => attach();", body);
            var page = Between(code, "private FrameworkElement BuildShortcutsPage(", "var page = TaggedPageLayout(");
            Assert.Matches(@"foreach \(var row in groups\.SelectMany\(group => group\.Rows\)\)\s*\{\s*var editor = row\.Editor as ControlsEditor;\s*if \(editor != null\) ShortcutsWatch\(editor, changed\);\s*if \(editor != null\) editors\.Add\(editor\);", page);

            // The step every re-read goes through: a burst of changes is read once, after it, by the page's
            // evaluation, and never once the build is dropped.
            var changed = Between(code, "private FrameworkElement BuildShortcutsPage(", "var editors =");
            Assert.Matches(@"var pending = false;\s*var dropped = false;\s*OnDrop\(\(\) => dropped = true\);", changed);
            Assert.Matches(@"Action changed = \(\) =>\s*\{\s*if \(pending \|\| dropped\) return;\s*pending = true;\s*Dispatcher\.BeginInvoke\(DispatcherPriority\.Background, new Action\(\(\) =>\s*\{\s*pending = false;\s*if \(!dropped\) evaluate\(\);\s*\}\)\);\s*\};", changed);
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
            // A burst of reports is reconciled once, after it, and never once the build is dropped.
            Assert.Matches(@"mappingsChanged = \(sender, args\) =>\s*\{\s*if \(pending \|\| dropped\(\)\) return;\s*pending = true;\s*Dispatcher\.BeginInvoke\(DispatcherPriority\.Background, new Action\(\(\) =>\s*\{\s*pending = false;\s*if \(dropped\(\)\) return;\s*foreach \(var editor in editors\) ShortcutsReconcile\(editor\);\s*changed\(\);", follow);
            Assert.Contains("Action release = () => PluginManager.InputMappingsChanged -= mappingsChanged;", follow);
            Assert.Matches(@"page\.Loaded \+= \(sender, args\) =>\s*\{\s*release\(\);\s*if \(!dropped\(\)\) PluginManager\.InputMappingsChanged \+= mappingsChanged;", follow);
            Assert.Contains("page.Unloaded += (sender, args) => release();", follow);
            Assert.Contains("OnDrop(release);", follow);
            Assert.Single(Regex.Matches(code, @"InputMappingsChanged \+="));

            var reconcile = Between(code, "private static void ShortcutsReconcile(", "private FrameworkElement BuildShortcutsHeader(");
            // It runs from a Dispatcher callback, so a surprise from SimHub's model is caught and logged here
            // rather than thrown onto SimHub's UI thread: the whole body is the try, and its catch is its own.
            Assert.Matches(@"^private static void ShortcutsReconcile\(ControlsEditor editor\)\s*\{\s*try\s*\{\s*var model = editor\.Model;", reconcile);
            Assert.Matches(@"\}\s*catch \(Exception ex\)\s*\{\s*Log\.Warn\([^;]*\);\s*\}\s*\}\s*$", reconcile);
            // An editor with no model, no list or no action is left alone, and so is a list SimHub cannot give.
            Assert.Contains("if (model == null || model.Triggers == null || string.IsNullOrEmpty(editor.ActionName)) return;", reconcile);
            Assert.Contains("if (PluginManager.GetInstance() == null) return;", reconcile);
            Assert.Contains("if (simhub == null) return;", reconcile);
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
            // The card's name is the screen's, beside its kind and size; the glance is the screen's own, by
            // its kind and its namespace, which is what ScreenInstance.ActionNames registers and the sidebar's
            // count reads.
            Assert.Contains("var group = ShortcutsCard(screen.Name, PanelShortcuts.GroupDetail(screen.Name, screen.Kind, screen.Width, screen.Height), null);", face);
            Assert.Contains("var glance = PanelShortcuts.GlanceBinding(screen.Kind, screen.Namespace, screen.Name);", face);
            Assert.Contains("foreach (var binding in PanelShortcuts.FaceBindings(screen.Namespace, screen.Name))", face);
            Assert.Contains("ShortcutsBinding(group, screen.Name, binding, BuildBinder(binding.Action, binding.BinderName), null);", face);
            Assert.Contains("ShortcutsBinding(group, screen.Name, glance, BuildBinder(glance.Action, glance.BinderName, hold: true), Ui.Caption(PanelCopy.FaceGlance));", face);
            var wall = Between(code, "private ShortcutsGroupState BuildShortcutsPitWall(", "return group;");
            Assert.Contains("var group = ShortcutsCard(screen.Name, PanelShortcuts.GroupDetail(screen.Name, screen.Kind, screen.Width, screen.Height), null);", wall);
            Assert.Contains("var glance = PanelShortcuts.GlanceBinding(screen.Kind, screen.Namespace, screen.Name);", wall);
            Assert.Contains("var caption = PanelShortcuts.PitWallGlances(screen.Width, screen.Height) ? Ui.Caption(PanelCopy.PitWallGlance) : Ui.Caption(PanelShortcuts.PortraitGlanceCaption);", wall);
            Assert.Contains("ShortcutsBinding(group, screen.Name, glance, BuildBinder(glance.Action, glance.BinderName, hold: true), caption);", wall);
            var bound = Between(code, "private bool ShortcutsGlanceBound(", "\n        }");
            Assert.Contains("var triggers = TriggersOf(PanelShortcuts.GlanceBinding(screen.Kind, screen.Namespace, screen.Name).Action);", bound);
            Assert.Contains("return triggers == null || triggers.Count > 0;", bound);
            var companion = Between(code, "private ShortcutsGroupState BuildShortcutsCompanion(", "return group;");
            Assert.Contains("var glance = PanelShortcuts.GlanceBinding(screen.Kind, screen.Namespace, screen.Name);", companion);
            Assert.Contains("ShortcutsBinding(group, screen.Name, glance, BuildBinder(glance.Action, glance.BinderName, hold: true), Ui.Caption(PanelCopy.CompanionGlance));", companion);
            var lights = Between(code, "private ShortcutsGroupState BuildShortcutsLights(", "return group;");
            Assert.Contains("foreach (var binding in PanelShortcuts.LightsBindings())", lights);
            Assert.Contains("ShortcutsBinding(group, null, binding, BuildBinder(binding.Action, binding.BinderName), null);", lights);

            var page = Between(code, "private FrameworkElement BuildShortcutsPage(", "var lights = BuildShortcutsLights();");
            // Over every screen the rig has, the same list the sidebar's count reads (BoundCount), so the cards
            // add up to it.
            Assert.Matches(@"foreach \(var screen in Settings\.RigScreens\(\)\)\s*\{\s*if \(screen == null\) continue;\s*if \(screen\.IsFace\) screens\.Add\(BuildShortcutsFace\(screen\)\);\s*else if \(screen\.IsPitWall && PanelShortcuts\.PitWallCard\(screen\.Width, screen\.Height, ShortcutsGlanceBound\(screen\)\)\) screens\.Add\(BuildShortcutsPitWall\(screen\)\);\s*else if \(screen\.IsCompanion\) screens\.Add\(BuildShortcutsCompanion\(screen\)\);", page);
            Assert.Equal(3, Regex.Matches(page, @"screens\.Add\(").Count);
        }

        /// <summary>
        /// Every part the page builds is put where it is drawn: each greyed row on its own card, the
        /// companion's paging line on the companion's card, the header and the clash lines above the cards
        /// and the empty line under them, each row on its card and each part of a row in it, and the build's
        /// drop that stops a stale read.
        /// </summary>
        [Fact]
        public void The_page_puts_every_part_where_it_is_drawn()
        {
            var code = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Shortcuts.cs"));
            Assert.Contains("ShortcutsSoon(group, PanelSoon.RigTest);", Between(code, "private ShortcutsGroupState BuildShortcutsLights(", "return group;"));
            Assert.Contains("ShortcutsSoon(group, PanelSoon.AlertDismissal);", Between(code, "private ShortcutsGroupState BuildShortcutsAlerts(", "return group;"));
            // Two greyed rows, each drawn once and only there.
            Assert.Equal(2, Regex.Matches(code, @"ShortcutsSoon\(group, PanelSoon\.").Count);
            var companion = Between(code, "private ShortcutsGroupState BuildShortcutsCompanion(", "return group;");
            Assert.Contains("Child = Ui.VStack(0, Ui.Caption(PanelCopy.CompanionPaging, BodyWidth), crumbs),", companion);
            Assert.Contains("var group = ShortcutsCard(screen.Name, PanelShortcuts.GroupDetail(screen.Name, screen.Kind, screen.Width, screen.Height), lead);", companion);

            // The page's order: the header, the clash lines, the cards, the empty line.
            var sections = Regex.Match(code, @"var sections = new List<UIElement> \{ header, banner \};\s*sections\.AddRange\(groups\.Select\(group => \(UIElement\)group\.Card\)\);\s*sections\.Add\(empty\);");
            Assert.True(sections.Success, "the page is its header, the clash lines, the cards and the empty line, in that order");
            Assert.Contains("OnDrop(() => dropped = true);", code);

            // A card: its header's name, line and count, the lead under the header, and its rows.
            var card = Between(code, "private static ShortcutsGroupState ShortcutsCard(", "return new ShortcutsGroupState");
            Assert.Contains("titleLine.Children.Add(name);", card);
            Assert.Contains("titleLine.Children.Add(line);", card);
            Assert.Contains("head.Children.Add(titleLine);", card);
            Assert.Contains("head.Children.Add(count);", card);
            Assert.Contains("Grid.SetColumn(count, 1);", card);
            Assert.Contains("if (lead != null) body.Children.Add(lead);", card);
            // The card's header, lead and rows go down it, one under another, as do the clash lines.
            Assert.Contains("var body = new StackPanel { Orientation = Orientation.Vertical };", card);
            Assert.Contains("var banner = new StackPanel { Orientation = Orientation.Vertical };", code);
            Assert.Contains("return new ShortcutsGroupState { Card = Ui.CardBox(body, 0), Body = body, CountHost = count, HasLead = lead != null };", code);
            Assert.Contains("group.Body.Children.Add(row);", Between(code, "private static void ShortcutsBinding(", "\n        }"));
            Assert.Contains("group.Body.Children.Add(shown);", Between(code, "private static void ShortcutsSoon(", "\n        }"));

            // A row: the name and its tags on one line, the caption, the press, and the binder's slot, each
            // handed to the row's panel, which lays them out; the Tag Ui.Soon reads to put its Soon tag after a
            // greyed row's name.
            var row = Between(code, "private static Border ShortcutsRow(", "private sealed class ShortcutsRowPanel");
            Assert.Contains("nameLine.Children.Add(name);", row);
            Assert.Contains("nameLine.Children.Add(tag);", row);
            Assert.Contains("var nameLine = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };", row);
            Assert.Matches(@"return new Border\s*\{\s*BorderBrush = Ui\.Brush\(Theme\.Rule\),\s*BorderThickness = new Thickness\(0, PanelMetrics\.BorderWeight, 0, 0\),\s*Padding = new Thickness\(PanelShortcuts\.RowPaddingX, PanelShortcuts\.RowPaddingY, PanelShortcuts\.RowPaddingX, PanelShortcuts\.RowPaddingY\),\s*Child = new ShortcutsRowPanel\(nameLine, caption, pressText, slot\),\s*Tag = new RowParts\(nameLine, control\),\s*\};", row);
            // A caption, where a row has one, under the name's line at its gap and a row caption's measure;
            // a row with none (every zone, Lights and greyed row) is drawn without.
            Assert.Matches(@"if \(caption != null\)\s*\{\s*caption\.Margin = new Thickness\(0, PanelShortcuts\.CaptionGap, 0, 0\);\s*caption\.MaxWidth = Math\.Min\(caption\.MaxWidth, PanelShell\.RowCaptionMaxWidth\);\s*\}", row);
            // A long name wraps; the press is the artboard's .cap, 16 after the name; the slot holds the
            // control, keeps the panel's corner and the clash outline's room, and is centred on the line.
            Assert.Matches(@"var name = Ui\.Text\(label, [^;]*\);\s*name\.TextWrapping = TextWrapping\.Wrap;\s*name\.VerticalAlignment = VerticalAlignment\.Center;", row);
            Assert.Matches(@"tag\.Margin = new Thickness\(PanelShortcuts\.TagGap, 0, 0, 0\);\s*tag\.VerticalAlignment = VerticalAlignment\.Center;", row);
            Assert.Matches(@"pressText = Ui\.Text\(press, Theme\.SizeSmall, FontWeights\.Normal, Theme\.TextSecondary\);\s*pressText\.VerticalAlignment = VerticalAlignment\.Center;\s*pressText\.Margin = new Thickness\(PanelShortcuts\.RowGap, 0, 0, 0\);", row);
            Assert.Matches(@"if \(control != null\)\s*\{\s*control\.Margin = new Thickness\(0\);\s*control\.VerticalAlignment = VerticalAlignment\.Center;\s*control\.MinWidth = 0;\s*slot = new Border\s*\{\s*Child = control,\s*HorizontalAlignment = HorizontalAlignment\.Stretch,\s*VerticalAlignment = VerticalAlignment\.Center,\s*BorderThickness = new Thickness\(PanelMetrics\.BorderWeight\),\s*CornerRadius = new CornerRadius\(Theme\.Radius\),\s*\};\s*\}", row);
            // The slot's width is the panel's, never fixed on it.
            Assert.DoesNotContain("Width = ", Between(code.Replace("\r\n", "\n"), "slot = new Border\n", "};"));

            // The row's panel: its parts in reading order, which is Tab's; flat or stacked from the width it is
            // given, never from the build; the name column, the press column and the slot from the model.
            var panel = Between(code, "private sealed class ShortcutsRowPanel", "private static void ShortcutsDropNameColumn(");
            Assert.Contains(": Panel", panel);
            Assert.Matches(@"foreach \(var part in new\[\] \{ name, caption, press, slot \}\)\s*\{\s*if \(part != null\) Children\.Add\(part\);\s*\}", panel);
            var measure = Between(panel, "protected override Size MeasureOverride(", "protected override Size ArrangeOverride(");
            Assert.Matches(@"var inside = double\.IsInfinity\(available\.Width\) \? PanelShortcuts\.RowFlatFrom : available\.Width;\s*stacks = PanelShortcuts\.RowStacksInside\(inside\);", measure);
            Assert.Contains("name.Measure(new Size(PanelShortcuts.NameColumn(inside, stacks), open));", measure);
            Assert.Contains("press.Measure(new Size(PanelShortcuts.RowGap + PanelShortcuts.PressWidth, open));", measure);
            Assert.Contains("line = Math.Max(name.DesiredSize.Height, press.DesiredSize.Height);", measure);
            // Stacked, the slot is under the line, the stack's gap above it; flat, it is on the line.
            Assert.Matches(@"if \(slot != null\)\s*\{\s*slot\.Measure\(new Size\(PanelShortcuts\.BinderSlot\(inside, stacks\), open\)\);\s*if \(stacks\) under \+= PanelShortcuts\.StackGap \+ slot\.DesiredSize\.Height;\s*else line = Math\.Max\(line, slot\.DesiredSize\.Height\);\s*\}", measure);
            // The caption across the row, at its own measure, under the line.
            Assert.Matches(@"if \(caption != null\)\s*\{\s*caption\.Measure\(new Size\(inside, open\)\);\s*under \+= caption\.DesiredSize\.Height;\s*\}\s*return new Size\(inside, line \+ under\);", measure);
            var arrange = Between(panel, "protected override Size ArrangeOverride(", "\n        }\n");
            Assert.Matches(@"var nameWidth = PanelShortcuts\.NameColumn\(inside, stacks\);\s*name\.Arrange\(new Rect\(0, 0, nameWidth, line\)\);\s*press\.Arrange\(new Rect\(nameWidth, 0, PanelShortcuts\.RowGap \+ PanelShortcuts\.PressWidth, line\)\);", arrange);
            Assert.Matches(@"var y = line;\s*if \(caption != null\)\s*\{\s*caption\.Arrange\(new Rect\(0, y, inside, caption\.DesiredSize\.Height\)\);\s*y \+= caption\.DesiredSize\.Height;\s*\}", arrange);
            Assert.Matches(@"if \(stacks\) slot\.Arrange\(new Rect\(0, y \+ PanelShortcuts\.StackGap, inside, slot\.DesiredSize\.Height\)\);\s*else slot\.Arrange\(new Rect\(nameWidth \+ PanelShortcuts\.RowGap \+ PanelShortcuts\.PressWidth \+ PanelShortcuts\.RowGap, 0, PanelShortcuts\.BinderSlot\(inside, false\), line\)\);", arrange);
            // A row is ruled above in the panel's rule colour, as the card's header is ruled under.
            Assert.Equal(2, Regex.Matches(code, Regex.Escape("BorderBrush = Ui.Brush(Theme.Rule),")).Count);
            Assert.Contains("CornerRadius = new CornerRadius(Theme.Radius),", Between(code.Replace("\r\n", "\n"), "slot = new Border\n", "};"));
            // The card: the header's rule under it, which the first row's missing rule relies on
            // (RuleAbove(true, false)); the count 16 from the name; the line beside the name in .cap.
            Assert.Contains("BorderThickness = new Thickness(0, 0, 0, PanelMetrics.BorderWeight),", card);
            Assert.Contains("var count = new Border { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(PanelShortcuts.RowGap, 0, 0, 0) };", card);
            Assert.Contains("var line = Ui.Text(detail, Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);", card);
            // The kind and size line only where there is one, 10 after the name, on the name's line, centred.
            Assert.Matches(@"if \(!string\.IsNullOrEmpty\(detail\)\)\s*\{\s*name\.Margin = new Thickness\(0, 0, PanelShortcuts\.GroupDetailGap, 0\);\s*var line = Ui\.Text\(detail, Theme\.SizeSmall, FontWeights\.Normal, Theme\.TextSecondary\);\s*line\.VerticalAlignment = VerticalAlignment\.Center;\s*titleLine\.Children\.Add\(line\);\s*\}", card);
            Assert.Matches(@"name\.TextWrapping = TextWrapping\.Wrap;\s*name\.VerticalAlignment = VerticalAlignment\.Center;\s*var titleLine = new WrapPanel \{ Orientation = Orientation\.Horizontal, VerticalAlignment = VerticalAlignment\.Center \};", card);
            // The header's name takes the room the count leaves, star then Auto, so a long name is measured at
            // the header's width and wraps there rather than pushing the count off the card.
            Assert.Matches(@"var head = new Grid\(\);\s*head\.ColumnDefinitions\.Add\(new ColumnDefinition \{ Width = new GridLength\(1, GridUnitType\.Star\) \}\);\s*head\.ColumnDefinitions\.Add\(new ColumnDefinition \{ Width = GridLength\.Auto \}\);", card);
            // The header is the bordered child the card's body starts with.
            Assert.Matches(@"body\.Children\.Add\(new Border\s*\{\s*BorderBrush = Ui\.Brush\(Theme\.Rule\),\s*BorderThickness = new Thickness\(0, 0, 0, PanelMetrics\.BorderWeight\),\s*Padding = [^\n]*\s*Child = head,\s*\}\);\s*if \(lead != null\) body\.Children\.Add\(lead\);", card);
            // A greyed row's key at the slot's left, where SimHub's bindings start.
            Assert.Contains("chip.HorizontalAlignment = HorizontalAlignment.Left;", Between(code, "private static void ShortcutsSoon(", "\n        }"));

            // The header: the caption and the filter, the filter in the second column when two fit.
            var header = Between(code, "private FrameworkElement BuildShortcutsHeader(", "return header;");
            Assert.Contains("grid.Children.Add(caption);", header);
            // The filter at the header's right in two columns, and at its left under the caption when the
            // header stacks (Segmented's constructor sets Right); in either, the header holds the filter itself,
            // which carries its own name since the kit's bar has a peer (#523).
            Assert.Contains("filter.HorizontalAlignment = HorizontalAlignment.Right;", Between(header, "if (TwoColumns)", "else"));
            Assert.Matches(@"else\s*\{\s*filter\.HorizontalAlignment = HorizontalAlignment\.Left;\s*filter\.Margin = new Thickness\(0, PanelShortcuts\.FilterGapStacked, 0, 0\);\s*header = Ui\.VStack\(0, caption, filter\);\s*\}", header);
            Assert.Contains("Grid.SetColumn(filter, 1);", header);
            Assert.Contains("grid.Children.Add(filter);", header);
            Assert.DoesNotContain("UserControl", header);
            // The caption and the filter share a foot, which FilterRaise's 30 less 19 assumes.
            Assert.Contains("caption.VerticalAlignment = VerticalAlignment.Bottom;", Between(header, "if (TwoColumns)", "else"));
            Assert.Contains("filter.VerticalAlignment = VerticalAlignment.Bottom;", Between(header, "if (TwoColumns)", "else"));
            Assert.Contains("var header = BuildShortcutsHeader(filter);", code);
            // The empty line keeps a message line's measure.
            Assert.Contains("var empty = Ui.Caption(string.Empty, BodyWidth);", code);
            // The page opens on All, as the artboard presses it.
            Assert.Contains("private string shortcutsFilter = PanelShortcuts.FilterAll;", code);
            // The shell's layout draws the title with its tag (#523), so the page no longer takes its title out
            // of PageLayout's stack to put it in a row with the tag.
            Assert.DoesNotContain("ShortcutsTitleTagged", code);
            Assert.DoesNotContain("stack.Children.RemoveAt(0);", code);
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
            // The record is cleared before the focus moves on: the row that takes it sets the record through its
            // own GotKeyboardFocus, synchronously, and clearing after would wipe that, so a second bind by
            // keyboard under Not bound would find no row to hand on from.
            Assert.Matches(@"if \(focused != null && focused\.Shown\.Visibility != Visibility\.Visible\)\s*\{\s*touch\.Row = null;\s*ShortcutsKeepFocus\(groups, focused, filter\);\s*\}", evaluate);
            Assert.Single(Regex.Matches(code, @"touch\.Row = null;"));
            // SimHub takes the focus off the row before this runs (its picker's Closed focuses the main window,
            // and a focused Clear leaves the tree), so the row last pressed or focused in stands for it while
            // the focus is on nothing but a bare window.
            Assert.Contains("if (focused == null && touch.Row != null && touch.Row.Shown.Visibility == Visibility.Visible && ShortcutsFocusOnNothing()) focused = touch.Row;", evaluate);
            TextOrder.Before(evaluate, "focused = touch.Row;", "row.Shown.Visibility = shows", "the touched row is taken before the rows are hidden");
            var nothing = Between(code, "private static bool ShortcutsFocusOnNothing(", "\n        }");
            Assert.Contains("var at = Keyboard.FocusedElement;", nothing);
            Assert.Contains("return at == null || at is Window;", nothing);
            var page = Between(code, "private FrameworkElement BuildShortcutsPage(", "var page = TaggedPageLayout(");
            Assert.Matches(@"foreach \(var row in groups\.SelectMany\(group => group\.Rows\)\.Where\(row => row\.Bindable\)\)\s*\{\s*var touched = row;\s*row\.Shown\.PreviewMouseDown \+= \(sender, args\) => touch\.Row = touched;\s*row\.Shown\.GotKeyboardFocus \+= \(sender, args\) => touch\.Row = touched;", page);
            TextOrder.Before(evaluate, "var focused =", "row.Shown.Visibility = shows", "focus is read before the rows are hidden");
            var keep = Between(code, "private static void ShortcutsKeepFocus(", "\n        }");
            Assert.Contains("row.Bindable && row.Shown.Visibility == Visibility.Visible", keep);
            // From the hidden row's own place, and the first row that takes the focus keeps it.
            Assert.Contains("var at = rows.IndexOf(from);", keep);
            Assert.Contains("if (row.Row.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)) && row.Row.IsKeyboardFocusWithin) return;", keep);
            // Rows after it, then the ones before it nearest first, reversed in the spelling hooks section 0
            // asks for: SpanOverloadTests cannot see a .Reverse() whose receiver is a call.
            Assert.Contains("var near = rows.Skip(at + 1).Concat(Enumerable.Reverse(rows.Take(Math.Max(0, at))));", keep);
            Assert.DoesNotContain(".Reverse()", code);
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
            Assert.Contains("caption.Margin = new Thickness(0, PanelShortcuts.CaptionGap, 0, 0);", code);
            // A glance's caption keeps a row caption's 520, as Ui.SettingRow draws every other, however wide
            // the name column grows.
            // The shell's one constant, which the kit reads too: the page keeps no copy of the number.
            Assert.Equal(520, PanelShell.RowCaptionMaxWidth);
            Assert.Null(typeof(PanelShortcuts).GetField("CaptionMaxWidth"));
            Assert.Matches(@"caption\.Margin = new Thickness\(0, PanelShortcuts\.CaptionGap, 0, 0\);\s*caption\.MaxWidth = Math\.Min\(caption\.MaxWidth, PanelShell\.RowCaptionMaxWidth\);", code);
            var widgets = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "Widgets.Kit.cs"));
            Assert.Contains("Caption(caption, PanelShell.RowCaptionMaxWidth)", widgets);
            // Every number the model takes from the artboard is drawn with: none is left for the view to
            // spell as a literal of its own.
            foreach (var name in new[]
            {
                "HeaderPaddingX", "HeaderPaddingY", "GroupTitleSize", "GroupDetailGap", "CountSize", "RowPaddingX", "RowPaddingY",
                "RowGap", "RowNameSize", "TagGap", "PressWidth", "CaptionGap", "StackGap", "LeadPaddingX", "LeadPaddingY", "LeadGap",
                "BannerPaddingX", "BannerPaddingY", "BannerGap", "BannerTextSize", "BannerIconSize", "BannerStackGap", "BannerTint",
                "IntroGap", "FilterRaise", "FilterGapStacked",
            })
            {
                Assert.Matches(@"PanelShortcuts\." + name + @"\b", code);
            }
            Assert.Contains("Padding = new Thickness(PanelShortcuts.HeaderPaddingX, PanelShortcuts.HeaderPaddingY, PanelShortcuts.HeaderPaddingX, PanelShortcuts.HeaderPaddingY),", code);
            Assert.Contains("Padding = new Thickness(PanelShortcuts.RowPaddingX, PanelShortcuts.RowPaddingY, PanelShortcuts.RowPaddingX, PanelShortcuts.RowPaddingY),", code);
            Assert.Contains("Padding = new Thickness(PanelShortcuts.LeadPaddingX, PanelShortcuts.LeadPaddingY, PanelShortcuts.LeadPaddingX, PanelShortcuts.LeadPaddingY),", code);
            Assert.Contains("Padding = new Thickness(PanelShortcuts.BannerPaddingX, PanelShortcuts.BannerPaddingY, PanelShortcuts.BannerPaddingX, PanelShortcuts.BannerPaddingY),", code);
            Assert.Contains("var name = Ui.Text(title ?? string.Empty, PanelShortcuts.GroupTitleSize, FontWeights.SemiBold, Theme.TextPrimary);", code);
            Assert.Contains("name.Margin = new Thickness(0, 0, PanelShortcuts.GroupDetailGap, 0);", code);
            Assert.Contains("var name = Ui.Text(label, PanelShortcuts.RowNameSize, FontWeights.Normal, Theme.TextPrimary);", code);
            Assert.Contains("tag.Margin = new Thickness(PanelShortcuts.TagGap, 0, 0, 0);", code);
            Assert.Contains("pressText.Margin = new Thickness(PanelShortcuts.RowGap, 0, 0, 0);", code);
            Assert.Matches(@"icon\.VerticalAlignment = VerticalAlignment\.Top;\s*icon\.Margin = new Thickness\(0, 1, PanelShortcuts\.BannerGap, 0\);", code);
            Assert.Contains("text = Ui.Text(string.Empty, PanelShortcuts.BannerTextSize, FontWeights.Normal, Theme.TextSecondary);", code);
            // A greyed row's chip is the artboard's .key, as every binder's slot starts.
            Assert.Contains("var chip = Ui.BindingChip(Ui.NotBound, false, key: true);", code);
            // The clash sentence keeps a message line's measure in a column with no ceiling.
            var clash = Between(code, "private static Border ShortcutsClashLine(", "return line;");
            Assert.Contains("text.MaxWidth = BodyWidth;", clash);
            Assert.Contains("text.HorizontalAlignment = HorizontalAlignment.Left;", clash);
            Assert.Contains("text.TextWrapping = TextWrapping.Wrap;", clash);
            // The icon docked at the left and the sentence filling the rest, in the amber box.
            Assert.Matches(@"var dock = new DockPanel \{ LastChildFill = true \};\s*DockPanel\.SetDock\(icon, Dock\.Left\);\s*dock\.Children\.Add\(icon\);\s*dock\.Children\.Add\(text\);", clash);
            Assert.Contains("Child = dock,", clash);
            Assert.Contains("Background = Ui.Tint(Theme.Caution, PanelShortcuts.BannerTint),", clash);
            // The artboard's 1 px outline in deep caution, at the panel's corner.
            Assert.Matches(@"BorderBrush = Ui\.Brush\(Theme\.CautionDeep\),\s*BorderThickness = new Thickness\(PanelMetrics\.BorderWeight\),\s*Background = [^\n]*\s*CornerRadius = new CornerRadius\(Theme\.Radius\),", clash);
            Assert.DoesNotContain("0.06", code);
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
            Assert.Contains("var crumbs = Ui.Crumbs(PanelShortcuts.PagingCrumbs);", Between(code, "private ShortcutsGroupState BuildShortcutsCompanion(", "return group;"));
            Assert.Contains("Ui.Anchor(row, PanelBindings.Anchor(binding.Action));", code);
            Assert.Contains("Ui.Soon(row, item)", code);
            Assert.Contains("ShortcutsSoon(group, PanelSoon.RigTest);", code);
            Assert.Contains("ShortcutsSoon(group, PanelSoon.AlertDismissal);", code);
            Assert.Contains("padding: PanelKit.SegmentedPaddingShortcuts", code);
            // Columns follow the room a row has, never the sidebar's state.
            Assert.DoesNotContain("!Narrow", code);

            // The page's frame: its intro, the filter's labels and its hook into the evaluation, the empty line,
            // the header's two columns only where TwoColumns says, and every anchor id AnchorTable pins drawn.
            Assert.Contains("Ui.Caption(PanelShortcuts.IntroCaption, BodyWidth)", code);
            // The Map tags "Every button in one list" New, so the page's title carries the tag, as Rig's does.
            Assert.Contains("var page = TaggedPageLayout(PanelShortcuts.Title, Ui.NewTag(), null, sections.ToArray());", code);
            Assert.Contains("BuildSegmented(PanelShortcuts.FilterValues, PanelShortcuts.FilterLabels, shortcutsFilter,", code);
            // The filter stands alone at the header's right, under no row title, so it carries a group name for
            // a screen reader: its three choices are never read out bare. The name is on the filter itself,
            // since the kit's segmented bar has an automation peer of its own (#523).
            var header = Between(code, "private FrameworkElement BuildShortcutsHeader(", "return header;");
            Assert.Contains("AutomationProperties.SetName(filter, PanelShortcuts.FilterTitle);", header);
            // Two names on the page, the filter's and each clash sentence's, both on elements with a peer.
            Assert.Equal(2, Regex.Matches(code, @"AutomationProperties\.SetName\(").Count);
            Assert.DoesNotContain("SetName(line", code);
            Assert.Contains("evaluate = () => ShortcutsEvaluate(groups, filter, banner, empty, touch);", code);
            Assert.Contains("if (editor != null) ShortcutsWatch(editor, changed);", code);
            Assert.Contains("            evaluate();\n", code.Replace("\r\n", "\n"));
            Assert.Contains("var line = ShortcutsClashLine(clash, out said);", code);
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
            Assert.Contains("ShortcutsRow(binding.Label, binding.Press, editor, caption, out slot, out press, tags);", code);

            Assert.Contains("if (row.Press != null) row.Press.Visibility = PanelShortcuts.ShowsPress(states[row], row.IsHold) ? Visibility.Visible : Visibility.Hidden;", code);
            Assert.Contains("var tags = binding.IsNew ? new FrameworkElement[] { Ui.NewTag() } : new FrameworkElement[0];", code);
            Assert.Contains("ShortcutsRow(item.Title, PanelShortcuts.Tap, chip, null, out slot, out press);", code);
            Assert.Equal(3, Regex.Matches(code, Regex.Escape("PanelShortcuts.GroupDetail(screen.Name, screen.Kind, screen.Width, screen.Height)")).Count);
            Assert.Contains("ShortcutsCard(PanelShortcuts.RigGroupTitle, PanelShortcuts.RigGroupDetail, null);", code);
            Assert.Contains("ShortcutsCard(PanelShortcuts.AlertsGroupTitle, null, null);", code);

            // Every row's binder in one slot of the model's width, the gap on the slot, never on SimHub's editor,
            // whose template draws its own Margin a second time; the editor names nothing of its own.
            // The page reads no width at all (the shell's rule 11): each row decides as it is measured whether
            // its binder sits beside the name or under it, so a resize at any width rebuilds nothing and
            // re-creates none of SimHub's editors on the page.
            Assert.DoesNotMatch(@"\bContentWidth(UpTo)?\b", code);
            Assert.DoesNotContain("ColumnRoom", code);
            Assert.DoesNotContain("ShortcutsLayout", code);
            Assert.Contains("Child = new ShortcutsRowPanel(nameLine, caption, pressText, slot),", code);
            Assert.Contains("control.Margin = new Thickness(0);", code);
            Assert.DoesNotContain("control.Margin = new Thickness(PanelShortcuts", code);
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
            // The clash lines only when the page is readable, and each row a line names outlined in caution,
            // every other row's outline cleared on every pass.
            Assert.Contains("var clashes = readable\n                ? PanelShortcuts.Clashes(groups.SelectMany(group => group.Rows).Where(row => row.Bindable).SelectMany(row => uses[row]))\n                : new List<PanelShortcuts.Clash>();", code.Replace("\r\n", "\n"));
            Assert.Contains("foreach (var clash in clashes)", code);
            var mark = Between(code, "foreach (var row in groups.SelectMany(group => group.Rows).Where(row => row.Slot != null))", "var emptyText =");
            Assert.Contains("var marked = row.Bindable && PanelShortcuts.Marks(clashes, row.Place, row.Does);", mark);
            Assert.Contains("row.Slot.BorderBrush = marked ? Ui.Brush(Theme.CautionDeep) : null;", mark);
            var slot = Between(code.Replace("\r\n", "\n"), "slot = new Border\n", "};");
            Assert.Contains("BorderThickness = new Thickness(PanelMetrics.BorderWeight),", slot);
            // Each row's state as the reads and the marks use it: a live row bindable, placed on its screen's
            // card (so two screens' uses of one action stay apart) and saying what it does; a greyed row not.
            Assert.Contains("group.Rows.Add(new ShortcutsRowState { Action = binding.Action, Editor = editor, Row = row, Shown = row, Slot = slot, Press = press, IsHold = binding.IsHold, Bindable = true, Place = place, Does = binding.Does });", code);
            Assert.Contains("group.Rows.Add(new ShortcutsRowState { Row = row, Shown = shown, Slot = slot, Press = press, Bindable = false });", code);
            // A binding's presses come off SimHub's mapping by the name of its press type.
            Assert.Contains("PanelShortcuts.FiresOn(mapping.PressType.ToString())", code);

            // What the filter leaves on screen, each the model's: the cards' order, a card only with a row, the
            // rule above a row, and the empty line only when no row shows.
            Assert.Contains("var groups = PanelShortcuts.CardOrder(screens, lights, alerts);", code);
            Assert.Contains("var groupShown = PanelShortcuts.CardShows(group.Rows.Select(row => states[row]), chosen);", code);
            Assert.Contains("group.Card.Visibility = groupShown ? Visibility.Visible : Visibility.Collapsed;", code);
            Assert.Contains("row.Row.BorderThickness = new Thickness(0, PanelShortcuts.RuleAbove(firstShown, group.HasLead) ? PanelMetrics.BorderWeight : 0, 0, 0);", code);
            Assert.Contains("var emptyText = PanelShortcuts.EmptyLine(chosen, anyShown);", code);
            // The loop's own bookkeeping: a hidden row draws no rule and leaves the next one first, and the
            // empty line hears of a card shown anywhere on the page, not only the last.
            Assert.Matches(@"var anyShown = false;\s*foreach \(var group in groups\)\s*\{\s*var firstShown = true;\s*foreach \(var row in group\.Rows\)\s*\{\s*(//[^\n]*\s*)*if \(row\.Press != null\) row\.Press\.Visibility = [^;]*;\s*var shows = PanelShortcuts\.Shows\(chosen, states\[row\]\);\s*row\.Shown\.Visibility = shows \? Visibility\.Visible : Visibility\.Collapsed;\s*if \(!shows\) continue;\s*row\.Row\.BorderThickness = new Thickness\(0, PanelShortcuts\.RuleAbove\(firstShown, group\.HasLead\) \? PanelMetrics\.BorderWeight : 0, 0, 0\);\s*firstShown = false;\s*\}\s*var groupShown = PanelShortcuts\.CardShows\(group\.Rows\.Select\(row => states\[row\]\), chosen\);\s*group\.Card\.Visibility = groupShown \? Visibility\.Visible : Visibility\.Collapsed;\s*anyShown \|= groupShown;", code);

            // The anchor puts the filter back to All on the first build after the page is opened, and never on
            // a rebuild in place, which keeps the route and its anchor: OnLeave runs on Go alone.
            // What the evaluation puts on screen: the filter's choice written and read again, every live row
            // read and a greyed one not, each card's count, each clash line, and the banner and the empty
            // line shown only with something in them.
            Assert.Matches(@"value =>\s*\{\s*shortcutsFilter = value;\s*if \(evaluate != null\) evaluate\(\);\s*\}", code);
            Assert.Contains("var read = row.Bindable ? ShortcutsUses(row) : null;", code);
            Assert.Contains("uses[row] = read;", code);
            Assert.Contains("group.CountHost.Child = count == null ? null : Ui.Numeral(count, PanelShortcuts.CountSize, Theme.TextSecondary);", code);
            Assert.Contains("group.CountHost.Visibility = count == null ? Visibility.Collapsed : Visibility.Visible;", code);
            Assert.Contains("row.Shown.Visibility = shows ? Visibility.Visible : Visibility.Collapsed;", code);
            Assert.Contains("banner.Children.Add(line);", code);
            Assert.Contains("if (banner.Children.Count > 0) line.Margin = new Thickness(0, PanelShortcuts.BannerStackGap, 0, 0);", code);
            Assert.Contains("banner.Visibility = banner.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;", code);
            Assert.Contains("empty.Visibility = emptyText == null ? Visibility.Collapsed : Visibility.Visible;", code);
            // The clash line is the trigger's name, strong, then the rest after a space, and nothing else:
            // ruling 50's deleted aside ("Fine if you meant it.") cannot come back as a third run.
            var clashLine = Between(code, "private static Border ShortcutsClashLine(", "return line;");
            Assert.Contains("text.Inlines.Add(new Run(clash.Lead) { FontWeight = FontWeights.SemiBold, Foreground = Ui.Brush(Theme.TextPrimary) });", clashLine);
            Assert.Contains("text.Inlines.Add(new Run(\" \" + clash.Rest));", clashLine);
            Assert.Equal(2, Regex.Matches(code, @"new Run\(").Count);
            Assert.Contains("var icon = Ui.NavIcon(PanelIcons.Warning, Theme.Caution, PanelShortcuts.BannerIconSize);", clashLine);
            Assert.Contains("BorderBrush = Ui.Brush(Theme.CautionDeep),", clashLine);
            // The artboard's role=status, on the sentence, which has an automation peer where the line's Border
            // has none: named, a polite live region, and read out when it is drawn new, never on the page's
            // first pass, as a status says nothing of what it holds when the page opens.
            Assert.Matches(@"AutomationProperties\.SetName\(text, clash\.Text\);\s*AutomationProperties\.SetLiveSetting\(text, AutomationLiveSetting\.Polite\);", clashLine);
            Assert.Matches(@"var told = banner\.Tag as HashSet<string>;\s*var drawn = new HashSet<string>\(StringComparer\.Ordinal\);\s*foreach \(var clash in clashes\)\s*\{\s*TextBlock said;\s*var line = ShortcutsClashLine\(clash, out said\);\s*if \(banner\.Children\.Count > 0\) line\.Margin = [^;]*;\s*banner\.Children\.Add\(line\);\s*drawn\.Add\(clash\.Text\);\s*if \(told != null && !told\.Contains\(clash\.Text\)\) ShortcutsAnnounce\(said\);\s*\}\s*banner\.Tag = drawn;", code);
            var announce = Between(code, "private void ShortcutsAnnounce(", "\n        }");
            Assert.Contains("if (!AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged)) return;", announce);
            Assert.Matches(@"var peer = UIElementAutomationPeer\.CreatePeerForElement\(text\);\s*if \(peer != null\) peer\.RaiseAutomationEvent\(AutomationEvents\.LiveRegionChanged\);", announce);
            var model = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "PanelShortcuts.cs"));
            Assert.DoesNotContain("meant it", code);
            Assert.DoesNotContain("meant it", model);

            // The rows' reads: each use carries its row's card and what it does, from the editor's model or
            // the shell's read, and a blank trigger is no binding.
            var usesOf = Between(code, "private IList<PanelShortcuts.BindingUse> ShortcutsUses(", "catch (Exception ex)");
            Assert.Contains("read.Select(trigger => new PanelShortcuts.BindingUse(trigger, row.Place, row.Does)).ToList();", usesOf);
            Assert.Contains(".Where(mapping => mapping != null && !string.IsNullOrWhiteSpace(mapping.Trigger))", usesOf);
            Assert.Contains(".Select(mapping => new PanelShortcuts.BindingUse(mapping.Trigger, row.Place, row.Does, PanelShortcuts.FiresOn(mapping.PressType.ToString())))", usesOf);
            // Ruling 60's null, which the view alone gives: an editor that could not be made, a shell read
            // that failed and a read that threw each read as "cannot be read", never as nothing bound.
            var usesAll = Between(code, "private IList<PanelShortcuts.BindingUse> ShortcutsUses(", "private void ShortcutsWatch(");
            Assert.Contains("if (editor == null) return null;", usesAll);
            // A model with no list falls back to the shell's read, rather than throwing into the catch.
            Assert.Contains("if (model == null || model.Triggers == null)", usesAll);
            Assert.Contains("var read = TriggersOf(row.Action);", usesAll);
            Assert.Contains("return read == null ? null :", usesAll);
            Assert.Matches(@"catch \(Exception ex\)\s*\{\s*Log\.Warn\([^;]*\);\s*return null;\s*\}", usesAll);

            var landing = Regex.Match(code, @"if \(shortcutsLanding\)\s*\{\s*shortcutsFilter = PanelShortcuts\.FilterFor\(shortcutsFilter, to == null \? null : to\.Anchor, true\);\s*shortcutsLanding = false;\s*\}\s*OnLeave\(""Shortcuts\.filterLanding"", \(\) => shortcutsLanding = true\);");
            Assert.True(landing.Success, "the filter's reset for an anchor is not gated on the page's first build");
            Assert.Single(Regex.Matches(code, @"PanelShortcuts\.FilterFor\(shortcutsFilter, to"));
        }
    }
}
