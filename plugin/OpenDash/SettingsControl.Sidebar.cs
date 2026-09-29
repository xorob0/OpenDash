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
        private StackPanel liveEyebrowHost;
        private TextBlock liveLine1;
        private TextBlock liveLine2;
        private LiveCard liveShown;

        /// <summary>The search field, kept across sidebar rebuilds so a query being typed survives a redraw
        /// the typing did not cause.</summary>
        private TextBox searchBox;
        private Popup searchFlyout;

        /// <summary>Draws the sidebar again from the rig, the route and what needs fixing.</summary>
        private void RefreshSidebar()
        {
            var narrow = Narrow;
            var padX = narrow ? PanelShell.RailPaddingX : PanelShell.SidebarPaddingX;
            var dock = new DockPanel { LastChildFill = true };

            var top = new StackPanel { Orientation = Orientation.Vertical };
            top.Children.Add(BuildMarkRow(narrow));
            top.Children.Add(BuildSearch(narrow));
            top.Children.Add(BuildLiveCard(narrow));
            top.Children.Add(BuildNavList(narrow));
            DockPanel.SetDock(top, Dock.Top);

            var foot = BuildFoot(narrow);
            DockPanel.SetDock(foot, Dock.Bottom);
            dock.Children.Add(foot);
            dock.Children.Add(top);
            // The spacer the artboard draws as flex-grow: the dock's last child takes what is left.
            dock.Children.Add(new Border());

            sidebarHost.Background = Ui.Brush(Theme.SurfaceInset);
            sidebarHost.BorderBrush = Ui.Brush(Theme.Rule);
            sidebarHost.BorderThickness = new Thickness(0, 0, PanelMetrics.BorderWeight, 0);
            sidebarHost.Padding = new Thickness(padX, PanelShell.SidebarPaddingTop, padX, PanelShell.SidebarPaddingBottom);
            sidebarHost.Child = dock;
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
                var wordmark = Ui.HStack(0,
                    Ui.Text("open", PanelShell.WordmarkSize, FontWeights.Light, Theme.TextPrimary),
                    Ui.Text("Dash", PanelShell.WordmarkSize, FontWeights.Bold, Theme.TextPrimary));
                var version = Ui.Text(OpenDash.Version, PanelShell.VersionSize, FontWeights.Medium, Theme.TextSecondary, PanelFonts.Data);
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
            Func<string, System.Collections.Generic.IList<PanelSearch.Hit>> find = query => PanelSearch.Find(PanelSearch.All(), query);
            Action<PanelSearch.Hit> pick = hit =>
            {
                if (searchFlyout != null) searchFlyout.IsOpen = false;
                if (searchBox != null) searchBox.Text = string.Empty;
                Go(hit.Route);
            };

            if (!narrow)
            {
                searchBox = new TextBox { ToolTip = "Search every setting" };
                var field = Ui.SearchField(searchBox, PanelSearch.Placeholder);
                var tag = Ui.NewTag();
                tag.Margin = new Thickness(6, 0, 0, 0);
                ((DockPanel)field.Child).Children.Insert(0, tag);
                DockPanel.SetDock(tag, Dock.Right);
                var list = Ui.Suggestions(searchBox, find, hit => hit.Text, pick, PanelSearch.NoMatch, popup: true, width: 360);
                var host = new Grid { Margin = new Thickness(0, 0, 0, PanelShell.SearchGap) };
                host.Children.Add(field);
                host.Children.Add(list);
                return host;
            }

            // The rail: a button, and the same list under a field of its own in a flyout beside it.
            var box = new TextBox();
            searchBox = box;
            var railField = Ui.SearchField(box, PanelSearch.Placeholder);
            railField.Width = 320;
            var results = Ui.Suggestions(box, find, hit => hit.Text, pick, PanelSearch.NoMatch, popup: false, width: 320);
            var content = Ui.VStack(0, railField, results);
            var toggle = new ToggleButton
            {
                Width = PanelShell.RailWidth - 2 * PanelShell.RailPaddingX,
                Height = PanelShell.SearchHeight,
                Background = Ui.Brush(Theme.SurfaceBase),
                BorderBrush = Ui.Brush(Theme.Border),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                Cursor = Cursors.Hand,
                ToolTip = "Search every setting",
                Content = Ui.NavIcon(PanelIcons.Search, Theme.TextSecondary, PanelShell.SearchIconSize),
                Margin = new Thickness(0, 0, 0, PanelShell.SearchGap),
            };
            toggle.Template = RailToggleTemplate();
            searchFlyout = Ui.Flyout(toggle, content);
            searchFlyout.Placement = PlacementMode.Right;
            searchFlyout.Opened += (sender, args) => Dispatcher.BeginInvoke(new Action(() => box.Focus()));
            var rail = new Grid();
            rail.Children.Add(toggle);
            rail.Children.Add(searchFlyout);
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
            if (narrow)
            {
                liveEyebrowHost = null;
                liveLine1 = null;
                liveLine2 = null;
                liveDot.Width = 9;
                liveDot.Height = 9;
                liveDot.HorizontalAlignment = HorizontalAlignment.Center;
                liveCard = new Border
                {
                    Height = PanelShell.RailLiveHeight,
                    Margin = new Thickness(0, 0, 0, PanelShell.LiveCardGap),
                    Background = Brushes.Transparent,
                    Child = liveDot,
                };
            }
            else
            {
                liveEyebrowHost = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var eyebrow = Ui.HStack(8, liveDot, liveEyebrowHost);
                eyebrow.Height = PanelShell.LiveEyebrowHeight;
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
            liveCard.ToolTip = card.Tooltip;
            if (liveEyebrowHost != null)
            {
                liveEyebrowHost.Children.Clear();
                liveEyebrowHost.Children.Add(Ui.Eyebrow(card.Eyebrow, card.EyebrowHex));
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
            for (var i = 0; i < PanelNav.Pages.Length; i++)
            {
                var page = PanelNav.Pages[i];
                var item = BuildNavItem(page, narrow, PanelNav.Count(page, screens, strips, matrices, BoundShortcuts()), PanelNav.Warns(page, issues), null);
                item.Margin = new Thickness(0, i == 0 ? 0 : PanelShell.NavItemGap, 0, 0);
                list.Children.Add(item);
                if (PanelNav.GapAfter(page))
                {
                    list.Children.Add(new Border
                    {
                        Height = 1,
                        Margin = new Thickness(narrow ? 4 : PanelShell.NavDividerMarginX, PanelShell.NavDividerMarginY, narrow ? 4 : PanelShell.NavDividerMarginX, PanelShell.NavDividerMarginY - PanelShell.NavItemGap),
                        Background = Ui.Brush(Theme.Rule),
                    });
                }
            }
            return list;
        }

        /// <summary>
        /// How many of OpenDash's actions somebody has bound, or null when SimHub's bindings cannot be read.
        /// </summary>
        /// <remarks>
        /// Null for now, which hides the count: the binding control is SimHub's ControlsEditor, and nothing
        /// in the panel reads SimHub's mappings without drawing one. A count that is sometimes wrong would be
        /// worse than none; the Shortcuts page is where reading them belongs.
        /// </remarks>
        private int? BoundShortcuts()
        {
            return null;
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
                    DockPanel.SetDock(tag, Dock.Right);
                    dock.Children.Add(tag);
                }
                if (count != null)
                {
                    var number = Ui.Text(count, PanelShell.NavCountSize, FontWeights.Medium, Theme.TextSecondary, PanelFonts.Data);
                    number.MinWidth = 10;
                    number.TextAlignment = TextAlignment.Right;
                    number.Margin = new Thickness(8, 0, 0, 0);
                    DockPanel.SetDock(number, Dock.Right);
                    dock.Children.Add(number);
                }
                if (warns)
                {
                    var dot = new Ellipse { Width = PanelShell.NavWarnSize, Height = PanelShell.NavWarnSize, Fill = Ui.Brush(Theme.Caution), ToolTip = PanelNav.WarnTooltip, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
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
                ToolTip = narrow ? NavTooltip(page, count, badge) : null,
                Template = NavItemTemplate(),
            };
            System.Windows.Automation.AutomationProperties.SetName(button, PanelNav.Label(page));
            if (!active)
            {
                button.MouseEnter += (sender, args) => button.Background = Ui.Brush(Theme.Hover);
                button.MouseLeave += (sender, args) => button.Background = Brushes.Transparent;
            }
            button.Click += (sender, args) => Go(page);
            return button;
        }

        private static string NavTooltip(PanelPage page, string count, string badge)
        {
            var text = PanelNav.Label(page);
            if (count != null) text += " · " + count;
            if (badge != null) text += " · " + badge;
            return text;
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
            var night = Ui.Switch(Settings.LightsNightMode, on =>
            {
                Settings.LightsNightMode = on;
                Save();
                RebuildPage();
            });
            night.ToolTip = "Night mode";
            FrameworkElement nightRow;
            if (narrow)
            {
                night.HorizontalAlignment = HorizontalAlignment.Center;
                night.Width = PanelShell.SwitchWidth;
                nightRow = night;
            }
            else
            {
                var dock = new DockPanel { LastChildFill = true };
                DockPanel.SetDock(night, Dock.Right);
                dock.Children.Add(night);
                dock.Children.Add(Ui.Eyebrow("Night mode"));
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
            var updates = BuildNavItem(PanelPage.Updates, narrow, null, false, badge);
            updates.Margin = new Thickness(0, PanelShell.UpdatesGap, 0, 0);

            var foot = new StackPanel { Orientation = Orientation.Vertical };
            foot.Children.Add(nightBlock);
            foot.Children.Add(updates);
            return foot;
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
