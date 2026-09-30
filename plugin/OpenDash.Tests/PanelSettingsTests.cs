// PanelSettingsTests.cs: the Settings page's words and decisions, held to Settings.dc.html and to
// docs/design/voice.md -- its sections and their order, the "On this page" row, SimHub's units, the alert
// table, the lighting preview, the greyed rows and what search finds.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelSettingsTests
    {
        private static string Page()
        {
            return RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Settings.cs"));
        }

        [Fact]
        public void Settings_says_what_the_artboard_says()
        {
            Assert.Equal("Settings", PanelSettings.Title);
            Assert.Equal("Race data", PanelDataTab.SectionTitle);
            Assert.Equal("Flags", PanelSettings.FlagsTitle);
            Assert.Equal("Alerts", PanelSettings.AlertsTitle);
            Assert.Equal("Lighting", PanelSettings.LightingTitle);
            Assert.Equal("Appearance", PanelSettings.AppearanceTitle);
            Assert.Equal("Driver", PanelSettings.DriverTitle);
            Assert.Equal("Units", PanelSettings.UnitsTitle);
            Assert.Equal("Set in SimHub.", PanelSettings.UnitsCaption);
            Assert.Equal("Flags in the pit lane", PanelSettings.FlagsInPitLaneTitle);
            Assert.Equal("Low fuel", PanelSettings.LowFuelTitle);
            Assert.Equal("Oil temperature", PanelSettings.OilTempTitle);
            Assert.Equal("Water temperature", PanelSettings.WaterTempTitle);
            // Ruling 66 and the critic's answer for the table: the number is in SimHub's unit, and the default
            // is the empty box's placeholder rather than a sentence.
            Assert.Equal("In SimHub's unit.", PanelSettings.TemperatureCaption);
            Assert.Equal("Brightness", PanelSettings.BrightnessTitle);
            Assert.Equal("SimHub's device brightness applies on top.", PanelSettings.BrightnessCaption);
            Assert.Equal("Night brightness", PanelSettings.NightBrightnessTitle);
            // The night slider is drawn bare, as the artboard draws it: a caption would name it again.
            Assert.Contains("Ui.Row(PanelSettings.NightBrightnessTitle, null, nightBrightness)", Page());
            Assert.Equal("Night mode", PanelSettings.NightModeTitle);
            Assert.Equal("Night mode button", PanelSettings.NightModeButtonTitle);
            Assert.Equal("Preview", PanelSettings.PreviewTitle);
            Assert.Equal(new[] { "Day", "Night" }, PanelSettings.PreviewLabels);
            Assert.Equal(new[] { "day", "night" }, PanelSettings.PreviewValues);
        }

        /// <summary>The greyed rows' words, which say what the tickets will build: the artboard's, with the tyre
        /// display's "then Pressure" as a bare "Pressure", since the two buttons side by side say the order.
        /// The yellow flags keep "My sector only", which #504 names and is to settle.</summary>
        [Fact]
        public void The_greyed_rows_say_what_is_coming()
        {
            Assert.Equal(new[] { "Temperature", "Pressure" }, PanelSettings.TyreDisplayLabels);
            Assert.Equal(new[] { "Whole track", "My sector only" }, PanelSettings.YellowFlagLabels);
            Assert.Equal(new[] { "Dark", "Light", "OLED black", "High contrast" }, PanelSettings.ThemeLabels);
            Assert.Equal(new[] { "Standard", "Deuteranopia", "Protanopia", "Tritanopia" }, PanelSettings.ColourVisionLabels);
            Assert.Equal(new[] { "As designed", "Darker", "Lighter", "Warmer" }, PanelSettings.ColoursLabels);
            Assert.Equal("First name", PanelSettings.FirstNameHint);
            Assert.Equal("Surname", PanelSettings.SurnameHint);
            Assert.Equal("000", PanelSettings.RaceNumberHint);
            Assert.Equal("Choose PNG or JPEG…", PanelSettings.LogoButton);
            Assert.Equal("Choose image…", PanelSettings.IdleBackgroundButton);
            Assert.Equal("L", PanelSettings.FuelTargetUnitFallback);
        }

        [Fact]
        public void Every_caption_is_a_sentence()
        {
            foreach (var caption in new[] { PanelSettings.UnitsCaption, PanelSettings.TemperatureCaption, PanelSettings.BrightnessCaption })
            {
                Assert.EndsWith(".", caption, StringComparison.Ordinal);
            }
        }

        /// <summary>voice.md: no contractions and no question anywhere in the page's words, the alert rows'
        /// included, and every title, column and option in sentence case, where an acronym keeps its capitals.</summary>
        [Fact]
        public void The_words_follow_the_voice()
        {
            var words = typeof(PanelSettings).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(string))
                .Select(field => (string)field.GetValue(null))
                .Concat(typeof(PanelSettings).GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(field => field.FieldType == typeof(string[]))
                    .SelectMany(field => (string[])field.GetValue(null)))
                .Concat(PanelSettings.Alerts.SelectMany(alert => new[] { alert.Title, alert.Op, alert.Unit, alert.Example }))
                .Where(text => text != null)
                .ToList();
            Assert.DoesNotContain(words, text => Regex.IsMatch(text, @"n't|'re\b|'ll\b|'ve\b|'m\b|'d\b"));
            Assert.DoesNotContain(words, text => text.EndsWith("?", StringComparison.Ordinal));
            var labels = PanelSettings.SectionTitles
                .Concat(new[]
                {
                    PanelSettings.UnitsTitle, PanelSettings.FlagsInPitLaneTitle, PanelSettings.BrightnessTitle,
                    PanelSettings.NightBrightnessTitle, PanelSettings.NightModeTitle, PanelSettings.NightModeButtonTitle,
                    PanelSettings.PreviewTitle, PanelSettings.AlertColumn, PanelSettings.WhenColumn, PanelSettings.TryLabel,
                })
                .Concat(PanelSettings.Alerts.Select(alert => alert.Title))
                .Concat(PanelSettings.SurfaceColumns)
                .Concat(PanelSettings.PreviewLabels)
                .Concat(PanelSettings.TyreDisplayLabels)
                .Concat(PanelSettings.YellowFlagLabels)
                .Concat(PanelSettings.ThemeLabels)
                .Concat(PanelSettings.ColourVisionLabels)
                .Concat(PanelSettings.ColoursLabels)
                .Concat(new[] { PanelSettings.FirstNameHint, PanelSettings.SurnameHint, PanelSettings.LogoButton, PanelSettings.IdleBackgroundButton });
            foreach (var label in labels)
            {
                Assert.True(char.IsUpper(label[0]), label);
                foreach (var word in label.Split(' ').Skip(1))
                {
                    var acronym = Regex.IsMatch(word, @"^[A-Z]{2,}s?…?$");
                    Assert.True(acronym || word == word.ToLowerInvariant(), label);
                }
            }
        }

        /// <summary>The page's anchor ids, which search, Home's fix rows and the capture scripts route to: a
        /// renamed one sends each of them to the page's top, so every id is pinned, and a new one is added here.</summary>
        [Fact]
        public void Its_anchor_ids_are_pinned()
        {
            Assert.Equal(new[]
            {
                "AnchorAlerts = settings.alerts",
                "AnchorAppearance = settings.appearance",
                "AnchorDriver = settings.driver",
                "AnchorFlags = settings.flags",
                "AnchorLighting = settings.lighting",
                "AnchorRaceData = settings.race-data",
            }, AnchorTable.Of(typeof(PanelSettings)));
        }

        [Fact]
        public void The_sections_come_in_the_artboards_order()
        {
            Assert.Equal(new[] { "Race data", "Flags", "Alerts", "Lighting", "Appearance", "Driver" }, PanelSettings.SectionTitles);
            Assert.Equal(new[] { PanelSettings.AnchorRaceData, PanelSettings.AnchorFlags, PanelSettings.AnchorAlerts, PanelSettings.AnchorLighting, PanelSettings.AnchorAppearance, PanelSettings.AnchorDriver }, PanelSettings.SectionAnchors);
            // The page draws them in that order, each under its own anchor.
            var page = Page();
            var at = PanelSettings.SectionAnchors.Select(anchor => page.IndexOf("PanelSettings." + AnchorName(anchor) + ")", StringComparison.Ordinal)).ToList();
            Assert.All(at, index => Assert.True(index >= 0));
            Assert.Equal(at.OrderBy(i => i), at);
        }

        private static string AnchorName(string anchor)
        {
            return typeof(PanelSettings).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Single(field => field.IsLiteral && field.Name.StartsWith("Anchor", StringComparison.Ordinal) && (string)field.GetValue(null) == anchor).Name;
        }

        /// <summary>The artboard's h2 on every section: 19 px with 12 under it, and the page's 28 between.</summary>
        [Fact]
        public void Every_heading_is_the_artboards()
        {
            var page = Page();
            foreach (var title in new[] { "PanelDataTab.SectionTitle", "PanelSettings.FlagsTitle", "PanelSettings.LightingTitle", "PanelSettings.AppearanceTitle", "PanelSettings.DriverTitle" })
            {
                Assert.Contains("PageSection(" + title + ", true, PanelKit.SectionHeadingGapSettings,", page);
            }
            // Alerts carries its New tag after the heading, so it draws the heading itself at the same size.
            Assert.Contains("Ui.Heading(PanelSettings.AlertsTitle, true), Ui.NewTag()", page);
            Assert.Contains("DrawsLighting();", page);
            Assert.Equal(28, PanelShell.SectionGapFor(PanelPage.Settings));
            Assert.Equal(12, PanelKit.SectionHeadingGapSettings);
        }

        /// <summary>
        /// New marks what the shipped plugin cannot do, for one release: the pit-lane switch and the Alerts
        /// heading, as the artboard tags them; the night preview, which only the Map tags; and delta precision
        /// (#322) and the clock (#324), which landed after v0.3.0-rc.7.
        /// </summary>
        [Fact]
        public void Every_control_the_shipped_plugin_lacks_carries_New()
        {
            var page = Page();
            Assert.Contains("Ui.SettingRow(PanelSettings.FlagsInPitLaneTitle, flagsInPitLane, null, Ui.NewTag())", page);
            Assert.Contains("Ui.Heading(PanelSettings.AlertsTitle, true), Ui.NewTag()", page);
            Assert.Contains("Ui.HStack(8, Ui.Eyebrow(PanelSettings.PreviewTitle), Ui.NewTag())", page);
            Assert.Contains("SettingsNew(Ui.Row(PanelDataTab.DeltaPrecisionTitle,", page);
            Assert.Contains("SettingsNew(Ui.Row(PanelDataTab.ClockTitle,", page);
        }

        /// <summary>The row sits 18 under the title, as the artboard's nav, although PageLayout puts 28 over it.</summary>
        [Fact]
        public void The_on_this_page_row_is_the_artboards()
        {
            Assert.Equal(18, PanelSettings.IndexTop);
            Assert.Equal(14, PanelSettings.IndexPaddingBottom);
            Assert.Equal(4, PanelSettings.IndexGap);
            Assert.Equal(30, PanelSettings.IndexLinkHeight);
            Assert.Equal(12, PanelSettings.IndexLinkPaddingX);
            Assert.Equal(14, PanelSettings.IndexLinkTextSize);
            // The page's own top padding, and where a press puts its heading.
            Assert.Equal(36, PanelSettings.IndexReadLine);
            Assert.Equal(20, PanelSettings.IndexJumpMargin);
            var page = Page();
            Assert.Contains("var all = new List<UIElement> { SettingsIndex(sections, to) };", page);
        }

        [Fact]
        public void The_section_being_read_is_the_last_to_reach_the_top()
        {
            var tops = new double[] { -900, -300, 20, 500, 1100, 1600 };
            Assert.Equal(2, PanelSettings.CurrentSection(tops, PanelSettings.IndexReadLine, 700, false, -1));
            Assert.Equal(1, PanelSettings.CurrentSection(new double[] { -900, -300, 40, 500, 1100, 1600 }, PanelSettings.IndexReadLine, 700, false, -1));
            // Nothing has reached the top yet: the first.
            Assert.Equal(0, PanelSettings.CurrentSection(new double[] { 120, 800, 1600 }, PanelSettings.IndexReadLine, 700, false, -1));
            // At the foot of the page a short last section never reaches the top, so it is the one read.
            Assert.Equal(5, PanelSettings.CurrentSection(tops, PanelSettings.IndexReadLine, 700, true, -1));
            // A link's press holds its own section while its heading is in view, even at the foot...
            Assert.Equal(4, PanelSettings.CurrentSection(new double[] { -2000, -1500, -900, -400, 200, 500 }, PanelSettings.IndexReadLine, 700, true, 4));
            // ...and lets go once it has scrolled out of view.
            Assert.Equal(3, PanelSettings.CurrentSection(new double[] { -2000, -1500, -900, 10, 800, 1300 }, PanelSettings.IndexReadLine, 700, false, 4));
            Assert.Equal(0, PanelSettings.CurrentSection(new double[0], PanelSettings.IndexReadLine, 700, false, -1));
            Assert.Equal(0, PanelSettings.CurrentSection(null, PanelSettings.IndexReadLine, 700, true, 2));
        }

        /// <summary>
        /// On arriving, the row marks and holds the section the route lands in, so the landing scroll cannot mark
        /// the one above it; a rebuild in place starts from the last build's mark and hold, not the route's, and
        /// every build reads the view once it is loaded, since a rebuild raises no ScrollChanged it can hear. A
        /// press focuses its section's heading, so a later focus restore or Tab does not scroll back up.
        /// </summary>
        [Fact]
        public void The_row_starts_from_where_the_view_is()
        {
            Assert.Equal(0, PanelSettings.IndexStartMark(-1, null));
            Assert.Equal(-1, PanelSettings.IndexStartHeld(-1, -1, null));
            Assert.Equal(2, PanelSettings.IndexStartMark(-1, PanelSoon.Incidents.Anchor));
            Assert.Equal(1, PanelSettings.IndexStartHeld(-1, -1, PanelSettings.AnchorFlags));
            // A rebuild in place, after the driver scrolled away from where the route landed.
            Assert.Equal(4, PanelSettings.IndexStartMark(4, PanelSettings.AnchorFlags));
            Assert.Equal(-1, PanelSettings.IndexStartHeld(4, -1, PanelSettings.AnchorFlags));
            Assert.Equal(5, PanelSettings.IndexStartHeld(5, 5, null));
            // Search lands on Flags, a short section the landing scroll leaves at the foot of the view: held,
            // it stays marked rather than Race data, whose top is the last to reach the read line.
            var landed = new double[] { -400, 560, 820, 1300, 1900, 2300 };
            var held = PanelSettings.IndexStartHeld(-1, -1, PanelSettings.AnchorFlags);
            Assert.Equal(1, PanelSettings.CurrentSection(landed, PanelSettings.IndexReadLine, 700, false, held));
            Assert.Equal(0, PanelSettings.CurrentSection(landed, PanelSettings.IndexReadLine, 700, false, -1));

            var page = Page();
            Assert.Contains("var start = PanelSettings.IndexStartMark(settingsIndexMark, anchor);", page);
            Assert.Contains("var held = PanelSettings.IndexStartHeld(settingsIndexMark, settingsIndexHeld, anchor);", page);
            Assert.Contains("mark(start);", page);
            Assert.Contains("OnLeave(\"Settings.indexMark\", () => { settingsIndexMark = -1; settingsIndexHeld = -1; });", page);
            Assert.Contains("PanelSettings.CurrentSection(tops, PanelSettings.IndexReadLine, scroll.ViewportHeight, atEnd, held)", page);
            Assert.Contains("Dispatcher.BeginInvoke(read, DispatcherPriority.Background);", page);
            Assert.Contains("SettingsFocusHeading(heading);", page);
            Assert.Contains("KeyboardNavigation.SetIsTabStop(heading, false);", page);
        }

        [Fact]
        public void A_route_marks_the_section_it_lands_in()
        {
            Assert.Equal(0, PanelSettings.SectionOf(null));
            Assert.Equal(3, PanelSettings.SectionOf(PanelSettings.AnchorLighting));
            Assert.Equal(5, PanelSettings.SectionOf(PanelSettings.AnchorDriver));
            // A search hit on a greyed row lands in the section that draws it.
            Assert.Equal(0, PanelSettings.SectionOf(PanelSoon.FuelTargetPerLap.Anchor));
            Assert.Equal(2, PanelSettings.SectionOf(PanelSoon.Incidents.Anchor));
            Assert.Equal(4, PanelSettings.SectionOf(PanelSoon.ColourVision.Anchor));
            Assert.Equal(0, PanelSettings.SectionOf("elsewhere"));
        }

        /// <summary>
        /// Below 560 of content a control goes under its title: the driver names' four examples, the widest
        /// control on the page, need about 500 beside their title. Never a test of Narrow, which is about the
        /// sidebar and not the room.
        /// </summary>
        [Fact]
        public void A_control_goes_under_its_title_only_where_the_two_do_not_fit()
        {
            Assert.True(PanelSettings.StacksControls(400));
            Assert.True(PanelSettings.StacksControls(559));
            Assert.False(PanelSettings.StacksControls(560));
            Assert.False(PanelSettings.StacksControls(679));
            Assert.DoesNotContain("!Narrow", Page());
            Assert.Equal(320, PanelSettings.SliderWidthFor(1000));
            Assert.Equal(300, PanelSettings.SliderWidthFor(300));
        }

        [Fact]
        public void The_units_line_reads_simhubs_own_names()
        {
            Assert.Equal("km/h · °C · bar · L", PanelSettings.UnitsLine("Kmh", "Celcius", "Bar", "Liters"));
            Assert.Equal("mph · °F · psi · gal", PanelSettings.UnitsLine("Mph", "Fahrenheit", "Psi", "Gallons"));
            Assert.Equal("km/h · K · kPa · L", PanelSettings.UnitsLine("Kmh", "Kelvin", "Kpa", "Liters"));
            // What SimHub did not answer is left out, and nothing at all leaves the caption alone.
            Assert.Equal("km/h · L", PanelSettings.UnitsLine("Kmh", null, "Nonsense", "Liters"));
            Assert.Null(PanelSettings.UnitsLine(null, null, null, null));
            // SimHub spells it Celcius, and the correct spelling is not one of its values.
            Assert.Null(PanelSettings.TemperatureUnit("Celsius"));
        }

        /// <summary>
        /// The table's rows in the artboard's order, the live three first, with where each shows today and the
        /// Rig scenario "Try" opens; the greyed four are the registry's own titles, so a row and its search hit
        /// cannot drift apart.
        /// </summary>
        /// <remarks>
        /// A live row's ticks are today's surfaces as #512 sets them out, not the artboard's mock: low fuel
        /// reaches the screens, the strips' car lamp and the matrix, and oil and water only the matrix, whose
        /// warning glyphs are the one thing that reads their thresholds. A greyed row's ticks are the
        /// artboard's, for #512 to decide.
        /// </remarks>
        [Fact]
        public void The_alert_table_is_the_artboards()
        {
            Assert.Equal(new[] { "Low fuel", "Oil temperature", "Water temperature", "Tyre wear", "Pit window open", "Incidents", "Hybrid battery low" }, PanelSettings.Alerts.Select(a => a.Title));
            Assert.Equal(new[] { PanelEmulation.LowFuel, PanelEmulation.Oil, PanelEmulation.Water, null, null, null, null }, PanelSettings.Alerts.Select(a => a.ScenarioId));
            Assert.Equal(new[] { "under", "over", "over", "over", "", "at", "under" }, PanelSettings.Alerts.Select(a => a.Op));
            Assert.Equal(new[] { "laps", null, null, "%", "", "x", "V" }, PanelSettings.Alerts.Select(a => a.Unit));
            Assert.False(PanelSettings.Alert(PanelSoon.PitWindowOpen.Title).HasThreshold);
            Assert.Equal(new[] { true, true, true, false }, PanelSettings.Alert(PanelSettings.LowFuelTitle).Surfaces);
            Assert.Equal(new[] { false, false, true, false }, PanelSettings.Alert(PanelSettings.OilTempTitle).Surfaces);
            Assert.Equal(new[] { false, false, true, false }, PanelSettings.Alert(PanelSettings.WaterTempTitle).Surfaces);
            Assert.Equal(new[] { true, false, false, true }, PanelSettings.Alert(PanelSoon.TyreWear.Title).Surfaces);
            Assert.Equal(new[] { true, true, true, true }, PanelSettings.Alert(PanelSoon.PitWindowOpen.Title).Surfaces);
            Assert.Equal(new[] { true, false, false, true }, PanelSettings.Alert(PanelSoon.Incidents.Title).Surfaces);
            Assert.Equal(new[] { true, false, true, false }, PanelSettings.Alert(PanelSoon.HybridBatteryLow.Title).Surfaces);
            // What a greyed row's box shows, faded: the artboard's examples, and nothing where it draws a dash.
            Assert.Equal(new string[] { null, null, null, "70", null, "12", null }, PanelSettings.Alerts.Select(a => a.Example));
            Assert.Equal(new[] { PanelSoon.TyreWear, PanelSoon.PitWindowOpen, PanelSoon.Incidents, PanelSoon.HybridBatteryLow }.Select(s => s.Title),
                PanelSettings.Alerts.Where(a => !a.Live).Select(a => a.Title));
            Assert.Null(PanelSettings.Alert("Nothing"));
            Assert.Equal(new[] { "Screens", "LEDs", "Matrix", "Races only" }, PanelSettings.SurfaceColumns);
            Assert.Equal("Alert", PanelSettings.AlertColumn);
            Assert.Equal("When", PanelSettings.WhenColumn);
            Assert.Equal("Try", PanelSettings.TryLabel);
            // Every row is drawn, in this order, and every live row writes the rig-wide setting.
            var page = Page();
            var rows = new[] { "PanelSettings.LowFuelTitle", "PanelSettings.OilTempTitle", "PanelSettings.WaterTempTitle", "PanelSoon.TyreWear.Title", "PanelSoon.PitWindowOpen.Title", "PanelSoon.Incidents.Title", "PanelSoon.HybridBatteryLow.Title" }
                .Select(row => page.IndexOf("PanelSettings.Alert(" + row + ")", StringComparison.Ordinal)).ToList();
            Assert.All(rows, index => Assert.True(index >= 0));
            Assert.Equal(rows.OrderBy(i => i), rows);
            Assert.Contains("Open(PanelPage.Rig, scenario)", page);
        }

        [Fact]
        public void The_surface_columns_fold_away_where_they_do_not_fit()
        {
            Assert.True(PanelSettings.AlertSurfacesFit(680));
            Assert.True(PanelSettings.AlertSurfacesFit(1112));
            Assert.False(PanelSettings.AlertSurfacesFit(679));
            Assert.Contains("Ui.SoonRow(PanelSoon.AlertDisplay)", Page());
        }

        /// <summary>A temperature of 0 is the unit's own default, which the empty box shows as its placeholder,
        /// in SimHub's spelling of the unit; nothing when SimHub cannot say.</summary>
        [Fact]
        public void A_temperature_of_nothing_is_the_units_default()
        {
            Assert.Equal(120, PanelSettings.TemperatureDefault(true, "Celcius"));
            Assert.Equal(248, PanelSettings.TemperatureDefault(true, "Fahrenheit"));
            Assert.Equal(393, PanelSettings.TemperatureDefault(true, "Kelvin"));
            Assert.Equal(110, PanelSettings.TemperatureDefault(false, "Celcius"));
            Assert.Equal(230, PanelSettings.TemperatureDefault(false, "Fahrenheit"));
            Assert.Null(PanelSettings.TemperatureDefault(true, null));
            Assert.Null(PanelSettings.TemperatureDefault(false, "Rankine"));

            Assert.Equal("", PanelSettings.ThresholdText(null));
            Assert.Equal("", PanelSettings.ThresholdText(0));
            Assert.Equal("130", PanelSettings.ThresholdText(130));

            Assert.Equal(0, PanelSettings.ParseThreshold("", 130, 999));
            Assert.Equal(0, PanelSettings.ParseThreshold("  ", 130, 999));
            Assert.Equal(0, PanelSettings.ParseThreshold(null, 130, 999));
            Assert.Equal(125, PanelSettings.ParseThreshold(" 125 ", 130, 999));
            Assert.Equal(130, PanelSettings.ParseThreshold("hot", 130, 999));
            Assert.Equal(999, PanelSettings.ParseThreshold("5000", 130, 999));
            Assert.Equal(0, PanelSettings.ParseThreshold("-4", 130, 999));

            var page = Page();
            Assert.Contains("Settings.SetLightsOilTemp(v); Save();", page);
            Assert.Contains("Settings.SetLightsWaterTemp(v); Save();", page);
            Assert.Contains("Settings.FlagBoxLowFuelLaps = v; Save();", page);
        }

        /// <summary>The page's code from a control's call to the end of its handler.</summary>
        private static string Handler(string page, string start)
        {
            var at = page.IndexOf(start, StringComparison.Ordinal);
            Assert.True(at >= 0, start);
            var end = page.IndexOf("});", at, StringComparison.Ordinal);
            Assert.True(end > at, start);
            return page.Substring(at, end - at);
        }

        /// <summary>The settings a handler writes, in its order.</summary>
        private static string[] Writes(string handler)
        {
            return Regex.Matches(handler, @"(?<![\w.])Settings\.(\w+) = ").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        }

        /// <summary>
        /// Every live control writes its own setting and nothing else, and saves: a control that reads one
        /// setting and writes another, or writes nothing, would otherwise pass every other test on the page.
        /// </summary>
        [Fact]
        public void Every_live_control_writes_its_own_setting_and_saves()
        {
            var page = Page();
            foreach (var pair in new[]
            {
                new[] { "BuildSegmented(Contract.PositionModes,", "PositionMode" },
                new[] { "BuildSegmented(Contract.DeltaReferences,", "DeltaReference" },
                new[] { "BuildSegmented(Contract.DeltaPrecisions,", "DeltaPrecision" },
                new[] { "BuildSegmented(Contract.SessionProgressModes,", "SessionProgress" },
                new[] { "BuildSegmented(Contract.DriverNameFormats,", "DriverNameFormat" },
                new[] { "BuildSegmented(Contract.ClockFormats,", "ClockFormat" },
                new[] { "BuildSegmented(Contract.BlueFlagDetails,", "BlueFlagDetail" },
                new[] { "BuildToggle(Settings.DriverNameTeam,", "DriverNameTeam" },
                new[] { "BuildToggle(Settings.FlagsInPitLane,", "FlagsInPitLane" },
                new[] { "BuildToggle(Settings.LightsNightMode,", "LightsNightMode" },
            })
            {
                var handler = Handler(page, pair[0]);
                Assert.Equal(new[] { pair[1] }, Writes(handler));
                var value = pair[0].StartsWith("BuildToggle", StringComparison.Ordinal) ? "on" : "value";
                Assert.Contains("Settings." + pair[1] + " = " + value + ";", handler);
                Assert.Contains("Save();", handler);
            }
            // Each segmented control reads the setting it writes.
            Assert.Contains("BuildSegmented(Contract.PositionModes, PanelDataTab.PositionLabels, Settings.PositionMode,", page);
            Assert.Contains("BuildSegmented(Contract.DeltaReferences, PanelDataTab.DeltaLabels, Settings.DeltaReference,", page);
            Assert.Contains("BuildSegmented(Contract.DeltaPrecisions, PanelDataTab.DeltaPrecisionLabels, Settings.DeltaPrecision,", page);
            Assert.Contains("BuildSegmented(Contract.SessionProgressModes, PanelDataTab.SessionLabels, Settings.SessionProgress,", page);
            Assert.Contains("BuildSegmented(Contract.DriverNameFormats, PanelDataTab.DriverNameLabels, Settings.DriverNameFormat,", page);
            Assert.Contains("BuildSegmented(Contract.ClockFormats, PanelDataTab.ClockLabels, Settings.ClockFormat,", page);
            Assert.Contains("BuildSegmented(Contract.BlueFlagDetails, PanelDataTab.BlueFlagLabels, Settings.BlueFlagDetail,", page);
            // The preview's Day and Night write nothing and save nothing.
            var pick = Handler(page, "BuildSegmented(PanelSettings.PreviewValues,");
            Assert.Empty(Writes(pick));
            Assert.DoesNotContain("Save()", pick);
        }

        /// <summary>
        /// SimHub's units are read in the order the page uses them -- speed, temperature, pressure, fuel -- and
        /// each temperature box shows its own default: the oil's on the oil box and the water's on the water's.
        /// </summary>
        [Fact]
        public void Each_unit_and_default_goes_where_it_belongs()
        {
            var page = Page();
            var order = new[] { "units.LocalSpeedUnit", "units.LocalTemperatureUnit", "units.LocalPressureUnit", "units.LocalFuelUnit" }
                .Select(read => page.IndexOf(read, StringComparison.Ordinal)).ToList();
            Assert.All(order, index => Assert.True(index >= 0));
            Assert.Equal(order.OrderBy(i => i), order);
            Assert.Contains("PanelSettings.UnitsLine(units[0], units[1], units[2], units[3])", page);
            Assert.Contains("var temperature = units[1];", page);
            Assert.Contains("PanelSettings.FuelUnit(units[3])", page);
            Assert.Contains("SettingsThresholdBox(Settings.LightsOilTemp, PanelSettings.TemperatureDefault(true, temperature),", page);
            Assert.Contains("SettingsThresholdBox(Settings.LightsWaterTemp, PanelSettings.TemperatureDefault(false, temperature),", page);
        }

        /// <summary>
        /// The preview writes nothing: it shows the day brightness by day and the night one by night, as the
        /// driver picks, following night mode until they do.
        /// </summary>
        [Fact]
        public void The_preview_shows_each_brightness_and_writes_nothing()
        {
            Assert.False(PanelSettings.ShowsNight(null, false));
            Assert.True(PanelSettings.ShowsNight(null, true));
            Assert.True(PanelSettings.ShowsNight(true, false));
            Assert.False(PanelSettings.ShowsNight(false, true));
            Assert.Equal(1.0, PanelSettings.PreviewLevel(false, 100, 25));
            Assert.Equal(0.25, PanelSettings.PreviewLevel(true, 100, 25));
            Assert.Equal(0.6, PanelSettings.PreviewLevel(false, 60, 25), 6);
            Assert.Equal("100%", PanelSettings.PreviewPercent(false, 100, 25));
            Assert.Equal("25%", PanelSettings.PreviewPercent(true, 100, 25));
            Assert.Equal("0%", PanelSettings.PreviewPercent(true, 100, -5));
            // A pick stands while night mode stays where it was made, and any move of night mode lets it go.
            Assert.Equal(false, PanelSettings.KeptPick(false, false, false));
            Assert.Equal(true, PanelSettings.KeptPick(true, true, true));
            Assert.Null(PanelSettings.KeptPick(false, false, true));
            Assert.Null(PanelSettings.KeptPick(true, true, false));
            Assert.Null(PanelSettings.KeptPick(null, false, false));
            // The pick is a field of the page's and the setting is written only by the switch. It is let go of
            // on leaving the page, on the switch's press, and on any change of night mode the repaint hears.
            var page = Page();
            Assert.Contains("settingsPreviewPick = value == PanelSettings.PreviewNight;", page);
            Assert.Contains("settingsPreviewPickedUnder = Settings.LightsNightMode;", page);
            Assert.Contains("settingsPreviewPick = PanelSettings.KeptPick(settingsPreviewPick, settingsPreviewPickedUnder, Settings.LightsNightMode);", page);
            Assert.Contains("OnLeave(\"Settings.previewPick\", () => settingsPreviewPick = null);", page);
            Assert.Matches(@"settingsPreviewPick = null;\s*Settings\.LightsNightMode = on;", page);
            Assert.Single(Regex.Matches(page, @"Settings\.LightsNightMode = on;"));
            Assert.Contains("if (v == Settings.LightsBrightness) return; Settings.LightsBrightness = v; Save(); ShowLightingChange();", page);
            Assert.Contains("if (v == Settings.LightsNightBrightness) return; Settings.LightsNightBrightness = v; Save(); ShowLightingChange();", page);
            Assert.Contains("OnLighting(repaint);", page);
        }

        /// <summary>The artboard's preview: a 3·9·3 strip of 20 px LEDs under a yellow, and the gear on an 8x8
        /// of 9 px cells.</summary>
        [Fact]
        public void The_preview_is_drawn_at_the_artboards_sizes()
        {
            Assert.Equal(3, PanelSettings.PreviewEnds);
            Assert.Equal(9, PanelSettings.PreviewCentre);
            Assert.Equal(PanelEmulation.Yellow, PanelSettings.PreviewStripScenario);
            Assert.Equal(PanelEmulation.Mid, PanelSettings.PreviewMatrixScenario);
            Assert.Equal("gear", PanelSettings.PreviewMatrixOptions().Rest);
            Assert.Contains("gear", Contract.FlagBoxRests);
            Assert.Equal(20, PanelSettings.PreviewStrip.Led);
            Assert.Equal(3, PanelSettings.PreviewStrip.Radius);
            Assert.Equal(3, PanelSettings.PreviewStrip.Gap);
            Assert.Equal(7, PanelSettings.PreviewStrip.GroupGap);
            Assert.Null(PanelSettings.PreviewStrip.GroundHex);
            Assert.Equal(Theme.SurfaceRaised, PanelSettings.PreviewStrip.UnlitHex);
            Assert.Equal(9, PanelSettings.PreviewMatrix.Cell);
            Assert.Equal(2, PanelSettings.PreviewMatrix.Gap);
            Assert.Equal(Theme.SurfaceZone, PanelSettings.PreviewMatrix.UnlitHex);
            Assert.Equal(40, PanelSettings.PreviewStageGap);
            Assert.Equal(18, PanelSettings.PreviewStagePadding);
            // The stage's padding gives back half the wrap gap its pictures carry, and WPF refuses a negative
            // padding at run time.
            Assert.Equal(8, PanelSettings.PreviewWrapGap);
            Assert.True(PanelSettings.PreviewStagePadding - PanelSettings.PreviewWrapGap / 2 >= 0);
            Assert.True(PanelSettings.IndexPaddingBottom - PanelSettings.IndexGap >= 0);
            Assert.Equal(22, PanelSettings.PreviewPercentSize);
            Assert.Equal(56, PanelSettings.PreviewPercentWidth);
            Assert.Equal(320, PanelSettings.SliderWidth);
            // The ends are the flag and the centre the revs, so both show the dim.
            var frame = PanelEmulation.StripFrame(PanelSettings.PreviewEnds, PanelSettings.PreviewCentre, PanelSettings.PreviewStripScenario);
            Assert.All(frame[0], led => Assert.Equal(Theme.FlagYellow, led));
            Assert.Contains(frame[1], led => led == Theme.ShiftStage1);
        }

        /// <summary>The night-mode button's chip opens the binding's own row on Shortcuts, at the .key size.</summary>
        [Fact]
        public void The_night_mode_button_opens_its_binding()
        {
            var page = Page();
            Assert.Contains("SettingsBindingKey(Contract.ToggleNightModeAction)", page);
            Assert.Contains("Go(PanelPage.Shortcuts, PanelBindings.Anchor(action))", page);
            Assert.Contains(Contract.ToggleNightModeAction, Contract.RigActionNames());
        }

        /// <summary>Every greyed row of the page is one of the registry's Settings entries, and it draws all of
        /// them, section by section.</summary>
        [Fact]
        public void It_draws_every_greyed_row_the_registry_gives_it()
        {
            Assert.Equal(PanelSoon.For(PanelPage.Settings).Select(item => item.Anchor).OrderBy(a => a, StringComparer.Ordinal),
                PanelSettings.SoonDrawn.Select(item => item.Anchor).OrderBy(a => a, StringComparer.Ordinal));
            Assert.Equal(PanelSettings.SectionTitles.Length, PanelSettings.SoonBySection.Length);
            Assert.Equal(PanelSettings.SoonDrawn.Length, PanelSettings.SoonDrawn.Distinct().Count());
        }

        /// <summary>
        /// Every section heading and every live row is found, and routed to its own section; the greyed rows
        /// are found through the registry, and the night-mode button by Shortcuts, where the binding is.
        /// </summary>
        [Fact]
        public void Search_finds_every_row_in_its_section()
        {
            var labels = PanelSettings.Search.Select(entry => entry.Label).ToList();
            foreach (var title in PanelSettings.SectionTitles) Assert.Contains(title, labels);
            foreach (var row in new[]
            {
                PanelDataTab.PositionTitle, PanelDataTab.DeltaTitle, PanelDataTab.DeltaPrecisionTitle, PanelDataTab.SessionTitle,
                PanelDataTab.DriverNameTitle, PanelDataTab.TeamNameTitle, PanelDataTab.ClockTitle, PanelSettings.UnitsTitle,
                PanelDataTab.BlueFlagTitle, PanelSettings.FlagsInPitLaneTitle,
                PanelSettings.LowFuelTitle, PanelSettings.OilTempTitle, PanelSettings.WaterTempTitle,
                PanelSettings.BrightnessTitle, PanelSettings.NightBrightnessTitle, PanelSettings.NightModeTitle,
            })
            {
                Assert.Contains(row, labels);
            }
            Assert.Equal(labels.Count, labels.Distinct().Count());
            Assert.DoesNotContain(PanelSettings.NightModeButtonTitle, labels);
            Assert.DoesNotContain(labels, label => PanelSettings.SoonDrawn.Any(item => item.Title == label));
            Assert.All(PanelSettings.Search, entry => Assert.Contains(entry.Route.Anchor, PanelSettings.SectionAnchors));
            Assert.Equal(PanelSettings.AnchorAlerts, PanelSettings.Search.Single(entry => entry.Label == PanelSettings.OilTempTitle).Route.Anchor);
            Assert.Equal(PanelSettings.AnchorRaceData, PanelSettings.Search.Single(entry => entry.Label == PanelSettings.UnitsTitle).Route.Anchor);
            Assert.NotEmpty(PanelSearch.Find(PanelSearch.All(), "night mode button"));
        }
    }
}
