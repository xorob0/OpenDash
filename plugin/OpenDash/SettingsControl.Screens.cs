// SettingsControl.Screens.cs: the Screens page (Screens.dc.html) -- a card per screen on the rig, and the
// selected screen under them: its header, what needs fixing, its live preview and the editor of its kind.
//
// The editors are the files beside this one: .Screens.Face.cs, .Screens.PitWall.cs, .Screens.Companion.cs
// and .Screens.Round.cs. Add, Edit and Remove open in the sheet (AddScreen.dc.html for Add). Every decision
// and every word is PanelScreens' or PanelAddScreen's; this file draws them.
//
// A screen is the unit (ADR 0017). A face is configured on a picture of itself, because "zone C" means nothing
// until you see where zone C is.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>The screen whose settings are showing: the one selected, else the first. Null when the
        /// rig is empty.</summary>
        private ScreenInstance SelectedScreen
        {
            get
            {
                var rig = Settings.RigScreens();
                if (rig.Count == 0) return null;
                var chosen = Settings.ScreenByNamespace(Selected(PanelPage.Screens));
                return chosen ?? rig[0];
            }
        }

        /// <summary>The size an icon is drawn at when it stands beside a line of prose rather than inside a
        /// control, which is the second of the two sizes control.icon describes.</summary>
        private const double IconAlone = PanelIcons.SizeAlone;

        /// <summary>Whether the Details under a screen's rows are open, for the session.</summary>
        private bool screensDetailsOpen;

        private FrameworkElement BuildScreensPage(PanelRoute to)
        {
            var rig = Settings.RigScreens();
            var sections = new List<UIElement>();
            if (PanelScreens.ShowsUnclaimedNote(rig)) sections.Add(BuildUnclaimedNote());
            sections.Add(Ui.Anchor(BuildScreenCards(rig), PanelScreens.AnchorCards));
            if (rig.Count == 0)
            {
                sections.Add(Ui.Prose(PanelCopy.EmptyRig, Theme.SizeBody));
                return PageLayout(PanelScreens.Title, null, sections.ToArray());
            }

            var screen = SelectedScreen;
            var selected = new List<UIElement> { BuildScreenHeader(screen) };
            var fix = BuildScreenFix(screen);
            if (fix != null) selected.Add(fix);
            // The screen itself, between its name and the controls that change it (ADR 0020). Null when its
            // package is not installed, which the fix above says in its own words.
            var preview = BuildScreenPreview(screen, ContentWidth);
            if (preview != null) selected.Add(preview);
            selected.Add(BuildScreenEditor(screen));

            var block = new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(0, 20, 0, 0),
                Child = Ui.VStack(18, selected.ToArray()),
            };
            sections.Add(block);
            return PageLayout(PanelScreens.Title, null, sections.ToArray());
        }

        /// <summary>
        /// The cards, one per screen, and the dashed tile that adds one: a picture of the screen's shape, its
        /// name, its kind, and whether SimHub has it.
        /// </summary>
        private FrameworkElement BuildScreenCards(IReadOnlyList<ScreenInstance> rig)
        {
            var current = SelectedScreen;
            var cards = new List<UIElement>();
            foreach (var screen in rig)
            {
                var captured = screen;
                var state = ScreensStateOf(captured);
                var thumb = Ui.Thumb(captured.IsSlots ? "round" : captured.Kind, captured.Width, captured.Height);
                cards.Add(Ui.DeviceCard(
                    thumb,
                    captured.Name,
                    PanelScreens.CardMeta(captured),
                    PanelScreens.StateLabel(state),
                    PanelScreens.StateHex(state),
                    current != null && ReferenceEquals(current, captured),
                    () =>
                    {
                        Select(PanelPage.Screens, captured.Namespace);
                        Redraw();
                    }));
            }
            cards.Add(Ui.DashedAddCard(PanelAddScreen.SectionTitle, ShowAddScreen));
            return Ui.CardGrid(PanelKit.CardMinWidth, PanelKit.CardGridGap, 6, cards.ToArray());
        }

        /// <summary>Whether SimHub has the screen: its folder, then whether it was written after SimHub started
        /// (the answer from the last Go, Redraw or Check again).</summary>
        private ScreenState ScreensStateOf(ScreenInstance screen)
        {
            return PanelScreens.StateOf(Installed(screen), PanelAttention.Has(issues, PanelAttention.ScreenRestart, screen.Namespace));
        }

        private bool Installed(ScreenInstance screen)
        {
            try
            {
                return screen.Folder != null && PackageExtractor.IsInstalled(plugin.Installer.SimHubRoot, screen.Folder);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not tell whether " + screen.Folder + " is installed: " + ex.Message);
                return true;
            }
        }

        /// <summary>
        /// The line an upgrading user meets, over the cards, and only them: a rig migrated from an older
        /// plugin holds cards for screens nobody owns. Nothing is deleted on their behalf (ADR 0017); the line
        /// goes when it stops being true rather than when somebody dismisses it.
        /// </summary>
        private FrameworkElement BuildUnclaimedNote()
        {
            var icon = Ui.NavIcon(PanelIcons.Warning, Theme.Caution, IconAlone);
            icon.VerticalAlignment = VerticalAlignment.Top;
            var text = Ui.Prose(PanelScreens.UnclaimedNote);
            return Ui.HStack(10, icon, text);
        }

        /// <summary>The name, the kind and size beside it, and the presses that act on the screen itself.</summary>
        private FrameworkElement BuildScreenHeader(ScreenInstance screen)
        {
            var title = Ui.SubHeading(screen.Name);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            var facts = Ui.Prose(PanelScreens.Facts(screen));
            facts.TextWrapping = TextWrapping.NoWrap;
            facts.VerticalAlignment = VerticalAlignment.Bottom;
            facts.Margin = new Thickness(0, 0, 0, 3);
            var name = Ui.HStack(12, title, facts);

            var edit = Ui.Button(PanelScreens.EditButton, PanelButtonKind.Outline, PanelButtonSize.Small);
            edit.ToolTip = PanelScreens.EditTooltip;
            edit.Click += (sender, args) => ShowEdit(screen);
            // New in this release, and tagged so for one (Screens.dc.html).
            var duplicate = Ui.Button(PanelScreens.DuplicateButton, PanelButtonKind.Outline, PanelButtonSize.Small);
            duplicate.Content = Ui.HStack(8, Ui.Text(PanelScreens.DuplicateButton, Theme.SizeSmall, FontWeights.Medium, Theme.TextPrimary), Ui.NewTag());
            duplicate.ToolTip = PanelScreens.DuplicateTooltip;
            duplicate.Click += (sender, args) => DuplicateScreen(screen);
            var remove = Ui.Button(PanelScreens.RemoveButton, PanelButtonKind.GhostDanger, PanelButtonSize.Small);
            remove.ToolTip = PanelScreens.RemoveTooltip;
            remove.Click += (sender, args) => ShowRemove(screen);
            var presses = Ui.HStack(6, edit, duplicate, remove);

            // Side by side while both fit, the presses under the name on a narrow page.
            if (!TwoColumns)
            {
                presses.Margin = new Thickness(0, 10, 0, 0);
                return Ui.VStack(0, name, presses);
            }
            var row = new DockPanel { LastChildFill = true };
            presses.VerticalAlignment = VerticalAlignment.Center;
            presses.Margin = new Thickness(24, 0, 0, 0);
            DockPanel.SetDock(presses, Dock.Right);
            row.Children.Add(presses);
            name.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(name);
            return row;
        }

        /// <summary>
        /// What is wrong with this screen, as a fix box: its folder is gone, or it was written after SimHub
        /// started and SimHub has not read it. Null when nothing is.
        /// </summary>
        private FrameworkElement BuildScreenFix(ScreenInstance screen)
        {
            switch (ScreensStateOf(screen))
            {
                case ScreenState.Missing:
                    var write = Ui.Button(PanelAttention.InstallAgain, PanelButtonKind.Outline, PanelButtonSize.Small);
                    write.ToolTip = PanelScreens.InstallAgainTooltip;
                    write.Click += (sender, args) => InstallScreenAgain(screen);
                    return Ui.FixBox(PanelScreens.MissingTitle, PanelAttention.MissingDetail, null, write);
                case ScreenState.Restart:
                    // The card above says this state in the same words: one phrase for one state.
                    return Ui.FixBox(PanelScreens.RestartToLoad, PanelScreens.RestartDetail(screen.Name), null, null, PanelIcons.Restart);
                default:
                    return null;
            }
        }

        /// <summary>The editor of the screen's kind, then its Details; a face lays its Details under the rows of
        /// its own left column, beside the aside.</summary>
        private FrameworkElement BuildScreenEditor(ScreenInstance screen)
        {
            if (!screen.IsCompanion && !screen.IsPitWall && !screen.IsSlots) return BuildFacePane(screen);
            FrameworkElement editor;
            if (screen.IsCompanion) editor = BuildCompanionPane(screen);
            else if (screen.IsPitWall) editor = BuildPitWallPane(screen);
            else editor = BuildSlotsPane(screen);
            var details = BuildScreenDetails(screen);
            details.Margin = new Thickness(0, 12, 0, 0);
            return Ui.VStack(0, editor, details);
        }

        /// <summary>
        /// The screen's names as SimHub and a reader of its properties meet them, folded away: what it is
        /// called, where its dashboard lives, what it publishes, and the dashboard's version.
        /// </summary>
        private FrameworkElement BuildScreenDetails(ScreenInstance screen)
        {
            var details = Ui.Collapsible(PanelScreens.DetailsTitle, null, screensDetailsOpen, () =>
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var rows = new[]
                {
                    new[] { PanelScreens.SimHubNameLabel, screen.Name, null },
                    new[] { PanelScreens.FolderLabel, screen.Folder ?? PanelScreens.NotInstalled, "code" },
                    new[] { PanelScreens.PropertiesLabel, PanelScreens.Properties(screen), "code" },
                    new[] { PanelScreens.VersionLabel, ScreensInstalledVersion(screen), null },
                };
                for (var i = 0; i < rows.Length; i++)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    var label = Ui.Text(rows[i][0], Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);
                    label.Margin = new Thickness(0, i == 0 ? 0 : 6, 0, 0);
                    var value = Ui.Text(rows[i][1] ?? string.Empty, rows[i][2] == null ? Theme.SizeSmall : Theme.SizeLabel, FontWeights.Normal, Theme.TextPrimary,
                        rows[i][2] == null ? null : new FontFamily("Consolas"));
                    value.TextWrapping = TextWrapping.Wrap;
                    value.Margin = label.Margin;
                    Grid.SetRow(label, i);
                    Grid.SetRow(value, i);
                    Grid.SetColumn(value, 1);
                    grid.Children.Add(label);
                    grid.Children.Add(value);
                }
                return grid;
            }, open => screensDetailsOpen = open);
            var block = new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(0, 6, 0, 0),
                Child = details,
            };
            return Ui.Anchor(block, PanelScreens.AnchorDetails);
        }

        /// <summary>The version the screen's dashboard says it is, read when Details is opened: the disk is
        /// asked only then.</summary>
        private string ScreensInstalledVersion(ScreenInstance screen)
        {
            try
            {
                if (screen.Folder == null) return PanelScreens.NotInstalled;
                var root = plugin.Installer.SimHubRoot;
                var exists = PackageExtractor.IsInstalled(root, screen.Folder);
                var sidecar = PackageExtractor.InstalledSidecar(root, screen.Folder);
                var text = exists && File.Exists(sidecar) ? File.ReadAllText(sidecar) : null;
                return DashboardInstaller.InstalledVersionFrom(exists, text) ?? PanelScreens.NotInstalled;
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read the version of " + screen.Folder + ": " + ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// Draws an editor again in place, and puts the keyboard back on the control it was on.
        /// </summary>
        /// <remarks>
        /// An editor redraws itself after a press so the picture says what was just set, and a redraw builds
        /// new controls: without this, a zone picked from the keyboard or a page ticked with Space would
        /// leave focus nowhere. The controls that redraw carry a Uid naming what they set, which is how the
        /// new one is found.
        /// </remarks>
        private static void ScreensRedraw(ContentControl host, Func<object> build)
        {
            string uid = null;
            if (host.IsKeyboardFocusWithin)
            {
                var at = Keyboard.FocusedElement as DependencyObject;
                while (at != null && !ReferenceEquals(at, host))
                {
                    var element = at as UIElement;
                    if (element != null && !string.IsNullOrEmpty(element.Uid))
                    {
                        uid = element.Uid;
                        break;
                    }
                    at = (at is Visual ? VisualTreeHelper.GetParent(at) : null) ?? LogicalTreeHelper.GetParent(at);
                }
            }
            host.Content = build();
            if (uid == null) return;
            host.Dispatcher.BeginInvoke(new Action(() =>
            {
                var target = ScreensFind(host, uid);
                if (target == null) return;
                if (target.Focusable) target.Focus();
                else target.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }), DispatcherPriority.Loaded);
        }

        private static UIElement ScreensFind(DependencyObject root, string uid)
        {
            var element = root as UIElement;
            if (element != null && string.Equals(element.Uid, uid, StringComparison.Ordinal)) return element;
            var count = root is Visual ? VisualTreeHelper.GetChildrenCount(root) : 0;
            for (var i = 0; i < count; i++)
            {
                var found = ScreensFind(VisualTreeHelper.GetChild(root, i), uid);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>A row of the kit at the Screens page's own segmented padding (Screens.dc.html's .seg, 13).</summary>
        private static Segmented ScreensSegmented(string[] values, string[] labels, string selected, Action<string> changed)
        {
            return BuildSegmented(values, labels, selected, changed, Segmented.BarHeight, PanelKit.SegmentedPaddingScreens);
        }

        // --- Adding, editing, duplicating and removing ---------------------------------------------------

        /// <summary>
        /// The add sheet (AddScreen.dc.html): the kind, then the size the kind comes in, then the name, and at
        /// its foot what to do once it is added.
        /// </summary>
        private void ShowAddScreen()
        {
            var catalogue = PackageCatalogue.From(plugin.Installer.PackageSource, new SimHubInstallLog());
            var types = PanelAddScreen.Types(catalogue);
            if (types.Count == 0)
            {
                ShowSheet(PanelAddScreen.SectionTitle, Ui.Prose(PanelAddScreen.NothingToAdd, Theme.SizeBody), null);
                return;
            }

            // The three answers, held here and read by whichever control last wrote one. The name is the only
            // one the driver types, so it is the only one that has to remember whether they have.
            var type = types[0];
            PackageEntry entry = PanelAddScreen.Offered(type)[PanelAddScreen.PreferredIndex(type)];
            var typed = false;

            var name = Ui.Input(string.Empty);
            name.HorizontalAlignment = HorizontalAlignment.Stretch;
            var note = Ui.Prose(string.Empty);
            var nextStep = Ui.Prose(PanelAddScreen.NextStep(string.Empty));
            name.TextChanged += (sender, args) =>
            {
                // Only what the driver types counts as theirs: a default the sheet filled in is not.
                if (name.IsKeyboardFocusWithin) typed = name.Text.Trim().Length > 0;
                nextStep.Text = PanelAddScreen.NextStep(name.Text);
            };

            var kindsHost = new ContentControl { Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            var sizesHost = new ContentControl { Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch };

            Action fillName = () =>
            {
                // Only while the driver has not typed one of their own: a default that overwrites what
                // somebody has just written is worse than no default at all.
                if (typed) return;
                name.Text = PackageCatalogue.UniqueName(PanelAddScreen.DefaultName(entry), Settings.RigScreens().Select(s => s.Name));
            };
            Action refreshNote = () =>
            {
                var second = Settings.RigScreens().Any(s => string.Equals(s.Namespace, StockNamespaceOf(entry), StringComparison.Ordinal));
                note.Text = PanelAddScreen.Note(entry, second);
                note.Visibility = note.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            };
            Action drawSizes = null;
            drawSizes = () =>
            {
                var offered = PanelAddScreen.Offered(type);
                var tiles = new List<UIElement>();
                for (var i = 0; i < offered.Count; i++)
                {
                    var option = offered[i];
                    tiles.Add(Ui.ChoiceTile(BuildSizeTile(type, option, i, ReferenceEquals(option, entry)), ReferenceEquals(option, entry), () =>
                    {
                        entry = option;
                        drawSizes();
                        fillName();
                        refreshNote();
                    }, centred: true));
                }
                sizesHost.Content = Ui.CardGrid(96, 8, 4, tiles.ToArray());
            };
            Action drawKinds = null;
            drawKinds = () =>
            {
                var tiles = new List<UIElement>();
                foreach (var candidate in types)
                {
                    var option = candidate;
                    tiles.Add(Ui.ChoiceTile(BuildKindTile(option.Label, option.Caption, null), ReferenceEquals(option, type), () =>
                    {
                        type = option;
                        entry = PanelAddScreen.Offered(type)[PanelAddScreen.PreferredIndex(type)];
                        drawKinds();
                        drawSizes();
                        fillName();
                        refreshNote();
                    }));
                }
                tiles.Add(Ui.ChoiceTile(BuildKindTile(PanelSoon.FlagsScreen.Title, PanelAddScreen.FlagsScreenCaption, Ui.SoonTag(PanelSoon.FlagsScreen)), false, null, false, PanelSoon.FlagsScreen));
                kindsHost.Content = Ui.CardGrid(140, 8, 3, tiles.ToArray());
            };
            drawKinds();
            drawSizes();
            fillName();
            refreshNote();

            var add = Ui.Button(PanelAddScreen.AddButton, PanelButtonKind.Primary, PanelButtonSize.Large);
            add.MinWidth = ButtonMinWidth;
            add.ToolTip = PanelAddScreen.AddTooltip;
            add.Click += (sender, args) =>
            {
                CloseSheet();
                AddScreen(entry, name.Text);
            };
            var cancel = Ui.Button(PanelAddScreen.CancelButton, PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.ToolTip = PanelAddScreen.CancelTooltip;
            cancel.Click += (sender, args) => CloseSheet();

            var displays = Ui.Soon(BuildDashedLine(PanelSoon.YourDisplays.Title, Ui.SoonTag(PanelSoon.YourDisplays)), PanelSoon.YourDisplays);
            var body = Ui.VStack(0,
                Ui.Step(1, PanelAddScreen.KindStep, kindsHost, first: true),
                Ui.Step(2, PanelAddScreen.SizeStep, Ui.VStack(8, displays, sizesHost)),
                Ui.Step(3, PanelAddScreen.NameStep, Ui.VStack(8, name, note)));
            var footer = Ui.VStack(14, Ui.Eyebrow(PanelAddScreen.NextStepsTitle), nextStep, SheetFooter(null, cancel, add));
            ShowSheet(PanelAddScreen.SectionTitle, body, footer);
        }

        /// <summary>A kind tile's words: its name at 15 SemiBold (and a Soon tag beside it when greyed), and
        /// the note under it at 12.</summary>
        private static FrameworkElement BuildKindTile(string title, string caption, FrameworkElement tag)
        {
            var head = new DockPanel { LastChildFill = true };
            if (tag != null)
            {
                tag.Margin = new Thickness(6, 0, 0, 0);
                DockPanel.SetDock(tag, Dock.Right);
                head.Children.Add(tag);
            }
            var name = Ui.Text(title, 15, FontWeights.SemiBold, Theme.TextPrimary);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            head.Children.Add(name);
            var note = Ui.Prose(caption, Theme.SizeLabel);
            note.Margin = new Thickness(0, 6, 0, 0);
            return Ui.VStack(0, head, note);
        }

        /// <summary>A size tile: the screen's outline in a 44 px band, its size in the display family and the
        /// name the design gives it, if any.</summary>
        private static FrameworkElement BuildSizeTile(ScreenType type, PackageEntry entry, int index, bool selected)
        {
            var shape = PanelAddScreen.TileShape(entry.Width, entry.Height);
            var ink = Ui.Brush(selected ? Theme.Accent : Theme.TextLabel);
            FrameworkElement outline;
            if (entry.Width > 0 && entry.Width == entry.Height)
            {
                outline = new Ellipse { Width = shape[0], Height = shape[1], Stroke = ink, StrokeThickness = 1.5 };
            }
            else
            {
                outline = new Border { Width = shape[0], Height = shape[1], BorderBrush = ink, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(Theme.Radius) };
            }
            outline.HorizontalAlignment = HorizontalAlignment.Center;
            outline.VerticalAlignment = VerticalAlignment.Center;
            var band = new Border { Height = 44, Child = outline };
            var label = Ui.Text(PanelAddScreen.SizeLabel(type, entry, index), 14, FontWeights.SemiBold, Theme.TextPrimary, PanelFonts.Data);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Margin = new Thickness(0, 6, 0, 0);
            var stack = Ui.VStack(0, band, label);
            var hint = PanelAddScreen.SizeHint(type, entry);
            if (!string.IsNullOrEmpty(hint))
            {
                var words = Ui.Text(hint, 11, FontWeights.Normal, Theme.TextSecondary);
                words.HorizontalAlignment = HorizontalAlignment.Center;
                words.Margin = new Thickness(0, 4, 0, 0);
                stack.Children.Add(words);
            }
            return stack;
        }

        /// <summary>A line in a dashed outline, 10 by 12 in: the Add sheet's greyed "Your displays".</summary>
        private static FrameworkElement BuildDashedLine(string text, FrameworkElement tag)
        {
            var dash = new Rectangle
            {
                Stroke = Ui.Brush(Theme.Border),
                StrokeThickness = PanelMetrics.BorderWeight,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                RadiusX = Theme.Radius,
                RadiusY = Theme.Radius,
            };
            var line = new DockPanel { LastChildFill = true, Margin = new Thickness(12, 10, 12, 10) };
            if (tag != null)
            {
                DockPanel.SetDock(tag, Dock.Right);
                line.Children.Add(tag);
            }
            line.Children.Add(Ui.Text(text, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary));
            var host = new Grid();
            host.Children.Add(dash);
            host.Children.Add(line);
            return host;
        }

        /// <summary>The size or the orientation control of the edit sheet: a segmented pair for a way round or
        /// a few sizes, a list for more.</summary>
        private FrameworkElement BuildSizeRow(ScreenType type, IReadOnlyList<PackageEntry> offered, SizeQuestion question, int selected, Action<PackageEntry> chose)
        {
            var labels = offered.Select((e, i) => PanelAddScreen.SizeLabel(type, e, i)).ToArray();
            var opens = selected < 0 || selected >= offered.Count ? 0 : selected;
            FrameworkElement control;
            if (question == SizeQuestion.Orientation || offered.Count <= 3)
            {
                var values = offered.Select((e, i) => i.ToString(CultureInfo.InvariantCulture)).ToArray();
                control = ScreensSegmented(values, labels, values[opens], value =>
                {
                    int index;
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out index)) return;
                    if (index < 0 || index >= offered.Count) return;
                    chose(offered[index]);
                });
            }
            else
            {
                control = Ui.ChoiceButton(labels, opens, index => chose(offered[index]));
            }
            return Ui.SettingRow(question == SizeQuestion.Orientation ? PanelAddScreen.OrientationTitle : PanelAddScreen.SizeTitle, control);
        }

        /// <summary>
        /// The one sheet for a screen that is already on the rig: its name, its size, and its dashboard.
        /// </summary>
        /// <remarks>
        /// The namespace is frozen at creation (ADR 0017) and none of the three moves it, so the settings and
        /// the bindings survive all of them and only the folder in DashTemplates is written.
        /// </remarks>
        private void ShowEdit(ScreenInstance screen)
        {
            var name = Ui.Input(screen.Name, 240);

            // The size question is asked only where this build has another size to offer, which is the same
            // rule the add sheet follows; a kind that ships one package draws no row at all.
            var catalogue = PackageCatalogue.From(plugin.Installer.PackageSource, new SimHubInstallLog());
            var type = PanelAddScreen.Types(catalogue).FirstOrDefault(t => string.Equals(t.Kind, screen.Kind, StringComparison.Ordinal));
            var question = type == null ? SizeQuestion.None : PanelAddScreen.Question(type);
            FrameworkElement sizeRow = null;
            PackageEntry chosen = null;
            if (question != SizeQuestion.None)
            {
                var offered = PanelAddScreen.Offered(type);
                var current = offered.FirstOrDefault(e => e.Width == screen.Width && e.Height == screen.Height) ?? offered[0];
                chosen = current;
                var opensOn = 0;
                for (var i = 0; i < offered.Count; i++)
                {
                    if (ReferenceEquals(offered[i], current)) opensOn = i;
                }
                sizeRow = BuildSizeRow(type, offered, question, opensOn, e => chosen = e);
            }

            var edited = Edited(screen);
            var reinstall = Ui.Button(PanelAddScreen.ReinstallButton, PanelButtonKind.Outline, PanelButtonSize.Small);
            reinstall.MinWidth = ButtonMinWidth;
            reinstall.ToolTip = PanelAddScreen.ReinstallTooltip;
            reinstall.Click += (sender, args) => ReinstallScreen(screen);
            var reinstallRow = Ui.SettingRow(
                PanelAddScreen.ReinstallTitle,
                reinstall,
                edited ? PanelAddScreen.ReinstallEditedCaption : PanelAddScreen.ReinstallCaption);

            var save = Ui.Button(PanelAddScreen.SaveButton, PanelButtonKind.Primary, PanelButtonSize.Large);
            save.MinWidth = ButtonMinWidth;
            save.ToolTip = PanelAddScreen.SaveTooltip;
            save.Click += (sender, args) => SaveEdit(screen, name.Text, chosen);
            var cancel = Ui.Button(PanelAddScreen.CancelButton, PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.ToolTip = PanelAddScreen.EditCancelTooltip;
            cancel.Click += (sender, args) => CloseSheet();

            var rows = new List<UIElement> { Ui.SettingRow(PanelAddScreen.NameTitle, name, PanelAddScreen.NameCaption) };
            if (sizeRow != null) rows.Add(sizeRow);
            rows.Add(reinstallRow);
            ShowSheet(PanelAddScreen.EditTitle + " " + screen.Name, Ui.Rows(rows.ToArray()), SheetFooter(PanelAddScreen.EditCaption, cancel, save));
        }

        /// <summary>
        /// Whether this screen's folder no longer matches what OpenDash last wrote into it, which is to say
        /// whether somebody has opened it in Dash Studio and saved. A folder that cannot be fingerprinted
        /// reads as unedited: the alternative is warning everybody whose disk could not be read.
        /// </summary>
        private bool Edited(ScreenInstance screen)
        {
            try
            {
                if (screen.Folder == null || plugin.Installer.Record == null) return false;
                var recorded = plugin.Installer.Record.Get(screen.Folder);
                if (recorded == null) return false;
                var now = FolderFingerprint.Of(PackageExtractor.InstalledFolder(plugin.Installer.SimHubRoot, screen.Folder));
                return now != null && !string.Equals(now, recorded, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not tell whether " + screen.Folder + " has been edited: " + ex.Message);
                return false;
            }
        }

        /// <summary>Applies whichever of the two answers was changed, and writes the dashboard when either
        /// was: SimHub lists a dashboard under its title, so a rename that stopped at the card left the
        /// screen listed under the name the driver had just stopped using.</summary>
        private void SaveEdit(ScreenInstance screen, string wanted, PackageEntry entry)
        {
            // Whatever was changed, even nothing: pressing Save on a screen's own edit sheet is the driver
            // saying that this one is theirs.
            screen.Keep();
            var sizeChanged = entry != null && (entry.Width != screen.Width || entry.Height != screen.Height);
            switch (PanelAddScreen.Edit(screen.Name, wanted, sizeChanged))
            {
                case ScreenEdit.Resize:
                    // The name first, so the folder ResizeScreen writes is titled with it rather than with the
                    // name the screen is about to stop having.
                    RenameScreen(screen, wanted);
                    ResizeScreen(screen, entry);
                    return;
                case ScreenEdit.Rename:
                    RenameScreen(screen, wanted);
                    Save();
                    var result = plugin.Installer.Write(screen);
                    Save();
                    plugin.Installer.Refresh();
                    Select(PanelPage.Screens, screen.Namespace);
                    Redraw();
                    Say(result.Ok ? PanelAddScreen.Renamed(screen.Name) : PanelAddScreen.RenameFailed(screen.Name, result.Error), result.Ok);
                    return;
                default:
                    Save();
                    Redraw();
                    return;
            }
        }

        /// <summary>Takes the name from the box, kept distinct from every other screen's. An empty box keeps
        /// the name it had, which is what PanelAddScreen.Edit has already decided.</summary>
        private void RenameScreen(ScreenInstance screen, string wanted)
        {
            var trimmed = (wanted ?? string.Empty).Trim();
            if (trimmed.Length == 0 || string.Equals(trimmed, screen.Name, StringComparison.Ordinal)) return;
            screen.Name = PackageCatalogue.UniqueName(
                trimmed,
                Settings.RigScreens().Where(s => !ReferenceEquals(s, screen)).Select(s => s.Name));
        }

        /// <summary>Writes this one screen's dashboard again, at the name and size it already has: the repair
        /// for a dashboard that is there but wrong.</summary>
        private void ReinstallScreen(ScreenInstance screen)
        {
            var result = plugin.Installer.Write(screen);
            Save(screen);
            plugin.Installer.Refresh();
            Select(PanelPage.Screens, screen.Namespace);
            Redraw();
            Say(result.Ok ? PanelAddScreen.Reinstalled(screen.Name) : PanelAddScreen.ReinstallFailed(screen.Name, result.Error), result.Ok);
        }

        private void ResizeScreen(ScreenInstance screen, PackageEntry entry)
        {
            if (entry == null || (entry.Width == screen.Width && entry.Height == screen.Height))
            {
                Redraw();
                return;
            }
            var log = new SimHubInstallLog();
            // The old folder first: a screen that was the stock one at its old size owns that package's own
            // folder, and leaving it behind would put a dashboard in SimHub's list that nothing answers for.
            var old = ScreenInstaller.Remove(screen, plugin.Installer.SimHubRoot, log);
            if (!old.Ok) Log.Warn("The old folder of " + screen.Name + " could not be removed: " + old.Error);

            Settings.ResizeScreen(screen, entry);
            Save();
            var result = plugin.Installer.Write(screen);
            Save();
            plugin.Installer.Refresh();
            Select(PanelPage.Screens, screen.Namespace);
            Redraw();
            Say(result.Ok ? PanelAddScreen.Resized(screen.Name, screen.SizeLabel, screen.Name) : PanelAddScreen.ResizeFailed(screen.Name, result.Error), result.Ok);
        }

        private static string StockNamespaceOf(PackageEntry entry)
        {
            var probe = new ScreenInstance { Kind = entry.Kind, Width = entry.Width, Height = entry.Height, Folder = entry.Folder };
            return probe.StockNamespace;
        }

        private void AddScreen(PackageEntry entry, string name)
        {
            var screen = Settings.AddScreen(entry, name);
            Save();
            var result = plugin.Installer.Write(screen);
            Save();
            plugin.Installer.Refresh();
            Select(PanelPage.Screens, screen.Namespace);
            Redraw();

            // Said at the moment it becomes true rather than left to be found: SimHub reads its template list
            // once, at startup, and assigning a dashboard to a display is in another part of SimHub entirely.
            // The dashboard is listed under its title, which is the name the driver just chose.
            Say(result.Ok ? PanelAddScreen.Added(screen.Name, screen.Name) : PanelAddScreen.AddFailed(screen.Name, result.Error), result.Ok);
        }

        /// <summary>
        /// A second screen set up like this one: its settings copied, its bindings not (they are bound to the
        /// source's actions), under a name, a namespace and a folder of its own.
        /// </summary>
        private void DuplicateScreen(ScreenInstance screen)
        {
            var catalogue = PackageCatalogue.From(plugin.Installer.PackageSource, new SimHubInstallLog());
            var copy = Settings.DuplicateScreen(screen.Namespace, catalogue);
            if (copy == null)
            {
                Log.Warn("Duplicating " + screen.Name + " made nothing: no package in this build makes " + (screen.Folder ?? screen.Kind) + ".");
                Say(PanelMessage.Caution(PanelAddScreen.DuplicateFailed(screen.Name)));
                return;
            }
            Save();
            var result = plugin.Installer.Write(copy);
            Save();
            plugin.Installer.Refresh();
            Select(PanelPage.Screens, copy.Namespace);
            Redraw();
            Say(result.Ok ? PanelAddScreen.Added(copy.Name, copy.Name) : PanelAddScreen.AddFailed(copy.Name, result.Error), result.Ok);
        }

        /// <summary>
        /// The remove sheet, which says the two things that are easy to miss: the folder goes, and a wheel
        /// button bound to this screen's actions stops doing anything, because the action is no longer
        /// registered. ADR 0017 accepts that cost and this is where it is paid.
        /// </summary>
        private void ShowRemove(ScreenInstance screen)
        {
            var remove = Ui.Button(PanelScreens.RemoveItButton, PanelButtonKind.Danger, PanelButtonSize.Large);
            remove.ToolTip = PanelScreens.RemoveTooltip;
            remove.Click += (sender, args) =>
            {
                var result = ScreenInstaller.Remove(screen, plugin.Installer.SimHubRoot, new SimHubInstallLog());
                Settings.RemoveScreen(screen.Namespace);
                Save();
                plugin.Installer.Refresh();
                Select(PanelPage.Screens, null);
                Redraw();
                Say(result.Ok ? PanelScreens.Removed(screen.Name) : PanelScreens.RemoveFailed(screen.Name, result.Error), result.Ok);
            };
            var keep = Ui.Button(PanelScreens.KeepButton, PanelButtonKind.Ghost, PanelButtonSize.Large);
            keep.ToolTip = PanelScreens.KeepTooltip;
            // The answer to the question the line over the cards asks of a migrated screen, as much as Remove
            // it is, so it keeps the screen as well as going back.
            keep.Click += (sender, args) =>
            {
                Save(screen);
                Redraw();
            };
            ShowSheet(PanelScreens.RemoveTitle(screen.Name), Ui.Prose(PanelScreens.RemoveBody(PanelScreens.HasActions(screen)), Theme.SizeBody),
                SheetFooter(null, keep, remove));
        }
    }
}
