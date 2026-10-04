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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>The car tables' button and the line under it. Dropped with the page, so a download that
        /// finishes after the page has gone writes nowhere.</summary>
        private Button carTablesButton;

        private TextBlock carTablesLine;

        /// <summary>The last failure to read the tables written to SimHub's log, so a page drawn again does not
        /// write it again.</summary>
        private static string carTablesLogged;

        /// <summary>Whether the car tables are downloading, held outside the build so a rebuild meanwhile draws
        /// the button disabled and the line saying so rather than a press that looks ready.</summary>
        private bool carTablesDownloading;

        /// <summary>What the car tables' row was last drawn from, the service's status and its count of cars, so
        /// a tick redraws the row only when the start's read or a download moved them.</summary>
        private string carTablesDrawn;

        /// <summary>The preview chip pressed, and the strip it was pressed for: held outside the build so a
        /// rebuild keeps it, and back to Live when another strip is selected.</summary>
        private string ledsScenario = PanelLeds.LiveScenario;

        private string ledsScenarioFor;

        /// <summary>Each card's repaint, by strip, so a switch, a Centre display pick or the strip's own
        /// Brightness below paints its card as well as the preview. Filled by the build and dropped with it.</summary>
        private readonly Dictionary<string, Action> ledsCardRepaints = new Dictionary<string, Action>(StringComparer.Ordinal);

        /// <summary>Set by a pick in the SimHub device picker, which redraws the whole page from inside the
        /// picker's list: the next build hands keyboard focus to the picker drawn in its place, since focus sat
        /// in the list's popup and nothing else would bring it back into the panel.</summary>
        private bool ledsFocusDevice;

        /// <summary>The label of the search hit that last went to this page, which tells LedsFollow which of the
        /// effects block's switches it was for; let go of on the way in.</summary>
        private string ledsSearchedLabel;

        /// <summary>Whether the page has been built since the last Go, as screensBuiltSinceGo is for Screens: a
        /// route's anchor selects a strip on the way in, never on a rebuild in place.</summary>
        private bool ledsBuiltSinceGo;

        private FrameworkElement BuildLedsPage(PanelRoute to)
        {
            if (!ledsBuiltSinceGo)
            {
                ledsBuiltSinceGo = true;
                OnLeave("Leds.follow", () => ledsBuiltSinceGo = false);
                var searched = ledsSearchedLabel;
                ledsSearchedLabel = null;
                if (to != null && to.Anchor != null) LedsFollow(to.Anchor, searched);
            }
            ledsCardRepaints.Clear();
            OnDrop(() =>
            {
                carTablesButton = null;
                carTablesLine = null;
                ledsCardRepaints.Clear();
            });
            var bars = Settings.LedBarList().Where(bar => bar != null).ToList();
            var current = LedsSelectedBar(bars);
            var sections = new List<UIElement> { Ui.Anchor(LedsCards(bars, current), PanelLeds.AnchorStrips) };
            if (current != null) sections.Add(LedsStripSection(current));
            sections.Add(LedsEveryStripSection());
            return PageLayout(PanelLeds.Title, null, sections.ToArray());
        }

        /// <summary>Selects the strip a route to <paramref name="anchor"/> needs (PanelLeds.StripFor): a search
        /// for Reverse direction, Full-strip spotter, Width or an effect lands on a strip that draws it, as Screens'
        /// ScreensFollow opens a screen that draws the row.</summary>
        private void LedsFollow(string anchor, string label)
        {
            var bars = Settings.LedBarList().Where(bar => bar != null).ToList();
            var current = LedsSelectedBar(bars);
            var strip = PanelLeds.StripFor(anchor, label, bars, current);
            if (strip != null && !ReferenceEquals(strip, current)) Select(PanelPage.Leds, strip.Namespace);
        }

        /// <summary>The strip whose settings are showing: the one selected, else the first; null with none.</summary>
        private LedBar LedsSelectedBar(IList<LedBar> bars)
        {
            var shown = PanelLeds.ShownStrip(bars.Select(bar => bar.Namespace).ToList(), Selected(PanelPage.Leds));
            return shown < 0 ? null : bars[shown];
        }

        /// <summary>How bright a strip's pictures are drawn now: full by day, the brightness in force at night.</summary>
        private double LedsDim(string ns)
        {
            return PanelLeds.PreviewDim(Settings.LightsNightMode, Settings.LightsNightBrightness, Settings.BarBrightness(ns));
        }

        /// <summary>What a strip is set to that changes its picture.</summary>
        private StripOptions LedsOptions(LedBar bar)
        {
            return PanelLeds.OptionsFor(Settings.BarSpotterWhole(bar.Namespace), bar.EffectsOff, Settings.BarCentre(bar.Namespace));
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
                // Lit only while SimHub shows the profile, so the picture never contradicts the state under it.
                var lit = PanelLeds.CardLit(profile, selected);
                // A longer strip than the card holds at 9 px shrinks to it rather than being cut at its edge.
                var fitted = new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    StretchDirection = StretchDirection.DownOnly,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                Border picture = null;
                Action paint = () =>
                {
                    var live = Settings.LedBarByNamespace(ns) ?? bar;
                    picture = Ui.Strip(PanelLeds.CardFrame(live.Shape, LedsOptions(live), lit), StripStyle.Card, LedsDim(ns));
                    fitted.Child = picture;
                };
                paint();
                ledsCardRepaints[ns] = paint;
                OnLighting(() => Ui.Redim(picture, LedsDim(ns)));
                var card = Ui.StripCard(
                    bar.Name,
                    PanelLeds.ShapeDots(bar.Shape),
                    fitted,
                    PanelLeds.StateText(profile, selected),
                    PanelLeds.StateHex(profile, selected),
                    ReferenceEquals(bar, current),
                    () =>
                    {
                        Select(PanelPage.Leds, ns);
                        Redraw();
                    });
                // The card trims a long name, so its hover has the whole of it.
                card.ToolTip = bar.Name;
                cards.Add(card);
            }
            cards.Add(Ui.InlineAddCard(PanelLights.AddBar, PanelKit.StripAddIcon, ShowAddLedBar));
            // The card grid is a panel, which a screen reader never sees: its name is set on a group around it, as
            // Matrix names its cards.
            var grid = new GroupBorder { Child = Ui.CardGrid(PanelLeds.CardMinWidth, PanelLeds.CardGap, PanelLeds.CardColumns, cards.ToArray()) };
            AutomationProperties.SetName(grid, PanelLights.BarsTitle);
            if (bars.Count > 0) return grid;
            // The empty state names the emptiness beside the tile that ends it; Home reads the same words.
            return Ui.VStack(12, Ui.Prose(PanelLeds.NoStrips, Theme.SizeBody), grid);
        }

        // --- The selected strip -------------------------------------------------------------------------------

        private FrameworkElement LedsStripSection(LedBar bar)
        {
            var ns = bar.Namespace;
            ledsScenario = PanelLeds.ScenarioFor(ledsScenario, ledsScenarioFor, ns);
            ledsScenarioFor = ns;
            // The page's one read of SimHub's devices, which the census before the build made (ledDevices).
            var targets = ledDevices.Targets;
            var declined = ledDevices.Declined;

            Action redrawOnlyPreview;
            var preview = LedsPreview(bar, out redrawOnlyPreview);
            // What changes the preview changes the strip's card above it too.
            Action redrawPreview = () =>
            {
                redrawOnlyPreview();
                Action repaintCard;
                if (ledsCardRepaints.TryGetValue(ns, out repaintCard)) repaintCard();
            };
            var parts = new List<UIElement> { LedsHeader(bar, targets, declined) };
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

        /// <summary>A greyed row spaced as LedsRow spaces a live one, its control 20 from the title: the kit's
        /// SoonRow would give it 24, and its title would wrap sooner than the rows around it.</summary>
        private static Border LedsSoonRow(SoonItem item, FrameworkElement control)
        {
            control.Margin = new Thickness(PanelKit.RowGapLeds - PanelShell.RowGap, 0, 0, 0);
            return Ui.SoonRow(item, control);
        }

        /// <summary>Two blocks side by side, 40 apart, where two columns fit, and one under the other where not.</summary>
        private FrameworkElement LedsColumns(FrameworkElement left, FrameworkElement right)
        {
            if (!TwoColumns) return Ui.VStack(24, left, right);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            left.Margin = new Thickness(0, 0, PanelLeds.ColumnGap / 2, 0);
            right.Margin = new Thickness(PanelLeds.ColumnGap / 2, 0, 0, 0);
            left.VerticalAlignment = VerticalAlignment.Top;
            right.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(left, 0);
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);
            return grid;
        }

        /// <summary>
        /// The strip's name, its hardware and shape in a chip beside it, what SimHub shows of its profile, and the
        /// presses on the strip. Where nothing here can install the profile, a line under it says why in place of
        /// Install or Update.
        /// </summary>
        private FrameworkElement LedsHeader(LedBar bar, IList<LedTarget> targets, IList<string> declined)
        {
            var title = Ui.SubHeading(bar.Name);
            // Wrapped rather than cut: a name has no length limit, and the header is the one place it is meant to be
            // read whole. The WrapPanel gives it the whole of the name's column.
            title.TextTrimming = TextTrimming.None;
            title.TextWrapping = TextWrapping.Wrap;
            title.VerticalAlignment = VerticalAlignment.Center;
            // The gap is the name's, so a chip the WrapPanel drops to a second line starts under the name.
            title.Margin = new Thickness(0, 0, 12, 0);
            // The hardware trims before the shape's numerals are cut, where a very narrow column leaves the chip less
            // than it needs.
            var lead = Ui.Text(PanelLeds.HardwareLead(bar.Shape), 13, FontWeights.Medium, Theme.TextPrimary);
            lead.VerticalAlignment = VerticalAlignment.Center;
            lead.TextTrimming = TextTrimming.CharacterEllipsis;
            var numerals = Ui.Text(PanelLeds.ShapeDots(bar.Shape), 14, FontWeights.SemiBold, Theme.TextPrimary, PanelFonts.Data);
            numerals.VerticalAlignment = VerticalAlignment.Center;
            var inChip = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(numerals, Dock.Right);
            inChip.Children.Add(numerals);
            inChip.Children.Add(lead);
            var chip = new Border
            {
                Height = 26,
                Padding = new Thickness(9, 0, 9, 0),
                Background = Ui.Brush(Theme.SurfaceRaised),
                CornerRadius = new CornerRadius(Theme.Radius),
                VerticalAlignment = VerticalAlignment.Center,
                Child = inChip,
            };
            chip.ToolTip = PanelLeds.HardwareLead(bar.Shape) + PanelLeds.ShapeDots(bar.Shape);
            var name = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(title);
            name.Children.Add(chip);
            var blocked = LedsProfileBlocked(bar, targets, declined, true);
            // Beside the name where two columns fit, and under it where they do not: the state and three or four
            // presses take up to 388 px, which beside the name and its chip ran past a single column.
            var stacked = !TwoColumns;
            var actions = BuildLedBarActions(bar.Namespace, blocked, stacked);
            var row = stacked ? (FrameworkElement)Ui.VStack(8, name, actions) : Ui.Row(name, actions);
            if (blocked == null) return row;
            return Ui.VStack(6, row, LedsCaptionLine(blocked));
        }

        /// <summary>Why nothing on this page can install the strip's profile, or null: PanelLeds.ProfileBlocked
        /// over whether the build carries it, whether SimHub lists the strip's device, and what SimHub offers, or
        /// PanelLeds.HeaderBlocked for the line under the header. From SimHub's devices as the caller walked them,
        /// once: each walk is a pass over every device on SimHub's interface thread.</summary>
        private static string LedsProfileBlocked(LedBar bar, IList<LedTarget> targets, IList<string> declined, bool header = false)
        {
            var offered = targets.Count;
            var embedded = EmbeddedProfileOf(bar) != null;
            var listed = LedsTargetOf(targets, bar.Device) != null;
            return header
                ? PanelLeds.HeaderBlocked(embedded, listed, offered, declined)
                : PanelLeds.ProfileBlocked(embedded, listed, offered, declined);
        }

        /// <summary>The device among <paramref name="targets"/> a strip names, or null: LedTargets.Find over a list
        /// already walked.</summary>
        private static LedTarget LedsTargetOf(IList<LedTarget> targets, string device)
        {
            return LedTargets.Find(device, targets);
        }

        /// <summary>
        /// Installs the strip's profile again from what this build embeds, where the page may: null, with the
        /// reason in SimHub's log, where the build carries no profile for it or SimHub does not list its device
        /// among <paramref name="targets"/>, the devices the press walked.
        /// </summary>
        /// <remarks>
        /// The Updates page's guard, for the same reason: InstallBar takes the strip's copy out of every device first,
        /// so running it for a device SimHub does not list would remove a profile that still lights and install
        /// nothing. A result that is not an install is logged too, so every "See SimHub's log" has a line behind it.
        /// </remarks>
        private static FlagBoxPlan LedsReinstall(LedBar bar, IList<LedTarget> targets)
        {
            var found = EmbeddedProfileOf(bar);
            if (found == null)
            {
                Log.Warn("The profile for " + bar.Name + " was not installed: this build carries no profile of its shape, " + bar.ProfileShapeId + ".");
                return null;
            }
            if (LedsTargetOf(targets, bar.Device) == null)
            {
                Log.Warn("The profile for " + bar.Name + " was not installed: the LED device it names is not in SimHub.");
                return null;
            }
            return LedsLogged(bar, InstallBar(bar, found.Json, targets));
        }

        /// <summary>An install's result, logged where it is not one, since StripInstaller returns Unavailable
        /// without a word.</summary>
        private static FlagBoxPlan LedsLogged(LedBar bar, FlagBoxPlan plan)
        {
            if (plan.State != FlagBoxInstallState.UpToDate)
            {
                Log.Warn("The profile for " + bar.Name + " was not installed on " + bar.Device + ": the install ended " + plan.State + ".");
            }
            return plan;
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
            // Home's press, outcome and all: where the strip is still unselected nothing here moves, so the line says so.
            again.Click += (sender, args) => CheckAgainAndSay(issue);
            var box = Ui.FixBox(PanelLeds.NotSelectedTitle, null, issue.Steps, again);
            box.Padding = new Thickness(PanelKit.FixPaddingX, PanelKit.FixPaddingYLights, PanelKit.FixPaddingX, PanelKit.FixPaddingYLights);
            return box;
        }

        /// <summary>
        /// The preview: the chips that pick a moment, a link to every device at once, and the strip drawn large
        /// with each group named under it.
        /// </summary>
        /// <remarks>
        /// Live is the car's own rev lights as the plugin has them, looked at every second while Live is
        /// pressed and repainted only when the frame changed, so the strip at rest comes back when the car's
        /// lights stop; the other chips are PanelEmulation's rules and never reach the hardware (#506). The LEDs
        /// sit in a Viewbox that only shrinks, so a 25-LED run fits a narrow window rather than running off it,
        /// and the group labels sit under it at their own size, in columns weighted as the groups are.
        /// </remarks>
        private FrameworkElement LedsPreview(LedBar bar, out Action redraw)
        {
            var ns = bar.Namespace;
            var ends = PanelLeds.Ends(bar.Shape);
            var centre = PanelLeds.Centre(bar.Shape);

            // The groups are drawn once and repainted in place.
            var groups = new List<Border>();
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var sizes = ends > 0 ? new[] { ends, centre, ends } : new[] { centre };
            for (var i = 0; i < sizes.Length; i++)
            {
                var strip = Ui.Strip(new[] { new string[sizes[i]] }, StripStyle.Preview);
                strip.Margin = new Thickness(i == 0 ? 0 : PanelLeds.PreviewGroupGap, 0, 0, 0);
                groups.Add(strip);
                row.Children.Add(strip);
            }
            var preview = new Border { Child = row, Padding = new Thickness(0, 12, 0, 0) };
            var fit = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = preview, HorizontalAlignment = HorizontalAlignment.Center };

            // Each label has its whole width in one cell as wide as the preview, placed under its group by
            // PanelLeds.PreviewLabelLefts whenever the room changes: a cell as wide as a 1-LED end would cut it.
            var columns = PanelLeds.PreviewColumns(ends, centre);
            var names = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 10, 0, 2) };
            var labels = new List<FrameworkElement>();
            foreach (var text in PanelLeds.PreviewLabels(ends, centre))
            {
                var label = Ui.Eyebrow(text);
                label.HorizontalAlignment = HorizontalAlignment.Left;
                labels.Add(label);
                names.Children.Add(label);
            }
            Action place = () =>
            {
                var room = names.ActualWidth;
                if (room <= 0) return;
                var widths = labels.Select(label =>
                {
                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    return label.DesiredSize.Width - label.Margin.Left - label.Margin.Right;
                }).ToArray();
                var lefts = PanelLeds.PreviewLabelLefts(columns, widths, room);
                for (var i = 0; i < labels.Count; i++) labels[i].Margin = new Thickness(lefts[i], 0, 0, 0);
            };
            names.SizeChanged += (sender, args) => place();

            string drawn = null;
            Action draw = () =>
            {
                var live = Settings.LedBarByNamespace(ns) ?? bar;
                var lights = plugin.CarLights;
                var running = PanelLeds.LiveRuns(lights.Ready, Settings.BarRpmStyle(ns), Settings.BarCentre(ns));
                var frame = PanelLeds.PreviewFrame(ledsScenario, ends, centre, LedsOptions(live), running, running ? lights.Run(centre) : null);
                var key = PanelLeds.FrameKey(frame);
                if (key == drawn) return;
                drawn = key;
                for (var i = 0; i < groups.Count && i < frame.Length; i++) LedsRepaint(groups[i], frame[i], StripStyle.Preview);
            };
            row.Opacity = LedsDim(ns);
            OnLighting(() => Ui.Redim(preview, LedsDim(ns)));

            var chips = new WrapPanel { Orientation = Orientation.Horizontal };
            Action drawChips = null;
            drawChips = () =>
            {
                chips.Children.Clear();
                var index = 0;
                foreach (var scenario in PanelLeds.Scenarios)
                {
                    var id = scenario.Key;
                    var at = index++;
                    var chip = Ui.Chip(scenario.Value, id == ledsScenario, () =>
                    {
                        // The pressed chip is drawn again, so keyboard focus is handed to its successor rather
                        // than falling out of the panel into SimHub's window.
                        var focused = at < chips.Children.Count && chips.Children[at].IsKeyboardFocusWithin;
                        ledsScenario = id;
                        drawChips();
                        draw();
                        if (focused) LedsFocusLater(() => at < chips.Children.Count ? chips.Children[at] : null);
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
                if (PanelLeds.TickRedraws(ledsScenario)) draw();
            });

            var link = Ui.LinkButton(PanelLeds.AllDevicesAtOnce);
            link.FontSize = Theme.SizeSmall;
            link.Height = PanelKit.ChipHeight;
            link.VerticalAlignment = VerticalAlignment.Top;
            link.Margin = new Thickness(12, 0, 0, 0);
            link.Click += (sender, args) =>
            {
                var rig = PanelLeds.RigScenario(ledsScenario);
                if (rig != null) Open(PanelPage.Rig, rig);
                else Go(PanelPage.Rig);
            };
            var top = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(link, Dock.Right);
            top.Children.Add(link);
            // A WrapPanel has no automation peer, so the chips are named on a group around them, as the Rig and
            // Matrix pages name theirs (PanelLeds.PreviewChipsName).
            var chipGroup = new GroupBorder { Child = chips };
            AutomationProperties.SetName(chipGroup, PanelLeds.PreviewChipsName);
            top.Children.Add(chipGroup);

            redraw = () =>
            {
                draw();
                Ui.Redim(preview, LedsDim(ns));
            };
            var body = Ui.VStack(8, top, Ui.VStack(0, fit, names));
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

        /// <summary>The control a kit row carries, or null where it carries none.</summary>
        private static UIElement LedsRowControl(Border row)
        {
            var parts = row == null ? null : row.Tag as RowParts;
            return parts == null ? null : parts.Control;
        }

        /// <summary>The first focusable control among a host's own children: the button of a Ui.ChoiceButton.</summary>
        private static UIElement LedsFirstControl(UIElement host)
        {
            var panel = host as Panel;
            if (panel == null) return host;
            return panel.Children.OfType<Control>().FirstOrDefault(control => control.Focusable);
        }

        /// <summary>The control, where it can take keyboard focus: null where it is gone, disabled or not a stop.</summary>
        private static UIElement LedsFocusable(UIElement control)
        {
            return control != null && control.IsEnabled && control.Focusable ? control : null;
        }

        /// <summary>Focuses a control once the layout that drew it has run: a control added a moment ago is
        /// not visible yet, and focus handed to it then goes nowhere.</summary>
        /// <remarks>
        /// Without scrolling to it. Focus hands the control back to the keyboard after a redraw under it, and a
        /// focused control asks its ScrollViewer to bring it into view: after a device pick, the line Say had just
        /// scrolled to at the top of the page was scrolled away again whenever the picker sat below the fold,
        /// which in one column is always. The request is raised inside Focus, so it is handled there, once.
        /// </remarks>
        private void LedsFocusLater(Func<UIElement> find)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var element = find();
                if (element == null) return;
                RequestBringIntoViewEventHandler stay = (sender, args) => args.Handled = true;
                element.AddHandler(FrameworkElement.RequestBringIntoViewEvent, stay);
                try
                {
                    element.Focus();
                }
                finally
                {
                    element.RemoveHandler(FrameworkElement.RequestBringIntoViewEvent, stay);
                }
            }), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Paints the LEDs of one group drawn by Ui.Strip in place: the preview repaints rather than draws its
        /// groups again, which on a tick would be every LED and label rebuilt each second.
        /// </summary>
        private static void LedsRepaint(Border strip, string[] colours, StripStyle style)
        {
            var row = strip == null ? null : strip.Child as StackPanel;
            var group = row != null && row.Children.Count > 0 ? row.Children[0] as StackPanel : null;
            if (group == null || colours == null) return;
            for (var i = 0; i < group.Children.Count && i < colours.Length; i++)
            {
                var led = group.Children[i] as Border;
                if (led != null) led.Background = Ui.Brush(colours[i] ?? style.UnlitHex);
            }
        }

        /// <summary>
        /// Rev lights: the #369 switch, the line saying whether the car in the sim is one Car Data has
        /// measured, the rig's width for the car's lights while the switch is on, and what the centre shows.
        /// </summary>
        private FrameworkElement LedsRevLights(LedBar bar, Action redrawPreview)
        {
            var ns = bar.Namespace;
            // A Border rather than a ContentControl, which WPF makes a tab stop: the icon is a picture.
            var carIcon = new Border { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
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
                width.Visibility = PanelLeds.ShowsMirrorFit(Settings.BarRpmStyle(ns)) ? Visibility.Visible : Visibility.Collapsed;
                var live = plugin.Live ?? LiveStatus.None;
                var loaded = PanelLeds.TablesLoaded(plugin.CarLights.CarCount);
                var missing = PanelLeds.TablesMissing(plugin.CarLights.CarCount, plugin.CarLights.Status);
                var known = PanelLeds.CarLineGood(plugin.LiveCarHasTable, loaded);
                var line = PanelLeds.CarLine(on, live.CarModel, plugin.LiveCarHasTable, loaded, missing, PanelLeds.TablesCoverGame(live.GameName));
                carLine.Visibility = line == null ? Visibility.Collapsed : Visibility.Visible;
                var key = line + "|" + known;
                if (line == null || key == shown) return;
                shown = key;
                var hex = PanelLeds.CarLineHex(known);
                carText.Text = line;
                carText.Foreground = Ui.Brush(hex);
                carIcon.Child = known
                    ? Ui.Icon(PanelIcons.RingCheck, hex, 14, PanelIcons.RingBox)
                    : Ui.Icon(PanelIcons.Warning, hex, 14, PanelIcons.NavBox);
            };

            // #369: one switch, the car's own rev lights or not. On writes the car's own style and off the plain
            // left-to-right ladder, through the contract's set so it writes against the list in view (the set
            // still holds meetInMiddle and f1 so an old file reads, and a strip carrying one reads as off).
            // Written here rather than through PanelLeds because contract.test.ts reads the panel's source for
            // these Contract names; PanelLedsTests holds that each reads back as the switch it was written by.
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

            // Drawn again after a pick, so the list marks the entry now chosen rather than the one it opened on,
            // and keyboard focus goes back to the button drawn in its place.
            var centre = new Border { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Action drawCentre = null;
            drawCentre = () =>
            {
                centre.Child = Ui.ChoiceButton(PanelLights.CentreLabels, PanelLeds.CentreIndex(Settings.BarCentre(ns)), i =>
                {
                    var live = Settings.LedBarByNamespace(ns);
                    if (live != null) live.Centre = Contract.LedCentres[i];
                    Save();
                    // Live draws the car's run only while the centre shows the revs, and the card and the preview
                    // draw the revs in the centre only while it does.
                    redrawPreview();
                    drawCentre();
                    LedsFocusLater(() => LedsFirstControl(centre.Child));
                }, 160);
            };
            drawCentre();

            return Ui.VStack(0,
                Ui.Anchor(LedsHeading(PanelLeds.RevLightsTitle), PanelLeds.AnchorRevLights),
                Ui.Rows(
                    Ui.Anchor(LedsRow(PanelLeds.CarRevLightsTitle, style), PanelLeds.AnchorRevStyle),
                    carLine,
                    width,
                    Ui.Anchor(LedsRow(PanelLeds.CentreDisplayTitle, centre), PanelLeds.AnchorCentre)));
        }

        /// <summary>This strip: the SimHub device its profile is in, a brightness of its own, its direction, and
        /// the greyed test of each LED.</summary>
        private FrameworkElement LedsThisStrip(LedBar bar, IList<LedTarget> targets, IList<string> declined, Action redrawPreview)
        {
            var ns = bar.Namespace;
            var movable = PanelLeds.DeviceMovable(EmbeddedProfileOf(bar) != null);
            var deviceRow = BuildLedDeviceRow(targets, declined, Settings.BarDevice(ns), movable, value => MoveLedBar(ns, value));
            var rows = new List<UIElement> { Ui.Anchor(deviceRow, PanelLeds.AnchorDevice) };

            // The chooser names the rig's brightness in force in its first entry, so it is drawn again when a
            // wheel's press or night mode moves that; the page itself is not rebuilt by one. It is drawn again
            // after a pick too, so the list marks the entry now chosen, and the pick says what it set.
            var brightness = new Border { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            var rigShown = -1;
            Func<int> rigInForce = () => PanelLeds.RigBrightnessInForce(Settings.LightsNightMode, Settings.LightsBrightness, Settings.LightsNightBrightness);
            Action<bool> drawBrightness = null;
            drawBrightness = refocus =>
            {
                refocus = refocus || brightness.IsKeyboardFocusWithin;
                rigShown = rigInForce();
                brightness.Child = Ui.ChoiceButton(PanelLeds.BrightnessLabels(rigShown), PanelLeds.BrightnessIndex(Settings.BarBrightness(ns)), i =>
                {
                    var value = PanelLeds.BrightnessValue(i);
                    Settings.SetBarBrightness(ns, value);
                    Save();
                    // At night the preview and the card are drawn at the lower of this and the night brightness.
                    redrawPreview();
                    drawBrightness(true);
                    // The page is not redrawn, so the last press's line goes first rather than stacking.
                    var live = Settings.LedBarByNamespace(ns) ?? bar;
                    ClearMessages();
                    Say(PanelLeds.BrightnessSaid(live.Name, value, Settings.LightsNightMode, Settings.LightsNightBrightness));
                }, 160);
                if (refocus) LedsFocusLater(() => LedsFirstControl(brightness.Child));
            };
            drawBrightness(false);
            if (ledsFocusDevice)
            {
                ledsFocusDevice = false;
                // The picker drawn in the pressed one's place, or, where the pick left the row a caption with
                // nothing to press, the Brightness chooser under it: focus that sat in the closed list must land
                // in the panel, not in SimHub's window.
                LedsFocusLater(() => LedsFocusable(LedsFirstControl(LedsRowControl(deviceRow))) ?? LedsFirstControl(brightness.Child));
            }
            // Not while its list is open: the chooser would be replaced under the popup, which would stay up with
            // the old value and out of Escape's reach. The redraw waits for the list to close.
            var owed = false;
            OnLighting(() =>
            {
                if (rigInForce() == rigShown) return;
                var open = LedsFirstControl(brightness.Child) as ToggleButton;
                if (open == null || open.IsChecked != true)
                {
                    drawBrightness(false);
                    return;
                }
                if (owed) return;
                owed = true;
                RoutedEventHandler closed = null;
                closed = (sender, args) =>
                {
                    open.Unchecked -= closed;
                    owed = false;
                    if (rigInForce() != rigShown) drawBrightness(false);
                };
                open.Unchecked += closed;
            });
            rows.Add(Ui.Anchor(LedsRow(PanelLeds.BrightnessTitle, brightness, null, Ui.NewTag()), PanelLeds.AnchorBrightness));

            // Only a shape that has a twin wired from the far end: a Fanatec wheel's wiring is its own.
            if (bar.SupportsReversal)
            {
                // The press redraws the page and then says a line at its top; the shell hands focus back to the switch
                // without scrolling the line away (RestoreFocus gives way to Say).
                var reverse = Ui.Switch(Settings.BarReversed(ns), on => ReverseLedBar(ns, on));
                rows.Add(Ui.Anchor(LedsRow(PanelLeds.ReverseTitle, reverse, null, Ui.NewTag()), PanelLeds.AnchorReverse));
            }
            rows.Add(LedsSoonRow(PanelSoon.EachLedInTurn, Ui.Button(PanelLeds.EachLedStart, PanelButtonKind.Outline, PanelButtonSize.Small)));

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
            // NEW, as Brightness and Reverse direction carry it: none of the fifteen switches (#370) shipped in rc.7.
            var tag = Ui.NewTag();
            tag.Margin = new Thickness(8, 0, 0, 0);
            tag.VerticalAlignment = VerticalAlignment.Center;
            var title = Ui.HStack(0, Ui.Heading(PanelLeds.EffectsTitle), tag);
            title.HorizontalAlignment = HorizontalAlignment.Left;
            head.Children.Add(title);

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
            if (PanelLeds.HasFullStripSpotter(bar.Shape))
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
                    LedsSoonRow(PanelSoon.IdleSweep, Ui.Switch(true, on => { })),
                    LedsSoonRow(PanelSoon.EngineStartAnimation, Ui.Switch(true, on => { })),
                    Ui.Anchor(BuildCarTablesRow(), PanelLeds.AnchorCarTables),
                    Ui.Soon(carData, PanelSoon.CarDataForAcAccLmu))));
        }

        /// <summary>
        /// Car Data: what is on disk, what the download is, whose measurements they are, and the button
        /// that fetches them.
        /// </summary>
        /// <remarks>
        /// A button rather than a setting, and that is the whole of #789. The tables used to arrive on
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
            carTablesButton.Click += (sender, args) => DownloadCarTables();

            var row = LedsRow(PanelLights.CarTablesTitle, carTablesButton);
            // Capped as a row's own caption is, so none of the three runs the width of a desk.
            carTablesLine = LedsCaptionLine(string.Empty);
            var lines = new UIElement[]
            {
                carTablesLine,
                LedsCaptionLine(PanelLights.CarTablesCaption),
                LedsCaptionLine(PanelLights.CarTablesAttribution),
            };
            // Under the row's title, in the column the kit draws it in; where the kit's row holds no such column,
            // under the row, since the licence's attribution and the status line are drawn either way.
            var parts = row.Tag as RowParts;
            var left = parts == null || parts.TitleLine == null ? null : parts.TitleLine.Parent as StackPanel;
            FrameworkElement drawn = row;
            if (left != null)
            {
                foreach (var line in lines) left.Children.Add(line);
            }
            else
            {
                drawn = Ui.VStack(0, new UIElement[] { row }.Concat(lines).ToArray());
            }
            RefreshCarTables();
            // The start reads the tables on a thread of its own, so a page opened before it lands would say
            // "Loading…" beside Download until something drew it again, while the car line above read the tables.
            // Each tick compares two properties and draws the row again only on a change; nothing reads the disk.
            OnTick(() =>
            {
                if (carTablesLine == null || carTablesDownloading) return;
                if (!string.Equals(CarTablesKey(), carTablesDrawn, StringComparison.Ordinal)) RefreshCarTables();
            });
            return drawn;
        }

        /// <summary>What the car tables' row is drawn from: the service's status and its count of cars.</summary>
        private string CarTablesKey()
        {
            return plugin.CarLights.Status + "|" + plugin.CarLights.CarCount.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A line under a row's title, capped at the width the kit gives a row's caption.</summary>
        private static TextBlock LedsCaptionLine(string text)
        {
            var line = Ui.Prose(text);
            line.Margin = new Thickness(0, PanelKit.FixDetailGap, 0, 0);
            // As the kit caps a row's own caption: prose that runs the width of a desk is a line nobody finishes.
            line.MaxWidth = PanelShell.RowCaptionMaxWidth;
            line.HorizontalAlignment = HorizontalAlignment.Left;
            return line;
        }

        /// <summary>The status line and the button's label, which are one answer and so are written together.</summary>
        private void RefreshCarTables()
        {
            if (carTablesLine == null) return;
            var service = plugin.CarLights;
            carTablesDrawn = CarTablesKey();
            var status = service.Status;
            // A failure to read is said in the row's words, so its reason goes to the log, once for each.
            var error = service.Error;
            if (PanelLights.CarTablesUnread(status) && !string.Equals(error, carTablesLogged, StringComparison.Ordinal))
            {
                carTablesLogged = error;
                Log.Warn("Car Data could not be read: " + error);
            }
            // Whether the copy is over a week old is worked out as the row is drawn: the status is written only
            // when the tables are read, so the age it gives stands still while SimHub runs. From the stamp the
            // service holds rather than the folder's, since a tick draws this row: the read writes the stamp
            // before the status, so read after it.
            var stale = PanelLights.CarTablesStale(service.CarCount, service.FetchedAt, DateTime.UtcNow);
            var line = PanelLights.CarTablesLine(status, stale);
            carTablesLine.Text = carTablesDownloading ? PanelLights.CarTablesDownloading : line;
            if (carTablesButton != null)
            {
                carTablesButton.Content = PanelLights.CarTablesButton(service.CarCount);
                carTablesButton.ToolTip = PanelLights.CarTablesButtonTooltip(service.CarCount);
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
                // The row says only that the download failed, so its reason goes to the log. The service keeps
                // the fetch's message in its status, with a copy on disk or without, and not in what Download
                // returns when a copy still works; a copy it could not read is logged as the row is drawn.
                var status = plugin.CarLights.Status;
                if (PanelLights.CarTablesDownloadDidNotAnswer(status)) Log.Warn("Car Data could not be downloaded: " + status);
                Dispatcher.Invoke(() =>
                {
                    carTablesDownloading = false;
                    RefreshCarTables();
                });
            }, new SimHubInstallLog());
        }

        // --- The strip's device and its presses -------------------------------------------------------------------

        /// <summary>
        /// The device picker: which of SimHub's LED devices a strip's profile goes to, as PanelLeds.DeviceRow
        /// decides it from SimHub's devices, the strip's own and the devices passed over.
        /// </summary>
        /// <remarks>
        /// A device SimHub has and OpenDash did not offer is named under the row in every shape, with the
        /// reason in SimHub's log: a wheel missing from the picker with nothing said about it is how #437 was
        /// reported, and the line would have answered it.
        /// </remarks>
        private static Border BuildLedDeviceRow(IList<LedTarget> targets, IList<string> declined, string current, bool movable, Action<string> chosen)
        {
            var row = PanelLeds.DeviceRow(targets.Select(t => new LedDeviceEntry(t.Id, t.Name, t.Connected)).ToList(), current, declined);
            if (!row.HasPicker) return LedsRow(PanelLights.BarDeviceTitle, null, row.Caption);
            var ids = row.Ids;
            var labels = row.Labels;
            var picker = Ui.ChoiceButton(labels.ToArray(), row.Selected, i =>
            {
                if (!string.Equals(ids[i], current, StringComparison.Ordinal)) chosen(ids[i]);
            }, PanelLeds.DevicePickerMinWidth);
            // Bounded by the room the row has, so a long device name trims inside the button rather than crushing
            // the row's title or running out of its column; the whole name is the hover. Set as the row is laid
            // out rather than from the content width, which would have a resize build the page again.
            picker.MaxWidth = PanelLeds.DevicePickerMaxWidth;
            // Where a pick could only be refused, the picker shows the strip's device and takes no pick: the header
            // says why.
            picker.IsEnabled = movable;
            if (!movable)
            {
                // Faded as the kit's other controls are when they take no press, since the choice button's own
                // template draws a disabled picker at full ink; and its hover still names the whole device.
                picker.Opacity = PanelMetrics.DisabledOpacity;
                ToolTipService.SetShowOnDisabled(picker, true);
            }
            if (row.Selected >= 0) picker.ToolTip = labels[row.Selected];
            var drawn = LedsRow(PanelLights.BarDeviceTitle, picker, row.Caption);
            drawn.SizeChanged += (sender, args) => picker.MaxWidth = PanelLeds.DevicePickerWidth(drawn.ActualWidth);
            return drawn;
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
            // RebuildPage keeps the lines, so the last press's goes first rather than stacking over this one's.
            ClearMessages();
            // The picker was pressed, and the page is drawn again under it: its successor takes keyboard focus.
            ledsFocusDevice = true;
            // By the profile the strip installs, which is the reversed twin for a strip wired from the far end, and
            // before the device is written: a strip whose profile the build does not carry keeps the device its
            // profile is on, and the page is drawn again so the picker says so.
            var found = EmbeddedProfileOf(bar);
            if (found == null)
            {
                Log.Warn("The profile for " + bar.Name + " was not moved: this build carries no profile of its shape, " + bar.ProfileShapeId + ".");
                RebuildPage();
                // A build that never reached the device row leaves the flag for an unrelated one: it ends here.
                ledsFocusDevice = false;
                Say(PanelMessage.Caution(PanelLeds.WithReason(PanelLeds.NotMoved(bar.Name), PanelLeds.NoProfileForStrip)));
                return;
            }
            bar.Device = LedBar.NormaliseDevice(device);
            Save();
            var targets = ledDevices.Targets;
            var plan = LedsLogged(bar, InstallBar(bar, found.Json, targets));
            var ok = plan.State == FlagBoxInstallState.UpToDate;
            var target = LedsTargetOf(targets, bar.Device);
            var line = ok ? PanelLeds.Moved(bar.Name, target == null ? null : target.Name, plan.Note) : PanelLeds.MovedNotInstalled(bar.Name, target == null ? null : target.Name);
            // The strip's device, and so whether its profile is selected, moved with the press: the card, the
            // fix box and the sidebar's dot are drawn again from what SimHub says now.
            RefreshAttention();
            RebuildPage();
            ledsFocusDevice = false;
            RefreshSidebar();
            Say(line, ok && plan.Note == null);
        }

        /// <summary>What SimHub shows of the strip's profile, in the card's words and ink, then the presses on the
        /// strip: Install or Update where its profile needs one and the page can install it, Rename and Remove.</summary>
        /// <remarks>A WrapPanel, so where the header is <paramref name="stacked"/> under the name the presses drop to
        /// a second line rather than running out of the column; beside the name it is one line, as the artboard
        /// draws it.</remarks>
        private FrameworkElement BuildLedBarActions(string ns, string blocked, bool stacked)
        {
            var presses = new List<UIElement>();
            var facts = StripFacts(ns);
            var profile = facts == null ? null : facts.Profile;
            var selected = facts == null ? null : facts.Selected;
            var state = PanelLeds.StateText(profile, selected);
            if (state != null)
            {
                var hex = PanelLeds.StateHex(profile, selected);
                var dot = new Ellipse { Width = PanelKit.LightCardStateDot, Height = PanelKit.LightCardStateDot, Fill = Ui.Brush(hex), VerticalAlignment = VerticalAlignment.Center };
                dot.Margin = new Thickness(0, 0, PanelKit.CardStateGap, 0);
                var word = Ui.Text(state, PanelKit.LightCardStateSize, FontWeights.Normal, hex);
                word.VerticalAlignment = VerticalAlignment.Center;
                var shown = Ui.HStack(0, dot, word);
                shown.VerticalAlignment = VerticalAlignment.Center;
                presses.Add(shown);
            }
            var action = blocked == null ? PanelLeds.ProfileAction(profile) : null;
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
            var actions = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (stacked) actions.HorizontalAlignment = HorizontalAlignment.Left;
            var line = stacked ? 3 : 0;
            for (var i = 0; i < presses.Count; i++)
            {
                var press = (FrameworkElement)presses[i];
                // The state sits 12 from the first press, as the artboard spaces it, and the presses 6 apart.
                var after = i == presses.Count - 1 ? 0 : i == 0 && state != null ? 12 : 6;
                press.Margin = new Thickness(0, line, after, line);
                actions.Children.Add(press);
            }
            return actions;
        }

        /// <summary>Installs the strip's profile, or this build's newer version of it, into its device.</summary>
        private void InstallLedBarProfile(string ns)
        {
            var bar = Settings.LedBarByNamespace(ns);
            if (bar == null) return;
            var facts = StripFacts(ns);
            var before = facts == null ? null : facts.Profile;
            // SimHub's devices as the page read them, for the guard, the install and the line.
            var targets = ledDevices.Targets;
            var declined = ledDevices.Declined;
            var blocked = LedsProfileBlocked(bar, targets, declined);
            var plan = blocked == null ? LedsReinstall(bar, targets) : null;
            Redraw();
            if (plan == null)
            {
                // The page was drawn before the build or SimHub changed under it: the reason, not the log.
                Say(PanelMessage.Caution(blocked ?? PanelLeds.ProfileFailed(bar.Name)));
                return;
            }
            var ok = plan.State == FlagBoxInstallState.UpToDate;
            var target = LedsTargetOf(targets, bar.Device);
            Say(PanelLeds.InstallSaid(ok, before, bar.Name, target == null ? null : target.Name, plan.Note), ok && plan.Note == null);
        }

        /// <summary>
        /// Turns the strip's direction round, which installs the other twin of its profile where the page can
        /// install it; where it cannot, the direction is saved and the line says what is left to do.
        /// </summary>
        private void ReverseLedBar(string ns, bool reversed)
        {
            var bar = Settings.LedBarByNamespace(ns);
            if (bar == null || !Settings.SetBarReversed(ns, reversed)) return;
            Save();
            var facts = StripFacts(ns);
            var held = PanelLeds.HeldInSimHub(facts == null ? null : facts.Profile);
            var targets = ledDevices.Targets;
            var declined = ledDevices.Declined;
            var blocked = LedsProfileBlocked(bar, targets, declined);
            var plan = blocked == null ? LedsReinstall(bar, targets) : null;
            Redraw();
            if (plan == null)
            {
                Say(PanelMessage.Caution(PanelLeds.WithReason(PanelLeds.ReverseSaid(bar.Name, reversed), blocked)));
                return;
            }
            var ok = plan.State == FlagBoxInstallState.UpToDate;
            var target = LedsTargetOf(targets, bar.Device);
            var line = ok ? PanelLeds.ReverseSaid(bar.Name, reversed, held, target == null ? null : target.Name, plan.Note) : PanelLeds.ReversedNotInstalled(bar.Name, reversed);
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
            // A blank name renames nothing, so the press waits for one rather than doing nothing when pressed.
            name.TextChanged += (sender, args) => save.IsEnabled = PanelLeds.CanRename(name.Text);
            save.IsEnabled = PanelLeds.CanRename(name.Text);
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
            if (bar == null || !PanelLeds.CanRename(wanted)) return;
            var facts = StripFacts(ns);
            var inSimHub = PanelLeds.RenameReinstalls(facts == null ? null : facts.Profile);
            Settings.RenameLedBar(ns, wanted);
            Save();
            // Only where the page can install it: a rename must not take a profile that still lights out of
            // SimHub and put nothing back.
            // SimHub's devices as the page read them, and only where the rename installs again.
            var targets = inSimHub ? ledDevices.Targets : null;
            var declined = inSimHub ? ledDevices.Declined : null;
            var blocked = inSimHub ? LedsProfileBlocked(bar, targets, declined) : null;
            var ok = true;
            if (inSimHub && blocked == null)
            {
                var plan = LedsReinstall(bar, targets);
                ok = plan != null && plan.State == FlagBoxInstallState.UpToDate;
            }
            Redraw();
            if (blocked != null)
            {
                Say(PanelMessage.Caution(PanelLeds.WithReason(PanelLeds.Renamed(bar.Name, false), blocked)));
                return;
            }
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
            ShowSheet(PanelLeds.RemoveTitle(bar.Name), Ui.Prose(PanelLeds.RemoveBody, Theme.SizeBody), SheetFooter(null, cancel, remove));
        }

        private void RemoveLedBar(string ns)
        {
            var bar = Settings.LedBarByNamespace(ns);
            if (bar == null) return;
            var name = bar.Name;
            var facts = StripFacts(ns);
            var held = PanelLeds.HeldInSimHub(facts == null ? null : facts.Profile);
            bool? takenOut;
            try
            {
                // Everywhere, not just the device the strip names: the device it was installed into may
                // have been removed from SimHub since, and a profile nothing attaches settings to any
                // more is a row in somebody's list that lights nothing.
                takenOut = StripInstaller.UninstallEverywhere(LedBarProfile.IdFor(ns), ledDevices.Targets);
            }
            catch (Exception ex)
            {
                Log.Warn("The profile for " + name + " could not be removed from SimHub: " + ex.Message);
                takenOut = null;
            }
            if (takenOut == false && held) Log.Warn("The profile for " + name + " was not removed from SimHub: no LED device OpenDash can reach holds it.");
            Settings.RemoveLedBar(ns);
            Save();
            Select(PanelPage.Leds, null);
            Redraw();
            Say(PanelLeds.Removed(name, held, takenOut), PanelLeds.RemovedCleanly(held, takenOut));
        }

        // --- The Add LEDs sheet ---------------------------------------------------------------------------------

        /// <summary>
        /// Adding a strip, in three steps: the SimHub device its profile goes to, its shape, and its name. The
        /// press adds the strip and installs its profile.
        /// </summary>
        /// <remarks>
        /// The shapes are read out of the assembly rather than listed here, exactly as the Updates page's
        /// rows are: the census is what this build embedded, so a shape it does not carry is not offered
        /// and cannot be added as a strip whose profile does not exist.
        ///
        /// <para>The two numbers cannot say how a wheel is wired, so a Fanatec owner who added a strip by its
        /// counts got the plain 3/9/3, which lights only some of the wheel's LEDs and starts the bar from the
        /// middle of the rim (#436). The device answers that question, which is why it is asked first: a
        /// Fanatec wheel picked here fixes the shape to the Fanatec 3 · 9 · 3, any other device offers the
        /// plain shape, and the sheet opens on a Fanatec wheel when the rig has one, so nobody is asked what
        /// their device already says (#686). The Fanatec wiring is offered only where the build embedded it.</para>
        /// </remarks>
        private void ShowAddLedBar()
        {
            var census = EmbeddedShapeIds();
            // Two numbers rather than a list of sixty-three. A driver knows how many LEDs their strip
            // has and how they are grouped, which is exactly A and B; a drop-down asked them to find
            // "3/9/3" among every other geometry and to know that is what their wheel is called.
            var sides = PanelLights.BarSides(census);
            var offersFanatec = PanelLights.OffersFanatec(census);
            if (!PanelLeds.SheetHasShapes(sides.Length, offersFanatec))
            {
                ShowSheet(PanelLights.AddBar, Ui.Prose(PanelLightRows.NoProfiles), null);
                return;
            }

            // Which device gets the profile. SimHub keeps one profile list per LED device, so this is
            // not a detail: a strip installed into the wrong one is written, saved and verified correctly
            // into a list the hardware does not read, which is exactly what a rig reported.
            var targets = ledDevices.Targets;
            var notOffered = ledDevices.Declined;
            var preferred = LedTargets.Preferred(targets);
            var device = preferred == null ? LedBar.ArduinoDevice : preferred.Id;

            var side = PanelLeds.StartSide(sides);
            var centres = PanelLights.BarCentres(census, side);
            var centre = PanelLeds.KeptCentre(centres, 9);
            // The one question the two numbers cannot answer: how the wheel is wired. The device picked answers
            // it, and side and centre keep what the driver chose so that another device gives that back.
            var fanatec = PanelLeds.WiringFollowsDevice(sides.Length > 0, offersFanatec, preferred == null ? null : preferred.Name);

            var name = Ui.Input(string.Empty);
            var typed = false;
            var footerNote = Ui.Prose(string.Empty, PanelKit.CardMetaSize);
            // Borders rather than ContentControls, which WPF makes tab stops: a host is only where a step's
            // controls go, and an invisible stop before each step is a Tab press that shows nothing.
            var shapeHost = new Border();
            var deviceHost = new Border();
            UIElement chosenDevice = null;

            Action updateFooter = () =>
            {
                // The name the press will add under, a cleared box included, and nothing where there is no
                // device to install on: step 3 says why.
                var target = targets.FirstOrDefault(t => string.Equals(t.Id, device, StringComparison.Ordinal));
                var adds = PanelLeds.NameToAdd(name.Text, DefaultBarName(PanelLights.BarShapeId(side, centre, fanatec)), TakenBarNames());
                var said = PanelLeds.InstallsOn(adds, target == null ? null : target.Name);
                footerNote.Text = said ?? string.Empty;
                footerNote.Visibility = said == null ? Visibility.Collapsed : Visibility.Visible;
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

            // Each picker is drawn once and keeps keyboard focus: a change of ends swaps only what is under the
            // ends bar, a change of centre only the picture and the note, and a device picked hands focus to
            // the one drawn in its place.
            Action showShape = () =>
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
                    shapeHost.Child = new Border
                    {
                        Background = Ui.Brush(Theme.SurfaceZone),
                        CornerRadius = new CornerRadius(Theme.Radius),
                        Padding = new Thickness(14, 12, 14, 12),
                        Child = fixedDock,
                    };
                    return;
                }

                var centreRow = new Border();
                var picture = new Border { HorizontalAlignment = HorizontalAlignment.Center };
                var note = Ui.Prose(string.Empty);
                Action showPicture = () =>
                {
                    // A 25-LED run is 438 wide at the sheet's size: it shrinks to a narrow sheet rather than being cut.
                    picture.Child = new Viewbox
                    {
                        Stretch = Stretch.Uniform,
                        StretchDirection = StretchDirection.DownOnly,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Child = Ui.Strip(PanelLeds.ShapeFrame(side, centre), StripStyle.AddLeds),
                    };
                    note.Text = PanelLights.BarShapeNote(side, centre, fanatec);
                };
                // Drawn again after a pick, so the list marks the count now chosen, and keyboard focus goes back
                // to the button drawn in its place.
                FrameworkElement middle = null;
                Action showCentre = null;
                showCentre = () =>
                {
                    middle = Ui.ChoiceButton(
                        centres.Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray(),
                        Array.IndexOf(centres, centre),
                        i =>
                        {
                            centre = centres[i];
                            showCentre();
                            refresh();
                            LedsFocusLater(() => LedsFirstControl(middle));
                        }, 90);
                    // No caption, as the artboard has none: the note under the picture is where the count is checked.
                    centreRow.Child = LedsSheetRow(PanelLights.BarCentreTitle, middle);
                    showPicture();
                };

                // The artboard's 40 px a button, where a bare digit would make it about 31.
                var ends = new Segmented(
                    sides.Select(n => new Segmented.Option(n.ToString(CultureInfo.InvariantCulture), PanelLeds.EndsLabel(n), minWidth: PanelKit.SegmentMinWidth)),
                    side.ToString(CultureInfo.InvariantCulture));
                ends.Changed += value =>
                {
                    side = int.Parse(value, CultureInfo.InvariantCulture);
                    centres = PanelLights.BarCentres(census, side);
                    // A side of none reaches twenty-five and a side of four stops at twelve, so the choice
                    // of centre follows the choice of ends rather than offering lengths nothing is built for.
                    centre = PanelLeds.KeptCentre(centres, centre);
                    showCentre();
                    refresh();
                };
                showCentre();
                var well = new Border { Background = Ui.Brush(Theme.SurfaceInset), CornerRadius = new CornerRadius(Theme.Radius), Child = picture };
                shapeHost.Child = Ui.VStack(12,
                    LedsSheetRow(PanelLights.BarEndsTitle, ends),
                    centreRow,
                    well,
                    note);
            };

            Action showDevices = null;
            showDevices = () =>
            {
                var list = new StackPanel { Orientation = Orientation.Vertical };
                var passedOver = notOffered ?? new string[0];
                chosenDevice = null;
                // A device passed over is a disabled row saying so, so the prose says only what no row can.
                if (PanelLeds.ShowsNoDevices(targets.Count, passedOver.Count)) list.Children.Add(Ui.Prose(PanelLights.NoDevices));
                foreach (var target in targets)
                {
                    var id = target.Id;
                    // The rig's strips already on the device, so a driver sees it is taken before adding another.
                    var onIt = Settings.LedBarList()
                        .Where(other => other != null && string.Equals(Settings.BarDevice(other.Namespace), id, StringComparison.Ordinal))
                        .Select(other => other.Name);
                    var chosen = string.Equals(id, device, StringComparison.Ordinal);
                    var radio = Ui.RadioRow(target.Name, PanelLeds.DeviceMeta(target.Connected, onIt), chosen, () =>
                    {
                        var focused = deviceHost.IsKeyboardFocusWithin;
                        device = id;
                        // A Fanatec wheel picked here is the wiring answered, and the shape step follows.
                        var wiring = PanelLeds.WiringFollowsDevice(sides.Length > 0, offersFanatec, target.Name);
                        if (wiring != fanatec)
                        {
                            fanatec = wiring;
                            showShape();
                        }
                        showDevices();
                        refresh();
                        if (focused) LedsFocusLater(() => chosenDevice);
                    });
                    radio.Margin = new Thickness(0, 0, 0, 4);
                    if (chosen) chosenDevice = radio;
                    list.Children.Add(radio);
                }
                foreach (var passed in passedOver)
                {
                    var radio = Ui.RadioRow(passed, PanelLeds.NotReachable, false, null, false);
                    radio.Margin = new Thickness(0, 0, 0, 4);
                    list.Children.Add(radio);
                }
                if (passedOver.Count > 0) list.Children.Add(Ui.Prose(PanelLeds.PassedOverNote));
                deviceHost.Child = list;
            };

            showShape();
            showDevices();
            refresh();

            var add = Ui.Button(PanelLeds.AddPress(targets.Count > 0), PanelButtonKind.Primary, PanelButtonSize.Large);
            add.MinWidth = ButtonMinWidth;
            add.Click += (sender, args) => AddLedBar(PanelLights.BarShapeId(side, centre, fanatec), name.Text, device);
            var cancel = Ui.Button("Cancel", PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.Click += (sender, args) => CloseSheet();

            var body = Ui.VStack(0,
                Ui.Step(1, PanelLights.BarDeviceTitle, deviceHost, true),
                Ui.Step(2, PanelLeds.ShapeStep, shapeHost),
                Ui.Step(3, PanelLights.BarNameTitle, name));
            ShowSheet(PanelLights.AddBar, body, Ui.VStack(14, footerNote, SheetFooter(null, cancel, add)));
        }

        /// <summary>A label and its control on one line, as the sheet's shape step draws them, with a caption
        /// under the label where it has one.</summary>
        private static FrameworkElement LedsSheetRow(string label, FrameworkElement control, string caption = null)
        {
            var text = Ui.Text(label, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary);
            // Wrapped rather than cut where a narrow sheet leaves the label less than the control beside it does:
            // "LEDs at each end" beside the six ends is about 378 px.
            text.TextWrapping = TextWrapping.Wrap;
            FrameworkElement words = text;
            if (!string.IsNullOrEmpty(caption))
            {
                var under = Ui.Prose(caption, PanelKit.CardMetaSize);
                under.Margin = new Thickness(0, 2, 0, 0);
                words = Ui.VStack(0, text, under);
            }
            words.VerticalAlignment = VerticalAlignment.Center;
            words.Margin = new Thickness(0, 0, 12, 0);
            control.HorizontalAlignment = HorizontalAlignment.Right;
            control.VerticalAlignment = VerticalAlignment.Center;
            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(control, Dock.Right);
            dock.Children.Add(control);
            dock.Children.Add(words);
            return dock;
        }

        /// <summary>What the name box opens on: "Wheel rim" for the Fanatec wheel and "Strip" for anything else,
        /// numbered as the rig numbers a second one. SimHub's list shows the profile under this name.</summary>
        private string DefaultBarName(string shape)
        {
            return PanelLeds.DefaultName(shape, TakenBarNames());
        }

        /// <summary>The names the rig's strips already have, which a new one is numbered against.</summary>
        private IEnumerable<string> TakenBarNames()
        {
            return Settings.LedBarList().Where(bar => bar != null).Select(bar => bar.Name).ToList();
        }

        private void AddLedBar(string shape, string name, string device)
        {
            var bar = Settings.AddLedBar(shape, string.IsNullOrWhiteSpace(name) ? DefaultBarName(shape) : name, device);
            Save();
            Select(PanelPage.Leds, bar.Namespace);
            // With no device SimHub lists there is nowhere to install: the strip is added, the press said no
            // more, and the line names the step left.
            var targets = ledDevices.Targets;
            var declined = ledDevices.Declined;
            var target = LedsTargetOf(targets, bar.Device);
            if (target == null)
            {
                Redraw();
                Say(PanelMessage.Caution(PanelLeds.AddedWithoutDevice(bar.Name, declined)));
                return;
            }
            var plan = LedsReinstall(bar, targets);
            var ok = plan != null && plan.State == FlagBoxInstallState.UpToDate;
            Redraw();
            // A note is not a failure, so the line stays the ordinary one and gains a sentence, before the select
            // it enables. The one note there is says the device is listing its maker's built-in profiles, which is
            // the only way an install can be correct and still leave nothing for the driver to select.
            var note = ok ? plan.Note : null;
            var line = ok ? PanelLights.BarAdded(bar.Name, target.Name, note) : PanelLights.BarAddFailed(bar.Name);
            Say(line, ok && note == null);
        }
    }
}
