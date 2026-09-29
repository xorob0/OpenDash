// SettingsControl.cs: the OpenDash page in SimHub's left menu. The shell -- a sidebar of pages, the main
// column the page that is showing fills, the sheet that opens beside it -- plus the controls every page uses.
//
// One page per thing on the rig since #503: Home, Rig, Screens, LEDs, Matrix, Shortcuts, Settings and, pinned
// at the sidebar's foot, Updates. The four tabs grouped settings by their kind, which was honest about the
// settings model and wrong about the driver: somebody who came to change their wheel found its rows on three
// tabs. The frame's numbers are PanelShell's, where a test holds them to the artboards; the page files are
// SettingsControl.<Page>*.cs, one owner each, and reach the shell through the members in the hook region.
//
// Every change writes the settings object and saves it at once; the attached properties read the same
// object, so a change reaches a running dashboard on the next frame (ADR 0003).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using SimHub.Plugins;
using SimHub.Plugins.Styles;
using SimHub.Plugins.UI;

namespace OpenDashPlugin
{
    public partial class SettingsControl : UserControl
    {
        public const string DocumentationUrl = "https://github.com/xorob0/OpenDash#readme";
        public const string IssuesUrl = "https://github.com/xorob0/OpenDash/issues";

        /// <summary>
        /// The measure a caption, a picture of a face or the live preview is drawn against inside a page.
        /// </summary>
        /// <remarks>
        /// A ceiling rather than a width: the main column grows to PanelShell.ContentMax and what stretches
        /// simply stretches, but a line of prose and a preview keep this, which is the room the artboards
        /// give them at their 1200 px width.
        /// </remarks>
        private const double BodyWidth = 880;

        /// <summary>What a button holds even when its word is short, so that two of them in a row are the
        /// same size.</summary>
        private const double ButtonMinWidth = 96;

        private readonly OpenDash plugin;

        private OpenDashSettings Settings => plugin.Settings;

        // --- The frame -------------------------------------------------------------------------------

        private readonly Grid root = new Grid();
        private readonly DockPanel frame = new DockPanel { LastChildFill = true };
        private readonly Border sidebarHost = new Border();
        private readonly ScrollViewer mainScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        private readonly Border mainFrame = new Border { HorizontalAlignment = HorizontalAlignment.Left };
        private readonly StackPanel messageHost = new StackPanel { Orientation = Orientation.Vertical };
        private readonly ContentControl pageHost = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };

        /// <summary>
        /// Where the panel is. Held for the session and never persisted: somebody who came to change a zone
        /// lands where they left off within one sitting, and somebody coming back next week lands on Home.
        /// </summary>
        private PanelRoute route = PanelRoute.Home;

        /// <summary>Which card each page has selected, for the session: a screen's namespace, a strip's, a
        /// matrix slot. A page that selects nothing leaves its entry empty.</summary>
        private readonly Dictionary<PanelPage, string> selections = new Dictionary<PanelPage, string>();

        private PanelLayout layout = PanelLayout.Full;
        private double controlWidth = PanelShell.FullFrom;

        /// <summary>What the page being left asked to have undone: a press it was waiting on, a download it
        /// was watching. Run and cleared on every Go, and only on Go.</summary>
        private readonly List<Action> leaveActions = new List<Action>();

        /// <summary>What the build that is showing asked to have let go of when it is replaced: the controls
        /// it held. Run and cleared on every Go and on every rebuild in place.</summary>
        private readonly List<Action> dropActions = new List<Action>();

        /// <summary>The room and the columns the page that is showing was built for, so a resize rebuilds it
        /// only when either has moved.</summary>
        private double builtContentWidth = -1;
        private bool builtTwoColumns;

        /// <summary>Holds a resize's rebuild until the window has stopped moving.</summary>
        private DispatcherTimer resizeSettle;

        /// <summary>Whether SimHub has taken the panel off screen since it was last shown.</summary>
        private bool wasUnloaded;

        public SettingsControl(OpenDash plugin)
        {
            this.plugin = plugin;
            Background = Ui.Brush(Theme.SurfaceBase);
            Foreground = Ui.Brush(Theme.TextPrimary);
            FontFamily = PanelFonts.Label;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            // What the idle screen's mark says, so a driver who read it there finds the same offer here.
            updateStatus = UpdateMark.Opening(Settings.CheckForUpdates, plugin.LastUpdateStatus, plugin.OfferedUpdate, plugin.RigVersion);
            Content = BuildFrame();

            // The answers arrive from the plugin, which asks for Init and for this page alike. Held only
            // while the page is on screen, so a page SimHub has let go of is not kept alive by the plugin;
            // the clock is the same, and nothing ticks behind a panel nobody is looking at.
            plugin.UpdateChecked += ShowUpdateAnswer;
            plugin.RigLightingPressed += ShowLightingChange;
            PluginManager.InputMappingsChanged += MappingsChanged;
            Loaded += (sender, args) =>
            {
                PluginManager.InputMappingsChanged -= MappingsChanged;
                PluginManager.InputMappingsChanged += MappingsChanged;
                plugin.UpdateChecked -= ShowUpdateAnswer;
                plugin.UpdateChecked += ShowUpdateAnswer;
                plugin.RigLightingPressed -= ShowLightingChange;
                plugin.RigLightingPressed += ShowLightingChange;
                StartClock();
                if (wasUnloaded)
                {
                    wasUnloaded = false;
                    CatchUp();
                }
            };
            Unloaded += (sender, args) =>
            {
                plugin.UpdateChecked -= ShowUpdateAnswer;
                plugin.RigLightingPressed -= ShowLightingChange;
                PluginManager.InputMappingsChanged -= MappingsChanged;
                StopClock();
                // A resize that had not settled would otherwise rebuild the page off screen, and on Screens
                // start a preview no Unloaded would ever dispose.
                if (resizeSettle != null) resizeSettle.Stop();
                // A page that is not on screen must not still be drawing a dashboard.
                DropPreview();
                wasUnloaded = true;
            };

            Go(route);
            // Init has already queued the day's check, so this asks only when that one did not start: never on
            // the startup path, never within the day, never twice in one start even when the first found no
            // network, and never at all unless the setting says so. A check still in flight answers here too.
            // A background check that finds nothing shows nothing.
            Check(manual: false);
        }

        /// <summary>
        /// The frame: the sidebar docked left, the main column scrolling beside it, and the sheet's layer
        /// over the main column.
        /// </summary>
        /// <remarks>
        /// Nothing sets a height. SimHub hands the control to its own settings menu, whose window is whatever
        /// the user has dragged it to; the sidebar fills it and the main column scrolls.
        /// </remarks>
        private UIElement BuildFrame()
        {
            var column = new StackPanel { Orientation = Orientation.Vertical };
            column.Children.Add(messageHost);
            column.Children.Add(pageHost);
            mainFrame.Child = column;
            mainScroll.Content = mainFrame;

            DockPanel.SetDock(sidebarHost, Dock.Left);
            frame.Children.Add(sidebarHost);
            frame.Children.Add(mainScroll);

            root.Children.Add(frame);
            root.Children.Add(BuildSheetLayer());
            root.SizeChanged += (sender, args) => Resize(args.NewSize.Width);
            // Bubbling, not tunnelling: an open drop-down list, a flyout and the search's suggestions each
            // put their own Escape away and mark it handled, so the sheet closes only on an Escape nothing
            // inside it wanted. PreviewKeyDown saw the key first and closed the sheet under an open list.
            root.KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Escape && SheetOpen)
                {
                    CloseSheet();
                    args.Handled = true;
                }
            };
            ApplyLayout();
            return root;
        }

        /// <summary>
        /// Follows the control's width: the sidebar, the gutter and the ceiling change at once. The sidebar is
        /// drawn again when it changes between labels and a rail, and the page when the room it was built for
        /// has moved -- its columns, or its width, which a picture or the Rig canvas is sized from -- once the
        /// window has stopped moving. A card grid decides its columns as it is measured.
        /// </summary>
        /// <remarks>
        /// The constructor builds the first page before SimHub has measured the control, at the
        /// <see cref="PanelShell.FullFrom"/> it assumes; the first real size is what corrects it.
        /// </remarks>
        private void Resize(double width)
        {
            if (width <= 0) return;
            controlWidth = width;
            var next = PanelShell.Layout(width);
            var flipped = PanelShell.IsNarrow(next) != PanelShell.IsNarrow(layout);
            layout = next;
            Ui.Layout = next;
            ApplyLayout();
            if (flipped)
            {
                RefreshSidebar();
                RebuildPage();
            }
            else if (PageRoomMoved())
            {
                SettleThenRebuild();
            }
            if (SheetOpen) SizeSheet();
        }

        /// <summary>Whether the page that is showing was built for other room than it has now.</summary>
        private bool PageRoomMoved()
        {
            return Math.Abs(ContentWidth - builtContentWidth) >= 1 || TwoColumns != builtTwoColumns;
        }

        private void SettleThenRebuild()
        {
            if (resizeSettle == null)
            {
                resizeSettle = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(150) };
                resizeSettle.Tick += (sender, args) =>
                {
                    resizeSettle.Stop();
                    if (IsLoaded && PageRoomMoved()) RebuildPage();
                };
            }
            resizeSettle.Stop();
            resizeSettle.Start();
        }

        private void ApplyLayout()
        {
            var pad = PanelShell.MainPaddingX(layout);
            mainFrame.Padding = new Thickness(pad, PanelShell.MainPaddingTop, pad, PanelShell.MainPaddingBottom);
            mainFrame.MaxWidth = WidePage(route.Page) ? double.PositiveInfinity : PanelShell.ContentMax + 2 * pad;
            mainFrame.HorizontalAlignment = HorizontalAlignment.Stretch;
            sidebarHost.Width = PanelShell.SidebarWidthFor(layout);
            sheetLayer.Margin = new Thickness(PanelShell.SidebarWidthFor(layout), 0, 0, 0);
        }

        /// <summary>The Rig page draws the rig and is better for the room, so it takes the whole column.</summary>
        private static bool WidePage(PanelPage page)
        {
            return page == PanelPage.Rig;
        }

        // --- The hooks a page reaches the shell through -------------------------------------------------
        //
        // Navigation: Go(route) and Go(page, anchor) leave the page; Open(page, id, anchor) goes with a card
        // selected; Select and Selected hold a page's card for the session (Rig's is a PanelEmulation
        // scenario id, which is how Settings' "Try" opens Rig on one); Redraw rebuilds the page in place after
        // a change, keeping the scroll and clearing the lines; ShowLightingChange redraws the sidebar's switch
        // and the page after night mode or a brightness changed, which the wheel's buttons do too.
        // Lifetime: OnDrop(action) lets go of what one build holds, on every rebuild and on Go; OnLeave(action)
        // undoes what the page started, on Go only; OnTick(action) is called every second while showing;
        // OnUpdate(checking, answered) hears the update check. Layout: PageLayout, PageSection, ContentWidth
        // (less the scroll bar), Narrow and TwoColumns, all read while building -- the shell rebuilds the page
        // when any of them moves. Lines: Say. Sheets: ShowSheet(title, body, footer, closed), SheetFooter,
        // CloseSheet. Shared facts and presses: TriggersOf, BoundCount and BindingChipFor
        // (SettingsControl.Bindings.cs, the chip landing on PanelBindings.Anchor(action), which Shortcuts tags);
        // GlyphSheet, InstallScreenAgain, BuildFlagBoxImportFallback, SafePlan, FlagBoxName, EmbeddedProfileOf,
        // ReinstallBar and UpdateBars (SettingsControl.Profiles.cs); BuildScreenPreview(screen, width).
        //
        // Ownership: a page owns SettingsControl.<Page>*.cs -- Updates owns .Updates.cs, .Updates.Plugin.cs,
        // .Updates.Packages.cs and .Updates.Lights.cs; Screens owns .Screens*.cs; LEDs owns .Lights.cs -- and
        // its Panel<Page>.cs with that file's test. Every other SettingsControl*.cs, Widgets*.cs, PanelShell.cs,
        // PanelSearch.cs, PanelSoon.cs and PanelBindings.cs are the shell's. All pages share one partial class,
        // so every member a page adds for itself is private and carries its page's name as a prefix
        // (ScreensTile, LedsDeviceList, MatrixIdleRow), or lives in a private nested class named for the page;
        // two agents writing the same unprefixed helper would only meet at the merge.

        /// <summary>
        /// Goes to a page, and to a row on it when the route names one.
        /// </summary>
        /// <remarks>
        /// The sheet goes first, so that whatever its <c>closed</c> callback draws is drawn before, and then
        /// cleared with, the page being left, rather than surviving into the next one. Then everything the
        /// page being left held goes with it: the live preview of a screen, which holds SimHub's renderer
        /// (ADR 0020); the page's lines; its tick callbacks and whatever it asked to have undone or dropped.
        /// The page is built afresh from the settings rather than re-shown, so nothing has to be kept in step
        /// while it is not on screen, and a control a page holds belongs to the one build that made it.
        /// </remarks>
        private void Go(PanelRoute to)
        {
            CloseSheet();
            var sidebarFocused = sidebarHost.IsKeyboardFocusWithin;
            DropPreview();
            ClearMessages();
            ClearTicks();
            ClearUpdateHandlers();
            RunLeaveActions();
            RunDropActions();
            ForgetBindings();
            route = to ?? PanelRoute.Home;
            ApplyLayout();
            RefreshAttention();
            pageHost.Content = BuildPage(route);
            RefreshSidebar();
            if (sidebarFocused) FocusNavItem(route.Page);
            if (route.Anchor != null) ScrollToAnchor(route.Anchor);
            else mainScroll.ScrollToTop();
        }

        private void Go(PanelPage page, string anchor = null)
        {
            Go(new PanelRoute(page, anchor));
        }

        /// <summary>
        /// Draws the page that is showing again, in place, after something changed the rig under it: what
        /// needs fixing is asked again, the sidebar's counts and dots follow, the lines are cleared for the
        /// press's own, and the scroll stays where the driver was.
        /// </summary>
        /// <remarks>
        /// It was Go to the same page, which threw the page back to its top on every press. The lines are
        /// still cleared, because a press that redraws says its own line after, and two presses' lines would
        /// otherwise stack; Say brings the top into view when it has something to say.
        /// </remarks>
        private void Redraw()
        {
            CloseSheet();
            ClearMessages();
            RefreshAttention();
            RebuildPage();
            RefreshSidebar();
        }

        /// <summary>Draws the page again without leaving it: the lines stay, the scroll stays, keyboard focus
        /// comes back to the control at the same place. For a change of layout, where nothing about the rig
        /// moved, and under Redraw.</summary>
        /// <remarks>
        /// The page is not left, so its OnLeave actions are not run: a press it is waiting on survives a
        /// rebuild. What the discarded build held is let go of through OnDrop.
        /// </remarks>
        private void RebuildPage()
        {
            var focus = pageHost.IsKeyboardFocusWithin ? FocusPath(pageHost, Keyboard.FocusedElement as DependencyObject) : null;
            DropPreview();
            ClearTicks();
            ClearUpdateHandlers();
            RunDropActions();
            var offset = mainScroll.VerticalOffset;
            pageHost.Content = BuildPage(route);
            mainScroll.ScrollToVerticalOffset(offset);
            if (focus != null) RestoreFocus(pageHost, focus);
        }

        /// <summary>
        /// Draws the sidebar's switch and the page again after the rig's lighting changed: a press here, or
        /// the wheel's night mode and brightness buttons, which the plugin reports through
        /// <see cref="OpenDash.RigLightingPressed"/>. A page need not watch for these itself.
        /// </summary>
        private void ShowLightingChange()
        {
            RefreshSidebar();
            RebuildPage();
        }

        /// <summary>
        /// Brings the panel up to date after SimHub showed another page: an update answer that landed while
        /// it was away was not heard, the preview it had was let go of on the way out, and a binding made on
        /// SimHub's own Controls and events page -- the page a driver most often leaves this one for -- was
        /// not heard either, because the mapping event is only listened to while the panel is on screen.
        /// </summary>
        private void CatchUp()
        {
            ForgetBindings();
            if (updateStatus.State == UpdateState.Checking && !plugin.UpdateCheckInFlight && !applying)
            {
                var last = plugin.LastUpdateStatus;
                updateStatus = last == null
                    ? new UpdateStatus { State = UpdateState.Idle, InstalledVersion = plugin.RigVersion }
                    : askedManually && !last.Manual ? last.AsManual() : last;
                askedManually = false;
            }
            RefreshAttention();
            RebuildPage();
            RefreshSidebar();
        }

        /// <summary>Selects a card on a page for the session, without drawing anything.</summary>
        private void Select(PanelPage page, string id)
        {
            if (id == null) selections.Remove(page);
            else selections[page] = id;
        }

        /// <summary>The card a page has selected, or null.</summary>
        private string Selected(PanelPage page)
        {
            string id;
            return selections.TryGetValue(page, out id) ? id : null;
        }

        /// <summary>Goes to a page with a card selected on it: Home's "Open Rim".</summary>
        private void Open(PanelPage page, string id, string anchor = null)
        {
            Select(page, id);
            Go(new PanelRoute(page, anchor));
        }

        /// <summary>The room a page has for its content at the width the panel is now, less the room the main
        /// column's scroll bar takes when the page is taller than the window.</summary>
        private double ContentWidth => PanelShell.ContentWidth(controlWidth, WidePage(route.Page), SystemParameters.VerticalScrollBarWidth);

        /// <summary>Whether the sidebar is a rail, which is when a page stacks what it would lay side by side.</summary>
        private bool Narrow => PanelShell.IsNarrow(layout);

        /// <summary>Whether a page may lay two blocks side by side.</summary>
        private bool TwoColumns => PanelShell.TwoColumns(layout, ContentWidth);

        /// <summary>
        /// Asks for something to be undone when the page is left for another: a press it is waiting on, a
        /// watch it set. Not run when the page is rebuilt in place (a resize, night mode, Redraw).
        /// </summary>
        private void OnLeave(Action undo)
        {
            if (undo != null) leaveActions.Add(undo);
        }

        /// <summary>
        /// Asks for the controls this build of the page holds to be let go of when the build is replaced,
        /// whether by another page or by a rebuild of this one, so a refresh that lands afterwards writes
        /// into nothing rather than into a control nobody can see.
        /// </summary>
        private void OnDrop(Action drop)
        {
            if (drop != null) dropActions.Add(drop);
        }

        private void RunLeaveActions()
        {
            Run(leaveActions, "Leaving a page failed to undo something: ");
        }

        private void RunDropActions()
        {
            Run(dropActions, "Replacing a page failed to let go of something: ");
        }

        private static void Run(List<Action> list, string failure)
        {
            var actions = list.ToList();
            list.Clear();
            foreach (var action in actions)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Warn(failure + ex.Message);
                }
            }
        }

        /// <summary>
        /// Where a focused control sits under a root, as the child index at each level of the visual tree,
        /// so the same place can be found in a rebuild that draws the same shape.
        /// </summary>
        private static List<int> FocusPath(DependencyObject root, DependencyObject focused)
        {
            if (root == null || focused == null) return null;
            var path = new List<int>();
            var node = focused;
            while (node != null && node != root)
            {
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(node);
                if (parent == null) return null;
                var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
                var index = -1;
                for (var i = 0; i < count; i++)
                {
                    if (System.Windows.Media.VisualTreeHelper.GetChild(parent, i) == node) { index = i; break; }
                }
                if (index < 0) return null;
                path.Insert(0, index);
                node = parent;
            }
            return node == root ? path : null;
        }

        /// <summary>Focuses the control at that place once the rebuild has been laid out, or the nearest
        /// focusable thing above it when the rebuild is shaped differently there.</summary>
        private void RestoreFocus(DependencyObject root, List<int> path)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DependencyObject node = root;
                IInputElement last = null;
                foreach (var index in path)
                {
                    if (index >= System.Windows.Media.VisualTreeHelper.GetChildrenCount(node)) break;
                    node = System.Windows.Media.VisualTreeHelper.GetChild(node, index);
                    var element = node as UIElement;
                    if (element != null && element.Focusable && element.IsVisible && element.IsEnabled) last = element;
                }
                if (last != null) Keyboard.Focus(last);
                else pageHost.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Builds a page. Each page's file owns its Build method and everything under it; this is the only
        /// place the shell names them.
        /// </summary>
        private FrameworkElement BuildPage(PanelRoute to)
        {
            builtContentWidth = ContentWidth;
            builtTwoColumns = TwoColumns;
            try
            {
                switch (to.Page)
                {
                    case PanelPage.Rig: return BuildRigPage(to);
                    case PanelPage.Screens: return BuildScreensPage(to);
                    case PanelPage.Leds: return BuildLedsPage(to);
                    case PanelPage.Matrix: return BuildMatrixPage(to);
                    case PanelPage.Shortcuts: return BuildShortcutsPage(to);
                    case PanelPage.Settings: return BuildSettingsPage(to);
                    case PanelPage.Updates: return BuildUpdatesPage(to);
                    default: return BuildHomePage(to);
                }
            }
            catch (Exception ex)
            {
                // A page that throws while it is drawn leaves the panel usable, the sidebar with it, and
                // says where to look rather than showing SimHub's own crash dialog.
                Log.Error("Drawing the " + PanelNav.Label(to.Page) + " page failed", ex);
                return Ui.VStack(12,
                    Ui.PageTitle(PanelNav.Label(to.Page)),
                    Ui.Prose("This page could not be drawn. See SimHub's log.", Theme.SizeBody, Theme.Caution));
            }
        }

        /// <summary>
        /// A page as the artboards lay one out: the title, with the page's own actions to its right, and
        /// the sections under it 28 apart.
        /// </summary>
        private FrameworkElement PageLayout(string title, FrameworkElement actions, params UIElement[] sections)
        {
            var head = actions == null ? (FrameworkElement)Ui.PageTitle(title) : Ui.Row(Ui.PageTitle(title), actions);
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(head);
            foreach (var section in sections)
            {
                if (section == null) continue;
                var element = section as FrameworkElement;
                if (element != null)
                {
                    var margin = element.Margin;
                    element.Margin = new Thickness(margin.Left, margin.Top + PanelShell.SectionGap, margin.Right, margin.Bottom);
                }
                stack.Children.Add(section);
            }
            return stack;
        }

        /// <summary>A section of a page: its heading, and what is under it 14 below.</summary>
        private static FrameworkElement PageSection(string heading, params UIElement[] children)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            if (!string.IsNullOrEmpty(heading))
            {
                var title = Ui.Heading(heading);
                title.Margin = new Thickness(0, 0, 0, 14);
                stack.Children.Add(title);
            }
            foreach (var child in children)
            {
                if (child != null) stack.Children.Add(child);
            }
            return stack;
        }

        /// <summary>Brings the row carrying that anchor into view once the page has been laid out.</summary>
        private void ScrollToAnchor(string anchor)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var target = Ui.FindAnchor(pageHost.Content as DependencyObject, anchor);
                if (target == null) return;
                target.BringIntoView();
            }), DispatcherPriority.Loaded);
        }

        private void Save()
        {
            plugin.SaveSettings();
        }

        /// <summary>
        /// Saves something the driver did to one screen, which also keeps that screen: a migrated screen
        /// somebody has set up is one they have (ScreenInstance.Keep).
        /// </summary>
        private void Save(ScreenInstance screen)
        {
            screen.Keep();
            Save();
        }

        // --- The controls every page uses --------------------------------------------------------------

        /// <summary>
        /// A switch, drawn as the artboards draw .tog.
        /// </summary>
        /// <remarks>
        /// It was SimHub's SHToggleButton, which looked like SimHub's and not like the redesign, and which
        /// declared no size of its own and grew to whatever it was measured against. The kit's switch is a
        /// ToggleButton too, so every caller that set a tooltip or an alignment on it keeps working.
        /// </remarks>
        private static ToggleButton BuildToggle(bool isOn, Action<bool> changed)
        {
            return Ui.Switch(isOn, changed);
        }

        private static Segmented BuildSegmented(string[] values, string[] labels, string selected, Action<string> changed)
        {
            var options = values.Select((value, i) => new Segmented.Option(value, labels[i]));
            var control = new Segmented(options, selected);
            control.Changed += changed;
            return control;
        }

        /// <summary>
        /// One value of a value set, as a drop-down: the control for a list longer than a segmented bar is
        /// drawn for. The labels are positional, so values[i] is what labels[i] names.
        /// </summary>
        /// <remarks>
        /// Ui.Field is the chrome, so the box carries the same ground, outline and ring as the fields beside
        /// it. Its corner stays SimHub's: a ComboBox has no CornerRadius, and the template that would give it
        /// one supplies the list under it as well, which Widgets.Field records.
        /// </remarks>
        private static ComboBox BuildChoice(string[] values, string[] labels, string selected, double width, Action<string> changed)
        {
            var box = new ComboBox { Width = width, HorizontalAlignment = HorizontalAlignment.Right };
            Ui.Field(box, Theme.ControlHeightSm);
            foreach (var label in labels) box.Items.Add(label);
            var index = Array.IndexOf(values, selected);
            box.SelectedIndex = index >= 0 ? index : 0;
            box.SelectionChanged += (sender, args) =>
            {
                if (box.SelectedIndex < 0 || box.SelectedIndex >= values.Length) return;
                changed(values[box.SelectedIndex]);
            };
            return box;
        }

        /// <summary>A page picker in page-number order, so that SelectedIndex is the page number.</summary>
        private static ComboBox BuildPageSelect(IReadOnlyList<ZonePage> pages, int selected, double width, Action<int> changed)
        {
            var box = new ComboBox { Width = width, HorizontalAlignment = HorizontalAlignment.Left };
            Ui.Field(box, Theme.ControlHeightSm);
            foreach (var page in pages) box.Items.Add(page.Name);
            box.SelectedIndex = selected >= 0 && selected < pages.Count ? selected : 0;
            box.SelectionChanged += (sender, args) =>
            {
                if (box.SelectedIndex < 0) return;
                changed(box.SelectedIndex);
            };
            return box;
        }

        private static FrameworkElement BuildPercentBox(int value, Action<int> changed) => BuildNumberBox(value, 0, 100, changed);

        /// <summary>A small number field that repairs whatever is typed into it rather than refusing it.</summary>
        private static FrameworkElement BuildNumberBox(int value, int min, int max, Action<int> changed)
        {
            var box = new TextBox
            {
                Width = 80,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                HorizontalAlignment = HorizontalAlignment.Right,
                Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            Ui.Field(box, Theme.ControlHeightSm);
            Action commit = () =>
            {
                int parsed;
                if (!int.TryParse(box.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out parsed)) parsed = value;
                if (parsed < min) parsed = min;
                if (parsed > max) parsed = max;
                box.Text = parsed.ToString(System.Globalization.CultureInfo.InvariantCulture);
                changed(parsed);
            };
            box.LostFocus += (sender, args) => commit();
            box.KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter) commit();
            };
            return box;
        }

        /// <summary>A text box for a name, at the width a form gives one.</summary>
        private static TextBox BuildNameBox(string text, double width = 280)
        {
            var box = new TextBox
            {
                Width = width,
                Text = text ?? string.Empty,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            Ui.Field(box, Theme.ControlHeightSm);
            return box;
        }

        /// <summary>The one accented action a page is allowed, which is the press somebody came to it to make.</summary>
        private static Button BuildPrimaryButton(string content, string tooltip)
        {
            var button = Ui.PrimaryButton(content);
            button.MinWidth = ButtonMinWidth;
            button.ToolTip = tooltip;
            return button;
        }

        /// <summary>Every other action: the outline the artboards draw.</summary>
        private static Button BuildSecondaryButton(string content, string tooltip)
        {
            var button = Ui.OutlineButton(content);
            button.MinWidth = ButtonMinWidth;
            button.ToolTip = tooltip;
            return button;
        }

        /// <summary>The press that takes something away: the outline again, in danger.</summary>
        private static Button BuildDestructiveButton(string content, string tooltip)
        {
            var button = Ui.DestructiveButton(content);
            button.MinWidth = ButtonMinWidth;
            button.ToolTip = tooltip;
            return button;
        }

        /// <summary>An action its ink alone carries: no ground, no outline, no padding and no minimum width.</summary>
        private static Button BuildTextButton(string content, string tooltip, string hex = Theme.TextPrimary)
        {
            var button = Ui.LinkButton(content, hex);
            button.ToolTip = tooltip;
            return button;
        }

        /// <summary>
        /// SimHub's own control for binding an input to an action, so a wheel button is bound here rather
        /// than by sending the driver to Controls and events to find the name.
        ///
        /// It is a SimHub UserControl and SimHub is not always there -- the panel is constructed in tests and
        /// could be constructed by a host that does not carry the style -- so a failure falls back to naming
        /// the action, which is exactly what somebody binding it by hand needs.
        /// </summary>
        private static FrameworkElement BuildBinder(string action, string friendlyName, bool hold = false)
        {
            try
            {
                var editor = new ControlsEditor { ActionName = Contract.FullActionName(action), FriendlyName = friendlyName, MinWidth = 260 };
                editor.HorizontalAlignment = HorizontalAlignment.Right;
                if (hold) HoldWhilePressed(editor);
                return editor;
            }
            catch (Exception ex)
            {
                Log.Warn("ControlsEditor is unavailable; naming the action instead: " + ex.Message);
                // The sheet's value on a binding row, which is the one string OpenDash sets on such a row
                // at all: the editor beside it is SimHub's and draws its own.
                var text = Ui.Text(friendlyName + ": bind " + Contract.FullActionName(action) + " in Controls and events", Theme.SizeLabel, FontWeights.Normal, Theme.TextPrimary);
                text.HorizontalAlignment = HorizontalAlignment.Right;
                return text;
            }
        }

        /// <summary>
        /// Forces a glance binding to the one press type that can hold anything.
        ///
        /// SimHub only calls an action's start on press and its end on release when the mapping's press type
        /// is `During`; every other type goes through TriggerAction, which fires start and end back to back.
        /// The binding dialog offers ShortAndLongPress by default, so a driver who binds the glance the
        /// obvious way gets a page that appears and vanishes in one frame.
        ///
        /// The glance is only meaningful as a hold, so any binding to it is corrected rather than
        /// second-guessed. The dialog writes into Model.Triggers; this watches that collection.
        ///
        /// Decided in #435, and kept as a correction rather than turned into a warning: a warning would leave
        /// in place a binding that cannot hold anything, and there is no press type but `During` a driver
        /// could choose and still have a glance. It is the one place OpenDash does not let SimHub's own
        /// control mean what it says, so the row says it instead of doing it silently: every glance row's
        /// caption ends on PanelCopy.GlanceBoundAsHold.
        /// </summary>
        private static void HoldWhilePressed(ControlsEditor editor)
        {
            // SimHub's own Loaded handler replaces Model with a new ControlsEditorModel every time the editor
            // is shown (ControlsEditor_Loaded in 9.12.6), and the Add dialog writes into that new model's
            // Triggers. Watching only the first model missed every glance bound on the page, so the watch
            // follows Model wherever it goes.
            ControlsEditorModel watched = null;
            System.Collections.Specialized.NotifyCollectionChangedEventHandler changed = null;
            Action<ControlsEditorModel> apply = model =>
            {
                if (model == null || model.Triggers == null) return;
                foreach (var mapping in model.Triggers)
                {
                    if (mapping != null && mapping.PressType != PressType.During) mapping.PressType = PressType.During;
                }
            };
            changed = (sender, args) => apply(watched);
            Action attach = () =>
            {
                var model = editor.Model;
                if (ReferenceEquals(model, watched)) return;
                if (watched != null && watched.Triggers != null) watched.Triggers.CollectionChanged -= changed;
                watched = model;
                if (watched != null && watched.Triggers != null) watched.Triggers.CollectionChanged += changed;
                apply(watched);
            };
            editor.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == null || args.PropertyName == "Model") attach();
            };
            editor.Loaded += (sender, args) => attach();
            attach();
        }

        /// <summary>
        /// SimHub's link button (SHLinkButton) carrying the accent text and the external-link icon.
        /// </summary>
        /// <remarks>
        /// The hover is drawn here because neither half of the link can inherit it: the ink is set on the
        /// TextBlock and on the Path rather than left to the button, so SHLinkButton's own template has
        /// nothing to reach. The two change together, or a hovered link is a lit word beside a dim glyph.
        /// </remarks>
        private static Button BuildLink(string text, string url)
        {
            Button button;
            try
            {
                button = new SHLinkButton();
            }
            catch (Exception ex)
            {
                Log.Warn("SHLinkButton is unavailable; using a plain button: " + ex.Message);
                button = new Button();
            }
            var icon = Ui.Icon(Ui.ExternalLinkIcon, Theme.Accent);
            var label = Ui.Text(text, Theme.SizeBody, FontWeights.Medium, Theme.Accent);
            button.Content = Ui.HStack(PanelMetrics.ButtonIconGap, icon, label);
            button.Height = Theme.ControlHeight;
            button.Padding = new Thickness(0);
            button.Cursor = Cursors.Hand;
            button.ToolTip = url;
            Action<string> ink = hex =>
            {
                icon.Stroke = Ui.Brush(hex);
                label.Foreground = Ui.Brush(hex);
            };
            button.MouseEnter += (sender, args) => ink(Theme.AccentHover);
            button.MouseLeave += (sender, args) => ink(Theme.Accent);
            button.Click += (sender, args) => Ui.OpenUrl(url);
            return button;
        }
    }
}
