// SettingsTests.cs: defaults, normalisation of what comes back from disk, and duplicate detection.
using System;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class SettingsTests
    {
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
            Assert.Equal(new[] { 0, 0, 14, 0 }, settings.FaceZones);
            Assert.Equal(new[] { 0, 1, 5, 6 }, settings.BarFields);
            Assert.Equal(Contract.DefaultFaceZoneMasks(), settings.FaceZoneMasks);
            Assert.Equal("Gear, speed, revs", FacePages.NameOf("A", settings.FaceZoneStart("A")));
            Assert.Equal("Lap times", FacePages.NameOf("B", settings.FaceZoneStart("B")));
            Assert.Equal("Relative", FacePages.NameOf("C", settings.FaceZoneStart("C")));
            Assert.Equal("Fuel", FacePages.NameOf("D", settings.FaceZoneStart("D")));
        }

        [Fact]
        public void Normalise_clamps_a_page_to_its_own_zone_catalogue()
        {
            // Zone A has four pages and zone D eight, so a module number from zones B and C is outside both.
            var settings = new OpenDashSettings { FaceZoneStarts = new[] { 17, 20, -3, 9 }, FaceZones = new[] { 17, 20, -3, 9 } };
            settings.Normalise();
            Assert.Equal(new[] { 0, 20, 14, 0 }, settings.FaceZoneStarts);
            // The page a zone is showing is repaired on its own: the wheel button moves it away from the
            // start page on purpose, and that has to survive a save.
            Assert.Equal(new[] { 0, 20, 14, 0 }, settings.FaceZones);
        }

        [Fact]
        public void Normalise_trims_a_mask_to_the_pages_that_exist_and_refills_an_empty_one()
        {
            var settings = new OpenDashSettings { FaceZoneMasks = new[] { 0xFF, 0, -1, 1 << 9 } };
            settings.Normalise();
            // Zone A keeps its four bits, the empty zone B is refilled, zone C is trimmed to twenty-one,
            // and zone D's mask names only a page it does not have, so it is empty and refilled too.
            Assert.Equal(0xF, settings.FaceZoneMasks[0]);
            Assert.Equal(Contract.DefaultZoneMask(1), settings.FaceZoneMasks[1]);
            Assert.Equal(Contract.DefaultZoneMask(2), settings.FaceZoneMasks[2]);
            Assert.Equal(Contract.DefaultZoneMask(3), settings.FaceZoneMasks[3]);
        }

        [Fact]
        public void A_page_the_mask_turns_off_snaps_forward_to_the_next_one_that_is_on()
        {
            var settings = new OpenDashSettings();
            settings.SetFaceZoneStart("A", 0);
            settings.SetFaceZonePageEnabled("A", 0, false);
            // Zone A opened on page 0; with it off the cycle runs forward to page 1 rather than back.
            Assert.Equal(1, settings.FaceZoneStart("A"));
            Assert.Equal(1, settings.FaceZone("A"));
            Assert.False(settings.FaceZonePageEnabled("A", 0));
        }

        [Fact]
        public void Snapping_forward_wraps_when_the_pages_after_it_are_off_too()
        {
            var settings = new OpenDashSettings();
            settings.SetFaceZoneStart("A", 1);
            settings.SetFaceZonePageEnabled("A", 2, false);
            settings.SetFaceZonePageEnabled("A", 3, false);
            settings.SetFaceZonePageEnabled("A", 1, false);
            Assert.Equal(0, settings.FaceZoneStart("A"));
        }

        [Fact]
        public void Turning_off_the_last_enabled_page_is_refused()
        {
            var settings = new OpenDashSettings();
            for (var page = 1; page < 4; page++) settings.SetFaceZonePageEnabled("A", page, false);
            settings.SetFaceZonePageEnabled("A", 0, false);
            // A zone with an empty cycle has nothing to draw, so the last page stays on.
            Assert.True(settings.FaceZonePageEnabled("A", 0));
            Assert.Equal(1, settings.FaceZoneMask("A"));
        }

        [Fact]
        public void Choosing_a_start_page_turns_that_page_on()
        {
            var settings = new OpenDashSettings();
            settings.SetFaceZonePageEnabled("D", 3, false);
            settings.SetFaceZoneStart("D", 3);
            // Picking a page in the panel is a clearer statement than the checkbox that turned it off.
            Assert.True(settings.FaceZonePageEnabled("D", 3));
            Assert.Equal(3, settings.FaceZoneStart("D"));
        }

        [Fact]
        public void Bar_fields_fall_back_per_slot()
        {
            var settings = new OpenDashSettings { BarFields = new[] { 99, 3 } };
            settings.Normalise();
            Assert.Equal(new[] { 0, 3, 5, 6 }, settings.BarFields);
            Assert.Equal("Race time", FacePages.FieldName(settings.BarField("Left1")));
            Assert.Equal("Clock", FacePages.FieldName(settings.BarField("Left2")));
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
            settings.SetFaceZoneStart("B", 14);
            var clashes = settings.FaceClashes();
            Assert.Single(clashes);
            Assert.Equal("relative", clashes[0].PageId);
            Assert.Equal(new[] { "B", "C" }, clashes[0].Zones);
            Assert.Equal("Zone B and zone C both show Relative.", clashes[0].Message());
            // Reported, not prevented: both zones keep the page.
            Assert.Equal(14, settings.FaceZoneStart("B"));
            Assert.Equal(14, settings.FaceZoneStart("C"));
        }

        [Fact]
        public void A_clash_across_two_catalogues_counts_because_it_is_the_same_drawing()
        {
            // Zone A's page 3 and module 13 are both the track map, under two different numbers.
            var settings = new OpenDashSettings();
            settings.SetFaceZoneStart("A", 3);
            settings.SetFaceZoneStart("B", 12);
            Assert.Equal("Zone A and zone B both show Track.", FacePageClash.Warning(settings));
        }

        [Fact]
        public void Three_zones_on_one_page_read_as_a_list()
        {
            // Module 5 and band D's first page are both fuel, so this is three zones on one drawing.
            var settings = new OpenDashSettings();
            settings.SetFaceZoneStart("B", 4);
            settings.SetFaceZoneStart("C", 4);
            settings.SetFaceZoneStart("D", 0);
            var clashes = settings.FaceClashes();
            Assert.Single(clashes);
            Assert.Equal(new[] { "B", "C", "D" }, clashes[0].Zones);
            Assert.Equal("Zone B, zone C and zone D all show Fuel.", clashes[0].Message());
        }

        [Fact]
        public void A_face_with_nothing_in_common_warns_about_nothing()
        {
            Assert.Equal(string.Empty, FacePageClash.Warning(new OpenDashSettings()));
        }

        [Fact]
        public void CopyFrom_carries_the_face()
        {
            var source = new OpenDashSettings();
            source.SetFaceZoneStart("B", 6);
            source.SetFaceZonePageEnabled("B", 0, false);
            source.SetBarField("Right2", 9);
            source.QuickGlance = Contract.QuickGlanceValue(3, 5);

            var copy = new OpenDashSettings();
            copy.CopyFrom(source);
            Assert.Equal(6, copy.FaceZoneStart("B"));
            Assert.False(copy.FaceZonePageEnabled("B", 0));
            Assert.Equal(9, copy.BarField("Right2"));
            Assert.Equal(Contract.QuickGlanceValue(3, 5), copy.QuickGlance);
            // A clone, not a share: writing the copy must not reach back into the source.
            copy.SetBarField("Right2", 2);
            Assert.Equal(9, source.BarField("Right2"));
        }

        [Fact]
        public void Face_accessors_refuse_a_letter_that_is_not_a_zone()
        {
            var settings = new OpenDashSettings();
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.FaceZone("E"));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetFaceZoneStart("E", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.BarField("Middle"));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetFaceZonePageEnabled("A", 4, true));
        }
    }
}
