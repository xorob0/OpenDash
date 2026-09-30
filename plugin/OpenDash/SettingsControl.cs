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
        /// <summary>The main column's scroll. Not a tab stop, nor is <see cref="pageHost"/>: WPF makes both
        /// focusable by default, which put two invisible stops before a page's first control. The sidebar's
        /// scroll is the same.</summary>
        private readonly ScrollViewer mainScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
        };
        private readonly Border mainFrame = new Border { HorizontalAlignment = HorizontalAlignment.Stretch };

        /// <summary>The main column's one grid column, which carries the ceiling: a star column with a
        /// MaxWidth stretches up to it and then stays beside the sidebar, where a Stretch element with a
        /// MaxWidth of its own is centred in whatever is left.</summary>
        private readonly ColumnDefinition mainColumn = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
        private readonly StackPanel messageHost = new StackPanel { Orientation = Orientation.Vertical };
        private readonly ContentControl pageHost = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, Focusable = false, IsTabStop = false };

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

        /// <summary>What the page being left asked to have undone, by key: a press it was waiting on, a
        /// download it was watching. Run and cleared on every Go, and only on Go; a key registered again
        /// replaces its action, so every rebuild in place registering the same undo leaves one copy.</summary>
        private readonly List<KeyValuePair<string, Action>> leaveActions = new List<KeyValuePair<string, Action>>();

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

        /// <summary>Whether the first page has been built. It waits for the control's first real width, so
        /// the page is built once at the width it is shown at rather than at an assumed one and again.</summary>
        private bool pageBuilt;

        /// <summary>Whether the build that is showing draws the rig's lighting -- night mode or a brightness --
        /// and so asked through <see cref="DrawsLighting"/> to be rebuilt when either changes.</summary>
        private bool pageDrawsLighting;

        /// <summary>Holds a lighting change's rebuild until a burst of wheel presses has finished.</summary>
        private DispatcherTimer lightingSettle;

        /// <summary>What the build that is showing asked, through <see cref="OnLighting"/>, to have repainted in
        /// place when night mode or a brightness changes: the dim on a picture of a strip or a matrix.
        /// Cleared by every build.</summary>
        private readonly List<Action> lightingActions = new List<Action>();

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
                // SizeChanged has built the first page by now; a host that never gave the control a width
                // gets it at the assumed one rather than an empty column.
                if (!pageBuilt) BuildFirstPage();
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
                if (lightingSettle != null) lightingSettle.Stop();
                // A page that is not on screen must not still be drawing a dashboard.
                DropPreview();
                wasUnloaded = true;
            };

            // The first page is built by the first real size (Resize), or by Loaded when none comes: built
            // here it was built at the assumed PanelShell.FullFrom and again 150 ms later at the real width,
            // so every opening loaded Screens' live preview twice and read SimHub's devices and profiles twice.
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
            var holder = new Grid();
            holder.ColumnDefinitions.Add(mainColumn);
            holder.Children.Add(mainFrame);
            mainScroll.Content = holder;

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
        /// The first real size builds the first page, at that width: nothing is built before SimHub has
        /// measured the control.
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
            if (!pageBuilt)
            {
                BuildFirstPage();
                return;
            }
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

        /// <summary>Builds the first page, once.</summary>
        private void BuildFirstPage()
        {
            if (pageBuilt) return;
            pageBuilt = true;
            Go(route);
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
                resizeSettle = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(PanelShell.ResizeSettleMs) };
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
            mainFrame.Padding = new Thickness(pad, PanelShell.MainPaddingTop, pad, PanelShell.MainPaddingBottomFor(route.Page));
            mainColumn.MaxWidth = WidePage(route.Page) ? double.PositiveInfinity : PanelShell.ContentMax + 2 * pad;
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
        // a change, keeping the scroll and clearing the lines. Lighting: ShowLightingChange sets the sidebar's
        // switch after night mode or a brightness changed, which the wheel's buttons do too, while the driver
        // is on track; it then runs what the build registered through OnLighting(repaint), in place and at
        // once -- re-dimming a picture is a property set, and a page whose lighting is only a dimmed preview
        // uses this and nothing else -- and last rebuilds the page, a burst of presses once, only when the
        // build called DrawsLighting(), which is for a page that draws the night switch or a brightness
        // itself (Home, Rig, Settings). A rebuild runs the whole build on SimHub's interface thread while the
        // driver may be on track, so a page whose build reads SimHub's devices, its profiles or the disk
        // (LedTargets, StripInstaller, FlagBoxInstaller, PackageExtractor, SafePlan, BarCensus) must not call
        // DrawsLighting(); reading settings, SimHub's own included (its units), is not that. A page that
        // writes LightsNightMode, LightsBrightness or LightsNightBrightness calls ShowLightingChange() after
        // Save -- never RefreshSidebar alone, which leaves the OnLighting repaints and the DrawsLighting
        // rebuild undone (Home's slider kept its "Brightness" label and day value with night mode on) -- and a
        // slider calls it when the drag ends rather than on every value, since the rebuild would take the
        // slider from under the pointer.
        // Lifetime: OnDrop(action) lets go of what one build holds, on every rebuild and on Go;
        // OnLeave(key, action) undoes what the page started, on Go only, one action per key -- a rebuild
        // registering the same key again replaces the action rather than adding a second, so an undo runs
        // once however many times the page was built since the last Go; OnTick(action) is called every
        // second while showing; OnUpdate(checking, answered) hears the update check. Layout: PageLayout,
        // PageSection, ContentWidth (less the scroll bar), Narrow (the rail: the sidebar is icons) and
        // TwoColumns (the only test for laying two blocks side by side: the full sidebar and at least
        // PanelShell.TwoColumnFrom of content; Narrow false does not mean two columns fit -- from 1000 to
        // about 1080 px the sidebar is full and the content is 679 to 759 -- so a page lays blocks side by
        // side only when TwoColumns is true and stacks them otherwise, never testing !Narrow), all read while
        // building -- the shell rebuilds the page when any of them moves, so anything a page has in flight
        // (a download, a press it is waiting on) lives in a field outside the build and the next build draws
        // it from there. WidePage(page) is the shell's list of pages that take the whole column rather than
        // stop at PanelShell.ContentMax (Rig alone: Rig is wide already, and there is no PanelShell.Wide).
        // Lines: Say. Sheets: ShowSheet(title, body, footer,
        // closed), SheetFooter, CloseSheet. Shared facts and presses: TriggersOf, BoundCount and BindingChipFor
        // (SettingsControl.Bindings.cs, the chip landing on PanelBindings.Anchor(action), which Shortcuts tags,
        // and every binding named through PanelBindings.TriggerLabel); issues, the answer RefreshAttention keeps,
        // asked through PanelAttention.Has and Of with its rule constants (ScreenRestart, StripUnselected and the
        // rest), and ScreenFacts, StripFacts and MatrixFacts for each device's state -- whether a strip is
        // Showing, a dashboard is waiting for a restart -- all read while building and never on the tick;
        // CheckAgain, which asks SimHub again and redraws (SettingsControl.Status.cs); ProfileSelected;
        // updateStatus and Check(manual), the update check's answer and asking it (SettingsControl.Live.cs);
        // GlyphSheet, InstallScreenAgain, BuildFlagBoxImportFallback, SafePlan, FlagBoxName, EmbeddedProfileOf,
        // InstallBar, InstallFlagBox, BarCensus, ReinstallBar and UpdateBars (SettingsControl.Profiles.cs);
        // BuildScreenPreview(screen, width).
        //
        // Ownership, file by file (each Panel*.cs with its own test, PanelFooTests.cs):
        //   Home       SettingsControl.Home.cs; PanelHome.cs
        //   Rig        SettingsControl.Rig.cs; PanelRigMap.cs
        //   Screens    SettingsControl.Screens*.cs (.Screens, .Screens.Face, .Screens.Round,
        //              .Screens.PitWall, .Screens.Companion); PanelScreens.cs, PanelAddScreen.cs,
        //              PanelFacePlan.cs, PanelPitWallPlan.cs, PanelReorder.cs
        //   LEDs       SettingsControl.Lights.cs; PanelLeds.cs
        //   Matrix     SettingsControl.Matrix.cs; PanelMatrix.cs
        //   Shortcuts  SettingsControl.Shortcuts.cs; PanelShortcuts.cs
        //   Settings   SettingsControl.Settings.cs; PanelSettings.cs, PanelDataTab.cs
        //   Updates    SettingsControl.Updates*.cs (.Updates, .Updates.Plugin, .Updates.Packages,
        //              .Updates.Lights); PanelUpdates.cs, PanelPackageRow.cs, PanelLightRows.cs,
        //              PanelConfirmation.cs
        //   shared     PanelCopy.cs and PanelLights.cs hold words several pages draw. Neither has per-page
        //              regions: a page adds the constants it needs and changes only constants its own page
        //              alone draws, never one another page reads. PanelCopy.LightRow is the Updates page's
        //              (the Matrix header draws PanelMatrix.ProfileRow); the empty states are the pages'
        //              own (PanelScreens.NoScreens, PanelLeds.NoStrips, PanelMatrix.NoPanels).
        //   shell      everything else: SettingsControl.cs and its other partials (.Sidebar, .Sheet, .Status,
        //              .Live, .Messages, .Preview, .Bindings, .Profiles), Widgets*.cs, Segmented.cs,
        //              PanelShell.cs, PanelKit.cs, PanelNav.cs, PanelAttention.cs, PanelSearch.cs,
        //              PanelSoon.cs, PanelBindings.cs, PanelEmulation.cs and the rest of Panel*.cs.
        // Frozen names inside page files: a page owns its words, but these members are read from a file its
        // agent may not edit, so the page keeps each one's name and meaning and may change only what it says.
        //   read by the shell: every page's Title, Search and SoonDrawn (PanelNav, PanelSearch, PanelAttention);
        //     PanelSettings.NightModeTitle (Sidebar); PanelShortcuts.Title (Bindings); PanelAddScreen.Reinstalled,
        //     ReinstallFailed and KindName (Profiles, PanelCopy); PanelLightRows.RowPlan, OutdatedBars and
        //     FanatecSuffix (Profiles, PanelLights); PanelReorder.TargetIndex (Widgets.Kit); PanelRigMap.GridStep
        //     (Widgets.Lights: DotGrid snaps to it) and RealHardwareTitle (PanelSoon); PanelLeds.StripUpdateRoute
        //     (PanelAttention).
        //   read by another page: PanelScreens.NoScreens and Title, PanelLeds.NoStrips, PanelMatrix.NoPanels,
        //     PanelRigMap.Title, PanelSettings.BrightnessTitle, NightBrightnessTitle and NightModeTitle (Home,
        //     Rig); PanelFacePlan.ZoneLabel and ZoneOrder, PanelShortcuts.QuickGlanceTitle (Shortcuts, Screens);
        //     PanelDataTab.RevBarTitle, RevBarCaption, RevBarValues and RevBarLabels (Screens);
        //     PanelLightRows.DotHex, FlagBoxCaption and ShapeLabel, PanelUpdates.AnchorLights (Matrix, LEDs).
        // A page's greyed rows and its search-label exemptions are its own: SoonDrawn and
        // SearchDrawnOtherwise in its Panel<Page>.cs. All pages share one partial class, so every member a
        // page adds for itself is private and carries its page's name as a prefix (ScreensTile,
        // LedsDeviceList, MatrixIdleRow), or lives in a private nested class named for the page; two agents
        // writing the same unprefixed helper would only meet at the merge.

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
            var panelFocused = IsKeyboardFocusWithin;
            var sidebarFocused = sidebarHost.IsKeyboardFocusWithin;
            CloseSheet();
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
            // Focus follows the press: to the new page's item when it came from the sidebar, and to the new
            // page's first control when it came from a page or a sheet, whose control has just been drawn away.
            if (sidebarFocused) FocusNavItem(route.Page);
            else if (panelFocused) FocusPageStart();
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
            // Nothing to rebuild before the first page: that one is built at the control's first real width.
            if (!pageBuilt) return;
            var focus = pageHost.IsKeyboardFocusWithin ? FocusPath(pageHost, Keyboard.FocusedElement as DependencyObject) : null;
            CommitTyping();
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
        /// Has the page's text box that is being typed in save what it holds, before a rebuild draws the page
        /// again from the settings.
        /// </summary>
        /// <remarks>
        /// A number box saves on Enter or on losing focus, and the box being replaced lost focus only after
        /// the new page was built: the new box showed the old value, took focus, and saved that old value
        /// over the typed one when the driver clicked away. A resize settling or a wheel's lighting press on
        /// Home or Settings lost an edit without a word. Taking logical focus off the box raises its
        /// LostFocus now, so the settings hold the typed value when the rebuild reads them; RestoreFocus
        /// then puts keyboard focus back at the same place in the new build.
        /// </remarks>
        private void CommitTyping()
        {
            var typing = Keyboard.FocusedElement as TextBox;
            if (typing == null)
            {
                var pageScope = FocusManager.GetFocusScope(pageHost);
                typing = pageScope == null ? null : FocusManager.GetFocusedElement(pageScope) as TextBox;
            }
            if (typing == null || !pageHost.IsAncestorOf(typing)) return;
            var scope = FocusManager.GetFocusScope(typing);
            if (scope != null) FocusManager.SetFocusedElement(scope, null);
            if (typing.IsKeyboardFocused) Keyboard.ClearFocus();
        }

        /// <summary>
        /// Shows a change to the rig's lighting: a press here, or the wheel's night mode and brightness
        /// buttons, which the plugin reports through <see cref="OpenDash.RigLightingPressed"/>. The sidebar's
        /// switch is set in place, and the page is rebuilt only when it draws lighting, once a burst of
        /// presses has stopped.
        /// </summary>
        /// <remarks>
        /// It rebuilt the sidebar and whatever page was showing on every press, on SimHub's interface thread
        /// while the driver was on track: Screens reloaded its live preview and Updates unpacked every strip
        /// profile. LEDs and Matrix draw lighting only as the dim on their pictures, and their builds walk
        /// SimHub's LED devices and read its matrix profiles, so they repaint that dim in place through
        /// <see cref="OnLighting"/> and are never rebuilt by a press. Only Home, Rig and Settings, which draw
        /// the switch or a brightness as a control, call <see cref="DrawsLighting"/> and are rebuilt, once a
        /// burst of presses has stopped.
        /// </remarks>
        private void ShowLightingChange()
        {
            SyncNightSwitch();
            Run(lightingActions.ToList(), "Repainting a page's lighting failed: ");
            if (!pageDrawsLighting) return;
            if (lightingSettle == null)
            {
                lightingSettle = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(PanelShell.LightingSettleMs) };
                lightingSettle.Tick += (sender, args) =>
                {
                    lightingSettle.Stop();
                    if (IsLoaded && pageDrawsLighting) RebuildPage();
                };
            }
            lightingSettle.Stop();
            lightingSettle.Start();
        }

        /// <summary>
        /// Called by a page while it is built when it draws night mode or a brightness as a control, so a
        /// change to either rebuilds it. A page that does not call it is not rebuilt by a wheel press. Not for
        /// a page whose build asks SimHub anything: that page repaints through <see cref="OnLighting"/>.
        /// </summary>
        private void DrawsLighting()
        {
            pageDrawsLighting = true;
        }

        /// <summary>
        /// Asks for something this build drew to be repainted in place when night mode or a brightness
        /// changes -- the dim on a picture of a strip or a matrix -- without rebuilding the page. Run at once
        /// on every change, so it must be cheap: a property set, never a read of SimHub. Cleared with the
        /// build.
        /// </summary>
        private void OnLighting(Action repaint)
        {
            if (repaint != null) lightingActions.Add(repaint);
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

        /// <summary>Whether the sidebar is a rail of icons. It says nothing about columns: between 1000 and
        /// 1080 px the sidebar is full and Narrow is false, yet there is not the room for two blocks side by
        /// side. Ask <see cref="TwoColumns"/> for that.</summary>
        private bool Narrow => PanelShell.IsNarrow(layout);

        /// <summary>Whether a page may lay two blocks side by side, and the only test for it: the full sidebar
        /// and at least <see cref="PanelShell.TwoColumnFrom"/> of content. When it is false a page stacks.</summary>
        private bool TwoColumns => PanelShell.TwoColumns(layout, ContentWidth);

        /// <summary>
        /// Asks for something to be undone when the page is left for another: a press it is waiting on, a
        /// watch it set. Not run when the page is rebuilt in place (a resize, night mode, Redraw).
        /// </summary>
        /// <remarks>
        /// Keyed, because a page calls it while it is built and a page is built again in place on every
        /// resize, Redraw and return to the panel: an unkeyed list gained a copy each time, and the undo ran
        /// once per build on the next Go. Registering a key that is already held replaces its action, so
        /// each key's undo runs once. Name the key for the page and the thing (e.g. "Updates.applyWaiting").
        /// </remarks>
        private void OnLeave(string key, Action undo)
        {
            if (key == null || undo == null) return;
            leaveActions.RemoveAll(pair => string.Equals(pair.Key, key, StringComparison.Ordinal));
            leaveActions.Add(new KeyValuePair<string, Action>(key, undo));
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
            var actions = leaveActions.Select(pair => pair.Value).ToList();
            leaveActions.Clear();
            Run(actions, "Leaving a page failed to undo something: ");
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

        /// <summary>Puts keyboard focus on the first control of the page that is showing, once it is laid out.</summary>
        private void FocusPageStart()
        {
            Dispatcher.BeginInvoke(new Action(() => pageHost.MoveFocus(new TraversalRequest(FocusNavigationDirection.First))), DispatcherPriority.Loaded);
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
            pageDrawsLighting = false;
            lightingActions.Clear();
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
                // A preview the build started before it threw was never added to the tree, so its own
                // Unloaded would never dispose it: SimHub's renderer would run behind the error page.
                DropPreview();
                return Ui.VStack(12,
                    Ui.PageTitle(PanelNav.Label(to.Page)),
                    Ui.Prose(PanelShell.PageFailed, Theme.SizeBody, Theme.Caution));
            }
        }

        /// <summary>
        /// A page as the artboards lay one out: the title, with the page's own actions to its right, and
        /// the sections under it as far apart as that page's artboard puts them
        /// (<see cref="PanelShell.SectionGapFor"/>).
        /// </summary>
        private FrameworkElement PageLayout(string title, FrameworkElement actions, params UIElement[] sections)
        {
            var head = actions == null ? (FrameworkElement)Ui.PageTitle(title) : Ui.Row(Ui.PageTitle(title), actions);
            var gap = PanelShell.SectionGapFor(route.Page);
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(head);
            foreach (var section in sections)
            {
                if (section == null) continue;
                var element = section as FrameworkElement;
                if (element != null)
                {
                    var margin = element.Margin;
                    element.Margin = new Thickness(margin.Left, margin.Top + gap, margin.Right, margin.Bottom);
                }
                stack.Children.Add(section);
            }
            return stack;
        }

        /// <summary>A section of a page: its heading at 17, and what is under it 14 below.</summary>
        private static FrameworkElement PageSection(string heading, params UIElement[] children)
        {
            return PageSection(heading, false, PanelKit.SectionHeadingGap, children);
        }

        /// <summary>A section of a page with its artboard's own heading: at 19 when <paramref name="large"/>,
        /// and <paramref name="gapBelow"/> over what is under it. Settings.dc.html's h2 is 19 with 12 under
        /// (PanelKit.SectionHeadingGapSettings).</summary>
        private static FrameworkElement PageSection(string heading, bool large, double gapBelow, params UIElement[] children)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            if (!string.IsNullOrEmpty(heading))
            {
                var title = Ui.Heading(heading, large);
                title.Margin = new Thickness(0, 0, 0, gapBelow);
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

        /// <summary>A segmented choice at the kit's numbers, or at a page's (PanelKit.SegmentedHeightMatrix,
        /// SegmentedPaddingScreens, SegmentedPaddingShortcuts). The labels are positional.</summary>
        private static Segmented BuildSegmented(string[] values, string[] labels, string selected, Action<string> changed, double height = Segmented.BarHeight, double padding = Segmented.OptionPadding)
        {
            var options = values.Select((value, i) => new Segmented.Option(value, labels[i]));
            var control = new Segmented(options, selected, height, padding);
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
        /// second-guessed. The dialog writes into Model.Triggers, and SimHub's Change command edits a mapping
        /// already there in place (btnChange_Click in 9.12.6 sets trigger.PressType and leaves the collection
        /// alone), so this watches the model, its collection, and every mapping in it.
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
            // follows Model wherever it goes, and Triggers when the model replaces it.
            ControlsEditorModel watched = null;
            System.Collections.ObjectModel.ObservableCollection<InputMapping> watchedTriggers = null;
            var watchedMappings = new List<InputMapping>();
            System.ComponentModel.PropertyChangedEventHandler pressTypeChanged = null;
            System.ComponentModel.PropertyChangedEventHandler modelChanged = null;
            System.Collections.Specialized.NotifyCollectionChangedEventHandler collectionChanged = null;

            Action<InputMapping> hold = mapping =>
            {
                if (mapping != null && mapping.PressType != PressType.During) mapping.PressType = PressType.During;
            };
            // Every mapping in the collection is watched for its press type, and the watch moves with the
            // collection: what left it is let go of, what joined it is watched and corrected.
            Action rewatchMappings = () =>
            {
                foreach (var mapping in watchedMappings) mapping.PropertyChanged -= pressTypeChanged;
                watchedMappings.Clear();
                if (watchedTriggers == null) return;
                foreach (var mapping in watchedTriggers)
                {
                    if (mapping == null) continue;
                    mapping.PropertyChanged += pressTypeChanged;
                    watchedMappings.Add(mapping);
                    hold(mapping);
                }
            };
            Action rewatchTriggers = () =>
            {
                var triggers = watched == null ? null : watched.Triggers;
                if (!ReferenceEquals(triggers, watchedTriggers))
                {
                    if (watchedTriggers != null) watchedTriggers.CollectionChanged -= collectionChanged;
                    watchedTriggers = triggers;
                    if (watchedTriggers != null) watchedTriggers.CollectionChanged += collectionChanged;
                }
                rewatchMappings();
            };
            pressTypeChanged = (sender, args) =>
            {
                if (args.PropertyName == null || args.PropertyName == "PressType") hold(sender as InputMapping);
            };
            collectionChanged = (sender, args) => rewatchMappings();
            modelChanged = (sender, args) =>
            {
                if (args.PropertyName == null || args.PropertyName == "Triggers") rewatchTriggers();
            };
            Action attach = () =>
            {
                var model = editor.Model;
                if (ReferenceEquals(model, watched)) return;
                if (watched != null) watched.PropertyChanged -= modelChanged;
                watched = model;
                if (watched != null) watched.PropertyChanged += modelChanged;
                rewatchTriggers();
            };
            // The mappings are SimHub's for the whole session (ControlsEditorModel's Triggers is a new
            // collection over PluginManager's own InputActionMapping), and a mapping's PropertyChanged holds
            // its handlers strongly. A handler left on one after the editor has gone kept the editor, and the
            // whole page it sat on, alive until SimHub closed, and ran once more per discarded page on every
            // press type change; so the watch is let go of whenever the editor leaves the tree, and Loaded
            // (or the next Model) takes it up again.
            Action detach = () =>
            {
                foreach (var mapping in watchedMappings) mapping.PropertyChanged -= pressTypeChanged;
                watchedMappings.Clear();
                if (watchedTriggers != null) watchedTriggers.CollectionChanged -= collectionChanged;
                watchedTriggers = null;
                if (watched != null) watched.PropertyChanged -= modelChanged;
                watched = null;
            };
            editor.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == null || args.PropertyName == "Model") attach();
            };
            editor.Loaded += (sender, args) => attach();
            editor.Unloaded += (sender, args) => detach();
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
