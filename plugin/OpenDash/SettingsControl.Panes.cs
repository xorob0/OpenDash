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
        private const double FaceWidth = BodyWidth - 50;
        private const double FaceRevBarHeight = 19;
        private const double FaceBarHeight = 24;
        private const double FaceBodyHeight = 150;
        private const double FaceBandHeight = 26;
        private const double FaceCellPadding = 6;

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
                BuildFaceWarning(screen),
                BuildWheelButtons(screen),
            };
            RefreshFaceWarning(screen);
            return Ui.VStack(12, rows.ToArray());
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
                AddAt(grid, BuildBarStrip(screen, face), row);
                row += 2;
            }
            AddAt(grid, BuildFaceBody(screen, face), row);
            AddAt(grid, BuildBandStrip(screen), row + 2);
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
        /// parts scaled to the plan's width; a row keeps the artboard's height.
        /// </summary>
        private static double BodyHeightFor(Contract.FaceSize f)
        {
            if (f.Body != Contract.FaceBody.Column) return FaceBodyHeight;
            var total = 0;
            foreach (var part in f.Parts) total += part;
            return Math.Max(FaceBodyHeight, Math.Round(FaceWidth * ((double)total / f.Width) / 2));
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
            var left = BuildBarEnd(screen, "Left1", single ? null : "Left2", 130);
            var right = BuildBarEnd(screen, "Right1", single ? null : "Right2", 140);
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
        private FrameworkElement BuildFaceBody(ScreenInstance screen, Contract.FaceSize face)
        {
            var grid = new Grid { Background = Ui.Brush(Theme.Rule) };
            var letters = face.BodyOrder;
            var stacked = face.Body == Contract.FaceBody.Column;
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
                var cell = BuildZoneCell(screen, letters[i], stacked ? FaceWidth : sizes[i]);
                if (stacked) Grid.SetRow(cell, i * 2);
                else Grid.SetColumn(cell, i * 2);
                grid.Children.Add(cell);
            }
            return grid;
        }

        /// <summary>"ZONE B" at the top, the page it opens on and how many it cycles at the bottom, and the
        /// class filter under them where the zone has a page it changes.</summary>
        private FrameworkElement BuildZoneCell(ScreenInstance screen, string letter, double width)
        {
            var inner = width - 2 * FaceCellPadding;
            var dock = new DockPanel { LastChildFill = false };
            var label = Ui.Label("Zone " + letter);
            DockPanel.SetDock(label, Dock.Top);
            dock.Children.Add(label);

            if (FacePages.OffersClassFilter(letter))
            {
                var classOnly = BuildClassFilterBox(screen, letter);
                classOnly.Margin = new Thickness(0, 6, 0, 0);
                DockPanel.SetDock(classOnly, Dock.Bottom);
                dock.Children.Add(classOnly);
            }

            var select = BuildZoneSelectFor(screen, letter, inner);
            DockPanel.SetDock(select, Dock.Bottom);
            dock.Children.Add(select);

            var mask = BuildMaskDrop(screen, letter, inner);
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
        private FrameworkElement BuildBandStrip(ScreenInstance screen)
        {
            var row = Ui.HStack(8, Ui.Label("Zone D"), BuildZoneSelectFor(screen, "D", 170), BuildMaskDrop(screen, "D", 150));
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
        private CheckBox BuildClassFilterBox(ScreenInstance screen, string letter)
        {
            var box = new CheckBox
            {
                Content = "My class only",
                FontSize = Theme.SizeLabel,
                FontFamily = PanelFonts.Label,
                Foreground = Ui.Brush(Theme.TextSecondary),
                IsChecked = screen.Face.IsClassOnly(letter),
                ToolTip = "Show the leaderboard and the relative in zone " + letter + " for your own class",
            };
            box.Checked += (sender, args) => { screen.Face.SetClassOnly(letter, true); Save(); };
            box.Unchecked += (sender, args) => { screen.Face.SetClassOnly(letter, false); Save(); };
            zoneClassBoxes[letter] = box;
            return box;
        }

        /// <summary>
        /// The enabled pages of a zone. This is the control that decides how long a driver's cycle is,
        /// which makes it the most consequential thing on the panel.
        /// </summary>
        private FrameworkElement BuildMaskDrop(ScreenInstance screen, string letter, double width)
        {
            var button = Ui.DropButton(width, MaskCaption(screen, letter), "Which pages zone " + letter + " cycles through");
            zoneMaskButtons[letter] = button;
            return Ui.Drop(button, BuildMaskPanel(screen, letter));
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
            if (zoneMaskButtons.TryGetValue(letter, out button)) Ui.SetDropText(button, MaskCaption(screen, letter));
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
            CheckBox classOnly;
            if (zoneClassBoxes.TryGetValue(letter, out classOnly))
            {
                var on = screen.Face.IsClassOnly(letter);
                if (classOnly.IsChecked != on) classOnly.IsChecked = on;
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
        /// </remarks>
        private FrameworkElement BuildWheelButtons(ScreenInstance screen)
        {
            var binders = Contract.FaceZoneLetters
                .Select(letter => (UIElement)Ui.Row(Ui.Label("Zone " + letter), BuildBinder(Contract.CycleZoneAction(screen.Namespace, letter), screen.Name + " · zone " + letter)))
                .ToArray();

            var zoneBox = new ComboBox
            {
                Width = 110,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            foreach (var letter in Contract.FaceZoneLetters) zoneBox.Items.Add("Zone " + letter);
            var glance = Contract.NormaliseQuickGlance(screen.Face.QuickGlance);
            zoneBox.SelectedIndex = Contract.QuickGlanceZone(glance);

            var pageHost = new Border { VerticalAlignment = VerticalAlignment.Center };
            Action fillPages = () =>
            {
                var letter = Contract.FaceZoneLetters[Math.Max(0, zoneBox.SelectedIndex)];
                var current = Contract.NormaliseQuickGlance(screen.Face.QuickGlance);
                var page = Contract.QuickGlanceZone(current) == zoneBox.SelectedIndex ? Contract.QuickGlancePage(current) : 0;
                pageHost.Child = BuildPageSelect(FacePages.For(letter), page, 200, index =>
                {
                    screen.Face.QuickGlance = Contract.QuickGlanceValue(zoneBox.SelectedIndex, index);
                    Save();
                    RefreshFaceWarning(screen);
                });
            };
            zoneBox.SelectionChanged += (sender, args) =>
            {
                if (zoneBox.SelectedIndex < 0) return;
                screen.Face.QuickGlance = Contract.QuickGlanceValue(zoneBox.SelectedIndex, 0);
                Save();
                fillPages();
            };
            fillPages();

            var glanceText = Ui.VStack(4, Ui.Body("Quick glance"),
                Ui.Caption("Hold to show one page, release to return. Usually the relative or the track."));
            glanceText.MaxWidth = 420;

            return Ui.Section("Wheel buttons on this screen",
                Ui.Caption("Bound per screen, so a second face can stay still while the one in front of you cycles.", BodyWidth),
                Ui.VStack(6, binders),
                Ui.Row(glanceText, Ui.HStack(8, zoneBox, pageHost)),
                Ui.Row(Ui.Label("Glance button"), BuildBinder(Contract.HoldQuickGlanceActionFor(screen.Namespace), screen.Name + " · quick glance", hold: true)));
        }

        // --- A pit wall ---------------------------------------------------------------------------

        private FrameworkElement BuildPitWallPane(ScreenInstance screen)
        {
            var rows = new List<UIElement>
            {
                Ui.Caption("Three pages share four data zones and one wide zone, so the letters need a picture.", BodyWidth),
                BuildPitWallPicture(),
            };
            foreach (var letter in Contract.PitWallZoneLetters)
            {
                var captured = letter;
                var index = Array.IndexOf(Contract.PitWallZoneLetters, captured);
                var select = BuildZoneSelect(ZonePages.Standard, screen.Zones[index], page =>
                {
                    screen.Zones[index] = Contract.NormaliseZonePage(page, Contract.PitWallDefaultZonePages[index]);
                    Save();
                });
                rows.Add(Ui.Row("Zone " + captured, ZoneDescription(captured), select));
            }
            var wide = BuildZoneSelect(ZonePages.Wide, screen.WideZone, page =>
            {
                screen.WideZone = Contract.NormaliseWideZonePage(page);
                Save();
            });
            rows.Add(Ui.Row("Wide zone", "The full-width zone on the tower page.", wide));
            rows.Add(Ui.Row("Web view address", "The page the Web view zone shows. http or https only; leave empty for none.", BuildWebViewBox(screen)));
            return Ui.VStack(12, rows.ToArray());
        }

        /// <summary>The three pages, so a letter is a position rather than a label.</summary>
        private static FrameworkElement BuildPitWallPicture()
        {
            var race = PitWallPage("Race", new[] { "Board", "A", "B" }, false);
            var tower = PitWallPage("Tower", new[] { "Wide", "C", "D" }, false);
            var telemetry = PitWallPage("Telemetry", new[] { "A", "B", "C" }, true);
            var row = Ui.HStack(16, race, tower, telemetry);
            row.HorizontalAlignment = HorizontalAlignment.Left;
            return row;
        }

        private static FrameworkElement PitWallPage(string title, string[] cells, bool stacked)
        {
            var grid = new Grid { Width = 172, Height = 104, Background = Ui.Brush(Theme.Rule) };
            if (stacked)
            {
                for (var i = 0; i < cells.Length; i++)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                }
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            for (var i = 0; i < cells.Length; i++)
            {
                var label = Ui.Label(cells[i], i == 0 ? Theme.TextSecondary : Theme.TextPrimary);
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                var cell = new Border { Background = Ui.Brush(Theme.SurfaceBase), Margin = new Thickness(0, i > 0 ? 1 : 0, 0, 0), Child = label };
                if (stacked)
                {
                    Grid.SetRow(cell, i);
                }
                else if (i == 0)
                {
                    // The first cell of each landscape page spans both rows on the left.
                    Grid.SetRow(cell, 0);
                    Grid.SetRowSpan(cell, 2);
                    Grid.SetColumn(cell, 0);
                    cell.Margin = new Thickness(0);
                }
                else
                {
                    Grid.SetRow(cell, i - 1);
                    Grid.SetColumn(cell, 1);
                    cell.Margin = new Thickness(1, i > 1 ? 1 : 0, 0, 0);
                }
                grid.Children.Add(cell);
            }

            var caption = Ui.Label(title);
            caption.Margin = new Thickness(0, 6, 0, 0);
            return Ui.VStack(0, new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(1),
                Child = grid,
            }, caption);
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
        private FrameworkElement BuildWebViewBox(ScreenInstance screen)
        {
            var box = new TextBox
            {
                Width = 320,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Text = screen.WebViewUrl ?? string.Empty,
                ToolTip = "An http or https address; anything else is ignored.",
            };
            Action commit = () =>
            {
                screen.WebViewUrl = Contract.NormaliseUrl(box.Text);
                Save();
                if (box.Text != screen.WebViewUrl) box.Text = screen.WebViewUrl;
            };
            box.LostFocus += (sender, args) => commit();
            box.KeyDown += (sender, args)  =>
            {
                if (args.Key == Key.Enter) commit();
            };
            return box;
        }

        // --- A companion ---------------------------------------------------------------------------

        private FrameworkElement BuildCompanionPane(ScreenInstance screen)
        {
            const int columns = 3;
            var rows = (Modules.Count + columns - 1) / columns;
            var grid = new Grid { Width = BodyWidth, HorizontalAlignment = HorizontalAlignment.Left };
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
                grid);
        }

        /// <summary>"07 Tyres" and its toggle; the description is the tooltip, so the grid stays readable.</summary>
        private FrameworkElement BuildModuleRow(ScreenInstance screen, Module module)
        {
            var name = Ui.Text(module.Number.ToString("00") + "  " + module.Name, Theme.SizeLabel, FontWeights.Normal, Theme.TextSecondary);
            name.VerticalAlignment = VerticalAlignment.Center;
            var index = module.Number - 1;
            var toggle = BuildToggle(screen.Modules != null && index < screen.Modules.Length && screen.Modules[index], on =>
            {
                if (screen.Modules == null || index >= screen.Modules.Length) return;
                screen.Modules[index] = on;
                Save();
            });
            toggle.HorizontalAlignment = HorizontalAlignment.Right;
            var row = Ui.Row(name, toggle);
            row.Margin = new Thickness(0, 0, 24, 8);
            row.ToolTip = module.Description;
            return row;
        }

        // --- A slots face ---------------------------------------------------------------------------

        /// <summary>
        /// The twelve-slot picture, for anyone running a card package.
        /// </summary>
        /// <remarks>
        /// On its own screen card rather than in a section of its own, which is how the card model
        /// survives the redesign without cluttering it: it is on screen only if you installed one. It
        /// leaves with the cards in XOR-95.
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
                BuildSlotWarning());
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
