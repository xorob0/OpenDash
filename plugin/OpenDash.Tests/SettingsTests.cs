// SettingsTests.cs: defaults, normalisation of what comes back from disk, and duplicate detection.
using System;
using System.Linq;
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
