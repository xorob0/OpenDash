// SettingsControl.Home.cs: the Home page, drawn to Main.dc.html -- what needs fixing, in the driver's terms and
// with the steps in SimHub; what each screen, strip and matrix is showing right now; and the two controls a
// driver reaches for between sessions.
//
// This file only draws. Every word, number and rule is PanelHome's (and PanelAttention's), where
// PanelHomeTests holds it. What needs fixing is asked of SimHub on Go and on "Check again"
// (SettingsControl.Status.cs), never on the tick, and each device's facts are copied at build from that one
// answer, so a row never says something the headline and the attention card were not drawn from. The tick
// repaints the strips from the car's own run, which is a property the plugin already holds, and each screen's
// line from its live zone pages, which are settings a wheel button writes: both reads, never a call into
// SimHub. An update check's answer can land while Home is showing, and adds or drops a row, so Home hears it
// and draws itself again.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private FrameworkElement BuildHomePage(PanelRoute to)
        {
            var screens = Settings.RigScreens();
            var strips = Settings.LedBarList();
            var matrices = Settings.MatrixPanels().ToList();
            var empty = PanelHome.RigEmpty(screens.Count, strips.Count, matrices.Count);
            // Only the rig's quick controls draw night mode and a brightness, and an empty rig has none.
            if (!empty) DrawsLighting();
            // The answer has already refreshed the issues and the facts when this runs; RebuildPage keeps the lines
            // and the scroll. Only an answer that moved what the headline, the fix rows and the lines were drawn
            // from rebuilds: "you have the newest release" lands mid-drag as often as any other, and a rebuild
            // then takes the slider or a pressed row from under the pointer.
            var drawn = PanelHome.DrawnFrom(issues, HomeFacts(screens, strips, matrices));
            OnUpdate(null, manual =>
            {
                if (PanelHome.DrawnFrom(issues, HomeFacts(screens, strips, matrices)) != drawn) RebuildPage();
            });
            var head = new StackPanel { Orientation = Orientation.Vertical };
            var eyebrow = Ui.Eyebrow(PanelHome.Title);
            eyebrow.Margin = new Thickness(0, 0, 0, PanelHome.HeaderGap);
            head.Children.Add(eyebrow);
            head.Children.Add(Ui.PageTitle(PanelAttention.Headline(issues.Count)));

            // What needs fixing stays on an empty rig: it can still wait on an update, and the headline counts
            // it, so a count over no rows would be a number nobody can read. Otherwise the empty rig is the
            // only thing on the page (voice.md), in place of Right now and the quick controls, which have
            // nothing to show or to change: it takes Right now's anchor, so search's "Right now" lands on it,
            // and an entry for the quick controls lands at the top of a page short enough to show it all.
            var sections = new List<FrameworkElement>();
            if (issues.Count > 0) sections.Add(Ui.Anchor(HomeAttentionCard(), PanelHome.AnchorAttention));
            if (empty)
            {
                sections.Add(Ui.Anchor(HomeEmptyRig(), PanelHome.AnchorRightNow));
            }
            else
            {
                sections.Add(Ui.Anchor(HomeRightNow(screens, strips, matrices), PanelHome.AnchorRightNow));
                sections.Add(Ui.Anchor(HomeQuickControls(), PanelHome.AnchorQuickControls));
            }

            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(head);
            foreach (var section in sections)
            {
                section.Margin = new Thickness(0, PanelShell.SectionGapFor(PanelPage.Home), 0, 0);
                stack.Children.Add(section);
            }
            return stack;
        }

        /// <summary>Each device's facts as its Right now row copies them at build: a screen's Installed, a
        /// strip's Profile and Selected, a matrix's Shown.</summary>
        private IEnumerable<string> HomeFacts(IEnumerable<ScreenInstance> screens, IEnumerable<LedBar> strips, IEnumerable<int> matrices)
        {
            var facts = new List<string>();
            foreach (var screen in screens.Where(screen => screen != null))
            {
                var fact = ScreenFacts(screen.Namespace);
                facts.Add(PanelHome.FactKey("screen", screen.Namespace, fact == null ? null : fact.Installed));
            }
            foreach (var bar in strips.Where(bar => bar != null))
            {
                var fact = StripFacts(bar.Namespace);
                facts.Add(PanelHome.FactKey("strip", bar.Namespace, fact == null ? null : fact.Profile, fact == null ? null : fact.Selected));
            }
            foreach (var slot in matrices)
            {
                var fact = MatrixFacts(slot);
                facts.Add(PanelHome.FactKey("matrix", slot.ToString(System.Globalization.CultureInfo.InvariantCulture), fact == null ? null : fact.Shown));
            }
            return facts;
        }

        // --- What needs fixing ---------------------------------------------------------------------------------

        /// <summary>The issues, one row each, ruled apart: an icon on the caution's tint, what is wrong and what
        /// to do, the steps in SimHub where there are some, and the one press that helps.</summary>
        private FrameworkElement HomeAttentionCard()
        {
            // Read once, and only up to where the rows stop changing: past PressBesideFrom every press sits
            // beside its text, so a resize there leaves the page alone (PanelShell.RebuildsOnResize).
            var beside = PanelHome.PressBeside(ContentWidthUpTo(PanelHome.PressBesideFrom));
            var rows = new StackPanel { Orientation = Orientation.Vertical };
            for (var i = 0; i < issues.Count; i++)
            {
                var row = HomeIssueRow(issues[i], beside);
                if (i > 0)
                {
                    row.BorderBrush = Ui.Brush(Theme.Rule);
                    row.BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0);
                }
                rows.Children.Add(row);
            }
            return Ui.CardBox(rows, 0);
        }

        private Border HomeIssueRow(PanelIssue issue, bool beside)
        {
            // A row with steps hangs from its top, as the artboard's second row does, and so does one whose press
            // is stacked under its text, whose icon would otherwise sit beside the press. A one-line row with its
            // press beside it is centred on its title and detail.
            var hasSteps = issue.Steps.Count > 0;
            var align = hasSteps || !beside ? VerticalAlignment.Top : VerticalAlignment.Center;

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
            // "Open " and a name the driver typed, which nothing caps: the label trims, at PressMaxWidth beside
            // the text and at its column under it, rather than losing its end and its right border or taking
            // the title's room. No hover: the title, which wraps, is where the whole name is read, and a hover
            // that repeats the label says nothing (voice.md).
            press.Content = new TextBlock { Text = issue.ActionLabel, TextTrimming = TextTrimming.CharacterEllipsis };
            press.Click += (sender, args) => HomeAct(issue);

            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(well, Dock.Left);
            dock.Children.Add(well);
            if (beside)
            {
                press.VerticalAlignment = align;
                press.MaxWidth = PanelHome.PressMaxWidth;
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
                    // After the redraw, which clears the lines; said from what SimHub answered the second time.
                    CheckAgain();
                    Say(PanelHome.CheckedAgain(issue, issues, StripFacts(issue.Subject)));
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

        /// <summary>One card per kind of device, each listing every device of that kind in rig order.</summary>
        private FrameworkElement HomeRightNow(IReadOnlyList<ScreenInstance> screens, IReadOnlyList<LedBar> strips, IList<int> matrices)
        {
            var dim = PanelEmulation.Dim(Settings.LightsNightMode, Settings.LightsNightBrightness);
            var grid = Ui.CardGrid(PanelHome.CardMinWidth, PanelHome.CardGap, PanelHome.CardMax,
                HomeCard(PanelPage.Screens, HomeScreenRows(screens), PanelScreens.NoScreens),
                HomeCard(PanelPage.Leds, HomeStripRows(strips, dim), PanelLeds.NoStrips),
                HomeCard(PanelPage.Matrix, matrices.Select(m => HomeMatrixRow(m, dim)).ToList(), PanelMatrix.NoPanels));
            return PageSection(PanelHome.RightNowTitle, grid);
        }

        /// <summary>The rig's first minute, as the Screens page draws it: the add tile, as wide as a Right now
        /// card, and the sentence under it. The tile goes to Screens and opens its Add sheet there.</summary>
        private FrameworkElement HomeEmptyRig()
        {
            var tile = Ui.DashedAddCard(PanelAddScreen.SectionTitle, () =>
            {
                Go(PanelPage.Screens);
                // Go puts focus on the Screens page's first control at Loaded. The sheet opens after that, at
                // Input, so its own focus (queued at Normal as it opens) lands last and inside the sheet, and
                // the opener it remembers is a control still on screen. Input is a background priority, run
                // only when no input is pending, so a sidebar press made while Screens was building is handled
                // first: the sheet belongs to the Screens build it follows, through that build's own OnDrop,
                // which a Go or a rebuild runs, and does not open over whatever page the press went to.
                var dropped = false;
                OnDrop(() => dropped = true);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!dropped) ShowAddScreen();
                }), DispatcherPriority.Input);
            });
            var line = Ui.Prose(PanelCopy.EmptyRig, PanelHome.DetailSize);
            line.Margin = new Thickness(0, PanelHome.EmptyRigGap, 0, 0);
            return Ui.VStack(0, Ui.CardGrid(PanelHome.CardMinWidth, PanelHome.CardGap, PanelHome.CardMax, tile), line);
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

        /// <summary>A screen on the Home card, and what its row needs to repaint its line on the tick.</summary>
        private sealed class HomeScreen
        {
            public string Namespace;
            public bool? Installed;
            public bool Restart;
            public TextBlock Line;
            public Ellipse Dot;
            public string Painted;
        }

        /// <summary>The screens, each with a line that follows its live zone pages once a second: a wheel's
        /// zone press writes them without a word to the panel.</summary>
        private IList<Button> HomeScreenRows(IReadOnlyList<ScreenInstance> screens)
        {
            var rows = new List<Button>();
            var shown = new List<HomeScreen>();
            foreach (var screen in screens)
            {
                if (screen == null) continue;
                var row = HomeScreenRow(screen);
                shown.Add(row.Key);
                rows.Add(row.Value);
            }
            if (shown.Count > 0) OnTick(() => { foreach (var screen in shown) HomePaintScreen(screen); });
            return rows;
        }

        /// <summary>A screen: its name and size, what it shows now (or what SimHub is missing), and its state.</summary>
        private KeyValuePair<HomeScreen, Button> HomeScreenRow(ScreenInstance screen)
        {
            var facts = ScreenFacts(screen.Namespace);
            var restart = PanelAttention.Has(issues, PanelAttention.ScreenRestart, screen.Namespace);
            var row = new HomeScreen
            {
                Namespace = screen.Namespace,
                Installed = facts == null ? null : facts.Installed,
                Restart = restart,
                Line = HomeLineText(false),
                Dot = HomeDot(null),
            };
            HomePaintScreen(row);

            var text = Ui.VStack(0, HomeNameLine(screen.Name, PanelHome.ScreenSize(screen)), row.Line);
            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(row.Dot, Dock.Right);
            dock.Children.Add(row.Dot);
            dock.Children.Add(text);
            var ns = screen.Namespace;
            return new KeyValuePair<HomeScreen, Button>(row, HomeRow(dock, () => Open(PanelPage.Screens, ns)));
        }

        /// <summary>Draws a screen's line from its settings as they are now, with the facts the last Go read,
        /// and only when the text or its ink moved.</summary>
        private void HomePaintScreen(HomeScreen row)
        {
            var screen = Settings.ScreenByNamespace(row.Namespace);
            if (screen == null) return;
            var line = PanelHome.ScreenLine(Settings, screen, row.Installed, row.Restart);
            var painted = line.Text + "|" + line.TextHex + "|" + line.DotHex;
            if (painted == row.Painted) return;
            row.Painted = painted;
            HomeSetLine(row.Line, line);
            HomeSetDot(row.Dot, line.DotHex);
        }

        /// <summary>A strip on the Home card, and what its row needs to repaint itself on the tick.</summary>
        private sealed class HomeStrip
        {
            public LedBar Bar;
            public int Ends;
            public int Centre;
            public double Dim;
            public FlagBoxInstallState? Profile;
            public bool? Selected;
            public bool KeepsRoom;
            public Viewbox Host;
            public Border Picture;
            public TextBlock Line;
            public Ellipse Dot;
            public string PaintedRun;
            public string PaintedLine;
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
                // The facts the headline and the attention card were drawn from, copied once: the tick reads
                // only the car's run and the strip's own settings.
                var facts = StripFacts(bar.Namespace);
                var strip = new HomeStrip
                {
                    Bar = bar,
                    Ends = span[0],
                    Centre = span[1],
                    Dim = dim,
                    Profile = facts == null ? null : facts.Profile,
                    Selected = facts == null ? null : facts.Selected,
                    // One line, trimmed with its whole text on hover: the live line carries the car's name,
                    // which nothing caps, and the one line KeepsRoom holds must be all it ever takes.
                    Line = HomeLineText(false),
                    Dot = HomeDot(null),
                };
                strip.KeepsRoom = PanelHome.StripLineKeepsRoom(Settings.BarRpmStyle(bar.Namespace), Settings.BarCentre(bar.Namespace), strip.Profile, strip.Selected);
                // The picture is built once, dark, and the tick sets its LEDs in place.
                strip.Picture = Ui.Strip(PanelEmulation.LiveFrame(null, strip.Ends, strip.Centre), StripStyle.Home, dim);
                strip.Line.Margin = new Thickness(0, PanelHome.StripRowGap, 0, 0);

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
                name.TextWrapping = TextWrapping.Wrap;
                name.VerticalAlignment = VerticalAlignment.Center;
                top.Children.Add(name);

                // Wider strips than the card shrink to it rather than spilling out of it, and none grows past
                // its own size in a wide card: the kit's fixed picture, which never grows (Ui.FitWidth).
                var host = (Viewbox)Ui.FitWidth(strip.Picture);
                host.Margin = new Thickness(0, PanelHome.StripRowGap, 0, 0);
                strip.Host = host;
                HomePaintStrip(strip);
                live.Add(strip);
                // The line carries its own gap, so a line with nothing to say takes its gap with it, unless the
                // strip can go live, whose card keeps one height as a session starts and ends.
                var ns = bar.Namespace;
                rows.Add(HomeRow(Ui.VStack(0, top, host, strip.Line), () => Open(PanelPage.Leds, ns)));
            }
            if (live.Count > 0) OnTick(() => { foreach (var strip in live) HomePaintStrip(strip); });
            return rows;
        }

        /// <summary>Draws a strip's picture and line from the car's own run as it is now, and only when either
        /// moved: the run the plugin already holds, and the facts copied when the page was built.</summary>
        private void HomePaintStrip(HomeStrip strip)
        {
            var profile = strip.Profile;
            var selected = strip.Selected;
            var cars = plugin.CarLights;
            var ns = strip.Bar.Namespace;
            var live = cars != null && PanelHome.StripLive(cars.Ready, Settings.BarRpmStyle(ns), Settings.BarCentre(ns), profile, selected);
            var run = live ? cars.Run(strip.Centre) : null;
            var line = PanelHome.StripLine(live, live ? cars.CarName : null, profile, selected);
            var runKey = run ?? string.Empty;
            if (runKey != strip.PaintedRun)
            {
                strip.PaintedRun = runKey;
                var frame = PanelEmulation.LiveFrame(run, strip.Ends, strip.Centre);
                if (!HomeRelight(strip.Picture, frame, StripStyle.Home.UnlitHex))
                {
                    // Ui.Strip no longer draws the tree HomeRelight walks: draw the frame whole rather than
                    // leave the picture dark, and say so once, since the in-place repaint wants fixing.
                    HomeRelightMissed();
                    strip.Picture = Ui.Strip(frame, StripStyle.Home, strip.Dim);
                    strip.Host.Child = strip.Picture;
                }
            }
            var lineKey = line.Text + "|" + line.TextHex + "|" + line.DotHex;
            if (lineKey == strip.PaintedLine) return;
            strip.PaintedLine = lineKey;
            HomeSetLine(strip.Line, line, strip.KeepsRoom);
            HomeSetDot(strip.Dot, line.DotHex);
        }

        /// <summary>
        /// Sets each LED of a picture Ui.Strip drew to a frame of the same shape, in place: the row, its
        /// groups, and a Border per LED. False, touching nothing, when the picture is not that tree or not
        /// that shape, so the caller can draw the frame whole instead.
        /// </summary>
        /// <remarks>
        /// The walk mirrors Ui.Strip's private tree (Widgets.Lights.cs), which the tests cannot build. Asked
        /// of the kit as Ui.Relight beside Ui.Redim, so the walk lives beside the tree it walks.
        /// </remarks>
        private static bool HomeRelight(Border picture, string[][] frame, string unlitHex)
        {
            var row = picture == null ? null : picture.Child as Panel;
            if (row == null || frame == null || row.Children.Count != frame.Length) return false;
            var leds = new List<Border>();
            for (var g = 0; g < frame.Length; g++)
            {
                var group = row.Children[g] as Panel;
                var lit = frame[g] ?? new string[0];
                if (group == null || group.Children.Count != lit.Length) return false;
                foreach (var child in group.Children)
                {
                    var led = child as Border;
                    if (led == null) return false;
                    leds.Add(led);
                }
            }
            var at = 0;
            foreach (var lit in frame)
            {
                foreach (var hex in lit ?? new string[0]) leds[at++].Background = Ui.Brush(hex ?? unlitHex);
            }
            return true;
        }

        private static bool homeRelightWarned;

        private static void HomeRelightMissed()
        {
            if (homeRelightWarned) return;
            homeRelightWarned = true;
            Log.Warn("Home could not repaint a strip in place, because Ui.Strip draws a different tree; it draws each frame whole instead.");
        }

        /// <summary>A matrix: its idle glyph (dark when no device shows it), its name and its content number.</summary>
        private Button HomeMatrixRow(int slot, double dim)
        {
            var facts = MatrixFacts(slot);
            var shown = facts == null ? null : facts.Shown;
            var title = Settings.MatrixName(slot) ?? PanelHome.MatrixName(slot);
            var line = PanelHome.MatrixLine(slot, title, Settings.MatrixRest(slot), shown);
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

            var name = Ui.Text(title, PanelHome.NameSize, FontWeights.SemiBold, Theme.TextPrimary);
            name.TextWrapping = TextWrapping.Wrap;
            var lineText = HomeLineText(true);
            HomeSetLine(lineText, line);
            var text = Ui.VStack(0, name, lineText);
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

        /// <summary>A name with its figure after it, as the Screens rows draw "Main dash 1280 × 480". The figure
        /// is docked first, so it is never cut, and a long name wraps beside it as Main.dc.html's does, as a
        /// strip's and a matrix's name do.</summary>
        private static FrameworkElement HomeNameLine(string name, string meta)
        {
            var dock = new DockPanel { LastChildFill = true, HorizontalAlignment = HorizontalAlignment.Left };
            if (!string.IsNullOrEmpty(meta))
            {
                var figure = Ui.Text(meta, PanelHome.MetaSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
                figure.VerticalAlignment = VerticalAlignment.Center;
                figure.Margin = new Thickness(PanelHome.MetaGap, 0, 0, 0);
                DockPanel.SetDock(figure, Dock.Right);
                dock.Children.Add(figure);
            }
            var title = Ui.Text(name ?? string.Empty, PanelHome.NameSize, FontWeights.SemiBold, Theme.TextPrimary);
            title.TextWrapping = TextWrapping.Wrap;
            title.VerticalAlignment = VerticalAlignment.Center;
            dock.Children.Add(title);
            return dock;
        }

        /// <summary>A device's line under its name, 3 under it: a screen's and a strip's on one line, trimmed with
        /// its whole text on hover as the artboard's now line is, and a matrix's, which no session changes,
        /// wrapped. HomeSetLine fills it.</summary>
        private static TextBlock HomeLineText(bool wrap)
        {
            var text = Ui.Text(string.Empty, PanelHome.LineSize, FontWeights.Normal, Theme.TextSecondary);
            if (wrap) text.TextWrapping = TextWrapping.Wrap;
            else text.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Margin = new Thickness(0, PanelHome.LineGap, 0, 0);
            text.Visibility = Visibility.Collapsed;
            return text;
        }

        /// <summary>Says a line: its text and ink, its whole text on hover where it is trimmed, and, when it has
        /// nothing to say, no room at all, gap included, or its room kept blank where the line will speak on a
        /// later tick.</summary>
        private static void HomeSetLine(TextBlock text, HomeLine line, bool keepsRoom = false)
        {
            text.Text = line.Text;
            text.Foreground = Ui.Brush(line.TextHex);
            text.ToolTip = text.TextTrimming == TextTrimming.None || line.Text.Length == 0 ? null : line.Text;
            text.Visibility = line.Text.Length > 0 ? Visibility.Visible : keepsRoom ? Visibility.Hidden : Visibility.Collapsed;
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
            // The value is saved once, when the hand lets go or a key moves it; the figure follows every step: a
            // drag's through preview, and a key's here, since the kit calls only changed for a key and the
            // rebuild that would redraw the figure waits for the keys to settle.
            // Writes the brightness the slider was built for, which its label names: a wheel's night-mode press
            // mid-drag rebuilds the page, and the drag's last value must not land on the other brightness.
            var slider = Ui.Slider(value, v =>
            {
                numeral.Text = PanelHome.Percent(v);
                if (nightOn)
                {
                    if (v == Settings.LightsNightBrightness) return;
                    Settings.LightsNightBrightness = v;
                    Save();
                    ShowLightingChange();
                }
                else
                {
                    if (v == Settings.LightsBrightness) return;
                    Settings.LightsBrightness = v;
                    Save();
                    ShowLightingChange();
                }
            }, v => numeral.Text = PanelHome.Percent(v), false);
            // The eyebrows over the controls are text beside them, which names nothing to UI Automation: each
            // control carries its own name, the slider the brightness in force, as its eyebrow does.
            System.Windows.Automation.AutomationProperties.SetName(slider, PanelHome.BrightnessLabel(nightOn));
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
            System.Windows.Automation.AutomationProperties.SetName(night, PanelSettings.NightModeTitle);

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
