// SettingsControl.Lights.cs: the LEDs page, drawn to Leds.dc.html, and its Add LEDs sheet, drawn to
// AddLeds.dc.html. The file keeps its name because PanelLedBarFormTests reads the Add LEDs form here.
//
// A card per strip, and under them the selected strip: its header (name, hardware and shape, the Install or
// Update its profile needs, Rename, Remove), what needs fixing, a preview of what it shows, its rev lights,
// what is its own (SimHub device, brightness, direction), and a switch for each effect its shape carries.
// Then what is the same for every strip. Every word and every rule it draws is PanelLeds' or PanelLights';
// this file only draws them. Installing, moving and the census are in SettingsControl.Profiles.cs, shared
// with Matrix, Updates and Home, and every one of them finds a strip's embedded profile by the strip's
// profile shape, which is the reversed twin for a strip wired from the far end.
//
// No DrawsLighting(): the page walks SimHub's LED devices while it builds, so a wheel press must not rebuild
// it. Each picture re-dims in place through OnLighting instead.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>The car tables' button and the line under it. Dropped with the page, so a download that
        /// finishes after the page has gone writes nowhere.</summary>
        private Button carTablesButton;

        private TextBlock carTablesLine;

        /// <summary>Whether the car tables are downloading, held outside the build so a rebuild meanwhile draws
        /// the button disabled and the line saying so rather than a press that looks ready.</summary>
        private bool carTablesDownloading;

        /// <summary>The preview chip pressed, and the strip it was pressed for: held outside the build so a
        /// rebuild keeps it, and back to Live when another strip is selected.</summary>
        private string ledsScenario = PanelLeds.LiveScenario;

        private string ledsScenarioFor;

        private FrameworkElement BuildLedsPage(PanelRoute to)
        {
            OnDrop(() =>
            {
                carTablesButton = null;
                carTablesLine = null;
            });
            var bars = Settings.LedBarList().Where(bar => bar != null).ToList();
            var current = LedsSelectedBar(bars);
            var sections = new List<UIElement> { Ui.Anchor(LedsCards(bars, current), PanelLeds.AnchorStrips) };
            if (current != null) sections.Add(LedsStripSection(current));
            sections.Add(LedsEveryStripSection());
            return PageLayout(PanelLeds.Title, null, sections.ToArray());
        }

        /// <summary>The strip whose settings are showing: the one selected, else the first; null with none.</summary>
        private LedBar LedsSelectedBar(IList<LedBar> bars)
        {
            if (bars.Count == 0) return null;
            var ns = Selected(PanelPage.Leds);
            return bars.FirstOrDefault(bar => string.Equals(bar.Namespace, ns, StringComparison.Ordinal)) ?? bars[0];
        }

        /// <summary>How bright a strip's pictures are drawn now: full by day, the brightness in force at night.</summary>
        private double LedsDim(string ns)
        {
            return PanelLeds.PreviewDim(Settings.LightsNightMode, Settings.LightsNightBrightness, Settings.BarBrightness(ns));
        }

        /// <summary>What a strip is set to that changes its picture.</summary>
        private StripOptions LedsOptions(LedBar bar)
        {
            return PanelLeds.OptionsFor(Settings.BarSpotterWhole(bar.Namespace), bar.EffectsOff);
        }

        // --- The cards ----------------------------------------------------------------------------------------

        /// <summary>A card per strip -- its name, its shape, a picture of it and whether SimHub shows it -- and
        /// the tile that adds one.</summary>
        private FrameworkElement LedsCards(IList<LedBar> bars, LedBar current)
        {
            var cards = new List<UIElement>();
            foreach (var bar in bars)
            {
                var ns = bar.Namespace;
                var facts = StripFacts(ns);
                var profile = facts == null ? null : facts.Profile;
                var selected = facts == null ? null : facts.Selected;
                var picture = Ui.Strip(PanelLeds.CardFrame(bar.Shape, LedsOptions(bar)), StripStyle.Card, LedsDim(ns));
                OnLighting(() => Ui.Redim(picture, LedsDim(ns)));
                cards.Add(Ui.StripCard(
                    bar.Name,
                    PanelLeds.ShapeDots(bar.Shape),
                    picture,
                    PanelLeds.StateText(profile, selected),
                    PanelLeds.StateHex(profile, selected),
                    ReferenceEquals(bar, current),
                    () =>
                    {
                        Select(PanelPage.Leds, ns);
                        Redraw();
                    }));
            }
            cards.Add(Ui.InlineAddCard(PanelLights.AddBar, PanelKit.StripAddIcon, ShowAddLedBar));
            var grid = Ui.CardGrid(PanelLeds.CardMinWidth, PanelLeds.CardGap, PanelLeds.CardColumns, cards.ToArray());
            AutomationProperties.SetName(grid, PanelLights.BarsTitle);
            if (bars.Count > 0) return grid;
            // The empty state names the emptiness beside the tile that ends it; Home reads the same words.
            return Ui.VStack(12, Ui.Prose(PanelLeds.NoStrips, Theme.SizeBody), grid);
        }

        // --- The selected strip -------------------------------------------------------------------------------

        private FrameworkElement LedsStripSection(LedBar bar)
        {
            var ns = bar.Namespace;
            if (!string.Equals(ledsScenarioFor, ns, StringComparison.Ordinal))
            {
                ledsScenario = PanelLeds.LiveScenario;
                ledsScenarioFor = ns;
            }
            IList<string> declined;
            var targets = LedTargets.All(out declined);

            Action redrawPreview;
            var preview = LedsPreview(bar, out redrawPreview);
            var parts = new List<UIElement> { LedsHeader(bar) };
            var fix = LedsFix(bar);
            if (fix != null) parts.Add(fix);
            parts.Add(Ui.Anchor(preview, PanelLeds.AnchorPreview));
            parts.Add(LedsColumns(LedsRevLights(bar, redrawPreview), LedsThisStrip(bar, targets, declined, redrawPreview)));
            parts.Add(Ui.Anchor(LedsEffects(bar, redrawPreview), PanelLeds.AnchorEffects));
            return LedsBlock(Ui.VStack(18, parts.ToArray()));
        }

        /// <summary>A section of the page under a rule, 22 below it, as the artboard draws the strip's and
        /// every strip's.</summary>
        private static Border LedsBlock(UIElement child)
        {
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(0, 22, 0, 0),
                Child = child,
            };
        }

        /// <summary>A block's 17 px heading with the artboard's 10 under it.</summary>
        private static TextBlock LedsHeading(string text)
        {
            var heading = Ui.Heading(text);
            heading.Margin = new Thickness(0, 0, 0, 10);
            return heading;
        }

        /// <summary>A .row as the LEDs artboard spaces it: its control 20 from the title rather than the kit's 24.</summary>
        private static Border LedsRow(string title, FrameworkElement control, string caption = null, params FrameworkElement[] tags)
        {
            if (control != null) control.Margin = new Thickness(PanelKit.RowGapLeds - PanelShell.RowGap, 0, 0, 0);
            return Ui.SettingRow(title, control, caption, tags);
        }

        /// <summary>Two blocks side by side, 40 apart, where two columns fit, and one under the other where not.</summary>
        private FrameworkElement LedsColumns(FrameworkElement left, FrameworkElement right)
        {
            if (!TwoColumns) return Ui.VStack(24, left, right);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            left.Margin = new Thickness(0, 0, 20, 0);
            right.Margin = new Thickness(20, 0, 0, 0);
            left.VerticalAlignment = VerticalAlignment.Top;
            right.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(left, 0);
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);
            return grid;
        }

        /// <summary>The strip's name, its hardware and shape in a chip beside it, and the presses on the strip.</summary>
        private FrameworkElement LedsHeader(LedBar bar)
        {
            var title = Ui.SubHeading(bar.Name);
            title.VerticalAlignment = VerticalAlignment.Center;
            var lead = Ui.Text(PanelLeds.HardwareLead(bar.Shape), 13, FontWeights.Medium, Theme.TextPrimary);
            lead.VerticalAlignment = VerticalAlignment.Center;
            var numerals = Ui.Text(PanelLeds.ShapeDots(bar.Shape), 14, FontWeights.SemiBold, Theme.TextPrimary, PanelFonts.Data);
            numerals.VerticalAlignment = VerticalAlignment.Center;
            var chip = new Border
            {
                Height = 26,
                Padding = new Thickness(9, 0, 9, 0),
                Background = Ui.Brush(Theme.SurfaceRaised),
                CornerRadius = new CornerRadius(Theme.Radius),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                Child = Ui.HStack(0, lead, numerals),
            };
            var name = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(title);
            name.Children.Add(chip);
            return Ui.Row(name, BuildLedBarActions(bar.Namespace));
        }

        /// <summary>
        /// The strip's profile is in SimHub and its device has another selected: what to do, in SimHub's own
        /// menus, with "Check again". Null when that is not known to be so.
        /// </summary>
        private FrameworkElement LedsFix(LedBar bar)
        {
            var issue = PanelAttention.Of(issues, PanelAttention.StripUnselected, bar.Namespace);
            if (issue == null) return null;
            var again = Ui.Button(issue.ActionLabel, PanelButtonKind.Outline, PanelButtonSize.Small);
            again.Click += (sender, args) => CheckAgain();
            var box = Ui.FixBox(PanelLeds.NotSelectedTitle, null, issue.Steps, again);
            box.Padding = new Thickness(PanelKit.FixPaddingX, PanelKit.FixPaddingYLights, PanelKit.FixPaddingX, PanelKit.FixPaddingYLights);
            return box;
        }

        /// <summary>
        /// The preview: the chips that pick a moment, a link to every device at once, and the strip drawn large
        /// with each group named under it.
        /// </summary>
        /// <remarks>
        /// Live is the car's own rev lights as the plugin has them, redrawn every second while they are loaded;
        /// the other chips are PanelEmulation's rules and never reach the hardware (#506). The groups sit in a
        /// Viewbox that only shrinks, so a 25-LED run fits a narrow window rather than running off it.
        /// </remarks>
        private FrameworkElement LedsPreview(LedBar bar, out Action redraw)
        {
            var ns = bar.Namespace;
            var ends = PanelLeds.Ends(bar.Shape);
            var centre = PanelLeds.Centre(bar.Shape);
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var preview = new Border { Child = row, Padding = new Thickness(0, 12, 0, 2), HorizontalAlignment = HorizontalAlignment.Center };
            var fit = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = preview };

            Action draw = () =>
            {
                var live = Settings.LedBarByNamespace(ns) ?? bar;
                var lights = plugin.CarLights;
                var running = lights.Ready && PanelLeds.UsesCarRevLights(Settings.BarRpmStyle(ns));
                var frame = PanelLeds.PreviewFrame(ledsScenario, ends, centre, LedsOptions(live), running, running ? lights.Run(centre) : null);
                var labels = PanelLeds.PreviewLabels(ends, centre);
                row.Children.Clear();
                for (var i = 0; i < frame.Length; i++)
                {
                    var strip = Ui.Strip(new[] { frame[i] }, StripStyle.Preview);
                    strip.HorizontalAlignment = HorizontalAlignment.Center;
                    var label = Ui.Eyebrow(i < labels.Length ? labels[i] : string.Empty);
                    label.HorizontalAlignment = HorizontalAlignment.Center;
                    var group = Ui.VStack(10, strip, label);
                    group.Margin = new Thickness(i == 0 ? 0 : 22, 0, 0, 0);
                    row.Children.Add(group);
                }
            };
            row.Opacity = LedsDim(ns);
            OnLighting(() => Ui.Redim(preview, LedsDim(ns)));

            var chips = new WrapPanel { Orientation = Orientation.Horizontal };
            Action drawChips = null;
            drawChips = () =>
            {
                chips.Children.Clear();
                foreach (var scenario in PanelLeds.Scenarios)
                {
                    var id = scenario.Key;
                    var chip = Ui.Chip(scenario.Value, id == ledsScenario, () =>
                    {
                        ledsScenario = id;
                        drawChips();
                        draw();
                    });
                    chip.Padding = new Thickness(PanelKit.ChipPaddingXLights, 0, PanelKit.ChipPaddingXLights, 0);
                    chip.Margin = new Thickness(0, 0, 6, 6);
                    chips.Children.Add(chip);
                }
            };
            drawChips();
            draw();
            OnTick(() =>
            {
                if (ledsScenario == PanelLeds.LiveScenario && plugin.CarLights.Ready) draw();
            });

            var link = Ui.LinkButton(PanelLeds.AllDevicesAtOnce);
            link.FontSize = Theme.SizeSmall;
            link.Height = PanelKit.ChipHeight;
            link.VerticalAlignment = VerticalAlignment.Top;
            link.Margin = new Thickness(12, 0, 0, 0);
            link.Click += (sender, args) => Go(PanelPage.Rig);
            var top = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(link, Dock.Right);
            top.Children.Add(link);
            top.Children.Add(chips);

            redraw = () =>
            {
                draw();
                Ui.Redim(preview, LedsDim(ns));
            };
            var body = Ui.VStack(8, top, fit);
            return new Border
            {
                Background = Ui.Brush(Theme.SurfaceInset),
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                CornerRadius = new CornerRadius(Theme.Radius),
                Padding = new Thickness(22, 18, 22, 16),
                Child = body,
            };
        }

        /// <summary>
        /// Rev lights: the #369 switch, the line saying whether the car in the sim is one Lovely Car Data has
        /// measured, the rig's width for the car's lights while the switch is on, and what the centre shows.
        /// </summary>
        private FrameworkElement LedsRevLights(LedBar bar, Action redrawPreview)
        {
            var ns = bar.Namespace;
            var carIcon = new ContentControl { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var carText = Ui.Prose(string.Empty);
            carText.VerticalAlignment = VerticalAlignment.Center;
            var carDock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(carIcon, Dock.Left);
            carDock.Children.Add(carIcon);
            carDock.Children.Add(carText);
            var carLine = Ui.SubRow(new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(0, PanelShell.RowPaddingY, 0, PanelShell.RowPaddingY),
                Child = carDock,
            });

            var width = Ui.SubRow(Ui.Anchor(LedsRow(PanelLeds.MirrorFitTitle,
                BuildSegmented(Contract.LedMirrorFits, PanelLights.MirrorFitLabels, Settings.LedMirrorFit, value =>
                {
                    Settings.LedMirrorFit = value;
                    Save();
                }), PanelLeds.MirrorFitCaption), PanelLeds.AnchorMirrorFit));

            string shown = null;
            Action show = () =>
            {
                var on = PanelLeds.UsesCarRevLights(Settings.BarRpmStyle(ns));
                width.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                var live = plugin.Live ?? LiveStatus.None;
                var known = plugin.LiveCarHasTable;
                var line = PanelLeds.CarLine(on, live.CarModel, known);
                carLine.Visibility = line == null ? Visibility.Collapsed : Visibility.Visible;
                var key = line + "|" + known;
                if (line == null || key == shown) return;
                shown = key;
                var hex = known ? Theme.StatusUpToDate : Theme.Caution;
                carText.Text = line;
                carText.Foreground = Ui.Brush(hex);
                carIcon.Content = known
                    ? Ui.Icon(PanelIcons.RingCheck, hex, 14, PanelIcons.RingBox)
                    : Ui.Icon(PanelIcons.Warning, hex, 14, PanelIcons.NavBox);
            };

            // #369: one switch, the car's own rev lights or not. On writes the car's own style and off the plain
            // left-to-right ladder, through the contract's set so it writes against the list in view (the set
            // still holds meetInMiddle and f1 so an old file reads, and a strip carrying one reads as off).
            var style = Ui.Switch(PanelLeds.UsesCarRevLights(Settings.BarRpmStyle(ns)), on =>
            {
                var live = Settings.LedBarByNamespace(ns);
                if (live != null) live.RpmStyle = Contract.NormaliseChoice(on ? Contract.LedRpmStyleCar : Contract.LedRpmStyleLeftToRight, Contract.LedRpmStyles, Contract.DefaultLedRpmStyle);
                Save();
                show();
                redrawPreview();
            });
            show();
            OnTick(show);

            var centre = Ui.ChoiceButton(PanelLights.CentreLabels, PanelLeds.CentreIndex(Settings.BarCentre(ns)), i =>
            {
                var live = Settings.LedBarByNamespace(ns);
                if (live != null) live.Centre = Contract.LedCentres[i];
                Save();
            }, 160);

            return Ui.VStack(0,
                Ui.Anchor(LedsHeading(PanelLeds.RevLightsTitle), PanelLeds.AnchorRevLights),
                Ui.Rows(
                    Ui.Anchor(LedsRow(PanelLeds.CarRevLightsTitle, style, PanelLeds.CarRevLightsCaption), PanelLeds.AnchorRevStyle),
                    carLine,
                    width,
                    Ui.Anchor(LedsRow(PanelLeds.CentreDisplayTitle, centre), PanelLeds.AnchorCentre)));
        }

        /// <summary>This strip: the SimHub device its profile is in, a brightness of its own, its direction, and
        /// the greyed test of each LED.</summary>
        private FrameworkElement LedsThisStrip(LedBar bar, IList<LedTarget> targets, IList<string> declined, Action redrawPreview)
        {
            var ns = bar.Namespace;
            var rows = new List<UIElement>
            {
                Ui.Anchor(BuildLedDeviceRow(targets, declined, Settings.BarDevice(ns), value => MoveLedBar(ns, value)), PanelLeds.AnchorDevice),
            };

            var brightness = Ui.ChoiceButton(PanelLeds.BrightnessLabels(Settings.LightsBrightness), PanelLeds.BrightnessIndex(Settings.BarBrightness(ns)), i =>
            {
                var value = PanelLeds.BrightnessValue(i);
                Settings.SetBarBrightness(ns, value);
                Save();
                redrawPreview();
                Say(PanelLeds.BrightnessSaid(bar.Name, value));
            }, 160);
            rows.Add(Ui.Anchor(LedsRow(PanelLeds.BrightnessTitle, brightness, null, Ui.NewTag()), PanelLeds.AnchorBrightness));

            // Only a shape that has a twin wired from the far end: a Fanatec wheel's wiring is its own.
            if (bar.SupportsReversal)
            {
                var reverse = Ui.Switch(Settings.BarReversed(ns), on => ReverseLedBar(ns, on));
                rows.Add(Ui.Anchor(LedsRow(PanelLeds.ReverseTitle, reverse, null, Ui.NewTag()), PanelLeds.AnchorReverse));
            }
            rows.Add(Ui.SoonRow(PanelSoon.EachLedInTurn, Ui.Button(PanelLeds.EachLedStart, PanelButtonKind.Outline, PanelButtonSize.Small)));

            return Ui.VStack(0,
                Ui.Anchor(LedsHeading(PanelLeds.ThisStripTitle), PanelLeds.AnchorThisStrip),
                Ui.Rows(rows.ToArray()));
        }

        /// <summary>
        /// A switch for each effect the strip's shape carries, three to a row, then how its flags move, whether a
        /// car alongside lights all of it where it has ends to light, and the greyed choice of where the pit
        /// limiter shows.
        /// </summary>
        private FrameworkElement LedsEffects(LedBar bar, Action redrawPreview)
        {
            var ns = bar.Namespace;
            var caption = Ui.Prose(PanelLeds.EffectsCaption);
            caption.VerticalAlignment = VerticalAlignment.Bottom;
            caption.Margin = new Thickness(12, 0, 0, 2);
            var head = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(caption, Dock.Right);
            head.Children.Add(caption);
            head.Children.Add(Ui.Heading(PanelLeds.EffectsTitle));

            var tiles = new List<UIElement>();
            foreach (var effect in PanelLeds.EffectsFor(bar.Shape))
            {
                var id = effect.Id;
                tiles.Add(LedsEffectTile(effect.Label, Settings.BarEffectEnabled(ns, id), on =>
                {
                    Settings.SetBarEffect(ns, id, on);
                    Save();
                    redrawPreview();
                }));
            }
            var grid = Ui.CardGrid(PanelLeds.EffectTileMinWidth, PanelLeds.EffectTileGap, PanelLeds.EffectColumns, tiles.ToArray());

            var rows = new List<UIElement>();
            var flags = LedsRow(PanelLeds.FlagAnimationTitle, Ui.Switch(Settings.BarFlagAnimation(ns), on =>
            {
                var live = Settings.LedBarByNamespace(ns);
                if (live != null) live.FlagAnimation = on;
                Save();
            }));
            // The first row under the grid has no rule over it, as the artboard draws it.
            flags.BorderThickness = new Thickness(0);
            rows.Add(Ui.SubRow(Ui.Anchor(flags, PanelLeds.AnchorFlagAnimation)));
            var shape = LightShape.Parse(bar.Shape);
            if (shape == null || PanelLeds.HasFullStripSpotter(shape.Left, shape.Right))
            {
                rows.Add(Ui.SubRow(Ui.Anchor(LedsRow(PanelLeds.SpotterTitle, Ui.Switch(Settings.BarSpotterWhole(ns), on =>
                {
                    var live = Settings.LedBarByNamespace(ns);
                    if (live != null) live.SpotterWhole = on;
                    Save();
                    redrawPreview();
                }), PanelLeds.SpotterCaption), PanelLeds.AnchorSpotter)));
            }
            var limiter = LedsRow(PanelSoon.PitLimiterLights.Title,
                BuildSegmented(PanelLeds.PitLimiterLightsValues, PanelLeds.PitLimiterLightsLabels, PanelLeds.PitLimiterLightsValues[1], value => { }));
            rows.Add(Ui.SubRow(Ui.Soon(limiter, PanelSoon.PitLimiterLights)));

            var under = Ui.Rows(rows.ToArray());
            under.Margin = new Thickness(0, 12, 0, 0);
            return Ui.VStack(0, head, grid, under);
        }

        /// <summary>The artboard's .fx: an effect's name and its switch, on the base ground, 10 by 12 in.</summary>
        private static Border LedsEffectTile(string label, bool on, Action<bool> changed)
        {
            var toggle = Ui.Switch(on, changed);
            toggle.Margin = new Thickness(12, 0, 0, 0);
            AutomationProperties.SetName(toggle, label);
            var text = Ui.Text(label, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary);
            text.TextWrapping = TextWrapping.Wrap;
            text.VerticalAlignment = VerticalAlignment.Center;
            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(toggle, Dock.Right);
            dock.Children.Add(toggle);
            dock.Children.Add(text);
            return new Border
            {
                Background = Ui.Brush(Theme.SurfaceBase),
                CornerRadius = new CornerRadius(Theme.Radius),
                Padding = new Thickness(12, 10, 12, 10),
                Child = dock,
            };
        }

        // --- Every strip ----------------------------------------------------------------------------------------

        private FrameworkElement LedsEveryStripSection()
        {
            var carData = Ui.SettingRow(PanelSoon.CarDataForAcAccLmu.Title, null);
            return LedsBlock(Ui.VStack(0,
                Ui.Anchor(LedsHeading(PanelLeds.EveryStripTitle), PanelLeds.AnchorEveryStrip),
                Ui.Rows(
                    Ui.SoonRow(PanelSoon.IdleSweep, Ui.Switch(true, on => { })),
                    Ui.SoonRow(PanelSoon.EngineStartAnimation, Ui.Switch(true, on => { })),
                    Ui.Anchor(BuildCarTablesRow(), PanelLeds.AnchorCarTables),
                    Ui.Soon(carData, PanelSoon.CarDataForAcAccLmu))));
        }

        /// <summary>
        /// Lovely Car Data: what is on disk, what the download is, whose measurements they are, and the button
        /// that fetches them.
        /// </summary>
        /// <remarks>
        /// A button rather than a setting, and that is the whole of #366. The tables used to arrive on
        /// their own during startup, weekly, gated by the update-check switch -- so a driver never chose
        /// to fetch them and was never told it had happened, and the reasoning about what is disclosed
        /// lived in a source comment. Two things are wrong with that. The visible one is that "the car's
        /// own" is the default bar style and its fallback is deliberately silent (ADR 0018 part 3), so a
        /// driver whose lights look generic had nothing to read and nothing to press. The other is the
        /// licence: OpenDash carries none of this data, and the copy being the user's own is what makes
        /// that work, which is a great deal truer of a press than of a background thread.
        ///
        /// The attribution stays underneath either way. CC BY-NC-SA 4.0 asks for it, and a driver is
        /// entitled to know whose numbers are lighting their wheel.
        /// </remarks>
        private FrameworkElement BuildCarTablesRow()
        {
            carTablesButton = Ui.Button(PanelLights.CarTablesButton(plugin.CarLights.CarCount), PanelButtonKind.Outline, PanelButtonSize.Small);
            carTablesButton.ToolTip = PanelLights.CarTablesButtonTooltip;
            carTablesButton.Click += (sender, args) => DownloadCarTables();

            var row = LedsRow(PanelLights.CarTablesTitle, carTablesButton);
            carTablesLine = Ui.Prose(string.Empty);
            carTablesLine.Margin = new Thickness(0, PanelKit.FixDetailGap, 0, 0);
            var parts = row.Tag as RowParts;
            var left = parts == null ? null : parts.TitleLine.Parent as StackPanel;
            if (left != null)
            {
                left.Children.Add(carTablesLine);
                var caption = Ui.Prose(PanelLights.CarTablesCaption);
                caption.Margin = new Thickness(0, PanelKit.FixDetailGap, 0, 0);
                left.Children.Add(caption);
                var attribution = Ui.Prose(PanelLights.CarTablesAttribution);
                attribution.Margin = new Thickness(0, PanelKit.FixDetailGap, 0, 0);
                left.Children.Add(attribution);
            }
            RefreshCarTables();
            return row;
        }

        /// <summary>The status line and the button's label, which are one answer and so are written together.</summary>
        private void RefreshCarTables()
        {
            if (carTablesLine == null) return;
            var service = plugin.CarLights;
            var line = service.Status;
            // A copy old enough that upstream has probably moved is mentioned and not acted on: nothing
            // refetches on its own any more, so the invitation is the whole of what staleness now does.
            if (service.Stale(DateTime.UtcNow)) line += " " + PanelLights.CarTablesStale;
            carTablesLine.Text = carTablesDownloading ? PanelLights.CarTablesDownloading : line;
            if (carTablesButton != null)
            {
                carTablesButton.Content = PanelLights.CarTablesButton(service.CarCount);
                carTablesButton.IsEnabled = !carTablesDownloading;
            }
        }

        /// <summary>
        /// The press: fetches, off the interface thread, and says what came back.
        /// </summary>
        /// <remarks>
        /// The answer may land after the page it was asked from has gone, which is why every control it
        /// writes to is checked first -- the same precaution the update check takes, for the same reason.
        /// </remarks>
        private void DownloadCarTables()
        {
            if (carTablesButton == null || carTablesDownloading) return;
            carTablesDownloading = true;
            RefreshCarTables();

            UpdateService.InBackground(() =>
            {
                plugin.CarLights.Download(DateTime.UtcNow);
                Dispatcher.Invoke(() =>
                {
                    carTablesDownloading = false;
                    RefreshCarTables();
                });
            }, new SimHubInstallLog());
        }

        // --- The strip's device and its presses -------------------------------------------------------------------

        /// <summary>
        /// The device picker: which of SimHub's LED devices a strip's profile goes to.
        /// </summary>
        /// <remarks>
        /// Three shapes rather than always a drop-down. A rig with one LED device has nothing to choose
        /// and is told where the profile went; a rig with none is told why there is nowhere for it to go,
        /// which is a thing about the rig rather than a failure; and only a rig with two or more is asked.
        ///
        /// A strip pointed at a device SimHub no longer has keeps its own entry at the top of the list,
        /// labelled as gone. Dropping it would silently re-point the strip at whatever sorted first, which
        /// is the class of bug this whole picker exists to close.
        ///
        /// A device SimHub has and OpenDash did not offer is named under the row in every shape, with
        /// the reason in SimHub's log: a wheel missing from the picker with nothing said about it is how
        /// #437 was reported, and the line would have answered it.
        /// </remarks>
        private static Border BuildLedDeviceRow(IList<LedTarget> targets, IList<string> declined, string current, Action<string> chosen)
        {
            if (targets.Count == 0)
            {
                return LedsRow(PanelLights.BarDeviceTitle, null, PanelLights.DeviceRowCaption(0, null, declined));
            }

            var ids = targets.Select(t => t.Id).ToList();
            var labels = targets.Select(t => t.Connected ? t.Name : t.Name + PanelLights.DeviceOffline).ToList();
            var known = ids.Contains(current, StringComparer.Ordinal);
            if (!known)
            {
                ids.Insert(0, current);
                labels.Insert(0, PanelLights.DeviceGone);
            }

            if (targets.Count == 1 && known)
            {
                return LedsRow(PanelLights.BarDeviceTitle, null,
                    PanelLights.DeviceRowCaption(targets.Count, PanelLights.OneDevice(labels[0]), declined));
            }

            var index = ids.FindIndex(id => string.Equals(id, current, StringComparison.Ordinal));
            var picker = Ui.ChoiceButton(labels.ToArray(), index, i =>
            {
                if (!string.Equals(ids[i], current, StringComparison.Ordinal)) chosen(ids[i]);
            }, 160);
            return LedsRow(PanelLights.BarDeviceTitle, picker, PanelLights.DeviceRowCaption(targets.Count, PanelLights.BarDeviceCaption, declined));
        }

        /// <summary>
        /// Moves one strip's profile to another of SimHub's LED devices.
        /// </summary>
        /// <remarks>
        /// Installing rather than recording: the profile is the whole point of the strip, and a setting
        /// that said "the wheel" while the profile sat in the Arduino's list would be the reported bug
        /// with a drop-down in front of it. The install takes the copy out of every device first.
        /// </remarks>
        private void MoveLedBar(string ns, string device)
        {
            var bar = Settings.LedBarByNamespace(ns);
            if (bar == null) return;
            bar.Device = LedBar.NormaliseDevice(device);
            Save();
            // By the profile the strip installs, which is the reversed twin for a strip wired from the far end.
            var found = EmbeddedProfileOf(bar);
            if (found == null)
            {
                Say(PanelMessage.Caution(PanelLights.BarMoveFailed(bar.Name)));
                return;
            }
            var plan = InstallBar(bar, found.Json);
            var ok = plan.State == FlagBoxInstallState.UpToDate;
            var target = LedTargets.Find(bar.Device);
            var line = ok ? PanelLeds.Moved(bar.Name, target == null ? null : target.Name) : PanelLights.BarMoveFailed(bar.Name);
            if (ok && plan.Note != null) line += " " + plan.Note;
            // The strip's device, and so whether its profile is selected, moved with the press: the card, the
            // fix box and the sidebar's dot are drawn again from what SimHub says now.
            RefreshAttention();
            RebuildPage();
            RefreshSidebar();
            Say(line, ok && plan.Note == null);
        }

        /// <summary>The presses on the strip: Install or Update where its profile needs one, Rename and Remove.</summary>
        private FrameworkElement BuildLedBarActions(string ns)
        {
            var presses = new List<UIElement>();
            var facts = StripFacts(ns);
            var action = PanelLeds.ProfileAction(facts == null ? null : facts.Profile);
            if (action != null)
            {
                var install = Ui.Button(action, PanelButtonKind.Outline, PanelButtonSize.Small);
                install.ToolTip = action == PanelLeds.UpdateProfile ? PanelLeds.UpdateTooltip : PanelLeds.InstallTooltip;
                install.Click += (sender, args) => InstallLedBarProfile(ns);
                presses.Add(install);
            }
            var rename = Ui.Button(PanelLeds.RenameButton, PanelButtonKind.Outline, PanelButtonSize.Small);
            rename.ToolTip = PanelLights.RenameBarTooltip;
            rename.Click += (sender, args) => ShowRenameLedBar(ns);
            presses.Add(rename);
            var remove = Ui.Button(PanelLeds.RemoveButton, PanelButtonKind.GhostDanger, PanelButtonSize.Small);
            remove.ToolTip = PanelLeds.RemoveTooltip;
            remove.Click += (sender, args) => ShowRemoveLedBar(ns);
            presses.Add(remove);
            return Ui.HStack(6, presses.ToArray());
        }

        /// <summary>Installs the strip's profile, or this build's newer version of it, into its device.</summary>
        private void InstallLedBarProfile(string ns)
        {
            var bar = Settings.LedBarByNamespace(ns);
            if (bar == null) return;
            var facts = StripFacts(ns);
            var update = facts != null && facts.Profile == FlagBoxInstallState.Outdated;
            var plan = ReinstallBar(bar);
            var ok = plan.State == FlagBoxInstallState.UpToDate;
            Redraw();
            var target = LedTargets.Find(bar.Device);
            var line = !ok ? PanelLeds.ProfileFailed(bar.Name)
                : update ? PanelLeds.ProfileUpdated(bar.Name)
                : PanelLeds.ProfileInstalled(bar.Name, target == null ? null : target.Name);
            if (ok && plan.Note != null) line += " " + plan.Note;
            Say(line, ok && plan.Note == null);
        }

        /// <summary>Turns the strip's direction round, which installs the other twin of its profile.</summary>
        private void ReverseLedBar(string ns, bool reversed)
        {
            var bar = Settings.LedBarByNamespace(ns);
            if (bar == null || !Settings.SetBarReversed(ns, reversed)) return;
            Save();
            var plan = ReinstallBar(bar);
            var ok = plan.State == FlagBoxInstallState.UpToDate;
            Redraw();
            var line = ok ? PanelLeds.ReverseSaid(bar.Name, reversed) : PanelLeds.ProfileFailed(bar.Name);
            if (ok && plan.Note != null) line += " " + plan.Note;
            Say(line, ok && plan.Note == null);
        }

        private void ShowRenameLedBar(string ns)
        {
            var bar = Settings.LedBarByNamespace(ns);
            if (bar == null) return;
            var name = Ui.Input(bar.Name);
            var save = Ui.Button(PanelLeds.RenameButton, PanelButtonKind.Primary, PanelButtonSize.Large);
            save.MinWidth = ButtonMinWidth;
            save.Click += (sender, args) => RenameLedBar(ns, name.Text);
            var cancel = Ui.Button("Cancel", PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.Click += (sender, args) => CloseSheet();
            var caption = Ui.Prose(PanelLights.BarNameCaption);
            caption.Margin = new Thickness(0, 8, 0, 0);
            ShowSheet("Rename " + bar.Name,
                Ui.VStack(0, name, caption),
                SheetFooter(null, cancel, save));
        }

        /// <summary>
        /// Renames the strip, and installs its profile again where SimHub has it, so SimHub's list carries the
        /// new name rather than the old one.
        /// </summary>
        private void RenameLedBar(string ns, string wanted)
        {
            var bar = Settings.LedBarByNamespace(ns);
            if (bar == null || string.IsNullOrWhiteSpace(wanted)) return;
            var facts = StripFacts(ns);
            var inSimHub = facts != null && (facts.Profile == FlagBoxInstallState.UpToDate || facts.Profile == FlagBoxInstallState.Outdated);
            Settings.RenameLedBar(ns, wanted);
            Save();
            var ok = true;
            if (inSimHub) ok = ReinstallBar(bar).State == FlagBoxInstallState.UpToDate;
            Redraw();
            Say(ok ? PanelLeds.Renamed(bar.Name, inSimHub) : PanelLeds.RenameNotInSimHub(bar.Name), ok);
        }

        /// <summary>Remove asks once, because it takes the profile out of SimHub as well.</summary>
        private void ShowRemoveLedBar(string ns)
        {
            var bar = Settings.LedBarByNamespace(ns);
            if (bar == null) return;
            var remove = Ui.Button(PanelLeds.RemoveConfirm, PanelButtonKind.Danger, PanelButtonSize.Large);
            remove.ToolTip = PanelLeds.RemoveTooltip;
            remove.Click += (sender, args) => RemoveLedBar(ns);
            var cancel = Ui.Button("Cancel", PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.Click += (sender, args) => CloseSheet();
            ShowSheet(PanelLeds.RemoveButton + " " + bar.Name, Ui.Prose(PanelLeds.RemoveBody, Theme.SizeBody), SheetFooter(null, cancel, remove));
        }

        private void RemoveLedBar(string ns)
        {
            var bar = Settings.LedBarByNamespace(ns);
            if (bar == null) return;
            var name = bar.Name;
            try
            {
                // Everywhere, not just the device the strip names: the device it was installed into may
                // have been removed from SimHub since, and a profile nothing attaches settings to any
                // more is a row in somebody's list that lights nothing.
                StripInstaller.UninstallEverywhere(LedBarProfile.IdFor(ns));
            }
            catch (Exception ex)
            {
                Log.Warn("The profile for " + name + " could not be taken out of SimHub: " + ex.Message);
            }
            Settings.RemoveLedBar(ns);
            Save();
            Select(PanelPage.Leds, null);
            Redraw();
            Say(PanelMessage.Info(PanelLeds.Removed(name)));
        }

        // --- The Add LEDs sheet ---------------------------------------------------------------------------------

        /// <summary>
        /// Adding a strip, in four steps: which hardware, its shape, the SimHub device its profile goes to, and
        /// its name. The press adds the strip and installs its profile.
        /// </summary>
        /// <remarks>
        /// The shapes are read out of the assembly rather than listed here, exactly as the Updates page's
        /// rows are: the census is what this build embedded, so a shape it does not carry is not offered
        /// and cannot be added as a strip whose profile does not exist.
        ///
        /// <para>The two numbers cannot say how a wheel is wired, so a Fanatec owner who added a strip by its
        /// counts got the plain 3/9/3, which lights only some of the wheel's LEDs and starts the bar from the
        /// middle of the rim. The Fanatec tile is that question, asked first because it decides the other two
        /// (#436), and offered only where the build embedded its profile.</para>
        /// </remarks>
        private void ShowAddLedBar()
        {
            var census = EmbeddedShapeIds();
            // Two numbers rather than a list of sixty-three. A driver knows how many LEDs their strip
            // has and how they are grouped, which is exactly A and B; a drop-down asked them to find
            // "3/9/3" among every other geometry and to know that is what their wheel is called.
            var sides = PanelLights.BarSides(census);
            var offersFanatec = PanelLights.OffersFanatec(census);
            if (sides.Length == 0 && !offersFanatec)
            {
                ShowSheet(PanelLights.AddBar, Ui.Prose(PanelLeds.NoProfiles), null);
                return;
            }

            // Which device gets the profile. SimHub keeps one profile list per LED device, so this is
            // not a detail: a strip installed into the wrong one is written, saved and verified correctly
            // into a list the hardware does not read, which is exactly what a rig reported.
            IList<string> notOffered;
            var targets = LedTargets.All(out notOffered);
            var found = PanelLeds.FoundFanatec(targets.Select(t => t.Name));
            var preferred = LedTargets.Preferred(targets);
            var device = preferred == null ? LedBar.ArduinoDevice : preferred.Id;

            var side = sides.Length == 0 ? PanelLights.FanatecSide : sides.Contains(3) ? 3 : sides[0];
            var centres = PanelLights.BarCentres(census, side);
            var centre = centres.Length == 0 ? PanelLights.FanatecCentre : centres.Contains(9) ? 9 : centres[0];
            // The one question the two numbers cannot answer: how the wheel is wired. Chosen, it decides them,
            // and side and centre keep what the driver chose so that Something else gives that back.
            var fanatec = sides.Length == 0 || PanelLeds.StartsOnFanatec(offersFanatec, found);

            var name = Ui.Input(string.Empty);
            var typed = false;
            var footerNote = Ui.Prose(string.Empty, PanelKit.CardMetaSize);
            var hardwareHost = new ContentControl();
            var shapeHost = new ContentControl();
            var deviceHost = new ContentControl();

            Action updateFooter = () =>
            {
                var target = targets.FirstOrDefault(t => string.Equals(t.Id, device, StringComparison.Ordinal));
                footerNote.Text = PanelLeds.InstallsOn(name.Text, target == null ? null : target.Name);
            };
            name.TextChanged += (sender, args) =>
            {
                typed = name.IsKeyboardFocusWithin;
                updateFooter();
            };
            Action refresh = () =>
            {
                if (!typed) name.Text = DefaultBarName(PanelLights.BarShapeId(side, centre, fanatec));
                updateFooter();
            };

            Action showShape = null;
            showShape = () =>
            {
                if (fanatec)
                {
                    var numerals = Ui.Text(PanelLeds.FanatecShape, 18, FontWeights.SemiBold, Theme.TextPrimary, PanelFonts.Data);
                    var fixedLabel = Ui.Eyebrow(PanelLeds.Fixed);
                    fixedLabel.VerticalAlignment = VerticalAlignment.Center;
                    var fixedDock = new DockPanel { LastChildFill = true };
                    DockPanel.SetDock(fixedLabel, Dock.Right);
                    fixedDock.Children.Add(fixedLabel);
                    fixedDock.Children.Add(Ui.VStack(4, numerals, Ui.Prose(PanelLeds.SetByTheWheel, PanelKit.CardMetaSize)));
                    shapeHost.Content = new Border
                    {
                        Background = Ui.Brush(Theme.SurfaceZone),
                        CornerRadius = new CornerRadius(Theme.Radius),
                        Padding = new Thickness(14, 12, 14, 12),
                        Child = fixedDock,
                    };
                    return;
                }

                var ends = BuildSegmented(
                    sides.Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray(),
                    sides.Select(PanelLeds.EndsLabel).ToArray(),
                    side.ToString(CultureInfo.InvariantCulture),
                    value =>
                    {
                        side = int.Parse(value, CultureInfo.InvariantCulture);
                        centres = PanelLights.BarCentres(census, side);
                        // A side of none reaches twenty-five and a side of four stops at twelve, so the choice
                        // of centre follows the choice of ends rather than offering lengths nothing is built for.
                        if (!centres.Contains(centre)) centre = centres.Contains(9) ? 9 : centres[0];
                        showShape();
                        refresh();
                    });
                var middle = Ui.ChoiceButton(
                    centres.Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray(),
                    Array.IndexOf(centres, centre),
                    i =>
                    {
                        centre = centres[i];
                        showShape();
                        refresh();
                    }, 90);
                var picture = Ui.Strip(PanelEmulation.StripFrame(side, centre, PanelEmulation.Yellow), StripStyle.AddLeds);
                picture.HorizontalAlignment = HorizontalAlignment.Center;
                var well = new Border { Background = Ui.Brush(Theme.SurfaceInset), CornerRadius = new CornerRadius(Theme.Radius), Child = picture };
                shapeHost.Content = Ui.VStack(12,
                    LedsSheetRow(PanelLights.BarEndsTitle, ends),
                    LedsSheetRow(PanelLights.BarCentreTitle, middle),
                    well,
                    Ui.Prose(PanelLights.BarShapeNote(side, centre, fanatec)));
            };

            Action showHardware = null;
            showHardware = () =>
            {
                var tiles = new List<UIElement>();
                if (offersFanatec)
                {
                    tiles.Add(LedsHardwareTile(PanelLights.BarFanatecTitle, found ? PanelLeds.FoundInSimHub : null,
                        PanelEmulation.StripFrame(PanelLights.FanatecSide, PanelLights.FanatecCentre, PanelEmulation.Yellow),
                        PanelLeds.FanatecShape, PanelLights.BarFanatecCaption, fanatec, () =>
                        {
                            fanatec = true;
                            showHardware();
                            showShape();
                            refresh();
                        }));
                }
                if (sides.Length > 0)
                {
                    tiles.Add(LedsHardwareTile(PanelLeds.SomethingElse, null,
                        PanelEmulation.StripFrame(0, 12, PanelEmulation.Idle),
                        PanelLeds.SomethingElseNote, null, !fanatec, () =>
                        {
                            fanatec = false;
                            showHardware();
                            showShape();
                            refresh();
                        }));
                }
                var grid = Ui.CardGrid(PanelLeds.HardwareTileMinWidth, PanelLeds.HardwareTileGap, 2, tiles.ToArray());
                hardwareHost.Content = offersFanatec
                    ? Ui.VStack(12, grid, LedsNote(PanelLeds.OtherWheelNote))
                    : (UIElement)grid;
            };

            Action showDevices = null;
            showDevices = () =>
            {
                var list = new StackPanel { Orientation = Orientation.Vertical };
                if (targets.Count == 0) list.Children.Add(Ui.Prose(PanelLights.DeviceRowCaption(0, null, notOffered)));
                foreach (var target in targets)
                {
                    var id = target.Id;
                    var radio = Ui.RadioRow(target.Name, target.Connected ? null : PanelLeds.NotConnected,
                        string.Equals(id, device, StringComparison.Ordinal), () =>
                        {
                            device = id;
                            showDevices();
                            updateFooter();
                        });
                    radio.Margin = new Thickness(0, 0, 0, 4);
                    list.Children.Add(radio);
                }
                foreach (var passed in notOffered ?? new string[0])
                {
                    var radio = Ui.RadioRow(passed, PanelLeds.NotReachable, false, null, false);
                    radio.Margin = new Thickness(0, 0, 0, 4);
                    list.Children.Add(radio);
                }
                deviceHost.Content = list;
            };

            showHardware();
            showShape();
            showDevices();
            refresh();

            var add = Ui.Button(PanelLeds.AddAndInstall, PanelButtonKind.Primary, PanelButtonSize.Large);
            add.MinWidth = ButtonMinWidth;
            add.Click += (sender, args) => AddLedBar(PanelLights.BarShapeId(side, centre, fanatec), name.Text, device);
            var cancel = Ui.Button("Cancel", PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.Click += (sender, args) => CloseSheet();

            var body = Ui.VStack(0,
                Ui.Step(1, PanelLeds.HardwareStep, hardwareHost, true),
                Ui.Step(2, PanelLeds.ShapeStep, shapeHost),
                Ui.Step(3, PanelLights.BarDeviceTitle, deviceHost),
                Ui.Step(4, PanelLights.BarNameTitle, name));
            ShowSheet(PanelLights.AddBar, body, Ui.VStack(14, footerNote, SheetFooter(null, cancel, add)));
        }

        /// <summary>A label and its control on one line, as the sheet's shape step draws them.</summary>
        private static FrameworkElement LedsSheetRow(string label, FrameworkElement control)
        {
            var text = Ui.Text(label, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary);
            text.VerticalAlignment = VerticalAlignment.Center;
            control.HorizontalAlignment = HorizontalAlignment.Right;
            control.VerticalAlignment = VerticalAlignment.Center;
            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(control, Dock.Right);
            dock.Children.Add(control);
            dock.Children.Add(text);
            return dock;
        }

        /// <summary>A hardware tile, AddLeds' .hw: its name with an eyebrow on the right, a picture of it and a
        /// line under it.</summary>
        private static Button LedsHardwareTile(string title, string eyebrow, IList<string[]> frame, string note, string tooltip, bool selected, Action pick)
        {
            var head = new DockPanel { LastChildFill = true };
            if (eyebrow != null)
            {
                var found = Ui.Eyebrow(eyebrow, Theme.StatusUpToDate);
                found.VerticalAlignment = VerticalAlignment.Center;
                found.Margin = new Thickness(8, 0, 0, 0);
                DockPanel.SetDock(found, Dock.Right);
                head.Children.Add(found);
            }
            head.Children.Add(Ui.Text(title, PanelKit.LightCardNameSize, FontWeights.SemiBold, Theme.TextPrimary));
            var tile = Ui.ChoiceTile(Ui.VStack(10, head, Ui.Strip(frame, StripStyle.Card), Ui.Prose(note, PanelKit.CardMetaSize)), selected, pick);
            if (tooltip != null) tile.ToolTip = tooltip;
            AutomationProperties.SetName(tile, title);
            return tile;
        }

        /// <summary>The sheet's note under the tiles: the ringed i and one line, inside a dashed rule.</summary>
        private static FrameworkElement LedsNote(string text)
        {
            var icon = Ui.Icon(PanelIcons.Info, Theme.TextSecondary, PanelIcons.Box);
            icon.VerticalAlignment = VerticalAlignment.Center;
            var line = Ui.Prose(text, PanelKit.CardMetaSize);
            line.VerticalAlignment = VerticalAlignment.Center;
            var dashes = new DoubleCollection { 3, 3 };
            dashes.Freeze();
            var rule = new Rectangle
            {
                Stroke = Ui.Brush(Theme.Border),
                StrokeThickness = PanelMetrics.BorderWeight,
                StrokeDashArray = dashes,
                RadiusX = Theme.Radius,
                RadiusY = Theme.Radius,
            };
            var grid = new Grid();
            grid.Children.Add(rule);
            grid.Children.Add(new Border { Padding = new Thickness(12, 10, 12, 10), Child = Ui.HStack(10, icon, line) });
            return grid;
        }

        /// <summary>What the name box opens on: "Wheel rim" for the Fanatec wheel and "Strip" for anything else,
        /// numbered as the rig numbers a second one. SimHub's list shows the profile under this name.</summary>
        private string DefaultBarName(string shape)
        {
            return PanelLeds.DefaultName(shape, Settings.LedBarList().Where(bar => bar != null).Select(bar => bar.Name));
        }

        private void AddLedBar(string shape, string name, string device)
        {
            var bar = Settings.AddLedBar(shape, string.IsNullOrWhiteSpace(name) ? DefaultBarName(shape) : name, device);
            Save();
            Select(PanelPage.Leds, bar.Namespace);
            var found = EmbeddedProfileOf(bar);
            var embedded = found == null ? null : found.Json;
            var ok = embedded != null;
            string note = null;
            if (ok)
            {
                var plan = InstallBar(bar, embedded);
                ok = plan.State == FlagBoxInstallState.UpToDate;
                note = plan.Note;
            }
            Redraw();
            // A note is not a failure, so the line stays the ordinary one and gains a sentence. The one
            // note there is says the device is listing its maker's built-in profiles, which is the only
            // way an install can be correct and still leave nothing for the driver to select.
            var target = LedTargets.Find(bar.Device);
            var line = ok ? PanelLights.BarAdded(bar.Name, target == null ? null : target.Name) : PanelLights.BarAddFailed(bar.Name);
            if (ok && note != null) line += " " + note;
            Say(line, ok && note == null);
        }
    }
}
