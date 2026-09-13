// SettingsControl.cs: the OpenDash page in SimHub's left menu, built in code from design/canvas/Plugin.dc.html:
// header (mark, wordmark, plugin version), then General, Data, Zones, Buttons, Layout, Companion, Pit wall
// and Dashboard sections, then the footer. Zones is the picture of the face; Layout is the twelve-slot
// picture it replaces, which stays while both models ship and leaves with the cards in XOR-95.
// Every change writes the settings object and saves it at once; the attached properties read the same object.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;
using SimHub.Plugins;
using SimHub.Plugins.Styles;
using SimHub.Plugins.UI;

namespace OpenDashPlugin
{
    public class SettingsControl : UserControl
    {
        public const string DocumentationUrl = "https://github.com/xorob0/OpenDash#readme";
        public const string IssuesUrl = "https://github.com/xorob0/OpenDash/issues";

        private const double PageWidth = 960;
        private const double PagePadding = 32;

        private readonly OpenDash plugin;
        private TextBlock warningText;
        private FrameworkElement warningRow;
        private TextBlock dashboardTitle;
        private Border statusHost;
        private Button reinstallButton;

        private OpenDashSettings Settings => plugin.Settings;

        public SettingsControl(OpenDash plugin)
        {
            this.plugin = plugin;
            Background = Ui.Brush(Theme.SurfaceBase);
            Foreground = Ui.Brush(Theme.TextPrimary);
            FontFamily = PanelFonts.Label;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            Content = BuildPage();
            RefreshWarning();
            RefreshFaceWarning();
            RefreshStatus();
            RefreshUpdateLine();
            RefreshRestoreButton();
            // Opening the page is the earliest a check may run: never on the startup path, and never at all unless
            // the setting says so. A background check that finds nothing shows nothing, so most visits say nothing.
            Check(manual: false);
        }

        // Page

        private UIElement BuildPage()
        {
            var page = new DockPanel { LastChildFill = true, MaxWidth = PageWidth, HorizontalAlignment = HorizontalAlignment.Left };
            var header = BuildHeader();
            DockPanel.SetDock(header, Dock.Top);
            var footer = BuildFooter();
            DockPanel.SetDock(footer, Dock.Bottom);
            var body = Ui.VStack(0, BuildGeneral(), BuildData(), BuildZones(), BuildButtons(), BuildLayout(), BuildCompanion(), BuildPitWall(), BuildLights(), BuildDashboard());
            body.Margin = new Thickness(PagePadding, 0, PagePadding, 0);
            body.VerticalAlignment = VerticalAlignment.Top;
            page.Children.Add(header);
            page.Children.Add(footer);
            page.Children.Add(body);

            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = page,
            };
            // The footer sits at the bottom of the viewport when the content is shorter (margin-top: auto).
            page.SetBinding(MinHeightProperty, new Binding("ViewportHeight") { Source = scroller });
            return scroller;
        }

        /// <summary>72 px: the segment mark and the wordmark on the left, "PLUGIN" and the version on the right.</summary>
        private FrameworkElement BuildHeader()
        {
            var wordmark = Ui.HStack(0,
                Ui.Text("Open", Theme.SizeWordmark, FontWeights.Light, Theme.TextPrimary, PanelFonts.Data),
                Ui.Text("Dash", Theme.SizeWordmark, FontWeights.Bold, Theme.TextPrimary, PanelFonts.Data));
            var left = Ui.HStack(12, Ui.Mark(), wordmark);
            var right = Ui.HStack(8, Ui.Label("Plugin"), Ui.Numeral(OpenDash.Version, Theme.SizeNumeral, Theme.TextSecondary));
            var grid = Ui.Row(left, right);
            grid.Height = 72;
            grid.Margin = new Thickness(PagePadding, 0, PagePadding, 0);
            return grid;
        }

        // General

        private FrameworkElement BuildGeneral()
        {
            var toggle = BuildToggle(Settings.ShiftLights, on =>
            {
                Settings.ShiftLights = on;
                plugin.SaveSettings();
            });
            return Ui.Section("General",
                Ui.Row("Shift lights on the dash", "Turn off if your DDU has physical LEDs. The rev bar stays.", toggle));
        }

        /// <summary>SimHub's own switch (SHToggleButton), so that it looks like every other toggle in SimHub.</summary>
        private static ToggleButton BuildToggle(bool isOn, Action<bool> changed)
        {
            ToggleButton toggle;
            try
            {
                toggle = new SHToggleButton();
            }
            catch (Exception ex)
            {
                Log.Warn("SHToggleButton is unavailable; using a plain toggle: " + ex.Message);
                toggle = new ToggleButton();
            }
            toggle.IsChecked = isOn;
            toggle.Checked += (sender, args) => changed(true);
            toggle.Unchecked += (sender, args) => changed(false);
            return toggle;
        }

        // Data

        private FrameworkElement BuildData()
        {
            var position = BuildSegmented(Contract.PositionModes, new[] { "Overall", "Class" }, Settings.PositionMode, value =>
            {
                Settings.PositionMode = value;
                plugin.SaveSettings();
            });
            var delta = BuildSegmented(Contract.DeltaReferences, new[] { "Session best", "All-time best" }, Settings.DeltaReference, value =>
            {
                Settings.DeltaReference = value;
                plugin.SaveSettings();
            });
            var session = BuildSegmented(Contract.SessionProgressModes, new[] { "Auto", "Laps", "Time" }, Settings.SessionProgress, value =>
            {
                Settings.SessionProgress = value;
                plugin.SaveSettings();
            });
            return Ui.Section("Data",
                Ui.Row("Position", "Overall, or within your class.", position),
                Ui.Row("Delta reference", "Which lap the delta compares against.", delta),
                Ui.Row("Session progress", "Auto shows laps when the session declares a lap count, time otherwise.", session));
        }

        private static Segmented BuildSegmented(string[] values, string[] labels, string selected, Action<string> changed)
        {
            var options = values.Select((value, i) => new Segmented.Option(value, labels[i]));
            var control = new Segmented(options, selected);
            control.Changed += changed;
            return control;
        }

        // Layout

        private FrameworkElement BuildLayout()
        {
            var caption = Ui.Caption("Any card in any slot. The same card may be assigned twice; the panel says so and does not prevent it.", 846);
            var sizes = Ui.Caption("The picture is the 1920 x 480 face. A face with fewer slots uses the first ones: a 6-slot face shows Slot 01 to Slot 06.", 846);
            return Ui.Section("Layout", caption, BuildSlotPicker(), sizes, BuildWarning());
        }

        /// <summary>A picture of the dash: the left 3 x 2 grid, the hero column, the right 3 x 2 grid (846 x 165).</summary>
        private FrameworkElement BuildSlotPicker()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(BuildSlotGrid(1));
            row.Children.Add(BuildHeroCell());
            row.Children.Add(BuildSlotGrid(7));
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = row,
            };
        }

        /// <summary>Six slots, three per row, separated by 1 px rules drawn by the grid's background.</summary>
        private FrameworkElement BuildSlotGrid(int firstSlot)
        {
            var grid = new Grid { Width = 337, Height = 165, Background = Ui.Brush(Theme.Rule) };
            for (var c = 0; c < 3; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var r = 0; r < 2; r++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < 6; i++)
            {
                var column = i % 3;
                var rowIndex = i / 3;
                var cell = BuildSlotCell(firstSlot + i);
                cell.Margin = new Thickness(column > 0 ? 1 : 0, rowIndex > 0 ? 1 : 0, 0, 0);
                Grid.SetColumn(cell, column);
                Grid.SetRow(cell, rowIndex);
                grid.Children.Add(cell);
            }
            return grid;
        }

        /// <summary>"SLOT 01" at the top, the card select at the bottom, 6 px padding.</summary>
        private FrameworkElement BuildSlotCell(int slot)
        {
            var label = Ui.Label("Slot " + slot.ToString("00"));
            var picker = BuildSlotSelect(slot);
            var dock = new DockPanel { LastChildFill = false };
            DockPanel.SetDock(label, Dock.Top);
            DockPanel.SetDock(picker, Dock.Bottom);
            dock.Children.Add(label);
            dock.Children.Add(picker);
            return new Border { Background = Ui.Brush(Theme.SurfaceBase), Padding = new Thickness(6), Child = dock };
        }

        /// <summary>Every card in card-number order, so that SelectedIndex is the card number.</summary>
        private ComboBox BuildSlotSelect(int slot)
        {
            var box = new ComboBox
            {
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = slot,
                ToolTip = "Card shown in slot " + slot,
            };
            foreach (var card in Cards.All) box.Items.Add(card.DisplayName);
            box.SelectedIndex = Settings.Slot(slot);
            box.SelectionChanged += OnSlotChanged;
            return box;
        }

        private void OnSlotChanged(object sender, SelectionChangedEventArgs args)
        {
            var box = (ComboBox)sender;
            if (box.SelectedIndex < 0) return;
            Settings.SetSlot((int)box.Tag, box.SelectedIndex);
            plugin.SaveSettings();
            RefreshWarning();
        }

        private static FrameworkElement BuildHeroCell()
        {
            var label = Ui.Label("Hero");
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            return new Border
            {
                Width = 170,
                Height = 165,
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(1, 0, 1, 0),
                Child = label,
            };
        }

        // Zones
        //
        // The picture from design/canvas/Plugin.dc.html: the rev bar, the bar, zones B, A and C across
        // the body and band D at the foot, at the sizes the artboard gives them (844 wide, rules at one
        // pixel). It is a plan of the face rather than a list, because "zone C" means nothing until you
        // see where zone C is.

        private const double FaceWidth = 844;
        private const double FaceRevBarHeight = 19;
        private const double FaceBarHeight = 24;
        private const double FaceBodyHeight = 138;
        private const double FaceBandHeight = 26;
        private const double FaceCellPadding = 6;

        /// <summary>
        /// The face this section is configuring.
        ///
        /// Every face has its own zones, its own bar and its own glance, so the panel has to say which
        /// one it is showing and let a driver with two screens reach the second. The reference face is
        /// where it opens, because that is the one nearly every rig has.
        /// </summary>
        private Contract.FaceSize face = Contract.ReferenceFace;

        /// <summary>The face plan and the wheel bindings, both rebuilt when the chosen face changes.</summary>
        private readonly ContentControl faceHost = new ContentControl();
        private readonly ContentControl bindingHost = new ContentControl();

        private readonly Dictionary<string, ComboBox> zoneSelects = new Dictionary<string, ComboBox>();
        private readonly Dictionary<string, ToggleButton> zoneMaskButtons = new Dictionary<string, ToggleButton>();
        private readonly Dictionary<string, List<CheckBox>> zoneMaskBoxes = new Dictionary<string, List<CheckBox>>();
        private readonly Dictionary<string, CheckBox> zoneClassBoxes = new Dictionary<string, CheckBox>();
        private readonly Dictionary<string, ToggleButton> barEndButtons = new Dictionary<string, ToggleButton>();
        private TextBlock faceWarningText;
        private FrameworkElement faceWarningRow;

        private FrameworkElement BuildZones()
        {
            var caption = Ui.Caption(
                "Each zone opens on the page chosen here and cycles through the ones left enabled. "
                + "The same page may sit in two zones; the panel says so and does not prevent it.",
                846);
            stripCaption = Ui.Caption("", 846);
            // The warning row is built before RebuildFace fills the hosts, because RebuildFace refreshes
            // it: doing it the other way round dereferenced a control that did not exist yet, and the
            // whole panel came up as "OpenDash settings could not be displayed".
            var picker = BuildFacePicker();
            var warning = BuildFaceWarning();
            RebuildFace();
            return Ui.Section("Zones", caption, picker, faceHost, stripCaption, warning);
        }

        /// <summary>
        /// Which face the zones below belong to.
        /// </summary>
        /// <remarks>
        /// Choosing one rebuilds the plan beneath as well as the controls, so the picture is the face
        /// that is being configured: the portrait reads as a column, and the nano at 800 x 286 shows
        /// no bar because it has none. The shape comes from the contract, which carries just enough
        /// of it for a plan, since the plugin cannot read a layout file.
        /// </remarks>
        private FrameworkElement BuildFacePicker()
        {
            var box = new ComboBox
            {
                Width = 160,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            foreach (var size in Contract.FaceSizes) box.Items.Add(size.ToString());
            box.SelectedIndex = 0;
            box.SelectionChanged += (sender, args) =>
            {
                if (box.SelectedIndex < 0) return;
                face = Contract.FaceSizes[box.SelectedIndex];
                RebuildFace();
            };
            return Ui.Row(
                "Screen",
                "Each screen keeps its own zones, bar and glance, so a face on the wheel and one beside it "
                    + "are set up apart rather than moving together.",
                box);
        }

        /// <summary>
        /// Draws the plan and the bindings for whichever face is chosen.
        ///
        /// The caches are cleared first: they are keyed by zone letter, so a control left in them from
        /// the previous face would be refreshed instead of the one on screen.
        /// </summary>
        private void RebuildFace()
        {
            zoneSelects.Clear();
            zoneMaskButtons.Clear();
            zoneMaskBoxes.Clear();
            zoneClassBoxes.Clear();
            barEndButtons.Clear();
            faceHost.Content = BuildFacePicture();
            RefreshStripCaption();
            bindingHost.Content = BuildZoneBindings();
            RefreshFaceWarning();
        }

        private TextBlock stripCaption;

        /// <summary>What the bar is, said for the face on screen rather than for every face at once.</summary>
        private void RefreshStripCaption()
        {
            if (stripCaption == null) return;
            stripCaption.Text = face.HasBar
                ? "The bar does not cycle: it carries what does not change during a lap. "
                    + (face.BarFieldsPerEnd == 1 ? "One field at each end, " : "Two fields at each end, ")
                    + "and between them the car settings your sim publishes \u2014 slip, TC, cut, bias, ABS, map and diff. "
                    + "A setting the sim has no value for takes its cell with it rather than leaving an empty box."
                : "This screen has no bar: at " + face + " the height is not there, so the body and band D have it instead.";
        }

        /// <summary>
        /// A plan of the chosen face: the rev bar, the bar if it has one, the body and band D.
        ///
        /// Drawn to that face's own proportions rather than to the reference face's, so the portrait
        /// reads as a column and the nano at 800 x 286 shows no bar, because it has none and offering
        /// its four bar fields was offering settings that do nothing.
        /// </summary>
        private FrameworkElement BuildFacePicture()
        {
            var grid = new Grid { Width = FaceWidth, Background = Ui.Brush(Theme.Rule), HorizontalAlignment = HorizontalAlignment.Left };
            var rows = new List<double> { FaceRevBarHeight, 1 };
            if (face.HasBar) rows.AddRange(new double[] { FaceBarHeight, 1 });
            rows.AddRange(new double[] { BodyHeightFor(face), 1, FaceBandHeight });
            foreach (var height in rows) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height) });

            var row = 0;
            AddAt(grid, BuildRevBarStrip(), row);
            row += 2;
            if (face.HasBar)
            {
                AddAt(grid, BuildBarStrip(), row);
                row += 2;
            }
            AddAt(grid, BuildFaceBody(), row);
            AddAt(grid, BuildBandStrip(), row + 2);
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = grid,
            };
        }

        /// <summary>
        /// How tall to draw the body.
        ///
        /// A stacked body needs room for three cells rather than one, so it is given the sum of its
        /// parts scaled to the plan's width; a row keeps the artboard's height, which is what the
        /// numbers above were read off.
        /// </summary>
        private static double BodyHeightFor(Contract.FaceSize f)
        {
            if (f.Body != Contract.FaceBody.Column) return FaceBodyHeight;
            var total = 0;
            foreach (var part in f.Parts) total += part;
            // The plan is FaceWidth wide whatever the face is, so the body keeps the face's own ratio
            // of body height to width.
            return Math.Max(FaceBodyHeight, Math.Round(FaceWidth * ((double)total / f.Width) / 2));
        }

        private static void AddAt(Grid grid, UIElement child, int row)
        {
            Grid.SetRow(child, row);
            grid.Children.Add(child);
        }

        /// <summary>The rev bar, which is the one part of the face that is not configurable here: whether it
        /// draws at all is the General section's toggle, and what it draws is the car's own shift points.</summary>
        private static FrameworkElement BuildRevBarStrip()
        {
            return new Border
            {
                Background = Ui.Brush(Theme.SurfaceInset),
                Padding = new Thickness(8, 0, 8, 0),
                Child = Ui.Label("Rev bar"),
            };
        }

        /// <summary>The bar: an end at each side and the car settings strip between them.</summary>
        private FrameworkElement BuildBarStrip()
        {
            // One field per end in portrait, two on a wide face, which is what the artboards draw.
            var single = face.BarFieldsPerEnd == 1;
            var left = BuildBarEnd("Left1", single ? null : "Left2", 120);
            var right = BuildBarEnd("Right1", single ? null : "Right2", 130);
            var middle = Ui.Label("Car settings");
            middle.HorizontalAlignment = HorizontalAlignment.Center;
            var dock = new DockPanel { LastChildFill = true, Margin = new Thickness(8, 0, 8, 0) };
            DockPanel.SetDock(left, Dock.Left);
            DockPanel.SetDock(right, Dock.Right);
            dock.Children.Add(left);
            dock.Children.Add(right);
            dock.Children.Add(middle);
            return new Border { Background = Ui.Brush(Theme.SurfaceInset), Child = dock };
        }

        /// <summary>
        /// One end of the bar. The canvas draws a single control reading "Race · lap", and an end really
        /// carries two fields, so the control opens a panel with a picker for each rather than splitting
        /// into two boxes the artboard does not have.
        /// </summary>
        private FrameworkElement BuildBarEnd(string firstSlot, string secondSlot, double width)
        {
            var button = Ui.DropButton(width, BarEndCaption(firstSlot, secondSlot), secondSlot == null ? "The field this end of the bar shows" : "The two fields this end of the bar shows");
            barEndButtons[firstSlot] = button;
            var rows = new List<UIElement>();
            var slots = secondSlot == null ? new[] { firstSlot } : new[] { firstSlot, secondSlot };
            foreach (var slot in slots)
            {
                var captured = slot;
                var select = BuildPageSelect(FacePages.BarFields, Settings.BarField(face, captured), 200, index =>
                {
                    Settings.SetBarField(face, captured, index);
                    plugin.SaveSettings();
                    Ui.SetDropText(button, BarEndCaption(firstSlot, secondSlot));
                });
                rows.Add(Ui.Row(Ui.Label(secondSlot == null ? "Field" : slot == firstSlot ? "First" : "Second"), select));
            }
            var panel = Ui.VStack(8, rows.ToArray());
            panel.Width = 260;
            return Ui.Drop(button, panel);
        }

        private string BarEndCaption(string firstSlot, string secondSlot)
        {
            var first = FacePages.FieldName(Settings.BarField(face, firstSlot));
            return secondSlot == null ? first : FacePages.EndLabel(first, FacePages.FieldName(Settings.BarField(face, secondSlot)));
        }

        /// <summary>
        /// The three body zones, in the order and along the axis this face draws them: B, A and C
        /// across a wide face, A over B over C in portrait.
        /// </summary>
        private FrameworkElement BuildFaceBody()
        {
            var grid = new Grid { Background = Ui.Brush(Theme.Rule) };
            var letters = face.BodyOrder;
            var stacked = face.Body == Contract.FaceBody.Column;
            // The parts are the face's real pixels; the plan is 844 wide, so they are shared out in
            // proportion rather than used as they are.
            var total = 0;
            foreach (var part in face.Parts) total += part;
            var span = stacked ? BodyHeightFor(face) : FaceWidth;
            var sizes = new double[letters.Length];
            for (var i = 0; i < letters.Length; i++) sizes[i] = Math.Round((span - 2) * face.Parts[i] / (double)total);

            for (var i = 0; i < letters.Length; i++)
            {
                if (stacked)
                {
                    if (i > 0) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(sizes[i]) });
                }
                else
                {
                    if (i > 0) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(sizes[i]) });
                }
            }
            for (var i = 0; i < letters.Length; i++)
            {
                // A stacked cell is as wide as the plan; a cell in a row is as wide as its share.
                var cell = BuildZoneCell(letters[i], stacked ? FaceWidth : sizes[i]);
                if (stacked) Grid.SetRow(cell, i * 2);
                else Grid.SetColumn(cell, i * 2);
                grid.Children.Add(cell);
            }
            return grid;
        }

        /// <summary>"ZONE B" at the top, the enabled pages and the start page at the bottom, and the
        /// class filter under them where the zone has a page it changes.</summary>
        private FrameworkElement BuildZoneCell(string letter, double width)
        {
            var inner = width - 2 * FaceCellPadding;
            var dock = new DockPanel { LastChildFill = false };
            var label = Ui.Label("Zone " + letter);
            DockPanel.SetDock(label, Dock.Top);
            dock.Children.Add(label);

            if (FacePages.OffersClassFilter(letter))
            {
                var classOnly = BuildClassFilterBox(letter);
                classOnly.Margin = new Thickness(0, 6, 0, 0);
                DockPanel.SetDock(classOnly, Dock.Bottom);
                dock.Children.Add(classOnly);
            }

            var select = BuildZoneSelectFor(letter, inner);
            DockPanel.SetDock(select, Dock.Bottom);
            dock.Children.Add(select);

            var mask = BuildMaskDrop(letter, inner);
            mask.Margin = new Thickness(0, 0, 0, 6);
            DockPanel.SetDock(mask, Dock.Bottom);
            dock.Children.Add(mask);

            return new Border
            {
                Background = Ui.Brush(Theme.SurfaceBase),
                Padding = new Thickness(FaceCellPadding),
                Child = dock,
            };
        }

        /// <summary>Band D, across the foot: the same two controls as a body zone, laid along the row.</summary>
        private FrameworkElement BuildBandStrip()
        {
            var row = Ui.HStack(8, Ui.Label("Zone D"), BuildZoneSelectFor("D", 150), BuildMaskDrop("D", 150));
            row.Margin = new Thickness(8, 0, 8, 0);
            return new Border { Background = Ui.Brush(Theme.SurfaceInset), Child = row };
        }

        /// <summary>The page a zone opens on. Writing it moves the live face too, so the dash on the desk
        /// follows the panel rather than waiting for the next session.</summary>
        private ComboBox BuildZoneSelectFor(string letter, double width)
        {
            var pages = FacePages.For(letter);
            var select = BuildPageSelect(pages, Settings.FaceZoneStart(face, letter), width, index =>
            {
                Settings.SetFaceZoneStart(face, letter, index);
                plugin.SaveSettings();
                RefreshZone(letter);
                RefreshFaceWarning();
            });
            select.ToolTip = "The page zone " + letter + " opens on";
            zoneSelects[letter] = select;
            return select;
        }

        /// <summary>
        /// Whether this zone's lists show the player's own class.
        ///
        /// It sits in the zone's own cell rather than beside PositionMode, because it is a property of
        /// the zone and not of the face: the point of it is zone B listing the race while zone C lists
        /// the class a driver is actually racing in.
        /// </summary>
        private CheckBox BuildClassFilterBox(string letter)
        {
            var box = new CheckBox
            {
                Content = "My class only",
                FontSize = Theme.SizeLabel,
                FontFamily = PanelFonts.Label,
                Foreground = Ui.Brush(Theme.TextSecondary),
                IsChecked = Settings.FaceZoneIsClassOnly(face, letter),
                ToolTip = "Show the leaderboard and the relative in zone " + letter + " for your own class",
            };
            box.Checked += (sender, args) => SetClassFilter(letter, true);
            box.Unchecked += (sender, args) => SetClassFilter(letter, false);
            zoneClassBoxes[letter] = box;
            return box;
        }

        private void SetClassFilter(string letter, bool classOnly)
        {
            Settings.SetFaceZoneClassOnly(face, letter, classOnly);
            plugin.SaveSettings();
        }

        /// <summary>A page picker in page-number order, so that SelectedIndex is the page number.</summary>
        private static ComboBox BuildPageSelect(IReadOnlyList<ZonePage> pages, int selected, double width, Action<int> changed)
        {
            var box = new ComboBox
            {
                Width = width,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            foreach (var page in pages) box.Items.Add(page.Name);
            box.SelectedIndex = selected >= 0 && selected < pages.Count ? selected : 0;
            box.SelectionChanged += (sender, args) =>
            {
                if (box.SelectedIndex < 0) return;
                changed(box.SelectedIndex);
            };
            return box;
        }

        /// <summary>
        /// The enabled pages of a zone. This is the control that decides how long a driver's cycle is,
        /// which makes it the most consequential thing on the panel; the canvas does not draw it, and
        /// docs/design/zones.md records that it is owed there.
        /// </summary>
        private FrameworkElement BuildMaskDrop(string letter, double width)
        {
            var button = Ui.DropButton(width, MaskCaption(letter), "Which pages zone " + letter + " cycles through");
            zoneMaskButtons[letter] = button;
            return Ui.Drop(button, BuildMaskPanel(letter));
        }

        private string MaskCaption(string letter)
        {
            var pages = FacePages.For(letter).Count;
            var on = 0;
            for (var page = 0; page < pages; page++)
            {
                if (Settings.FaceZonePageEnabled(face, letter, page)) on++;
            }
            return on + " of " + pages + " pages";
        }

        /// <summary>A checkbox per page, seven to a column, with All and None above them.</summary>
        private FrameworkElement BuildMaskPanel(string letter)
        {
            const int perColumn = 7;
            var pages = FacePages.For(letter);
            var boxes = new List<CheckBox>();
            zoneMaskBoxes[letter] = boxes;

            var grid = new Grid();
            var columns = (pages.Count + perColumn - 1) / perColumn;
            var rows = Math.Min(pages.Count, perColumn);
            for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (var r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var i = 0; i < pages.Count; i++)
            {
                var page = pages[i].Number;
                var box = new CheckBox
                {
                    Content = pages[i].Name,
                    FontSize = Theme.SizeLabel,
                    FontFamily = PanelFonts.Label,
                    Foreground = Ui.Brush(Theme.TextPrimary),
                    IsChecked = Settings.FaceZonePageEnabled(face, letter, page),
                    Margin = new Thickness(0, 0, 20, 6),
                    MinWidth = 120,
                };
                box.Checked += (sender, args) => SetPage(letter, page, true);
                box.Unchecked += (sender, args) => SetPage(letter, page, false);
                boxes.Add(box);
                Grid.SetColumn(box, i / perColumn);
                Grid.SetRow(box, i % perColumn);
                grid.Children.Add(box);
            }

            var all = BuildMaskLink("All", () => SetEveryPage(letter, true));
            var none = BuildMaskLink("None", () => SetEveryPage(letter, false));
            var header = Ui.Row(Ui.Label("Pages zone " + letter + " cycles"), Ui.HStack(12, all, none));
            header.Margin = new Thickness(0, 0, 0, 10);
            return Ui.VStack(0, header, grid);
        }

        private static Button BuildMaskLink(string text, Action clicked)
        {
            var button = new Button
            {
                Content = Ui.Text(text, Theme.SizeLabel, FontWeights.Medium, Theme.Accent),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
            };
            button.Click += (sender, args) => clicked();
            return button;
        }

        private void SetPage(string letter, int page, bool enabled)
        {
            Settings.SetFaceZonePageEnabled(face, letter, page, enabled);
            plugin.SaveSettings();
            RefreshZone(letter);
            RefreshFaceWarning();
        }

        /// <summary>None leaves the page the zone is on enabled, because a zone with an empty cycle has
        /// nothing to draw; the settings object refuses it and the checkbox follows what it decided.</summary>
        private void SetEveryPage(string letter, bool enabled)
        {
            var pages = FacePages.For(letter);
            if (enabled)
            {
                for (var i = 0; i < pages.Count; i++) Settings.SetFaceZonePageEnabled(face, letter, pages[i].Number, true);
            }
            else
            {
                var keep = Settings.FaceZoneStart(face, letter);
                for (var i = 0; i < pages.Count; i++)
                {
                    if (pages[i].Number != keep) Settings.SetFaceZonePageEnabled(face, letter, pages[i].Number, false);
                }
            }
            plugin.SaveSettings();
            RefreshZone(letter);
            RefreshFaceWarning();
        }

        /// <summary>Puts the controls of one zone back in step with the settings, which may have moved on
        /// their own: turning off the page a zone sits on snaps it forward to the next enabled one.</summary>
        private void RefreshZone(string letter)
        {
            ComboBox select;
            if (zoneSelects.TryGetValue(letter, out select))
            {
                var start = Settings.FaceZoneStart(face, letter);
                if (select.SelectedIndex != start) select.SelectedIndex = start;
            }
            ToggleButton button;
            if (zoneMaskButtons.TryGetValue(letter, out button)) Ui.SetDropText(button, MaskCaption(letter));
            List<CheckBox> boxes;
            if (zoneMaskBoxes.TryGetValue(letter, out boxes))
            {
                var pages = FacePages.For(letter);
                for (var i = 0; i < boxes.Count && i < pages.Count; i++)
                {
                    var enabled = Settings.FaceZonePageEnabled(face, letter, pages[i].Number);
                    if (boxes[i].IsChecked != enabled) boxes[i].IsChecked = enabled;
                }
            }
            CheckBox classOnly;
            if (zoneClassBoxes.TryGetValue(letter, out classOnly))
            {
                var on = Settings.FaceZoneIsClassOnly(face, letter);
                if (classOnly.IsChecked != on) classOnly.IsChecked = on;
            }
        }

        /// <summary>"Zone B and zone C both show Relative." Says so and allows it, as the canvas asks.</summary>
        private FrameworkElement BuildFaceWarning()
        {
            faceWarningText = Ui.Text("", Theme.SizeSmall, FontWeights.Normal, Theme.Caution);
            faceWarningText.TextWrapping = TextWrapping.Wrap;
            var icon = Ui.Icon(Ui.WarningIcon, Theme.Caution);
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, 1, 0, 0);
            faceWarningRow = Ui.HStack(10, icon, faceWarningText);
            faceWarningRow.Visibility = Visibility.Collapsed;
            return faceWarningRow;
        }

        private void RefreshFaceWarning()
        {
            var message = FacePageClash.Warning(Settings.Face(face));
            // Called while the panel is still being assembled as well as after it, so a row that does
            // not exist yet is nothing to refresh rather than a crash.
            if (faceWarningText == null || faceWarningRow == null) return;
            faceWarningText.Text = message;
            faceWarningRow.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        // Buttons

        /// <summary>A button per zone to cycle it, and one held for a glance.</summary>
        private FrameworkElement BuildButtons()
        {
            var caption = Ui.Caption("Hold to show one page, release to return. Usually the relative or the map.");
            var zoneBox = new ComboBox
            {
                Width = 110,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            foreach (var letter in Contract.FaceZoneLetters) zoneBox.Items.Add("Zone " + letter);
            zoneBox.SelectedIndex = Contract.QuickGlanceZone(Settings.QuickGlanceOf(face));

            var pageHost = new Border { VerticalAlignment = VerticalAlignment.Center };
            Action fillPages = () =>
            {
                var letter = Contract.FaceZoneLetters[Math.Max(0, zoneBox.SelectedIndex)];
                var page = Contract.QuickGlanceZone(Settings.QuickGlanceOf(face)) == zoneBox.SelectedIndex
                    ? Contract.QuickGlancePage(Settings.QuickGlanceOf(face))
                    : 0;
                pageHost.Child = BuildPageSelect(FacePages.For(letter), page, 200, index =>
                {
                    Settings.SetQuickGlance(face, Contract.QuickGlanceValue(zoneBox.SelectedIndex, index));
                    plugin.SaveSettings();
                });
            };
            zoneBox.SelectionChanged += (sender, args) =>
            {
                if (zoneBox.SelectedIndex < 0) return;
                Settings.SetQuickGlance(face, Contract.QuickGlanceValue(zoneBox.SelectedIndex, 0));
                plugin.SaveSettings();
                fillPages();
            };
            fillPages();

            var text = Ui.VStack(4, Ui.Body("Quick glance"), caption);
            text.MaxWidth = 460;
            text.HorizontalAlignment = HorizontalAlignment.Left;

            var rows = new List<UIElement>
            {
                Ui.Row(
                    "Next page",
                    "Bind a wheel button per zone. A driver cycles a zone without taking a hand off the wheel, "
                        + "which is the whole point of zones.",
                    bindingHost),
                Ui.Row(text, Ui.HStack(8, zoneBox, pageHost)),
                BuildGlanceBinding(),
            };
            return Ui.Section("Buttons", rows.ToArray());
        }

        /// <summary>One binder per zone, stacked, each naming the zone it cycles.</summary>
        private FrameworkElement BuildZoneBindings()
        {
            var binders = Contract.FaceZoneLetters
                .Select(letter => BuildBinder(Contract.CycleZoneAction(face, letter), "Zone " + letter))
                .ToArray();
            var stack = Ui.VStack(6, binders);
            stack.HorizontalAlignment = HorizontalAlignment.Right;
            return stack;
        }

        private FrameworkElement BuildGlanceBinding()
        {
            return Ui.Row(Ui.Label("Glance button"), BuildBinder(Contract.HoldQuickGlanceActionFor(face), "Quick glance", hold: true));
        }

        /// <summary>
        /// SimHub's own control for binding an input to an action, so a wheel button is bound here
        /// rather than by sending the driver to Controls and events to find the name.
        ///
        /// It is a SimHub UserControl and SimHub is not always there -- the panel is constructed in
        /// tests and could be constructed by a host that does not carry the style -- so a failure
        /// falls back to naming the action, which is exactly what somebody binding it by hand needs.
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
                var text = Ui.Text(friendlyName + " — bind " + Contract.FullActionName(action) + " in Controls and events", Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);
                text.HorizontalAlignment = HorizontalAlignment.Right;
                return text;
            }
        }

        // Companion

        /// <summary>The 21 companion modules as toggles in three columns, in page order.</summary>
        private FrameworkElement BuildCompanion()
        {
            var caption = Ui.Caption(
                "Each module is a page of the companion dashboard. A module that is off is skipped when you page with a wheel button. "
                + "Energy, Damage and Track rivals are off because iRacing publishes none of their data; switch them on for a sim that does.",
                846);
            return Ui.Section("Companion", caption, BuildModuleGrid());
        }

        private FrameworkElement BuildModuleGrid()
        {
            const int columns = 3;
            var rows = (Modules.Count + columns - 1) / columns;
            var grid = new Grid { Width = 846, HorizontalAlignment = HorizontalAlignment.Left };
            for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var i = 0; i < Modules.Count; i++)
            {
                var module = Modules.All[i];
                var cell = BuildModuleRow(module);
                Grid.SetColumn(cell, i / rows);
                Grid.SetRow(cell, i % rows);
                grid.Children.Add(cell);
            }
            return grid;
        }

        /// <summary>"07 Tyres" and its toggle; the description is the tooltip, so the grid stays readable.</summary>
        private FrameworkElement BuildModuleRow(Module module)
        {
            var name = Ui.Text(module.Number.ToString("00") + "  " + module.Name, Theme.SizeLabel, FontWeights.Normal, Theme.TextSecondary);
            name.VerticalAlignment = VerticalAlignment.Center;
            var toggle = BuildToggle(Settings.Module(module.Number), on =>
            {
                Settings.SetModule(module.Number, on);
                plugin.SaveSettings();
            });
            toggle.HorizontalAlignment = HorizontalAlignment.Right;
            var row = Ui.Row(name, toggle);
            row.Margin = new Thickness(0, 0, 24, 8);
            row.ToolTip = module.Description;
            return row;
        }

        // Lights

        /// <summary>
        /// The lights page: brightness for the rig, then what each of SimHub's four matrix contents
        /// does. Drawn in docs/design/plugin.md before it was drawn here.
        ///
        /// Rotation and serpentine are not on it. They are SimHub device settings decided by the corner
        /// the data cable enters, and a second place to set them is a second place to disagree; the
        /// guide says where they live instead.
        /// </summary>
        private FrameworkElement BuildLights()
        {
            var rows = new List<UIElement>
            {
                Ui.Caption(
                    "An 8x8 LED matrix beside the screen. openDash builds the profile and puts it where you can find it, "
                        + "but does not install it: SimHub keeps matrix profiles in a file it rewrites itself. Import it once, "
                        + "and everything on this page reaches it while you drive.",
                    846),
                BuildFlagBoxRow(),
                Ui.Row("Brightness", "Percent, for every light openDash drives. SimHub's own device brightness applies on top.", BuildPercentBox(Settings.LightsBrightness, v => { Settings.LightsBrightness = v; plugin.SaveSettings(); })),
                Ui.Row("Night brightness", "Used while night mode is on. 64 LEDs at full output beside a wheel in a dark room is too bright.", BuildPercentBox(Settings.LightsNightBrightness, v => { Settings.LightsNightBrightness = v; plugin.SaveSettings(); })),
                Ui.Row("Night mode", "A switch you flip, not a time of day we guess at.", BuildToggle(Settings.LightsNightMode, on => { Settings.LightsNightMode = on; plugin.SaveSettings(); })),
                Ui.Row("Critical flags only", "Quiet until something matters: drops the chequer, the white, the green and the start gantry.", BuildToggle(Settings.FlagBoxCriticalOnly, on => { Settings.FlagBoxCriticalOnly = on; plugin.SaveSettings(); })),
                Ui.Row("Show the gear", "What the box shows when nothing else is on it. Off leaves it dark.", BuildToggle(Settings.FlagBoxGear, on => { Settings.FlagBoxGear = on; plugin.SaveSettings(); })),
                Ui.Row("Low fuel, laps", "Lights when the laps left in the tank fall under this. Laps, not litres: litres mean nothing without the car.", BuildNumberBox(Settings.FlagBoxLowFuelLaps, 0, 99, v => { Settings.FlagBoxLowFuelLaps = v; plugin.SaveSettings(); })),
                Ui.Row("Oil temperature", "In your own unit; 0 uses the default for it (120 C, 248 F).", BuildNumberBox(Settings.FlagBoxOilTemp, 0, 999, v => { Settings.FlagBoxOilTemp = v; plugin.SaveSettings(); })),
                Ui.Row("Water temperature", "In your own unit; 0 uses the default for it (110 C, 230 F).", BuildNumberBox(Settings.FlagBoxWaterTemp, 0, 999, v => { Settings.FlagBoxWaterTemp = v; plugin.SaveSettings(); })),
                Ui.Caption("SimHub composes up to four matrix contents. Matrix 1 does everything by default; switch on a second only if you own a second box.", 846),
            };
            foreach (var matrix in Contract.FlagBoxMatrices) rows.Add(BuildMatrixRow(matrix));
            return Ui.Section("Lights", rows.ToArray());
        }

        /// <summary>One matrix: what it shows at rest, what may take it over, and which side it is on.</summary>
        private FrameworkElement BuildMatrixRow(int matrix)
        {
            var m = matrix;
            var rest = BuildSegmented(Contract.FlagBoxRests, new[] { "Dark", "Gear" }, Settings.MatrixRest(m), value =>
            {
                Settings.FlagBoxRest[m - 1] = value;
                plugin.SaveSettings();
            });
            var side = BuildSegmented(Contract.FlagBoxSides, new[] { "Both", "Left", "Right" }, Settings.MatrixSide(m), value =>
            {
                Settings.FlagBoxSide[m - 1] = value;
                plugin.SaveSettings();
            });
            var stack = Ui.VStack(
                4,
                Ui.Row("At rest", "What this panel shows when nothing has taken it over.", rest),
                Ui.Row("Flags", "Let the flag catalogue take this panel.", BuildToggle(Settings.MatrixFlags(m), on => { Settings.FlagBoxFlags[m - 1] = on; plugin.SaveSettings(); })),
                Ui.Row("Spotter", "Let a car alongside take this panel.", BuildToggle(Settings.MatrixSpotter(m), on => { Settings.FlagBoxSpotter[m - 1] = on; plugin.SaveSettings(); })),
                Ui.Row("Warnings", "Let low fuel, oil and water take this panel.", BuildToggle(Settings.MatrixWarnings(m), on => { Settings.FlagBoxWarnings[m - 1] = on; plugin.SaveSettings(); })),
                // Which side the box is physically on. One to the left of the wheel lighting for a car on
                // the right is worse than no box at all, so it is asked rather than guessed.
                Ui.Row("Mounted", "Which side of the rig this box is on. A left box must not light for a car on your right.", side));
            stack.Margin = new Thickness(24, 4, 0, 12);
            return Ui.VStack(0, Ui.Caption("Matrix " + m, 846), stack);
        }

        private TextBlock flagBoxLine;
        private Button flagBoxButton;

        /// <summary>
        /// The install row: what SimHub holds now, and one button that changes it.
        ///
        /// openDash hands SimHub a profile object through its own public API and SimHub writes its own
        /// settings file (FlagBoxInstaller.cs); nothing here edits that file. It is a button rather
        /// than something that happens at startup because a profile paints hardware the user owns, and
        /// that is a thing to be asked about once rather than assumed -- ADR 0013.
        /// </summary>
        private FrameworkElement BuildFlagBoxRow()
        {
            flagBoxLine = Ui.Caption("", 460);
            flagBoxButton = BuildSecondaryButton("Install into SimHub", "Adds openDash's profile to SimHub's matrix profiles. It never changes a profile you made yourself.");
            flagBoxButton.Click += (sender, args) => InstallFlagBox();

            var text = Ui.VStack(4, Ui.Body("Flag box profile"), flagBoxLine);
            text.MaxWidth = 460;
            text.HorizontalAlignment = HorizontalAlignment.Left;
            var right = Ui.VStack(4, flagBoxButton, FlagBoxPathBox());
            RefreshFlagBox();
            return Ui.Row(text, right);
        }

        /// <summary>Re-reads SimHub's matrix profiles and repaints the row.</summary>
        private void RefreshFlagBox()
        {
            if (flagBoxLine == null) return;
            var plan = SafePlan();
            flagBoxLine.Text = FlagBoxInstallPlan.Summary(plan, plugin.FlagBox?.Path);
            if (flagBoxButton == null) return;
            flagBoxButton.Content = FlagBoxInstallPlan.ButtonLabel(plan);
            // Nothing to press when there is no profile to install or nowhere to put it.
            var usable = plan != null && plan.State != FlagBoxInstallState.NotEmbedded && plan.State != FlagBoxInstallState.Unavailable;
            flagBoxButton.IsEnabled = usable;
        }

        private FlagBoxPlan SafePlan()
        {
            try
            {
                return FlagBoxInstaller.Plan(plugin.FlagBoxJson);
            }
            catch (Exception ex)
            {
                Log.Warn("Reading SimHub's matrix profiles failed: " + ex.Message);
                return new FlagBoxPlan { State = FlagBoxInstallState.Unavailable };
            }
        }

        private void InstallFlagBox()
        {
            try
            {
                FlagBoxInstaller.Install(plugin.FlagBoxJson);
            }
            catch (Exception ex)
            {
                Log.Error("Installing the flag box profile failed", ex);
            }
            RefreshFlagBox();
        }

        private FrameworkElement FlagBoxPathBox()
        {
            var box = new TextBox
            {
                Width = 320,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsReadOnly = true,
                Text = plugin.FlagBox?.Path ?? string.Empty,
                ToolTip = "Where openDash left the profile. Read-only: copy it, then import it in SimHub.",
            };
            return box;
        }

        private FrameworkElement BuildPercentBox(int value, Action<int> changed) => BuildNumberBox(value, 0, 100, changed);

        /// <summary>A small number field that repairs whatever is typed into it rather than refusing it.</summary>
        private FrameworkElement BuildNumberBox(int value, int min, int max, Action<int> changed)
        {
            var box = new TextBox
            {
                Width = 80,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                HorizontalAlignment = HorizontalAlignment.Right,
                Text = value.ToString(CultureInfo.InvariantCulture),
            };
            Action commit = () =>
            {
                int parsed;
                if (!int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) parsed = value;
                if (parsed < min) parsed = min;
                if (parsed > max) parsed = max;
                box.Text = parsed.ToString(CultureInfo.InvariantCulture);
                changed(parsed);
            };
            box.LostFocus += (sender, args) => commit();
            box.KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter) commit();
            };
            return box;
        }

        // Pit wall

        /// <summary>The four zone pages, the wide zone page and the web view address.</summary>
        private FrameworkElement BuildPitWall()
        {
            var caption = Ui.Caption("Each pit wall page carries four data zones and, on the tower page, one wide zone. Choose what each one shows.", 846);
            var rows = new List<UIElement> { caption };
            foreach (var letter in Contract.PitWallZoneLetters)
            {
                var captured = letter;
                var select = BuildZoneSelect(ZonePages.Standard, Settings.Zone(captured), index =>
                {
                    Settings.SetZone(captured, index);
                    plugin.SaveSettings();
                });
                rows.Add(Ui.Row("Zone " + captured, ZoneDescription(captured), select));
            }
            var wide = BuildZoneSelect(ZonePages.Wide, Settings.WideZone, index =>
            {
                Settings.WideZone = Contract.NormaliseWideZonePage(index);
                plugin.SaveSettings();
            });
            rows.Add(Ui.Row("Wide zone", "The full-width zone on the tower page.", wide));
            rows.Add(Ui.Row("Web view address", "The page the Web view zone shows. http or https only; leave empty for none.", BuildWebViewBox()));
            return Ui.Section("Pit wall", rows.ToArray());
        }

        /// <summary>Where each zone sits, so the letters mean something before the dashboard is open.</summary>
        private static string ZoneDescription(string letter)
        {
            switch (letter)
            {
                case "A": return "Race page, upper right. Telemetry page, top.";
                case "B": return "Race page, lower right. Telemetry page, middle.";
                case "C": return "Tower page, lower left. Telemetry page, bottom.";
                default: return "Tower page, lower right.";
            }
        }

        /// <summary>The pages in page-number order, so that SelectedIndex is the page number.</summary>
        private static ComboBox BuildZoneSelect(IReadOnlyList<ZonePage> pages, int selected, Action<int> changed)
        {
            var box = new ComboBox
            {
                Width = 220,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            foreach (var page in pages) box.Items.Add(page.Name);
            box.SelectedIndex = selected >= 0 && selected < pages.Count ? selected : 0;
            box.SelectionChanged += (sender, args) =>
            {
                if (box.SelectedIndex < 0) return;
                changed(box.SelectedIndex);
            };
            return box;
        }

        /// <summary>The web view address. Written when the box loses focus or Enter is pressed, then normalised.</summary>
        private FrameworkElement BuildWebViewBox()
        {
            var box = new TextBox
            {
                Width = 320,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Text = Settings.WebViewUrl ?? string.Empty,
                ToolTip = "An http or https address; anything else is ignored.",
            };
            Action commit = () =>
            {
                Settings.WebViewUrl = Contract.NormaliseUrl(box.Text);
                plugin.SaveSettings();
                if (box.Text != Settings.WebViewUrl) box.Text = Settings.WebViewUrl;
            };
            box.LostFocus += (sender, args) => commit();
            box.KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter) commit();
            };
            return box;
        }

        /// <summary>
        /// Forces a glance binding to the one press type that can hold anything.
        ///
        /// SimHub only calls an action's start on press and its end on release when the mapping's
        /// press type is `During`; every other type goes through TriggerAction, which fires start and
        /// end back to back. The binding dialog offers ShortAndLongPress by default, so a driver who
        /// binds the glance the obvious way gets a page that appears and vanishes in one frame.
        ///
        /// The glance is only meaningful as a hold, so any binding to it is corrected rather than
        /// second-guessed. The dialog writes into Model.Triggers; this watches that collection.
        /// </summary>
        private static void HoldWhilePressed(ControlsEditor editor)
        {
            var model = editor.Model;
            if (model == null) return;
            Action apply = () =>
            {
                foreach (var mapping in model.Triggers)
                {
                    if (mapping != null && mapping.PressType != PressType.During) mapping.PressType = PressType.During;
                }
            };
            model.Triggers.CollectionChanged += (sender, args) => apply();
            apply();
        }

        /// <summary>The duplicate warning: triangle icon and caution text, hidden while every card is unique.</summary>
        private FrameworkElement BuildWarning()
        {
            warningText = Ui.Text("", Theme.SizeSmall, FontWeights.Normal, Theme.Caution);
            warningText.TextWrapping = TextWrapping.Wrap;
            var icon = Ui.Icon(Ui.WarningIcon, Theme.Caution);
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, 1, 0, 0);
            warningRow = Ui.HStack(10, icon, warningText);
            warningRow.Visibility = Visibility.Collapsed;
            return warningRow;
        }

        private void RefreshWarning()
        {
            var message = DuplicateAssignment.Warning(Settings.Slots);
            warningText.Text = message;
            warningRow.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        // Dashboard

        private TextBlock updateLine;
        private Button updateButton;
        private Button restoreButton;
        private Button checkButton;
        private UpdateStatus updateStatus = new UpdateStatus();
        private UpdateService updateService;
        private bool confirmingEdited;
        private bool applying;
        private bool confirmingReinstall;

        private UpdateService Updates =>
            updateService ?? (updateService = new UpdateService(new ReleaseClient(OpenDash.Version), new SimHubInstallLog()));

        private FrameworkElement BuildDashboard()
        {
            dashboardTitle = Ui.Body("OpenDash");
            var caption = Ui.Caption("One dashboard per screen size, installed in SimHub DashTemplates. Reinstall restores every embedded copy; settings are kept.");
            var text = Ui.VStack(4, dashboardTitle, caption);
            text.MaxWidth = 460;
            text.HorizontalAlignment = HorizontalAlignment.Left;

            updateLine = Ui.Caption(string.Empty);
            updateLine.Visibility = Visibility.Collapsed;
            text.Children.Add(updateLine);

            statusHost = new Border { VerticalAlignment = VerticalAlignment.Center };
            reinstallButton = BuildReinstallButton();
            updateButton = BuildUpdateButton();
            restoreButton = BuildRestoreButton();
            var right = Ui.HStack(24, statusHost, restoreButton, updateButton, reinstallButton);

            return Ui.Section("Dashboard", Ui.Row(text, right), BuildCheckRow());
        }

        /// <summary>The switch, its one sentence, and a button for somebody who would rather ask now.</summary>
        private FrameworkElement BuildCheckRow()
        {
            var toggle = BuildToggle(Settings.CheckForUpdates, on =>
            {
                Settings.CheckForUpdates = on;
                plugin.SaveSettings();
                if (!on)
                {
                    updateStatus = new UpdateStatus { State = UpdateState.Disabled, InstalledVersion = plugin.Installer.InstalledVersion };
                }
                RefreshUpdateLine();
            });

            checkButton = BuildSecondaryButton("Check now", "Ask GitHub for the newest release now, without waiting for the daily check.");
            checkButton.Click += (sender, args) => Check(manual: true);

            var right = Ui.HStack(24, checkButton, toggle);
            return Ui.Row(Ui.VStack(4, Ui.Body("Check for updates"), Ui.Caption(UpdateWording.CheckCaption)), right);
        }

        private Button BuildUpdateButton()
        {
            var button = BuildSecondaryButton("Update", "Download the newest release and replace the dashboards installed here.");
            button.Visibility = Visibility.Collapsed;
            button.Click += (sender, args) => ApplyUpdate();
            return button;
        }

        /// <summary>
        /// Puts back the copy kept when a dashboard somebody edited was replaced.
        /// </summary>
        /// <remarks>
        /// The confirmation before replacing an edited dashboard promises that a copy is kept and can be put back.
        /// Until this existed nothing in the plugin could put one back, so the promise was true only for somebody
        /// willing to unzip a file by hand.
        /// </remarks>
        private Button BuildRestoreButton()
        {
            var button = BuildSecondaryButton("Put mine back", "Restore the dashboards that were replaced when you last chose to update over your own edits.");
            button.Visibility = Visibility.Collapsed;
            button.Click += (sender, args) => RestoreKept();
            return button;
        }

        private void RestoreKept()
        {
            if (applying) return;
            var root = plugin.Installer.SimHubRoot;
            var restored = new List<string>();
            foreach (var folder in plugin.Installer.Packages.Select(p => p.FolderName).Where(f => f != null).Distinct())
            {
                var kept = PackageExtractor.KeptCopies(root, folder).FirstOrDefault(path => path.Contains(PackageExtractor.EditedSuffix));
                if (kept == null) continue;
                try
                {
                    if (PackageExtractor.Restore(root, folder, new SimHubInstallLog(), kept)) restored.Add(folder);
                }
                catch (Exception ex)
                {
                    Log.Error("Putting back " + folder + " failed", ex);
                }
            }
            plugin.Installer.Refresh();
            plugin.SaveSettings();
            RefreshStatus();
            updateLine.Text = restored.Count == 0
                ? "There was nothing of yours to put back."
                : "Put back " + (restored.Count == 1 ? "1 dashboard" : restored.Count + " dashboards") + ". " + UpdateWording.Reopen;
            updateLine.Visibility = Visibility.Visible;
            RefreshRestoreButton();
        }

        /// <summary>The button appears only when there is something of the user's to put back.</summary>
        private void RefreshRestoreButton()
        {
            if (restoreButton == null) return;
            var root = plugin.Installer.SimHubRoot;
            var any = plugin.Installer.Packages
                .Select(p => p.FolderName)
                .Where(f => f != null)
                .Distinct()
                .Any(f => PackageExtractor.KeptCopies(root, f).Any(path => path.Contains(PackageExtractor.EditedSuffix)));
            restoreButton.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        }

        private static Button BuildSecondaryButton(string content, string tooltip)
        {
            Button button;
            try
            {
                button = new SHButtonPrimary();
            }
            catch (Exception ex)
            {
                Log.Warn("SHButtonPrimary is unavailable; using a plain button: " + ex.Message);
                button = new Button();
            }
            button.Content = content;
            button.MinWidth = 96;
            button.ToolTip = tooltip;
            return button;
        }

        /// <summary>
        /// Asks, off the UI thread, and shows whatever came back.
        /// </summary>
        /// <remarks>
        /// Nothing here blocks: a socket that never answers would otherwise freeze the settings page, and on the
        /// thread SimHub calls Init on it would freeze SimHub's start.
        /// </remarks>
        private void Check(bool manual)
        {
            if (!Settings.CheckForUpdates && !manual) return;
            // Whether a request will be made is decided here rather than on the background thread, because saying
            // "Checking for updates…" and then not checking left the panel on that sentence for as long as it was
            // open, and hid an offer it had already found.
            if (!UpdateCheck.ShouldCheck(Settings.CheckForUpdates, Settings.LastUpdateCheckTicks, DateTime.UtcNow, manual)) return;

            checkButton.IsEnabled = false;
            updateStatus = new UpdateStatus { State = UpdateState.Checking, InstalledVersion = plugin.Installer.InstalledVersion, Manual = manual };
            RefreshUpdateLine();

            var installed = UpdateCheck.ComparableInstalled(plugin.Installer.InstalledVersion, OpenDash.Version);
            UpdateService.InBackground(() =>
            {
                var ticks = Settings.LastUpdateCheckTicks;
                var answer = Updates.Check(installed, Settings.CheckForUpdates, ref ticks, DateTime.UtcNow, manual);
                Dispatcher.Invoke(() =>
                {
                    if (answer == null)
                    {
                        // The service declined after all. Whatever was showing before is still the truth.
                        updateStatus = new UpdateStatus { State = UpdateState.Idle, InstalledVersion = plugin.Installer.InstalledVersion };
                    }
                    if (answer != null)
                    {
                        updateStatus = answer;
                        if (ticks != Settings.LastUpdateCheckTicks)
                        {
                            Settings.LastUpdateCheckTicks = ticks;
                            plugin.SaveSettings();
                        }
                    }
                    checkButton.IsEnabled = true;
                    confirmingEdited = false;
                    RefreshUpdateLine();
                });
            }, new SimHubInstallLog());
        }

        /// <summary>
        /// Applies the release the last check found, asking once before replacing a dashboard somebody has edited.
        /// </summary>
        private void ApplyUpdate()
        {
            // A second click before the first has been answered used to fall straight through the confirmation,
            // because the confirming branch returned without disabling anything.
            if (applying) return;

            var release = Updates.LastReleases.FirstOrDefault(r => r.Version == updateStatus.LatestVersion);
            if (release == null)
            {
                // Nothing to act on, so the line says so and the button is put back where the status says it
                // belongs, which is how a button left over from an earlier state disappears on the press that
                // found it stale rather than staying to be pressed again.
                RefreshUpdateLine();
                updateLine.Text = UpdateWording.NothingToApply;
                updateLine.Visibility = Visibility.Visible;
                return;
            }

            var edited = plugin.Installer.Packages.Where(p => p.Edited).Select(p => p.FolderName).ToList();
            if (edited.Count > 0 && !confirmingEdited)
            {
                // One click to be told, a second to mean it. A dialog would be the SimHub way and a modal in a
                // settings page is worse than a button that changes what it says.
                confirmingEdited = true;
                updateButton.Content = "Replace anyway";
                updateLine.Text = (edited.Count == 1 ? "1 dashboard has" : edited.Count + " dashboards have")
                    + " changed since OpenDash wrote them: " + string.Join(", ", edited)
                    + ". Updating replaces what is there. A copy of yours is kept beside it in DashTemplates, "
                    + "and \"Put mine back\" restores it.";
                updateLine.Visibility = Visibility.Visible;
                return;
            }

            var replaceEdited = confirmingEdited;
            applying = true;
            updateButton.IsEnabled = false;
            reinstallButton.IsEnabled = false;
            checkButton.IsEnabled = false;
            updateLine.Text = "Downloading " + updateStatus.LatestVersion + "…";
            updateLine.Visibility = Visibility.Visible;

            UpdateService.InBackground(() =>
            {
                var outcome = Updates.Apply(plugin.Installer, release, replaceEdited);
                Dispatcher.Invoke(() =>
                {
                    applying = false;
                    confirmingEdited = false;
                    updateButton.Content = "Update";
                    updateButton.IsEnabled = true;
                    reinstallButton.IsEnabled = true;
                    checkButton.IsEnabled = true;
                    // The record is written in memory by the installer and saved here, on the UI thread, which is
                    // the moment it is safe to serialise the settings.
                    plugin.SaveSettings();
                    plugin.Installer.Refresh();
                    RefreshRestoreButton();
                    if (outcome.Ok && outcome.Updated.Count > 0)
                    {
                        updateStatus = new UpdateStatus { State = UpdateState.UpToDate, InstalledVersion = plugin.Installer.InstalledVersion, Manual = true };
                    }
                    RefreshStatus();
                    // The button's visibility is computed nowhere but here, so a status that has just stopped
                    // offering an update has to be redrawn or the button outlives the release it was offering.
                    // It runs before the outcome sentence is written because it writes the line as well.
                    RefreshUpdateLine();
                    updateLine.Text = outcome.Line;
                    updateLine.Visibility = Visibility.Visible;
                });
            }, new SimHubInstallLog(), mustFinish: true);
        }

        private void RefreshUpdateLine()
        {
            if (updateLine == null) return;
            var line = updateStatus.Line;
            updateLine.Text = line ?? string.Empty;
            updateLine.Visibility = updateStatus.IsVisible && line != null ? Visibility.Visible : Visibility.Collapsed;
            updateButton.Visibility = updateStatus.State == UpdateState.UpdateAvailable ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>SimHub's primary button (SHButtonPrimary); a plain button when the type cannot be created.</summary>
        private Button BuildReinstallButton()
        {
            Button button;
            try
            {
                button = new SHButtonPrimary();
            }
            catch (Exception ex)
            {
                Log.Warn("SHButtonPrimary is unavailable; using a plain button: " + ex.Message);
                button = new Button();
            }
            button.Content = "Reinstall";
            button.MinWidth = 96;
            button.ToolTip = "Extract every embedded dashboard into DashTemplates again. Your settings are kept.";
            button.Click += (sender, args) => Reinstall();
            return button;
        }

        /// <summary>
        /// Writes every embedded dashboard again, asking once before replacing one somebody has edited.
        /// </summary>
        /// <remarks>
        /// It used to leave an edited folder alone and report "Up to date", which made the button appear to have
        /// worked while nothing happened. Worse, the only path that could replace an edited folder was the Update
        /// button's second click, and that button appears only while a newer release exists, so a person who had
        /// edited a dashboard had no way at all to get OpenDash's own version back.
        /// </remarks>
        private void Reinstall()
        {
            // Two installers over the same DashTemplates folders is the one combination that can delete a folder
            // one of them is extracting into, so whichever starts first holds the field.
            if (applying) return;

            var edited = plugin.Installer.Packages.Where(p => p.Edited).Select(p => p.FolderName).ToList();
            if (edited.Count > 0 && !confirmingReinstall)
            {
                confirmingReinstall = true;
                reinstallButton.Content = "Replace anyway";
                updateLine.Text = (edited.Count == 1 ? "1 dashboard has" : edited.Count + " dashboards have")
                    + " changed since OpenDash wrote them: " + string.Join(", ", edited)
                    + ". Reinstalling replaces what is there. A copy of yours is kept beside it in DashTemplates, "
                    + "and \"Put mine back\" restores it.";
                updateLine.Visibility = Visibility.Visible;
                return;
            }

            var replaceEdited = confirmingReinstall;
            reinstallButton.IsEnabled = false;
            try
            {
                plugin.Installer.EnsureInstalled(true, replaceEdited);
                var replaced = plugin.Installer.Packages.Count(p => p.Extracted);
                var held = plugin.Installer.Packages.Count(p => p.HeldBack);
                updateLine.Text = held > 0
                    ? "Reinstalled " + replaced + ". " + held + " left alone because you have edited them."
                    : "Reinstalled " + replaced + (replaced == 1 ? " dashboard. " : " dashboards. ") + UpdateWording.Reopen;
                updateLine.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Log.Error("Reinstall failed", ex);
                updateLine.Text = "The reinstall did not finish: " + ex.Message;
                updateLine.Visibility = Visibility.Visible;
            }
            finally
            {
                confirmingReinstall = false;
                reinstallButton.Content = "Reinstall";
                reinstallButton.IsEnabled = true;
                plugin.SaveSettings();
                RefreshStatus();
                RefreshRestoreButton();
            }
        }

        /// <summary>Title "OpenDash <installed version> · <n> dashboards" and the status pill: a 6 px dot and a tracked
        /// label showing the worst status across the packages; the tooltip lists every dashboard with its own status.</summary>
        private void RefreshStatus()
        {
            var installer = plugin.Installer;
            var version = installer.InstalledVersion ?? installer.EmbeddedVersion ?? OpenDash.Version;
            dashboardTitle.Text = DashboardInstaller.Summary(version, installer.PackageCount);

            string dot;
            var label = Theme.TextPrimary;
            switch (installer.Status)
            {
                case InstallStatus.UpToDate:
                    dot = Theme.StatusUpToDate;
                    break;
                case InstallStatus.UpdateAvailable:
                    dot = Theme.StatusUpdateAvailable;
                    break;
                case InstallStatus.Failed:
                    dot = Theme.StatusFailed;
                    break;
                default:
                    dot = Theme.StatusNotInstalled;
                    label = Theme.TextLabel;
                    break;
            }
            statusHost.Child = Ui.HStack(8, Ui.Dot(dot), Ui.Label(installer.Status.Label(), label));
            statusHost.ToolTip = installer.HasEmbeddedPackage
                ? (installer.Packages.Count > 0 ? installer.PackageReport() : installer.LastError)
                : "This build of the plugin carries no dashboard package.";
        }

        // Footer

        /// <summary>56 px, rule on top: Documentation and Report an issue links, "MIT LICENCE" on the right.</summary>
        private FrameworkElement BuildFooter()
        {
            var links = Ui.HStack(24, BuildLink("Documentation", DocumentationUrl), BuildLink("Report an issue", IssuesUrl));
            var grid = Ui.Row(links, Ui.Label("MIT licence"));
            grid.Height = 56;
            grid.Margin = new Thickness(PagePadding, 0, PagePadding, 0);
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = grid,
            };
        }

        /// <summary>SimHub's link button (SHLinkButton) carrying the canvas's accent text and external-link icon.</summary>
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
            button.Content = Ui.HStack(6,
                Ui.Text(text, Theme.SizeBody, FontWeights.Medium, Theme.Accent),
                Ui.Icon(Ui.ExternalLinkIcon, Theme.Accent));
            button.Padding = new Thickness(0);
            button.Cursor = Cursors.Hand;
            button.ToolTip = url;
            button.Click += (sender, args) => Ui.OpenUrl(url);
            return button;
        }
    }
}
