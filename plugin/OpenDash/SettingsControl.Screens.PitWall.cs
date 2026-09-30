// SettingsControl.Screens.PitWall.cs: a pit wall's editor on the Screens page (Screens.dc.html).
//
// A landscape wall shows one of three pages, picked with "Page on screen", which drives both the picture at
// the wall's own 16 by 9 and the zone list beside it: a zone letter is a position on that page, so the list
// only names the zones the picture draws. A wall standing on end has one layout of four zones, set as the
// "Portrait layout" row instead of the three pages. The glance's binding is on Shortcuts; the chip beside
// its choices says what it is bound to and goes there.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private FrameworkElement BuildPitWallPane(ScreenInstance screen)
        {
            var host = new ContentControl { Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            Action redraw = null;
            redraw = () => ScreensRedraw(host, () => BuildPitWallEditor(screen, redraw));
            redraw();
            return host;
        }

        private FrameworkElement BuildPitWallEditor(ScreenInstance screen, Action redraw)
        {
            var portrait = PanelScreens.IsPortrait(screen);
            var blocks = new List<UIElement>();
            var rows = new List<UIElement>();
            if (!portrait)
            {
                var page = Contract.NormalisePitWallPage(screen.PitWallPage);
                var values = Enumerable.Range(0, Contract.PitWallPageNames.Length).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray();
                var pages = ScreensSegmented(values, Contract.PitWallPageNames, page.ToString(CultureInfo.InvariantCulture), value =>
                {
                    int chosen;
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out chosen)) return;
                    screen.PitWallPage = Contract.NormalisePitWallPage(chosen);
                    ScreensSave(screen, redraw);
                });
                pages.Uid = "screens.pitwall.page";
                var pageRow = Ui.SettingRow(PanelScreens.PitWallPageTitle, pages);
                pageRow.BorderThickness = new Thickness(0);
                pageRow.Padding = new Thickness(0, 0, 0, 12);
                blocks.Add(Ui.Anchor(pageRow, PanelScreens.AnchorPitWallPage));
                blocks.Add(BuildPitWallLayout(screen, page, redraw));
            }

            rows.Add(Ui.Anchor(Ui.SettingRow(PanelScreens.WebViewTitle, BuildWebViewBox(screen)), PanelScreens.AnchorWebView));
            // One answer for the screen and not one per zone, as a face has: the zones are widgets pointed at
            // one dashboard file per rectangle, so two zones of one column are the same file.
            var classOnly = Ui.Switch(screen.PitWallClassOnly, on => { screen.PitWallClassOnly = on; ScreensSave(screen); });
            System.Windows.Automation.AutomationProperties.SetName(classOnly, PanelScreens.ClassOnlyTitle);
            classOnly.Uid = "screens.pitwall.classonly";
            rows.Add(Ui.Anchor(Ui.SettingRow(PanelScreens.ClassOnlyTitle, classOnly), PanelScreens.AnchorClassOnly));
            var flags = ScreensSegmented(Contract.CompanionFlagFormats, PanelScreens.BarFlagLabels, Settings.ScreenPitWallFlagFormat(screen.Namespace),
                value => { screen.PitWallFlagFormat = Contract.NormalisePitWallFlagFormat(value); ScreensSave(screen); });
            flags.Uid = "screens.pitwall.flags";
            rows.Add(Ui.Anchor(Ui.SettingRow(PanelScreens.FlagDisplayTitle, flags), PanelScreens.AnchorFlagDisplay));
            if (portrait)
            {
                rows.Add(Ui.Anchor(Ui.SettingRow(PanelScreens.PortraitTitle, BuildPortraitLayout(screen, redraw), null, Ui.NewTag()), PanelScreens.AnchorPortrait));
            }
            else
            {
                rows.Add(Ui.Anchor(Ui.SettingRow(PanelShortcuts.QuickGlanceTitle, BuildPitWallGlance(screen, redraw), PanelCopy.PitWallGlance), PanelScreens.AnchorGlance));
            }
            blocks.Add(Ui.Rows(rows.ToArray()));
            return Ui.VStack(16, blocks.ToArray());
        }

        /// <summary>The page on screen, and the list of its zones beside it where there are two columns and
        /// under it where there are not.</summary>
        private FrameworkElement BuildPitWallLayout(ScreenInstance screen, int page, Action redraw)
        {
            var twoColumns = TwoColumns;
            var picture = Ui.Anchor(BuildPitWallPicture(screen, PanelPitWallPlan.Pages[page], PanelPitWallPlan.PictureWidthFor(ContentWidth, twoColumns)), PanelScreens.AnchorZones);
            var list = BuildPitWallZoneList(screen, page, redraw);
            if (!twoColumns) return Ui.VStack(16, picture, list);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelPitWallPlan.ListGap) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelPitWallPlan.ListWidth) });
            grid.Children.Add(picture);
            list.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(list, 2);
            grid.Children.Add(list);
            return grid;
        }

        /// <summary>
        /// One page, drawn at the rectangles PanelPitWallPlan gives it scaled to the width inside its frame:
        /// the picture is <paramref name="outer"/> wide with its rule, never 2 px past its column.
        /// </summary>
        /// <remarks>
        /// A Canvas rather than a Grid because the three pages have three shapes and only one of them is a
        /// table: the Tower page puts a wide zone across the top of its right half and two zones side by
        /// side under it, which no arrangement of star rows and columns draws.
        /// </remarks>
        private FrameworkElement BuildPitWallPicture(ScreenInstance screen, PanelPitWallPlan.Page page, double outer)
        {
            var width = PanelPitWallPlan.CanvasWidth(outer);
            var canvas = new Canvas
            {
                Width = width,
                Height = PanelPitWallPlan.PictureHeight(width),
                Background = Ui.Brush(Theme.SurfaceInset),
            };
            foreach (var panel in PanelPitWallPlan.Scaled(page, width))
            {
                var slot = Contract.PitWallZoneSlotByKey(page.Title + panel.Name);
                var name = ScreensCellText(panel.Configurable ? PanelScreens.PitWallZoneLabel(slot) : panel.Name, PanelPitWallPlan.ZoneNameSize, FontWeights.SemiBold, Theme.TextSecondary);
                // A zone's page, or what a fixed panel always shows.
                var what = ScreensCellText(slot == null ? panel.Shows ?? string.Empty : ScreensZonePageName(slot, screen), PanelPitWallPlan.ZonePageSize, FontWeights.SemiBold, Theme.TextPrimary);
                what.Margin = new Thickness(0, 4, 0, 0);
                var box = new Border
                {
                    Width = panel.Width,
                    Height = panel.Height,
                    Background = Ui.Brush(Theme.SurfaceZone),
                    BorderBrush = Ui.Brush(Theme.Rule),
                    BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                    CornerRadius = new CornerRadius(Theme.Radius),
                    Padding = new Thickness(PanelPitWallPlan.ZonePaddingX, PanelPitWallPlan.ZonePaddingY, PanelPitWallPlan.ZonePaddingX, PanelPitWallPlan.ZonePaddingY),
                    Child = Ui.VStack(0, name, what),
                };
                Canvas.SetLeft(box, panel.X);
                Canvas.SetTop(box, panel.Y);
                canvas.Children.Add(box);
            }
            return new Border
            {
                Width = outer,
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(PanelPitWallPlan.Frame),
                CornerRadius = new CornerRadius(Theme.Radius),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = canvas,
            };
        }

        /// <summary>The page a zone shows, by name.</summary>
        private static string ScreensZonePageName(Contract.PitWallZoneSlot slot, ScreenInstance screen)
        {
            var page = screen.ZonePage(slot.Key);
            return slot.Wide ? ZonePages.WideName(page) : ZonePages.StandardName(page);
        }

        /// <summary>The card beside the picture: the page's zones, each a choice of what it shows. A choice
        /// redraws the editor, so the picture and the list both say what was just set.</summary>
        private FrameworkElement BuildPitWallZoneList(ScreenInstance screen, int page, Action redraw)
        {
            var stack = Ui.VStack(10, Ui.Eyebrow(PanelScreens.PitWallZonesLabel(Contract.PitWallPageNames[page])));
            foreach (var slot in PanelScreens.PitWallZones(page))
            {
                var captured = slot;
                var pages = (captured.Wide ? ZonePages.Wide : ZonePages.Standard).Select(p => p.Name).ToArray();
                var choice = Ui.ChoiceButton(pages, screen.ZonePage(captured.Key), index =>
                {
                    screen.SetZonePage(captured.Key, index);
                    ScreensSave(screen, redraw);
                });
                choice.Uid = "screens.pitwall.zone." + captured.Key;
                // No hover saying where the zone is: the picture beside the list shows it.
                var label = Ui.Text(PanelScreens.PitWallZoneLabel(captured), Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary);
                stack.Children.Add(ScreensAsideLine(label, choice));
            }
            return Ui.CardBox(stack, 14);
        }

        /// <summary>The portrait wall's four zones, one choice each, named with their letter.</summary>
        private FrameworkElement BuildPortraitLayout(ScreenInstance screen, Action redraw)
        {
            var choices = new List<FrameworkElement>();
            foreach (var slot in PanelScreens.PortraitZones())
            {
                var captured = slot;
                var choice = Ui.ChoiceButton(PanelScreens.PortraitLabels(captured.Slot), screen.ZonePage(captured.Key), index =>
                {
                    screen.SetZonePage(captured.Key, index);
                    ScreensSave(screen, redraw);
                }, PanelScreens.PortraitChoiceWidth);
                choice.Uid = "screens.pitwall.portrait." + captured.Key;
                choice.ToolTip = PanelPitWallPlan.ZonePosition(captured);
                choices.Add(choice);
            }
            return ScreensWrap(PanelScreens.ControlsWidth(ContentWidth), choices.ToArray());
        }

        /// <summary>
        /// The glance: which zone lends its place, then which page it shows there, and the chip saying what
        /// the held button is bound to.
        /// </summary>
        private FrameworkElement BuildPitWallGlance(ScreenInstance screen, Action redraw)
        {
            var glance = Contract.NormalisePitWallQuickGlance(screen.PitWallQuickGlance);
            var zoneIndex = Contract.QuickGlanceZone(glance);
            var page = Contract.QuickGlancePage(glance);
            var zone = Ui.ChoiceButton(PanelScreens.PitWallGlanceZoneLabels(), zoneIndex, chosen =>
            {
                screen.PitWallQuickGlance = Contract.PitWallQuickGlanceValue(chosen, Contract.QuickGlancePage(Contract.NormalisePitWallQuickGlance(screen.PitWallQuickGlance)));
                ScreensSave(screen, redraw);
            }, PanelScreens.GlanceZoneWidth);
            zone.Uid = "screens.pitwall.glance.zone";
            var pages = Ui.ChoiceButton(ZonePages.Standard.Select(p => p.Name).ToArray(), page, chosen =>
            {
                screen.PitWallQuickGlance = Contract.PitWallQuickGlanceValue(Contract.QuickGlanceZone(Contract.NormalisePitWallQuickGlance(screen.PitWallQuickGlance)), chosen);
                ScreensSave(screen, redraw);
            }, PanelScreens.GlancePageWidth);
            pages.Uid = "screens.pitwall.glance.page";
            var chip = BindingChipFor(Contract.HoldQuickGlanceActionFor(screen.Namespace));
            chip.Uid = "screens.pitwall.glance.chip";
            return ScreensWrap(PanelScreens.ControlsWidth(ContentWidth), zone, pages, chip);
        }

        /// <summary>
        /// The web view address. Written when the box loses focus or Enter is pressed, then normalised.
        /// </summary>
        /// <remarks>
        /// The "https://" an empty box shows is drawn and not stored. Contract.NormaliseUrl keeps only an
        /// absolute address, so a bare scheme written into the setting would be blanked on the first
        /// commit and the box would empty itself in front of whoever was typing into it. WPF has no
        /// watermark of its own, so it is a text block over the box rather than behind it.
        /// </remarks>
        private FrameworkElement BuildWebViewBox(ScreenInstance screen)
        {
            var width = PanelPitWallPlan.AddressWidthFor(ContentWidth);
            var box = Ui.Input(screen.WebViewUrl ?? string.Empty, width);
            var watermark = Ui.Text(PanelPitWallPlan.AddressPlaceholder, PanelShell.InputTextSize, FontWeights.Normal, Theme.TextLabel);
            watermark.HorizontalAlignment = HorizontalAlignment.Left;
            watermark.VerticalAlignment = VerticalAlignment.Center;
            watermark.Margin = new Thickness(PanelShell.InputPaddingX, 0, 0, 0);
            watermark.IsHitTestVisible = false;

            Action reread = () =>
            {
                var empty = box.Text.Length == 0;
                watermark.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
                box.ToolTip = empty ? PanelScreens.WebViewEmptyTooltip : box.Text;
            };
            Action commit = () =>
            {
                var normalised = Contract.NormaliseUrl(box.Text);
                if (!string.Equals(normalised, screen.WebViewUrl ?? string.Empty, StringComparison.Ordinal) || !string.Equals(box.Text, normalised, StringComparison.Ordinal))
                {
                    screen.WebViewUrl = normalised;
                    ScreensSave(screen);
                }
                if (box.Text != screen.WebViewUrl) box.Text = screen.WebViewUrl ?? string.Empty;
            };
            box.TextChanged += (sender, args) => reread();
            box.LostFocus += (sender, args) => commit();
            box.KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter) commit();
            };
            reread();

            var host = new Grid { Width = width, HorizontalAlignment = HorizontalAlignment.Right };
            host.Children.Add(box);
            host.Children.Add(watermark);
            return host;
        }
    }
}
