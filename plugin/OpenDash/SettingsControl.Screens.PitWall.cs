// SettingsControl.Screens.PitWall.cs: what a pit wall shows under its card on the Screens page.
//
// The pit wall gets a picture of its three pages, because its letters are positions. Re-hosted from the old
// Rig tab by the #503 foundation; the Screens page agent owns it. The glance's binding moved to Shortcuts.
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
            // The glance's binding is on Shortcuts, with every other one; the page it shows is set here.
            var chip = BindingChipFor(Contract.HoldQuickGlanceActionFor(screen.Namespace));
            return Ui.VStack(0,
                PageSection("Layout", Ui.FitWidth(BuildPitWallPicture())),
                PageSection(PanelScreens.ZonesTitle, Ui.VStack(0, BuildPitWallRows(screen))),
                Ui.Row(PanelShortcuts.QuickGlanceTitle, null, Ui.HStack(8, BuildPitWallGlanceSelect(screen), chip)));
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
                Save(screen);
            };
            return Ui.Anchor(Ui.Row(PanelScreens.PitWallPageTitle, "Does not change while you race.", select), PanelScreens.AnchorPitWallPage);
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
                Save(screen);
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
                        page => { screen.SetZonePage(captured.Key, page); Save(screen); });
                    slots.Add(Ui.Row(
                        captured.Wide ? "Wide zone" : "Zone " + captured.Slot,
                        PanelPitWallPlan.ZoneDescription(captured),
                        select));
                }
                var group = PageSection(pageName + " page", slots.ToArray());
                group.Margin = new Thickness(0, PanelPitWallPlan.GroupGap, 0, 0);
                rows.Add(group);
            }
            rows.Add(Ui.Anchor(Ui.Row(PanelScreens.WebViewTitle, "http or https only. Leave empty for none.", BuildWebViewBox(screen)), PanelScreens.AnchorWebView));
            // One answer for the screen and not one per zone, as a face has: the four zones are widgets
            // pointed at one dashboard file per rectangle, so two zones of one column are the same file.
            var classOnly = BuildToggle(screen.PitWallClassOnly, on => { screen.PitWallClassOnly = on; Save(screen); });
            classOnly.ToolTip = "Show only your own class";
            rows.Add(Ui.Row("My class only", null, classOnly));
            rows.Add(BuildPitWallFlagRow(screen));
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
                box.ToolTip = empty ? "http or https only." : box.Text;
            };
            Action commit = () =>
            {
                screen.WebViewUrl = Contract.NormaliseUrl(box.Text);
                Save(screen);
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
    }
}
