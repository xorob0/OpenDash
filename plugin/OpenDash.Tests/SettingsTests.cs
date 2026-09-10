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
            Assert.Equal(Enumerable.Range(0, 12).ToArray(), settings.Slots);
        }

        [Fact]
        public void Normalise_clamps_card_numbers_to_the_slot_default()
        {
            var settings = new OpenDashSettings { Slots = new[] { 99, -1, 5 } };
            settings.Normalise();
            Assert.Equal(new[] { 0, 1, 5, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, settings.Slots);
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
            Assert.Equal(1, settings.Slot(2));
            Assert.Equal(11, settings.Slot(12));
            settings.SetSlot(12, 7);
            Assert.Equal(7, settings.Slot(12));
            settings.SetSlot(3, 42);
            Assert.Equal(2, settings.Slot(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetSlot(13, 0));
        }

        [Fact]
        public void Duplicates_are_reported_as_on_the_canvas()
        {
            var slots = Contract.DefaultSlots();
            slots[8] = 7; // slot 9 shows Fuel laps, like slot 8
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
            slots[3] = 0; // slot 4 no longer shows Delta
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
    }
}
