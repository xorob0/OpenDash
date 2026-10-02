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

        /// <summary>The page's lines in this order, with only white space between them.</summary>
        private static string Lines(params string[] lines)
        {
            return string.Join(@"\s*", lines.Select(Regex.Escape));
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
            // The number is in SimHub's unit, and the default
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

        /// <summary>The greyed rows' words, which say what the tickets will build: the artboard's, but for the
        /// tyre display's second button, whose "then Pressure" is a fragment voice.md does not allow in a
        /// chooser. It is two names, and the row is bare, as the artboard draws it: #325 settles the words.</summary>
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

        /// <summary>
        /// No text the page draws is a glyph alone, the route PanelIconsTests' guard cannot see: that guard reads
        /// only literal arguments, and a constant handed to a hand-built TextBlock passes it. Every public
        /// string of PanelSettings and PanelDataTab, every word, unit and example of the alert table, and every
        /// placeholder the page hands SettingsHinted, has a letter or a digit in it; the greyed boxes' dash is
        /// drawn, and the tyre wear's percent sign sits inside its "70%".
        /// </summary>
        [Fact]
        public void No_text_on_the_page_is_a_glyph_alone()
        {
            var texts = new[] { typeof(PanelSettings), typeof(PanelDataTab) }
                .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Select(field => new { field.Name, Value = field.GetValue(null) }))
                .SelectMany(field => field.Value is string ? new[] { field.Name + " = " + (string)field.Value }
                    : field.Value is string[] ? ((string[])field.Value).Select(text => field.Name + "[] = " + text).ToArray()
                    : new string[0])
                .Concat(PanelSettings.Alerts.SelectMany(alert => new[]
                {
                    "Alert.Title = " + alert.Title, "Alert.Op = " + alert.Op, "Alert.Unit = " + alert.Unit, "Alert.Example = " + alert.Example,
                }))
                .ToList();
            Assert.Contains("TyreDisplayLabels[] = Pressure", texts);
            // Low fuel's two units, both drawn alone after the box, both read here and in the voice loop.
            Assert.Contains("LapUnit = lap", texts);
            Assert.Contains("LapsUnit = laps", texts);
            Assert.Contains("Alert.Example = 70%", texts);
            Assert.DoesNotContain(texts, text =>
            {
                var value = text.Substring(text.IndexOf(" = ", StringComparison.Ordinal) + 3);
                return value.Length > 0 && !value.Any(char.IsLetterOrDigit);
            });

            var page = Page();
            var hints = Regex.Matches(page, @"SettingsHinted\([^;\n]*PanelSettings\.(\w+Hint)\)").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(new[] { "FirstNameHint", "SurnameHint", "RaceNumberHint" }, hints);
            Assert.All(hints, name => Assert.True(((string)typeof(PanelSettings).GetField(name).GetValue(null)).Any(char.IsLetterOrDigit), name));
            Assert.Equal(3, Regex.Matches(page, @"SettingsHinted\(").Count - 1);
            // The temperatures' placeholder is the unit's default, a number, when it is set and when it follows
            // SimHub's unit.
            Assert.Contains("placeholder = SettingsHint(box, PanelSettings.ThresholdText(fallback));", page);
            Assert.Equal(2, Regex.Matches(page, @"SettingsHint\(").Count - 1);
            Assert.Equal(2, Regex.Matches(page, @"Default\.Text = PanelSettings\.ThresholdText\(PanelSettings\.TemperatureDefault\(").Count);
        }

        [Fact]
        public void Every_caption_is_a_sentence()
        {
            foreach (var caption in new[] { PanelSettings.UnitsCaption, PanelSettings.TemperatureCaption, PanelSettings.BrightnessCaption })
            {
                Assert.EndsWith(".", caption, StringComparison.Ordinal);
            }
        }

        /// <summary>voice.md: no contractions and no question anywhere in the page's words, PanelDataTab's and the
        /// alert rows' included, and every title, column and option in sentence case, where an acronym keeps
        /// its capitals, with no exception.</summary>
        [Fact]
        public void The_words_follow_the_voice()
        {
            var words = new[] { typeof(PanelSettings), typeof(PanelDataTab) }
                .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
                .SelectMany(field => field.FieldType == typeof(string) ? new[] { (string)field.GetValue(null) }
                    : field.FieldType == typeof(string[]) ? (string[])field.GetValue(null)
                    : new string[0])
                .Concat(PanelSettings.Alerts.SelectMany(alert => new[] { alert.Title, alert.Op, alert.Unit, alert.Example }))
                .Where(text => text != null)
                .ToList();
            // Either apostrophe, since copy taken from an artboard carries the typographic one, and the
            // pronoun contractions by name, so a possessive such as "SimHub's" still passes.
            const string contraction = @"n['’]t\b|['’](re|ll|ve|m|d)\b|\b(it|that|there|here|what|who|let)['’]s\b";
            foreach (var sample in new[] { "It's set in SimHub.", "That's the default.", "Isn’t shown.", "Don’t.", "We’re here.", "You'd see it." })
            {
                Assert.Matches(new Regex(contraction, RegexOptions.IgnoreCase), sample);
            }
            Assert.DoesNotMatch(new Regex(contraction, RegexOptions.IgnoreCase), "SimHub's device brightness applies on top.");
            Assert.DoesNotContain(words, text => Regex.IsMatch(text, contraction, RegexOptions.IgnoreCase));
            Assert.DoesNotContain(words, text => text.EndsWith("?", StringComparison.Ordinal));
            var labels = PanelSettings.SectionTitles
                .Concat(new[]
                {
                    PanelSettings.UnitsTitle, PanelSettings.FlagsInPitLaneTitle, PanelSettings.BrightnessTitle,
                    PanelSettings.NightBrightnessTitle, PanelSettings.NightModeTitle, PanelSettings.NightModeButtonTitle,
                    PanelSettings.PreviewTitle, PanelSettings.AlertColumn, PanelSettings.ThresholdColumn, PanelSettings.TryLabel,
                })
                .Concat(PanelSettings.Alerts.Select(alert => alert.Title))
                .Concat(PanelSettings.SurfaceColumns)
                .Concat(PanelSettings.PreviewLabels)
                .Concat(PanelSettings.YellowFlagLabels)
                .Concat(PanelSettings.TyreDisplayLabels)
                .Concat(PanelSettings.ThemeLabels)
                .Concat(PanelSettings.ColourVisionLabels)
                .Concat(PanelSettings.ColoursLabels)
                .Concat(new[] { PanelSettings.FirstNameHint, PanelSettings.SurnameHint, PanelSettings.LogoButton, PanelSettings.IdleBackgroundButton })
                // The race data rows' titles and options, which are PanelDataTab's. The driver names and the
                // clock are worked examples -- a name and a time -- and not labels, so they are left out.
                .Concat(new[]
                {
                    PanelDataTab.PositionTitle, PanelDataTab.DeltaTitle, PanelDataTab.DeltaPrecisionTitle, PanelDataTab.SessionTitle,
                    PanelDataTab.DriverNameTitle, PanelDataTab.TeamNameTitle, PanelDataTab.ClockTitle, PanelDataTab.BlueFlagTitle,
                })
                .Concat(PanelDataTab.PositionLabels)
                .Concat(PanelDataTab.DeltaLabels)
                .Concat(PanelDataTab.DeltaPrecisionLabels)
                .Concat(PanelDataTab.SessionLabels)
                .Concat(PanelDataTab.BlueFlagLabels);
            Assert.Contains("Session progress", labels);
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
                "AnchorBlueFlag = settings.blue-flag-detail",
                "AnchorBrightness = settings.brightness",
                "AnchorClock = settings.clock",
                "AnchorDelta = settings.delta-reference",
                "AnchorDeltaPrecision = settings.delta-precision",
                "AnchorDriver = settings.driver",
                "AnchorDriverNames = settings.driver-names",
                "AnchorFlags = settings.flags",
                "AnchorFlagsInPitLane = settings.flags-in-pit-lane",
                "AnchorLighting = settings.lighting",
                "AnchorLowFuel = settings.low-fuel",
                "AnchorNightBrightness = settings.night-brightness",
                "AnchorNightMode = settings.night-mode",
                "AnchorOilTemp = settings.oil-temperature",
                "AnchorPosition = settings.position",
                "AnchorRaceData = settings.race-data",
                "AnchorSession = settings.session-progress",
                "AnchorTeamNames = settings.team-names",
                "AnchorUnits = settings.units",
                "AnchorWaterTemp = settings.water-temperature",
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
            // Each section's method under its own anchor, which search, Home's routes and the row's links land
            // on, and each link named by its section's title and scrolling to that section.
            for (var s = 0; s < SectionMethods.Length; s++)
            {
                Assert.Matches(@"Ui\.Anchor\(" + Regex.Escape(SectionMethods[s]) + @"(units)?\), PanelSettings\." + AnchorName(PanelSettings.SectionAnchors[s]) + @"\),", page);
            }
            Assert.Contains("var link = SettingsIndexLink(PanelSettings.SectionTitles[i]);", page);
            Assert.Contains("SettingsJumpTo(scroll, sections[index]);", page);
            Assert.Contains("var heading = SettingsHeadingOf(sections[i]);", page);
        }

        /// <summary>The methods that draw each section, in the page's order.</summary>
        private static readonly string[] SectionMethods =
        {
            "SettingsRaceData(", "SettingsFlags(", "SettingsAlerts(", "SettingsLighting(", "SettingsAppearance(", "SettingsDriver(",
        };

        /// <summary>The page's code for one section's method, from its declaration to the next member.</summary>
        private static string SectionBody(string page, int section)
        {
            var start = page.IndexOf("private FrameworkElement " + SectionMethods[section], StringComparison.Ordinal);
            Assert.True(start >= 0, SectionMethods[section]);
            var next = Regex.Match(page.Substring(start + 1), @"\n        (private |/// |// ---)");
            return next.Success ? page.Substring(start, next.Index + 1) : page.Substring(start);
        }

        /// <summary>
        /// Each section draws its rows in the artboard's order, and the Race data's clock after the team names
        /// where the build adds it. Read from each section's own method, so a row moved to another section
        /// fails too. The six greyed rows whose control is wide -- the tyre buttons, the yellow flags, the three
        /// appearance choosers and the driver's names, Colour vision the widest, which StackControlsBelow is
        /// sized for -- are held with their SettingsFit, so they stack below 560 as the live rows do.
        /// </summary>
        [Fact]
        public void Each_section_draws_its_rows_in_the_artboards_order()
        {
            var rows = new[]
            {
                new[]
                {
                    "SettingsFit(Ui.Row(PanelDataTab.PositionTitle,", "SettingsFit(SettingsNew(Ui.Row(PanelDataTab.DeltaTitle,",
                    "SettingsFit(SettingsNew(Ui.Row(PanelDataTab.DeltaPrecisionTitle,", "SettingsFit(Ui.Row(PanelDataTab.SessionTitle,",
                    "SettingsFit(Ui.Row(PanelDataTab.DriverNameTitle,", "Ui.Row(PanelDataTab.TeamNameTitle,",
                    "SettingsFit(SettingsNew(Ui.Row(PanelDataTab.ClockTitle,", "Ui.SettingRow(PanelSoon.FuelTargetPerLap.Title,",
                    "Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.TyreDisplay.Title,", "SettingsNew(Ui.Row(PanelSettings.UnitsTitle,",
                },
                new[]
                {
                    "SettingsFit(Ui.Row(PanelDataTab.BlueFlagTitle,", "Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.YellowFlags.Title,",
                    "Ui.SettingRow(PanelSettings.FlagsInPitLaneTitle,",
                },
                new[]
                {
                    "PanelSettings.Alert(PanelSettings.LowFuelTitle)",
                    "PanelSettings.Alert(PanelSettings.OilTempTitle)", "PanelSettings.Alert(PanelSettings.WaterTempTitle)",
                    "PanelSettings.Alert(PanelSoon.TyreWear.Title)", "PanelSettings.Alert(PanelSoon.PitWindowOpen.Title)",
                    "PanelSettings.Alert(PanelSoon.Incidents.Title)", "PanelSettings.Alert(PanelSoon.HybridBatteryLow.Title)",
                    "Ui.SettingRow(PanelSoon.AlertDisplay.Title,",
                },
                new[]
                {
                    "PanelKit.SectionHeadingGapSettings,\n                card,", "SettingsFit(Ui.Row(PanelSettings.BrightnessTitle,",
                    "SettingsFit(Ui.Row(PanelSettings.NightBrightnessTitle,", "Ui.Row(PanelSettings.NightModeTitle,",
                    "Ui.Row(PanelSettings.NightModeButtonTitle,", "Ui.SoonRow(PanelSoon.SimTimeOfDay)", "Ui.SoonRow(PanelSoon.ScreenDimming)",
                },
                new[]
                {
                    "Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.DashTheme.Title,", "Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.ColourVision.Title,",
                    "Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.Colours.Title,",
                },
                new[]
                {
                    "Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.BrandName.Title,", "Ui.SettingRow(PanelSoon.BrandRaceNumber.Title,",
                    "Ui.SettingRow(PanelSoon.BrandLogo.Title,", "Ui.SettingRow(PanelSoon.IdleScreenBackground.Title,",
                },
            };
            var page = Page();
            Assert.Equal(SectionMethods.Length, rows.Length);
            for (var s = 0; s < rows.Length; s++)
            {
                var body = SectionBody(page, s);
                var at = rows[s].Select(row => body.IndexOf(row, StringComparison.Ordinal)).ToList();
                for (var r = 0; r < at.Count; r++) Assert.True(at[r] >= 0, SectionMethods[s] + " draws " + rows[s][r]);
                Assert.Equal(at.OrderBy(i => i), at);
            }
        }

        /// <summary>
        /// SoonBySection, which marks the "On this page" link after a search lands on a greyed row, is exactly
        /// the registry entries each section's own method draws, in its order: an entry filed under the wrong
        /// section would mark the wrong link.
        /// </summary>
        [Fact]
        public void Each_sections_greyed_rows_are_the_ones_it_draws()
        {
            var named = typeof(PanelSoon).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(SoonItem))
                .ToDictionary(field => field.Name, field => (SoonItem)field.GetValue(null));
            var page = Page();
            for (var s = 0; s < SectionMethods.Length; s++)
            {
                var drawn = Regex.Matches(SectionBody(page, s), @"PanelSoon\.(\w+)").Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .Where(named.ContainsKey)
                    .Select(name => named[name])
                    .Distinct()
                    .ToList();
                Assert.Equal(drawn, PanelSettings.SoonBySection[s]);
                foreach (var item in PanelSettings.SoonBySection[s]) Assert.Equal(s, PanelSettings.SectionOf(item.Anchor));
            }
        }

        /// <summary>
        /// Every greyed row carries its own ticket and no live row carries one. Every Ui.Soon on the page wraps
        /// a SettingRow whose title is the same registry entry as the ticket it names, the one exception being
        /// the alert cell's own wrapper, which takes the row's ticket; the four greyed alert rows each name
        /// their own entry, the three live ones none; and no Ui.Soon wraps a live row.
        /// </summary>
        [Fact]
        public void Each_greyed_row_carries_its_own_ticket_and_no_live_row_carries_one()
        {
            var page = Page();
            var paired = new Regex(@"^Ui\.Soon\((?:SettingsFit\()?Ui\.SettingRow\(PanelSoon\.(\w+)\.Title,[^\n]*, PanelSoon\.\1\)");
            var wrapped = 0;
            foreach (Match soon in Regex.Matches(page, @"Ui\.Soon\([^\n]*"))
            {
                if (soon.Value.StartsWith("Ui.Soon(cell, soon);", StringComparison.Ordinal)) continue;
                Assert.True(paired.IsMatch(soon.Value), soon.Value);
                wrapped++;
            }
            // Fuel target, tyre display, yellow flags, the folded alert display, the three appearance rows and
            // the four driver rows.
            Assert.Equal(11, wrapped);
            Assert.Contains("if (soon != null) cell = Ui.Soon(cell, soon);", page);
            Assert.Single(Regex.Matches(page, @"Ui\.Soon\(cell, soon\);"));
            // The paired regex itself: a row wrapped in another row's ticket does not pass.
            Assert.DoesNotMatch(paired, "Ui.Soon(Ui.SettingRow(PanelSoon.FuelTargetPerLap.Title, fuelTarget), PanelSoon.TyreDisplay),");
            Assert.Matches(paired, "Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.TyreDisplay.Title, tyres)), PanelSoon.TyreDisplay),");

            var greyedAlerts = Regex.Matches(page, @"PanelSettings\.Alert\(PanelSoon\.(\w+)\.Title\), null, null, temperature, surfaces, PanelSoon\.\1[,)]");
            Assert.Equal(4, greyedAlerts.Count);
            Assert.Equal(new[] { "TyreWear", "PitWindowOpen", "Incidents", "HybridBatteryLow" }, greyedAlerts.Cast<Match>().Select(m => m.Groups[1].Value));
            Assert.Equal(7, Regex.Matches(page, @"SettingsAlertRow\(grid, row\+\+, ").Count);
            var live = Regex.Matches(page, @"SettingsAlertRow\(grid, row\+\+, PanelSettings\.Alert\(PanelSettings\.\w+\)[^\n]*").Cast<Match>().Select(m => m.Value).ToList();
            Assert.Equal(3, live.Count);
            Assert.All(live, line => Assert.EndsWith(", surfaces, null);", line));

            Assert.DoesNotMatch(@"Ui\.Soon\((SettingsFit\(|SettingsNew\()*Ui\.Row\(", page);
            foreach (var title in new[]
            {
                "PanelDataTab.PositionTitle", "PanelDataTab.DeltaTitle", "PanelDataTab.DeltaPrecisionTitle", "PanelDataTab.SessionTitle",
                "PanelDataTab.DriverNameTitle", "PanelDataTab.TeamNameTitle", "PanelDataTab.ClockTitle", "PanelSettings.UnitsTitle",
                "PanelDataTab.BlueFlagTitle", "PanelSettings.FlagsInPitLaneTitle", "PanelSettings.LowFuelTitle", "PanelSettings.OilTempTitle",
                "PanelSettings.WaterTempTitle", "PanelSettings.BrightnessTitle", "PanelSettings.NightBrightnessTitle", "PanelSettings.NightModeTitle",
                "PanelSettings.NightModeButtonTitle",
            })
            {
                Assert.DoesNotMatch(@"Ui\.Soon\([^\n]*" + Regex.Escape(title), page);
            }
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
            // DrawsLighting rebuilds the page on every brightness or night-mode press, the wheel's included, on
            // SimHub's interface thread, so the build reads no device, profile or disk.
            foreach (var reads in new[] { "SafePlan(", "BarCensus", "LedTargets", "StripInstaller", "FlagBoxInstaller", "PackageExtractor" })
            {
                Assert.DoesNotContain(reads, page);
            }
            Assert.DoesNotMatch(@"\b(File|Directory)\.", page);
            Assert.Equal(28, PanelShell.SectionGapFor(PanelPage.Settings));
            Assert.Equal(12, PanelKit.SectionHeadingGapSettings);
        }

        /// <summary>
        /// New marks what the shipped plugin cannot do, for one release: the pit-lane switch and the Alerts
        /// heading, as the artboard tags them; the night preview, which only the Map tags; and the rows that
        /// landed after v0.3.0-rc.7, the tagged release, although no artboard tags them: delta precision
        /// (#322), the clock (#324), the night-mode button, whose action rc.7 does not register, and the Units
        /// line, which rc.7's panel never drew: nothing in it read SimHub's GameUnitSettings. A read-only row
        /// is no exception, since what the tag says is that the row is new. Nor is an old row that gained an
        /// answer: rc.7's Delta reference offered session and all-time best only, and Last lap (#322) is
        /// something rc.7 cannot do, so the row carries New for it.
        /// </summary>
        [Fact]
        public void New_marks_what_the_artboards_tag_and_what_rc7_lacks()
        {
            var page = Page();
            Assert.Contains("Ui.SettingRow(PanelSettings.FlagsInPitLaneTitle, flagsInPitLane, null, Ui.NewTag())", page);
            Assert.Contains("Ui.Heading(PanelSettings.AlertsTitle, true), Ui.NewTag()", page);
            Assert.Contains("Ui.HStack(PanelSettings.TitleTagGap, Ui.Eyebrow(PanelSettings.PreviewTitle), Ui.NewTag())", page);
            Assert.Contains("SettingsNew(Ui.Row(PanelDataTab.DeltaTitle, PanelDataTab.DeltaCaption, delta))", page);
            // rc.7 had the first two; the third is the one the tag is for.
            Assert.Equal(new[] { "session", "alltime", "lastlap" }, Contract.DeltaReferences);
            Assert.Contains("SettingsNew(Ui.Row(PanelDataTab.DeltaPrecisionTitle,", page);
            Assert.Contains("SettingsNew(Ui.Row(PanelDataTab.ClockTitle,", page);
            Assert.Contains("SettingsNew(Ui.Row(PanelSettings.NightModeButtonTitle, null, SettingsBindingKey(Contract.ToggleNightModeAction)))", page);
            Assert.Contains("SettingsNew(Ui.Row(PanelSettings.UnitsTitle, PanelSettings.UnitsCaption, unitsLine))", page);
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
            // The page is the row, then all six sections, laid out together under the title.
            Assert.Matches(@"var all = new List<UIElement> \{ SettingsIndex\(sections, to\) \};\s*all\.AddRange\(sections\);", page);
            Assert.Contains("return PageLayout(PanelSettings.Title, null, all.ToArray());", page);
            // A link for every section from the first, each kept, to be lit, and drawn in the row's wrap, which
            // is what the row's border holds: a row, wrapping onto a second line where the six do not fit.
            Assert.Contains("var wrap = new WrapPanel { Orientation = Orientation.Horizontal };", page);
            Assert.Matches(Lines(
                "for (var i = 0; i < PanelSettings.SectionTitles.Length; i++)",
                "{",
                "var index = i;",
                "var heading = SettingsHeadingOf(sections[i]);",
                "var link = SettingsIndexLink(PanelSettings.SectionTitles[i]);"), page);
            Assert.Matches(@"links\.Add\(link\);\s*wrap\.Children\.Add\(link\);", page);
            Assert.Matches(Lines(
                "Margin = new Thickness(0, PanelSettings.IndexTop - PanelShell.SectionGapFor(PanelPage.Settings), 0, 0),",
                "Child = wrap,",
                "};"), page);
            // It hears the column's scroller once, when it is first loaded, and reads the view on every move of
            // it that moved something.
            Assert.Matches(Lines(
                "row.Loaded += (sender, args) =>",
                "{",
                "if (scroll != null) return;",
                "scroll = SettingsScrollOf(row);",
                "if (scroll == null) return;",
                "scroll.ScrollChanged += follow;"), page);
            Assert.Matches(Lines(
                "if (args.VerticalChange == 0 && args.ViewportHeightChange == 0 && args.ExtentHeightChange == 0) return;",
                "read();"), page);
            // A read from a dropped build, or before the scroller is found, or while the row is hidden, writes
            // nothing.
            Assert.Matches(Lines(
                "Action read = () =>",
                "{",
                "if (dropped || scroll == null || !row.IsVisible) return;",
                "var tops = sections.Select(section => SettingsTopIn(scroll, section)).ToList();"), page);
            // The scroller is found by walking up from the row, one parent a step.
            Assert.Matches(Lines(
                "var node = element;",
                "while (node != null)",
                "{",
                "node = VisualTreeHelper.GetParent(node);",
                "var scroll = node as ScrollViewer;",
                "if (scroll != null) return scroll;",
                "}",
                "return null;"), page);
            // A section's heading is the first thing it draws.
            Assert.Contains("var heading = panel == null || panel.Children.Count == 0 ? null : panel.Children[0] as FrameworkElement;", page);
            // A link's own template: its ground and padding are the button's, so the mark and the hover show,
            // and its label is drawn, centred in its height.
            Assert.Matches(Lines(
                "var chrome = new FrameworkElementFactory(typeof(Border));",
                "chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));",
                "chrome.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));",
                "chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(Theme.Radius));",
                "var presenter = new FrameworkElementFactory(typeof(ContentPresenter));",
                "presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);",
                "chrome.AppendChild(presenter);"), page);
            // A mark is kept on the link, so the pointer leaving a marked link leaves it marked.
            Assert.Matches(Lines(
                "link.Tag = current;",
                "SettingsInkIndexLink(link, current || link.IsMouseOver);"), page);
            // The rule that closes the row, and a link lit in the primary ink on the zone ground.
            Assert.Matches(@"BorderBrush = Ui\.Brush\(Theme\.Rule\),\s*BorderThickness = new Thickness\(0, 0, 0, PanelMetrics\.BorderWeight\),\s*Padding = new Thickness\(0, 0, 0, PanelSettings\.IndexPaddingBottom - PanelSettings\.IndexGap\),", page);
            Assert.Contains("link.Background = lit ? Ui.Brush(Theme.SurfaceZone) : Brushes.Transparent;", page);
            Assert.Contains("if (label != null) label.Foreground = Ui.Brush(lit ? Theme.TextPrimary : Theme.TextSecondary);", page);
            // It follows only the main column's own scroller, a text box's included scroller aside, and lets go
            // of it when the build is dropped, so an old build's sections are not held by a live handler.
            Assert.Contains("if (scroll == null || !ReferenceEquals(args.OriginalSource, scroll)) return;", page);
            Assert.Matches(@"OnDrop\(\(\) =>\s*\{\s*dropped = true;\s*if \(scroll != null\) scroll\.ScrollChanged -= follow;\s*\}\);", page);
        }

        /// <summary>
        /// Every metric the page draws with is PanelSettings', held to Settings.dc.html where the artboard has
        /// one: td padding 10/12 at 14 px with the name in 500, the .ck 16, Try 13, the Threshold cell's gap 8, the
        /// Units line 14 in 400, the .num-in's and the .num's 600, the preview card's padding 16/18 with gap 16
        /// and 8 under it, the name inputs 150 with gap 8, the .t gap of 8 before a tag, the h2's 10, the tyre
        /// buttons' 6 and the .idx's 500. The page types no gap, padding, size or weight of its own: no numeral
        /// but 0 in a Thickness, a Width, a Height, a FontSize, an HStack's or a VStack's gap, and no
        /// FontWeights; and each metric is pinned where it is drawn.
        /// </summary>
        [Fact]
        public void Every_metric_is_the_artboards()
        {
            Assert.Equal(12, PanelSettings.AlertCellPaddingX);
            Assert.Equal(10, PanelSettings.AlertCellPaddingY);
            Assert.Equal(14, PanelSettings.AlertTextSize);
            Assert.Equal(500, PanelSettings.AlertNameFontWeight);
            Assert.Equal(16, PanelSettings.AlertCheck);
            Assert.Equal(13, PanelSettings.AlertTryTextSize);
            Assert.Equal(8, PanelSettings.AlertThresholdGap);
            Assert.Equal(14, PanelSettings.UnitsTextSize);
            Assert.Equal(18, PanelSettings.PreviewPaddingX);
            Assert.Equal(16, PanelSettings.PreviewPaddingY);
            Assert.Equal(16, PanelSettings.PreviewGap);
            Assert.Equal(8, PanelSettings.PreviewMarginBottom);
            Assert.Equal(150, PanelSettings.NameInputWidth);
            Assert.Equal(8, PanelSettings.DriverInputGap);
            Assert.Equal(8, PanelSettings.TitleTagGap);
            Assert.Equal(10, PanelSettings.AlertsHeadingTagGap);
            Assert.Equal(6, PanelSettings.TyreButtonGap);
            Assert.Equal(8, PanelSettings.FuelTargetGap);
            Assert.Equal(500, PanelSettings.IndexLinkFontWeight);
            Assert.Equal(400, PanelSettings.UnitsFontWeight);
            Assert.Equal(600, PanelSettings.NumberFieldFontWeight);
            Assert.Equal(600, PanelSettings.PreviewPercentFontWeight);
            // The build's own, where the artboard has no counterpart: a native tick's box, the tag under a
            // surface column's name, the gap over a stacked control and over the folded row, and the names'
            // star weight against each surface column's 1.
            Assert.Equal(12, PanelSettings.AlertCheckIcon);
            Assert.Equal(4, PanelSettings.AlertHeaderTagGap);
            Assert.Equal(10, PanelSettings.StackedControlGap);
            Assert.Equal(8, PanelSettings.AlertFoldGap);
            Assert.Equal(3, PanelSettings.AlertNameWeight);
            Assert.Equal(150, PanelSettings.AlertNameMinWidth);
            // WPF's own inset of a text box's text, which a placeholder over it matches.
            Assert.Equal(2, PanelSettings.HintInset);

            var page = Page();
            Assert.DoesNotMatch(@"HStack\(\d", page);
            Assert.DoesNotMatch(@"VStack\(\d", page);
            // A star column's 1 is a share, not a metric.
            foreach (Match size in Regex.Matches(page.Replace("new GridLength(1, GridUnitType.Star)", "new GridLength(Share, GridUnitType.Star)"), @"(?<!\w)(?:Width|Height|FontSize) = [^,;\n}]*"))
            {
                Assert.DoesNotMatch(@"(?<![\w.])[1-9]\d*(?![\w.])", size.Value);
            }
            Assert.Matches(@"(?<!\w)(?:Width|Height|FontSize) = [^,;\n}]*", "link.FontSize = 18;");
            Assert.DoesNotContain("FontWeights.", page);
            foreach (Match thickness in Regex.Matches(page, @"new Thickness\([^\n]*"))
            {
                Assert.DoesNotMatch(@"(?<![\w.])[1-9]\d*(?![\w.])", thickness.Value);
            }
            Assert.Matches(@"(?<![\w.])[1-9]\d*(?![\w.])", "new Thickness(0, 0, 0, 12),");
            Assert.DoesNotMatch(@"(?<![\w.])[1-9]\d*(?![\w.])", "new Thickness(0, PanelKit.SectionHeadingGapSettings, 0, 0),");
            // Each metric where it is drawn.
            Assert.Contains("Ui.HStack(PanelSettings.FuelTargetGap,", page);
            Assert.Contains("var tyres = Ui.HStack(PanelSettings.TyreButtonGap,", page);
            Assert.Contains("var name = Ui.HStack(PanelSettings.DriverInputGap,", page);
            Assert.Contains("Height = PanelSettings.IndexLinkHeight,", page);
            Assert.Contains("Padding = new Thickness(PanelSettings.IndexLinkPaddingX, 0, PanelSettings.IndexLinkPaddingX, 0),", page);
            Assert.Contains("scroll.ScrollToVerticalOffset(Math.Max(0, scroll.VerticalOffset + top - PanelSettings.IndexJumpMargin));", page);
            Assert.Contains("parts.Control.Margin = new Thickness(0, PanelSettings.StackedControlGap, 0, 0);", page);
            Assert.Contains("Padding = new Thickness(PanelSettings.AlertCellPaddingX, PanelSettings.AlertCellPaddingY, PanelSettings.AlertCellPaddingX, PanelSettings.AlertCellPaddingY),", page);
            Assert.Matches(@"Width = PanelSettings\.AlertCheck,\s*Height = PanelSettings\.AlertCheck,", page);
            Assert.Contains("link.FontSize = PanelSettings.AlertTryTextSize;", page);
            Assert.Contains("tag.Margin = new Thickness(0, PanelSettings.AlertHeaderTagGap, 0, 0);", page);
            Assert.Equal(2, Regex.Matches(page, Regex.Escape("tag.Margin = new Thickness(PanelSettings.TitleTagGap, 0, 0, 0);")).Count);
            Assert.Contains("var card = Ui.CardBox(Ui.VStack(PanelSettings.PreviewGap, head, ground), 0);", page);
            Assert.Contains("card.Padding = new Thickness(PanelSettings.PreviewPaddingX, PanelSettings.PreviewPaddingY, PanelSettings.PreviewPaddingX, PanelSettings.PreviewPaddingY);", page);
            Assert.Contains("card.Margin = new Thickness(0, 0, 0, PanelSettings.PreviewMarginBottom);", page);
            Assert.Matches(@"var ground = new Border\s*\{\s*Background = Ui\.Brush\(Theme\.SurfaceInset\),\s*CornerRadius = new CornerRadius\(Theme\.Radius\),\s*Padding = new Thickness\(PanelSettings\.PreviewStagePadding\),", page);
            Assert.Contains("var gap = i < pictures.Length - 1 ? PanelSettings.PreviewStageGap : 0;", page);
            Assert.Contains("percent.Width = PanelSettings.PreviewPercentWidth;", page);
            Assert.Contains("folded.Margin = new Thickness(0, PanelSettings.AlertFoldGap, 0, 0);", page);
            Assert.Contains("Margin = new Thickness(0, PanelSettings.IndexTop - PanelShell.SectionGapFor(PanelPage.Settings), 0, 0),", page);
            Assert.Contains("Padding = new Thickness(0, 0, 0, PanelSettings.IndexPaddingBottom - PanelSettings.IndexGap),", page);
            Assert.Contains("link.Margin = new Thickness(0, 0, PanelSettings.IndexGap, PanelSettings.IndexGap);", page);
            Assert.Matches(@"var heading = Ui\.HStack\(PanelSettings\.AlertsHeadingTagGap, Ui\.Heading\(PanelSettings\.AlertsTitle, true\), Ui\.NewTag\(\)\);\s*heading\.HorizontalAlignment = HorizontalAlignment\.Left;\s*heading\.Margin = new Thickness\(0, 0, 0, PanelKit\.SectionHeadingGapSettings\);", page);
            Assert.Contains("hint.Margin = new Thickness(box.Padding.Left + PanelMetrics.BorderWeight + PanelSettings.HintInset, 0, box.Padding.Right + PanelMetrics.BorderWeight + PanelSettings.HintInset, 0);", page);
            Assert.Contains("box.FontWeight = FontWeight.FromOpenTypeWeight(PanelSettings.NumberFieldFontWeight);", page);
            Assert.Contains("var unitsLine = Ui.Text(string.Empty, PanelSettings.UnitsTextSize, FontWeight.FromOpenTypeWeight(PanelSettings.UnitsFontWeight), Theme.TextPrimary);", page);
            Assert.Contains("Ui.Text(string.Empty, PanelSettings.PreviewPercentSize, FontWeight.FromOpenTypeWeight(PanelSettings.PreviewPercentFontWeight), Theme.TextSecondary, PanelFonts.Data)", page);
            Assert.Contains("Ui.Text(alert.Title, PanelSettings.AlertTextSize, FontWeight.FromOpenTypeWeight(PanelSettings.AlertNameFontWeight), Theme.TextPrimary)", page);
            Assert.Contains("Ui.Text(text, PanelSettings.IndexLinkTextSize, FontWeight.FromOpenTypeWeight(PanelSettings.IndexLinkFontWeight), Theme.TextSecondary)", page);
            // The .ck's corner and border, the rule under a row (never over it), a caption's gap under its name,
            // and the .idx's corner.
            Assert.Matches(@"CornerRadius = new CornerRadius\(Theme\.Radius\),\s*BorderBrush = Ui\.Brush\(on \? Theme\.Accent : Theme\.Border\),\s*BorderThickness = new Thickness\(PanelMetrics\.BorderWeight\),", page);
            Assert.Matches(@"var rule = new Border\s*\{\s*BorderBrush = Ui\.Brush\(Theme\.Rule\),\s*BorderThickness = new Thickness\(0, 0, 0, PanelMetrics\.BorderWeight\),\s*IsHitTestVisible = false,\s*\};", page);
            Assert.Contains("under.Margin = new Thickness(0, PanelKit.FixDetailGap, 0, 0);", page);
            Assert.Contains("chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(Theme.Radius));", page);
            // The table's columns: the names, then the threshold sized to its content, the four surfaces sharing
            // the slack, and Try sized to its content.
            Assert.Matches(@"grid\.ColumnDefinitions\.Add\(names\);[^\n]*\n[^\n]*\n\s*grid\.ColumnDefinitions\.Add\(new ColumnDefinition \{ Width = GridLength\.Auto \}\);\s*if \(surfaces\)\s*\{\s*foreach \(var column in PanelSettings\.SurfaceColumns\) grid\.ColumnDefinitions\.Add\(new ColumnDefinition \{ Width = new GridLength\(1, GridUnitType\.Star\) \}\);\s*\}\s*grid\.ColumnDefinitions\.Add\(new ColumnDefinition \{ Width = GridLength\.Auto \}\);", page);
            Assert.Contains("SettingsAlertCell(grid, row, column++, Ui.HStack(PanelSettings.AlertThresholdGap, when.ToArray()), soon, false);", page);
            // The tick's look: the accent with the tick when on, the border and no fill when off.
            Assert.Contains("BorderBrush = Ui.Brush(on ? Theme.Accent : Theme.Border),", page);
            Assert.Contains("Background = on ? Ui.Brush(Theme.Accent) : Brushes.Transparent,", page);
            Assert.Matches(@"if \(on\)\s*\{\s*var tick = Ui\.Icon\(PanelIcons\.Check, Theme\.OnAccent, PanelSettings\.AlertCheckIcon, PanelIcons\.Box\);\s*tick\.HorizontalAlignment = HorizontalAlignment\.Center;\s*tick\.VerticalAlignment = VerticalAlignment\.Center;\s*box\.Child = tick;", page);
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
            // ...and lets go once it has scrolled out of view, below it or above it.
            Assert.Equal(3, PanelSettings.CurrentSection(new double[] { -2000, -1500, -900, 10, 800, 1300 }, PanelSettings.IndexReadLine, 700, false, 4));
            Assert.Equal(5, PanelSettings.CurrentSection(new double[] { -3000, -2500, -2000, -1500, -100, 20 }, PanelSettings.IndexReadLine, 700, false, 4));
            // Above the top, the hold lasts until the heading is a read line past it: nudged 20 up it still
            // holds, even at the foot; 40 up, past the 36, it lets go.
            Assert.Equal(4, PanelSettings.CurrentSection(new double[] { -2000, -1500, -900, -400, -20, 500 }, PanelSettings.IndexReadLine, 700, true, 4));
            Assert.Equal(5, PanelSettings.CurrentSection(new double[] { -2000, -1500, -900, -400, -40, 500 }, PanelSettings.IndexReadLine, 700, true, 4));
            Assert.Equal(0, PanelSettings.CurrentSection(new double[0], PanelSettings.IndexReadLine, 700, false, -1));
            Assert.Equal(0, PanelSettings.CurrentSection(null, PanelSettings.IndexReadLine, 700, true, 2));
        }

        /// <summary>
        /// On arriving, the row marks and holds the section the route lands in, so the landing scroll cannot mark
        /// the one above it; a rebuild in place starts from the last build's mark and hold, not the route's, and
        /// every build reads the view once it is loaded, since a rebuild raises no ScrollChanged it can hear. A
        /// press focuses its section's heading, so Tab does not scroll back up to the row, and a rebuild's focus
        /// restore pulls the view back to the heading rather than to the top of the page.
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
            // Race data, the first section, is a mark and a hold like any other: a rebuild in place after the
            // driver scrolled back up from where a route landed keeps Race data, and a press on its link at the
            // foot of the page holds it while its heading is in view.
            Assert.Equal(0, PanelSettings.IndexStartMark(0, PanelSettings.AnchorFlags));
            Assert.Equal(-1, PanelSettings.IndexStartHeld(0, -1, PanelSettings.AnchorFlags));
            Assert.Equal(0, PanelSettings.IndexStartHeld(0, 0, PanelSettings.AnchorFlags));
            Assert.Equal(0, PanelSettings.CurrentSection(new double[] { 0, 300, 600, 900, 1200, 1500 }, PanelSettings.IndexReadLine, 700, true, 0));
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
            // Every mark keeps itself and the hold for the next build in place, the write side of the two reads
            // above, and lights exactly the section it names.
            Assert.Matches(@"settingsIndexMark = current;\s*settingsIndexHeld = held;\s*for \(var k = 0; k < links\.Count; k\+\+\) SettingsPaintIndexLink\(links\[k\], k == current\);", page);
            Assert.Contains("OnLeave(\"Settings.indexMark\", () => { settingsIndexMark = -1; settingsIndexHeld = -1; });", page);
            Assert.Contains("PanelSettings.CurrentSection(tops, PanelSettings.IndexReadLine, scroll.ViewportHeight, atEnd, held)", page);
            Assert.Contains("Dispatcher.BeginInvoke(read, DispatcherPriority.Background);", page);
            Assert.Contains("SettingsFocusHeading(heading);", page);
            Assert.Contains("KeyboardNavigation.SetIsTabStop(heading, false);", page);
            // A press holds its section, marks it, scrolls to it and focuses its heading, in that order.
            Assert.Matches(@"link\.Click \+= \(sender, args\) =>\s*\{\s*held = index;\s*mark\(index\);\s*SettingsJumpTo\(scroll, sections\[index\]\);\s*SettingsFocusHeading\(heading\);\s*\};", page);
            // The heading takes the focus but is no tab stop, and is focused once the jump has been laid out. It
            // is as wide as its words, so its focus ring hugs them rather than running across the column.
            Assert.Matches(@"heading\.Focusable = true;\s*heading\.HorizontalAlignment = HorizontalAlignment\.Left;\s*KeyboardNavigation\.SetIsTabStop\(heading, false\);", page);
            Assert.Matches(@"if \(heading\.IsVisible\) Keyboard\.Focus\(heading\);\s*\}\), DispatcherPriority\.Loaded\);", page);
            // A read lets go of the hold once the held section is no longer the one read, and a view scrolled to
            // its foot marks the short last section.
            Assert.Matches(@"var current = PanelSettings\.CurrentSection\(tops, PanelSettings\.IndexReadLine, scroll\.ViewportHeight, atEnd, held\);\s*if \(current != held\) held = -1;\s*mark\(current\);", page);
            Assert.Contains("var atEnd = scroll.ScrollableHeight > 0 && scroll.VerticalOffset >= scroll.ScrollableHeight - 1;", page);

            // What those calls are fed: the route's own anchor, so a search or a Home fix landing on Flags marks
            // Flags; each section's top as its distance down from the top of the view, so the mark follows the
            // scroll and a press's jump lands its heading the margin under the top; and a jump to a section the
            // scroller cannot measure brings it into view instead.
            Assert.Contains("var anchor = to == null ? null : to.Anchor;", page);
            Assert.Matches(Lines(
                "if (!section.IsVisible) return double.MaxValue;",
                "try",
                "{",
                "return section.TranslatePoint(new Point(0, 0), scroll).Y;"), page);
            Assert.Matches(Lines(
                "var top = scroll == null ? double.MaxValue : SettingsTopIn(scroll, section);",
                "if (top == double.MaxValue)",
                "{",
                "section.BringIntoView();",
                "return;",
                "}",
                "scroll.ScrollToVerticalOffset(Math.Max(0, scroll.VerticalOffset + top - PanelSettings.IndexJumpMargin));"), page);
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
            // A search hit on a live row lands in the section that draws it.
            Assert.Equal(0, PanelSettings.SectionOf(PanelSettings.AnchorUnits));
            Assert.Equal(1, PanelSettings.SectionOf(PanelSettings.AnchorFlagsInPitLane));
            Assert.Equal(2, PanelSettings.SectionOf(PanelSettings.AnchorWaterTemp));
            Assert.Equal(3, PanelSettings.SectionOf(PanelSettings.AnchorNightMode));
            Assert.Equal(PanelSettings.SectionAnchors.Length, PanelSettings.RowAnchorsBySection.Length);
            for (var s = 0; s < PanelSettings.RowAnchorsBySection.Length; s++)
            {
                foreach (var anchor in PanelSettings.RowAnchorsBySection[s]) Assert.Equal(s, PanelSettings.SectionOf(anchor));
            }
            // Every Anchor* constant but the sections' is a live row's, filed under its section once.
            var rows = AnchorTable.Of(typeof(PanelSettings)).Select(line => line.Substring(line.IndexOf(" = ", StringComparison.Ordinal) + 3))
                .Where(anchor => !PanelSettings.SectionAnchors.Contains(anchor))
                .OrderBy(a => a, StringComparer.Ordinal);
            Assert.Equal(rows, PanelSettings.RowAnchorsBySection.SelectMany(section => section).OrderBy(a => a, StringComparer.Ordinal));
        }

        /// <summary>
        /// Below 560 of content a control goes under its title: the widest row, the greyed Colour vision with
        /// its four options, title and Soon tag, needs about 510, and 560 clears it by about 50. Never a test
        /// of Narrow, which is about the sidebar and not the room.
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

        /// <summary>
        /// The page asks the rules above where it draws: the stacking rule in SettingsFit and on the rows sized
        /// for it, the fold on the table's surface columns and its folded row, the slider width, and Try on
        /// the live rows alone. The rules are pure and tested on their own; this holds the calls.
        /// </summary>
        [Fact]
        public void The_page_asks_the_layout_rules_where_it_draws()
        {
            var page = Page();
            Assert.Contains("if (!PanelSettings.StacksControls(ContentWidthUpTo(PanelSettings.StackControlsBelow))) return row;", page);
            // SettingsFit and SettingsNew give a row back untouched only when it is not one they can work on.
            Assert.Matches(Lines(
                "if (parts == null || parts.Control == null || grid == null) return row;",
                "grid.RowDefinitions.Add("), page);
            Assert.Matches(Lines(
                "if (parts == null || parts.TitleLine == null) return row;",
                "var tag = Ui.NewTag();"), page);
            Assert.Contains("SettingsFit(Ui.Row(PanelDataTab.DriverNameTitle,", page);
            foreach (var greyed in new[] { "TyreDisplay", "YellowFlags", "DashTheme", "ColourVision", "Colours", "BrandName" })
            {
                Assert.Contains("Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon." + greyed + ".Title,", page);
            }
            Assert.Contains("var surfaces = PanelSettings.AlertSurfacesFit(ContentWidthUpTo(PanelSettings.AlertSurfacesFrom));", page);
            Assert.Matches(@"if \(surfaces\)\s*\{\s*foreach \(var on in alert\.Surfaces\)", page);
            Assert.Matches(@"if \(!surfaces\)\s*\{\s*(//[^\n]*\s*)*folded = Ui\.Soon\(Ui\.SettingRow\(PanelSoon\.AlertDisplay\.Title, null\), PanelSoon\.AlertDisplay\);", page);
            Assert.Matches(@"if \(alert\.Live\)\s*\{\s*var link = Ui\.LinkButton\(PanelSettings\.TryLabel\);", page);
            Assert.Contains("brightness.Width = PanelSettings.SliderWidthFor(ContentWidthUpTo(PanelSettings.SliderWidth));", page);
            Assert.Contains("nightBrightness.Width = PanelSettings.SliderWidthFor(ContentWidthUpTo(PanelSettings.SliderWidth));", page);
            // Every read of the width stops at the threshold it decides (PanelShell.RebuildsOnResize): a bare
            // ContentWidth has no ceiling, and a page that reads it is rebuilt by every settled resize at every
            // width, which commits whatever is in a threshold box and takes the keyboard out of it. The caps
            // stop that only above the largest of them, the fold's 680: the shell keeps the largest cap a build
            // asks for and reads min(680, content) as one continuous width, so below 680 of content every
            // settled resize still rebuilds the page, although it draws differently only at 320, 560 and 680.
            // A read that records only which side of a threshold the build saw is the shell's to add.
            var bare = new Regex(@"(?<![\w.])ContentWidth(?!UpTo)\b");
            Assert.DoesNotMatch(bare, page);
            Assert.Matches(bare, "PanelSettings.StacksControls(ContentWidth)");
            Assert.DoesNotMatch(bare, "PanelSettings.StacksControls(ContentWidthUpTo(PanelSettings.StackControlsBelow))");
            Assert.Equal(4, Regex.Matches(page, @"ContentWidthUpTo\(").Count);
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
        /// SimHub applies its units after its own Settings window has closed and after SimHub's main window has
        /// been activated again, from that window's Apply, or with a new GameManager on a change of game, and
        /// nothing rebuilds the page then: the page reads them again on its tick and, when one moved, sets
        /// every text they decide again in place, so a driver never types a threshold against a stale unit and
        /// a box being typed in is never rebuilt under them. A window's Activated would read them before
        /// SimHub has set them. A failed read is logged once, not once a second.
        /// </summary>
        [Fact]
        public void The_page_follows_simhubs_units_in_place()
        {
            var metric = new[] { "Kmh", "Celcius", "Bar", "Liters" };
            Assert.False(PanelSettings.UnitsMoved(metric, new[] { "Kmh", "Celcius", "Bar", "Liters" }));
            Assert.True(PanelSettings.UnitsMoved(metric, new[] { "Kmh", "Fahrenheit", "Bar", "Liters" }));
            Assert.True(PanelSettings.UnitsMoved(new string[4], metric));
            Assert.False(PanelSettings.UnitsMoved(new string[4], new string[4]));
            Assert.True(PanelSettings.UnitsMoved(metric, null));
            var page = Page();
            // The tick never stands down for the keyboard: it rebuilds nothing, so a box being typed in keeps its
            // text and caret.
            Assert.Matches(@"OnTick\(\(\) =>\s*\{\s*var now = SettingsUnitNames\(\);\s*if \(!PanelSettings\.UnitsMoved\(units, now\)\) return;\s*units = now;\s*foreach \(var follow in settingsUnitsFollow\) follow\(now\);\s*\}\);", page);
            // Set in place, never rebuilt: a rebuild would commit a half-typed number and move the caret, so the
            // tick would have to stand down while any text box had the keyboard.
            Assert.DoesNotContain("RebuildPage(", page);
            Assert.DoesNotContain("Keyboard.FocusedElement", page);
            Assert.DoesNotContain("Activated", page);
            // Emptied before the sections are built, and again when the build is let go of, so a follower of a
            // build -- and the discarded page its texts hold -- never outlives it, on a Go to another page too.
            Assert.Matches(@"var units = SettingsUnitNames\(\);\s*settingsUnitsFollow\.Clear\(\);\s*OnDrop\(\(\) => settingsUnitsFollow\.Clear\(\)\);\s*var sections = ", page);
            // Everything the units decide follows them: the Units line (collapsed when SimHub answers none),
            // the fuel target's unit, the unit after each temperature and both temperature defaults.
            Assert.Equal(4, Regex.Matches(page, @"settingsUnitsFollow\.Add\(").Count);
            Assert.Matches(@"Action<string\[\]> showUnits = now =>\s*\{\s*var line = PanelSettings\.UnitsLine\(now\[0\], now\[1\], now\[2\], now\[3\]\);\s*unitsLine\.Text = line \?\? string\.Empty;\s*unitsLine\.Visibility = line == null \? Visibility\.Collapsed : Visibility\.Visible;\s*\};\s*showUnits\(units\);\s*settingsUnitsFollow\.Add\(showUnits\);", page);
            Assert.Contains("settingsUnitsFollow.Add(now => fuelUnit.Text = PanelSettings.FuelTargetUnit(now[3]));", page);
            Assert.Contains("settingsUnitsFollow.Add(now => unit.Text = PanelSettings.TemperatureUnit(now[1]) ?? string.Empty);", page);
            Assert.Matches(@"settingsUnitsFollow\.Add\(now =>\s*\{\s*oilDefault\.Text = PanelSettings\.ThresholdText\(PanelSettings\.TemperatureDefault\(true, now\[1\]\)\);\s*waterDefault\.Text = PanelSettings\.ThresholdText\(PanelSettings\.TemperatureDefault\(false, now\[1\]\)\);\s*\}\);", page);
            // A failed read is logged once: the flag is set after the warning and cleared by a read that works.
            Assert.Matches(@"if \(!settingsUnitsFailed\) Log\.Warn\(""Reading SimHub's units failed: "" \+ ex\.Message\);\s*settingsUnitsFailed = true;", page);
            Assert.Contains("settingsUnitsFailed = false;", page);
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
            // Every unit the table draws alone after the box has a letter in it. The tyre wear's percent sign
            // has none, so it sits inside the example, "70%", as the panel joins every percentage to its number
            // (a glyph is a PanelIcons path or part of a longer literal).
            Assert.Equal(new[] { "laps", null, null, "", "", "x", "V" }, PanelSettings.Alerts.Select(a => a.Unit));
            Assert.All(PanelSettings.Alerts.Where(a => !string.IsNullOrEmpty(a.Unit)), a => Assert.Matches(@"[\p{L}\p{Nd}]", a.Unit));
            Assert.False(PanelSettings.Alert(PanelSoon.PitWindowOpen.Title).HasThreshold);
            Assert.Equal(new[] { true, true, true, false }, PanelSettings.Alert(PanelSettings.LowFuelTitle).Surfaces);
            Assert.Equal(new[] { false, false, true, false }, PanelSettings.Alert(PanelSettings.OilTempTitle).Surfaces);
            Assert.Equal(new[] { false, false, true, false }, PanelSettings.Alert(PanelSettings.WaterTempTitle).Surfaces);
            Assert.Equal(new[] { true, false, false, true }, PanelSettings.Alert(PanelSoon.TyreWear.Title).Surfaces);
            Assert.Equal(new[] { true, true, true, true }, PanelSettings.Alert(PanelSoon.PitWindowOpen.Title).Surfaces);
            Assert.Equal(new[] { true, false, false, true }, PanelSettings.Alert(PanelSoon.Incidents.Title).Surfaces);
            Assert.Equal(new[] { true, false, true, false }, PanelSettings.Alert(PanelSoon.HybridBatteryLow.Title).Surfaces);
            // What a greyed row's box holds, faded with the row: the artboard's values as the box's text in the
            // field's ink, and where it has none, the artboard's dash, drawn as a stroke in the label ink. Pit
            // window open, an event with no word before its box, still draws the box, as the artboard does.
            Assert.Equal(new string[] { null, null, null, "70%", null, "12", null }, PanelSettings.Alerts.Select(a => a.Example));
            Assert.Equal(8, PanelSettings.NoValueDashWidth);
            Assert.Equal(1.5, PanelSettings.NoValueDashWeight);
            var drawn = Page();
            Assert.Contains("when.Add(box ?? SettingsGreyedBox(alert.Example));", drawn);
            Assert.Matches(@"if \(example != null\) return SettingsNumberField\(example\);\s*return SettingsNoValue\(SettingsNumberField\(string\.Empty\)\);", drawn);
            Assert.Matches(@"new System\.Windows\.Shapes\.Rectangle\s*\{\s*Width = PanelSettings\.NoValueDashWidth,\s*Height = PanelSettings\.NoValueDashWeight,\s*Fill = Ui\.Brush\(Theme\.TextLabel\),\s*\};\s*return SettingsOverlaid\(box, dash\);", drawn);
            Assert.Contains("Ui.HStack(PanelSettings.FuelTargetGap, SettingsGreyedBox(null), fuelUnit)", drawn);
            // The box is drawn on every row, outside the word's condition.
            Assert.Matches(@"if \(alert\.HasThreshold\) when\.Add\(Ui\.Caption\(alert\.Op\)\);\s*when\.Add\(box \?\? SettingsGreyedBox\(alert\.Example\)\);", drawn);
            Assert.Equal(new[] { PanelSoon.TyreWear, PanelSoon.PitWindowOpen, PanelSoon.Incidents, PanelSoon.HybridBatteryLow }.Select(s => s.Title),
                PanelSettings.Alerts.Where(a => !a.Live).Select(a => a.Title));
            Assert.Null(PanelSettings.Alert("Nothing"));
            Assert.Equal(new[] { "Screens", "LEDs", "Matrix", "Races only" }, PanelSettings.SurfaceColumns);
            Assert.Equal("Alert", PanelSettings.AlertColumn);
            // A heading is a noun (voice.md), where the artboard's "When" is an adverb: "Trigger",
            // which also covers Pit window open, an event with no threshold. Search still finds the table by
            // "threshold".
            Assert.Equal("Trigger", PanelSettings.ThresholdColumn);
            Assert.Contains(PanelSearch.Find(PanelSearch.All(), "threshold"), hit => hit.Route.Anchor == PanelSettings.AnchorAlerts);
            Assert.Equal("Try", PanelSettings.TryLabel);
            // Every row is drawn, in this order, and every live row writes the rig-wide setting.
            var page = Page();
            var rows = new[] { "PanelSettings.LowFuelTitle", "PanelSettings.OilTempTitle", "PanelSettings.WaterTempTitle", "PanelSoon.TyreWear.Title", "PanelSoon.PitWindowOpen.Title", "PanelSoon.Incidents.Title", "PanelSoon.HybridBatteryLow.Title" }
                .Select(row => page.IndexOf("PanelSettings.Alert(" + row + ")", StringComparison.Ordinal)).ToList();
            Assert.All(rows, index => Assert.True(index >= 0));
            Assert.Equal(rows.OrderBy(i => i), rows);
            Assert.Contains("Open(PanelPage.Rig, scenario)", page);
        }

        /// <summary>
        /// The table is put together head first and a row at a time: the head on the grid's first row, each
        /// alert on a row of its own that it adds, and its cells from the first column on. The head names the
        /// surface columns only where they are drawn, a greyed name carries its Soon tag, and the ticks and
        /// surface heads sit in the middle of their columns.
        /// </summary>
        [Fact]
        public void The_alert_table_is_put_together_head_first_and_row_by_row()
        {
            var page = Page();
            Assert.Matches(Lines(
                "var row = 0;",
                "SettingsAlertHeader(grid, row++, surfaces);",
                "SettingsAlertRow(grid, row++, PanelSettings.Alert(PanelSettings.LowFuelTitle)"), page);
            Assert.Matches(Lines(
                "private static void SettingsAlertHeader(Grid grid, int row, bool surfaces)",
                "{",
                "grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });",
                "SettingsAlertRule(grid, row);",
                "var column = 0;"), page);
            Assert.Matches(Lines(
                "grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });",
                "if (!last) SettingsAlertRule(grid, row);",
                "var column = 0;"), page);
            Assert.Matches(Lines(
                "SettingsAlertCell(grid, row, column++, Ui.Eyebrow(PanelSettings.ThresholdColumn), null, false);",
                "if (surfaces)",
                "{",
                "foreach (var name in PanelSettings.SurfaceColumns)"), page);
            Assert.Matches(Lines(
                "FrameworkElement nameLine = name;",
                "if (soon != null)",
                "{",
                "var line = new WrapPanel { Orientation = Orientation.Horizontal };",
                "var tag = Ui.SoonTag(soon);"), page);
            Assert.Matches(Lines(
                "line.Children.Add(name);",
                "line.Children.Add(tag);",
                "nameLine = line;"), page);
            // The name cell: the name against the cell's left edge with its caption under it, and a greyed
            // name's tag after it, centred on the name's line.
            Assert.Matches(Lines(
                "nameLine = line;",
                "}",
                "nameLine.HorizontalAlignment = HorizontalAlignment.Left;",
                "var nameCell = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };",
                "nameCell.Children.Add(nameLine);"), page);
            Assert.Matches(Lines(
                "var tag = Ui.SoonTag(soon);",
                "tag.Margin = new Thickness(PanelSettings.TitleTagGap, 0, 0, 0);",
                "tag.VerticalAlignment = VerticalAlignment.Center;",
                "line.Children.Add(name);"), page);
            Assert.Matches(Lines(
                "content.VerticalAlignment = VerticalAlignment.Center;",
                "if (centred) content.HorizontalAlignment = HorizontalAlignment.Center;",
                "else if (align == HorizontalAlignment.Right) content.HorizontalAlignment = HorizontalAlignment.Right;"), page);
        }

        [Fact]
        public void The_surface_columns_fold_away_where_they_do_not_fit()
        {
            Assert.True(PanelSettings.AlertSurfacesFit(680));
            Assert.True(PanelSettings.AlertSurfacesFit(1112));
            Assert.False(PanelSettings.AlertSurfacesFit(679));
            var page = Page();
            // Folded, the row is the name alone: a switch in the off position would say alerts are off.
            Assert.Contains("Ui.Soon(Ui.SettingRow(PanelSoon.AlertDisplay.Title, null), PanelSoon.AlertDisplay)", page);
            Assert.DoesNotContain("Ui.SoonRow(PanelSoon.AlertDisplay)", page);
            // A greyed name wraps its tag under it rather than clipping it at the cell's edge.
            Assert.DoesNotContain("Ui.HStack(8, name, Ui.SoonTag(soon))", page);
            Assert.Contains("var line = new WrapPanel { Orientation = Orientation.Horizontal };", page);
            // Every tick hovers #512, the columns' own ticket, on a greyed row as on a live one: the artboard
            // titles every checkbox "Coming soon · #512".
            Assert.Contains("SettingsAlertCell(grid, row, column++, SettingsCheck(on), PanelSoon.AlertDisplay, true);", page);
            Assert.DoesNotContain("soon ?? PanelSoon.AlertDisplay", page);
            // The surface columns' names are not faded, as the artboard's th is not: the hover and the anchor
            // are set on the cell rather than by a Soon wrapper.
            Assert.Contains("var cell = SettingsAlertCell(grid, row, column++, head, null, true);", page);
            Assert.Contains("cell.ToolTip = PanelSoon.AlertDisplay.Tip;", page);
            Assert.Contains("Ui.Anchor(cell, PanelSoon.AlertDisplay.Anchor);", page);
            Assert.DoesNotContain("SettingsAlertCell(grid, row, column++, head, PanelSoon.AlertDisplay, true);", page);
        }

        /// <summary>The artboard's th width: the names take 30% of the table and the surface columns share the
        /// rest, each at least its heading's width.</summary>
        [Fact]
        public void The_names_take_the_artboards_share_of_the_table()
        {
            Assert.Equal(0.3, PanelSettings.AlertNameShare);
            Assert.Equal(268.2, PanelSettings.AlertNameMaxWidth(894), 6);
            Assert.Equal(PanelSettings.AlertNameMinWidth, PanelSettings.AlertNameMaxWidth(300));
            Assert.True(PanelSettings.AlertNameMaxWidth(PanelSettings.AlertSurfacesFrom - 2) > PanelSettings.AlertNameMinWidth);
            var page = Page();
            Assert.Contains("? new ColumnDefinition { Width = new GridLength(PanelSettings.AlertNameWeight, GridUnitType.Star), MinWidth = PanelSettings.AlertNameMinWidth }", page);
            Assert.Contains(": new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };", page);
            // The 30% is of the table as it is laid out, so a resize re-lays it without a rebuild.
            Assert.Contains("if (surfaces) grid.SizeChanged += (sender, args) => names.MaxWidth = PanelSettings.AlertNameMaxWidth(args.NewSize.Width);", page);
            Assert.Contains("foreach (var column in PanelSettings.SurfaceColumns) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });", page);
            // Measured with the layout rounding the panel draws with (SettingsControl sets UseLayoutRounding),
            // which rounds each tracked glyph's margin: without it "Races only" measures 67.4 and draws 72.
            Assert.Matches(@"head\.UseLayoutRounding = true;\s*head\.Measure\(new Size\(double\.PositiveInfinity, double\.PositiveInfinity\)\);\s*grid\.ColumnDefinitions\[column\]\.MinWidth = Math\.Ceiling\(head\.DesiredSize\.Width\) \+ 2 \* PanelSettings\.AlertCellPaddingX;", page);
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

            var max = PanelSettings.TemperatureMax;
            Assert.Equal(0, PanelSettings.ParseThreshold("", 130, max));
            Assert.Equal(0, PanelSettings.ParseThreshold("  ", 130, max));
            Assert.Equal(0, PanelSettings.ParseThreshold(null, 130, max));
            Assert.Equal(125, PanelSettings.ParseThreshold(" 125 ", 130, max));
            Assert.Equal(130, PanelSettings.ParseThreshold("hot", 130, max));
            Assert.Equal(max, PanelSettings.ParseThreshold("5000", 130, max));
            Assert.Equal(0, PanelSettings.ParseThreshold("-4", 130, max));

            // The boxes' clamps: two digits of laps, and three of temperature, which holds every unit's own
            // default -- a Kelvin rig types 393 for its oil, and a Fahrenheit rig 248 -- without a word.
            Assert.Equal(99, PanelSettings.LowFuelMax);
            Assert.Equal(999, PanelSettings.TemperatureMax);
            Assert.All(Contract.DefaultOilTemp.Values.Concat(Contract.DefaultWaterTemp.Values), value => Assert.InRange(value, 1, PanelSettings.TemperatureMax));
            Assert.InRange(Contract.DefaultFlagBoxLowFuelLaps, 1, PanelSettings.LowFuelMax);

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
        /// Every live control reads and writes its own setting and nothing else, and saves: a control that
        /// reads one setting and writes another, or writes nothing, would otherwise pass every other test on
        /// the page.
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
            // Each control reads the setting it writes, under the name its row draws (Each_row_draws_its_own_control_and_words).
            Assert.Contains("var position = BuildSegmented(Contract.PositionModes, PanelDataTab.PositionLabels, Settings.PositionMode,", page);
            Assert.Contains("var delta = BuildSegmented(Contract.DeltaReferences, PanelDataTab.DeltaLabels, Settings.DeltaReference,", page);
            Assert.Contains("var deltaPrecision = BuildSegmented(Contract.DeltaPrecisions, PanelDataTab.DeltaPrecisionLabels, Settings.DeltaPrecision,", page);
            Assert.Contains("var session = BuildSegmented(Contract.SessionProgressModes, PanelDataTab.SessionLabels, Settings.SessionProgress,", page);
            Assert.Contains("var driverName = BuildSegmented(Contract.DriverNameFormats, PanelDataTab.DriverNameLabels, Settings.DriverNameFormat,", page);
            Assert.Contains("var clock = BuildSegmented(Contract.ClockFormats, PanelDataTab.ClockLabels, Settings.ClockFormat,", page);
            Assert.Contains("var blueFlag = BuildSegmented(Contract.BlueFlagDetails, PanelDataTab.BlueFlagLabels, Settings.BlueFlagDetail,", page);
            Assert.Contains("var teamName = BuildToggle(Settings.DriverNameTeam,", page);
            Assert.Contains("var flagsInPitLane = BuildToggle(Settings.FlagsInPitLane,", page);
            Assert.Contains("var nightMode = BuildToggle(Settings.LightsNightMode,", page);
            // The three alert boxes, each whole: what it reads, its default and clamp, and what it writes.
            Assert.Contains("var lowFuel = Ui.NumberInput(Settings.FlagBoxLowFuelLaps, 0, PanelSettings.LowFuelMax, v => { Settings.FlagBoxLowFuelLaps = v; Save(); });", page);
            Assert.Contains("var oilTemp = SettingsThresholdBox(Settings.LightsOilTemp, PanelSettings.TemperatureDefault(true, temperature), PanelSettings.TemperatureMax, v => { Settings.SetLightsOilTemp(v); Save(); }, out oilDefault);", page);
            Assert.Contains("var waterTemp = SettingsThresholdBox(Settings.LightsWaterTemp, PanelSettings.TemperatureDefault(false, temperature), PanelSettings.TemperatureMax, v => { Settings.SetLightsWaterTemp(v); Save(); }, out waterDefault);", page);
            // Try opens the row's own scenario.
            Assert.Contains("var scenario = alert.ScenarioId;", page);
            // Each slider reads, commits and previews its own brightness: the day's between the two calls, the
            // night's after the second.
            var day = page.IndexOf("var brightness = Ui.Slider(Settings.LightsBrightness,", StringComparison.Ordinal);
            var night = page.IndexOf("var nightBrightness = Ui.Slider(Settings.LightsNightBrightness,", StringComparison.Ordinal);
            Assert.True(day >= 0 && night > day);
            var dayCommit = page.IndexOf("v => { if (v == Settings.LightsBrightness) return; Settings.LightsBrightness = v; Save(); ShowLightingChange(); },", StringComparison.Ordinal);
            var nightCommit = page.IndexOf("v => { if (v == Settings.LightsNightBrightness) return; Settings.LightsNightBrightness = v; Save(); ShowLightingChange(); },", StringComparison.Ordinal);
            Assert.True(dayCommit > day && dayCommit < night, "the day slider's commit");
            Assert.True(nightCommit > night, "the night slider's commit");
            Assert.Single(Regex.Matches(page, @"Settings\.LightsBrightness = v;"));
            Assert.Single(Regex.Matches(page, @"Settings\.LightsNightBrightness = v;"));
            // Each slider's drag repaints the preview with its own brightness, and only while the preview
            // shows the time of day it sets.
            var dayPreview = page.IndexOf("v => { if (!night()) paint(false, v, Settings.LightsNightBrightness); }", StringComparison.Ordinal);
            var nightPreview = page.IndexOf("v => { if (night()) paint(true, Settings.LightsBrightness, v); }", StringComparison.Ordinal);
            Assert.True(dayPreview > day && dayPreview < night, "the day slider's preview");
            Assert.True(nightPreview > night, "the night slider's preview");

            // The preview's Day and Night write nothing and save nothing.
            var pick = Handler(page, "BuildSegmented(PanelSettings.PreviewValues,");
            Assert.Empty(Writes(pick));
            Assert.DoesNotContain("Save()", pick);
        }

        /// <summary>
        /// Every row draws its own control and its own words: a control drawn on another's row, a caption
        /// moved onto another row or dropped, or a title given another's control, would otherwise pass, since
        /// each control's write and each constant's value are held on their own. Each row is pinned whole.
        /// </summary>
        [Fact]
        public void Each_row_draws_its_own_control_and_words()
        {
            var page = Page();
            foreach (var row in new[]
            {
                "Ui.Anchor(SettingsFit(Ui.Row(PanelDataTab.PositionTitle, PanelDataTab.PositionCaption, position)), PanelSettings.AnchorPosition),",
                "Ui.Anchor(SettingsFit(SettingsNew(Ui.Row(PanelDataTab.DeltaTitle, PanelDataTab.DeltaCaption, delta))), PanelSettings.AnchorDelta),",
                "Ui.Anchor(SettingsFit(SettingsNew(Ui.Row(PanelDataTab.DeltaPrecisionTitle, PanelDataTab.DeltaPrecisionCaption, deltaPrecision))), PanelSettings.AnchorDeltaPrecision),",
                "Ui.Anchor(SettingsFit(Ui.Row(PanelDataTab.SessionTitle, PanelDataTab.SessionCaption, session)), PanelSettings.AnchorSession),",
                "Ui.Anchor(SettingsFit(Ui.Row(PanelDataTab.DriverNameTitle, PanelDataTab.DriverNameCaption, driverName)), PanelSettings.AnchorDriverNames),",
                "Ui.Anchor(Ui.Row(PanelDataTab.TeamNameTitle, PanelDataTab.TeamNameCaption, teamName), PanelSettings.AnchorTeamNames),",
                "Ui.Anchor(SettingsFit(SettingsNew(Ui.Row(PanelDataTab.ClockTitle, PanelDataTab.ClockCaption, clock))), PanelSettings.AnchorClock),",
                "Ui.Soon(Ui.SettingRow(PanelSoon.FuelTargetPerLap.Title, fuelTarget), PanelSoon.FuelTargetPerLap),",
                "Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.TyreDisplay.Title, tyres)), PanelSoon.TyreDisplay),",
                "Ui.Anchor(SettingsNew(Ui.Row(PanelSettings.UnitsTitle, PanelSettings.UnitsCaption, unitsLine)), PanelSettings.AnchorUnits));",
                "Ui.Anchor(SettingsFit(Ui.Row(PanelDataTab.BlueFlagTitle, PanelDataTab.BlueFlagCaption, blueFlag)), PanelSettings.AnchorBlueFlag),",
                "Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.YellowFlags.Title, SettingsGreyedChoice(PanelSettings.YellowFlagLabels))), PanelSoon.YellowFlags),",
                "Ui.Anchor(Ui.SettingRow(PanelSettings.FlagsInPitLaneTitle, flagsInPitLane, null, Ui.NewTag()), PanelSettings.AnchorFlagsInPitLane));",
                "SettingsAlertRow(grid, row++, PanelSettings.Alert(PanelSettings.LowFuelTitle), lowFuel, null, temperature, surfaces, null);",
                "SettingsAlertRow(grid, row++, PanelSettings.Alert(PanelSettings.OilTempTitle), oilTemp, PanelSettings.TemperatureCaption, temperature, surfaces, null);",
                "SettingsAlertRow(grid, row++, PanelSettings.Alert(PanelSettings.WaterTempTitle), waterTemp, PanelSettings.TemperatureCaption, temperature, surfaces, null);",
                "Ui.Anchor(SettingsFit(Ui.Row(PanelSettings.BrightnessTitle, PanelSettings.BrightnessCaption, brightness)), PanelSettings.AnchorBrightness),",
                "Ui.Anchor(SettingsFit(Ui.Row(PanelSettings.NightBrightnessTitle, null, nightBrightness)), PanelSettings.AnchorNightBrightness),",
                "Ui.Anchor(Ui.Row(PanelSettings.NightModeTitle, null, nightMode), PanelSettings.AnchorNightMode),",
                "Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.DashTheme.Title, SettingsGreyedChoice(PanelSettings.ThemeLabels))), PanelSoon.DashTheme),",
                "Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.ColourVision.Title, SettingsGreyedChoice(PanelSettings.ColourVisionLabels))), PanelSoon.ColourVision),",
                "Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.Colours.Title, SettingsGreyedChoice(PanelSettings.ColoursLabels))), PanelSoon.Colours));",
                "SettingsHinted(Ui.Input(string.Empty, PanelSettings.NameInputWidth), PanelSettings.FirstNameHint),",
                "SettingsHinted(Ui.Input(string.Empty, PanelSettings.NameInputWidth), PanelSettings.SurnameHint));",
                "Ui.Soon(Ui.SettingRow(PanelSoon.BrandRaceNumber.Title, SettingsHinted(SettingsNumberField(string.Empty), PanelSettings.RaceNumberHint)), PanelSoon.BrandRaceNumber),",
                "Ui.Soon(Ui.SettingRow(PanelSoon.BrandLogo.Title, Ui.Button(PanelSettings.LogoButton, PanelButtonKind.Outline, PanelButtonSize.Small)), PanelSoon.BrandLogo),",
                "Ui.Soon(Ui.SettingRow(PanelSoon.IdleScreenBackground.Title, Ui.Button(PanelSettings.IdleBackgroundButton, PanelButtonKind.Outline, PanelButtonSize.Small)), PanelSoon.IdleScreenBackground));",
            })
            {
                Assert.Contains(row, page);
            }

            // The threshold cell: the word, the box, then the unit -- SimHub's temperature unit where the row
            // has none of its own.
            Assert.Contains("if (alert.HasThreshold) when.Add(Ui.Caption(alert.Op));", page);
            Assert.Matches(@"if \(alert\.Unit == null\)\s*\{\s*var unit = Ui\.Caption\(PanelSettings\.TemperatureUnit\(temperature\) \?\? string\.Empty\);\s*settingsUnitsFollow\.Add\(now => unit\.Text = PanelSettings\.TemperatureUnit\(now\[1\]\) \?\? string\.Empty\);\s*when\.Add\(unit\);\s*\}\s*else if \(alert\.Unit == PanelSettings\.LapsUnit && box is TextBox\)\s*\{[^}]*var unit = Ui\.Caption\(PanelSettings\.LapsUnitFor\(typed\.Text\)\);\s*typed\.TextChanged \+= \(sender, args\) => unit\.Text = PanelSettings\.LapsUnitFor\(typed\.Text\);\s*when\.Add\(unit\);\s*\}\s*else if \(alert\.Unit\.Length > 0\) when\.Add\(Ui\.Caption\(alert\.Unit\)\);", page);
            // A name's caption under it, set off by the fix-detail gap, after the name itself.
            Assert.Matches(@"nameCell\.Children\.Add\(nameLine\);\s*if \(caption != null\)\s*\{\s*var under = Ui\.Caption\(caption\);\s*under\.Margin = new Thickness\(0, PanelKit\.FixDetailGap, 0, 0\);\s*nameCell\.Children\.Add\(under\);\s*\}", page);
            // A greyed name carries its Soon tag after it, and the four surface headings theirs under the name.
            Assert.Matches(@"line\.Children\.Add\(name\);\s*line\.Children\.Add\(tag\);\s*nameLine = line;", page);
            Assert.Matches(@"head\.Children\.Add\(label\);\s*head\.Children\.Add\(tag\);", page);
            // Every cell is placed in its own row and column of the table, and every rule runs under the whole row.
            Assert.Matches(@"Grid\.SetRow\(cell, row\);\s*Grid\.SetColumn\(cell, column\);\s*grid\.Children\.Add\(cell\);\s*return cell;", page);
            Assert.Matches(@"Grid\.SetRow\(rule, row\);\s*Grid\.SetColumnSpan\(rule, Math\.Max\(1, grid\.ColumnDefinitions\.Count\)\);\s*grid\.Children\.Add\(rule\);", page);
            // A live row's Try opens Rig on its scenario and is the cell's content.
            Assert.Matches(@"link\.Click \+= \(sender, args\) => Open\(PanelPage\.Rig, scenario\);\s*tryIt = link;", page);

            // The table's head in the artboard's order: Alert, then Threshold.
            var alertHead = page.IndexOf("SettingsAlertCell(grid, row, column++, Ui.Eyebrow(PanelSettings.AlertColumn), null, false);", StringComparison.Ordinal);
            var thresholdHead = page.IndexOf("SettingsAlertCell(grid, row, column++, Ui.Eyebrow(PanelSettings.ThresholdColumn), null, false);", StringComparison.Ordinal);
            Assert.True(alertHead >= 0 && thresholdHead > alertHead, "the table's head");

            // The greyed rows' words where they are drawn: the fuel unit SimHub's, litres when it cannot say;
            // the tyre buttons main then secondary; every greyed option disabled and the first chosen.
            Assert.Contains("var fuelUnit = Ui.Caption(PanelSettings.FuelTargetUnit(units[3]));", page);
            Assert.Equal("gal", PanelSettings.FuelTargetUnit("Gallons"));
            Assert.Equal("L", PanelSettings.FuelTargetUnit("Liters"));
            Assert.Equal(PanelSettings.FuelTargetUnitFallback, PanelSettings.FuelTargetUnit(null));
            var main = page.IndexOf("Ui.Button(PanelSettings.TyreDisplayLabels[0], PanelButtonKind.Outline, PanelButtonSize.Small),", StringComparison.Ordinal);
            var secondary = page.IndexOf("Ui.Button(PanelSettings.TyreDisplayLabels[1], PanelButtonKind.Outline, PanelButtonSize.Small));", StringComparison.Ordinal);
            Assert.True(main >= 0 && secondary > main, "the tyre buttons");
            Assert.Contains("var options = labels.Select((label, i) => new Segmented.Option(\"option\" + i, label, true));", page);
            Assert.Contains("return new Segmented(options, \"option0\");", page);

            // The preview's Day and Night start on what the preview shows.
            Assert.Contains("var pick = BuildSegmented(PanelSettings.PreviewValues, PanelSettings.PreviewLabels, night() ? PanelSettings.PreviewNight : PanelSettings.PreviewDay, value =>", page);
        }

        /// <summary>
        /// A temperature box commits on Enter and on losing focus -- which the shell's CommitTyping raises on a
        /// rebuild -- through ParseThreshold, writes only when the value moved, and shows the unit's default
        /// as its placeholder. ParseThreshold and ThresholdText are tested on their own; this holds the box's
        /// use of them.
        /// </summary>
        [Fact]
        public void A_temperature_box_commits_on_enter_and_on_leaving_it()
        {
            var page = Page();
            var start = page.IndexOf("private static FrameworkElement SettingsThresholdBox(int? value, int? fallback, int max, Action<int> changed, out TextBlock placeholder)", StringComparison.Ordinal);
            Assert.True(start >= 0);
            var end = page.IndexOf("\n        }\n", start, StringComparison.Ordinal);
            var box = page.Substring(start, end - start);
            Assert.Contains("var box = SettingsNumberField(PanelSettings.ThresholdText(value));", box);
            Assert.Contains("var last = value ?? 0;", box);
            Assert.Contains("var next = PanelSettings.ParseThreshold(box.Text, last, max);", box);
            Assert.Contains("box.Text = PanelSettings.ThresholdText(next);", box);
            Assert.Matches(@"if \(next == last\) return;\s*last = next;\s*changed\(next\);", box);
            Assert.Contains("box.LostFocus += (sender, args) => commit();", box);
            Assert.Contains("if (args.Key == Key.Enter) commit();", box);
            Assert.Matches(@"placeholder = SettingsHint\(box, PanelSettings\.ThresholdText\(fallback\)\);\s*return SettingsOverlaid\(box, placeholder\);", box);

            // Every box the driver types a number in -- both temperatures and Low fuel -- takes its next digit
            // after its value when it is focused by Tab or by the shell's focus restore after a rebuild, whose
            // new box starts with its caret at 0: "13", a rebuild, then "0" would read 013, which is 13.
            Assert.Matches(Lines(
                "var box = SettingsNumberField(PanelSettings.ThresholdText(value));",
                "SettingsTypeAtEnd(box);"), box);
            Assert.Matches(Lines(
                "var lowFuel = Ui.NumberInput(Settings.FlagBoxLowFuelLaps, 0, PanelSettings.LowFuelMax, v => { Settings.FlagBoxLowFuelLaps = v; Save(); });",
                "SettingsTypeAtEnd(lowFuel);"), page);
            Assert.Matches(Lines(
                "private static void SettingsTypeAtEnd(TextBox box)",
                "{",
                "box.GotKeyboardFocus += (sender, args) =>",
                "{",
                "if (Mouse.LeftButton != MouseButtonState.Pressed) box.CaretIndex = box.Text.Length;",
                "};",
                "}"), page);
            Assert.Equal(2, Regex.Matches(page, @"SettingsTypeAtEnd\(\w+\);").Count);
        }

        /// <summary>
        /// SimHub's units are read in the order the page uses them -- speed, temperature, pressure, fuel -- and
        /// each temperature box shows its own default: the oil's on the oil box and the water's on the water's.
        /// </summary>
        [Fact]
        public void Each_unit_and_default_goes_where_it_belongs()
        {
            var page = Page();
            var order = new[] { "units.LocalSpeedUnitString,", "units.LocalTemperatureUnitString,", "units.LocalPressureUnitString,", "units.LocalFuelUnitString," }
                .Select(read => page.IndexOf(read, StringComparison.Ordinal)).ToList();
            Assert.All(order, index => Assert.True(index >= 0));
            Assert.Equal(order.OrderBy(i => i), order);
            Assert.DoesNotMatch(@"units\.Local\w+Unit\.ToString\(\)", page);
            // The units are SimHub's own, read through the PluginManager from its GameManager's unit settings, and
            // a read SimHub cannot answer, or one that throws, gives four nulls.
            Assert.Matches(Lines(
                "var manager = plugin == null ? null : plugin.PluginManager;",
                "var game = manager == null ? null : manager.GameManager;",
                "var units = game == null ? null : game.GameUnitSettings;",
                "var names = units == null ? new string[4] : new[]"), page);
            Assert.Matches(Lines(
                "settingsUnitsFailed = true;",
                "return new string[4];"), page);
            Assert.Contains("PanelSettings.UnitsLine(now[0], now[1], now[2], now[3])", page);
            Assert.Contains("showUnits(units);", page);
            Assert.Contains("var temperature = units[1];", page);
            Assert.Contains("PanelSettings.FuelTargetUnit(units[3])", page);
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
            // By night the night brightness alone, whatever the day one is: not the one scaled by the other.
            Assert.Equal(0.25, PanelSettings.PreviewLevel(true, 60, 25));
            Assert.Equal("25%", PanelSettings.PreviewPercent(true, 60, 25));
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

            // The wiring: the repaint reads the day brightness then the night one, as PreviewLevel takes them;
            // the paint dims the strip and the matrix alike and writes the numeral for the time it shows; and
            // a press of Day or Night repaints at once.
            Assert.Contains("Action repaint = () => paint(night(), Settings.LightsBrightness, Settings.LightsNightBrightness);", page);
            Assert.Matches(@"Action<bool, int, int> paint = \(atNight, day, dark\) =>\s*\{\s*var level = PanelSettings\.PreviewLevel\(atNight, day, dark\);\s*Ui\.Redim\(strip, level\);\s*Ui\.Redim\(matrix, level\);\s*percent\.Text = PanelSettings\.PreviewPercent\(atNight, day, dark\);\s*\};", page);
            Assert.Matches(@"settingsPreviewPickedUnder = Settings\.LightsNightMode;\s*repaint\(\);", page);
            // The build paints once itself, or a page opened at night would show an undimmed preview with no
            // numeral until the next change of lighting.
            Assert.Matches(@"Action repaint = \(\) => paint\(night\(\), Settings\.LightsBrightness, Settings\.LightsNightBrightness\);\s*repaint\(\);\s*OnLighting\(repaint\);", page);
            // Rounded, not truncated: 29 would otherwise read 28%, and 57 read 56%.
            Assert.Equal("29%", PanelSettings.PreviewPercent(false, 29, 25));
            Assert.Equal("57%", PanelSettings.PreviewPercent(true, 100, 57));
            Assert.Matches(@"return PanelSettings\.ShowsNight\(settingsPreviewPick, Settings\.LightsNightMode\);", page);
        }

        /// <summary>
        /// What the page's own helpers do, beyond where they are called: a placeholder shows only while its box
        /// is empty and follows every keystroke; a stacked control goes on the row under its title across both
        /// columns; the night-mode chip says "Shortcuts" rather than "Not bound" when SimHub's mappings cannot be
        /// read, at the .key size either way; and the alert table sits in its card.
        /// </summary>
        [Fact]
        public void The_pages_helpers_do_what_their_rows_rely_on()
        {
            var page = Page();
            Assert.Contains("Action show = () => hint.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;", page);
            Assert.Matches(@"show\(\);\s*box\.TextChanged \+= \(sender, args\) => show\(\);", page);
            Assert.Contains("hint.IsHitTestVisible = false;", page);
            // The box and its placeholder share one cell of a grid as wide as the box, the placeholder over the
            // box's text and centred in its height: drop either and a threshold box loses its field or the
            // unit's default it shows while empty, and a greyed box its dash.
            Assert.Matches(Lines(
                "hint.VerticalAlignment = VerticalAlignment.Center;",
                "hint.HorizontalAlignment = box.TextAlignment == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left;",
                "hint.Margin = new Thickness(box.Padding.Left + PanelMetrics.BorderWeight + PanelSettings.HintInset, 0, box.Padding.Right + PanelMetrics.BorderWeight + PanelSettings.HintInset, 0);",
                "hint.IsHitTestVisible = false;",
                "Action show = () => hint.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;",
                "show();",
                "box.TextChanged += (sender, args) => show();",
                "var grid = new Grid { Width = box.Width, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };",
                "grid.Children.Add(box);",
                "grid.Children.Add(hint);",
                "return grid;"), page);

            Assert.Matches(@"grid\.RowDefinitions\.Add\(new RowDefinition \{ Height = GridLength\.Auto \}\);\s*grid\.RowDefinitions\.Add\(new RowDefinition \{ Height = GridLength\.Auto \}\);\s*Grid\.SetRow\(parts\.Control, 1\);\s*Grid\.SetColumn\(parts\.Control, 0\);\s*Grid\.SetColumnSpan\(parts\.Control, 2\);", page);

            Assert.Contains("if (triggers == null) chip = Ui.BindingChip(PanelShortcuts.Title, null, open, true);", page);
            Assert.Contains("chip = Ui.BindingChip(text ?? Ui.NotBound, text != null, open, true);", page);
            Assert.Contains("chip.ToolTip = PanelBindings.ChipTooltip;", page);

            Assert.Contains("return PageSection(null, true, PanelKit.SectionHeadingGapSettings, heading, Ui.CardBox(grid, 0), folded);", page);

            // SettingsNew puts the New tag on the row's title line, which is all it is for.
            Assert.Matches(@"var tag = Ui\.NewTag\(\);\s*tag\.Margin = new Thickness\(PanelSettings\.TitleTagGap, 0, 0, 0\);\s*tag\.VerticalAlignment = VerticalAlignment\.Center;\s*parts\.TitleLine\.Children\.Add\(tag\);\s*return row;", page);

            // The artboards' .num-in, whole: 64 wide, 8 in, Barlow Condensed SemiBold at 15, right-aligned.
            Assert.Matches(@"var box = Ui\.Input\(text, PanelShell\.NumberInputWidth\);\s*box\.Padding = new Thickness\(PanelShell\.NumberInputPaddingX - PanelMetrics\.BorderWeight, 0, PanelShell\.NumberInputPaddingX - PanelMetrics\.BorderWeight, 0\);\s*box\.FontFamily = PanelFonts\.Data;\s*box\.FontWeight = FontWeight\.FromOpenTypeWeight\(PanelSettings\.NumberFieldFontWeight\);\s*box\.FontSize = PanelShell\.NumberInputTextSize;\s*box\.HorizontalContentAlignment = HorizontalAlignment\.Right;\s*box\.TextAlignment = TextAlignment\.Right;\s*return box;", page);
            // A placeholder in the label ink, which is what tells an empty temperature box (the unit's default,
            // following SimHub's unit) from a typed number, in the box's own face and alignment.
            Assert.Matches(@"return new TextBlock\s*\{\s*Text = hint,\s*FontFamily = box\.FontFamily,\s*FontWeight = box\.FontWeight,\s*FontSize = box\.FontSize,\s*Foreground = Ui\.Brush\(Theme\.TextLabel\),\s*TextAlignment = box\.TextAlignment,\s*\};", page);
            Assert.Contains("hint.HorizontalAlignment = box.TextAlignment == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left;", page);

            // The alert table: a rule under the head and under every row but the last, as the artboard's
            // tr:last-child td has none; a name that wraps; Try against the right edge; every cell answering the
            // pointer across its whole ground; and a greyed row's name faded and hovering its ticket as its
            // other cells are.
            Assert.Matches(@"grid\.RowDefinitions\.Add\(new RowDefinition \{ Height = GridLength\.Auto \}\);\s*SettingsAlertRule\(grid, row\);\s*var column = 0;\s*SettingsAlertCell\(grid, row, column\+\+, Ui\.Eyebrow\(PanelSettings\.AlertColumn\)", page);
            Assert.Contains("if (!last) SettingsAlertRule(grid, row);", page);
            Assert.Contains("SettingsAlertRow(grid, row++, PanelSettings.Alert(PanelSoon.HybridBatteryLow.Title), null, null, temperature, surfaces, PanelSoon.HybridBatteryLow, true);", page);
            Assert.Single(Regex.Matches(page, @"SettingsAlertRow\(grid, row\+\+, [^\n]*, true\);"));
            Assert.Contains("name.TextWrapping = TextWrapping.Wrap;", page);
            Assert.Contains("SettingsAlertCell(grid, row, column, tryIt ?? new Border(), soon, false, HorizontalAlignment.Right);", page);
            Assert.Contains("else if (align == HorizontalAlignment.Right) content.HorizontalAlignment = HorizontalAlignment.Right;", page);
            Assert.Matches(@"Background = Brushes\.Transparent,\s*Child = content,", page);
            Assert.Contains("SettingsAlertCell(grid, row, column++, nameCell, soon, false);", page);
            // A live alert's name cell carries its row's anchor, which its search entry lands on.
            Assert.Matches(Lines(
                "var nameBox = SettingsAlertCell(grid, row, column++, nameCell, soon, false);",
                "if (alert.Anchor != null) Ui.Anchor(nameBox, alert.Anchor);"), page);

            // A link of the On this page row: a hand over it, the panel's focus ring, the zone ground under the
            // pointer, and back to its mark when the pointer leaves.
            Assert.Matches(@"Cursor = Cursors\.Hand,\s*Content = label,\s*Template = new ControlTemplate\(typeof\(Button\)\) \{ VisualTree = chrome \},\s*FocusVisualStyle = Ui\.FocusRing\(\),", page);
            Assert.Contains("link.MouseEnter += (sender, args) => SettingsInkIndexLink(link, true);", page);
            Assert.Contains("link.MouseLeave += (sender, args) => SettingsInkIndexLink(link, (bool)link.Tag);", page);
            Assert.Contains("SettingsInkIndexLink(link, current || link.IsMouseOver);", page);
            // A heading a link focuses shows the panel's focus ring.
            Assert.Matches(@"KeyboardNavigation\.SetIsTabStop\(heading, false\);\s*heading\.FocusVisualStyle = Ui\.FocusRing\(\);", page);
            // A stacked control goes under its title across both columns, against the left edge.
            Assert.Matches(@"Grid\.SetRow\(parts\.Control, 1\);\s*Grid\.SetColumn\(parts\.Control, 0\);\s*Grid\.SetColumnSpan\(parts\.Control, 2\);\s*parts\.Control\.HorizontalAlignment = HorizontalAlignment\.Left;", page);
            // A surface heading and its tag centred over their column; Try as tall as its text, not the link's
            // own height; the preview's three pictures on one line, 40 apart and centred in its height.
            Assert.Matches(@"var head = new StackPanel \{ Orientation = Orientation\.Vertical, HorizontalAlignment = HorizontalAlignment\.Center \};\s*var label = Ui\.Eyebrow\(name\);\s*label\.HorizontalAlignment = HorizontalAlignment\.Center;\s*var tag = Ui\.SoonTag\(PanelSoon\.AlertDisplay\);\s*tag\.HorizontalAlignment = HorizontalAlignment\.Center;", page);
            Assert.Matches(@"link\.FontSize = PanelSettings\.AlertTryTextSize;\s*link\.Height = double\.NaN;", page);
            Assert.Matches(Lines(
                "var stage = new StackPanel { Orientation = Orientation.Horizontal };",
                "var pictures = new FrameworkElement[] { strip, matrix, percent };",
                "for (var i = 0; i < pictures.Length; i++)",
                "{",
                "pictures[i].VerticalAlignment = VerticalAlignment.Center;",
                "var gap = i < pictures.Length - 1 ? PanelSettings.PreviewStageGap : 0;",
                "pictures[i].Margin = new Thickness(0, 0, gap, 0);",
                "stage.Children.Add(pictures[i]);",
                "}"), page);

            // The preview's head: its name on the left and Day|Night against the right edge.
            Assert.Contains("var head = new DockPanel { LastChildFill = false };", page);
            Assert.Contains("DockPanel.SetDock(eyebrow, Dock.Left);", page);
            Assert.Contains("DockPanel.SetDock(pick, Dock.Right);", page);
            Assert.Matches(@"head\.Children\.Add\(eyebrow\);\s*head\.Children\.Add\(pick\);", page);
            // The stage draws all three pictures, in the artboard's order.
            Assert.Contains("var pictures = new FrameworkElement[] { strip, matrix, percent };", page);
            Assert.Contains("stage.Children.Add(pictures[i]);", page);
        }

        /// <summary>The artboard's preview: a 3·9·3 strip of 20 px LEDs, a yellow on the ends and the revs in the
        /// centre -- three green, three amber, one red, two unlit -- and the gear on an 8x8 of 9 px cells.</summary>
        [Fact]
        public void The_preview_is_drawn_at_the_artboards_sizes()
        {
            Assert.Equal(3, PanelSettings.PreviewEnds);
            Assert.Equal(PanelEmulation.Yellow, PanelSettings.PreviewStripScenario);
            Assert.Equal(PanelEmulation.Mid, PanelSettings.PreviewMatrixScenario);
            Assert.Equal("gear", PanelSettings.PreviewMatrixOptions().Rest);
            Assert.Contains("gear", Contract.FlagBoxRests);
            Assert.Equal(20, PanelSettings.PreviewStrip.Led);
            Assert.Equal(3, PanelSettings.PreviewStrip.Radius);
            Assert.Equal(3, PanelSettings.PreviewStrip.Gap);
            Assert.Equal(7, PanelSettings.PreviewStrip.GroupGap);
            Assert.Null(PanelSettings.PreviewStrip.GroundHex);
            // No ground, border, padding or corner of their own: the stage's inset ground is theirs.
            Assert.Equal(0, PanelSettings.PreviewStrip.PadX);
            Assert.Equal(0, PanelSettings.PreviewStrip.PadY);
            Assert.Null(PanelSettings.PreviewStrip.BorderHex);
            Assert.Equal(0, PanelSettings.PreviewStrip.CornerRadius);
            Assert.Equal(Theme.SurfaceRaised, PanelSettings.PreviewStrip.UnlitHex);
            Assert.Equal(9, PanelSettings.PreviewMatrix.Cell);
            Assert.Equal(2, PanelSettings.PreviewMatrix.Gap);
            Assert.Equal(0, PanelSettings.PreviewMatrix.Pad);
            Assert.Null(PanelSettings.PreviewMatrix.GroundHex);
            Assert.Null(PanelSettings.PreviewMatrix.BorderHex);
            Assert.Equal(0, PanelSettings.PreviewMatrix.CornerRadius);
            Assert.Equal(Theme.SurfaceZone, PanelSettings.PreviewMatrix.UnlitHex);
            Assert.Equal(40, PanelSettings.PreviewStageGap);
            Assert.Equal(18, PanelSettings.PreviewStagePadding);
            // The stage scales down where it does not fit, never up, and never wraps into a lopsided second line:
            // Ui.FitWidth, which PanelKitTests holds to Uniform, DownOnly and set left, as every fixed-size
            // picture on the panel is. The artboard centres it, a departure plugin.md's Settings section records.
            var page = Page();
            Assert.Contains("Child = Ui.FitWidth(stage),", page);
            Assert.DoesNotContain("new Viewbox", page);
            Assert.DoesNotContain("var stage = new WrapPanel", page);
            Assert.True(PanelSettings.IndexPaddingBottom - PanelSettings.IndexGap >= 0);
            Assert.Equal(22, PanelSettings.PreviewPercentSize);
            Assert.Equal(56, PanelSettings.PreviewPercentWidth);
            Assert.Equal(320, PanelSettings.SliderWidth);
            // The ends are the flag and the centre the revs, so both show the dim.
            var frame = PanelSettings.PreviewStripFrame();
            Assert.Equal(3, frame.Length);
            Assert.Equal(new[] { Theme.FlagYellow, Theme.FlagYellow, Theme.FlagYellow }, frame[0]);
            Assert.Equal(new[] { Theme.FlagYellow, Theme.FlagYellow, Theme.FlagYellow }, frame[2]);
            Assert.Equal(new[] { Theme.ShiftStage1, Theme.ShiftStage1, Theme.ShiftStage1, Theme.ShiftStage2, Theme.ShiftStage2, Theme.ShiftStage2, Theme.ShiftStage3, null, null }, frame[1]);
            Assert.Contains("var strip = Ui.Strip(PanelSettings.PreviewStripFrame(), PanelSettings.PreviewStrip);", page);
            // The numeral against the right of its 56 with tabular figures, so "25%" and "100%" stand in one place.
            Assert.Matches(@"percent\.Width = PanelSettings\.PreviewPercentWidth;\s*percent\.TextAlignment = TextAlignment\.Right;\s*Typography\.SetNumeralAlignment\(percent, FontNumeralAlignment\.Tabular\);", page);
            Assert.Contains("var matrix = Ui.Matrix(PanelEmulation.MatrixFrame(GlyphSheet, PanelSettings.PreviewMatrixScenario, PanelSettings.PreviewMatrixOptions()), PanelSettings.PreviewMatrix);", page);
        }

        /// <summary>The night-mode button's chip opens the binding's own row on Shortcuts, at the .key size.</summary>
        [Fact]
        public void The_night_mode_button_opens_its_binding()
        {
            var page = Page();
            Assert.Contains("SettingsBindingKey(Contract.ToggleNightModeAction)", page);
            // The chip is as wide as SimHub's name for the trigger, so below 560 it goes under its title.
            Assert.Contains("SettingsFit(SettingsNew(Ui.Row(PanelSettings.NightModeButtonTitle,", page);
            Assert.Contains("Go(PanelPage.Shortcuts, PanelBindings.Anchor(action))", page);
            Assert.Contains(Contract.ToggleNightModeAction, Contract.RigActionNames());
            // What the chip reads: the bindings of the action it was handed, and SimHub's name for them, or Not
            // bound when there are none. The page draws the chip itself for the .key size, so no shell test
            // covers these lines.
            Assert.Matches(Lines(
                "var triggers = TriggersOf(action);",
                "Action open = () => Go(PanelPage.Shortcuts, PanelBindings.Anchor(action));"), page);
            Assert.Matches(Lines(
                "var text = PanelBindings.ChipText(triggers);",
                "chip = Ui.BindingChip(text ?? Ui.NotBound, text != null, open, true);"), page);
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
            // A section's heading lands on its section, and a live row on its own row, never on its section's
            // top, which a section taller than the view would leave the row below.
            for (var s = 0; s < PanelSettings.SectionTitles.Length; s++)
            {
                Assert.Equal(PanelSettings.SectionAnchors[s], PanelSettings.Search.Single(entry => entry.Label == PanelSettings.SectionTitles[s]).Route.Anchor);
            }
            var rowEntries = PanelSettings.Search.Where(entry => !PanelSettings.SectionTitles.Contains(entry.Label)).ToList();
            Assert.Equal(16, rowEntries.Count);
            Assert.All(rowEntries, entry => Assert.DoesNotContain(entry.Route.Anchor, PanelSettings.SectionAnchors));
            Assert.Equal(rowEntries.Count, rowEntries.Select(entry => entry.Route.Anchor).Distinct().Count());
            Assert.Equal(PanelSettings.AnchorOilTemp, PanelSettings.Search.Single(entry => entry.Label == PanelSettings.OilTempTitle).Route.Anchor);
            Assert.Equal(PanelSettings.AnchorUnits, PanelSettings.Search.Single(entry => entry.Label == PanelSettings.UnitsTitle).Route.Anchor);
            // The three live alerts land on the anchor their name cell carries.
            foreach (var alert in PanelSettings.Alerts)
            {
                if (!alert.Live) Assert.Null(alert.Anchor);
                else Assert.Equal(alert.Anchor, PanelSettings.Search.Single(entry => entry.Label == alert.Title).Route.Anchor);
            }
            Assert.NotEmpty(PanelSearch.Find(PanelSearch.All(), "night mode button"));
            // The words a driver may search for each entry by, where its label does not say them: every entry's,
            // in the order the page lists them, so a word dropped or loosened is a test that fails.
            var keywords = new[]
            {
                new[] { PanelDataTab.SectionTitle },
                new[] { PanelDataTab.PositionTitle, "overall", "class" },
                new[] { PanelDataTab.DeltaTitle, "session best", "all-time best", "last lap" },
                new[] { PanelDataTab.DeltaPrecisionTitle, "hundredths", "thousandths", "decimals" },
                new[] { PanelDataTab.SessionTitle, "laps", "time" },
                new[] { PanelDataTab.DriverNameTitle, "name format", "surname" },
                new[] { PanelDataTab.TeamNameTitle, "team" },
                new[] { PanelDataTab.ClockTitle, "24h", "12h", "time of day" },
                new[] { PanelSettings.UnitsTitle, "km/h", "mph", "celsius", "fahrenheit", "litres", "gallons" },
                new[] { PanelSettings.FlagsTitle },
                new[] { PanelDataTab.BlueFlagTitle, "blue flag" },
                new[] { PanelSettings.FlagsInPitLaneTitle, "pit", "band d" },
                new[] { PanelSettings.AlertsTitle, "warning", "threshold", "trigger" },
                new[] { PanelSettings.LowFuelTitle, "warning", "laps", "fuel" },
                new[] { PanelSettings.OilTempTitle, "warning", "threshold", "hot" },
                new[] { PanelSettings.WaterTempTitle, "warning", "threshold", "coolant", "hot" },
                new[] { PanelSettings.LightingTitle, "lights", "preview" },
                new[] { PanelSettings.BrightnessTitle, "lights", "leds", "dim" },
                new[] { PanelSettings.NightBrightnessTitle, "night", "dim" },
                new[] { PanelSettings.NightModeTitle, "dark", "dim" },
                new[] { PanelSettings.AppearanceTitle, "look", "theme", "colours" },
                new[] { PanelSettings.DriverTitle, "branding", "logo", "race number" },
            };
            Assert.Equal(keywords.Select(pair => pair[0]), labels);
            foreach (var pair in keywords)
            {
                Assert.Equal(pair.Skip(1), PanelSettings.Search.Single(entry => entry.Label == pair[0]).Keywords);
            }
            Assert.Equal(PanelSettings.AnchorUnits, PanelSearch.Find(PanelSearch.All(), "fahrenheit").First().Route.Anchor);
            Assert.Equal(PanelSettings.AnchorDriver, PanelSearch.Find(PanelSearch.All(), "branding").First().Route.Anchor);

            // Each entry lands in the section whose own method draws its label's constant, so a hit marks that
            // section's link; and a row's entry lands on the anchor that section wraps the row in.
            var page = Page();
            var constants = new[] { typeof(PanelSettings), typeof(PanelDataTab) }
                .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(field => field.IsLiteral && field.FieldType == typeof(string) && !field.Name.StartsWith("Anchor", StringComparison.Ordinal))
                    .Select(field => new { Name = type.Name + "." + field.Name, Value = (string)field.GetValue(null) }))
                .ToList();
            foreach (var entry in PanelSettings.Search)
            {
                var names = constants.Where(c => c.Value == entry.Label).Select(c => c.Name).ToList();
                Assert.NotEmpty(names);
                var drawnIn = Enumerable.Range(0, SectionMethods.Length)
                    .Where(section => names.Any(name => Regex.IsMatch(SectionBody(page, section), Regex.Escape(name) + @"\b")))
                    .ToList();
                Assert.True(drawnIn.Count == 1, entry.Label + " is drawn in " + drawnIn.Count + " sections");
                Assert.True(PanelSettings.SectionOf(entry.Route.Anchor) == drawnIn[0], entry.Label + " routes to " + entry.Route.Anchor);
                if (PanelSettings.SectionAnchors.Contains(entry.Route.Anchor)) continue;
                var body = SectionBody(page, drawnIn[0]);
                var anchorName = AnchorName(entry.Route.Anchor);
                var wrapped = names.Any(name => Regex.IsMatch(body, @"Ui\.Anchor\((SettingsFit\(|SettingsNew\()*Ui\.(Row|SettingRow)\(" + Regex.Escape(name) + @",[^\n]*\), PanelSettings\." + anchorName + @"\)"));
                var alertRow = PanelSettings.Alerts.Any(alert => alert.Title == entry.Label && alert.Anchor == entry.Route.Anchor);
                Assert.True(wrapped || alertRow, entry.Label + " is drawn under " + anchorName);
            }
        }
    
        /// <summary>Low fuel's unit agrees with the number in its box: "under [1] lap", "under [2]
        /// laps", and the plural for nothing readable, as it is typed.</summary>
        [Fact]
        public void Low_fuels_unit_is_lap_or_laps_by_the_value()
        {
            Assert.Equal("lap", PanelSettings.LapsUnitFor(1));
            Assert.Equal("laps", PanelSettings.LapsUnitFor(2));
            Assert.Equal("laps", PanelSettings.LapsUnitFor(0));
            Assert.Equal("laps", PanelSettings.LapsUnitFor(PanelSettings.LowFuelMax));
            Assert.Equal("lap", PanelSettings.LapsUnitFor(" 1 "));
            Assert.Equal("laps", PanelSettings.LapsUnitFor("12"));
            Assert.Equal("laps", PanelSettings.LapsUnitFor(string.Empty));
            Assert.Equal("laps", PanelSettings.LapsUnitFor((string)null));
            Assert.Equal(PanelSettings.LapsUnit, PanelSettings.Alert(PanelSettings.LowFuelTitle).Unit);
        }
    }
}
