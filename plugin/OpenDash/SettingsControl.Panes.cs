// SettingsControl.Panes.cs: what each kind of screen shows under its card.
//
// The face is drawn as a plan of itself -- the rev bar, the bar if it has one, zones B, A and C across
// the body and band D at the foot -- at that face's own proportions, so the portrait reads as a column
// and the nano at 800 x 286 shows no bar because it has none. It is a plan rather than a list because
// "zone C" means nothing until you see where zone C is. The shape comes from the contract, which
// carries just enough of it for a plan, since the plugin cannot read a layout file.
//
// The pit wall gets a picture of its three pages for the same reason: its letters are positions.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        // --- A face ------------------------------------------------------------------------------

        private FrameworkElement BuildFacePane(ScreenInstance screen)
        {
            var size = screen.FaceSize;
            if (size == null)
            {
                return Ui.Caption(
                    "openDash no longer ships a face at " + screen.SizeLabel + ", so there is nothing to configure here. "
                    + "Your settings for this screen are kept in case it comes back.",
                    BodyWidth);
            }
            var face = size.Value;

            var rows = new List<UIElement>
            {
                Ui.Caption(
                    "Pick the page a zone opens on, and how many pages its button cycles through. "
                    + "A zone with one page enabled never changes.",
                    BodyWidth),
                BuildFacePicture(screen, face),
                BuildStripCaption(face),
                BuildRevBarRow(screen),
                BuildFlagFormatRow(screen),
                BuildLapReviewRow(screen),
                BuildFaceWarning(screen),
                BuildWheelButtons(screen, face),
            };
            RefreshFaceWarning(screen);
            return Ui.VStack(12, rows.ToArray());
        }

        /// <summary>
        /// What this screen carries at the top: the shift lights, a plain RPM bar, or nothing.
        /// </summary>
        /// <remarks>
        /// On the screen's own pane rather than on the Data tab, and it is the row that most obviously
        /// never belonged there: a wheel whose rim already carries LEDs across its top wants no rev bar
        /// and the display on the desk beside it wants one, and the rig-wide switch answered for both.
        /// Both arrangements are built into every face and SimHub shows the one this picks, so the
        /// choice costs no reinstall; off redraws the face without the well, and the zones start where
        /// the recess did.
        ///
        /// First of the three rows under the plan, because it is the one that changes the plan: the
        /// other two decide what covers the face and this decides what the face is.
        /// </remarks>
        private FrameworkElement BuildRevBarRow(ScreenInstance screen)
        {
            var control = BuildSegmented(PanelDataTab.RevBarValues, PanelDataTab.RevBarLabels, Settings.ScreenRevBar(screen.Namespace), value =>
            {
                Settings.SetScreenRevBar(screen.Namespace, value);
                Save();
                // The plan above redraws without the well, which is the whole of what Off does.
                Redraw();
            });
            var row = Ui.Row(PanelDataTab.RevBarTitle, PanelDataTab.RevBarCaption, control);
            row.HorizontalAlignment = HorizontalAlignment.Stretch;
            return row;
        }

        /// <summary>Which of the two formats a flag takes on this screen.</summary>
        /// <remarks>
        /// On the screen's own pane rather than on the Data tab, because it is the one flag setting that
        /// is not the same decision everywhere: a rig with a rim and a display in the corner of the eye
        /// wants the rim readable and the corner impossible to miss. Both formats are built into every
        /// face and SimHub shows the one this picks, as it does with the rev bar, so the choice costs no
        /// reinstall. Under the plan of the face rather than above it, since the plan is what shows where
        /// band D and the body are.
        /// </remarks>
        private FrameworkElement BuildFlagFormatRow(ScreenInstance screen)
        {
            var control = BuildSegmented(Contract.FlagFormats, new[] { "Band D", "Full screen" }, Settings.ScreenFlagFormat(screen.Namespace), value =>
            {
                screen.FlagFormat = value;
                Save();
            });
            var row = Ui.Row(
                "Flags",
                "Band D hands a flag the strip at the foot. Full screen hands it zones B, A and C together, which cannot be missed and takes the gear with it for as long as the flag is out.",
                control);
            row.HorizontalAlignment = HorizontalAlignment.Stretch;
            return row;
        }

        /// <summary>When this screen shows the lap review.</summary>
        /// <remarks>
        /// On the screen's own pane rather than on the Data tab, and for a stronger version of the
        /// reason the flag format is there: the panel takes the hero for four seconds at every
        /// crossing, so a rig with a display on the desk and a rim in the driver's hands wants it on
        /// the one and certainly not on the other. Under the flag format, because the two are the
        /// same question asked about two things that cover the face.
        ///
        /// Off leads the control, which is also the default: what takes the face is asked for.
        /// </remarks>
        private FrameworkElement BuildLapReviewRow(ScreenInstance screen)
        {
            var control = BuildSegmented(Contract.LapReviewModes, new[] { "Off", "Races", "Always" }, Settings.ScreenLapReview(screen.Namespace), value =>
            {
                screen.LapReview = value;
                Save();
            });
            var row = Ui.Row(
                "Lap review",
                "A panel over the gear for four seconds at the line: the lap you have just done, its sectors, what it was worth against "
                + "the session best and the lap before, and the fuel it cost. Races means the sessions your sim calls Race.",
                control);
            row.HorizontalAlignment = HorizontalAlignment.Stretch;
            return row;
        }

        private TextBlock BuildStripCaption(Contract.FaceSize face)
        {
            stripCaption = Ui.Caption(face.HasBar
                ? "The bar does not cycle: it carries what does not change during a lap. "
                    + (face.BarFieldsPerEnd == 1 ? "One field at each end, " : "Two fields at each end, ")
                    + "and between them the car settings your sim publishes — slip, TC, cut, bias, ABS, map and diff. "
                    + "A setting the sim has no value for takes its cell with it rather than leaving an empty box."
                : "This screen has no bar: at " + face + " the height is not there, so the body and band D have it instead.",
                BodyWidth);
            return stripCaption;
        }

        private FrameworkElement BuildFacePicture(ScreenInstance screen, Contract.FaceSize face)
        {
            var plan = PanelFacePlan.For(face);
            var grid = new Grid { Width = PanelFacePlan.PictureWidth, Background = Ui.Brush(Theme.Rule), HorizontalAlignment = HorizontalAlignment.Left };
            var rows = new List<double> { plan.RevBar, PanelFacePlan.Seam };
            if (plan.HasBar) rows.AddRange(new double[] { plan.Bar, PanelFacePlan.Seam });
            rows.AddRange(new double[] { plan.Body, PanelFacePlan.Seam, plan.Band });
            foreach (var height in rows) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height) });

            var row = 0;
            AddAt(grid, BuildRevBarStrip(), row);
            row += 2;
            if (plan.HasBar)
            {
                AddAt(grid, BuildBarStrip(screen, face), row);
                row += 2;
            }
            AddAt(grid, BuildFaceBody(screen, plan), row);
            AddAt(grid, BuildBandStrip(screen), row + 2);
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = grid,
            };
        }

        private static void AddAt(Grid grid, UIElement child, int row)
        {
            Grid.SetRow(child, row);
            grid.Children.Add(child);
        }

        /// <summary>The rev bar, which is the one part of the face that is not configurable here: whether it
        /// draws at all is a Data setting, and what it draws is the car's own shift points.</summary>
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
        private FrameworkElement BuildBarStrip(ScreenInstance screen, Contract.FaceSize face)
        {
            var single = face.BarFieldsPerEnd == 1;
            var left = BuildBarEnd(screen, "Left1", single ? null : "Left2", PanelFacePlan.BarEndLeftWidth);
            var right = BuildBarEnd(screen, "Right1", single ? null : "Right2", PanelFacePlan.BarEndRightWidth);
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
        /// One end of the bar. An end carries two fields on a wide face, so the control opens a panel
        /// with a picker for each rather than splitting into two boxes the plan has no room for.
        /// </summary>
        private FrameworkElement BuildBarEnd(ScreenInstance screen, string firstSlot, string secondSlot, double width)
        {
            var button = Ui.DropButton(width, BarEndCaption(screen, firstSlot, secondSlot), secondSlot == null ? "The field this end of the bar shows" : "The two fields this end of the bar shows");
            barEndButtons[firstSlot] = button;
            var rows = new List<UIElement>();
            var slots = secondSlot == null ? new[] { firstSlot } : new[] { firstSlot, secondSlot };
            foreach (var slot in slots)
            {
                var captured = slot;
                var select = BuildPageSelect(FacePages.BarFields, screen.Face.BarField(captured), 200, index =>
                {
                    screen.Face.SetBarField(captured, index);
                    Save();
                    Ui.SetDropText(button, BarEndCaption(screen, firstSlot, secondSlot));
                });
                rows.Add(Ui.Row(Ui.Label(secondSlot == null ? "Field" : slot == firstSlot ? "First" : "Second"), select));
            }
            var panel = Ui.VStack(8, rows.ToArray());
            panel.Width = 260;
            return Ui.Drop(button, panel);
        }

        private static string BarEndCaption(ScreenInstance screen, string firstSlot, string secondSlot)
        {
            var first = FacePages.FieldName(screen.Face.BarField(firstSlot));
            return secondSlot == null ? first : FacePages.EndLabel(first, FacePages.FieldName(screen.Face.BarField(secondSlot)));
        }

        /// <summary>
        /// The three body zones, in the order and along the axis this face draws them: B, A and C
        /// across a wide face, A over B over C in portrait.
        /// </summary>
        private FrameworkElement BuildFaceBody(ScreenInstance screen, PanelFacePlan plan)
        {
            var grid = new Grid { Background = Ui.Brush(Theme.Rule) };
            for (var i = 0; i < plan.Letters.Length; i++)
            {
                if (plan.Stacked)
                {
                    if (i > 0) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(PanelFacePlan.Seam) });
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(plan.Cells[i]) });
                }
                else
                {
                    if (i > 0) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelFacePlan.Seam) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(plan.Cells[i]) });
                }
            }
            for (var i = 0; i < plan.Letters.Length; i++)
            {
                var cell = BuildZoneCell(screen, plan.Letters[i], plan.Stacked ? PanelFacePlan.PictureWidth : plan.Cells[i]);
                if (plan.Stacked) Grid.SetRow(cell, i * 2);
                else Grid.SetColumn(cell, i * 2);
                grid.Children.Add(cell);
            }
            return grid;
        }

        /// <summary>
        /// One zone of the body: its letter, the page it opens on directly under it, how many pages it
        /// cycles under that, and the class filter at the foot where the zone has a page it changes.
        /// </summary>
        /// <remarks>
        /// The three read down in the order they are written here, which they did not before: a
        /// DockPanel of three bottom-docked children draws them from the bottom up, so the count came
        /// out above the select it counts. One stack is what holds the order the canvas draws, and an
        /// edit that adds a fourth control to the dock cannot invert it again.
        ///
        /// The cell is the tightest box on the panel. At the reference face it is 138 high, of which the
        /// padding takes 16, the letter about 16, the two controls 48 and the gaps between them 18,
        /// which leaves the class filter the 40 a toggle and its label need; a control added here has to
        /// shrink another or move out of the cell rather than draw past the seam.
        /// </remarks>
        private FrameworkElement BuildZoneCell(ScreenInstance screen, string letter, double width)
        {
            var inner = PanelFacePlan.Inner(width);
            var dock = new DockPanel { LastChildFill = false };
            var stack = Ui.VStack(PanelFacePlan.CellGap,
                Ui.Label(PanelFacePlan.ZoneLabel(letter), Theme.TextSecondary),
                BuildZoneSelectFor(screen, letter, inner),
                BuildMaskDrop(screen, letter, inner));
            DockPanel.SetDock(stack, Dock.Top);
            dock.Children.Add(stack);

            if (FacePages.OffersClassFilter(letter))
            {
                var classOnly = BuildClassFilterRow(screen, letter);
                DockPanel.SetDock(classOnly, Dock.Bottom);
                dock.Children.Add(classOnly);
            }

            return new Border
            {
                Background = Ui.Brush(Theme.SurfaceBase),
                Padding = new Thickness(PanelFacePlan.CellPaddingX, PanelFacePlan.CellPaddingY, PanelFacePlan.CellPaddingX, PanelFacePlan.CellPaddingY),
                Child = dock,
            };
        }

        /// <summary>Band D, across the foot: the same two controls as a body zone, laid along the row
        /// rather than stacked, since a band has the width and not the height.</summary>
        private FrameworkElement BuildBandStrip(ScreenInstance screen)
        {
            var row = Ui.HStack(PanelFacePlan.BandGap,
                Ui.Label(PanelFacePlan.ZoneLabel("D"), Theme.TextSecondary),
                BuildZoneSelectFor(screen, "D", PanelFacePlan.BandSelectWidth),
                BuildMaskDrop(screen, "D", PanelFacePlan.BandSelectWidth));
            row.Margin = new Thickness(8, 0, 8, 0);
            return new Border { Background = Ui.Brush(Theme.SurfaceInset), Child = row };
        }

        /// <summary>The page a zone opens on. Writing it moves the live face too, so the dash on the desk
        /// follows the panel rather than waiting for the next session.</summary>
        private ComboBox BuildZoneSelectFor(ScreenInstance screen, string letter, double width)
        {
            var pages = FacePages.For(letter);
            var select = BuildPageSelect(pages, screen.Face.Start(letter), width, index =>
            {
                screen.Face.SetStart(letter, index);
                Save();
                RefreshZone(screen, letter);
                RefreshFaceWarning(screen);
            });
            select.ToolTip = "The page zone " + letter + " opens on";
            // The field chrome, so that the two controls of a cell are one pair rather than SimHub's box
            // above the panel's own. A ComboBox keeps SimHub's template, which is what Ui.Field leaves it.
            Ui.Field(select, Theme.ControlHeightSm);
            zoneSelects[letter] = select;
            return select;
        }

        /// <summary>
        /// Whether this zone's lists show the player's own class.
        ///
        /// It sits in the zone's own cell rather than beside the position mode, because it is a property
        /// of the zone and not of the face: the point of it is zone B listing the race while zone C
        /// lists the class a driver is actually racing in.
        /// </summary>
        /// <remarks>
        /// A toggle and a label beside it, because a boolean is a toggle everywhere else on the panel and
        /// this was one of the two places it was not. Nothing but the control itself writes the setting,
        /// so the toggle is not kept for refreshing the way the page select and the count are.
        /// </remarks>
        private FrameworkElement BuildClassFilterRow(ScreenInstance screen, string letter)
        {
            var toggle = BuildToggle(screen.Face.IsClassOnly(letter), on => { screen.Face.SetClassOnly(letter, on); Save(); });
            toggle.ToolTip = "Show the leaderboard and the relative in zone " + letter + " for your own class";
            toggle.VerticalAlignment = VerticalAlignment.Center;
            toggle.HorizontalAlignment = HorizontalAlignment.Left;
            // Measured inside a vertical stack, which is not a nicety. SHToggleButton declares no size of
            // its own and grows to whatever it is measured against; every other toggle on the panel sits
            // in a row of automatic height and so is measured against infinity, but this one is docked to
            // the bottom of a cell as tall as the face's body, and it drew as a white disc filling zone B
            // and zone C. A vertical StackPanel measures its children with an infinite height, which is
            // the same question the rows ask and gets the same answer, without this file having to know
            // what size SimHub draws a switch at.
            var sized = Ui.VStack(0, toggle);
            sized.VerticalAlignment = VerticalAlignment.Center;
            // And a floor under its width, because SimHub's switch draws wider than it measures: the
            // label beside it started underneath the knob. A floor rather than a fixed width, so a switch
            // that is genuinely wider than this still gets the room it asks for.
            sized.MinWidth = PanelFacePlan.SwitchWidth;
            return Ui.HStack(PanelFacePlan.CellGap, sized, Ui.Text("My class only", Theme.SizeLabel, FontWeights.Normal, Theme.TextSecondary));
        }

        /// <summary>
        /// The enabled pages of a zone. This is the control that decides how long a driver's cycle is,
        /// which makes it the most consequential thing on the panel.
        /// </summary>
        private FrameworkElement BuildMaskDrop(ScreenInstance screen, string letter, double width)
        {
            var caption = MaskCaption(screen, letter);
            var button = Ui.DropButton(width, caption, "Which pages zone " + letter + " cycles through");
            DressAsCount(button, caption);
            zoneMaskButtons[letter] = button;
            return Ui.Drop(button, BuildMaskPanel(screen, letter));
        }

        /// <summary>
        /// A count is not a value: the canvas sets it as a tracked label at eleven in text.label under a
        /// twelve pixel chevron, where a drop button's caption is a value at twelve in text.primary.
        /// </summary>
        /// <remarks>
        /// The chrome stays the kit's -- this rewrites the caption of a button Ui.DropButton built and
        /// touches nothing else -- because the kit has no tracked-caption variant to ask for, and a
        /// second drop button drawn here would be a field the control kit already covers. The variant
        /// belongs in Widgets.cs beside DropButton; until it is there, the pair below is where it lives.
        ///
        /// A tracked label is one block per character rather than a TextBlock, so Ui.SetDropText has
        /// nothing to write to and SetCountCaption replaces the whole caption instead. The gap of five
        /// the canvas leaves between the caption and the chevron is the slack in a docked row rather
        /// than a number: the caption takes the left of the box and the chevron the right.
        /// </remarks>
        private static void DressAsCount(ToggleButton button, string text)
        {
            var host = new Border { VerticalAlignment = VerticalAlignment.Center };
            var chevron = Ui.Icon(Ui.ChevronIcon, Theme.TextLabel, PanelFacePlan.CountChevronSize);
            chevron.HorizontalAlignment = HorizontalAlignment.Right;
            var row = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(chevron, Dock.Right);
            row.Children.Add(chevron);
            row.Children.Add(host);
            button.Content = row;
            button.Tag = host;
            SetCountCaption(button, text);
        }

        private static void SetCountCaption(ToggleButton button, string text)
        {
            var host = button.Tag as Border;
            if (host != null) host.Child = Ui.Label(text, Theme.TextLabel, PanelFacePlan.CountCaptionSize);
        }

        private static string MaskCaption(ScreenInstance screen, string letter)
        {
            var pages = FacePages.For(letter).Count;
            var on = 0;
            for (var page = 0; page < pages; page++)
            {
                if (screen.Face.PageEnabled(letter, page)) on++;
            }
            return on + " of " + pages + " pages";
        }

        /// <summary>A checkbox per page, seven to a column, with All and None above them.</summary>
        private FrameworkElement BuildMaskPanel(ScreenInstance screen, string letter)
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
                    IsChecked = screen.Face.PageEnabled(letter, page),
                    Margin = new Thickness(0, 0, 20, 6),
                    MinWidth = 120,
                };
                box.Checked += (sender, args) => SetPage(screen, letter, page, true);
                box.Unchecked += (sender, args) => SetPage(screen, letter, page, false);
                boxes.Add(box);
                Grid.SetColumn(box, i / perColumn);
                Grid.SetRow(box, i % perColumn);
                grid.Children.Add(box);
            }

            var all = BuildMaskLink("All", () => SetEveryPage(screen, letter, true));
            var none = BuildMaskLink("None", () => SetEveryPage(screen, letter, false));
            var header = Ui.Row(Ui.Label("Pages zone " + letter + " cycles"), Ui.HStack(12, all, none));
            header.Margin = new Thickness(0, 0, 0, 10);
            return Ui.VStack(0, header, grid);
        }

        private static Button BuildMaskLink(string text, Action clicked)
        {
            var button = new Button
            {
                Content = Ui.Text(text, Theme.SizeLabel, FontWeights.Medium, Theme.Accent),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
            };
            button.Click += (sender, args) => clicked();
            return button;
        }

        private void SetPage(ScreenInstance screen, string letter, int page, bool enabled)
        {
            screen.Face.SetPageEnabled(letter, page, enabled);
            Save();
            RefreshZone(screen, letter);
            RefreshFaceWarning(screen);
        }

        /// <summary>None leaves the page the zone is on enabled, because a zone with an empty cycle has
        /// nothing to draw; the settings object refuses it and the checkbox follows what it decided.</summary>
        private void SetEveryPage(ScreenInstance screen, string letter, bool enabled)
        {
            var pages = FacePages.For(letter);
            if (enabled)
            {
                for (var i = 0; i < pages.Count; i++) screen.Face.SetPageEnabled(letter, pages[i].Number, true);
            }
            else
            {
                var keep = screen.Face.Start(letter);
                for (var i = 0; i < pages.Count; i++)
                {
                    if (pages[i].Number != keep) screen.Face.SetPageEnabled(letter, pages[i].Number, false);
                }
            }
            Save();
            RefreshZone(screen, letter);
            RefreshFaceWarning(screen);
        }

        /// <summary>Puts the controls of one zone back in step with the settings, which may have moved on
        /// their own: turning off the page a zone sits on snaps it forward to the next enabled one.</summary>
        private void RefreshZone(ScreenInstance screen, string letter)
        {
            ComboBox select;
            if (zoneSelects.TryGetValue(letter, out select))
            {
                var start = screen.Face.Start(letter);
                if (select.SelectedIndex != start) select.SelectedIndex = start;
            }
            ToggleButton button;
            if (zoneMaskButtons.TryGetValue(letter, out button)) SetCountCaption(button, MaskCaption(screen, letter));
            List<CheckBox> boxes;
            if (zoneMaskBoxes.TryGetValue(letter, out boxes))
            {
                var pages = FacePages.For(letter);
                for (var i = 0; i < boxes.Count && i < pages.Count; i++)
                {
                    var enabled = screen.Face.PageEnabled(letter, pages[i].Number);
                    if (boxes[i].IsChecked != enabled) boxes[i].IsChecked = enabled;
                }
            }
        }

        /// <summary>"Zone B and zone C both show Relative." Says so and allows it: a driver watching the
        /// relative in two places is a choice and not a mistake.</summary>
        private FrameworkElement BuildFaceWarning(ScreenInstance screen)
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

        private void RefreshFaceWarning(ScreenInstance screen)
        {
            // Called while the pane is still being assembled as well as after it, and after a tab has
            // been left, so a row that is not there is nothing to refresh rather than a crash.
            if (faceWarningText == null || faceWarningRow == null || screen?.Face == null) return;
            var message = FacePageClash.Warning(screen.Face);
            faceWarningText.Text = message;
            faceWarningRow.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        // --- The wheel buttons of one screen -----------------------------------------------------

        /// <summary>
        /// A button per zone to cycle it, and one held for a glance, bound to this screen's own actions.
        /// </summary>
        /// <remarks>
        /// Per screen, which is what lets a second face stay still while the one in front of the driver
        /// cycles. Before ADR 0017 the actions were per face *size*, so two screens of one size shared
        /// one set of buttons and there was no way to give them different ones.
        ///
        /// The four are laid out in the order the picture above them draws the zones, wrapping into
        /// pairs, so that a driver binding zone C looks in the place zone C is. PanelFacePlan.ZoneOrder
        /// is that order; Contract.FaceZoneLetters keeps its own, because it indexes the settings.
        /// </remarks>
        private FrameworkElement BuildWheelButtons(ScreenInstance screen, Contract.FaceSize face)
        {
            var wrap = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var letter in PanelFacePlan.ZoneOrder(face))
            {
                var pair = Ui.HStack(8,
                    Ui.Label(PanelFacePlan.ZoneLabel(letter)),
                    BuildBinder(Contract.CycleZoneAction(screen.Namespace, letter), screen.Name + " · zone " + letter));
                pair.Margin = new Thickness(0, 0, PanelFacePlan.BindingGap, PanelFacePlan.BindingGap);
                wrap.Children.Add(pair);
            }

            var glanceText = Ui.VStack(4, Ui.Body("Quick glance"),
                Ui.Caption("Hold to show one page, release to return. Usually the relative or the track."));
            glanceText.MaxWidth = 420;

            return Ui.Section("Wheel buttons on this screen",
                Ui.Caption("Bound per screen, so a second face can stay still while the one in front of you cycles.", BodyWidth),
                wrap,
                Ui.Row(glanceText, Ui.HStack(PanelFacePlan.GlanceBinderGap,
                    BuildGlanceSelect(screen),
                    BuildBinder(Contract.HoldQuickGlanceActionFor(screen.Namespace), screen.Name + " · quick glance", hold: true))));
        }

        /// <summary>
        /// The one page the glance shows, zone and page together.
        /// </summary>
        /// <remarks>
        /// One select of fifty-four rather than a zone box beside a page box: the glance is one choice,
        /// and a zone without a page means nothing. The list is Contract.FaceZoneLetters order, which is
        /// the order the settings are in, and the item a row reads is PanelFacePlan.GlanceLabel, which is
        /// where the wording is pinned.
        /// </remarks>
        private ComboBox BuildGlanceSelect(ScreenInstance screen)
        {
            var options = PanelFacePlan.GlanceOptions();
            var select = new ComboBox
            {
                Width = PanelFacePlan.GlanceSelectWidth,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "The page a held button shows",
            };
            Ui.Field(select, Theme.ControlHeightSm);
            foreach (var option in options) select.Items.Add(PanelFacePlan.GlanceLabel(option));
            var glance = Contract.NormaliseQuickGlance(screen.Face.QuickGlance);
            var index = Array.IndexOf(options, glance);
            select.SelectedIndex = index >= 0 ? index : 0;
            select.SelectionChanged += (sender, args) =>
            {
                if (select.SelectedIndex < 0 || select.SelectedIndex >= options.Length) return;
                screen.Face.QuickGlance = options[select.SelectedIndex];
                Save();
                // The clash line counts the glance among the participants, so the warning has to be asked
                // again here and not only when a zone moves.
                RefreshFaceWarning(screen);
            };
            return select;
        }

        // --- A pit wall ---------------------------------------------------------------------------

        /// <summary>
        /// Two sections and not one: where the zones are, and what each of them shows.
        /// </summary>
        /// <remarks>
        /// The picture answers a different question from the list under it, and the canvas gives each its
        /// own heading. The pane therefore carries both headings itself, which is why the Rig tab does not
        /// wrap a pit wall the way it wraps the other kinds.
        /// </remarks>
        private FrameworkElement BuildPitWallPane(ScreenInstance screen)
        {
            var glanceText = Ui.VStack(4, Ui.Body("Quick glance"),
                Ui.Caption("Hold to show one page over the zone it belongs to, release to put the zone back."));
            glanceText.MaxWidth = 420;

            return Ui.VStack(0,
                Ui.Section("Where the zones are",
                    Ui.Caption("Three pages share four data zones and one wide zone, so the letters need a picture.", BodyWidth),
                    BuildPitWallPicture()),
                Ui.Section("What each zone shows", Ui.VStack(PanelPitWallPlan.RowGap, BuildPitWallRows(screen))),
                // A binding and not a wheel button: nobody drives a pit wall, so the gesture is whatever
                // SimHub will bind, and a keyboard key beside the monitor is the likelier one.
                Ui.Section("A page on demand",
                    Ui.Caption("Bound per screen. A keyboard key serves as well as a wheel button here.", BodyWidth),
                    Ui.Row(glanceText, Ui.HStack(PanelFacePlan.GlanceBinderGap,
                        BuildPitWallGlanceSelect(screen),
                        BuildBinder(Contract.HoldQuickGlanceActionFor(screen.Namespace), screen.Name + " · quick glance", hold: true)))));
        }

        /// <summary>Which of the three pages this pit wall shows, which is set here and nowhere else.</summary>
        private FrameworkElement BuildPitWallPageRow(ScreenInstance screen)
        {
            var select = new ComboBox
            {
                Width = PanelPitWallPlan.SelectWidth,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "The page this pit wall shows",
            };
            Ui.Field(select, PanelPitWallPlan.SelectHeight);
            foreach (var name in Contract.PitWallPageNames) select.Items.Add(name);
            select.SelectedIndex = Contract.NormalisePitWallPage(screen.PitWallPage);
            select.SelectionChanged += (sender, args) =>
            {
                if (select.SelectedIndex < 0) return;
                screen.PitWallPage = Contract.NormalisePitWallPage(select.SelectedIndex);
                Save();
            };
            return Ui.Row("Page", "Which of the three this pit wall shows. It is set here and does not change while you race.", select);
        }

        /// <summary>The one page the glance shows, zone and page together, as the face's own select is.</summary>
        private ComboBox BuildPitWallGlanceSelect(ScreenInstance screen)
        {
            var options = PanelPitWallPlan.GlanceOptions();
            var select = new ComboBox
            {
                Width = PanelPitWallPlan.SelectWidth,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "The page a held binding shows",
            };
            Ui.Field(select, PanelPitWallPlan.SelectHeight);
            foreach (var option in options) select.Items.Add(PanelPitWallPlan.GlanceLabel(option));
            var index = Array.IndexOf(options, Contract.NormalisePitWallQuickGlance(screen.PitWallQuickGlance));
            select.SelectedIndex = index >= 0 ? index : 0;
            select.SelectionChanged += (sender, args) =>
            {
                if (select.SelectedIndex < 0 || select.SelectedIndex >= options.Length) return;
                screen.PitWallQuickGlance = options[select.SelectedIndex];
                Save();
            };
            return select;
        }

        /// <summary>
        /// One group per page, then the address and the filter.
        /// </summary>
        /// <remarks>
        /// Grouped by page because a zone belongs to one. Four rows under a single heading, beside a
        /// picture of three pages, was a panel that could not say which page it was talking about -- and
        /// underneath it the race page's zone A and the telemetry page's were the same setting, so
        /// pointing one somewhere moved the other. Each page's zones now sit under that page's name and
        /// answer only for it.
        /// </remarks>
        private UIElement[] BuildPitWallRows(ScreenInstance screen)
        {
            var rows = new List<UIElement>();
            rows.Add(BuildPitWallPageRow(screen));
            foreach (var pageName in Contract.PitWallPageNames)
            {
                var slots = new List<UIElement>();
                foreach (var slot in Contract.PitWallZoneSlots)
                {
                    if (!slot.Landscape || !string.Equals(slot.Page, pageName, StringComparison.Ordinal)) continue;
                    var captured = slot;
                    var select = BuildZoneSelect(
                        captured.Wide ? ZonePages.Wide : ZonePages.Standard,
                        screen.ZonePage(captured.Key),
                        page => { screen.SetZonePage(captured.Key, page); Save(); });
                    slots.Add(Ui.Row(
                        captured.Wide ? "Wide zone" : "Zone " + captured.Slot,
                        PanelPitWallPlan.ZoneDescription(captured),
                        select));
                }
                rows.Add(Ui.Section(pageName + " page", slots.ToArray()));
            }
            rows.Add(Ui.Row("Web view address", "The page the Web view zone shows. http or https only; leave empty for none.", BuildWebViewBox(screen)));
            // One answer for the screen and not one per zone, as a face has: the four zones are widgets
            // pointed at one dashboard file per rectangle, so two zones of one column are the same file.
            var classOnly = BuildToggle(screen.PitWallClassOnly, on => { screen.PitWallClassOnly = on; Save(); });
            classOnly.ToolTip = "Show the board, the leaderboard and the relative for your own class";
            rows.Add(Ui.Row("My class only", "The board and the list zones show the class you are racing in rather than the whole field.", classOnly));
            return rows.ToArray();
        }

        /// <summary>The three pages, so a letter is a position rather than a label.</summary>
        private static FrameworkElement BuildPitWallPicture()
        {
            var pages = new List<UIElement>();
            foreach (var page in PanelPitWallPlan.Pages) pages.Add(BuildPitWallPage(page));
            var row = Ui.HStack(PanelPitWallPlan.ThumbGap, pages.ToArray());
            row.HorizontalAlignment = HorizontalAlignment.Left;
            return row;
        }

        /// <summary>
        /// One page, drawn at the rectangles PanelPitWallPlan gives it.
        /// </summary>
        /// <remarks>
        /// A Canvas rather than a Grid because the three pages have three shapes and only one of them is a
        /// table: the Tower page puts a wide zone across the top of its right half and two zones side by
        /// side under it, which no arrangement of star rows and columns draws. Placing every panel
        /// absolutely is also what lets the picture and the sentences beside it come from one table.
        /// </remarks>
        private static FrameworkElement BuildPitWallPage(PanelPitWallPlan.Page page)
        {
            var canvas = new Canvas
            {
                Width = PanelPitWallPlan.ThumbWidth,
                Height = PanelPitWallPlan.ThumbHeight,
                Background = Ui.Brush(Theme.SurfaceInset),
            };
            foreach (var panel in page.Panels)
            {
                // The accent says the letter can be pointed at a page in the list below; text.label says
                // the panel is part of the page and there is nothing here to set.
                var label = Ui.Label(panel.Name, panel.Configurable ? Theme.Accent : Theme.TextLabel);
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                var box = new Border
                {
                    Width = panel.Width,
                    Height = panel.Height,
                    Background = Ui.Brush(Theme.Field),
                    BorderBrush = Ui.Brush(Theme.Rule),
                    BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                    Child = label,
                };
                Canvas.SetLeft(box, panel.X);
                Canvas.SetTop(box, panel.Y);
                canvas.Children.Add(box);
            }

            var caption = Ui.Text(page.Title, PanelPitWallPlan.CaptionSize, FontWeights.Normal, Theme.TextSecondary);
            caption.Margin = new Thickness(0, PanelPitWallPlan.CaptionGap, 0, 0);
            var picture = new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                Child = canvas,
            };
            return Ui.VStack(0, picture, caption);
        }

        /// <summary>The pages in page-number order, so that SelectedIndex is the page number.</summary>
        private static ComboBox BuildZoneSelect(IReadOnlyList<ZonePage> pages, int selected, Action<int> changed)
        {
            var box = new ComboBox
            {
                Width = PanelPitWallPlan.SelectWidth,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            Ui.Field(box, PanelPitWallPlan.SelectHeight);
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
        /// The web view address. Written when the box loses focus or Enter is pressed, then normalised.
        /// </summary>
        /// <remarks>
        /// The "https://" an empty box shows is drawn and not stored. Contract.NormaliseUrl keeps only an
        /// absolute address, so a bare scheme written into the setting would be blanked on the first
        /// commit and the box would empty itself in front of whoever was typing into it. WPF has no
        /// watermark of its own, so it is a text block over the box rather than behind it: the box paints
        /// ui.field and anything underneath is covered.
        ///
        /// At 220 an address of any length is cut off, so the tooltip carries the whole of it once there
        /// is one; the rule it has to satisfy is in the row's caption and does not need saying twice.
        /// </remarks>
        private FrameworkElement BuildWebViewBox(ScreenInstance screen)
        {
            var box = new TextBox
            {
                Width = PanelPitWallPlan.AddressWidth,
                Text = screen.WebViewUrl ?? string.Empty,
            };
            Ui.Field(box, PanelPitWallPlan.SelectHeight);
            // The kit's own pair rather than a second answer: both panel sheets write every field
            // `padding: 0 8px 0 10px`, twenty times between them and never symmetrically, so the address
            // box is padded like the select beside it.
            box.Padding = new Thickness(PanelMetrics.FieldPaddingLeft, 0, PanelMetrics.FieldPaddingRight, 0);

            var watermark = Ui.Text(PanelPitWallPlan.AddressPlaceholder, Theme.SizeBody, FontWeights.Normal, Theme.TextLabel);
            watermark.HorizontalAlignment = HorizontalAlignment.Left;
            watermark.Margin = new Thickness(PanelMetrics.FieldPaddingLeft + PanelMetrics.BorderWeight, 0, 0, 0);
            watermark.IsHitTestVisible = false;

            Action reread = () =>
            {
                var empty = box.Text.Length == 0;
                watermark.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
                box.ToolTip = empty ? "An http or https address; anything else is ignored." : box.Text;
            };
            Action commit = () =>
            {
                screen.WebViewUrl = Contract.NormaliseUrl(box.Text);
                Save();
                if (box.Text != screen.WebViewUrl) box.Text = screen.WebViewUrl;
            };
            box.TextChanged += (sender, args) => reread();
            box.LostFocus += (sender, args) => commit();
            box.KeyDown += (sender, args)  =>
            {
                if (args.Key == Key.Enter) commit();
            };
            reread();

            var host = new Grid { Width = PanelPitWallPlan.AddressWidth, HorizontalAlignment = HorizontalAlignment.Right };
            host.Children.Add(box);
            host.Children.Add(watermark);
            return host;
        }

        // --- A companion ---------------------------------------------------------------------------

        private FrameworkElement BuildCompanionPane(ScreenInstance screen)
        {
            var columns = PanelCompanionPlan.ModuleColumns;
            var rows = (Modules.Count + columns - 1) / columns;
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var i = 0; i < Modules.Count; i++)
            {
                var module = Modules.All[i];
                var cell = BuildModuleRow(screen, module);
                Grid.SetColumn(cell, i / rows);
                Grid.SetRow(cell, i % rows);
                grid.Children.Add(cell);
            }
            return Ui.VStack(12,
                Ui.Caption(
                    "A companion shows one module at a time and the header says which. Turn off what you never page to; "
                    + "a module that is off is skipped. Energy, Damage and Track rivals are off because iRacing publishes "
                    + "none of their data; switch them on for a sim that does.",
                    BodyWidth),
                grid,
                BuildCompanionPaging(screen));
        }

        /// <summary>
        /// How a companion is paged, which is not openDash's to decide any more.
        /// </summary>
        /// <remarks>
        /// There were two selects here -- the module a session opens on and the one a held button
        /// shows -- and a binder for openDash's own next-module action. All three needed openDash to be
        /// the thing choosing which screen was up, and that is exactly what stopped a tap working:
        /// SimHub's only touch gesture maps a tap to the previous or next screen, and its navigation
        /// walks the screens whose expression is true, so with one of twenty-one enabled there was
        /// nothing to walk. A row that no longer does anything is worse than a row that is not there,
        /// so they are replaced by the sentence saying where the controls went.
        /// </remarks>
        /// <summary>One module of the catalogue, numbered as the panel numbers them.</summary>
        private ComboBox BuildModuleSelect(int selected, string tooltip, Action<int> chosen)
        {
            var select = new ComboBox
            {
                Width = PanelPitWallPlan.SelectWidth,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = tooltip,
            };
            Ui.Field(select, Theme.ControlHeightSm);
            foreach (var module in Modules.All) select.Items.Add(module.Number.ToString("00") + " \u00b7 " + module.Name);
            select.SelectedIndex = selected >= 0 && selected < Modules.Count ? selected : 0;
            select.SelectionChanged += (sender, args) =>
            {
                if (select.SelectedIndex < 0 || select.SelectedIndex >= Modules.Count) return;
                chosen(select.SelectedIndex);
                Save();
            };
            return select;
        }

        private FrameworkElement BuildCompanionPaging(ScreenInstance screen)
        {
            var startText = Ui.VStack(4, Ui.Body("Opens on"),
                Ui.Caption("The module a session starts on. openDash holds it there for a few seconds while SimHub loads, then hands the paging back."));
            startText.MaxWidth = 420;
            return Ui.Section("Which module is up",
                Ui.Caption(
                    "Tap the left or right half of the screen to go back or forward through the modules you have left on. "
                    + "To page it from a wheel button instead, bind SimHub's own \"Next screen\" for this dashboard under "
                    + "Controls and events.",
                    BodyWidth),
                Ui.Row(startText, BuildModuleSelect(Settings.ScreenCompanionStart(screen.Namespace), "The module a session opens on", value =>
                {
                    screen.CompanionStart = value;
                    // And force it now, so the screen in front of you moves rather than waiting for the
                    // next SimHub start. Somebody choosing where it opens is looking at the thing.
                    screen.OpenOnStartModule();
                })));
        }

        /// <summary>How this companion draws a flag. Full screen by default, which is what a phone on a
        /// stand beside the wheel is for: a 12 px strip at that distance says nothing.</summary>
        private FrameworkElement BuildCompanionFlagRow(ScreenInstance screen)
        {
            var text = Ui.VStack(4, Ui.Body("Flags"),
                Ui.Caption("Full screen takes the module for as long as the flag is out, which is what a screen out of your eyeline is good for. "
                    + "The bar is the thin strip at the foot. Off leaves the module alone."));
            text.MaxWidth = 420;
            var segmented = BuildSegmented(
                Contract.CompanionFlagFormats,
                new[] { "Off", "Bar", "Full screen" },
                Settings.ScreenCompanionFlagFormat(screen.Namespace),
                value => { screen.CompanionFlagFormat = Contract.NormaliseCompanionFlagFormat(value); Save(); });
            return Ui.Row(text, segmented);
        }

        /// <summary>"Tyres" and its toggle. The number and the description are the tooltip: the grid reads
        /// as a column of names, and the header's "n / 21" can still be matched to a row.</summary>
        private FrameworkElement BuildModuleRow(ScreenInstance screen, Module module)
        {
            var name = Ui.Body(module.Name);
            var index = module.Number - 1;
            var toggle = BuildToggle(screen.Modules != null && index < screen.Modules.Length && screen.Modules[index], on =>
            {
                if (screen.Modules == null || index >= screen.Modules.Length) return;
                screen.Modules[index] = on;
                Save();
            });
            toggle.HorizontalAlignment = HorizontalAlignment.Right;
            var row = Ui.Row(name, toggle);
            row.Margin = new Thickness(0, 0, PanelCompanionPlan.ModuleColumnGap, PanelCompanionPlan.ModuleRowGap);
            row.ToolTip = module.Number.ToString("00") + " · " + module.Description;
            return row;
        }

        // --- A slots face ---------------------------------------------------------------------------

        /// <summary>
        /// The twelve-slot picture, for anyone running a card package.
        /// </summary>
        /// <remarks>
        /// On its own screen card rather than in a section of its own, which is how the card model
        /// survives the redesign without cluttering it: it is on screen only if you installed one. It
        /// leaves with the cards in #146.
        /// </remarks>
        private FrameworkElement BuildSlotsPane()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(BuildSlotGrid(1));
            row.Children.Add(BuildHeroCell());
            row.Children.Add(BuildSlotGrid(7));
            var picture = new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = row,
            };
            return Ui.VStack(12,
                Ui.Caption(
                    "The card face openDash shipped before the zones. Any card in any slot; the same card may be "
                    + "assigned twice and the panel says so without preventing it. A face with fewer slots uses the "
                    + "first ones.",
                    BodyWidth),
                picture,
                BuildSlotsRevBarRow(),
                BuildSlotWarning());
        }

        /// <summary>
        /// What a card face carries at the top, which is the rig-wide `RevBar` and not a screen's own.
        /// </summary>
        /// <remarks>
        /// A zone face answers this on its own pane, under its own property. A card face owns no
        /// properties at all -- it reads the twelve shared slots and nothing else, which is why two of
        /// them cannot be told apart -- so its arc reads the rig-wide name, and this row is where that
        /// name is still written from. It is a property of the model being retired rather than
        /// something this fixes, and it is here so that a driver with a round face does not lose the
        /// switch when the zone faces take their own.
        /// </remarks>
        private FrameworkElement BuildSlotsRevBarRow()
        {
            var control = BuildSegmented(PanelDataTab.RevBarValues, PanelDataTab.RevBarLabels, Settings.RevBarMode(), value =>
            {
                Settings.SetRevBar(value);
                Save();
            });
            var row = Ui.Row(
                PanelDataTab.RevBarTitle,
                "Shift lights, a plain arc, or off. A card face has no settings of its own, so this is the answer for every card face on the rig.",
                control);
            row.HorizontalAlignment = HorizontalAlignment.Stretch;
            return row;
        }

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
            box.SelectionChanged += (sender, args) =>
            {
                if (box.SelectedIndex < 0) return;
                Settings.SetSlot((int)box.Tag, box.SelectedIndex);
                Save();
                RefreshSlotWarning();
            };
            return box;
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

        private TextBlock slotWarningText;
        private FrameworkElement slotWarningRow;

        private FrameworkElement BuildSlotWarning()
        {
            slotWarningText = Ui.Text("", Theme.SizeSmall, FontWeights.Normal, Theme.Caution);
            slotWarningText.TextWrapping = TextWrapping.Wrap;
            var icon = Ui.Icon(Ui.WarningIcon, Theme.Caution);
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, 1, 0, 0);
            slotWarningRow = Ui.HStack(10, icon, slotWarningText);
            RefreshSlotWarning();
            return slotWarningRow;
        }

        private void RefreshSlotWarning()
        {
            if (slotWarningText == null || slotWarningRow == null) return;
            var message = DuplicateAssignment.Warning(Settings.Slots);
            slotWarningText.Text = message;
            slotWarningRow.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
