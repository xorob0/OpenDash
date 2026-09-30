// SettingsControl.Home.cs: the Home page, drawn to Main.dc.html -- what needs fixing, in the driver's terms and
// with the steps in SimHub; what each screen, strip and matrix is showing right now; and the two controls a
// driver reaches for between sessions.
//
// This file only draws. Every word, number and rule is PanelHome's (and PanelAttention's), where
// PanelHomeTests holds it. What needs fixing is asked of SimHub on Go and on "Check again"
// (SettingsControl.Status.cs), never on the tick; the tick only repaints the strips from the car's own run,
// which is a property the plugin already holds.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private FrameworkElement BuildHomePage(PanelRoute to)
        {
            DrawsLighting();
            var head = new StackPanel { Orientation = Orientation.Vertical };
            var eyebrow = Ui.Eyebrow(PanelHome.Title);
            eyebrow.Margin = new Thickness(0, 0, 0, PanelHome.HeaderGap);
            head.Children.Add(eyebrow);
            head.Children.Add(Ui.PageTitle(PanelAttention.Headline(issues.Count)));

            var sections = new List<FrameworkElement>();
            if (issues.Count > 0) sections.Add(Ui.Anchor(HomeAttentionCard(), PanelHome.AnchorAttention));
            sections.Add(Ui.Anchor(HomeRightNow(), PanelHome.AnchorRightNow));
            sections.Add(Ui.Anchor(HomeQuickControls(), PanelHome.AnchorQuickControls));

            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(head);
            foreach (var section in sections)
            {
                section.Margin = new Thickness(0, PanelShell.SectionGapFor(PanelPage.Home), 0, 0);
                stack.Children.Add(section);
            }
            return stack;
        }

        // --- What needs fixing ---------------------------------------------------------------------------------

        /// <summary>The issues, one row each, ruled apart: an icon on the caution's tint, what is wrong and what
        /// to do, the steps in SimHub where there are some, and the one press that helps.</summary>
        private FrameworkElement HomeAttentionCard()
        {
            var rows = new StackPanel { Orientation = Orientation.Vertical };
            for (var i = 0; i < issues.Count; i++)
            {
                var row = HomeIssueRow(issues[i]);
                if (i > 0)
                {
                    row.BorderBrush = Ui.Brush(Theme.Rule);
                    row.BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0);
                }
                rows.Children.Add(row);
            }
            return Ui.CardBox(rows, 0);
        }

        private Border HomeIssueRow(PanelIssue issue)
        {
            // A row with steps hangs from its top, as the artboard's second row does; a one-line row is centred.
            var hasSteps = issue.Steps.Count > 0;
            var align = hasSteps ? VerticalAlignment.Top : VerticalAlignment.Center;

            var icon = Ui.NavIcon(PanelHome.IssueIcon(issue), Theme.Caution, PanelHome.IconSize);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
            var well = new Border
            {
                Width = PanelHome.IconWell,
                Height = PanelHome.IconWell,
                CornerRadius = new CornerRadius(Theme.Radius),
                Background = Ui.Tint(Theme.Caution, PanelHome.IconTint),
                VerticalAlignment = align,
                Margin = new Thickness(0, 0, PanelHome.IconGap, 0),
                Child = icon,
            };

            var text = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = align };
            var title = Ui.Text(issue.Title, PanelShell.RowTitleSize, FontWeights.SemiBold, Theme.TextPrimary);
            title.TextWrapping = TextWrapping.Wrap;
            text.Children.Add(title);
            if (!string.IsNullOrEmpty(issue.Detail))
            {
                var detail = Ui.Prose(issue.Detail, PanelHome.DetailSize);
                detail.Margin = new Thickness(0, PanelHome.DetailGap, 0, 0);
                text.Children.Add(detail);
            }
            if (hasSteps)
            {
                var steps = Ui.Steps(issue.Steps);
                steps.Margin = new Thickness(0, PanelHome.StepsGap, 0, 0);
                text.Children.Add(steps);
            }

            var press = Ui.Button(issue.ActionLabel, PanelButtonKind.Outline);
            press.Click += (sender, args) => HomeAct(issue);

            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(well, Dock.Left);
            dock.Children.Add(well);
            if (PanelHome.PressBeside(TwoColumns))
            {
                press.VerticalAlignment = align;
                press.Margin = new Thickness(PanelHome.IconGap, 0, 0, 0);
                DockPanel.SetDock(press, Dock.Right);
                dock.Children.Add(press);
                dock.Children.Add(text);
            }
            else
            {
                press.HorizontalAlignment = HorizontalAlignment.Left;
                press.Margin = new Thickness(0, PanelHome.StepsGap, 0, 0);
                text.Children.Add(press);
                dock.Children.Add(text);
            }
            return new Border { Padding = new Thickness(PanelHome.IssuePaddingX, PanelHome.IssuePaddingY, PanelHome.IssuePaddingX, PanelHome.IssuePaddingY), Child = dock };
        }

        /// <summary>What an issue's press does: opens its page with the thing selected, asks SimHub again, or
        /// writes a screen's dashboard back (PanelHome.Press decides which).</summary>
        private void HomeAct(PanelIssue issue)
        {
            switch (PanelHome.Press(issue))
            {
                case HomePress.CheckAgain:
                    CheckAgain();
                    Say(PanelHome.CheckedAgain(issue, issues));
                    return;
                case HomePress.Reinstall:
                    var screen = Settings.ScreenByNamespace(issue.Subject);
                    if (screen != null)
                    {
                        InstallScreenAgain(screen);
                        return;
                    }
                    Open(issue.Page, issue.Subject, issue.Anchor);
                    return;
                case HomePress.Open:
                    Open(issue.Page, issue.Subject, issue.Anchor);
                    return;
                default:
                    Go(issue.Route);
                    return;
            }
        }

        // --- Right now -----------------------------------------------------------------------------------------

        /// <summary>One card per kind of device, each listing every device of that kind in rig order, or the
        /// empty rig's card alone when there is nothing at all.</summary>
        private FrameworkElement HomeRightNow()
        {
            var screens = Settings.RigScreens();
            var strips = Settings.LedBarList();
            var matrices = Settings.MatrixPanels().ToList();
            if (PanelHome.RigEmpty(screens.Count, strips.Count, matrices.Count))
                return PageSection(PanelHome.RightNowTitle, HomeEmptyRig());

            var dim = PanelEmulation.Dim(Settings.LightsNightMode, Settings.LightsNightBrightness);
            var grid = Ui.CardGrid(PanelHome.CardMinWidth, PanelHome.CardGap, PanelHome.CardMax,
                HomeCard(PanelPage.Screens, screens.Select(HomeScreenRow).ToList(), PanelScreens.NoScreens),
                HomeCard(PanelPage.Leds, HomeStripRows(strips, dim), PanelLeds.NoStrips),
                HomeCard(PanelPage.Matrix, matrices.Select(m => HomeMatrixRow(m, dim)).ToList(), PanelMatrix.NoPanels));
            return PageSection(PanelHome.RightNowTitle, grid);
        }

        /// <summary>The rig's first minute: the Screens page's own empty state and the press that goes there.</summary>
        private FrameworkElement HomeEmptyRig()
        {
            var title = Ui.Text(PanelScreens.NoScreens, PanelShell.RowTitleSize, FontWeights.SemiBold, Theme.TextPrimary);
            var line = Ui.Prose(PanelCopy.EmptyRig, PanelHome.DetailSize);
            line.Margin = new Thickness(0, PanelHome.DetailGap, 0, 0);
            var open = Ui.Button(PanelHome.OpenScreens, PanelButtonKind.Primary);
            open.HorizontalAlignment = HorizontalAlignment.Left;
            open.Margin = new Thickness(0, PanelHome.StepsGap + 4, 0, 0);
            open.Click += (sender, args) => Go(PanelPage.Screens);
            var stack = Ui.VStack(0, title, line, open);
            return Ui.CardBox(stack, PanelHome.IssuePaddingX);
        }

        /// <summary>A card: the page's name and an Open link over its rows, or over the page's own empty state.</summary>
        private FrameworkElement HomeCard(PanelPage page, IList<Button> rows, string empty)
        {
            var open = Ui.LinkButton(PanelHome.OpenLink);
            open.Height = double.NaN;
            open.FontSize = PanelHome.OpenLinkSize;
            open.VerticalAlignment = VerticalAlignment.Center;
            open.Click += (sender, args) => Go(page);
            var eyebrow = Ui.Eyebrow(PanelNav.Label(page));
            eyebrow.VerticalAlignment = VerticalAlignment.Center;
            var headDock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(open, Dock.Right);
            headDock.Children.Add(open);
            headDock.Children.Add(eyebrow);
            var head = new Border
            {
                Padding = new Thickness(PanelHome.CardHeadPaddingX, PanelHome.CardHeadPaddingTop, PanelHome.CardHeadPaddingX, PanelHome.CardHeadPaddingBottom),
                Child = headDock,
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(head);
            if (rows.Count == 0)
            {
                var none = Ui.Prose(PanelHome.EmptyLine(empty), PanelHome.DetailSize);
                none.Margin = new Thickness(PanelHome.RowPaddingX, 0, PanelHome.RowPaddingX, PanelHome.RowPaddingY + PanelHome.CardHeadPaddingBottom);
                stack.Children.Add(none);
            }
            foreach (var row in rows) stack.Children.Add(row);
            return Ui.CardBox(stack, 0);
        }

        /// <summary>A screen: its name and size, what it shows now (or what SimHub is missing), and its state.</summary>
        private Button HomeScreenRow(ScreenInstance screen)
        {
            var facts = ScreenFacts(screen.Namespace);
            var restart = PanelAttention.Has(issues, PanelAttention.ScreenRestart, screen.Namespace);
            var line = PanelHome.ScreenLine(Settings, screen, facts == null ? null : facts.Installed, restart);

            var dot = HomeDot(line.DotHex);
            var text = Ui.VStack(0, HomeNameLine(screen.Name, PanelHome.ScreenSize(screen)), HomeLineText(line));
            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(dot, Dock.Right);
            dock.Children.Add(dot);
            dock.Children.Add(text);
            var ns = screen.Namespace;
            return HomeRow(dock, () => Open(PanelPage.Screens, ns));
        }

        /// <summary>A strip on the Home card, and what its row needs to repaint itself on the tick.</summary>
        private sealed class HomeStrip
        {
            public LedBar Bar;
            public int Ends;
            public int Centre;
            public double Dim;
            public Border Host;
            public TextBlock Line;
            public Ellipse Dot;
            public string Painted;
        }

        /// <summary>
        /// The strips, each with its name, shape and state over its picture and its line; the pictures and the
        /// lines follow the car's own run once a second.
        /// </summary>
        private IList<Button> HomeStripRows(IReadOnlyList<LedBar> bars, double dim)
        {
            var rows = new List<Button>();
            var live = new List<HomeStrip>();
            foreach (var bar in bars)
            {
                if (bar == null) continue;
                var span = PanelHome.StripSpan(bar.Shape);
                var strip = new HomeStrip
                {
                    Bar = bar,
                    Ends = span[0],
                    Centre = span[1],
                    Dim = dim,
                    Host = new Border { HorizontalAlignment = HorizontalAlignment.Left },
                    Line = Ui.Text(string.Empty, PanelHome.LineSize, FontWeights.Normal, Theme.TextSecondary),
                    Dot = HomeDot(null),
                };
                strip.Line.TextTrimming = TextTrimming.CharacterEllipsis;
                HomePaintStrip(strip);
                live.Add(strip);

                var top = new DockPanel { LastChildFill = true };
                strip.Dot.Margin = new Thickness(PanelHome.MetaGap, 0, 0, 0);
                DockPanel.SetDock(strip.Dot, Dock.Right);
                top.Children.Add(strip.Dot);
                var shape = PanelHome.StripShape(bar);
                if (shape.Length > 0)
                {
                    var meta = Ui.Text(shape, PanelHome.MetaSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
                    meta.VerticalAlignment = VerticalAlignment.Center;
                    meta.Margin = new Thickness(PanelHome.MetaGap, 0, 0, 0);
                    DockPanel.SetDock(meta, Dock.Right);
                    top.Children.Add(meta);
                }
                var name = Ui.Text(bar.Name ?? string.Empty, PanelHome.NameSize, FontWeights.SemiBold, Theme.TextPrimary);
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                name.VerticalAlignment = VerticalAlignment.Center;
                top.Children.Add(name);

                var ns = bar.Namespace;
                rows.Add(HomeRow(Ui.VStack(PanelHome.StripRowGap, top, strip.Host, strip.Line), () => Open(PanelPage.Leds, ns)));
            }
            if (live.Count > 0) OnTick(() => { foreach (var strip in live) HomePaintStrip(strip); });
            return rows;
        }

        /// <summary>Draws a strip's picture and line from the car's own run as it is now, and only when either
        /// moved: the run the plugin already holds, and the facts the last Go read.</summary>
        private void HomePaintStrip(HomeStrip strip)
        {
            var facts = StripFacts(strip.Bar.Namespace);
            var profile = facts == null ? null : facts.Profile;
            var selected = facts == null ? null : facts.Selected;
            var cars = plugin.CarLights;
            var ns = strip.Bar.Namespace;
            var live = cars != null && PanelHome.StripLive(cars.Ready, Settings.BarRpmStyle(ns), Settings.BarCentre(ns), profile, selected);
            var run = live ? cars.Run(strip.Centre) : null;
            var line = PanelHome.StripLine(live, live ? cars.CarName : null, profile, selected);
            var painted = (run ?? string.Empty) + "|" + line.Text + "|" + line.TextHex + "|" + line.DotHex;
            if (painted == strip.Painted) return;
            strip.Painted = painted;

            // Wider strips than the card shrink to it rather than spilling out of it.
            strip.Host.Child = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = Ui.Strip(PanelEmulation.LiveFrame(run, strip.Ends, strip.Centre), StripStyle.Home, strip.Dim),
            };
            strip.Line.Text = line.Text;
            strip.Line.Foreground = Ui.Brush(line.TextHex);
            strip.Line.Visibility = line.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            HomeSetDot(strip.Dot, line.DotHex);
        }

        /// <summary>A matrix: its idle glyph (dark when no device shows it), its name and its content number.</summary>
        private Button HomeMatrixRow(int slot, double dim)
        {
            var facts = MatrixFacts(slot);
            var shown = facts == null ? null : facts.Shown;
            var line = PanelHome.MatrixLine(slot, Settings.MatrixRest(slot), shown,
                PanelAttention.Has(issues, PanelAttention.FlagBoxOutdated));
            IList<string> cells = new string[64];
            if (PanelHome.MatrixDrawsGlyph(shown))
            {
                var options = new MatrixOptions
                {
                    Rest = Settings.MatrixRest(slot),
                    Side = Settings.MatrixSide(slot),
                    Bands = Settings.MatrixGearBands(slot),
                    CarLadder = Settings.MatrixGearCarLadder(slot),
                };
                cells = PanelEmulation.MatrixFrame(GlyphSheet, PanelEmulation.Idle, options);
            }
            var picture = Ui.Matrix(cells, MatrixStyle.Home, dim);
            picture.VerticalAlignment = VerticalAlignment.Center;
            picture.Margin = new Thickness(0, 0, PanelHome.RowGap, 0);

            var name = Ui.Text(Settings.MatrixName(slot) ?? PanelHome.MatrixName(slot), PanelHome.NameSize, FontWeights.SemiBold, Theme.TextPrimary);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            var text = Ui.VStack(0, name, HomeLineText(line));
            text.VerticalAlignment = VerticalAlignment.Center;
            var dot = HomeDot(line.DotHex);

            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(picture, Dock.Left);
            DockPanel.SetDock(dot, Dock.Right);
            dock.Children.Add(picture);
            dock.Children.Add(dot);
            dock.Children.Add(text);
            var id = slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return HomeRow(dock, () => Open(PanelPage.Matrix, id));
        }

        /// <summary>A name with its figure after it, as the Screens rows draw "Main dash 1280 × 480".</summary>
        private static FrameworkElement HomeNameLine(string name, string meta)
        {
            var dock = new DockPanel { LastChildFill = false };
            var title = Ui.Text(name ?? string.Empty, PanelHome.NameSize, FontWeights.SemiBold, Theme.TextPrimary);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(title, Dock.Left);
            dock.Children.Add(title);
            if (!string.IsNullOrEmpty(meta))
            {
                var figure = Ui.Text(meta, PanelHome.MetaSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
                figure.VerticalAlignment = VerticalAlignment.Center;
                figure.Margin = new Thickness(PanelHome.MetaGap, 0, 0, 0);
                DockPanel.SetDock(figure, Dock.Left);
                dock.Children.Add(figure);
            }
            return dock;
        }

        /// <summary>A device's line under its name, one line and trimmed.</summary>
        private static TextBlock HomeLineText(HomeLine line)
        {
            var text = Ui.Text(line.Text, PanelHome.LineSize, FontWeights.Normal, line.TextHex);
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Margin = new Thickness(0, PanelHome.LineGap, 0, 0);
            if (line.Text.Length == 0) text.Visibility = Visibility.Collapsed;
            return text;
        }

        /// <summary>The state dot, which keeps its room when there is nothing to say so the rows line up.</summary>
        private static Ellipse HomeDot(string hex)
        {
            var dot = new Ellipse
            {
                Width = PanelHome.DotSize,
                Height = PanelHome.DotSize,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(PanelHome.RowGap, 0, 0, 0),
            };
            HomeSetDot(dot, hex);
            return dot;
        }

        private static void HomeSetDot(Ellipse dot, string hex)
        {
            dot.Fill = hex == null ? null : Ui.Brush(hex);
            dot.Visibility = hex == null ? Visibility.Hidden : Visibility.Visible;
        }

        /// <summary>A device row: the artboard's .rowlink, a press as wide as the card with a rule on top that
        /// takes the hover ground under the pointer.</summary>
        private static Button HomeRow(UIElement content, Action click)
        {
            var row = new Button
            {
                Content = content,
                Padding = new Thickness(PanelHome.RowPaddingX, PanelHome.RowPaddingY, PanelHome.RowPaddingX, PanelHome.RowPaddingY),
                Background = Brushes.Transparent,
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Cursor = System.Windows.Input.Cursors.Hand,
                Template = HomeRowTemplate(),
                FocusVisualStyle = Ui.FocusRing(),
            };
            row.Click += (sender, args) => click();
            return row;
        }

        private static ControlTemplate homeRowTemplate;

        private static ControlTemplate HomeRowTemplate()
        {
            if (homeRowTemplate != null) return homeRowTemplate;
            var chrome = new FrameworkElementFactory(typeof(Border), "chrome");
            chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            chrome.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            chrome.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            chrome.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            chrome.AppendChild(presenter);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = chrome };
            var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            over.Setters.Add(new Setter(Border.BackgroundProperty, Ui.Brush(Theme.Hover), "chrome"));
            template.Triggers.Add(over);
            homeRowTemplate = template;
            return template;
        }

        // --- Quick controls ------------------------------------------------------------------------------------

        /// <summary>
        /// Brightness, night mode, and the Rig page. The slider edits the brightness in force: the night one
        /// while night mode is on and the day one otherwise, which is the rule the wheel buttons follow too.
        /// The three sit side by side where two blocks fit, and stack where they do not.
        /// </summary>
        private FrameworkElement HomeQuickControls()
        {
            var nightOn = Settings.LightsNightMode;
            var value = PanelHome.BrightnessInForce(nightOn, Settings.LightsBrightness, Settings.LightsNightBrightness);
            var numeral = Ui.Text(PanelHome.Percent(value), PanelHome.QuickValueSize, FontWeights.SemiBold, Theme.TextPrimary, PanelFonts.Data);
            System.Windows.Documents.Typography.SetNumeralAlignment(numeral, FontNumeralAlignment.Tabular);
            numeral.VerticalAlignment = VerticalAlignment.Center;
            // The value is saved once, when the hand lets go or a key moves it; the figure follows every step.
            var slider = Ui.Slider(value, v =>
            {
                if (Settings.LightsNightMode)
                {
                    if (v == Settings.LightsNightBrightness) return; Settings.LightsNightBrightness = v; Save(); ShowLightingChange();
                }
                else
                {
                    if (v == Settings.LightsBrightness) return; Settings.LightsBrightness = v; Save(); ShowLightingChange();
                }
            }, v => numeral.Text = PanelHome.Percent(v), false);
            var label = Ui.Eyebrow(PanelHome.BrightnessLabel(nightOn));
            label.VerticalAlignment = VerticalAlignment.Center;
            var labelLine = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(numeral, Dock.Right);
            labelLine.Children.Add(numeral);
            labelLine.Children.Add(label);

            var night = Ui.Switch(nightOn, on =>
            {
                Settings.LightsNightMode = on;
                Save();
                ShowLightingChange();
            });
            night.HorizontalAlignment = HorizontalAlignment.Left;

            var rig = Ui.Button(PanelHome.OpenRig, PanelButtonKind.Outline, PanelButtonSize.Small);
            rig.Padding = new Thickness(PanelHome.QuickRigPaddingX, 0, PanelHome.QuickRigPaddingX, 0);
            rig.HorizontalAlignment = HorizontalAlignment.Left;
            rig.Click += (sender, args) => Go(PanelPage.Rig);

            var cells = new FrameworkElement[]
            {
                Ui.VStack(PanelHome.QuickGap, labelLine, slider),
                Ui.VStack(PanelHome.QuickGap, Ui.Eyebrow(PanelSettings.NightModeTitle), night),
                Ui.VStack(PanelHome.QuickGap, Ui.Eyebrow(PanelHome.TryTitle), rig),
            };

            var beside = TwoColumns;
            Panel body;
            if (beside)
            {
                var grid = new Grid();
                foreach (var share in PanelHome.QuickColumns)
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(share, GridUnitType.Star) });
                body = grid;
            }
            else
            {
                body = new StackPanel { Orientation = Orientation.Vertical };
            }
            for (var i = 0; i < cells.Length; i++)
            {
                var rule = i == 0 ? 0 : PanelMetrics.BorderWeight;
                var cell = new Border
                {
                    Padding = new Thickness(PanelHome.QuickPaddingX, PanelHome.QuickPaddingY, PanelHome.QuickPaddingX, PanelHome.QuickPaddingY),
                    BorderBrush = Ui.Brush(Theme.Rule),
                    BorderThickness = beside ? new Thickness(rule, 0, 0, 0) : new Thickness(0, rule, 0, 0),
                    Child = cells[i],
                };
                if (beside) Grid.SetColumn(cell, i);
                body.Children.Add(cell);
            }
            return PageSection(PanelHome.QuickControlsTitle, Ui.CardBox(body, 0));
        }
    }
}
