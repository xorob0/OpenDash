// PanelDataTabTests.cs: the race data rows' copy, which was the Data tab's and is the Settings page's.
//
// The Position row carries two sentences the canvas does not, which is a decision and not an oversight; a
// later reader comparing the tab against the canvas would otherwise delete them as a difference. Pinning
// them here is what makes the deletion fail rather than pass quietly.
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelDataTabTests
    {
        [Fact]
        public void The_section_says_what_the_canvas_says()
        {
            // The heading is the whole of what the section has to say, so it carries no caption
            // restating it; docs/design/voice.md is the rule. The Settings page draws this constant and
            // search finds the section by it.
            Assert.Equal("Race data", PanelDataTab.SectionTitle);
            Assert.Null(PanelDataTab.SectionCaption);
            var page = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Settings.cs"));
            Assert.Contains("PageSection(PanelDataTab.SectionTitle,", page);
            Assert.Contains(PanelSettings.Search, entry => entry.Label == PanelDataTab.SectionTitle && entry.Route.Anchor == PanelSettings.AnchorRaceData);
        }

        /// <remarks>
        /// Two settings have shipped declared, mirrored and attached with no way to reach them: the
        /// flag format, which made a whole format unreachable, and the blue flag detail. Both were
        /// found by reading the code rather than by a red suite, because declaring a property and
        /// drawing a control for it are two edits and nothing held them together. This is the thing
        /// that holds them together.
        /// </remarks>
        [Fact]
        public void Every_rig_setting_can_be_reached_from_the_panel()
        {
            var sources = string.Concat(RepoPaths.SettingsControlCode());
            var unreachable = Contract.SharedPropertyNames()
                // The twelve slots are the card face's own and sit on that screen's pane, which writes
                // them through Contract.SlotProperty rather than by name. ShiftLights has no control
                // because RevBar supersedes it and SetRevBar writes both; the two cannot disagree. The
                // idle screen's two are published rather than chosen, and the one setting behind them is
                // the update check's switch on the Updates page (#83). The class best is published from
                // SimHub's own frame and nobody sets it.
                .Where(name => !name.StartsWith("Slot", StringComparison.Ordinal) && name != Contract.ShiftLights)
                .Where(name => name != Contract.UpdateAvailable && name != Contract.UpdateVersion && name != Contract.ClassBestLap)
                // A theme's own settings are drawn from their declaration, which names no theme, so they are
                // reached through Contract.ThemeSettings rather than by name; the two lines after this hold that. #715.
                .Where(name => !Contract.ThemeSettings.Any(setting => setting.Property == name))
                .Where(name => !sources.Contains("Settings." + name) && !sources.Contains("Set" + name))
                .ToArray();
            Assert.Equal(Array.Empty<string>(), unreachable);
            Assert.Contains("Contract.ThemeSettingsOf(", sources);
            Assert.Contains("Settings.SetThemeSetting(", sources);
        }

        [Fact]
        public void The_blue_flag_row_offers_the_three_the_contract_declares()
        {
            Assert.Equal(new[] { "none", "class", "positionClass" }, Contract.BlueFlagDetails);
            Assert.Equal("Blue flag detail", PanelDataTab.BlueFlagTitle);
            // The three values are the control's to show; the caption says only what the row is about.
            Assert.Equal("What shows next to a blue flag.", PanelDataTab.BlueFlagCaption);
            Assert.Equal(new[] { "Nothing", "Class", "Position and class" }, PanelDataTab.BlueFlagLabels);
            Assert.Equal(Contract.BlueFlagDetails.Length, PanelDataTab.BlueFlagLabels.Length);
            Assert.Contains("BuildSegmented(Contract.BlueFlagDetails, PanelDataTab.BlueFlagLabels,", string.Concat(RepoPaths.SettingsControlCode()));
        }

        /// <remarks>
        /// "Class" and not the artboard's "In class": the pinned caption begins with the word, and a value in
        /// a chooser is a name rather than a fragment (docs/design/voice.md).
        /// </remarks>
        [Fact]
        public void The_position_and_session_rows_name_every_value_the_contract_declares()
        {
            Assert.Equal("Position", PanelDataTab.PositionTitle);
            Assert.Equal("Session progress", PanelDataTab.SessionTitle);
            Assert.Equal("Auto picks laps or time to suit the session.", PanelDataTab.SessionCaption);
            Assert.Equal(new[] { "Overall", "Class" }, PanelDataTab.PositionLabels);
            Assert.Equal(Contract.PositionModes.Length, PanelDataTab.PositionLabels.Length);
            Assert.StartsWith(PanelDataTab.PositionLabels[1], PanelDataTab.PositionCaption, StringComparison.Ordinal);
            Assert.Equal(new[] { "Auto", "Laps", "Time" }, PanelDataTab.SessionLabels);
            Assert.Equal(Contract.SessionProgressModes.Length, PanelDataTab.SessionLabels.Length);
            var source = string.Concat(RepoPaths.SettingsControlCode());
            Assert.Contains("BuildSegmented(Contract.PositionModes, PanelDataTab.PositionLabels,", source);
            Assert.Contains("BuildSegmented(Contract.SessionProgressModes, PanelDataTab.SessionLabels,", source);
        }

        /// <remarks>
        /// The four values are worked examples of one name rather than descriptions of a format, which
        /// is the one thing about this row a later reader is likely to "tidy" into "Initial and surname"
        /// and its three siblings. docs/design/voice.md is why they are examples: a value in a chooser is
        /// read without its label and beside the values next to it, and `L. Byrne` next to `B. Liam` is
        /// the answer where two prose fragments are a puzzle.
        /// </remarks>
        [Fact]
        public void The_driver_name_row_shows_each_format_by_example()
        {
            Assert.Equal("Driver names", PanelDataTab.DriverNameTitle);
            Assert.Null(PanelDataTab.DriverNameCaption);
            Assert.Equal(new[] { "Liam Byrne", "L. Byrne", "B. Liam", "Byrne Liam" }, PanelDataTab.DriverNameLabels);
            // One label per format, in the contract's own order, or the control offers a value it cannot
            // name or names one the contract does not have.
            Assert.Equal(Contract.DriverNameFormats.Length, PanelDataTab.DriverNameLabels.Length);
        }

        /// <remarks>
        /// The labels were an inline array in SettingsControl.Data.cs (now SettingsControl.Settings.cs), where nothing could hold them to
        /// the contract; the segmented control indexes them by value, so a third reference with two labels
        /// throws while the tab is drawn rather than in a test. #322 moved them here to be counted.
        /// </remarks>
        [Fact]
        public void The_delta_row_names_every_reference_the_contract_declares()
        {
            Assert.Equal("Delta reference", PanelDataTab.DeltaTitle);
            // Still true of three references: each of them is a lap.
            Assert.Equal("Which lap the delta compares against.", PanelDataTab.DeltaCaption);
            Assert.Equal(new[] { "Session best", "All-time best", "Last lap" }, PanelDataTab.DeltaLabels);
            Assert.Equal(Contract.DeltaReferences.Length, PanelDataTab.DeltaLabels.Length);
            var source = string.Concat(RepoPaths.SettingsControlCode());
            Assert.Contains("BuildSegmented(Contract.DeltaReferences, PanelDataTab.DeltaLabels,", source);
        }

        /// <remarks>
        /// The Settings artboard draws the row bare (#322); its caption is the build's, so its words are held
        /// here. The labels are words and not worked examples, as the artboard's are: a numeral on the panel
        /// is drawn in Barlow, and the canvas keeps numerals to Barlow Condensed.
        /// </remarks>
        [Fact]
        public void The_delta_precision_row_names_both_precisions_in_words()
        {
            Assert.Equal("Delta precision", PanelDataTab.DeltaPrecisionTitle);
            Assert.Equal("Thousandths for a hotlap, hundredths to read at a glance.", PanelDataTab.DeltaPrecisionCaption);
            Assert.Equal(new[] { "Hundredths", "Thousandths" }, PanelDataTab.DeltaPrecisionLabels);
            Assert.Equal(Contract.DeltaPrecisions.Length, PanelDataTab.DeltaPrecisionLabels.Length);
            Assert.DoesNotContain(PanelDataTab.DeltaPrecisionLabels, label => label.Any(char.IsDigit));
            var source = string.Concat(RepoPaths.SettingsControlCode());
            Assert.Contains("BuildSegmented(Contract.DeltaPrecisions, PanelDataTab.DeltaPrecisionLabels,", source);
            // Directly under the reference it qualifies.
            var reference = source.IndexOf("Ui.Row(PanelDataTab.DeltaTitle,", StringComparison.Ordinal);
            var precision = source.IndexOf("Ui.Row(PanelDataTab.DeltaPrecisionTitle,", StringComparison.Ordinal);
            Assert.True(reference >= 0 && precision > reference, "the precision row is not under the reference row");
            Assert.Equal(-1, source.IndexOf("Ui.Row(", reference + 1, precision - reference - 1, StringComparison.Ordinal));
        }

        [Fact]
        public void The_team_row_says_what_happens_to_a_car_with_no_team()
        {
            Assert.Equal("Team names", PanelDataTab.TeamNameTitle);
            // The fallback is the half a driver cannot guess: without it a column of teams that is half
            // empty on a sprint grid reads as a fault rather than as the setting doing what it says.
            Assert.Equal("Names the team instead of the driver, and keeps the driver where the sim has no team.", PanelDataTab.TeamNameCaption);
        }

        /// <remarks>
        /// Examples for the reason the driver names are: `14:32` beside `2:32 PM` is the answer, where
        /// "24-hour" beside "12-hour" asks a reader to picture both. The caption is the one thing the
        /// control cannot show, that the sim's clock follows the setting as well as the wall clock.
        /// </remarks>
        [Fact]
        public void The_clock_row_shows_each_format_by_example()
        {
            Assert.Equal("Clock", PanelDataTab.ClockTitle);
            Assert.Equal("The sim's time of day follows it too.", PanelDataTab.ClockCaption);
            Assert.Equal(new[] { "14:32", "2:32 PM" }, PanelDataTab.ClockLabels);
            Assert.Equal(Contract.ClockFormats.Length, PanelDataTab.ClockLabels.Length);
        }

        [Fact]
        public void The_position_row_keeps_the_sentences_the_canvas_does_not_carry()
        {
            // The canvas's own sentence, "Overall, or within your class", is what the segmented control
            // beside the row already says in two words. What the row keeps is what the canvas does not
            // carry and the control cannot show: that Class filters the lists as well as numbering them
            // since #212, and that a zone can ask for the same filter without the rig doing so. The
            // row used to promise an override instead, which is the direction the two settings do not
            // run in; pinning the words here is what makes a quiet return to that fail.
            Assert.Equal("Class also shows only your own class in lists. A zone can ask for that on its own.", PanelDataTab.PositionCaption);
            Assert.DoesNotContain("override", PanelDataTab.PositionCaption);
        }
    }
}
