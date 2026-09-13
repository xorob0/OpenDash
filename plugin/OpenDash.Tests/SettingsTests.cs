// SettingsTests.cs: defaults, normalisation of what comes back from disk, and duplicate detection.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class SettingsTests
    {
        /// <summary>The face these tests configure. Every setting belongs to one, and the reference face
        /// is the one a rig almost always has.</summary>
        private static readonly Contract.FaceSize Face = Contract.ReferenceFace;

        [Fact]
        public void Defaults_match_the_contract()
        {
            var settings = new OpenDashSettings();
            Assert.True(settings.ShiftLights);
            Assert.Equal("overall", settings.PositionMode);
            Assert.Equal("session", settings.DeltaReference);
            Assert.Equal("auto", settings.SessionProgress);
            Assert.Equal(Contract.DefaultSlots(), settings.Slots);
        }

        [Fact]
        public void Normalise_clamps_card_numbers_to_the_slot_default()
        {
            var settings = new OpenDashSettings { Slots = new[] { 99, -1, 5 } };
            settings.Normalise();
            // Out of range falls back to the slot's default; 5 is kept.
            Assert.Equal(new[] { 12, 0, 5, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, settings.Slots);
        }

        [Fact]
        public void Normalise_repairs_missing_and_oversized_slot_arrays()
        {
            var missing = new OpenDashSettings { Slots = null };
            missing.Normalise();
            Assert.Equal(Contract.DefaultSlots(), missing.Slots);

            var oversized = new OpenDashSettings { Slots = Enumerable.Repeat(3, 20).ToArray() };
            oversized.Normalise();
            Assert.Equal(12, oversized.Slots.Length);
            Assert.All(oversized.Slots, card => Assert.Equal(3, card));
        }

        [Theory]
        [InlineData("class", "class")]
        [InlineData("Class", "class")]
        [InlineData(" overall ", "overall")]
        [InlineData("bogus", "overall")]
        [InlineData("", "overall")]
        [InlineData(null, "overall")]
        public void Normalise_restores_unknown_modes_to_the_default(string stored, string expected)
        {
            var settings = new OpenDashSettings { PositionMode = stored };
            settings.Normalise();
            Assert.Equal(expected, settings.PositionMode);
        }

        [Fact]
        public void Normalise_covers_every_choice_setting()
        {
            var settings = new OpenDashSettings { DeltaReference = "ALLTIME", SessionProgress = "nonsense" };
            settings.Normalise();
            Assert.Equal("alltime", settings.DeltaReference);
            Assert.Equal("auto", settings.SessionProgress);
        }

        [Fact]
        public void Slot_and_SetSlot_are_one_based_and_safe()
        {
            var settings = new OpenDashSettings { Slots = new[] { 4 } };
            Assert.Equal(4, settings.Slot(1));
            Assert.Equal(0, settings.Slot(2));
            Assert.Equal(10, settings.Slot(12));
            settings.SetSlot(12, 7);
            Assert.Equal(7, settings.Slot(12));
            settings.SetSlot(3, 42);
            Assert.Equal(1, settings.Slot(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetSlot(13, 0));
        }

        [Fact]
        public void Duplicates_are_reported_as_on_the_canvas()
        {
            var slots = Contract.DefaultSlots();
            slots[7] = 7; // slot 8 shows Fuel laps, like slot 9
            var duplicates = DuplicateAssignment.Find(slots);
            var single = Assert.Single(duplicates);
            Assert.Equal(7, single.Card);
            Assert.Equal(new[] { 8, 9 }, single.Slots);
            Assert.Equal("Fuel laps is assigned to slots 8 and 9.", single.Message());
            Assert.Equal("Fuel laps is assigned to slots 8 and 9.", DuplicateAssignment.Warning(slots));
        }

        [Fact]
        public void Duplicates_list_three_or_more_slots_with_commas()
        {
            var slots = Contract.DefaultSlots();
            slots[4] = 11; // slot 5 no longer shows Delta
            slots[0] = 3;
            slots[1] = 3;
            slots[11] = 3;
            var duplicate = Assert.Single(DuplicateAssignment.Find(slots));
            Assert.Equal("Delta is assigned to slots 1, 2 and 12.", duplicate.Message());
        }

        [Fact]
        public void Duplicates_are_empty_for_the_default_layout()
        {
            Assert.Empty(new OpenDashSettings().Duplicates());
            Assert.Equal("", DuplicateAssignment.Warning(Contract.DefaultSlots()));
            Assert.Empty(DuplicateAssignment.Find(null));
        }

        [Fact]
        public void Duplicates_are_ordered_by_card_and_one_message_per_line()
        {
            var slots = new[] { 9, 9, 2, 2, 4, 5, 6, 7, 8, 0, 10, 11 };
            var messages = DuplicateAssignment.Warning(slots).Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            Assert.Equal(new[] { "Best lap is assigned to slots 3 and 4.", "ABS is assigned to slots 1 and 2." }, messages);
        }

        [Fact]
        public void Second_screen_settings_start_at_their_defaults()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            Assert.Equal(21, settings.Modules.Length);
            Assert.True(settings.Module(1));
            Assert.False(settings.Module(6));
            Assert.False(settings.Module(20));
            Assert.False(settings.Module(21));
            Assert.Equal(new[] { 0, 1, 4, 2 }, settings.Zones);
            Assert.Equal(0, settings.Zone("A"));
            Assert.Equal(2, settings.Zone("D"));
            Assert.Equal(5, settings.WideZone);
            Assert.Equal("", settings.WebViewUrl);
        }

        [Fact]
        public void A_short_or_broken_second_screen_state_is_repaired()
        {
            var settings = new OpenDashSettings
            {
                Modules = new[] { false, true },
                Zones = new[] { 99, 3 },
                WideZone = 42,
                WebViewUrl = "javascript:alert(1)",
            };
            settings.Normalise();
            // What the file carried is kept; the rest goes back to the catalogue defaults.
            Assert.Equal(21, settings.Modules.Length);
            Assert.False(settings.Module(1));
            Assert.True(settings.Module(2));
            Assert.False(settings.Module(6));
            // 99 is not a page, so zone A falls back; zone B keeps the 3 the file gave it.
            Assert.Equal(new[] { 0, 3, 4, 2 }, settings.Zones);
            Assert.Equal(Contract.DefaultWideZonePage, settings.WideZone);
            Assert.Equal("", settings.WebViewUrl);
        }

        [Fact]
        public void Second_screen_settings_are_copied_and_set_through_the_contract()
        {
            var source = new OpenDashSettings();
            source.SetModule(6, true);
            source.SetZone("B", 7);
            source.WideZone = 1;
            source.WebViewUrl = "https://garage61.net";
            var copy = new OpenDashSettings();
            copy.CopyFrom(source);
            Assert.True(copy.Module(6));
            Assert.Equal(7, copy.Zone("B"));
            Assert.Equal(1, copy.WideZone);
            Assert.Equal("https://garage61.net", copy.WebViewUrl);
            // The copy is independent: it holds its own arrays.
            source.SetModule(6, false);
            Assert.True(copy.Module(6));
            Assert.Throws<ArgumentOutOfRangeException>(() => copy.SetModule(22, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => copy.SetZone("E", 0));
        }

        // --- The dash face ---------------------------------------------------------------------

        [Fact]
        public void Face_defaults_are_the_gear_lap_times_the_relative_and_fuel()
        {
            var settings = new OpenDashSettings();
            Assert.Equal(new[] { 0, 0, 14, 0 }, settings.Face(Face).Zones);
            Assert.Equal(new[] { 0, 1, 5, 6 }, settings.Face(Face).BarFields);
            Assert.Equal(Contract.DefaultFaceZoneMasks(), settings.Face(Face).Masks);
            Assert.Equal("Gear, speed, revs", FacePages.NameOf("A", settings.FaceZoneStart(Face, "A")));
            Assert.Equal("Lap times", FacePages.NameOf("B", settings.FaceZoneStart(Face, "B")));
            Assert.Equal("Relative", FacePages.NameOf("C", settings.FaceZoneStart(Face, "C")));
            Assert.Equal("Fuel", FacePages.NameOf("D", settings.FaceZoneStart(Face, "D")));
        }

        [Fact]
        public void Normalise_clamps_a_page_to_its_own_zone_catalogue()
        {
            // Zone A has four pages and zone D eight, so a module number from zones B and C is outside both.
            var settings = new OpenDashSettings();
            settings.Face(Face).Starts = new[] { 17, 20, -3, 9 };
            settings.Face(Face).Zones = new[] { 17, 20, -3, 9 };
            settings.Normalise();
            Assert.Equal(new[] { 0, 20, 14, 0 }, settings.Face(Face).Starts);
            // The page a zone is showing is repaired on its own: the wheel button moves it away from the
            // start page on purpose, and that has to survive a save.
            Assert.Equal(new[] { 0, 20, 14, 0 }, settings.Face(Face).Zones);
        }

        [Fact]
        public void Normalise_trims_a_mask_to_the_pages_that_exist_and_refills_an_empty_one()
        {
            var settings = new OpenDashSettings();
            settings.Face(Face).Masks = new[] { 0xFF, 0, -1, 1 << 9 };
            settings.Normalise();
            // Zone A keeps its four bits, the empty zone B is refilled, zone C is trimmed to twenty-one,
            // and zone D's mask names only a page it does not have, so it is empty and refilled too.
            Assert.Equal(0xF, settings.Face(Face).Masks[0]);
            Assert.Equal(Contract.DefaultZoneMask(1), settings.Face(Face).Masks[1]);
            Assert.Equal(Contract.DefaultZoneMask(2), settings.Face(Face).Masks[2]);
            Assert.Equal(Contract.DefaultZoneMask(3), settings.Face(Face).Masks[3]);
        }

        [Fact]
        public void A_page_the_mask_turns_off_snaps_forward_to_the_next_one_that_is_on()
        {
            var settings = new OpenDashSettings();
            settings.SetFaceZoneStart(Face, "A", 0);
            settings.SetFaceZonePageEnabled(Face, "A", 0, false);
            // Zone A opened on page 0; with it off the cycle runs forward to page 1 rather than back.
            Assert.Equal(1, settings.FaceZoneStart(Face, "A"));
            Assert.Equal(1, settings.FaceZone(Face, "A"));
            Assert.False(settings.FaceZonePageEnabled(Face, "A", 0));
        }

        [Fact]
        public void Snapping_forward_wraps_when_the_pages_after_it_are_off_too()
        {
            var settings = new OpenDashSettings();
            settings.SetFaceZoneStart(Face, "A", 1);
            settings.SetFaceZonePageEnabled(Face, "A", 2, false);
            settings.SetFaceZonePageEnabled(Face, "A", 3, false);
            settings.SetFaceZonePageEnabled(Face, "A", 1, false);
            Assert.Equal(0, settings.FaceZoneStart(Face, "A"));
        }

        [Fact]
        public void Turning_off_the_last_enabled_page_is_refused()
        {
            var settings = new OpenDashSettings();
            for (var page = 1; page < 4; page++) settings.SetFaceZonePageEnabled(Face, "A", page, false);
            settings.SetFaceZonePageEnabled(Face, "A", 0, false);
            // A zone with an empty cycle has nothing to draw, so the last page stays on.
            Assert.True(settings.FaceZonePageEnabled(Face, "A", 0));
            Assert.Equal(1, settings.FaceZoneMask(Face, "A"));
        }

        [Fact]
        public void Choosing_a_start_page_turns_that_page_on()
        {
            var settings = new OpenDashSettings();
            settings.SetFaceZonePageEnabled(Face, "D", 3, false);
            settings.SetFaceZoneStart(Face, "D", 3);
            // Picking a page in the panel is a clearer statement than the checkbox that turned it off.
            Assert.True(settings.FaceZonePageEnabled(Face, "D", 3));
            Assert.Equal(3, settings.FaceZoneStart(Face, "D"));
        }

        [Fact]
        public void Bar_fields_fall_back_per_slot()
        {
            var settings = new OpenDashSettings { BarFields = new[] { 99, 3 } };
            settings.Normalise();
            Assert.Equal(new[] { 0, 3, 5, 6 }, settings.Face(Face).BarFields);
            Assert.Equal("Race time", FacePages.FieldName(settings.BarField(Face, "Left1")));
            Assert.Equal("Clock", FacePages.FieldName(settings.BarField(Face, "Left2")));
        }

        [Fact]
        public void Quick_glance_keeps_a_zone_and_page_that_exist_and_drops_the_rest()
        {
            Assert.Equal(212, Contract.NormaliseQuickGlance(212));
            Assert.Equal(0, Contract.NormaliseQuickGlance(0));
            // Zone A has four pages, so page 12 of zone A is not a glance.
            Assert.Equal(Contract.DefaultQuickGlance, Contract.NormaliseQuickGlance(12));
            // There is no fifth zone.
            Assert.Equal(Contract.DefaultQuickGlance, Contract.NormaliseQuickGlance(400));
            Assert.Equal(Contract.DefaultQuickGlance, Contract.NormaliseQuickGlance(-1));
            Assert.Equal(2, Contract.QuickGlanceZone(212));
            Assert.Equal(12, Contract.QuickGlancePage(212));
            Assert.Equal(212, Contract.QuickGlanceValue(2, 12));
        }

        [Fact]
        public void Two_zones_on_the_same_page_are_reported_and_allowed()
        {
            var settings = new OpenDashSettings();
            settings.SetFaceZoneStart(Face, "B", 14);
            var clashes = settings.FaceClashes(Face);
            Assert.Single(clashes);
            Assert.Equal("relative", clashes[0].PageId);
            Assert.Equal(new[] { "B", "C" }, clashes[0].Zones);
            Assert.Equal("Zone B and zone C both show Relative.", clashes[0].Message());
            // Reported, not prevented: both zones keep the page.
            Assert.Equal(14, settings.FaceZoneStart(Face, "B"));
            Assert.Equal(14, settings.FaceZoneStart(Face, "C"));
        }

        [Fact]
        public void A_clash_across_two_catalogues_counts_because_it_is_the_same_drawing()
        {
            // Zone A's page 3 and module 13 are both the track map, under two different numbers.
            var settings = new OpenDashSettings();
            settings.SetFaceZoneStart(Face, "A", 3);
            settings.SetFaceZoneStart(Face, "B", 12);
            Assert.Equal("Zone A and zone B both show Track.", FacePageClash.Warning(settings.Face(Face)));
        }

        [Fact]
        public void Three_zones_on_one_page_read_as_a_list()
        {
            // Module 5 and band D's first page are both fuel, so this is three zones on one drawing.
            var settings = new OpenDashSettings();
            settings.SetFaceZoneStart(Face, "B", 4);
            settings.SetFaceZoneStart(Face, "C", 4);
            settings.SetFaceZoneStart(Face, "D", 0);
            var clashes = settings.FaceClashes(Face);
            Assert.Single(clashes);
            Assert.Equal(new[] { "B", "C", "D" }, clashes[0].Zones);
            Assert.Equal("Zone B, zone C and zone D all show Fuel.", clashes[0].Message());
        }

        [Fact]
        public void A_face_with_nothing_in_common_warns_about_nothing()
        {
            Assert.Equal(string.Empty, FacePageClash.Warning(new OpenDashSettings().Face(Face)));
        }

        [Fact]
        public void CopyFrom_carries_the_face()
        {
            var source = new OpenDashSettings();
            source.SetFaceZoneStart(Face, "B", 6);
            source.SetFaceZonePageEnabled(Face, "B", 0, false);
            source.SetBarField(Face, "Right2", 9);
            source.SetQuickGlance(Face, Contract.QuickGlanceValue(3, 5));

            var copy = new OpenDashSettings();
            copy.CopyFrom(source);
            Assert.Equal(6, copy.FaceZoneStart(Face, "B"));
            Assert.False(copy.FaceZonePageEnabled(Face, "B", 0));
            Assert.Equal(9, copy.BarField(Face, "Right2"));
            Assert.Equal(Contract.QuickGlanceValue(3, 5), copy.QuickGlanceOf(Face));
            // A clone, not a share: writing the copy must not reach back into the source.
            copy.SetBarField(Face, "Right2", 2);
            Assert.Equal(9, source.BarField(Face, "Right2"));
        }

        [Fact]
        public void Face_accessors_refuse_a_letter_that_is_not_a_zone()
        {
            var settings = new OpenDashSettings();
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.FaceZone(Face, "E"));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetFaceZoneStart(Face, "E", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.BarField(Face, "Middle"));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetFaceZonePageEnabled(Face, "A", 4, true));
        }

        // --- What a wheel button does ----------------------------------------------------------

        [Fact]
        public void Cycling_steps_through_the_enabled_pages_and_wraps()
        {
            var settings = new OpenDashSettings();
            settings.OpenOnStartPages();
            Assert.Equal(0, settings.FaceZone(Face, "A"));
            Assert.Equal(1, settings.CycleFaceZone(Face, "A"));
            Assert.Equal(2, settings.CycleFaceZone(Face, "A"));
            Assert.Equal(3, settings.CycleFaceZone(Face, "A"));
            Assert.Equal(0, settings.CycleFaceZone(Face, "A"));
        }

        [Fact]
        public void Cycling_skips_the_pages_the_mask_turns_off()
        {
            // The mask is what sets the length of the cycle, which is the whole point of the panel's
            // most consequential control.
            var settings = new OpenDashSettings();
            settings.SetFaceZonePageEnabled(Face, "A", 1, false);
            settings.SetFaceZonePageEnabled(Face, "A", 2, false);
            settings.OpenOnStartPages();
            Assert.Equal(3, settings.CycleFaceZone(Face, "A"));
            Assert.Equal(0, settings.CycleFaceZone(Face, "A"));
        }

        [Fact]
        public void A_mask_of_any_length_cycles_through_exactly_the_pages_it_leaves_on()
        {
            // Zone B cycles twenty-one, which is where a mask can be any of two million shapes. What
            // has to hold for every one of them is that the cycle visits the enabled pages, in order,
            // and returns to where it started after as many presses as there are pages enabled.
            var masks = new[]
            {
                Contract.DefaultZoneMask(1),                        // everything
                1 << 4,                                             // one page
                (1 << 0) | (1 << 4) | (1 << 14),                    // three, spread out
                (1 << 19) | (1 << 20),                              // two, at the far end
                0x155555,                                           // every other page
                (1 << 0) | (1 << 20),                               // the first and the last
            };

            foreach (var mask in masks)
            {
                var settings = new OpenDashSettings();
                settings.Face(Face).Masks = new[] { Contract.DefaultZoneMask(0), mask, Contract.DefaultZoneMask(2), Contract.DefaultZoneMask(3) };
                settings.Normalise();
                Assert.Equal(mask, settings.FaceZoneMask(Face, "B"));

                var expected = new List<int>();
                for (var page = 0; page < Modules.Count; page++)
                {
                    if ((mask & (1 << page)) != 0) expected.Add(page);
                }

                settings.OpenOnStartPages();
                var first = settings.FaceZone(Face, "B");
                Assert.Contains(first, expected);

                // One press per enabled page comes back to the start, having seen each one once.
                var seen = new List<int> { first };
                for (var press = 1; press < expected.Count; press++) seen.Add(settings.CycleFaceZone(Face, "B"));
                Assert.Equal(expected.Count, seen.Distinct().Count());
                Assert.Equal(expected.OrderBy(p => p), seen.OrderBy(p => p));
                Assert.Equal(first, settings.CycleFaceZone(Face, "B"));
            }
        }

        [Fact]
        public void A_zone_with_one_page_left_stays_where_it_is()
        {
            var settings = new OpenDashSettings();
            for (var page = 1; page < 4; page++) settings.SetFaceZonePageEnabled(Face, "A", page, false);
            settings.OpenOnStartPages();
            Assert.Equal(0, settings.CycleFaceZone(Face, "A"));
            Assert.Equal(0, settings.CycleFaceZone(Face, "A"));
        }

        [Fact]
        public void A_zone_opens_on_the_page_it_is_set_to_open_on()
        {
            var settings = new OpenDashSettings();
            settings.SetFaceZoneStart(Face, "B", 6);
            settings.CycleFaceZone(Face, "B");
            settings.CycleFaceZone(Face, "B");
            Assert.Equal(8, settings.FaceZone(Face, "B"));
            // Which is what Init does, so a session begins where the driver set it rather than where
            // they happened to leave it.
            settings.OpenOnStartPages();
            Assert.Equal(6, settings.FaceZone(Face, "B"));
        }

        // --- Listing the class a driver is racing in ---------------------------------------------

        [Fact]
        public void The_class_filter_is_off_and_is_per_zone()
        {
            var settings = new OpenDashSettings();
            foreach (var letter in Contract.FaceZoneLetters) Assert.False(settings.FaceZoneIsClassOnly(Face, letter));

            // The point of it being per zone: zone B lists the race, zone C lists the class.
            settings.SetFaceZoneClassOnly(Face, "C", true);
            Assert.True(settings.FaceZoneIsClassOnly(Face, "C"));
            Assert.False(settings.FaceZoneIsClassOnly(Face, "B"));
        }

        [Fact]
        public void The_class_filter_is_per_face_as_well_as_per_zone()
        {
            // It lives in FaceSettings like every other zone setting, so a rig with a face on the wheel
            // and one beside it filters them apart rather than filtering both at once.
            var rim = Contract.FaceSizes[3];
            var settings = new OpenDashSettings();
            settings.SetFaceZoneClassOnly(Face, "C", true);
            Assert.True(settings.FaceZoneIsClassOnly(Face, "C"));
            Assert.False(settings.FaceZoneIsClassOnly(rim, "C"));
        }

        [Fact]
        public void The_class_filter_survives_a_save_and_a_short_array()
        {
            var settings = new OpenDashSettings();
            settings.Face(Face).ClassOnly = new[] { true };
            settings.Normalise();
            Assert.Equal(Contract.FaceZoneLetters.Length, settings.Face(Face).ClassOnly.Length);
            Assert.True(settings.FaceZoneIsClassOnly(Face, "A"));
            Assert.False(settings.FaceZoneIsClassOnly(Face, "D"));

            var copy = new OpenDashSettings();
            copy.CopyFrom(settings);
            Assert.True(copy.FaceZoneIsClassOnly(Face, "A"));
            // A clone, not the same array: editing one settings object must not edit the other.
            copy.SetFaceZoneClassOnly(Face, "A", false);
            Assert.True(settings.FaceZoneIsClassOnly(Face, "A"));
        }

        [Fact]
        public void The_panel_offers_the_class_filter_only_where_a_page_would_change()
        {
            // Zones B and C hold the leaderboard and the relative. Zone A lists nobody, and band D's
            // relative page is three gaps rather than a list.
            Assert.True(FacePages.OffersClassFilter("B"));
            Assert.True(FacePages.OffersClassFilter("C"));
            Assert.False(FacePages.OffersClassFilter("A"));
            Assert.False(FacePages.OffersClassFilter("D"));
        }

        [Fact]
        public void A_held_glance_shows_its_page_and_a_release_puts_the_zone_back()
        {
            var settings = new OpenDashSettings();
            settings.OpenOnStartPages();
            settings.CycleFaceZone(Face, "C");
            var before = settings.FaceZone(Face, "C");

            settings.BeginQuickGlance();
            Assert.True(settings.GlanceHeld);
            Assert.Equal(Contract.QuickGlancePage(Contract.DefaultQuickGlance), settings.FaceZone(Face, "C"));

            settings.EndQuickGlance();
            Assert.False(settings.GlanceHeld);
            Assert.Equal(before, settings.FaceZone(Face, "C"));
        }

        [Fact]
        public void A_glance_shows_a_page_the_mask_has_turned_off()
        {
            // A glance is an explicit thing a driver asked for by holding a button; the mask is about
            // what the cycle steps through. The track map is exactly the page somebody would want
            // held and never cycled to.
            var settings = new OpenDashSettings();
            settings.SetFaceZonePageEnabled(Face, "C", 12, false);
            settings.OpenOnStartPages();
            settings.BeginQuickGlance();
            Assert.Equal(12, settings.FaceZone(Face, "C"));
            settings.EndQuickGlance();
            Assert.False(settings.FaceZonePageEnabled(Face, "C", 12));
        }

        [Fact]
        public void A_second_press_while_a_glance_is_held_does_not_lose_the_page_underneath()
        {
            var settings = new OpenDashSettings();
            settings.OpenOnStartPages();
            var before = settings.FaceZone(Face, "C");
            settings.BeginQuickGlance();
            settings.BeginQuickGlance();
            settings.EndQuickGlance();
            Assert.Equal(before, settings.FaceZone(Face, "C"));
            // And a release with no press does nothing at all.
            settings.EndQuickGlance();
            Assert.Equal(before, settings.FaceZone(Face, "C"));
        }

        [Fact]
        public void Two_faces_cycle_apart()
        {
            // The whole reason the settings are per face. Before this, a rig with a face on the wheel
            // and one beside it moved both zones with one button and there was no way to give the
            // second screen a different catalogue.
            var settings = new OpenDashSettings();
            settings.Normalise();
            var other = Contract.FaceSizes[3];

            settings.SetFaceZoneStart(Face, "B", 6);
            settings.SetFaceZoneStart(other, "B", 11);
            Assert.Equal(6, settings.FaceZoneStart(Face, "B"));
            Assert.Equal(11, settings.FaceZoneStart(other, "B"));

            settings.CycleFaceZone(Face, "B");
            Assert.Equal(11, settings.FaceZone(other, "B"));

            settings.SetFaceZonePageEnabled(other, "B", 0, false);
            Assert.True(settings.FaceZonePageEnabled(Face, "B", 0));
            Assert.False(settings.FaceZonePageEnabled(other, "B", 0));

            settings.SetBarField(other, "Right2", 9);
            Assert.Equal(9, settings.BarField(other, "Right2"));
            Assert.Equal(Contract.DefaultBarFields[3], settings.BarField(Face, "Right2"));
        }

        [Fact]
        public void A_glance_moves_every_face_to_its_own_page()
        {
            // One button, and which screen a driver is looking at is not knowable here, so every face
            // glances to whatever it was configured to glance at.
            var settings = new OpenDashSettings();
            settings.Normalise();
            var other = Contract.FaceSizes[3];
            settings.SetQuickGlance(Face, Contract.QuickGlanceValue(1, 3));
            settings.SetQuickGlance(other, Contract.QuickGlanceValue(2, 7));
            var before = settings.FaceZone(Face, "B");
            var otherBefore = settings.FaceZone(other, "C");

            settings.BeginQuickGlance();
            Assert.True(settings.GlanceHeld);
            Assert.Equal(3, settings.FaceZone(Face, "B"));
            Assert.Equal(7, settings.FaceZone(other, "C"));

            settings.EndQuickGlance();
            Assert.False(settings.GlanceHeld);
            Assert.Equal(before, settings.FaceZone(Face, "B"));
            Assert.Equal(otherBefore, settings.FaceZone(other, "C"));
        }

        [Fact]
        public void A_settings_file_written_before_the_faces_were_separate_is_kept()
        {
            // rc.3 shipped a panel with zone controls and one set of zones behind them. A driver who
            // used it must not find their face reset by an upgrade, so the old fields are read once
            // into the reference face and then never written again.
            var settings = new OpenDashSettings
            {
                FaceZoneStarts = new[] { 2, 6, 11, 3 },
                FaceZones = new[] { 2, 6, 11, 3 },
                BarFields = new[] { 4, 1, 5, 9 },
                QuickGlance = Contract.QuickGlanceValue(1, 3),
            };
            settings.Normalise();

            Assert.Equal(6, settings.FaceZoneStart(Face, "B"));
            Assert.Equal(9, settings.BarField(Face, "Right2"));
            Assert.Equal(Contract.QuickGlanceValue(1, 3), settings.QuickGlanceOf(Face));
            // Only the reference face: nothing can know which screen those settings were for.
            Assert.Equal(Contract.DefaultFaceZonePages[1], settings.FaceZoneStart(Contract.FaceSizes[3], "B"));
            // And the legacy fields are cleared, so a later save cannot overwrite the new state with them.
            Assert.Null(settings.FaceZones);
            Assert.Null(settings.BarFields);
            Assert.Equal(0, settings.QuickGlance);
        }

        [Fact]
        public void A_migration_never_overwrites_a_face_that_is_already_set()
        {
            // The legacy fields survive in a settings file that also has the new ones, and adopting
            // them a second time would undo whatever the driver has set since.
            var settings = new OpenDashSettings { FaceZoneStarts = new[] { 2, 6, 11, 3 }, FaceZones = new[] { 2, 6, 11, 3 } };
            settings.Face(Face).Starts = new[] { 1, 2, 3, 4 };
            settings.Normalise();
            Assert.Equal(2, settings.FaceZoneStart(Face, "B"));
        }

        [Fact]
        public void A_face_the_build_no_longer_ships_is_dropped()
        {
            // A settings file from another version can name a size nothing ships at. Carrying it
            // forward would attach a group of properties for a face nobody has.
            var settings = new OpenDashSettings();
            settings.Faces["Face1x1"] = new FaceSettings();
            settings.Normalise();
            Assert.False(settings.Faces.ContainsKey("Face1x1"));
            Assert.Equal(Contract.FaceSizes.Count, settings.Faces.Count);
        }

        [Fact]
        public void A_rig_of_two_faces_a_pit_wall_and_a_companion_round_trips()
        {
            // SimHub persists the settings with Json.NET; what is asserted here is the shape rather than
            // that serialiser, namely that every part of a rig is a plain settable member and comes back
            // as it went in. A second face is the case that matters: the two carry different zones.
            var rim = Contract.FaceSizes[3];
            var settings = new OpenDashSettings
            {
                Screens = new List<string> { Contract.FacePrefix(Face), Contract.FacePrefix(rim), Contract.CompanionPrefix, Contract.PitWallPrefix },
            };
            settings.Normalise();
            settings.SetFaceZoneStart(Face, "B", 4);
            settings.SetFaceZoneStart(rim, "B", 9);
            settings.SetBarField(rim, "Left1", 3);
            settings.SetModule(6, true);
            settings.SetZone("A", 3);
            settings.WebViewUrl = "https://garage61.net";

            var read = JsonSerializer.Deserialize<OpenDashSettings>(JsonSerializer.Serialize(settings));
            read.Normalise();

            Assert.Equal(settings.Screens, read.Screens);
            Assert.Equal(4, read.FaceZoneStart(Face, "B"));
            Assert.Equal(9, read.FaceZoneStart(rim, "B"));
            Assert.Equal(3, read.BarField(rim, "Left1"));
            Assert.True(read.Module(6));
            Assert.Equal(3, read.Zone("A"));
            Assert.Equal("https://garage61.net", read.WebViewUrl);
            // And the rig it names is the rig it keeps: a face outside it is not added back by a save.
            Assert.Equal(2, read.Screens.Count(Contract.IsKnownFacePrefix));
        }

        [Fact]
        public void Normalise_fills_in_a_screen_it_has_not_seen()
        {
            // What happens when a screen is added: the rig names it before anything has configured it,
            // and it has to start from the defaults rather than from nothing.
            var settings = new OpenDashSettings { Screens = new List<string> { Contract.FacePrefix(Face) } };
            settings.Normalise();
            Assert.Equal(new[] { Contract.FacePrefix(Face) }, settings.Faces.Keys);

            var rim = Contract.FaceSizes[3];
            settings.Screens.Add(Contract.FacePrefix(rim));
            settings.Normalise();
            Assert.True(settings.Faces.ContainsKey(Contract.FacePrefix(rim)));
            Assert.Equal(Contract.DefaultFaceZones(), settings.Face(rim).Zones);
            Assert.Equal(Contract.DefaultFaceZoneMasks(), settings.Face(rim).Masks);
            Assert.Equal(Contract.DefaultQuickGlance, settings.Face(rim).QuickGlance);
        }

        [Fact]
        public void A_screen_no_version_ships_is_dropped_from_the_rig()
        {
            // The same reason a face group at an unshipped size is dropped: a settings file can come
            // from another version, and a screen nobody has would attach a group nothing reads.
            var settings = new OpenDashSettings
            {
                Screens = new List<string> { Contract.PitWallPrefix, "Face1x1", null, Contract.PitWallPrefix },
            };
            settings.Normalise();
            Assert.Equal(new[] { Contract.PitWallPrefix }, settings.Screens);
        }

        // --- The lights ------------------------------------------------------------------------
        //
        // A settings file is whatever came back from disk: written by an older build, hand-edited, or
        // truncated. Normalise() is the only thing between that and a profile reading it, so the tests
        // here are all about what a bad file turns into.

        [Fact]
        public void Normalise_repairs_per_matrix_arrays_that_came_back_short()
        {
            var settings = new OpenDashSettings
            {
                FlagBoxRest = new[] { "dark" },
                FlagBoxFlags = new[] { false },
                FlagBoxSide = new[] { "left" },
            };
            settings.Normalise();

            // What the file named is kept; what it did not name falls back to the default for that
            // matrix rather than to the default for matrix 1.
            Assert.Equal(4, settings.FlagBoxRest.Length);
            Assert.Equal("dark", settings.MatrixRest(1));
            Assert.Equal("dark", settings.MatrixRest(4));
            Assert.False(settings.MatrixFlags(1));
            Assert.False(settings.MatrixFlags(2));
            Assert.Equal("left", settings.MatrixSide(1));
            Assert.Equal("both", settings.MatrixSide(2));
        }

        [Fact]
        public void Normalise_replaces_a_per_matrix_value_that_is_not_one_of_the_choices()
        {
            // A hand-edited file, or one written by a build that had a choice this one dropped.
            var settings = new OpenDashSettings
            {
                FlagBoxRest = new[] { "sideways", "gear", null, "dark" },
                FlagBoxSide = new[] { "up", "right", "both", null },
            };
            settings.Normalise();

            Assert.Equal("gear", settings.MatrixRest(1));
            Assert.Equal("gear", settings.MatrixRest(2));
            Assert.Equal("dark", settings.MatrixRest(3));
            Assert.Equal("both", settings.MatrixSide(1));
            Assert.Equal("right", settings.MatrixSide(2));
            Assert.Equal("both", settings.MatrixSide(4));
        }

        [Fact]
        public void Normalise_survives_per_matrix_arrays_that_are_null_or_too_long()
        {
            var settings = new OpenDashSettings
            {
                FlagBoxRest = null,
                FlagBoxSpotter = new[] { true, true, true, true, true, true },
            };
            settings.Normalise();

            Assert.Equal("gear", settings.MatrixRest(1));
            Assert.Equal("dark", settings.MatrixRest(2));
            Assert.Equal(4, settings.FlagBoxSpotter.Length);
            Assert.True(settings.MatrixSpotter(4));
        }

        [Fact]
        public void A_matrix_outside_one_to_four_reads_as_its_default_rather_than_throwing()
        {
            // Nothing should ask, but a profile is a file and the panel is a UI; neither is worth
            // crashing SimHub's settings page over.
            var settings = new OpenDashSettings();
            settings.Normalise();
            Assert.Equal("both", settings.MatrixSide(0));
            Assert.Equal("both", settings.MatrixSide(9));
            Assert.False(settings.MatrixFlags(9));
        }

        [Fact]
        public void Normalise_clamps_the_brightnesses_and_repairs_the_thresholds()
        {
            var settings = new OpenDashSettings
            {
                LightsBrightness = 250,
                LightsNightBrightness = -4,
                FlagBoxLowFuelLaps = -1,
                FlagBoxOilTemp = -20,
            };
            settings.Normalise();

            Assert.Equal(100, settings.LightsBrightness);
            Assert.Equal(0, settings.LightsNightBrightness);
            Assert.Equal(Contract.DefaultFlagBoxLowFuelLaps, settings.FlagBoxLowFuelLaps);
            // Zero, not a Celsius number: zero means "not set" and lets the profile pick the default
            // for whichever unit SimHub is in.
            Assert.Equal(0, settings.FlagBoxOilTemp);
        }

        [Fact]
        public void The_defaults_are_a_working_single_box_setup()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();

            Assert.Equal("gear", settings.MatrixRest(1));
            Assert.True(settings.MatrixFlags(1));
            Assert.True(settings.MatrixSpotter(1));
            Assert.True(settings.MatrixWarnings(1));
            foreach (var matrix in new[] { 2, 3, 4 })
            {
                Assert.Equal("dark", settings.MatrixRest(matrix));
                Assert.False(settings.MatrixFlags(matrix));
                Assert.False(settings.MatrixSpotter(matrix));
                Assert.False(settings.MatrixWarnings(matrix));
            }
            Assert.Equal(100, settings.LightsBrightness);
            Assert.True(settings.LightsNightBrightness < settings.LightsBrightness);
            Assert.False(settings.LightsNightMode);
            Assert.False(settings.FlagBoxCriticalOnly);
            Assert.True(settings.FlagBoxGear);
        }

        [Fact]
        public void A_settings_file_written_before_the_lights_existed_comes_back_with_them_defaulted()
        {
            // Every rc.1 user's file is one of these. It must not come back with a dark matrix 1.
            var json = "{\"ShiftLights\":false,\"PositionMode\":\"class\"}";
            var settings = JsonSerializer.Deserialize<OpenDashSettings>(json);
            settings.Normalise();

            Assert.False(settings.ShiftLights);
            Assert.Equal("gear", settings.MatrixRest(1));
            Assert.True(settings.MatrixFlags(1));
            Assert.Equal(100, settings.LightsBrightness);
        }

        [Fact]
        public void The_lights_are_declared_whatever_the_rig_is()
        {
            // Unlike a screen the rig has not got. There is nothing to detect -- openDash does not
            // install the profile (ADR 0013) -- and it is a fixed handful of names.
            var settings = new OpenDashSettings { Screens = new List<string>() };
            settings.Normalise();
            var declared = settings.DeclaredProperties().ToList();
            foreach (var name in Contract.LightsPropertyNames()) Assert.Contains(name, declared);
        }

        [Fact]
        public void The_declared_properties_grow_and_shrink_with_the_rig()
        {
            // Eight face sizes times twenty-one properties is what the plugin used to attach whatever
            // the rig was. What it attaches now is the four modes and the twelve slots, which every
            // screen shares, and one group per screen the settings hold.
            const int perFace = 4 + 4 + 4 + 4 + 4 + 1;
            var shared = Contract.SharedPropertyNames().Count();
            Assert.Equal(16, shared);
            // The lights are declared whatever the rig is: openDash does not install the flag box
            // profile (ADR 0013), so there is nothing to detect, and it is a fixed handful of names
            // rather than the hundred and thirty-six that made the screens worth narrowing.
            var lights = Contract.LightsPropertyNames().Count();

            var settings = new OpenDashSettings { Screens = new List<string>() };
            settings.Normalise();
            Assert.Equal(Contract.SharedPropertyNames().Concat(Contract.LightsPropertyNames()), settings.DeclaredProperties());

            settings.Screens.Add(Contract.FacePrefix(Face));
            settings.Screens.Add(Contract.FacePrefix(Contract.FaceSizes[3]));
            settings.Screens.Add(Contract.CompanionPrefix);
            settings.Screens.Add(Contract.PitWallPrefix);
            settings.Normalise();
            var names = settings.DeclaredProperties().ToList();
            Assert.Equal(shared + 2 * perFace + Modules.Count + 6 + lights, names.Count);
            Assert.Equal(names.Count, names.Distinct().Count());
            Assert.Contains("Face1920x480ZoneA", names);
            Assert.Contains("Face850x480ZoneA", names);
            Assert.Contains("CompanionModule21", names);
            Assert.Contains("WebViewUrl", names);
            // And the six faces the rig has not got are not declared at all.
            Assert.DoesNotContain("Face1280x480ZoneA", names);

            settings.Screens.Remove(Contract.FacePrefix(Contract.FaceSizes[3]));
            settings.Normalise();
            Assert.Equal(shared + perFace + Modules.Count + 6 + lights, settings.DeclaredProperties().Count());

            // A settings file that has never named a rig reads as every screen, which is what the plugin
            // attached before a rig could be named at all.
            var old = new OpenDashSettings();
            Assert.Equal(Contract.PropertyNames(), old.DeclaredProperties());
            old.Normalise();
            Assert.Equal(Contract.ScreenPrefixes(), old.Screens);
        }

        [Fact]
        public void The_five_actions_are_named_as_verbs()
        {
            // Five per face, because two faces on one rig have to cycle apart.
            var actions = Contract.ActionNames().ToArray();
            Assert.Equal(new[] { "Face1920x480CycleZoneA", "Face1920x480CycleZoneB", "Face1920x480CycleZoneC", "Face1920x480CycleZoneD", "Face1920x480HoldQuickGlance" }, actions.Take(5).ToArray());
            Assert.Equal(Contract.FaceSizes.Count * 5, actions.Length);
            // The action and the property it reads must not share a name: one is what the glance is
            // set to, the other is the button that shows it.
            Assert.DoesNotContain(Contract.HoldQuickGlanceActionFor(Face), Contract.PropertyNames());
            Assert.Throws<ArgumentOutOfRangeException>(() => Contract.CycleZoneAction(Face, "E"));
        }
    }
}
