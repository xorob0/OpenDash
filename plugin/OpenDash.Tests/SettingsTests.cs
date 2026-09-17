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
            Assert.Equal(Contract.RevBarShift, settings.RevBarMode());
            Assert.Equal("overall", settings.PositionMode);
            Assert.Equal("session", settings.DeltaReference);
            Assert.Equal("auto", settings.SessionProgress);
            Assert.Equal(Contract.DefaultSlots(), settings.Slots);
        }

        /// <summary>
        /// XOR-138 and XOR-119. The mode arrived after rc.2 shipped, so a settings file may not carry
        /// it, and the one thing that must not happen is a driver who turned the shift lights off
        /// finding them back on after an update.
        /// </summary>
        [Fact]
        public void A_settings_file_written_before_the_mode_existed_keeps_its_answer()
        {
            // What Json.NET leaves behind for an rc.2 file: ShiftLights set, RevBar absent.
            var off = new OpenDashSettings { ShiftLights = false, RevBar = null };
            Assert.Equal(Contract.RevBarRpm, off.RevBarMode());
            off.Normalise();
            Assert.Equal(Contract.RevBarRpm, off.RevBar);
            Assert.False(off.ShiftLights);

            var on = new OpenDashSettings { ShiftLights = true, RevBar = null };
            on.Normalise();
            Assert.Equal(Contract.RevBarShift, on.RevBar);
            Assert.True(on.ShiftLights);
        }

        [Fact]
        public void The_mode_wins_over_the_alias_once_it_is_set_and_the_two_stay_in_step()
        {
            // A file that carries both: the mode is what the driver chose last, so it decides, and
            // the alias is rewritten to agree rather than left to contradict it.
            var settings = new OpenDashSettings { ShiftLights = true, RevBar = Contract.RevBarOff };
            settings.Normalise();
            Assert.Equal(Contract.RevBarOff, settings.RevBar);
            Assert.False(settings.ShiftLights);

            settings.SetRevBar(Contract.RevBarShift);
            Assert.True(settings.ShiftLights);
            settings.SetRevBar(Contract.RevBarRpm);
            Assert.False(settings.ShiftLights);
        }

        [Fact]
        public void An_unreadable_mode_falls_back_through_the_alias()
        {
            var settings = new OpenDashSettings { ShiftLights = false, RevBar = "sparkles" };
            settings.Normalise();
            Assert.Equal(Contract.RevBarRpm, settings.RevBar);

            // Case and whitespace are the shapes a hand-edited file has.
            var typed = new OpenDashSettings { RevBar = "  OFF " };
            typed.Normalise();
            Assert.Equal(Contract.RevBarOff, typed.RevBar);

            // And an unknown mode handed to the setter is refused rather than stored.
            var set = new OpenDashSettings();
            set.SetRevBar("sparkles");
            Assert.Equal(Contract.DefaultRevBar, set.RevBar);
        }

        [Fact]
        public void CopyFrom_carries_the_mode()
        {
            var source = new OpenDashSettings();
            source.SetRevBar(Contract.RevBarOff);
            var target = new OpenDashSettings();
            target.CopyFrom(source);
            Assert.Equal(Contract.RevBarOff, target.RevBar);
            Assert.False(target.ShiftLights);
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
            // Since ADR 0017 the companion's rotation and the pit wall's zones live on the screen that
            // has them, so the defaults are a new screen's rather than the settings object's.
            var companion = Screen(Contract.KindCompanion, 850, 480);
            Assert.Equal(21, companion.Modules.Length);
            Assert.True(companion.Modules[0]);
            Assert.False(companion.Modules[5]);
            Assert.False(companion.Modules[19]);
            Assert.False(companion.Modules[20]);

            var wall = Screen(Contract.KindPitWall, 1920, 1080);
            Assert.Equal(new[] { 0, 1, 4, 2 }, wall.Zones);
            Assert.Equal(5, wall.WideZone);
            Assert.Equal("", wall.WebViewUrl);
        }

        [Fact]
        public void A_short_or_broken_second_screen_state_is_repaired()
        {
            var companion = new ScreenInstance
            {
                Kind = Contract.KindCompanion,
                Width = 850,
                Height = 480,
                Modules = new[] { false, true },
            };
            companion.Normalise();
            // What the file carried is kept; the rest goes back to the catalogue defaults.
            Assert.Equal(21, companion.Modules.Length);
            Assert.False(companion.Modules[0]);
            Assert.True(companion.Modules[1]);
            Assert.False(companion.Modules[5]);

            var wall = new ScreenInstance
            {
                Kind = Contract.KindPitWall,
                Width = 1920,
                Height = 1080,
                Zones = new[] { 99, 3 },
                WideZone = 42,
                WebViewUrl = "javascript:alert(1)",
            };
            wall.Normalise();
            // 99 is not a page, so zone A falls back; zone B keeps the 3 the file gave it.
            Assert.Equal(new[] { 0, 3, 4, 2 }, wall.Zones);
            Assert.Equal(Contract.DefaultWideZonePage, wall.WideZone);
            Assert.Equal("", wall.WebViewUrl);
        }

        [Fact]
        public void Second_screen_settings_are_copied_with_the_rig()
        {
            var source = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            source.Normalise();
            var companion = Screen(Contract.KindCompanion, 850, 480);
            var wall = Screen(Contract.KindPitWall, 1920, 1080);
            source.Rig.Add(companion);
            source.Rig.Add(wall);
            companion.Modules[5] = true;
            wall.Zones[1] = 7;
            wall.WideZone = 1;
            wall.WebViewUrl = "https://garage61.net";

            var copy = new OpenDashSettings();
            copy.CopyFrom(source);
            Assert.True(copy.ScreenByNamespace(Contract.CompanionPrefix).Modules[5]);
            Assert.Equal(7, copy.ScreenZone(Contract.PitWallPrefix, "B"));
            Assert.Equal(1, copy.ScreenWideZone(Contract.PitWallPrefix));
            Assert.Equal("https://garage61.net", copy.ScreenWebViewUrl(Contract.PitWallPrefix));

            // The copy is independent: it holds its own screens and its own arrays.
            companion.Modules[5] = false;
            Assert.True(copy.ScreenByNamespace(Contract.CompanionPrefix).Modules[5]);
            Assert.Throws<ArgumentOutOfRangeException>(() => copy.ScreenZone(Contract.PitWallPrefix, "E"));
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
            // An article and a lower-case noun, as the canvas writes it, rather than the catalogue name.
            Assert.Equal("Zone B and zone C both show the relative.", clashes[0].Message());
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
            // The glance is moved off the track so that this reads about the two catalogues alone; that
            // it defaults to the track and would join in is the next test's business.
            settings.SetQuickGlance(Face, Contract.QuickGlanceValue(0, 2));
            Assert.Equal("Zone A and zone B both show the track.", FacePageClash.Warning(settings.Face(Face)));
        }

        [Fact]
        public void A_page_that_does_not_read_after_an_article_keeps_its_catalogue_name()
        {
            // "the gear, speed, revs" is not a sentence, and no short noun for that page exists to
            // invent, so it is spelled the way the panel's own drop-down spells it.
            Assert.Equal("Gear, speed, revs", FacePageClash.DisplayName("Gear, speed, revs"));
            Assert.Equal("the relative", FacePageClash.DisplayName("Relative"));
            Assert.Equal("the lap times", FacePageClash.DisplayName("Lap times"));
            // No two zones can land on that page today -- only zone A's catalogue holds it -- so the
            // sentence is pinned on the clash itself rather than on a face that cannot be arranged.
            Assert.Equal(
                "Zone A and zone B both show Gear, speed, revs.",
                new FacePageClash("gearSpeedRevs", "Gear, speed, revs", new[] { "A", "B" }).Message());
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
            Assert.Equal("Zone B, zone C and zone D all show the fuel.", clashes[0].Message());
        }

        [Fact]
        public void A_face_with_nothing_in_common_warns_about_nothing()
        {
            // The defaults included: zone C opens on the relative and the glance is set to the track,
            // so the fifth participant agrees with nobody.
            Assert.Equal(string.Empty, FacePageClash.Warning(new OpenDashSettings().Face(Face)));
        }

        [Fact]
        public void The_quick_glance_is_the_fifth_participant_and_is_said_by_name()
        {
            // The glance is compared against each zone's start page by page id, as the zones are
            // compared with each other, and the line is shown rather than blocking the setting.
            var settings = new OpenDashSettings();
            settings.SetQuickGlance(Face, Contract.QuickGlanceValue(2, 12));
            settings.SetFaceZoneStart(Face, "C", 12);
            var clashes = settings.FaceClashes(Face);
            Assert.Single(clashes);
            Assert.Equal("track", clashes[0].PageId);
            Assert.Equal(new[] { "C" }, clashes[0].Zones);
            Assert.True(clashes[0].Glance);
            Assert.Equal("Zone C and the quick glance both show the track.", clashes[0].Message());
            // Said, not prevented: the zone keeps the page and so does the glance.
            Assert.Equal(12, settings.FaceZoneStart(Face, "C"));
            Assert.Equal(Contract.QuickGlanceValue(2, 12), settings.QuickGlanceOf(Face));

            // By id and not by number: zone A's page 3 and module 13 are one drawing, so a glance held
            // on zone A's track page clashes with a zone B showing module 13.
            var across = new OpenDashSettings();
            across.SetQuickGlance(Face, Contract.QuickGlanceValue(0, 3));
            across.SetFaceZoneStart(Face, "B", 12);
            Assert.Equal("Zone B and the quick glance both show the track.", FacePageClash.Warning(across.Face(Face)));

            // And three participants read as a list, the glance last.
            var three = new OpenDashSettings();
            three.SetQuickGlance(Face, Contract.QuickGlanceValue(2, 12));
            three.SetFaceZoneStart(Face, "B", 12);
            three.SetFaceZoneStart(Face, "C", 12);
            Assert.Equal("Zone B, zone C and the quick glance all show the track.", FacePageClash.Warning(three.Face(Face)));
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
            // forward would put a screen on the rig whose package can never be written.
            var settings = new OpenDashSettings();
            settings.Faces["Face1x1"] = new FaceSettings();
            settings.Faces["Face1920x480"] = new FaceSettings { Starts = new[] { 1, 2, 3, 0 } };
            settings.Normalise();
            Assert.Null(settings.ScreenByNamespace("Face1x1"));
            // The one that does ship is carried across with what the driver had set on it.
            var kept = settings.ScreenByNamespace("Face1920x480");
            Assert.NotNull(kept);
            Assert.Equal(2, kept.Face.Start("B"));
            // And the dictionary it came from is spent, so nothing keeps a second copy of the zones.
            Assert.Empty(settings.Faces);
        }

        [Fact]
        public void A_rig_of_two_faces_a_pit_wall_and_a_companion_round_trips()
        {
            // SimHub persists the settings with Json.NET; what is asserted here is the shape rather than
            // that serialiser, namely that every part of a rig is a plain settable member and comes back
            // as it went in. Two faces of one size is the case that matters: the two carry different
            // zones, which before ADR 0017 was not expressible at all.
            var settings = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            settings.Normalise();
            var entry = new PackageEntry { Package = "p", Folder = "openDash 1280x480", Kind = Contract.KindFace, Width = 1280, Height = 480 };
            var main = settings.AddScreen(entry, "Main dash");
            var rim = settings.AddScreen(entry, "Rim");
            settings.Rig.Add(Screen(Contract.KindCompanion, 850, 480));
            settings.Rig.Add(Screen(Contract.KindPitWall, 1920, 1080));
            settings.Normalise();

            main.Face.SetStart("B", 4);
            rim.Face.SetStart("B", 9);
            rim.Face.SetBarField("Left1", 3);
            settings.ScreenByNamespace(Contract.CompanionPrefix).Modules[5] = true;
            settings.ScreenByNamespace(Contract.PitWallPrefix).Zones[0] = 3;
            settings.ScreenByNamespace(Contract.PitWallPrefix).WebViewUrl = "https://garage61.net";

            var read = JsonSerializer.Deserialize<OpenDashSettings>(JsonSerializer.Serialize(settings));
            read.Normalise();

            Assert.Equal(4, read.ScreenFace("Face1280x480").Start("B"));
            Assert.Equal(9, read.ScreenFace("Rim").Start("B"));
            Assert.Equal(3, read.ScreenFace("Rim").BarField("Left1"));
            Assert.True(read.ScreenByNamespace(Contract.CompanionPrefix).Modules[5]);
            Assert.Equal(3, read.ScreenZone(Contract.PitWallPrefix, "A"));
            Assert.Equal("https://garage61.net", read.ScreenWebViewUrl(Contract.PitWallPrefix));
            // The names and the folders survive, which is what makes the two 1280x480 screens tellable
            // apart in SimHub's own dashboard list.
            Assert.Equal("Main dash", read.ScreenByNamespace("Face1280x480").Name);
            Assert.Equal("openDash Rim", read.ScreenByNamespace("Rim").Folder);
            Assert.Equal(4, read.RigScreens().Count);
        }

        [Fact]
        public void A_screen_that_has_just_been_added_starts_from_the_defaults()
        {
            // What happens when a screen is added: the rig holds it before anything has configured it,
            // and it has to start from the defaults rather than from nothing.
            var settings = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            settings.Normalise();
            var rim = Contract.FaceSizes[3];
            var entry = new PackageEntry { Package = "p", Folder = "openDash 850x480", Kind = Contract.KindFace, Width = rim.Width, Height = rim.Height };

            var added = settings.AddScreen(entry, null);
            settings.Normalise();
            Assert.Equal(Contract.FacePrefix(rim), added.Namespace);
            Assert.Equal(Contract.DefaultFaceZones(), added.Face.Zones);
            Assert.Equal(Contract.DefaultFaceZoneMasks(), added.Face.Masks);
            Assert.Equal(Contract.DefaultQuickGlance, added.Face.QuickGlance);
            // Unnamed, it takes its size, which is what the panel prefills the box with.
            Assert.Equal("850 × 480", added.Name);
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
        public void Normalise_repairs_a_strip_setting_it_does_not_recognise()
        {
            // A profile reads both through isnull() with its own default, so a value that reaches the
            // strip unrecognised lights nothing at all: every gate inside compares against a spelling.
            var settings = new OpenDashSettings { LedCentre = "sparkles", LedRpmStyle = null };
            settings.Normalise();
            Assert.Equal(Contract.DefaultLedCentre, settings.LedCentre);
            Assert.Equal(Contract.DefaultLedRpmStyle, settings.LedRpmStyle);

            // Canonical casing, the way every other choice is normalised.
            var typed = new OpenDashSettings { LedCentre = " ThrottleBrake ", LedRpmStyle = "MEETINMIDDLE" };
            typed.Normalise();
            Assert.Equal("throttleBrake", typed.LedCentre);
            Assert.Equal("meetInMiddle", typed.LedRpmStyle);
        }

        [Fact]
        public void A_settings_file_carrying_only_the_old_low_fuel_name_keeps_its_threshold()
        {
            // LightsLowFuelLaps supersedes FlagBoxLowFuelLaps, and the plugin publishes the one number
            // under both names rather than renaming the field: a settings file on disk is keyed by the
            // name that shipped, and a driver who set five laps against rc.2 keeps five.
            var json = "{\"FlagBoxLowFuelLaps\":5}";
            var settings = JsonSerializer.Deserialize<OpenDashSettings>(json);
            settings.Normalise();
            Assert.Equal(5, settings.FlagBoxLowFuelLaps);
            var declared = settings.DeclaredProperties().ToList();
            Assert.Contains(Contract.LightsLowFuelLaps, declared);
            Assert.Contains(Contract.FlagBoxLowFuelLaps, declared);
        }

        [Fact]
        public void A_settings_file_carrying_the_retired_strip_centre_is_migrated_into_rpm()
        {
            // rpmOnly lit the same centre as rpm and only left the sides dark, so it was retired into
            // rpm. A rig upgraded with it still stored would otherwise match no conditional group in
            // the profile, and its strip centre would go dark.
            var json = "{\"LedCentre\":\"rpmOnly\"}";
            var settings = JsonSerializer.Deserialize<OpenDashSettings>(json);
            settings.Normalise();
            Assert.Equal("rpm", settings.LedCentre);
            Assert.Contains(settings.LedCentre, Contract.LedCentres);
        }

        [Fact]
        public void CopyFrom_carries_the_lights()
        {
            // It carried none of them before the strips were added: the panel's copy handed back a rig
            // with the brightness at 100 and matrix 1 back on flags, whatever the driver had set.
            var source = new OpenDashSettings
            {
                LightsBrightness = 60,
                LightsNightBrightness = 10,
                LightsNightMode = true,
                FlagBoxCriticalOnly = true,
                FlagBoxGear = false,
                FlagBoxLowFuelLaps = 5,
                FlagBoxOilTemp = 130,
                FlagBoxWaterTemp = 115,
                LedCentre = "fuel",
                LedRpmStyle = "f1",
                LedFlagAnimation = false,
            };
            source.Normalise();
            source.FlagBoxRest[1] = "gear";
            source.FlagBoxSide[0] = "left";
            source.FlagBoxFlags[3] = true;

            var copy = new OpenDashSettings();
            copy.CopyFrom(source);
            Assert.Equal(60, copy.LightsBrightness);
            Assert.Equal(10, copy.LightsNightBrightness);
            Assert.True(copy.LightsNightMode);
            Assert.True(copy.FlagBoxCriticalOnly);
            Assert.False(copy.FlagBoxGear);
            Assert.Equal(5, copy.FlagBoxLowFuelLaps);
            Assert.Equal(130, copy.FlagBoxOilTemp);
            Assert.Equal(115, copy.FlagBoxWaterTemp);
            Assert.Equal("gear", copy.MatrixRest(2));
            Assert.Equal("left", copy.MatrixSide(1));
            Assert.True(copy.MatrixFlags(4));
            Assert.Equal("fuel", copy.LedCentre);
            Assert.Equal("f1", copy.LedRpmStyle);
            Assert.False(copy.LedFlagAnimation);

            // A clone, not the same array: editing one settings object must not edit the other.
            copy.FlagBoxRest[1] = "dark";
            Assert.Equal("gear", source.MatrixRest(2));
        }

        [Fact]
        public void A_settings_file_written_before_the_strips_existed_comes_back_with_them_defaulted()
        {
            // The strips shipped their profiles before the plugin had either name, so every file
            // written until now is one of these.
            var json = "{\"LightsBrightness\":80}";
            var settings = JsonSerializer.Deserialize<OpenDashSettings>(json);
            settings.Normalise();
            Assert.Equal(80, settings.LightsBrightness);
            Assert.Equal(Contract.DefaultLedCentre, settings.LedCentre);
            Assert.Equal(Contract.DefaultLedRpmStyle, settings.LedRpmStyle);
            Assert.True(settings.LedFlagAnimation);
        }

        [Fact]
        public void The_defaults_are_a_working_single_box_setup()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();

            Assert.Equal("gear", settings.MatrixRest(1));
            Assert.True(settings.MatrixFlags(1));
            Assert.True(settings.MatrixPit(1));
            Assert.True(settings.MatrixSpotter(1));
            Assert.True(settings.MatrixWarnings(1));
            foreach (var matrix in new[] { 2, 3, 4 })
            {
                Assert.Equal("dark", settings.MatrixRest(matrix));
                Assert.False(settings.MatrixFlags(matrix));
                Assert.False(settings.MatrixPit(matrix));
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
            // Eight face sizes times twenty-two properties is what the plugin used to attach whatever
            // the rig was. What it attaches now is the four modes, the twelve slots and the rev bar,
            // which every screen shares, and one group per screen the rig holds.
            const int perFace = 4 + 4 + 4 + 4 + 4 + 1 + 1;
            var shared = Contract.SharedPropertyNames().Count();
            Assert.Equal(17, shared);
            // The lights are declared whatever the rig is: openDash does not install the flag box
            // profile (ADR 0013), so there is nothing to detect, and it is a fixed handful of names
            // rather than the hundred and thirty-six that made the screens worth narrowing.
            var lights = Contract.LightsPropertyNames().Count();

            // An empty rig is a new install, and declares nothing of any screen's.
            var settings = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            settings.Normalise();
            Assert.Equal(Contract.SharedPropertyNames().Concat(Contract.LightsPropertyNames()), settings.DeclaredProperties());

            settings.Rig.Add(Screen(Contract.KindFace, Face.Width, Face.Height));
            settings.Rig.Add(Screen(Contract.KindFace, Contract.FaceSizes[3].Width, Contract.FaceSizes[3].Height));
            settings.Rig.Add(Screen(Contract.KindCompanion, 850, 480));
            settings.Rig.Add(Screen(Contract.KindPitWall, 1920, 1080));
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

            settings.RemoveScreen("Face850x480");
            settings.Normalise();
            Assert.Equal(shared + perFace + Modules.Count + 6 + lights, settings.DeclaredProperties().Count());
        }

        /// <summary>A screen on the stock namespace for its kind and size, as the first one at a size is.</summary>
        private static ScreenInstance Screen(string kind, int width, int height)
        {
            var screen = new ScreenInstance { Kind = kind, Width = width, Height = height };
            screen.Namespace = screen.StockNamespace;
            screen.Normalise();
            return screen;
        }

        // --- ADR 0017: a screen is an instance -------------------------------------------------

        [Fact]
        public void Two_faces_of_one_size_are_configured_apart()
        {
            // The whole point of ADR 0017. Before it, both of these were Faces["Face1280x480"] and
            // cycling a zone on one moved the same zone on the other.
            var settings = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            settings.Normalise();
            var entry = new PackageEntry { Package = "p", Folder = "openDash 1280x480", Kind = Contract.KindFace, Width = 1280, Height = 480 };

            var main = settings.AddScreen(entry, "Main dash");
            var rim = settings.AddScreen(entry, "Rim");
            settings.Normalise();

            // The first takes the stock namespace and the stock folder, so its package is the embedded
            // one byte for byte and nothing about today's rigs changes.
            Assert.Equal("Face1280x480", main.Namespace);
            Assert.Equal("openDash 1280x480", main.Folder);
            Assert.True(main.IsStock);

            // The second gets its own of both.
            Assert.Equal("Rim", rim.Namespace);
            Assert.Equal("openDash Rim", rim.Folder);
            Assert.False(rim.IsStock);

            main.Face.SetStart("B", 3);
            rim.Face.SetStart("B", 7);
            Assert.Equal(3, main.Face.Start("B"));
            Assert.Equal(7, rim.Face.Start("B"));

            var names = settings.DeclaredProperties().ToList();
            Assert.Contains("Face1280x480ZoneB", names);
            Assert.Contains("RimZoneB", names);
            Assert.Equal(names.Count, names.Distinct().Count());

            // And their wheel buttons are separate, which is what lets one face sit still while the
            // one in front of the driver cycles.
            Assert.Contains("RimCycleZoneB", rim.ActionNames());
            Assert.Contains("Face1280x480CycleZoneB", main.ActionNames());
        }

        /// <summary>
        /// The Install tab's rows and the rig are one list, and ADR 0017 is what could set them at odds.
        /// </summary>
        /// <remarks>
        /// A second screen made from a package takes a folder of its own, so a row keyed on the folder
        /// would lose it; the rim face and the companion are both 850 × 480, so a row keyed on the size
        /// would hand one of them the other's screens. The package a screen was made from survives both.
        /// A screen from a settings file written before that was recorded is the case the fallback to the
        /// kind and the size exists for, and without it such a screen would stop counting anywhere.
        /// </remarks>
        [Fact]
        public void A_second_screen_at_one_size_counts_against_its_own_package()
        {
            var settings = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            settings.Normalise();
            var rim = new PackageEntry { Package = "rim", Folder = "openDash 850x480", Kind = Contract.KindFace, Width = 850, Height = 480 };
            var phone = new PackageEntry { Package = "companion", Folder = "openDash Companion", Kind = Contract.KindCompanion, Width = 850, Height = 480 };
            Assert.Equal("Rim", rim.DisplayName);
            Assert.Equal("Phone", phone.DisplayName);

            var first = settings.AddScreen(rim, "Main dash");
            var second = settings.AddScreen(rim, "Spare");
            settings.AddScreen(phone, "Phone");
            settings.Normalise();

            Assert.NotEqual(first.Folder, second.Folder);
            Assert.Equal(2, PanelPackageRow.Uses(rim, settings.RigScreens()));
            Assert.Equal(1, PanelPackageRow.Uses(phone, settings.RigScreens()));

            settings.Rig.Add(new ScreenInstance { Namespace = "Legacy", Name = "Legacy", Kind = Contract.KindFace, Width = 850, Height = 480 });
            settings.Normalise();
            Assert.Equal(3, PanelPackageRow.Uses(rim, settings.RigScreens()));
            Assert.Equal(1, PanelPackageRow.Uses(phone, settings.RigScreens()));
        }

        [Fact]
        public void A_namespace_is_frozen_at_creation_and_a_rename_is_a_label()
        {
            // ADR 0017: a property name is a public interface under ADR 0003, so a rename that
            // re-pointed one would break whatever had been bound to it with no diagnostic.
            var settings = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            settings.Normalise();
            var entry = new PackageEntry { Package = "p", Folder = "openDash 1280x480", Kind = Contract.KindFace, Width = 1280, Height = 480 };
            settings.AddScreen(entry, "First");
            var screen = settings.AddScreen(entry, "Main dash");
            Assert.Equal("MainDash", screen.Namespace);
            Assert.Equal("openDash Main dash", screen.Folder);

            screen.Name = "Rim";
            settings.Normalise();
            Assert.Equal("MainDash", screen.Namespace);
            Assert.Equal("openDash Main dash", screen.Folder);
            Assert.Equal("Rim", screen.Name);
        }

        [Theory]
        [InlineData("Main dash", "MainDash")]
        [InlineData("rim", "rim")]
        [InlineData("Pit  wall  2", "PitWall2")]
        [InlineData("1920 dash", "dash")]
        [InlineData("écran", "cran")]
        public void A_namespace_is_the_letters_and_digits_of_the_name(string name, string expected)
        {
            // Letters and digits only: contract.ts records that a second dot inside a property name is
            // unverified, and a user-typed name is not the place to find out. A leading run of digits
            // goes because a namespace that begins with one reads as a number wherever it is parsed.
            Assert.Equal(expected, Contract.Slug(name));
        }

        [Fact]
        public void A_name_that_slugs_to_nothing_still_gets_a_screen()
        {
            // A name written entirely in a script the slug drops must not be a refusal to add a screen.
            Assert.Equal("Screen", PackageCatalogue.UniqueNamespace("日本語", new HashSet<string>()));
            Assert.Equal("Screen2", PackageCatalogue.UniqueNamespace("日本語", new HashSet<string> { "Screen" }));
        }

        [Fact]
        public void A_namespace_never_collides_with_a_stock_one()
        {
            // Naming a second 1280x480 screen "Face1280x480" would make it read the first one's
            // properties, which is the exact collision the record exists to stop.
            var taken = new HashSet<string> { "Face1280x480" };
            Assert.Equal("Face1280x4802", PackageCatalogue.UniqueNamespace("Face1280x480", taken));
            // And a reserved one is taken even when the rig does not hold it yet.
            Assert.Equal("PitWall2", PackageCatalogue.UniqueNamespace("Pit wall", new HashSet<string>()));
        }

        [Fact]
        public void A_new_install_has_an_empty_rig_and_an_upgrade_keeps_what_is_installed()
        {
            // A settings file that has never installed anything is a new install, and its empty rig is
            // the state the panel teaches from (XOR-34). Nothing has to be dismissed.
            var fresh = new OpenDashSettings();
            fresh.Normalise();
            Assert.Empty(fresh.RigScreens());

            // A file that has installed dashboards gets one screen per folder, carrying the settings
            // that were keyed by prefix before the rig existed.
            var upgraded = new OpenDashSettings
            {
                FolderFingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "openDash 1280x480", "a" },
                    { "openDash Companion", "b" },
                    { "openDash Pit wall", "c" },
                },
                Faces = new Dictionary<string, FaceSettings>(StringComparer.Ordinal)
                {
                    { "Face1280x480", new FaceSettings { Starts = new[] { 1, 2, 3, 4 } } },
                },
                WideZone = 3,
            };
            upgraded.Normalise();
            Assert.Equal(3, upgraded.RigScreens().Count);
            var face = upgraded.ScreenByNamespace("Face1280x480");
            Assert.NotNull(face);
            Assert.Equal(Contract.KindFace, face.Kind);
            Assert.Equal(1280, face.Width);
            // The zones the user had set are the zones they still have.
            Assert.Equal(2, face.Face.Start("B"));
            Assert.Equal(3, upgraded.ScreenByNamespace("PitWall").WideZone);
            Assert.Equal("Companion", upgraded.ScreenByNamespace("Companion").Name);
        }

        [Fact]
        public void A_second_pit_wall_gets_names_of_its_own_for_the_unprefixed_properties()
        {
            // WebViewUrl carries no prefix at all -- it was named before the idiom and ADR 0003 does not
            // allow renaming a published property -- so only the stock pit wall keeps that spelling.
            var stock = Contract.ScreenPropertyNames(Contract.KindPitWall, Contract.PitWallPrefix).ToList();
            Assert.Contains("WebViewUrl", stock);
            Assert.Contains("PitWallWide", stock);

            var second = Contract.ScreenPropertyNames(Contract.KindPitWall, "Garage").ToList();
            Assert.Contains("GarageWebViewUrl", second);
            Assert.Contains("GarageWide", second);
            Assert.DoesNotContain("WebViewUrl", second);
            Assert.Empty(stock.Intersect(second));
        }

        [Fact]
        public void A_rig_of_one_screen_per_size_declares_exactly_what_it_used_to()
        {
            // The load-bearing compatibility claim in ADR 0017, and the reason
            // packages/dash/test/declared-properties.txt does not change: a rig holding the stock
            // screen of every kind declares the same names, in the same order, as the prefix list did.
            var settings = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            foreach (var face in Contract.FaceSizes) settings.Rig.Add(Screen(Contract.KindFace, face.Width, face.Height));
            settings.Rig.Add(Screen(Contract.KindCompanion, 850, 480));
            settings.Rig.Add(Screen(Contract.KindPitWall, 1920, 1080));
            settings.Normalise();
            Assert.Equal(Contract.PropertyNames(), settings.DeclaredProperties());
        }

        [Fact]
        public void A_screen_carries_only_the_settings_of_its_own_kind()
        {
            // A settings file that carried a face's zones on a pit wall -- which only a hand-edit could
            // produce -- must not attach a face's properties to it.
            var confused = new ScreenInstance
            {
                Kind = Contract.KindPitWall,
                Width = 1920,
                Height = 1080,
                Face = new FaceSettings(),
                Modules = Contract.DefaultModules(),
            };
            confused.Normalise();
            Assert.Null(confused.Face);
            Assert.Null(confused.Modules);
            Assert.Null(confused.FlagFormat);
            Assert.NotNull(confused.Zones);
        }

        [Fact]
        public void The_flag_format_is_the_band_by_default_and_is_set_per_screen()
        {
            // Per screen like the zones: the face in the driver's peripheral vision is the one a
            // full-face flag is for, and the one they read directly should keep its band.
            var settings = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            settings.Rig.Add(Screen(Contract.KindFace, Face.Width, Face.Height));
            settings.Rig.Add(Screen(Contract.KindFace, Contract.FaceSizes[3].Width, Contract.FaceSizes[3].Height));
            settings.Normalise();
            Assert.Equal("band", settings.ScreenFlagFormat("Face1920x480"));
            Assert.Equal("band", settings.ScreenFlagFormat("Face850x480"));

            settings.ScreenOf("Face850x480").FlagFormat = "full";
            settings.Normalise();
            Assert.Equal("band", settings.ScreenFlagFormat("Face1920x480"));
            Assert.Equal("full", settings.ScreenFlagFormat("Face850x480"));

            // A spelling the panel never wrote falls back rather than reaching the face as itself, and
            // a screen the rig no longer holds reads the default rather than throwing on SimHub's
            // data thread, the way every other per-screen read does.
            settings.ScreenOf("Face850x480").FlagFormat = "enormous";
            settings.Normalise();
            Assert.Equal("band", settings.ScreenFlagFormat("Face850x480"));
            Assert.Equal("band", settings.ScreenFlagFormat("Face1280x720"));
        }

        [Fact]
        public void The_flag_format_survives_a_save()
        {
            var settings = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            settings.Rig.Add(Screen(Contract.KindFace, Face.Width, Face.Height));
            settings.ScreenOf("Face1920x480").FlagFormat = "full";
            settings.Normalise();

            var read = JsonSerializer.Deserialize<OpenDashSettings>(JsonSerializer.Serialize(settings));
            read.Normalise();
            Assert.Equal("full", read.ScreenFlagFormat("Face1920x480"));

            var copy = new OpenDashSettings();
            copy.CopyFrom(settings);
            Assert.Equal("full", copy.ScreenFlagFormat("Face1920x480"));
        }

        [Fact]
        public void A_companion_opens_on_its_start_module_and_its_button_skips_what_is_off()
        {
            var settings = new OpenDashSettings { Rig = new List<ScreenInstance>() };
            settings.Rig.Add(Screen(Contract.KindCompanion, 850, 480));
            settings.Normalise();
            var companion = settings.ScreenOf("Companion");
            Assert.Equal(Contract.DefaultCompanionStart, companion.CompanionStart);
            Assert.Equal(Contract.DefaultCompanionQuickGlance, companion.CompanionQuickGlance);

            // Modules 6, 20 and 21 are off by default -- iRacing carries none of their data -- so the
            // button steps 4, then 6 to the next one that is on, which is 7.
            companion.CompanionPage = 3;
            Assert.Equal(4, settings.CycleScreenModule("Companion"));
            companion.CompanionPage = 4;
            Assert.Equal(6, settings.CycleScreenModule("Companion"));
            // And it wraps over the two that are off at the end of the catalogue.
            companion.CompanionPage = 18;
            Assert.Equal(0, settings.CycleScreenModule("Companion"));

            // A glance shows a module the rotation has turned off, because a glance is a thing the
            // driver asked for by holding a button, and releasing puts the page back.
            companion.CompanionPage = 2;
            companion.CompanionQuickGlance = 5;
            settings.BeginScreenGlance("Companion");
            Assert.Equal(5, companion.CompanionPage);
            settings.BeginScreenGlance("Companion");
            settings.EndScreenGlance("Companion");
            Assert.Equal(2, companion.CompanionPage);

            // Init puts it back on the module it opens on, which is what that setting means.
            companion.CompanionStart = 7;
            companion.CompanionPage = 15;
            settings.OpenOnStartPages();
            Assert.Equal(7, companion.CompanionPage);

            // A screen the rig no longer holds reads its defaults rather than throwing on SimHub's
            // own thread, the way every other per-screen read does.
            Assert.Equal(Contract.DefaultCompanionStart, settings.ScreenCompanionStart("Gone"));
            Assert.Equal(Contract.DefaultCompanionQuickGlance, settings.ScreenCompanionQuickGlance("Gone"));
            Assert.Equal(Contract.DefaultCompanionStart, settings.CycleScreenModule("Gone"));
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
