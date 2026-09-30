// SettingsControl.Screens.Face.cs: a face's editor on the Screens page (Screens.dc.html) -- a picture of the
// face that is also its controls, the aside the picture opens, and the rows under it.
//
// The picture is drawn at the face's own proportions (PanelFacePlan): twenty rev segments, the info bar where
// the face has one, zones B, A and C across the body (A over B over C on the portrait), and band D at the
// foot. Each part is a press that opens what it shows in the aside: the Info bar's four fields, or a zone's
// pages as a list to tick and drag. The aside stands beside the picture when the page has two columns and
// under it otherwise. Every change is saved through ScreensSave(screen), because setting something on a screen
// is the driver keeping it, and the editor redraws itself in place so the picture says what was just set.
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
        /// <summary>What each face's aside shows (a zone letter or PanelScreens.BarKey), by namespace, for the
        /// session.</summary>
        private readonly Dictionary<string, string> screensFaceAside = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Whether a zone list shows every page or only the ticked ones, for the session.</summary>
        private bool screensShowAll;

        private static ControlTemplate screensZoneTemplate;

        private FrameworkElement BuildFacePane(ScreenInstance screen)
        {
            var size = screen.FaceSize;
            if (size == null)
            {
                return Ui.VStack(12, Ui.Prose(PanelScreens.NoLongerShipped(screen.SizeLabel), Theme.SizeBody), BuildScreenDetails(screen));
            }
            var face = size.Value;
            var host = new ContentControl { Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            Action redraw = null;
            redraw = () => ScreensRedraw(host, () => BuildFaceEditor(screen, face, redraw));
            redraw();
            return host;
        }

        /// <summary>The picture and the aside it opens, then the rows: in two columns where there is room, one
        /// under the other where there is not.</summary>
        private FrameworkElement BuildFaceEditor(ScreenInstance screen, Contract.FaceSize face, Action redraw)
        {
            string picked;
            screensFaceAside.TryGetValue(screen.Namespace, out picked);
            var key = PanelScreens.AsideKey(picked, face);
            Action<string> pick = chosen =>
            {
                screensFaceAside[screen.Namespace] = chosen;
                // A zone picked opens on its ticked pages, as the artboard's does, whatever the last one showed.
                screensShowAll = false;
                redraw();
            };

            // Beside the aside only while the picture's column is as wide as the artboard's, and the picture
            // no taller than the page can show beside its rows.
            var sideBySide = PanelFacePlan.SideBySide(ContentWidth, TwoColumns);
            var column = PanelFacePlan.PictureWidthFor(ContentWidth, sideBySide);
            var picture = Ui.Anchor(BuildFacePicture(screen, face, PanelFacePlan.FitWidth(face, column), key, pick), PanelScreens.AnchorZones);
            var aside = Ui.CardBox(key == PanelScreens.BarKey ? BuildInfoBarAside(screen, face, redraw) : BuildZoneAside(screen, key, redraw));
            var rows = BuildFaceRows(screen, redraw, column);

            if (!sideBySide) return Ui.VStack(16, picture, aside, rows);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelFacePlan.AsideGap) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelFacePlan.AsideWidth) });
            var left = Ui.VStack(16, picture, rows);
            grid.Children.Add(left);
            aside.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(aside, 2);
            grid.Children.Add(aside);
            return grid;
        }

        // --- The rows under the picture -----------------------------------------------------------------

        private FrameworkElement BuildFaceRows(ScreenInstance screen, Action redraw, double column)
        {
            var ns = screen.Namespace;
            // The screen's own rev bar: off redraws the face without the well, and the strip above goes dark.
            var revBar = ScreensSegmented(PanelDataTab.RevBarValues, PanelDataTab.RevBarLabels, Settings.ScreenRevBar(ns), value =>
            {
                Settings.SetScreenRevBar(ns, value);
                ScreensSave(screen, redraw);
            });
            revBar.Uid = "screens.revbar";
            var flags = ScreensSegmented(Contract.FlagFormats, PanelScreens.FlagLabels, Settings.ScreenFlagFormat(ns), value =>
            {
                screen.FlagFormat = value;
                ScreensSave(screen);
            });
            flags.Uid = "screens.flags";
            var lapReview = ScreensSegmented(Contract.LapReviewModes, PanelScreens.LapReviewLabels, Settings.ScreenLapReview(ns), value =>
            {
                screen.LapReview = value;
                ScreensSave(screen);
            });
            lapReview.Uid = "screens.lapreview";

            var rows = new List<UIElement>
            {
                Ui.Anchor(Ui.SettingRow(PanelScreens.RevBarTitle, revBar), PanelScreens.AnchorRevBar),
                Ui.Anchor(Ui.SettingRow(PanelScreens.FlagDisplayTitle, flags), PanelScreens.AnchorFlagDisplay),
                Ui.Anchor(Ui.SettingRow(PanelScreens.LapReviewTitle, lapReview, PanelScreens.LapReviewCaption), PanelScreens.AnchorLapReview),
                Ui.Anchor(Ui.SettingRow(PanelShortcuts.QuickGlanceTitle, BuildFaceGlance(screen, redraw, column), PanelCopy.FaceGlance), PanelScreens.AnchorGlance),
            };
            var clash = PanelScreens.PageClash(screen.Face);
            if (clash.Length > 0) rows.Add(BuildScreensWarning(clash));
            rows.Add(Ui.SoonRow(PanelSoon.RevFill));
            rows.Add(Ui.SoonRow(PanelSoon.SpotterAtRevBarEnds));
            rows.Add(Ui.SoonRow(PanelSoon.PitPageInPitLane));
            rows.Add(Ui.SoonRow(PanelSoon.PopUps));
            rows.Add(Ui.SoonRow(PanelSoon.DeltaEdgeLights));
            rows.Add(Ui.SoonRow(PanelSoon.ScreenCare));
            rows.Add(Ui.SoonRow(PanelSoon.Fit));
            rows.Add(BuildScreenDetails(screen));
            return Ui.Rows(rows.ToArray());
        }

        /// <summary>
        /// The quick glance: which zone lends its place, then which of that zone's pages it shows, and the
        /// chip saying what the held button is bound to (the binding is on Shortcuts).
        /// </summary>
        /// <remarks>
        /// Two choices in the order a driver makes them, and the second offers only the pages the first zone
        /// carries (ruling 32). The value stays zone × 100 + page.
        /// </remarks>
        private FrameworkElement BuildFaceGlance(ScreenInstance screen, Action redraw, double column)
        {
            var glance = Contract.NormaliseQuickGlance(screen.Face.QuickGlance);
            var zoneIndex = Contract.QuickGlanceZone(glance);
            var zone = Ui.ChoiceButton(PanelScreens.GlanceZoneLabels(), zoneIndex, chosen =>
            {
                screen.Face.QuickGlance = PanelScreens.GlanceWithZone(screen.Face.QuickGlance, chosen);
                ScreensSave(screen, redraw);
            }, PanelScreens.GlanceZoneWidth);
            zone.Uid = "screens.glance.zone";
            var page = Ui.ChoiceButton(PanelScreens.GlancePageLabels(zoneIndex), Contract.QuickGlancePage(glance), chosen =>
            {
                screen.Face.QuickGlance = Contract.QuickGlanceValue(zoneIndex, chosen);
                // The clash line counts the glance among what shows a page twice, so the editor redraws.
                ScreensSave(screen, redraw);
            }, PanelScreens.GlancePageWidth);
            page.Uid = "screens.glance.page";
            var chip = BindingChipFor(Contract.HoldQuickGlanceActionFor(screen.Namespace));
            chip.Uid = "screens.glance.chip";
            return ScreensWrap(PanelScreens.ControlsWidth(column), zone, page, chip);
        }

        /// <summary>Controls that sit side by side and wrap under each other past <paramref name="maxWidth"/>,
        /// 8 apart: a row measures its control at infinite width, so the wrap is told how wide it may be.</summary>
        private static FrameworkElement ScreensWrap(double maxWidth, params FrameworkElement[] controls)
        {
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, MaxWidth = maxWidth };
            foreach (var control in controls)
            {
                control.Margin = new Thickness(8, 2, 0, 2);
                control.VerticalAlignment = VerticalAlignment.Center;
                wrap.Children.Add(control);
            }
            return wrap;
        }

        /// <summary>A caution line with its icon: "Zone B and zone C both show Relative." Said, and allowed.</summary>
        private static FrameworkElement BuildScreensWarning(string message)
        {
            var text = Ui.Prose(message, Theme.SizeSmall, Theme.Caution);
            var icon = Ui.Icon(Ui.WarningIcon, Theme.Caution);
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, 1, 0, 0);
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 10, 0, 10) };
            DockPanel.SetDock(icon, Dock.Left);
            row.Children.Add(icon);
            text.Margin = new Thickness(10, 0, 0, 0);
            row.Children.Add(text);
            return row;
        }

        // --- The picture --------------------------------------------------------------------------------

        /// <summary>
        /// The face as a picture of its own parts, each a press: the rev strip, the info bar, the three zones
        /// and band D, 5 apart on the inset ground.
        /// </summary>
        private FrameworkElement BuildFacePicture(ScreenInstance screen, Contract.FaceSize face, double width, string key, Action<string> pick)
        {
            var plan = PanelFacePlan.For(face, PanelFacePlan.RowsWidth(width));
            var rows = new StackPanel { Orientation = Orientation.Vertical };
            var revOn = !string.Equals(Settings.ScreenRevBar(screen.Namespace), Contract.RevBarOff, StringComparison.Ordinal);
            rows.Children.Add(BuildRevStrip(plan.RevBar, revOn));
            if (plan.HasBar)
            {
                var bar = BuildInfoBarCell(screen, face, plan.Bar, key == PanelScreens.BarKey, () => pick(PanelScreens.BarKey));
                bar.Margin = new Thickness(0, PanelFacePlan.Seam, 0, 0);
                rows.Children.Add(bar);
            }
            var body = BuildFaceBody(screen, plan, key, pick);
            body.Margin = new Thickness(0, PanelFacePlan.Seam, 0, 0);
            rows.Children.Add(body);
            var band = BuildBandCell(screen, plan.Band, PanelFacePlan.BandButtonMax(plan.Width), key == "D", () => pick("D"));
            band.Margin = new Thickness(0, PanelFacePlan.Seam, 0, 0);
            rows.Children.Add(band);
            return new Border
            {
                Width = width,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Ui.Brush(Theme.SurfaceInset),
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(PanelFacePlan.Frame),
                CornerRadius = new CornerRadius(Theme.Radius),
                Padding = new Thickness(PanelFacePlan.Inset),
                Child = rows,
            };
        }

        /// <summary>Twenty segments of a mid-range shift, which are not a control: whether they draw at all is
        /// the Rev bar row under the picture, and what they draw is the car's.</summary>
        private static FrameworkElement BuildRevStrip(double height, bool on)
        {
            var colours = PanelFacePlan.RevColours(on);
            var grid = new Grid();
            for (var i = 0; i < colours.Length; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var segment = new Border
                {
                    Background = Ui.Brush(colours[i]),
                    CornerRadius = new CornerRadius(1),
                    Margin = new Thickness(i == 0 ? 0 : PanelFacePlan.RevGap, 0, 0, 0),
                };
                Grid.SetColumn(segment, i);
                grid.Children.Add(segment);
            }
            return new Border
            {
                Height = height,
                Background = Ui.Brush(Theme.SurfaceBase),
                CornerRadius = new CornerRadius(Theme.Radius),
                Padding = new Thickness(5, 3, 5, 3),
                Child = grid,
                IsHitTestVisible = false,
            };
        }

        /// <summary>The info bar: an end at each side and the car's settings between them, 3 : 4 : 3.</summary>
        private Button BuildInfoBarCell(ScreenInstance screen, Contract.FaceSize face, double height, bool selected, Action pick)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            var left = ScreensCellText(PanelScreens.BarEnd(screen.Face, face, true), PanelFacePlan.BarTextSize, FontWeights.Normal, Theme.TextSecondary);
            var middle = ScreensCellText(PanelScreens.InfoBarMiddle, PanelFacePlan.BarTextSize, FontWeights.Normal, Theme.TextLabel);
            middle.TextAlignment = TextAlignment.Center;
            var right = ScreensCellText(PanelScreens.BarEnd(screen.Face, face, false), PanelFacePlan.BarTextSize, FontWeights.Normal, Theme.TextSecondary);
            right.TextAlignment = TextAlignment.Right;
            Grid.SetColumn(middle, 1);
            Grid.SetColumn(right, 2);
            grid.Children.Add(left);
            grid.Children.Add(middle);
            grid.Children.Add(right);
            var cell = ScreensZoneButton(grid, selected, pick, new Thickness(8, 0, 8, 0));
            cell.Height = height;
            cell.VerticalContentAlignment = VerticalAlignment.Center;
            cell.ToolTip = PanelScreens.InfoBarTitle;
            cell.Uid = "screens.zone." + PanelScreens.BarKey;
            return cell;
        }

        /// <summary>The three body zones, in the order and along the axis this face draws them.</summary>
        private FrameworkElement BuildFaceBody(ScreenInstance screen, PanelFacePlan plan, string key, Action<string> pick)
        {
            var grid = new Grid();
            if (!plan.Stacked) grid.Height = plan.Body;
            for (var i = 0; i < plan.Letters.Length; i++)
            {
                if (plan.Stacked) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(plan.Cells[i] + (i == 0 ? 0 : PanelFacePlan.Seam)) });
                else grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(plan.Cells[i] + (i == 0 ? 0 : PanelFacePlan.Seam)) });
            }
            for (var i = 0; i < plan.Letters.Length; i++)
            {
                var letter = plan.Letters[i];
                var cell = BuildZoneCell(screen, letter, key == letter, () => pick(letter), letter == "A" && !plan.Stacked);
                if (plan.Stacked)
                {
                    cell.Margin = new Thickness(0, i == 0 ? 0 : PanelFacePlan.Seam, 0, 0);
                    Grid.SetRow(cell, i);
                }
                else
                {
                    cell.Margin = new Thickness(i == 0 ? 0 : PanelFacePlan.Seam, 0, 0, 0);
                    Grid.SetColumn(cell, i);
                }
                grid.Children.Add(cell);
            }
            return grid;
        }

        /// <summary>One zone: its letter and how many pages it cycles, the page it opens on, and the button
        /// that advances it. Zone A's page is centred, as it sits in the middle of the face.</summary>
        private Button BuildZoneCell(ScreenInstance screen, string letter, bool selected, Action pick, bool centred)
        {
            var top = new DockPanel { LastChildFill = true };
            var count = ScreensCellText(PanelScreens.ZoneCount(screen.Face, letter), PanelFacePlan.CountSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
            DockPanel.SetDock(count, Dock.Right);
            top.Children.Add(count);
            top.Children.Add(ScreensCellText(letter, PanelFacePlan.LetterSize, FontWeights.SemiBold, selected ? Theme.Accent : Theme.TextSecondary, PanelFonts.Data));

            var page = ScreensCellText(FacePages.NameOf(letter, PanelScreens.FirstTicked(screen.Face, letter)), PanelFacePlan.PageSize, FontWeights.SemiBold, Theme.TextPrimary);
            page.VerticalAlignment = VerticalAlignment.Center;
            if (centred) page.TextAlignment = TextAlignment.Center;
            var button = ScreensCellText(PanelScreens.ZoneButtonLine(TriggersOf(Contract.CycleZoneAction(screen.Namespace, letter))), PanelFacePlan.ButtonLineSize, FontWeights.Normal, Theme.TextSecondary);

            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(top, Dock.Top);
            DockPanel.SetDock(button, Dock.Bottom);
            button.Margin = new Thickness(0, PanelFacePlan.CellGap, 0, 0);
            page.Margin = new Thickness(0, PanelFacePlan.CellGap, 0, 0);
            dock.Children.Add(top);
            dock.Children.Add(button);
            dock.Children.Add(page);
            var cell = ScreensZoneButton(dock, selected, pick, new Thickness(PanelFacePlan.CellPaddingX, PanelFacePlan.CellPaddingY, PanelFacePlan.CellPaddingX, PanelFacePlan.CellPaddingY));
            cell.ToolTip = PanelFacePlan.ZoneLabel(letter);
            cell.Uid = "screens.zone." + letter;
            return cell;
        }

        /// <summary>Band D across the foot: the same facts as a zone, along the row.</summary>
        private Button BuildBandCell(ScreenInstance screen, double height, double buttonMax, bool selected, Action pick)
        {
            var dock = new DockPanel { LastChildFill = true };
            var letter = ScreensCellText("D", PanelFacePlan.BandLetterSize, FontWeights.SemiBold, selected ? Theme.Accent : Theme.TextSecondary, PanelFonts.Data);
            letter.Margin = new Thickness(0, 0, 12, 0);
            DockPanel.SetDock(letter, Dock.Left);
            dock.Children.Add(letter);
            var button = ScreensCellText(PanelScreens.ZoneButtonLine(TriggersOf(Contract.CycleZoneAction(screen.Namespace, "D"))), PanelFacePlan.ButtonLineSize, FontWeights.Normal, Theme.TextSecondary);
            button.Margin = new Thickness(12, 0, 0, 0);
            // The page is what the band shows; a long binding is cut short before it is.
            button.MaxWidth = buttonMax;
            DockPanel.SetDock(button, Dock.Right);
            dock.Children.Add(button);
            var count = ScreensCellText(PanelScreens.ZoneCount(screen.Face, "D"), PanelFacePlan.CountSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
            count.Margin = new Thickness(12, 0, 0, 0);
            DockPanel.SetDock(count, Dock.Right);
            dock.Children.Add(count);
            dock.Children.Add(ScreensCellText(FacePages.NameOf("D", PanelScreens.FirstTicked(screen.Face, "D")), PanelFacePlan.BandPageSize, FontWeights.SemiBold, Theme.TextPrimary));
            var cell = ScreensZoneButton(dock, selected, pick, new Thickness(PanelFacePlan.CellPaddingX, 0, PanelFacePlan.CellPaddingX, 0));
            cell.Height = height;
            cell.VerticalContentAlignment = VerticalAlignment.Center;
            cell.ToolTip = PanelFacePlan.ZoneLabel("D");
            cell.Uid = "screens.zone.D";
            return cell;
        }

        /// <summary>A line of a cell, cut short rather than wrapped: a cell is as tall as the face makes it.</summary>
        private static TextBlock ScreensCellText(string text, double size, FontWeight weight, string hex, FontFamily family = null)
        {
            var block = Ui.Text(text ?? string.Empty, size, weight, hex, family);
            block.TextTrimming = TextTrimming.CharacterEllipsis;
            block.VerticalAlignment = VerticalAlignment.Center;
            return block;
        }

        /// <summary>
        /// A part of the picture as a press (the artboard's .zone): the zone ground inside the rule, outlined 2
        /// in the accent when it is the one the aside shows, and the hover ground under the pointer.
        /// </summary>
        private static Button ScreensZoneButton(UIElement content, bool selected, Action pick, Thickness padding)
        {
            var edge = selected ? PanelFacePlan.SelectedEdge : PanelMetrics.BorderWeight;
            var less = edge - PanelMetrics.BorderWeight;
            var button = new Button
            {
                Background = Ui.Brush(Theme.SurfaceZone),
                BorderBrush = Ui.Brush(selected ? Theme.Accent : Theme.Rule),
                BorderThickness = new Thickness(edge),
                Padding = new Thickness(Math.Max(0, padding.Left - less), Math.Max(0, padding.Top - less), Math.Max(0, padding.Right - less), Math.Max(0, padding.Bottom - less)),
                Content = content,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Cursor = Cursors.Hand,
                FocusVisualStyle = Ui.FocusRing(),
                Template = ScreensZoneTemplate(),
            };
            System.Windows.Automation.AutomationProperties.SetItemStatus(button, selected ? "pressed" : "not pressed");
            if (pick != null) button.Click += (sender, args) => pick();
            return button;
        }

        private static ControlTemplate ScreensZoneTemplate()
        {
            if (screensZoneTemplate != null) return screensZoneTemplate;
            var chrome = new FrameworkElementFactory(typeof(Border), "chrome");
            chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            chrome.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            chrome.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            chrome.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(Theme.Radius));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            chrome.AppendChild(presenter);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = chrome };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, Ui.Brush(Theme.Hover), "chrome"));
            template.Triggers.Add(hover);
            screensZoneTemplate = template;
            return template;
        }

        // --- The asides ---------------------------------------------------------------------------------

        /// <summary>The Info bar: a choice per end field, from FacePages.BarFields.</summary>
        private FrameworkElement BuildInfoBarAside(ScreenInstance screen, Contract.FaceSize face, Action redraw)
        {
            var stack = Ui.VStack(12, Ui.Heading(PanelScreens.InfoBarTitle));
            var fields = FacePages.BarFields.Select(field => field.Name).ToArray();
            foreach (var row in PanelScreens.BarRows(face))
            {
                var slot = row.Slot;
                var choice = Ui.ChoiceButton(fields, screen.Face.BarField(slot), index =>
                {
                    screen.Face.SetBarField(slot, index);
                    ScreensSave(screen, redraw);
                });
                choice.Uid = "screens.bar." + slot;
                stack.Children.Add(ScreensAsideLine(Ui.Text(row.Label, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary), choice));
            }
            return stack;
        }

        /// <summary>A line of an aside: its words on the left and its control on the right.</summary>
        private static FrameworkElement ScreensAsideLine(FrameworkElement words, FrameworkElement control)
        {
            var line = new DockPanel { LastChildFill = true };
            control.VerticalAlignment = VerticalAlignment.Center;
            control.Margin = new Thickness(12, 0, 0, 0);
            DockPanel.SetDock(control, Dock.Right);
            line.Children.Add(control);
            words.VerticalAlignment = VerticalAlignment.Center;
            line.Children.Add(words);
            return line;
        }

        /// <summary>
        /// A zone: its pages in the order it cycles from the page it opens on, which is drawn first and marked
        /// First, each ticked into the cycle or not and dragged into place; then the class filter where
        /// the zone lists cars, and the two buttons that page it.
        /// </summary>
        private FrameworkElement BuildZoneAside(ScreenInstance screen, string letter, Action redraw)
        {
            var face = screen.Face;
            var head = new DockPanel { LastChildFill = true };
            var count = Ui.Text(PanelScreens.ZoneCount(face, letter), PanelScreens.HeadCountSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
            count.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(count, Dock.Right);
            head.Children.Add(count);
            head.Children.Add(Ui.Heading(PanelFacePlan.ZoneLabel(letter)));

            // A tick or an untick leaves the start where it is unless it unticked the start itself, which
            // FaceSettings moves on to the next ticked page; a drag moves it only when it puts another page
            // first (ruling 29). Never a SetStart per press: that would put the running zone back on its first
            // page every time a box was ticked.
            Action settle = () =>
            {
                ScreensSave(screen, redraw);
            };
            var showAll = screensShowAll;
            var rows = new List<FrameworkElement>();
            foreach (var row in PanelScreens.ZoneRows(face, letter, showAll))
            {
                var page = row.Page;
                rows.Add(BuildZonePageRow(row, on =>
                {
                    PanelScreens.Tick(face, letter, page, on);
                    settle();
                }));
            }
            var list = Ui.Reorderable(rows, (from, to) =>
            {
                PanelScreens.Reorder(face, letter, showAll, from, to);
                settle();
            });
            var pages = Ui.VStack(2, list);
            if (showAll && PanelScreens.ListsSoonModules(letter))
            {
                pages.Children.Add(Ui.Soon(BuildSoonPageRow(PanelSoon.CircleTracker.Title, Ui.SoonTag(PanelSoon.CircleTracker)), PanelSoon.CircleTracker));
                pages.Children.Add(Ui.Soon(BuildSoonPageRow(PanelSoon.Launch.Title, Ui.SoonTag(PanelSoon.Launch)), PanelSoon.Launch));
            }

            var links = Ui.HStack(12,
                ScreensLink("screens.showall", showAll ? PanelScreens.OnlyTicked : PanelScreens.ShowAll, () =>
                {
                    screensShowAll = !screensShowAll;
                    redraw();
                }),
                ScreensLink("screens.all", PanelScreens.AllPages, () => { PanelScreens.SetEveryPage(face, letter, true); settle(); }, PanelScreens.AllPagesTooltip),
                ScreensLink("screens.none", PanelScreens.NoPages, () => { PanelScreens.SetEveryPage(face, letter, false); settle(); }, PanelScreens.NoPagesTooltip));
            var hint = Ui.HStack(6, Ui.Text(PanelScreens.DragHint, Theme.SizeLabel, FontWeights.Normal, Theme.TextSecondary), Ui.NewTag());
            var foot = ScreensAsideLine(links, hint);

            var stack = Ui.VStack(14, head, pages, foot);
            if (FacePages.OffersClassFilter(letter))
            {
                var classOnly = Ui.Switch(face.IsClassOnly(letter), on =>
                {
                    face.SetClassOnly(letter, on);
                    ScreensSave(screen);
                });
                // No hover: the label beside it already says it. Named for a screen reader, which the label
                // beside it is not tied to.
                System.Windows.Automation.AutomationProperties.SetName(classOnly, PanelScreens.ClassOnlyTitle);
                classOnly.Uid = "screens.classonly";
                var line = ScreensAsideLine(Ui.Text(PanelScreens.ClassOnlyTitle, Theme.SizeBody, FontWeights.Medium, Theme.TextPrimary), classOnly);
                stack.Children.Add(Ui.Anchor(ScreensRuled(line), PanelScreens.AnchorClassOnly));
            }
            var previous = Ui.HStack(8, Ui.Text(PanelScreens.PreviousPageTitle, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary), Ui.NewTag());
            var nextChip = ScreensAsideChip(BindingChipFor(Contract.CycleZoneAction(screen.Namespace, letter)));
            nextChip.Uid = "screens.zone.next";
            var backChip = ScreensAsideChip(BindingChipFor(Contract.CycleZoneBackAction(screen.Namespace, letter)));
            backChip.Uid = "screens.zone.back";
            stack.Children.Add(ScreensRuled(Ui.VStack(10,
                ScreensAsideLine(Ui.Text(PanelScreens.NextPageTitle, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary), nextChip),
                ScreensAsideLine(previous, backChip))));
            return stack;
        }

        /// <summary>One page of the zone list: its tick, its name, and First or Not in iRacing after it. The
        /// last ticked page cannot be unticked, since a zone with no pages draws nothing: its tick stays
        /// answering, the settings refuse the untick, and the redraw puts the tick back. The whole row is the
        /// tick's label, as the artboard's .pg is a label.</summary>
        private static FrameworkElement BuildZonePageRow(ZoneRow row, Action<bool> ticked)
        {
            var box = new CheckBox
            {
                IsChecked = row.Ticked,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                // The name is printed beside the tick, so only the locked page has something to add.
                ToolTip = row.Locked ? PanelScreens.LastPageTooltip : null,
                Uid = "screens.page." + row.Page,
            };
            System.Windows.Automation.AutomationProperties.SetName(box, row.Name);
            box.Checked += (sender, args) => ticked(true);
            box.Unchecked += (sender, args) => ticked(false);
            var line = new DockPanel { LastChildFill = true, Height = 32 };
            ScreensLabelFor(line, box);
            DockPanel.SetDock(box, Dock.Left);
            line.Children.Add(box);
            if (row.First)
            {
                var first = Ui.Eyebrow(PanelScreens.FirstTag, Theme.Accent);
                first.VerticalAlignment = VerticalAlignment.Center;
                first.Margin = new Thickness(8, 0, 0, 0);
                DockPanel.SetDock(first, Dock.Right);
                line.Children.Add(first);
            }
            if (row.NotInIracing)
            {
                var none = Ui.Text(PanelScreens.NotInIracing, Theme.SizeLabel, FontWeights.Normal, Theme.TextSecondary);
                none.VerticalAlignment = VerticalAlignment.Center;
                none.Margin = new Thickness(8, 0, 0, 0);
                DockPanel.SetDock(none, Dock.Right);
                line.Children.Add(none);
            }
            var name = ScreensCellText(row.Name, Theme.SizeBody, FontWeights.Normal, row.Ticked ? Theme.TextPrimary : Theme.TextSecondary);
            line.Children.Add(name);
            return line;
        }

        /// <summary>
        /// Makes a press anywhere on <paramref name="row"/> toggle <paramref name="box"/>, as a label wrapped
        /// round a checkbox does; a press on the box itself is the box's own.
        /// </summary>
        /// <remarks>
        /// On a release that ends a press on the same row, as the segmented option chooses: the press takes
        /// the mouse, so a click that closed a flyout or a sheet's dim over the row does not tick it.
        /// </remarks>
        private static void ScreensLabelFor(Panel row, CheckBox box)
        {
            row.Background = Brushes.Transparent;
            row.Cursor = Cursors.Hand;
            row.MouseLeftButtonDown += (sender, args) =>
            {
                var source = args.OriginalSource as DependencyObject;
                if (source != null && (ReferenceEquals(source, box) || box.IsAncestorOf(source))) return;
                if (row.CaptureMouse()) args.Handled = true;
            };
            row.MouseLeftButtonUp += (sender, args) =>
            {
                if (!row.IsMouseCaptured) return;
                row.ReleaseMouseCapture();
                args.Handled = true;
                var at = args.GetPosition(row);
                if (at.X < 0 || at.Y < 0 || at.X > row.ActualWidth || at.Y > row.ActualHeight) return;
                box.IsChecked = box.IsChecked != true;
            };
        }

        /// <summary>A binding chip in the zone aside, cut short at <see cref="PanelFacePlan.AsideChipMax"/> so
        /// a long device name leaves the words beside it their room.</summary>
        private static FrameworkElement ScreensAsideChip(FrameworkElement chip)
        {
            chip.MaxWidth = PanelFacePlan.AsideChipMax;
            var button = chip as ContentControl;
            var label = button == null ? null : button.Content as TextBlock;
            if (label != null) label.TextTrimming = TextTrimming.CharacterEllipsis;
            return chip;
        }

        /// <summary>A page not built yet, greyed in the list under Show all, beside the grip's room: 16 in, where
        /// a real row's tick stands after its grip, so the ticks and the tags line up.</summary>
        private static FrameworkElement BuildSoonPageRow(string title, FrameworkElement tag)
        {
            var line = new DockPanel { LastChildFill = true, Height = 32, Margin = new Thickness(16, 0, 0, 0) };
            tag.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(tag, Dock.Right);
            line.Children.Add(tag);
            var box = new CheckBox { IsChecked = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            DockPanel.SetDock(box, Dock.Left);
            line.Children.Add(box);
            line.Children.Add(ScreensCellText(title, Theme.SizeBody, FontWeights.Normal, Theme.TextSecondary));
            return line;
        }

        /// <summary>A block of an aside under a rule, 12 below it.</summary>
        private static FrameworkElement ScreensRuled(UIElement child)
        {
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(0, 12, 0, 0),
                Child = child,
            };
        }

        /// <summary>A press drawn as a word in the accent, with nothing around it: Show all, All, None.</summary>
        private static Button ScreensLink(string uid, string text, Action click, string tooltip = null)
        {
            var button = new Button
            {
                Uid = uid,
                ToolTip = tooltip,
                Content = Ui.Text(text, Theme.SizeSmall, FontWeights.Medium, Theme.Accent),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                FocusVisualStyle = Ui.FocusRing(),
                Template = ScreensZoneTemplate(),
            };
            button.Click += (sender, args) => click();
            return button;
        }
    }
}
