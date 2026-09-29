// SettingsControl.Sidebar.cs: the sidebar -- the mark and the version, search, the live card, one item per
// page with its count and its dot, night mode, and Updates pinned at the foot with its badge.
//
// Drawn from Sidebar.dc.html's geometry (PanelShell) and colours (Theme), with two departures the rulings
// settle: the mark is Ui.Mark(), which is media/logo.svg, and not the artboard's three rising bars, which
// docs/design/brand.md rejected; and the labels are sentence case, since brand.md is taking the uppercase
// transform away. Below 1000 px the sidebar is a rail: the icons alone with their labels as tooltips, the
// search a button that opens the same list, the live card its dot.
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>The live card's parts, rewritten on the tick without rebuilding the sidebar.</summary>
        private Border liveCard;
        private Ellipse liveDot;
        private Ellipse liveRing;
        private TextBlock liveEyebrow;
        private TextBlock liveLine1;
        private TextBlock liveLine2;
        private LiveCard liveShown;

        /// <summary>
        /// The search, built once for each shape of sidebar and moved into every rebuild of it, so a query
        /// being typed, its list and the caret survive a redraw the typing did not cause: an update answer,
        /// a page change, a night-mode press.
        /// </summary>
        private FrameworkElement searchFull;
        private FrameworkElement searchRail;
        private TextBox searchBox;
        private TextBox railSearchBox;
        private Popup searchFlyout;

        /// <summary>The sidebar's night switch, set in place when the wheel changes night mode rather than drawn
        /// again, and the guard that keeps that setting from being heard as a press.</summary>
        private ToggleButton nightSwitch;
        private bool syncingNight;

        /// <summary>The nav items of the sidebar that is showing, so focus can follow a press to the new one.</summary>
        private readonly System.Collections.Generic.Dictionary<PanelPage, Button> navItems = new System.Collections.Generic.Dictionary<PanelPage, Button>();

        /// <summary>
        /// Draws the sidebar again from the rig, the route and what needs fixing.
        /// </summary>
        /// <remarks>
        /// The items above the foot scroll, with no bar, when the window is too short for them: under about
        /// 645 px the foot would otherwise cover Settings, then Shortcuts.
        ///
        /// Keyboard focus inside the sidebar is carried into the new one: the nav item, the night switch or
        /// the search that had it has it again, rather than focus dropping to the window when the control
        /// holding it is drawn away by an update answer, a mapping change or a finished run.
        /// </remarks>
        private void RefreshSidebar()
        {
            PanelPage? focusedItem = null;
            var nightFocused = false;
            TextBox focusedSearch = null;
            if (sidebarHost.IsKeyboardFocusWithin)
            {
                foreach (var pair in navItems)
                {
                    if (pair.Value.IsKeyboardFocusWithin) focusedItem = pair.Key;
                }
                nightFocused = nightSwitch != null && nightSwitch.IsKeyboardFocusWithin;
                if (searchBox != null && searchBox.IsKeyboardFocusWithin) focusedSearch = searchBox;
                else if (railSearchBox != null && railSearchBox.IsKeyboardFocusWithin) focusedSearch = railSearchBox;
            }

            var narrow = Narrow;
            var padX = narrow ? PanelShell.RailPaddingX : PanelShell.SidebarPaddingX;
            var dock = new DockPanel { LastChildFill = true };
            navItems.Clear();

            var top = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Top };
            top.Children.Add(BuildMarkRow(narrow));
            top.Children.Add(Adopt(BuildSearch(narrow)));
            top.Children.Add(BuildLiveCard(narrow));
            top.Children.Add(BuildNavList(narrow));

            var foot = BuildFoot(narrow);
            DockPanel.SetDock(foot, Dock.Bottom);
            dock.Children.Add(foot);
            // The flex-grow the artboard puts between the items and the foot: the dock's last child takes
            // what is left, and scrolls when that is less than the items need.
            //
            // A focus ring is drawn in the nearest ScrollContentPresenter's adorner layer, which is clipped to
            // the viewport, and the kit's ring stands FocusRingOutset outside the control. The items fill the
            // viewport's width, so the ring lost its sides on every item above the foot. The viewport is
            // widened by the ring on each side and the items drawn back in by as much, so nothing moves and
            // the ring is whole.
            top.Margin = new Thickness(FocusRingOutset, 0, FocusRingOutset, 0);
            dock.Children.Add(new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false,
                Margin = new Thickness(-FocusRingOutset, 0, -FocusRingOutset, 0),
                Content = top,
            });

            sidebarHost.Background = Ui.Brush(Theme.SurfaceInset);
            sidebarHost.BorderBrush = Ui.Brush(Theme.Rule);
            sidebarHost.BorderThickness = new Thickness(0, 0, PanelMetrics.BorderWeight, 0);
            sidebarHost.Padding = new Thickness(padX, PanelShell.SidebarPaddingTop, padX, PanelShell.SidebarPaddingBottom);
            sidebarHost.Child = dock;

            if (focusedItem.HasValue) FocusNavItem(focusedItem.Value);
            else if (nightFocused) FocusLater(nightSwitch);
            else if (focusedSearch != null && focusedSearch.IsVisible) FocusLater(focusedSearch);
        }

        /// <summary>How far the kit's focus ring stands outside the control it rings: the offset and the ring.</summary>
        private const double FocusRingOutset = Theme.FocusRingOffset + Theme.FocusRing;

        /// <summary>Focuses a control once the sidebar it was drawn into has been laid out.</summary>
        private void FocusLater(IInputElement element)
        {
            if (element == null) return;
            Dispatcher.BeginInvoke(new Action(() => Keyboard.Focus(element)), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>Sets the sidebar's night switch to the setting without drawing the sidebar again, so a
        /// wheel press moves it and a driver focused on it keeps the focus.</summary>
        private void SyncNightSwitch()
        {
            if (nightSwitch == null) return;
            var on = Settings.LightsNightMode;
            if (nightSwitch.IsChecked == on) return;
            syncingNight = true;
            try
            {
                nightSwitch.IsChecked = on;
            }
            finally
            {
                syncingNight = false;
            }
        }

        /// <summary>Takes an element that is kept between rebuilds out of the sidebar it was last drawn in.</summary>
        private static FrameworkElement Adopt(FrameworkElement element)
        {
            var panel = element.Parent as Panel;
            if (panel != null) panel.Children.Remove(element);
            return element;
        }

        /// <summary>Puts keyboard focus on a page's item, after a press on the old one was drawn away.</summary>
        private void FocusNavItem(PanelPage page)
        {
            Button item;
            if (!navItems.TryGetValue(page, out item)) return;
            Dispatcher.BeginInvoke(new Action(() => Keyboard.Focus(item)), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private FrameworkElement BuildMarkRow(bool narrow)
        {
            var mark = Ui.Mark(PanelShell.MarkSize);
            FrameworkElement row;
            if (narrow)
            {
                mark.HorizontalAlignment = HorizontalAlignment.Center;
                row = mark;
                mark.ToolTip = "OpenDash " + OpenDash.Version;
            }
            else
            {
                // The wordmark is the one place "openDash" is spelled that way; every sentence says OpenDash.
                // Both runs in the display family, which is the one that carries Light and Bold (brand.md):
                // Barlow ships Regular, Medium and SemiBold only, and WPF drew "open" Regular and "Dash"
                // SemiBold from it.
                var open = Ui.Text("open", PanelShell.WordmarkSize, FontWeights.Light, Theme.TextPrimary, PanelFonts.Data);
                var dash = Ui.Text("Dash", PanelShell.WordmarkSize, FontWeights.Bold, Theme.TextPrimary, PanelFonts.Data);
                // One line of the row's own 21, as the artboard's line-height: 1 draws it. WPF's default is the
                // face's 1.2, about 25, which the 21 px row clipped at the descender of the p.
                foreach (var run in new[] { open, dash })
                {
                    run.LineHeight = PanelShell.WordmarkSize;
                    run.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
                }
                var wordmark = Ui.HStack(0, open, dash);
                // SemiBold, which the display family carries; the artboard's 500 is a face it does not.
                var version = Ui.Text(OpenDash.Version, PanelShell.VersionSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
                var dock = new DockPanel { LastChildFill = false };
                var left = Ui.HStack(10, mark, wordmark);
                DockPanel.SetDock(left, Dock.Left);
                DockPanel.SetDock(version, Dock.Right);
                dock.Children.Add(left);
                dock.Children.Add(version);
                dock.Margin = new Thickness(PanelShell.MarkRowPaddingX, 0, PanelShell.MarkRowPaddingX, 0);
                row = dock;
            }
            return new Border
            {
                Height = PanelShell.MarkRowHeight,
                Margin = new Thickness(0, 0, 0, PanelShell.MarkRowGap),
                Child = row,
            };
        }

        /// <summary>
        /// Search: a field over every page's rows on the full sidebar, a button opening the same list on the
        /// rail. A hit goes to its page and scrolls to its row. New in this release, so it says so.
        /// </summary>
        private FrameworkElement BuildSearch(bool narrow)
        {
            if (!narrow && searchFull != null) return searchFull;
            if (narrow && searchRail != null) return searchRail;

            Func<string, System.Collections.Generic.IList<PanelSearch.Hit>> find = query => PanelSearch.Find(PanelSearch.All(), query);
            Action<PanelSearch.Hit> pick = hit =>
            {
                if (searchFlyout != null) searchFlyout.IsOpen = false;
                if (searchBox != null) searchBox.Text = string.Empty;
                if (railSearchBox != null) railSearchBox.Text = string.Empty;
                Go(hit.Route);
            };

            if (!narrow)
            {
                // No tooltip: the placeholder already says Search.
                searchBox = new TextBox();
                var field = Ui.SearchField(searchBox, PanelSearch.Placeholder);
                var tag = Ui.NewTag();
                tag.Margin = new Thickness(6, 0, 0, 0);
                ((DockPanel)field.Child).Children.Insert(0, tag);
                DockPanel.SetDock(tag, Dock.Right);
                var list = Ui.Suggestions(searchBox, find, hit => hit.Text, pick, PanelSearch.NoMatch, popup: true, width: 360);
                var host = new Grid { Margin = new Thickness(0, 0, 0, PanelShell.SearchGap) };
                host.Children.Add(field);
                host.Children.Add(list);
                searchFull = host;
                return host;
            }

            // The rail: a button, and the same list under a field of its own in a flyout beside it.
            var box = new TextBox();
            railSearchBox = box;
            var railField = Ui.SearchField(box, PanelSearch.Placeholder);
            railField.Width = 320;
            var results = Ui.Suggestions(box, find, hit => hit.Text, pick, PanelSearch.NoMatch, popup: false, width: 320);
            var content = Ui.VStack(0, railField, results);
            var toggle = new ToggleButton
            {
                // The rail's inside, 39: its 56 less the rule and 8 each side. It was 40 and lost its right edge.
                Width = PanelShell.RailInnerWidth,
                Height = PanelShell.SearchHeight,
                Background = Ui.Brush(Theme.SurfaceBase),
                BorderBrush = Ui.Brush(Theme.Border),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                Cursor = Cursors.Hand,
                ToolTip = PanelSearch.RailTooltip,
                Content = Ui.NavIcon(PanelIcons.Search, Theme.TextSecondary, PanelShell.SearchIconSize),
                Margin = new Thickness(0, 0, 0, PanelShell.SearchGap),
                FocusVisualStyle = Ui.FocusRing(),
            };
            System.Windows.Automation.AutomationProperties.SetName(toggle, PanelSearch.Placeholder);
            toggle.Template = RailToggleTemplate();
            searchFlyout = Ui.Flyout(toggle, content);
            searchFlyout.Placement = PlacementMode.Right;
            searchFlyout.Opened += (sender, args) => Dispatcher.BeginInvoke(new Action(() => box.Focus()));
            var rail = new Grid();
            rail.Children.Add(toggle);
            rail.Children.Add(searchFlyout);
            searchRail = rail;
            return rail;
        }

        private static ControlTemplate RailToggleTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new System.Windows.TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new System.Windows.TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new System.Windows.TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(Theme.Radius));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            return new ControlTemplate(typeof(ToggleButton)) { VisualTree = border };
        }

        /// <summary>
        /// The live card: at one height whatever it says, so the items under it never move when a session
        /// starts (PanelShell.LiveCardHeight). On the rail it is its dot, with the card's words as a tooltip.
        /// </summary>
        private FrameworkElement BuildLiveCard(bool narrow)
        {
            liveShown = null;
            liveDot = new Ellipse { Width = PanelShell.LiveDotSize, Height = PanelShell.LiveDotSize, VerticalAlignment = VerticalAlignment.Center };
            // The artboard's 3 px ring round a live dot, drawn behind it and outside the dot's own box so the
            // eyebrow does not move when a session starts.
            liveRing = new Ellipse
            {
                Width = PanelShell.LiveDotSize + 2 * PanelShell.LiveRingWidth,
                Height = PanelShell.LiveDotSize + 2 * PanelShell.LiveRingWidth,
                Margin = new Thickness(-PanelShell.LiveRingWidth),
                IsHitTestVisible = false,
            };
            var dot = new Grid { Width = PanelShell.LiveDotSize, Height = PanelShell.LiveDotSize, VerticalAlignment = VerticalAlignment.Center, ClipToBounds = false };
            dot.Children.Add(liveRing);
            dot.Children.Add(liveDot);
            if (narrow)
            {
                liveEyebrow = null;
                liveLine1 = null;
                liveLine2 = null;
                dot.Width = dot.Height = liveDot.Width = liveDot.Height = 9;
                liveRing.Width = liveRing.Height = 9 + 2 * PanelShell.LiveRingWidth;
                dot.HorizontalAlignment = HorizontalAlignment.Center;
                liveCard = new Border
                {
                    Height = PanelShell.RailLiveHeight,
                    Margin = new Thickness(0, 0, 0, PanelShell.LiveCardGap),
                    Background = Brushes.Transparent,
                    Child = dot,
                };
            }
            else
            {
                // One block that trims rather than the tracked eyebrow, which is a glyph per block and cannot:
                // "Live · Assetto Corsa Competizione" is wider than the 151 px the card has for it.
                liveEyebrow = Ui.Text(string.Empty, PanelShell.EyebrowSize, FontWeights.SemiBold, Theme.TextSecondary);
                liveEyebrow.TextTrimming = TextTrimming.CharacterEllipsis;
                liveEyebrow.VerticalAlignment = VerticalAlignment.Center;
                var eyebrow = new DockPanel { LastChildFill = true, Height = PanelShell.LiveEyebrowHeight };
                dot.Margin = new Thickness(0, 0, 8, 0);
                DockPanel.SetDock(dot, Dock.Left);
                eyebrow.Children.Add(dot);
                eyebrow.Children.Add(liveEyebrow);
                liveLine1 = Ui.Text(string.Empty, Theme.SizeBody, FontWeights.Medium, Theme.TextPrimary);
                liveLine1.Height = PanelShell.LiveCarHeight;
                liveLine1.TextTrimming = TextTrimming.CharacterEllipsis;
                liveLine1.Margin = new Thickness(0, PanelShell.LiveLineGap, 0, 0);
                liveLine2 = Ui.Text(string.Empty, Theme.SizeLabel, FontWeights.Normal, Theme.TextSecondary);
                liveLine2.Height = PanelShell.LiveTrackHeight;
                liveLine2.TextTrimming = TextTrimming.CharacterEllipsis;
                liveLine2.Margin = new Thickness(0, PanelShell.LiveLineGap, 0, 0);
                var stack = new StackPanel { Orientation = Orientation.Vertical };
                stack.Children.Add(eyebrow);
                stack.Children.Add(liveLine1);
                stack.Children.Add(liveLine2);
                liveCard = new Border
                {
                    Height = PanelShell.LiveCardHeight,
                    Margin = new Thickness(0, 0, 0, PanelShell.LiveCardGap),
                    Padding = new Thickness(PanelShell.LiveCardPaddingX, PanelShell.LiveCardPaddingTop, PanelShell.LiveCardPaddingX, PanelShell.LiveCardPaddingBottom),
                    Background = Ui.Brush(Theme.SurfaceZone),
                    BorderBrush = Ui.Brush(Theme.Rule),
                    BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                    CornerRadius = new CornerRadius(Theme.Radius),
                    Child = stack,
                };
            }
            RefreshLive();
            return liveCard;
        }

        /// <summary>Rewrites the live card from what the plugin last copied out of SimHub's frame, and only
        /// when it says something new. Called on the tick.</summary>
        private void RefreshLive()
        {
            if (liveCard == null) return;
            LiveCard card;
            try
            {
                card = PanelLive.Card(plugin.Live);
            }
            catch (Exception ex)
            {
                Log.Warn("The live card could not be read: " + ex.Message);
                return;
            }
            if (liveShown != null && liveShown.Eyebrow == card.Eyebrow && liveShown.Line1 == card.Line1 && liveShown.Line2 == card.Line2) return;
            liveShown = card;
            liveDot.Fill = Ui.Brush(card.DotHex);
            liveRing.Fill = card.Live ? Ui.Tint(card.DotHex, PanelShell.LiveRingOpacity) : Brushes.Transparent;
            liveCard.ToolTip = card.Tooltip;
            if (liveEyebrow != null)
            {
                liveEyebrow.Text = card.Eyebrow ?? string.Empty;
                liveEyebrow.Foreground = Ui.Brush(card.EyebrowHex);
            }
            if (liveLine1 != null) liveLine1.Text = card.Line1 ?? string.Empty;
            if (liveLine2 != null) liveLine2.Text = card.Line2 ?? string.Empty;
        }

        private FrameworkElement BuildNavList(bool narrow)
        {
            var list = new StackPanel { Orientation = Orientation.Vertical };
            var screens = Settings.RigScreens().Count;
            var strips = Settings.LedBarList().Count;
            var matrices = Settings.MatrixPanels().Count();
            var bound = BoundCount();
            for (var i = 0; i < PanelNav.Pages.Length; i++)
            {
                var page = PanelNav.Pages[i];
                var item = BuildNavItem(page, narrow, PanelNav.Count(page, screens, strips, matrices, bound), PanelNav.Warns(page, issues), null);
                item.Margin = new Thickness(0, i == 0 ? 0 : PanelShell.NavItemGap, 0, 0);
                list.Children.Add(item);
                if (PanelNav.GapAfter(page))
                {
                    // The artboard's rule is an item of its own in a column with a 2 px gap: the gap, eight,
                    // the pixel, eight, and the next item's gap, which is its own top margin here.
                    list.Children.Add(new Border
                    {
                        Height = 1,
                        Margin = new Thickness(narrow ? 4 : PanelShell.NavDividerMarginX, PanelShell.NavItemGap + PanelShell.NavDividerMarginY, narrow ? 4 : PanelShell.NavDividerMarginX, PanelShell.NavDividerMarginY),
                        Background = Ui.Brush(Theme.Rule),
                    });
                }
            }
            return list;
        }

        /// <summary>
        /// One item: an 18 px icon, the label, the amber dot when the page has something to fix, the count;
        /// the page that is showing on the zone ground with the accent bar down its left edge.
        /// </summary>
        private FrameworkElement BuildNavItem(PanelPage page, bool narrow, string count, bool warns, string badge)
        {
            var active = route.Page == page;
            var ink = active ? Theme.TextPrimary : Theme.TextSecondary;
            var icon = Ui.NavIcon(PanelNav.Icon(page), ink);
            var dock = new DockPanel { LastChildFill = true, VerticalAlignment = VerticalAlignment.Center };

            if (!narrow)
            {
                icon.Margin = new Thickness(0, 0, PanelShell.NavIconGap, 0);
                DockPanel.SetDock(icon, Dock.Left);
                dock.Children.Add(icon);
                if (badge != null)
                {
                    var tag = new Border
                    {
                        Height = PanelShell.BadgeHeight,
                        Padding = new Thickness(PanelShell.BadgePaddingX, 0, PanelShell.BadgePaddingX, 0),
                        CornerRadius = new CornerRadius(Theme.Radius),
                        Background = Ui.Tint(Theme.Caution, 0.14),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = Ui.Text(badge, PanelShell.BadgeTextSize, FontWeights.SemiBold, Theme.Caution, PanelFonts.Data),
                    };
                    // The artboard's 12 px flex gap stands before every trailing element, the badge too, so
                    // the amber dot beside it does not touch it.
                    tag.Margin = new Thickness(PanelShell.NavTrailGap, 0, 0, 0);
                    DockPanel.SetDock(tag, Dock.Right);
                    dock.Children.Add(tag);
                }
                if (count != null)
                {
                    // SemiBold: the display family carries no Medium, which WPF would draw SemiBold anyway.
                    var number = Ui.Text(count, PanelShell.NavCountSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
                    number.MinWidth = 10;
                    number.TextAlignment = TextAlignment.Right;
                    number.Margin = new Thickness(PanelShell.NavTrailGap, 0, 0, 0);
                    DockPanel.SetDock(number, Dock.Right);
                    dock.Children.Add(number);
                }
                if (warns)
                {
                    var dot = new Ellipse { Width = PanelShell.NavWarnSize, Height = PanelShell.NavWarnSize, Fill = Ui.Brush(Theme.Caution), ToolTip = PanelNav.WarnTooltip, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(PanelShell.NavTrailGap, 0, 0, 0) };
                    DockPanel.SetDock(dot, Dock.Right);
                    dock.Children.Add(dot);
                }
                dock.Children.Add(Ui.Text(PanelNav.Label(page), PanelShell.NavTextSize, FontWeights.Medium, ink));
            }
            else
            {
                // The rail: the icon alone, the dot over its corner, and the label as the tooltip.
                var holder = new Grid { Width = PanelIcons.NavSize, Height = PanelIcons.NavSize, HorizontalAlignment = HorizontalAlignment.Center };
                holder.Children.Add(icon);
                if (warns || badge != null)
                {
                    holder.Children.Add(new Ellipse
                    {
                        Width = PanelShell.NavWarnSize,
                        Height = PanelShell.NavWarnSize,
                        Fill = Ui.Brush(Theme.Caution),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, -3, -5, 0),
                    });
                }
                dock.Children.Add(holder);
            }

            var bar = new Rectangle { Width = PanelShell.NavActiveBar, Fill = active ? Ui.Brush(Theme.Accent) : Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Left };
            var content = new Grid();
            content.Children.Add(new Border { Padding = new Thickness(narrow ? 0 : PanelShell.NavItemPaddingX, 0, narrow ? 0 : PanelShell.NavItemPaddingX, 0), Child = dock });
            content.Children.Add(bar);

            var button = new Button
            {
                Height = PanelShell.NavItemHeight,
                Background = active ? Ui.Brush(Theme.SurfaceZone) : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                Content = content,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                FocusVisualStyle = Ui.FocusRing(),
                ToolTip = narrow ? NavTooltip(page, count, badge, warns) : null,
                Template = NavItemTemplate(),
            };
            System.Windows.Automation.AutomationProperties.SetName(button, PanelNav.Label(page));
            if (!active)
            {
                button.MouseEnter += (sender, args) => button.Background = Ui.Brush(Theme.Hover);
                button.MouseLeave += (sender, args) => button.Background = Brushes.Transparent;
            }
            button.Click += (sender, args) => Go(page);
            navItems[page] = button;
            return button;
        }

        /// <summary>The rail item's tooltip: its label, its count and badge, and what its amber dot means,
        /// which the full sidebar's dot carries as a tooltip of its own.</summary>
        private static string NavTooltip(PanelPage page, string count, string badge, bool warns)
        {
            return PanelNav.RailTooltip(page, count, badge, warns);
        }

        private static ControlTemplate NavItemTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new System.Windows.TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(Theme.Radius));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            border.AppendChild(presenter);
            return new ControlTemplate(typeof(Button)) { VisualTree = border };
        }

        /// <summary>
        /// The foot: night mode over a rule, then Updates with its badge. Night mode is LightsNightMode, the
        /// same setting Home, Rig and Settings show and the wheel button toggles, so a change here redraws
        /// the page as well.
        /// </summary>
        private FrameworkElement BuildFoot(bool narrow)
        {
            Action<bool> pressed = on =>
            {
                if (syncingNight) return;
                Settings.LightsNightMode = on;
                Save();
                ShowLightingChange();
            };
            // On the rail night mode is an icon among icons, a crescent that lights in the accent when on, with
            // its label as the tooltip; a bare switch at the foot of a column of icons read as nothing.
            var night = narrow ? RailNightToggle(Settings.LightsNightMode, pressed) : Ui.Switch(Settings.LightsNightMode, pressed);
            System.Windows.Automation.AutomationProperties.SetName(night, PanelSettings.NightModeTitle);
            nightSwitch = night;
            FrameworkElement nightRow;
            if (narrow)
            {
                // The label is not drawn on the rail, so it is the tooltip there and only there.
                night.ToolTip = PanelSettings.NightModeTitle;
                night.HorizontalAlignment = HorizontalAlignment.Center;
                nightRow = night;
            }
            else
            {
                var dock = new DockPanel { LastChildFill = true };
                DockPanel.SetDock(night, Dock.Right);
                dock.Children.Add(night);
                dock.Children.Add(Ui.Eyebrow(PanelSettings.NightModeTitle));
                nightRow = dock;
            }
            var nightBlock = new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(narrow ? 0 : PanelShell.FootPaddingX, PanelShell.FootPaddingY, narrow ? 0 : PanelShell.FootPaddingX, PanelShell.FootPaddingY),
                Child = nightRow,
            };

            var restart = PendingRestart();
            var badge = PanelNav.UpdatesBadge(restart, updateStatus.State == UpdateState.UpdateAvailable, updateStatus.LatestVersion);
            var updates = BuildNavItem(PanelPage.Updates, narrow, null, PanelNav.UpdatesWarns(issues), badge);
            updates.Margin = new Thickness(0, PanelShell.UpdatesGap, 0, 0);

            var foot = new StackPanel { Orientation = Orientation.Vertical };
            foot.Children.Add(nightBlock);
            foot.Children.Add(updates);
            return foot;
        }

        /// <summary>
        /// Night mode on the rail: the crescent in secondary ink on no ground when off, and in the accent on the
        /// zone ground when on, the rail's inside wide and a nav item high, with the kit's focus ring.
        /// </summary>
        private static ToggleButton RailNightToggle(bool on, Action<bool> changed)
        {
            var icon = Ui.NavIcon(PanelIcons.Night, on ? Theme.Accent : Theme.TextSecondary);
            var toggle = new ToggleButton
            {
                Width = PanelShell.RailInnerWidth,
                Height = PanelShell.NavItemHeight,
                IsChecked = on,
                Background = on ? Ui.Brush(Theme.SurfaceZone) : Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Content = icon,
                Template = RailToggleTemplate(),
                FocusVisualStyle = Ui.FocusRing(),
            };
            Action<bool> ink = lit =>
            {
                Ui.SetIconInk(icon, lit ? Theme.Accent : Theme.TextSecondary);
                toggle.Background = lit ? Ui.Brush(Theme.SurfaceZone) : Brushes.Transparent;
            };
            toggle.Checked += (sender, args) => { ink(true); changed(true); };
            toggle.Unchecked += (sender, args) => { ink(false); changed(false); };
            return toggle;
        }

        /// <summary>Whether a plugin update waits for SimHub to close. False when that cannot be read.</summary>
        private bool PendingRestart()
        {
            try
            {
                return PluginUpdate.Pending(plugin.Installer.SimHubRoot);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not tell whether an update waits for a restart: " + ex.Message);
                return false;
            }
        }
    }
}
