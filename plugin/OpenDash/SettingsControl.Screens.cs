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

        /// <summary>Whether the page has been built since the last Go: a route's anchor is followed by the first
        /// build after a Go and never by a rebuild in place, which keeps the same route. Cleared by the
        /// "Screens.follow" leave action, which the shell runs on every Go before it builds.</summary>
        private bool screensBuiltSinceGo;

        private FrameworkElement BuildScreensPage(PanelRoute to)
        {
            var rig = Settings.RigScreens();
            // The dashboards' versions are read again for this build, and not for an editor's redraw in it.
            screensVersions.Clear();
            // A route to a row opens a screen that draws it, and the zone and list state the row needs, before
            // the page is built: the shell scrolls to the anchor once it is. Only on the way in, never on a
            // rebuild, which keeps the same route and would take the driver back to it after every press. The
            // way in is told through OnLeave, which runs on Go only, before the build.
            if (!screensBuiltSinceGo)
            {
                screensBuiltSinceGo = true;
                OnLeave("Screens.follow", () => screensBuiltSinceGo = false);
                if (to != null && to.Anchor != null) ScreensFollow(to.Anchor, rig);
                if (to != null && to.Anchor == PanelScreens.AnchorAdd) ScreensOpenAddOnArrival();
            }
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
            // Read up to BodyWidth, where the preview stops growing: a wider window does not reload it. Less the
            // preview's own 1 px frame each side, which ScreenPreview draws round the width it is given: fitted to
            // the whole column, the frame stood 2 px past it and the page's clip cut its right edge.
            var preview = BuildScreenPreview(screen, ContentWidthUpTo(BodyWidth) - 2 * PanelMetrics.BorderWeight);
            if (preview != null) selected.Add(preview);
            selected.Add(BuildScreenEditor(screen));

            var block = new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(0, PanelScreens.SelectedTop, 0, 0),
                Child = Ui.VStack(PanelScreens.SelectedGap, selected.ToArray()),
            };
            sections.Add(block);
            return PageLayout(PanelScreens.Title, null, sections.ToArray());
        }

        /// <summary>
        /// Opens the Add sheet for a route to the add tile (PanelScreens.AnchorAdd), once the page it arrives on
        /// is drawn: Home's empty-rig tile, Rig's empty canvas and a search for "Add a screen" go there (#792).
        /// </summary>
        /// <remarks>
        /// Go puts focus on the page's first control at Loaded. The sheet opens after that, at Input, so its own
        /// focus (queued at Normal as it opens) lands last and inside the sheet, and the opener it remembers is a
        /// control still on screen. Input is a background priority, run only when no input is pending, so a
        /// sidebar press made while the page was building is handled first: the sheet belongs to the build it
        /// follows, through that build's own OnDrop, and does not open over whatever page the press went to.
        /// </remarks>
        private void ScreensOpenAddOnArrival()
        {
            var dropped = false;
            OnDrop(() => dropped = true);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!dropped) ShowAddScreen();
            }), DispatcherPriority.Input);
        }

        /// <summary>Selects the screen a route to <paramref name="anchor"/> needs, and on a face opens the
        /// zone, and the whole list, the row is drawn in.</summary>
        private void ScreensFollow(string anchor, IReadOnlyList<ScreenInstance> rig)
        {
            var current = SelectedScreen;
            var screen = PanelScreens.ScreenFor(anchor, rig, current);
            if (screen == null) return;
            if (!ReferenceEquals(screen, current)) Select(PanelPage.Screens, screen.Namespace);
            var face = screen.FaceSize;
            if (face == null) return;
            string picked;
            screensFaceAside.TryGetValue(screen.Namespace, out picked);
            var aside = PanelScreens.AsideFor(anchor, picked, face.Value);
            if (aside != null && !string.Equals(aside, picked, StringComparison.Ordinal)) screensFaceAside[screen.Namespace] = aside;
            if (PanelScreens.ShowsEveryPage(anchor)) screensShowAll = true;
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
                var thumb = Ui.Thumb(PanelScreens.ThumbKind(captured), captured.Width, captured.Height);
                var card = Ui.DeviceCard(
                    thumb,
                    captured.Name,
                    PanelScreens.CardMeta(captured),
                    PanelScreens.StateLabel(state),
                    PanelScreens.StateHex(state),
                    current != null && ReferenceEquals(current, captured),
                    () =>
                    {
                        // A selection, not a change to the rig: nothing needs asking again, and the line a
                        // press left (Added Rim. Restart SimHub...) stays.
                        Select(PanelPage.Screens, captured.Namespace);
                        RebuildPage();
                    });
                // The kit trims a long name with no hover, and only the selected screen's is read whole in the
                // header: the card's hover says it where it is cut. The state's own hover is the innermost.
                ScreensHoverWhenCut(card, captured.Name, () => ScreensTextIn(card, captured.Name));
                cards.Add(card);
            }
            cards.Add(Ui.Anchor(Ui.DashedAddCard(PanelAddScreen.SectionTitle, ShowAddScreen), PanelScreens.AnchorAdd));
            var grid = Ui.CardGrid(PanelKit.CardMinWidth, PanelKit.CardGridGap, PanelScreens.CardColumns, cards.ToArray());
            if (rig.Count > 0) return grid;
            // The empty state names the emptiness over the tile that ends it, as LEDs and Matrix draw theirs; Home's
            // card and the Updates table read the same words.
            return Ui.VStack(12, Ui.Prose(PanelScreens.NoScreens, Theme.SizeBody), grid);
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
            title.TextWrapping = TextWrapping.NoWrap;
            title.ToolTip = screen.Name;
            var facts = Ui.Prose(PanelScreens.Facts(screen));
            facts.TextWrapping = TextWrapping.NoWrap;
            facts.VerticalAlignment = VerticalAlignment.Bottom;
            facts.Margin = new Thickness(0, 0, 0, 3);
            var name = new ScreensNameLine(PanelScreens.HeaderGap) { VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(title);
            name.Children.Add(facts);

            var edit = Ui.Button(PanelScreens.EditButton, PanelButtonKind.Outline, PanelButtonSize.Small);
            edit.ToolTip = PanelScreens.EditTooltip;
            edit.Click += (sender, args) => ShowEdit(screen);
            // New in this release, and tagged so for one (Screens.dc.html).
            var duplicate = Ui.Button(PanelScreens.DuplicateButton, PanelButtonKind.Outline, PanelButtonSize.Small);
            duplicate.Content = Ui.HStack(8, Ui.Text(PanelScreens.DuplicateButton, Theme.SizeSmall, FontWeights.Medium, Theme.TextPrimary), Ui.NewTag());
            duplicate.ToolTip = PanelScreens.DuplicateTooltip;
            duplicate.Click += (sender, args) => DuplicateScreen(screen);
            var remove = Ui.Button(PanelScreens.RemoveButton, PanelButtonKind.GhostDanger, PanelButtonSize.Small);
            remove.ToolTip = PanelScreens.RemoveTooltipFor(screen);
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
        /// A screen's name and the facts after it, the facts always whole: the name is measured at what the
        /// line leaves it, so a long one is cut short with an ellipsis rather than pushing the facts out of
        /// sight. Ui.HStack measures every child unbounded, so a name there never trimmed.
        /// </summary>
        private sealed class ScreensNameLine : Panel
        {
            private readonly double gap;

            public ScreensNameLine(double gap)
            {
                this.gap = gap;
            }

            protected override Size MeasureOverride(Size available)
            {
                if (InternalChildren.Count < 2) return new Size(0, 0);
                var title = InternalChildren[0];
                var facts = InternalChildren[1];
                facts.Measure(new Size(double.PositiveInfinity, available.Height));
                var room = double.IsInfinity(available.Width) ? double.PositiveInfinity : Math.Max(0, available.Width - facts.DesiredSize.Width - gap);
                title.Measure(new Size(room, available.Height));
                return new Size(title.DesiredSize.Width + gap + facts.DesiredSize.Width, Math.Max(title.DesiredSize.Height, facts.DesiredSize.Height));
            }

            protected override Size ArrangeOverride(Size final)
            {
                if (InternalChildren.Count < 2) return final;
                var title = InternalChildren[0];
                var facts = InternalChildren[1];
                var titleWidth = Math.Min(title.DesiredSize.Width, Math.Max(0, final.Width - facts.DesiredSize.Width - gap));
                title.Arrange(new Rect(0, 0, titleWidth, final.Height));
                facts.Arrange(new Rect(titleWidth + gap, 0, facts.DesiredSize.Width, final.Height));
                return final;
            }
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
                    return Ui.FixBox(PanelScreens.MissingTitle, PanelScreens.MissingDetailFor(screen), null, write);
                case ScreenState.Restart:
                    // The card above says this state in the same words: one phrase for one state.
                    return Ui.FixBox(PanelCopy.RestartToLoad, PanelScreens.RestartDetail(screen.Name), null, null, PanelIcons.Restart);
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
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelScreens.DetailsLabelWidth) });
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
            details.Uid = "screens.details";
            var block = new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(0, 6, 0, 0),
                Child = details,
            };
            return Ui.Anchor(block, PanelScreens.AnchorDetails);
        }

        /// <summary>The versions Details has read in this build of the page, by namespace.</summary>
        private readonly Dictionary<string, string> screensVersions = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// The version the screen's dashboard says it is, read from its sidecar the first time Details is
        /// drawn open in a build of the page and kept for the rest of it.
        /// </summary>
        /// <remarks>
        /// A face draws Details inside its editor, which redraws itself after every tick, drag and pick while
        /// Details is open; without the cache each of those asked the disk again on SimHub's UI thread. A
        /// page build -- Go, Redraw, a card pressed -- reads it afresh.
        /// </remarks>
        private string ScreensInstalledVersion(ScreenInstance screen)
        {
            string version;
            if (screensVersions.TryGetValue(screen.Namespace ?? string.Empty, out version)) return version;
            version = ScreensReadVersion(screen);
            screensVersions[screen.Namespace ?? string.Empty] = version;
            return version;
        }

        private string ScreensReadVersion(ScreenInstance screen)
        {
            try
            {
                if (screen.Folder == null) return PanelScreens.NotInstalled;
                var root = plugin.Installer.SimHubRoot;
                var exists = PackageExtractor.IsInstalled(root, screen.Folder);
                var sidecar = PackageExtractor.InstalledSidecar(root, screen.Folder);
                var text = exists && File.Exists(sidecar) ? File.ReadAllText(sidecar) : null;
                return PanelScreens.VersionShown(DashboardInstaller.InstalledVersionFrom(exists, text));
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read the version of " + screen.Folder + ": " + ex.Message);
                return PanelScreens.VersionUnknown;
            }
        }

        /// <summary>
        /// Saves a change made in a screen's editor, which keeps the screen, then draws the editor again
        /// through <paramref name="redraw"/> where it has one.
        /// </summary>
        /// <remarks>
        /// Keeping a screen the migration made can end the line over the cards, the sidebar's warning and
        /// Home's issue, none of which an editor's in-place redraw reaches; ADR 0017 has the line go when it
        /// stops being true. So the first change to such a screen (PanelScreens.RebuildsPageAfterSave) also
        /// asks what needs fixing again and rebuilds the page and the sidebar, through ScreensRefreshAfterKeep.
        /// The editor itself redraws in place first, which keeps the keyboard where it was.
        /// </remarks>
        private void ScreensSave(ScreenInstance screen, Action redraw = null)
        {
            var rebuilds = PanelScreens.RebuildsPageAfterSave(screen);
            Save(screen);
            if (redraw != null) redraw();
            if (!rebuilds) return;
            ScreensRefreshAfterKeep();
        }

        /// <summary>Counts the page refreshes a keep has asked for: a refresh runs only while it is the last
        /// one asked for, and a Go counts one more, so a refresh still waiting when the page is left never runs.</summary>
        private int screensKeepRefresh;

        /// <summary>
        /// Asks what needs fixing again and rebuilds the page and the sidebar, once nothing is pressed or
        /// holding the mouse, none of this page's sheets is open, and only while the panel is on screen and
        /// Screens is the page shown.
        /// </summary>
        /// <remarks>
        /// A save runs inside the event that raised it, and the web view box saves on LostFocus, which a
        /// button raises when it takes focus in its mouse down, before its mouse up. Work posted at Background
        /// runs whenever no input is queued, which is the case while a driver still holds the button: the
        /// rebuild then took the pressed control out of the tree mid-press, its capture went, and the release
        /// landed on a new copy that never saw the press, so Edit opened no sheet and a sidebar item did not
        /// navigate. So the refresh waits for the left button to be up, then is posted again, behind the
        /// Click the release raised in the same input.
        ///
        /// It also waits while anything holds the mouse capture. A choice button's list is a Popup that does
        /// not stay open, and such a Popup holds the capture while it is open: the release that ended the press
        /// can be the one that opened it, and a rebuild then detached its toggle and WPF closed the list under
        /// the driver's pointer. The wait re-checks after every input, so the refresh runs once the list closes.
        ///
        /// And it waits for a sheet this page opened to close, since a rebuild under a sheet detaches the
        /// control the sheet gives focus back to. The page knows its own sheets through ShowSheet's closed
        /// callback (ScreensShowSheet), not through the shell's sheet layer, which is not a hook.
        ///
        /// A refresh that lands after the panel was left is dropped, as the shell's own settles are: rebuilt
        /// off screen, Screens would start a live preview no Unloaded would ever dispose. Nothing is lost by
        /// dropping it: the screen is already kept, and a Go or the return to the panel asks again and rebuilds.
        /// </remarks>
        private void ScreensRefreshAfterKeep()
        {
            var ticket = ++screensKeepRefresh;
            OnLeave("Screens.keepRefresh", () => screensKeepRefresh++);
            ScreensRefreshWhenFree(ticket);
        }

        /// <summary>Whether the mouse is in the middle of something: its left button down, or its capture held
        /// by a press, a drag or an open list.</summary>
        private static bool ScreensMouseBusy
        {
            get { return Mouse.LeftButton == MouseButtonState.Pressed || Mouse.Captured != null; }
        }

        private void ScreensRefreshWhenFree(int ticket)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ticket != screensKeepRefresh || !IsLoaded) return;
                if (ScreensMouseBusy)
                {
                    // Once the release, or the close that let the capture go, has been processed, the Click it
                    // raised included.
                    ProcessInputEventHandler released = null;
                    released = (sender, args) =>
                    {
                        if (ScreensMouseBusy) return;
                        InputManager.Current.PostProcessInput -= released;
                        ScreensRefreshWhenFree(ticket);
                    };
                    InputManager.Current.PostProcessInput += released;
                    return;
                }
                if (screensSheetOpen)
                {
                    screensAfterSheet = () => ScreensRefreshWhenFree(ticket);
                    return;
                }
                RefreshAttention();
                RebuildPage();
                RefreshSidebar();
            }), DispatcherPriority.Background);
        }

        /// <summary>Whether a sheet this page opened is open: set once ShowSheet has returned, cleared by the
        /// sheet's closed callback, however it closes.</summary>
        private bool screensSheetOpen;

        /// <summary>What waits for this page's sheet to close: a refresh a keep asked for while it was open.</summary>
        private Action screensAfterSheet;

        /// <summary>
        /// Opens one of this page's sheets through the shell's ShowSheet, and tracks it through its closed
        /// callback.
        /// </summary>
        /// <remarks>
        /// The flag is set after ShowSheet returns: a sheet replacing another runs the first one's closed
        /// inside ShowSheet, which clears the flag and lets a waiting refresh post itself; set before the call,
        /// the flag would be cleared with the new sheet still open. The posted refresh runs after this returns
        /// and finds the flag set again, so it waits for the new sheet in turn.
        /// </remarks>
        private void ScreensShowSheet(string title, UIElement body, UIElement footer)
        {
            ShowSheet(title, body, footer, ScreensSheetClosed);
            screensSheetOpen = true;
        }

        private void ScreensSheetClosed()
        {
            screensSheetOpen = false;
            var after = screensAfterSheet;
            screensAfterSheet = null;
            if (after != null) after();
        }

        /// <summary>
        /// Draws an editor again in place, and puts the keyboard back on the control it was on.
        /// </summary>
        /// <remarks>
        /// An editor redraws itself after a press so the picture says what was just set, and a redraw builds
        /// new controls: without this, a zone picked from the keyboard or a page ticked with Space would
        /// leave focus nowhere, which WPF answers by moving it out of the panel. The controls that redraw
        /// carry a Uid naming what they set, which is how the new one is found.
        ///
        /// The control can be gone from the new drawing: a page unticked under Only ticked leaves the list.
        /// The keyboard then goes to the control that took its place -- the next one the old drawing named
        /// that is still drawn, else the one before it -- and never stays on a control that was removed.
        ///
        /// A focused control with no Uid, which a redraw can catch when a press that takes no focus (a
        /// click on a zone page's name, a drag of its grip) redraws around it, is found again at the same
        /// place in the new drawing, which a redraw draws in the same shape, and the host's first control
        /// takes the keyboard when that place is gone.
        /// </remarks>
        private static void ScreensRedraw(ContentControl host, Func<object> build)
        {
            object drawn;
            string uid = null;
            List<string> named = null;
            List<int> place = null;
            var focused = host.IsKeyboardFocusWithin;
            if (focused)
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
                if (uid != null)
                {
                    named = new List<string>();
                    ScreensUids(host, named);
                }
                else
                {
                    place = ScreensFocusPath(host, Keyboard.FocusedElement as DependencyObject);
                }
            }
            // Outside the shell's BuildPage, whose guard draws PanelShell.PageFailed for a page that throws: an
            // editor redrawn after a press would otherwise throw into SimHub's dispatcher and leave the old
            // controls answering on screen.
            try
            {
                drawn = build();
            }
            catch (Exception ex)
            {
                Log.Error("Drawing a Screens editor failed", ex);
                drawn = Ui.Prose(PanelShell.PageFailed, Theme.SizeBody, Theme.Caution);
            }
            host.Content = drawn;
            if (!focused) return;
            host.Dispatcher.BeginInvoke(new Action(() =>
            {
                UIElement target = null;
                if (uid != null)
                {
                    target = ScreensFind(host, uid);
                    var at = named.IndexOf(uid);
                    for (var i = at + 1; target == null && at >= 0 && i < named.Count; i++) target = ScreensFind(host, named[i]);
                    for (var i = at - 1; target == null && i >= 0; i--) target = ScreensFind(host, named[i]);
                }
                else if (place != null)
                {
                    target = ScreensAt(host, place);
                }
                if (target == null)
                {
                    host.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
                    return;
                }
                if (target.Focusable) target.Focus();
                else target.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Where a focused control sits under <paramref name="root"/>, as the child index at each level of the
        /// visual tree, or null where it is not under it: the page's own walk, so a redraw does not reach into
        /// the shell's internals.
        /// </summary>
        private static List<int> ScreensFocusPath(DependencyObject root, DependencyObject focused)
        {
            if (root == null || focused == null) return null;
            var path = new List<int>();
            var node = focused;
            while (node != null && node != root)
            {
                var parent = node is Visual ? VisualTreeHelper.GetParent(node) : null;
                if (parent == null) return null;
                var index = -1;
                var count = VisualTreeHelper.GetChildrenCount(parent);
                for (var i = 0; i < count; i++)
                {
                    if (VisualTreeHelper.GetChild(parent, i) == node)
                    {
                        index = i;
                        break;
                    }
                }
                if (index < 0) return null;
                path.Insert(0, index);
                node = parent;
            }
            return node == root ? path : null;
        }

        /// <summary>The deepest control that can take the keyboard along <paramref name="path"/>
        /// (ScreensFocusPath's child indexes) under <paramref name="root"/>, or null where the path leads to
        /// none.</summary>
        private static UIElement ScreensAt(DependencyObject root, List<int> path)
        {
            DependencyObject node = root;
            UIElement last = null;
            foreach (var index in path)
            {
                if (node == null || index >= VisualTreeHelper.GetChildrenCount(node)) break;
                node = VisualTreeHelper.GetChild(node, index);
                var element = node as UIElement;
                if (element != null && element.Focusable && element.IsVisible && element.IsEnabled) last = element;
            }
            return last;
        }

        /// <summary>Every Uid under <paramref name="root"/>, in the order the drawing reads.</summary>
        private static void ScreensUids(DependencyObject root, List<string> into)
        {
            var element = root as UIElement;
            if (element != null && !string.IsNullOrEmpty(element.Uid) && !into.Contains(element.Uid)) into.Add(element.Uid);
            var count = root is Visual ? VisualTreeHelper.GetChildrenCount(root) : 0;
            for (var i = 0; i < count; i++) ScreensUids(VisualTreeHelper.GetChild(root, i), into);
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
                ScreensShowSheet(PanelAddScreen.SectionTitle, Ui.Prose(PanelAddScreen.NothingToAdd, Theme.SizeBody), null);
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
            var nextStep = Ui.Prose(string.Empty);
            // The foot names what Add will call the screen, which is not always what the box says.
            Action refreshStep = () => nextStep.Text = PanelAddScreen.NextStep(PanelAddScreen.NameFor(name.Text, entry, Settings.RigScreens().Select(s => s.Name)));
            name.TextChanged += (sender, args) =>
            {
                // Only what the driver types counts as theirs: a default the sheet filled in is not.
                typed = PanelAddScreen.Typed(typed, name.IsKeyboardFocusWithin, name.Text);
                refreshStep();
            };

            var kindsHost = new ContentControl { Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            var sizesHost = new ContentControl { Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch };

            Action fillName = () =>
            {
                // Only while the driver has not typed one of their own: a default that overwrites what
                // somebody has just written is worse than no default at all.
                name.Text = PanelAddScreen.FilledName(name.Text, typed, entry, Settings.RigScreens().Select(s => s.Name));
                refreshStep();
            };
            Action refreshNote = () =>
            {
                var second = PanelAddScreen.SettingsTaken(entry, Settings.RigScreens());
                note.Text = PanelAddScreen.Note(type, entry, second);
                note.Visibility = note.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            };
            TextBlock sizeTitle = null;
            // A pick redraws the grid its tile sits in while that tile has the keyboard, so both grids are drawn
            // through ScreensRedraw and every tile carries a Uid: otherwise the focused tile leaves the tree,
            // WPF moves focus out of the panel, and Tab stops cycling in the sheet and Escape stops closing it.
            Action drawSizes = null;
            drawSizes = () => ScreensRedraw(sizesHost, () =>
            {
                var offered = PanelAddScreen.Offered(type);
                var tiles = new List<UIElement>();
                for (var i = 0; i < offered.Count; i++)
                {
                    var option = offered[i];
                    var tile = Ui.ChoiceTile(BuildSizeTile(type, option, i, ReferenceEquals(option, entry)), ReferenceEquals(option, entry), () =>
                    {
                        entry = option;
                        drawSizes();
                        fillName();
                        refreshNote();
                    }, centred: true);
                    tile.Uid = "screens.add.size." + i.ToString(CultureInfo.InvariantCulture);
                    tiles.Add(tile);
                }
                return Ui.CardGrid(PanelAddScreen.SizeTileLeast, PanelAddScreen.TileGap, PanelAddScreen.SizeColumns, tiles.ToArray());
            });
            Action drawKinds = null;
            drawKinds = () => ScreensRedraw(kindsHost, () =>
            {
                var tiles = new List<UIElement>();
                foreach (var candidate in types)
                {
                    var option = candidate;
                    var tile = Ui.ChoiceTile(BuildKindTile(option.Label, option.Caption, null), ReferenceEquals(option, type), () =>
                    {
                        type = option;
                        entry = PanelAddScreen.Offered(type)[PanelAddScreen.PreferredIndex(type)];
                        if (sizeTitle != null) sizeTitle.Text = PanelAddScreen.SizeStepTitle(type);
                        drawKinds();
                        drawSizes();
                        fillName();
                        refreshNote();
                    });
                    tile.Uid = "screens.add.kind." + option.Kind;
                    tiles.Add(tile);
                }
                tiles.Add(Ui.ChoiceTile(BuildKindTile(PanelSoon.FlagsScreen.Title, PanelAddScreen.FlagsScreenCaption, Ui.SoonTag(PanelSoon.FlagsScreen)), false, null, false, PanelSoon.FlagsScreen));
                return Ui.CardGrid(PanelAddScreen.KindTileLeast, PanelAddScreen.TileGap, PanelAddScreen.KindColumns, tiles.ToArray());
            });
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
            var sizeStep = Ui.Step(2, PanelAddScreen.SizeStepTitle(type), Ui.VStack(8, displays, sizesHost));
            // The step's title is the kit's text beside its number ring, and follows the kind: "Orientation"
            // over a way round, as the edit sheet asks the same question, "Size" otherwise.
            sizeTitle = ScreensStepTitle(sizeStep);
            var body = Ui.VStack(0,
                Ui.Step(1, PanelAddScreen.KindStep, kindsHost, first: true),
                sizeStep,
                Ui.Step(3, PanelAddScreen.NameStep, Ui.VStack(8, name, note)));
            var footer = Ui.VStack(14, Ui.Eyebrow(PanelAddScreen.NextStepsTitle), nextStep, SheetFooter(null, cancel, add));
            ScreensShowSheet(PanelAddScreen.SectionTitle, body, footer);
        }

        /// <summary>The title of a step Ui.Step drew: the text beside its number ring.</summary>
        private static TextBlock ScreensStepTitle(Border step)
        {
            var stack = step == null ? null : step.Child as Panel;
            var head = stack == null || stack.Children.Count == 0 ? null : stack.Children[0] as Panel;
            return head == null ? null : head.Children.OfType<TextBlock>().LastOrDefault();
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
            var name = Ui.Text(title, PanelAddScreen.KindTitleSize, FontWeights.SemiBold, Theme.TextPrimary);
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
            var band = new Border { Height = PanelAddScreen.SizeBand, Child = outline };
            var label = Ui.Text(PanelAddScreen.SizeLabel(type, entry, index), PanelAddScreen.SizeLabelSize, FontWeights.SemiBold, Theme.TextPrimary, PanelFonts.Data);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Margin = new Thickness(0, 6, 0, 0);
            var stack = Ui.VStack(0, band, label);
            var hint = PanelAddScreen.SizeHint(type, entry);
            if (!string.IsNullOrEmpty(hint))
            {
                var words = Ui.Text(hint, PanelAddScreen.SizeHintSize, FontWeights.Normal, Theme.TextSecondary);
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
        /// a few sizes, a list for more. <paramref name="choices"/> is PanelAddScreen.EditSizes: a null entry is
        /// the screen's own size where no package offers it, drawn as that size and chosen as no resize.</summary>
        private FrameworkElement BuildSizeRow(ScreenType type, ScreenInstance screen, IReadOnlyList<PackageEntry> choices, SizeQuestion question, Action<PackageEntry> chose)
        {
            var offset = choices.Count > 0 && choices[0] == null ? 1 : 0;
            var labels = choices.Select((e, i) => e == null ? screen.SizeLabel : PanelAddScreen.SizeLabel(type, e, i - offset)).ToArray();
            var opens = offset == 1 ? 0 : Math.Max(0, PanelAddScreen.OpensOn(choices, screen.Width, screen.Height));
            FrameworkElement control;
            if (question == SizeQuestion.Orientation || choices.Count <= 3)
            {
                var values = choices.Select((e, i) => i.ToString(CultureInfo.InvariantCulture)).ToArray();
                control = ScreensSegmented(values, labels, values[opens], value =>
                {
                    int index;
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out index)) return;
                    if (index < 0 || index >= choices.Count) return;
                    chose(choices[index]);
                });
            }
            else
            {
                control = Ui.ChoiceButton(labels, opens, index => chose(choices[index]));
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
                // Nothing is chosen until a size is picked: a screen at a size no package offers opens on its own
                // size, and Save keeps it (PanelAddScreen.Resizes).
                var choices = PanelAddScreen.EditSizes(PanelAddScreen.Offered(type), screen.Width, screen.Height);
                sizeRow = BuildSizeRow(type, screen, choices, question, e => chosen = e);
            }

            var edited = Edited(screen);
            var reinstall = Ui.Button(PanelAddScreen.ReinstallButton, PanelButtonKind.Outline, PanelButtonSize.Small);
            reinstall.MinWidth = ButtonMinWidth;
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
            ScreensShowSheet(PanelAddScreen.EditSheetTitle(screen.Name), Ui.Rows(rows.ToArray()), SheetFooter(PanelAddScreen.EditCaptionFor(screen), cancel, save));
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
            var sizeChanged = PanelAddScreen.Resizes(entry, screen.Width, screen.Height);
            var edit = PanelAddScreen.Edit(screen.Name, wanted, sizeChanged);
            // An edit that writes the dashboard is refused whole while another writer runs, the sheet left open.
            if (edit != ScreenEdit.None && WriteRefused()) return;
            // Whatever was changed, even nothing: pressing Save on a screen's own edit sheet is the driver
            // saying that this one is theirs.
            screen.Keep();
            switch (edit)
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
                    WriteScreenThen(() => plugin.Installer.Write(screen), result =>
                    {
                        Save();
                        Select(PanelPage.Screens, screen.Namespace);
                        Redraw();
                        if (!result.Ok) Log.Warn("Writing " + screen.Name + " after a rename failed: " + result.Error);
                        Say(result.Ok ? PanelAddScreen.Renamed(screen.Name) : PanelAddScreen.RenameFailed(screen.Name), result.Ok);
                    });
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
            if (WriteRefused()) return;
            WriteScreenThen(() => plugin.Installer.Write(screen), result =>
            {
                Save(screen);
                Select(PanelPage.Screens, screen.Namespace);
                Redraw();
                if (!result.Ok) Log.Warn("Reinstalling " + screen.Name + " failed: " + result.Error);
                Say(result.Ok ? PanelAddScreen.Reinstalled(screen.Name) : PanelAddScreen.ReinstallFailed(screen.Name, result.Error), result.Ok);
            });
        }

        private void ResizeScreen(ScreenInstance screen, PackageEntry entry)
        {
            if (entry == null || (entry.Width == screen.Width && entry.Height == screen.Height))
            {
                Redraw();
                return;
            }
            if (WriteRefused()) return;
            var log = new SimHubInstallLog();
            // The old folder first: a screen that was the stock one at its old size owns that package's own
            // folder, and leaving it behind would put a dashboard in SimHub's list that nothing answers for. Removed
            // on the write's thread, by the folder the screen holds now, since the settings move it below.
            var before = new ScreenInstance { Name = screen.Name, Folder = screen.Folder };
            Settings.ResizeScreen(screen, entry);
            Save();
            WriteScreenThen(() =>
            {
                var old = ScreenInstaller.Remove(before, plugin.Installer.SimHubRoot, log);
                if (!old.Ok) Log.Warn("The old folder of " + before.Name + " could not be removed: " + old.Error);
                return plugin.Installer.Write(screen);
            }, result =>
            {
                Save();
                Select(PanelPage.Screens, screen.Namespace);
                Redraw();
                if (!result.Ok) Log.Warn("Writing " + screen.Name + " at its new size failed: " + result.Error);
                Say(result.Ok ? PanelAddScreen.Resized(screen.Name, screen.SizeLabel, screen.Name) : PanelAddScreen.ResizeFailed(screen.Name, screen.SizeLabel), result.Ok);
            });
        }

        private void AddScreen(PackageEntry entry, string name)
        {
            if (WriteRefused()) return;
            var screen = Settings.AddScreen(entry, name);
            Save();
            WriteScreenThen(() => plugin.Installer.Write(screen), result =>
            {
                Save();
                Select(PanelPage.Screens, screen.Namespace);
                Redraw();

                // Said at the moment it becomes true rather than left to be found: SimHub reads its template list
                // once, at startup, and assigning a dashboard to a display is in another part of SimHub entirely.
                // The dashboard is listed under its title, which is the name the driver just chose.
                if (!result.Ok) Log.Warn("Installing " + screen.Name + " failed: " + result.Error);
                Say(result.Ok ? PanelAddScreen.Added(screen.Name, screen.Name) : PanelAddScreen.AddFailed(screen.Name), result.Ok);
            });
        }

        /// <summary>
        /// A second screen set up like this one: its settings copied, its bindings not (they are bound to the
        /// source's actions), under a name, a namespace and a folder of its own.
        /// </summary>
        private void DuplicateScreen(ScreenInstance screen)
        {
            if (WriteRefused()) return;
            var catalogue = PackageCatalogue.From(plugin.Installer.PackageSource, new SimHubInstallLog());
            var copy = Settings.DuplicateScreen(screen.Namespace, catalogue);
            if (copy == null)
            {
                Log.Warn("Duplicating " + screen.Name + " made nothing: no package in this build makes " + (screen.Folder ?? screen.Kind) + ".");
                Say(PanelMessage.Caution(PanelAddScreen.DuplicateFailed(screen.Name)));
                return;
            }
            Save();
            WriteScreenThen(() => plugin.Installer.Write(copy), result =>
            {
                Save();
                Select(PanelPage.Screens, copy.Namespace);
                Redraw();
                if (!result.Ok) Log.Warn("Installing " + copy.Name + ", a copy of " + screen.Name + ", failed: " + result.Error);
                Say(result.Ok ? PanelAddScreen.Added(copy.Name, copy.Name) : PanelAddScreen.AddFailed(copy.Name), result.Ok);
            });
        }

        /// <summary>
        /// The remove sheet, which says the two things that are easy to miss: the folder goes, and a wheel
        /// button bound to this screen's actions stops doing anything, because the action is no longer
        /// registered. ADR 0017 accepts that cost and this is where it is paid.
        /// </summary>
        private void ShowRemove(ScreenInstance screen)
        {
            var remove = Ui.Button(PanelScreens.RemoveItButton, PanelButtonKind.Danger, PanelButtonSize.Large);
            remove.ToolTip = PanelScreens.RemoveTooltipFor(screen);
            remove.Click += (sender, args) =>
            {
                if (WriteRefused()) return;
                WriteScreenThen(() => ScreenInstaller.Remove(screen, plugin.Installer.SimHubRoot, new SimHubInstallLog()), result =>
                {
                    Settings.RemoveScreen(screen.Namespace);
                    Save();
                    Select(PanelPage.Screens, null);
                    Redraw();
                    if (!result.Ok) Log.Warn("The dashboard of " + screen.Name + " could not be removed: " + result.Error);
                    Say(result.Ok ? PanelScreens.Removed(screen.Name) : PanelScreens.RemoveFailed(screen.Name), result.Ok);
                });
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
            ScreensShowSheet(PanelScreens.RemoveTitle(screen.Name), Ui.Prose(PanelScreens.RemoveBody(screen), Theme.SizeBody),
                SheetFooter(null, keep, remove));
        }
    }
}
