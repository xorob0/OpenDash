// SettingsControl.Screens.Face.cs: what a face shows under its card on the Screens page.
//
// The face is drawn as a plan of itself -- the rev bar, the bar if it has one, zones B, A and C across the
// body and band D at the foot -- at that face's own proportions, so the portrait reads as a column and the
// nano at 800 x 286 shows no bar because it has none. It is a plan rather than a list because "zone C" means
// nothing until you see where zone C is. Re-hosted from the old Rig tab by the #503 foundation; the Screens
// page agent owns it. Every change is saved through Save(screen), because setting something on a screen is
// the driver keeping it.
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
        private readonly Dictionary<string, ComboBox> zoneSelects = new Dictionary<string, ComboBox>();
        private readonly Dictionary<string, ToggleButton> zoneMaskButtons = new Dictionary<string, ToggleButton>();
        private readonly Dictionary<string, List<CheckBox>> zoneMaskBoxes = new Dictionary<string, List<CheckBox>>();
        private readonly Dictionary<string, ToggleButton> barEndButtons = new Dictionary<string, ToggleButton>();
        private TextBlock faceWarningText;
        private FrameworkElement faceWarningRow;


        private FrameworkElement BuildFacePane(ScreenInstance screen)
        {
            var size = screen.FaceSize;
            if (size == null)
            {
                return Ui.Caption(
                    "OpenDash no longer ships a " + screen.SizeLabel + " face. Your settings are kept.",
                    BodyWidth);
            }
            var face = size.Value;

            // The controls this build holds go with the page: a refresh after it has gone must not write into
            // a control nobody can see and read as having done something.
            OnDrop(() =>
            {
                zoneSelects.Clear();
                zoneMaskButtons.Clear();
                zoneMaskBoxes.Clear();
                barEndButtons.Clear();
                faceWarningText = null;
                faceWarningRow = null;
            });
            var picture = BuildFacePicture(screen, face);
            picture.Margin = new Thickness(0, 0, 0, 12);
            var warning = BuildFaceWarning(screen);
            warning.Margin = new Thickness(0, 0, 0, 12);
            var rows = new List<UIElement>
            {
                Ui.Anchor(picture, PanelScreens.AnchorZones),
                warning,
                Ui.Anchor(BuildRevBarRow(screen), PanelScreens.AnchorRevBar),
                Ui.Anchor(BuildFlagFormatRow(screen), PanelScreens.AnchorFlagDisplay),
                Ui.Anchor(BuildLapReviewRow(screen), PanelScreens.AnchorLapReview),
                BuildFaceGlanceRow(screen),
            };
            RefreshFaceWarning(screen);
            return Ui.VStack(0, rows.ToArray());
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
                Save(screen);
                // The plan above redraws without the well, which is the whole of what Off does.
                Redraw();
            });
            return Ui.Row(PanelDataTab.RevBarTitle, PanelDataTab.RevBarCaption, control);
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
                Save(screen);
            });
            var row = Ui.Row(
                PanelScreens.FlagDisplayTitle,
                "Full screen covers the zones while the flag is out.",
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
                Save(screen);
            });
            var row = Ui.Row(
                PanelScreens.LapReviewTitle,
                "Shows your last lap for four seconds after the line.",
                control);
            row.HorizontalAlignment = HorizontalAlignment.Stretch;
            return row;
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
            var button = Ui.DropButton(width, BarEndCaption(screen, firstSlot, secondSlot), secondSlot == null ? "The field at this end of the bar" : "The two fields at this end of the bar");
            barEndButtons[firstSlot] = button;
            var rows = new List<UIElement>();
            var slots = secondSlot == null ? new[] { firstSlot } : new[] { firstSlot, secondSlot };
            foreach (var slot in slots)
            {
                var captured = slot;
                var select = BuildPageSelect(FacePages.BarFields, screen.Face.BarField(captured), 200, index =>
                {
                    screen.Face.SetBarField(captured, index);
                    Save(screen);
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

        /// <summary>Band D, across the foot: the same controls as a body zone, laid along the row
        /// rather than stacked, since a band has the width and not the height. The class filter is
        /// among them because D7 reads it, and the row asks the same question a zone cell asks rather
        /// than naming the letters itself, so one rule decides both.</summary>
        private FrameworkElement BuildBandStrip(ScreenInstance screen)
        {
            var children = new List<UIElement>
            {
                Ui.Label(PanelFacePlan.ZoneLabel("D"), Theme.TextSecondary),
                BuildZoneSelectFor(screen, "D", PanelFacePlan.BandSelectWidth),
                BuildMaskDrop(screen, "D", PanelFacePlan.BandSelectWidth),
            };
            if (FacePages.OffersClassFilter("D")) children.Add(BuildClassFilterRow(screen, "D"));
            var row = Ui.HStack(PanelFacePlan.BandGap, children.ToArray());
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
                Save(screen);
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
            var toggle = BuildToggle(screen.Face.IsClassOnly(letter), on => { screen.Face.SetClassOnly(letter, on); Save(screen); });
            toggle.ToolTip = "Show only your own class in zone " + letter;
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
            Save(screen);
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
            Save(screen);
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

        /// <summary>
        /// The quick glance on this screen: the one page a held button shows. The button itself is bound on
        /// Shortcuts, with every other binding; the chip says what it is bound to and goes there.
        /// </summary>
        private FrameworkElement BuildFaceGlanceRow(ScreenInstance screen)
        {
            var chip = BindingChipFor(Contract.HoldQuickGlanceActionFor(screen.Namespace));
            return Ui.Row(PanelShortcuts.QuickGlanceTitle, null, Ui.HStack(8, BuildGlanceSelect(screen), chip));
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
                Save(screen);
                // The clash line counts the glance among the participants, so the warning has to be asked
                // again here and not only when a zone moves.
                RefreshFaceWarning(screen);
            };
            return select;
        }
    }
}
