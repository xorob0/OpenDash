// SettingsControl.Settings.cs: the Settings page, drawn to Settings.dc.html -- only what is the same everywhere:
// the race data, the flags, the alert thresholds, the lighting, and the appearance and driver rows the
// tickets behind them will fill.
//
// A lap time compares against the same lap on the rim as it does on the pit wall, so these are not per screen.
// Every word and every decision is PanelSettings' or PanelDataTab's, where PanelSettingsTests and
// PanelDataTabTests hold them; this file only draws. Every control writes its setting at once and saves.
// The page draws night mode and both brightnesses as controls, so it calls DrawsLighting and is built again
// when either moves, the wheel's buttons included; the one thing it reads of SimHub while building is its
// units, which are settings.
//
// The delta's two rows stay adjacent Ui.Row calls: PanelDataTabTests holds that the precision row sits
// directly under the reference it qualifies (#322).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>The lighting preview's Day or Night, which writes nothing; null follows night mode. Kept
        /// outside the build so a rebuild keeps it, and let go of when the page is left.</summary>
        private bool? settingsPreviewPick;

        private FrameworkElement BuildSettingsPage(PanelRoute to)
        {
            DrawsLighting();
            OnLeave("Settings.previewPick", () => settingsPreviewPick = null);
            var units = SettingsUnitNames();
            var sections = new FrameworkElement[]
            {
                Ui.Anchor(SettingsRaceData(units), PanelSettings.AnchorRaceData),
                Ui.Anchor(SettingsFlags(), PanelSettings.AnchorFlags),
                Ui.Anchor(SettingsAlerts(units), PanelSettings.AnchorAlerts),
                Ui.Anchor(SettingsLighting(), PanelSettings.AnchorLighting),
                Ui.Anchor(SettingsAppearance(), PanelSettings.AnchorAppearance),
                Ui.Anchor(SettingsDriver(), PanelSettings.AnchorDriver),
            };
            var all = new List<UIElement> { SettingsIndex(sections, to) };
            all.AddRange(sections);
            return PageLayout(PanelSettings.Title, null, all.ToArray());
        }

        // --- On this page -----------------------------------------------------------------------------

        /// <summary>
        /// The row of links under the title, one per section: a press scrolls its section to the top, and the
        /// link of the section being read is marked as the page scrolls.
        /// </summary>
        private FrameworkElement SettingsIndex(IList<FrameworkElement> sections, PanelRoute to)
        {
            var links = new List<Button>();
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
            ScrollViewer scroll = null;
            var held = -1;
            Action<int> mark = current =>
            {
                for (var k = 0; k < links.Count; k++) SettingsPaintIndexLink(links[k], k == current);
            };
            for (var i = 0; i < PanelSettings.SectionTitles.Length; i++)
            {
                var index = i;
                var link = SettingsIndexLink(PanelSettings.SectionTitles[i]);
                link.Margin = new Thickness(0, 0, PanelSettings.IndexGap, PanelSettings.IndexGap);
                link.Click += (sender, args) =>
                {
                    held = index;
                    mark(index);
                    SettingsJumpTo(scroll, sections[index]);
                };
                links.Add(link);
                wrap.Children.Add(link);
            }
            mark(PanelSettings.SectionOf(to == null ? null : to.Anchor));

            var row = new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, 0, 0, PanelMetrics.BorderWeight),
                Padding = new Thickness(0, 0, 0, PanelSettings.IndexPaddingBottom - PanelSettings.IndexGap),
                // PageLayout puts the page's section gap over every section; the artboard's row sits 18 under
                // the title.
                Margin = new Thickness(0, PanelSettings.IndexTop - PanelShell.SectionGapFor(PanelPage.Settings), 0, 0),
                Child = wrap,
            };

            ScrollChangedEventHandler follow = (sender, args) =>
            {
                // ScrollChanged bubbles, and a text box on the page has a scroller of its own.
                if (scroll == null || !ReferenceEquals(args.OriginalSource, scroll)) return;
                if (args.VerticalChange == 0 && args.ViewportHeightChange == 0 && args.ExtentHeightChange == 0) return;
                var tops = sections.Select(section => SettingsTopIn(scroll, section)).ToList();
                var atEnd = scroll.ScrollableHeight > 0 && scroll.VerticalOffset >= scroll.ScrollableHeight - 1;
                var current = PanelSettings.CurrentSection(tops, PanelSettings.IndexReadLine, scroll.ViewportHeight, atEnd, held);
                if (current != held) held = -1;
                mark(current);
            };
            row.Loaded += (sender, args) =>
            {
                if (scroll != null) return;
                scroll = SettingsScrollOf(row);
                if (scroll != null) scroll.ScrollChanged += follow;
            };
            OnDrop(() =>
            {
                if (scroll != null) scroll.ScrollChanged -= follow;
            });
            return row;
        }

        /// <summary>One link of the row: the artboard's .idx, 30 high, 12 in, 14 px Medium in the secondary ink,
        /// on the zone ground in the primary ink when it is the section being read or under the pointer.</summary>
        private static Button SettingsIndexLink(string text)
        {
            var chrome = new FrameworkElementFactory(typeof(Border));
            chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            chrome.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(Theme.Radius));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            chrome.AppendChild(presenter);
            var label = Ui.Text(text, PanelSettings.IndexLinkTextSize, FontWeights.Medium, Theme.TextSecondary);
            var link = new Button
            {
                Height = PanelSettings.IndexLinkHeight,
                Padding = new Thickness(PanelSettings.IndexLinkPaddingX, 0, PanelSettings.IndexLinkPaddingX, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Content = label,
                Template = new ControlTemplate(typeof(Button)) { VisualTree = chrome },
                FocusVisualStyle = Ui.FocusRing(),
                Tag = false,
            };
            link.MouseEnter += (sender, args) => SettingsInkIndexLink(link, true);
            link.MouseLeave += (sender, args) => SettingsInkIndexLink(link, (bool)link.Tag);
            return link;
        }

        private static void SettingsPaintIndexLink(Button link, bool current)
        {
            link.Tag = current;
            SettingsInkIndexLink(link, current || link.IsMouseOver);
        }

        private static void SettingsInkIndexLink(Button link, bool lit)
        {
            link.Background = lit ? Ui.Brush(Theme.SurfaceZone) : Brushes.Transparent;
            var label = link.Content as TextBlock;
            if (label != null) label.Foreground = Ui.Brush(lit ? Theme.TextPrimary : Theme.TextSecondary);
        }

        /// <summary>The main column's scroller, which the row follows and scrolls.</summary>
        private static ScrollViewer SettingsScrollOf(DependencyObject element)
        {
            var node = element;
            while (node != null)
            {
                node = VisualTreeHelper.GetParent(node);
                var scroll = node as ScrollViewer;
                if (scroll != null) return scroll;
            }
            return null;
        }

        /// <summary>A section's top, measured from the top of the scroller's view.</summary>
        private static double SettingsTopIn(ScrollViewer scroll, FrameworkElement section)
        {
            if (!section.IsVisible) return double.MaxValue;
            try
            {
                return section.TranslatePoint(new Point(0, 0), scroll).Y;
            }
            catch (InvalidOperationException)
            {
                // A build being replaced: its sections have left the scroller before its handler has.
                return double.MaxValue;
            }
        }

        /// <summary>Scrolls a section's heading to the top of the view, a margin under its edge.</summary>
        private static void SettingsJumpTo(ScrollViewer scroll, FrameworkElement section)
        {
            var top = scroll == null ? double.MaxValue : SettingsTopIn(scroll, section);
            if (top == double.MaxValue)
            {
                section.BringIntoView();
                return;
            }
            scroll.ScrollToVerticalOffset(Math.Max(0, scroll.VerticalOffset + top - PanelSettings.IndexJumpMargin));
        }

        // --- Rows ------------------------------------------------------------------------------------

        /// <summary>
        /// A row whose control goes under its title when the content is too narrow for the two side by side
        /// (PanelSettings.StacksControls): four worked examples of a name beside their title need about 500.
        /// </summary>
        private Border SettingsFit(Border row)
        {
            if (!PanelSettings.StacksControls(ContentWidth)) return row;
            var parts = row.Tag as RowParts;
            var grid = row.Child as Grid;
            if (parts == null || parts.Control == null || grid == null) return row;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(parts.Control, 1);
            Grid.SetColumn(parts.Control, 0);
            Grid.SetColumnSpan(parts.Control, 2);
            parts.Control.HorizontalAlignment = HorizontalAlignment.Left;
            parts.Control.Margin = new Thickness(0, PanelSettings.StackedControlGap, 0, 0);
            return row;
        }

        /// <summary>
        /// A finished row with the New tag after its title, for a row Ui.Row has to draw: the delta rows stay
        /// literal Ui.Row calls (PanelDataTabTests), and Ui.Row takes no tags.
        /// </summary>
        /// <remarks>
        /// New marks what the shipped plugin cannot do, for one release. Delta precision (#322) and the clock
        /// (#324) landed after v0.3.0-rc.7, whose contract has neither.
        /// </remarks>
        private static Border SettingsNew(Border row)
        {
            var parts = row.Tag as RowParts;
            if (parts == null || parts.TitleLine == null) return row;
            var tag = Ui.NewTag();
            tag.Margin = new Thickness(8, 0, 0, 0);
            tag.VerticalAlignment = VerticalAlignment.Center;
            parts.TitleLine.Children.Add(tag);
            return row;
        }

        /// <summary>A segmented control that is drawn and not built: every option disabled, the first chosen.
        /// Only ever inside Ui.Soon, which carries its ticket.</summary>
        private static Segmented SettingsGreyedChoice(string[] labels)
        {
            var options = labels.Select((label, i) => new Segmented.Option("option" + i, label, true));
            return new Segmented(options, "option0");
        }

        /// <summary>The artboards' .num-in, 64 by 30 with the numeral right-aligned, holding text rather than a
        /// clamped value: an empty threshold is the unit's default, and a greyed one shows an example.</summary>
        private static TextBox SettingsNumberField(string text)
        {
            var box = Ui.Input(text, PanelShell.NumberInputWidth);
            box.Padding = new Thickness(PanelShell.NumberInputPaddingX - PanelMetrics.BorderWeight, 0, PanelShell.NumberInputPaddingX - PanelMetrics.BorderWeight, 0);
            box.FontFamily = PanelFonts.Data;
            box.FontWeight = FontWeights.SemiBold;
            box.FontSize = PanelShell.NumberInputTextSize;
            box.HorizontalContentAlignment = HorizontalAlignment.Right;
            box.TextAlignment = TextAlignment.Right;
            return box;
        }

        /// <summary>A text field with a placeholder drawn over it while it is empty, in the label ink and the
        /// field's own face and alignment.</summary>
        private static FrameworkElement SettingsHinted(TextBox box, string hint)
        {
            if (string.IsNullOrEmpty(hint)) return box;
            var text = new TextBlock
            {
                Text = hint,
                FontFamily = box.FontFamily,
                FontWeight = box.FontWeight,
                FontSize = box.FontSize,
                Foreground = Ui.Brush(Theme.TextLabel),
                TextAlignment = box.TextAlignment,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = box.TextAlignment == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Margin = new Thickness(box.Padding.Left + PanelMetrics.BorderWeight + 2, 0, box.Padding.Right + PanelMetrics.BorderWeight + 2, 0),
                IsHitTestVisible = false,
            };
            Action show = () => text.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;
            show();
            box.TextChanged += (sender, args) => show();
            var grid = new Grid { Width = box.Width, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            grid.Children.Add(box);
            grid.Children.Add(text);
            return grid;
        }

        /// <summary>
        /// A threshold where 0 is the unit's default: empty while it is, with the default as its placeholder,
        /// and committed on Enter or on leaving it (PanelSettings.ParseThreshold), only when it moved.
        /// </summary>
        private static FrameworkElement SettingsThresholdBox(int? value, int? fallback, int max, Action<int> changed)
        {
            var box = SettingsNumberField(PanelSettings.ThresholdText(value));
            var last = value ?? 0;
            Action commit = () =>
            {
                var next = PanelSettings.ParseThreshold(box.Text, last, max);
                box.Text = PanelSettings.ThresholdText(next);
                if (next == last) return;
                last = next;
                changed(next);
            };
            box.LostFocus += (sender, args) => commit();
            box.KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter) commit();
            };
            return SettingsHinted(box, PanelSettings.ThresholdText(fallback));
        }

        /// <summary>
        /// SimHub's four units, as the enum names GameReaderCommon spells them, or nulls where SimHub cannot be
        /// asked. A read of SimHub's own settings, which a build that draws the lighting may make.
        /// </summary>
        private string[] SettingsUnitNames()
        {
            try
            {
                var manager = plugin == null ? null : plugin.PluginManager;
                var game = manager == null ? null : manager.GameManager;
                var units = game == null ? null : game.GameUnitSettings;
                if (units == null) return new string[4];
                return new[]
                {
                    units.LocalSpeedUnit.ToString(),
                    units.LocalTemperatureUnit.ToString(),
                    units.LocalPressureUnit.ToString(),
                    units.LocalFuelUnit.ToString(),
                };
            }
            catch (Exception ex)
            {
                Log.Warn("Reading SimHub's units failed: " + ex.Message);
                return new string[4];
            }
        }

        // --- Race data -------------------------------------------------------------------------------

        private FrameworkElement SettingsRaceData(string[] units)
        {
            var position = BuildSegmented(Contract.PositionModes, PanelDataTab.PositionLabels, Settings.PositionMode, value =>
            {
                Settings.PositionMode = value;
                Save();
            });
            var delta = BuildSegmented(Contract.DeltaReferences, PanelDataTab.DeltaLabels, Settings.DeltaReference, value =>
            {
                Settings.DeltaReference = value;
                Save();
            });
            // Under the reference it qualifies, and rig-wide for the same reason: a delta read to the
            // thousandth on the rim and to the hundredth on the pit wall is two answers. #322.
            var deltaPrecision = BuildSegmented(Contract.DeltaPrecisions, PanelDataTab.DeltaPrecisionLabels, Settings.DeltaPrecision, value =>
            {
                Settings.DeltaPrecision = value;
                Save();
            });
            var session = BuildSegmented(Contract.SessionProgressModes, PanelDataTab.SessionLabels, Settings.SessionProgress, value =>
            {
                Settings.SessionProgress = value;
                Save();
            });
            // Rig-wide: a leaderboard on the rim and a board on the pit wall write the same name, and which
            // of the four formats a driver reads fastest is a fact about the driver. #385.
            var driverName = BuildSegmented(Contract.DriverNameFormats, PanelDataTab.DriverNameLabels, Settings.DriverNameFormat, value =>
            {
                Settings.DriverNameFormat = value;
                Save();
            });
            var teamName = BuildToggle(Settings.DriverNameTeam, on =>
            {
                Settings.DriverNameTeam = on;
                Save();
            });
            // A driver reads a clock one way on the rim and on the pit wall, and the sim's time of day the way
            // they read their own. #324.
            var clock = BuildSegmented(Contract.ClockFormats, PanelDataTab.ClockLabels, Settings.ClockFormat, value =>
            {
                Settings.ClockFormat = value;
                Save();
            });

            var fuelUnit = PanelSettings.FuelUnit(units[3]) ?? PanelSettings.FuelTargetUnitFallback;
            var fuelTarget = Ui.HStack(PanelSettings.AlertWhenGap, SettingsNumberField(string.Empty), Ui.Caption(fuelUnit));
            var tyres = Ui.HStack(6,
                Ui.Button(PanelSettings.TyreDisplayLabels[0], PanelButtonKind.Outline, PanelButtonSize.Small),
                Ui.Button(PanelSettings.TyreDisplayLabels[1], PanelButtonKind.Outline, PanelButtonSize.Small));

            // SimHub's own units, which OpenDash follows and never sets; where it cannot be asked, the caption
            // alone says where they are set.
            var line = PanelSettings.UnitsLine(units[0], units[1], units[2], units[3]);
            var unitsLine = line == null ? null : Ui.Text(line, PanelSettings.UnitsTextSize, FontWeights.Normal, Theme.TextPrimary);

            return PageSection(PanelDataTab.SectionTitle, true, PanelKit.SectionHeadingGapSettings,
                SettingsFit(Ui.Row(PanelDataTab.PositionTitle, PanelDataTab.PositionCaption, position)),
                SettingsFit(Ui.Row(PanelDataTab.DeltaTitle, PanelDataTab.DeltaCaption, delta)),
                SettingsFit(SettingsNew(Ui.Row(PanelDataTab.DeltaPrecisionTitle, PanelDataTab.DeltaPrecisionCaption, deltaPrecision))),
                SettingsFit(Ui.Row(PanelDataTab.SessionTitle, PanelDataTab.SessionCaption, session)),
                SettingsFit(Ui.Row(PanelDataTab.DriverNameTitle, PanelDataTab.DriverNameCaption, driverName)),
                Ui.Row(PanelDataTab.TeamNameTitle, PanelDataTab.TeamNameCaption, teamName),
                SettingsFit(SettingsNew(Ui.Row(PanelDataTab.ClockTitle, PanelDataTab.ClockCaption, clock))),
                Ui.Soon(Ui.SettingRow(PanelSoon.FuelTargetPerLap.Title, fuelTarget), PanelSoon.FuelTargetPerLap),
                Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.TyreDisplay.Title, tyres)), PanelSoon.TyreDisplay),
                Ui.Row(PanelSettings.UnitsTitle, PanelSettings.UnitsCaption, unitsLine));
        }

        // --- Flags -----------------------------------------------------------------------------------

        private FrameworkElement SettingsFlags()
        {
            // A rig setting rather than a per-screen one: what the band may say about the car behind is the
            // same answer on the wheel as on the pit wall.
            var blueFlag = BuildSegmented(Contract.BlueFlagDetails, PanelDataTab.BlueFlagLabels, Settings.BlueFlagDetail, value =>
            {
                Settings.BlueFlagDetail = value;
                Save();
            });
            // Rig-wide since #503: a flag shown in the pit lane is the same answer on every surface.
            var flagsInPitLane = BuildToggle(Settings.FlagsInPitLane, on =>
            {
                Settings.FlagsInPitLane = on;
                Save();
            });
            return PageSection(PanelSettings.FlagsTitle, true, PanelKit.SectionHeadingGapSettings,
                SettingsFit(Ui.Row(PanelDataTab.BlueFlagTitle, PanelDataTab.BlueFlagCaption, blueFlag)),
                Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.YellowFlags.Title, SettingsGreyedChoice(PanelSettings.YellowFlagLabels))), PanelSoon.YellowFlags),
                Ui.SettingRow(PanelSettings.FlagsInPitLaneTitle, flagsInPitLane, null, Ui.NewTag()));
        }

        // --- Alerts ----------------------------------------------------------------------------------

        /// <summary>
        /// The thresholds the rig warns at, as the artboard's table: one row an alert, what it warns at, where
        /// it shows, and a "Try" that opens Rig on it. Rig-wide since #503: a threshold is a fact about the car,
        /// not about which corner of the rig a box is in.
        /// </summary>
        /// <remarks>
        /// Through SetLightsOilTemp and SetLightsWaterTemp, never into one matrix's entry: Normalise fills
        /// every panel's entry from the rig's value on the next save, so an index into the per-panel arrays
        /// is an edit the save undoes. Zero is the unit's own default, chosen from SimHub's TemperatureUnit,
        /// and the empty box shows it. The four surface columns are #512's and greyed; where they do not fit
        /// they fold into one greyed row under the table.
        /// </remarks>
        private FrameworkElement SettingsAlerts(string[] units)
        {
            var temperature = units[1];
            var surfaces = PanelSettings.AlertSurfacesFit(ContentWidth);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (surfaces)
            {
                foreach (var column in PanelSettings.SurfaceColumns) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lowFuel = Ui.NumberInput(Settings.FlagBoxLowFuelLaps, 0, PanelSettings.LowFuelMax, v => { Settings.FlagBoxLowFuelLaps = v; Save(); });
            var oilTemp = SettingsThresholdBox(Settings.LightsOilTemp, PanelSettings.TemperatureDefault(true, temperature), PanelSettings.TemperatureMax, v => { Settings.SetLightsOilTemp(v); Save(); });
            var waterTemp = SettingsThresholdBox(Settings.LightsWaterTemp, PanelSettings.TemperatureDefault(false, temperature), PanelSettings.TemperatureMax, v => { Settings.SetLightsWaterTemp(v); Save(); });

            var row = 0;
            SettingsAlertHeader(grid, row++, surfaces);
            SettingsAlertRow(grid, row++, PanelSettings.Alert(PanelSettings.LowFuelTitle), lowFuel, null, temperature, surfaces, null);
            SettingsAlertRow(grid, row++, PanelSettings.Alert(PanelSettings.OilTempTitle), oilTemp, PanelSettings.TemperatureCaption, temperature, surfaces, null);
            SettingsAlertRow(grid, row++, PanelSettings.Alert(PanelSettings.WaterTempTitle), waterTemp, PanelSettings.TemperatureCaption, temperature, surfaces, null);
            SettingsAlertRow(grid, row++, PanelSettings.Alert(PanelSoon.TyreWear.Title), null, null, temperature, surfaces, PanelSoon.TyreWear);
            SettingsAlertRow(grid, row++, PanelSettings.Alert(PanelSoon.PitWindowOpen.Title), null, null, temperature, surfaces, PanelSoon.PitWindowOpen);
            SettingsAlertRow(grid, row++, PanelSettings.Alert(PanelSoon.Incidents.Title), null, null, temperature, surfaces, PanelSoon.Incidents);
            SettingsAlertRow(grid, row++, PanelSettings.Alert(PanelSoon.HybridBatteryLow.Title), null, null, temperature, surfaces, PanelSoon.HybridBatteryLow, true);

            var heading = Ui.HStack(10, Ui.Heading(PanelSettings.AlertsTitle, true), Ui.NewTag());
            heading.HorizontalAlignment = HorizontalAlignment.Left;
            heading.Margin = new Thickness(0, 0, 0, PanelKit.SectionHeadingGapSettings);
            FrameworkElement folded = null;
            if (!surfaces)
            {
                folded = Ui.SoonRow(PanelSoon.AlertDisplay);
                folded.Margin = new Thickness(0, PanelSettings.PreviewMarginBottom, 0, 0);
            }
            return PageSection(null, true, PanelKit.SectionHeadingGapSettings, heading, Ui.CardBox(grid, 0), folded);
        }

        /// <summary>The table's head: the column names in the label face, the greyed ones with their Soon tag
        /// under the name, and a rule under the row.</summary>
        private static void SettingsAlertHeader(Grid grid, int row, bool surfaces)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            SettingsAlertRule(grid, row);
            var column = 0;
            SettingsAlertCell(grid, row, column++, Ui.Eyebrow(PanelSettings.AlertColumn), null, false);
            SettingsAlertCell(grid, row, column++, Ui.Eyebrow(PanelSettings.WhenColumn), null, false);
            if (surfaces)
            {
                foreach (var name in PanelSettings.SurfaceColumns)
                {
                    var head = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
                    var label = Ui.Eyebrow(name);
                    label.HorizontalAlignment = HorizontalAlignment.Center;
                    var tag = Ui.SoonTag(PanelSoon.AlertDisplay);
                    tag.HorizontalAlignment = HorizontalAlignment.Center;
                    tag.Margin = new Thickness(0, PanelSettings.AlertHeaderTagGap, 0, 0);
                    head.Children.Add(label);
                    head.Children.Add(tag);
                    SettingsAlertCell(grid, row, column++, head, PanelSoon.AlertDisplay, true);
                }
            }
        }

        /// <summary>
        /// One alert: its name (with its caption, or its Soon tag when it is greyed), what it warns at, where it
        /// shows, and "Try", which opens Rig on its scenario. A greyed row is faded cell by cell with its own
        /// ticket; on a live row only the surface cells are, with #512's.
        /// </summary>
        private void SettingsAlertRow(Grid grid, int row, SettingsAlert alert, FrameworkElement box, string caption, string temperature, bool surfaces, SoonItem soon, bool last = false)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (!last) SettingsAlertRule(grid, row);
            var column = 0;

            var name = Ui.Text(alert.Title, PanelSettings.AlertTextSize, FontWeights.Medium, Theme.TextPrimary);
            name.TextWrapping = TextWrapping.Wrap;
            var nameLine = soon == null ? (FrameworkElement)name : Ui.HStack(8, name, Ui.SoonTag(soon));
            nameLine.HorizontalAlignment = HorizontalAlignment.Left;
            var nameCell = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            nameCell.Children.Add(nameLine);
            if (caption != null)
            {
                var under = Ui.Caption(caption);
                under.Margin = new Thickness(0, PanelKit.FixDetailGap, 0, 0);
                nameCell.Children.Add(under);
            }
            SettingsAlertCell(grid, row, column++, nameCell, soon, false);

            var when = new List<UIElement>();
            if (alert.HasThreshold)
            {
                when.Add(Ui.Caption(alert.Op));
                when.Add(box ?? SettingsHinted(SettingsNumberField(string.Empty), alert.Example));
                var unit = alert.Unit ?? PanelSettings.TemperatureUnit(temperature);
                if (!string.IsNullOrEmpty(unit)) when.Add(Ui.Caption(unit));
            }
            SettingsAlertCell(grid, row, column++, Ui.HStack(PanelSettings.AlertWhenGap, when.ToArray()), soon, false);

            if (surfaces)
            {
                foreach (var on in alert.Surfaces) SettingsAlertCell(grid, row, column++, SettingsCheck(on), soon ?? PanelSoon.AlertDisplay, true);
            }

            FrameworkElement tryIt = null;
            if (alert.Live)
            {
                var link = Ui.LinkButton(PanelSettings.TryLabel);
                link.FontSize = PanelSettings.AlertTryTextSize;
                link.Height = double.NaN;
                var scenario = alert.ScenarioId;
                link.Click += (sender, args) => Open(PanelPage.Rig, scenario);
                tryIt = link;
            }
            SettingsAlertCell(grid, row, column, tryIt ?? new Border(), soon, false, HorizontalAlignment.Right);
        }

        private static void SettingsAlertCell(Grid grid, int row, int column, FrameworkElement content, SoonItem soon, bool centred, HorizontalAlignment align = HorizontalAlignment.Left)
        {
            content.VerticalAlignment = VerticalAlignment.Center;
            if (centred) content.HorizontalAlignment = HorizontalAlignment.Center;
            else if (align == HorizontalAlignment.Right) content.HorizontalAlignment = HorizontalAlignment.Right;
            FrameworkElement cell = new Border
            {
                Padding = new Thickness(PanelSettings.AlertCellPaddingX, PanelSettings.AlertCellPaddingY, PanelSettings.AlertCellPaddingX, PanelSettings.AlertCellPaddingY),
                Child = content,
            };
            if (soon != null) cell = Ui.Soon(cell, soon);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }

        /// <summary>The rule under a row of the table, across every column and behind the cells.</summary>
        private static void SettingsAlertRule(Grid grid, int row)
        {
            var rule = new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, 0, 0, PanelMetrics.BorderWeight),
                IsHitTestVisible = false,
            };
            Grid.SetRow(rule, row);
            Grid.SetColumnSpan(rule, Math.Max(1, grid.ColumnDefinitions.Count));
            grid.Children.Add(rule);
        }

        /// <summary>The artboard's .ck, drawn: a 16 px box, the accent with a tick when it is on.</summary>
        private static FrameworkElement SettingsCheck(bool on)
        {
            var box = new Border
            {
                Width = PanelSettings.AlertCheck,
                Height = PanelSettings.AlertCheck,
                CornerRadius = new CornerRadius(Theme.Radius),
                BorderBrush = Ui.Brush(on ? Theme.Accent : Theme.Border),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                Background = on ? Ui.Brush(Theme.Accent) : Brushes.Transparent,
            };
            if (on)
            {
                var tick = Ui.Icon(PanelIcons.Check, Theme.OnAccent, PanelSettings.AlertCheckIcon, PanelIcons.Box);
                tick.HorizontalAlignment = HorizontalAlignment.Center;
                tick.VerticalAlignment = VerticalAlignment.Center;
                box.Child = tick;
            }
            return box;
        }

        // --- Lighting --------------------------------------------------------------------------------

        /// <summary>
        /// The preview, then the two brightnesses, night mode, its button, and the two rows #128 will fill.
        /// </summary>
        /// <remarks>
        /// Brightness and night mode are the rig's rather than any one device's, so they sit here and in the
        /// sidebar rather than inside the first device that happened to want them. The preview's Day and Night
        /// write nothing: they show what each brightness looks like. A slider's drag repaints the preview as it
        /// goes and saves on release, and every write calls ShowLightingChange, which re-dims the preview in
        /// place and rebuilds the page once the presses stop.
        /// </remarks>
        private FrameworkElement SettingsLighting()
        {
            var strip = Ui.Strip(PanelEmulation.StripFrame(PanelSettings.PreviewEnds, PanelSettings.PreviewCentre, PanelSettings.PreviewStripScenario), PanelSettings.PreviewStrip);
            var matrix = Ui.Matrix(PanelEmulation.MatrixFrame(GlyphSheet, PanelSettings.PreviewMatrixScenario, PanelSettings.PreviewMatrixOptions()), PanelSettings.PreviewMatrix);
            var percent = Ui.Text(string.Empty, PanelSettings.PreviewPercentSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
            percent.Width = PanelSettings.PreviewPercentWidth;
            percent.TextAlignment = TextAlignment.Right;
            Typography.SetNumeralAlignment(percent, FontNumeralAlignment.Tabular);

            Func<bool> night = () => PanelSettings.ShowsNight(settingsPreviewPick, Settings.LightsNightMode);
            Action<bool, int, int> paint = (atNight, day, dark) =>
            {
                var level = PanelSettings.PreviewLevel(atNight, day, dark);
                Ui.Redim(strip, level);
                Ui.Redim(matrix, level);
                percent.Text = PanelSettings.PreviewPercent(atNight, day, dark);
            };
            Action repaint = () => paint(night(), Settings.LightsBrightness, Settings.LightsNightBrightness);
            repaint();
            OnLighting(repaint);

            var pick = BuildSegmented(PanelSettings.PreviewValues, PanelSettings.PreviewLabels, night() ? PanelSettings.PreviewNight : PanelSettings.PreviewDay, value =>
            {
                settingsPreviewPick = value == PanelSettings.PreviewNight;
                repaint();
            });
            var head = new DockPanel { LastChildFill = false };
            // New on the control: the Map is the only artboard that tags the night preview, and a thing new
            // only on the Map carries the tag where it is drawn.
            var eyebrow = Ui.HStack(8, Ui.Eyebrow(PanelSettings.PreviewTitle), Ui.NewTag());
            DockPanel.SetDock(eyebrow, Dock.Left);
            DockPanel.SetDock(pick, Dock.Right);
            head.Children.Add(eyebrow);
            head.Children.Add(pick);

            // Side by side 40 apart, and wrapped onto a second line where the column is too narrow for the three.
            var stage = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var pictures = new FrameworkElement[] { strip, matrix, percent };
            for (var i = 0; i < pictures.Length; i++)
            {
                pictures[i].VerticalAlignment = VerticalAlignment.Center;
                pictures[i].Margin = new Thickness(0, PanelSettings.PreviewWrapGap / 2, i < pictures.Length - 1 ? PanelSettings.PreviewStageGap : 0, PanelSettings.PreviewWrapGap / 2);
                stage.Children.Add(pictures[i]);
            }
            var ground = new Border
            {
                Background = Ui.Brush(Theme.SurfaceInset),
                CornerRadius = new CornerRadius(Theme.Radius),
                Padding = new Thickness(PanelSettings.PreviewStagePadding, PanelSettings.PreviewStagePadding - PanelSettings.PreviewWrapGap / 2, PanelSettings.PreviewStagePadding, PanelSettings.PreviewStagePadding - PanelSettings.PreviewWrapGap / 2),
                Child = stage,
            };
            var card = Ui.CardBox(Ui.VStack(PanelSettings.PreviewGap, head, ground), 0);
            card.Padding = new Thickness(PanelSettings.PreviewPaddingX, PanelSettings.PreviewPaddingY, PanelSettings.PreviewPaddingX, PanelSettings.PreviewPaddingY);
            card.Margin = new Thickness(0, 0, 0, PanelSettings.PreviewMarginBottom);

            // A number box reported on every blur; a slider reports once, on release or a key, and the
            // unchanged-value guard stays so a key that hits an end is not a change.
            var brightness = Ui.Slider(Settings.LightsBrightness,
                v => { if (v == Settings.LightsBrightness) return; Settings.LightsBrightness = v; Save(); ShowLightingChange(); },
                v => { if (!night()) paint(false, v, Settings.LightsNightBrightness); });
            brightness.Width = PanelSettings.SliderWidthFor(ContentWidth);
            var nightBrightness = Ui.Slider(Settings.LightsNightBrightness,
                v => { if (v == Settings.LightsNightBrightness) return; Settings.LightsNightBrightness = v; Save(); ShowLightingChange(); },
                v => { if (night()) paint(true, Settings.LightsBrightness, v); });
            nightBrightness.Width = PanelSettings.SliderWidthFor(ContentWidth);

            // The switch is the setting; the preview follows it again once it is pressed.
            var nightMode = BuildToggle(Settings.LightsNightMode, on =>
            {
                settingsPreviewPick = null;
                Settings.LightsNightMode = on;
                Save();
                ShowLightingChange();
            });

            return PageSection(PanelSettings.LightingTitle, true, PanelKit.SectionHeadingGapSettings,
                card,
                SettingsFit(Ui.Row(PanelSettings.BrightnessTitle, PanelSettings.BrightnessCaption, brightness)),
                SettingsFit(Ui.Row(PanelSettings.NightBrightnessTitle, null, nightBrightness)),
                Ui.Row(PanelSettings.NightModeTitle, null, nightMode),
                Ui.Row(PanelSettings.NightModeButtonTitle, null, SettingsBindingKey(Contract.ToggleNightModeAction)),
                Ui.SoonRow(PanelSoon.SimTimeOfDay),
                Ui.SoonRow(PanelSoon.ScreenDimming));
        }

        /// <summary>
        /// The night-mode button's chip at the artboard's .key size (28, 10 in): what it is bound to, Not bound,
        /// or "Shortcuts" when SimHub's mappings cannot be read, and a press lands on its row on Shortcuts. The
        /// shell's BindingChipFor draws the 26 px chip only.
        /// </summary>
        private FrameworkElement SettingsBindingKey(string action)
        {
            var triggers = TriggersOf(action);
            Action open = () => Go(PanelPage.Shortcuts, PanelBindings.Anchor(action));
            Button chip;
            if (triggers == null) chip = Ui.BindingChip(PanelShortcuts.Title, null, open, true);
            else
            {
                var text = PanelBindings.ChipText(triggers);
                chip = Ui.BindingChip(text ?? Ui.NotBound, text != null, open, true);
            }
            chip.ToolTip = PanelBindings.ChipTooltip;
            return chip;
        }

        // --- Appearance and Driver (greyed) ------------------------------------------------------------

        private FrameworkElement SettingsAppearance()
        {
            return PageSection(PanelSettings.AppearanceTitle, true, PanelKit.SectionHeadingGapSettings,
                Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.DashTheme.Title, SettingsGreyedChoice(PanelSettings.ThemeLabels))), PanelSoon.DashTheme),
                Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.ColourVision.Title, SettingsGreyedChoice(PanelSettings.ColourVisionLabels))), PanelSoon.ColourVision),
                Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.Colours.Title, SettingsGreyedChoice(PanelSettings.ColoursLabels))), PanelSoon.Colours));
        }

        private FrameworkElement SettingsDriver()
        {
            var name = Ui.HStack(PanelSettings.DriverInputGap,
                SettingsHinted(Ui.Input(string.Empty, PanelSettings.NameInputWidth), PanelSettings.FirstNameHint),
                SettingsHinted(Ui.Input(string.Empty, PanelSettings.NameInputWidth), PanelSettings.SurnameHint));
            return PageSection(PanelSettings.DriverTitle, true, PanelKit.SectionHeadingGapSettings,
                Ui.Soon(SettingsFit(Ui.SettingRow(PanelSoon.BrandName.Title, name)), PanelSoon.BrandName),
                Ui.Soon(Ui.SettingRow(PanelSoon.BrandRaceNumber.Title, SettingsHinted(SettingsNumberField(string.Empty), PanelSettings.RaceNumberHint)), PanelSoon.BrandRaceNumber),
                Ui.Soon(Ui.SettingRow(PanelSoon.BrandLogo.Title, Ui.Button(PanelSettings.LogoButton, PanelButtonKind.Outline, PanelButtonSize.Small)), PanelSoon.BrandLogo),
                Ui.Soon(Ui.SettingRow(PanelSoon.IdleScreenBackground.Title, Ui.Button(PanelSettings.IdleBackgroundButton, PanelButtonKind.Outline, PanelButtonSize.Small)), PanelSoon.IdleScreenBackground));
        }
    }
}
