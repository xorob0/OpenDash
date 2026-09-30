// SettingsControl.Shortcuts.cs: the Shortcuts page -- every wheel button and key in one list, a card per
// screen and one for the rig's lights, drawn to Shortcuts.dc.html.
//
// Each binding is SimHub's own ControlsEditor (BuildBinder), so a button is bound here rather than by sending
// the driver to Controls and events to find an action's name; SimHub draws "Click to configure" or each
// binding with its press type, and Change, Clear and Add on hover, in a slot of fixed width that every row
// gives it, with its own name column dropped. Around it the page draws what the artboard adds: the press each action
// answers to, a card's "3 of 6", the All | Bound | Not bound filter, and the line naming a button bound to two
// things. Those three read each editor's own Model.Triggers, again whenever a binding is made, changed or
// cleared, and are hidden when SimHub's mappings cannot be read. PanelShortcuts decides the rows, the words and
// the geometry; this file only draws. The Shortcuts page agent owns it.
//
// Exactly one hold binder per kind of screen, each captioned with its glance sentence: PanelCopyTests counts
// the call sites, and the caption is what says the binding is corrected to a hold (#435).
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using SimHub.Plugins;
using SimHub.Plugins.UI;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>The filter chosen on Shortcuts, kept for the session so that a rebuild keeps it.</summary>
        private string shortcutsFilter = PanelShortcuts.FilterAll;

        /// <summary>
        /// Whether the next build of Shortcuts is the first since the page was opened, when an anchor puts the
        /// filter back to All. A rebuild in place (a resize, the rail, coming back to the panel, Redraw) builds
        /// the same route with its anchor still on it, and must keep what the driver chose since; OnLeave runs
        /// on every Go and never on a rebuild, so it sets this again. A route cannot tell the two apart by
        /// reference, since a search hit re-uses its entry's route.
        /// </summary>
        private bool shortcutsLanding = true;

        /// <summary>One row the page drew: its action, SimHub's editor (or the fallback text), the bordered row
        /// and the element that shows or hides it (a greyed row's Soon wrapper).</summary>
        private sealed class ShortcutsRowState
        {
            public string Action;
            public FrameworkElement Editor;
            public Border Row;
            public FrameworkElement Shown;
            public bool Bindable;
            public string Place;
            public string Does;
        }

        /// <summary>One card: its rows, the host of its count, and whether a line sits between its header and
        /// its first row (the companion's paging), which decides whether that row draws its rule.</summary>
        private sealed class ShortcutsGroupState
        {
            public FrameworkElement Card;
            public StackPanel Body;
            public Border CountHost;
            public bool HasLead;
            public readonly List<ShortcutsRowState> Rows = new List<ShortcutsRowState>();
        }

        /// <summary>How this build lays a row out: its binder beside the name and the press or under them, and
        /// the width of the slot SimHub's editor is given, the same in every row so their columns line up.</summary>
        private sealed class ShortcutsLayout
        {
            public ShortcutsLayout(double contentWidth)
            {
                Stacks = PanelShortcuts.RowStacks(contentWidth);
                Binder = PanelShortcuts.BinderSlot(contentWidth, Stacks);
            }

            public readonly bool Stacks;
            public readonly double Binder;
        }

        private FrameworkElement BuildShortcutsPage(PanelRoute to)
        {
            if (shortcutsLanding)
            {
                shortcutsFilter = PanelShortcuts.FilterFor(shortcutsFilter, to == null ? null : to.Anchor, true);
                shortcutsLanding = false;
            }
            OnLeave("Shortcuts.filterLanding", () => shortcutsLanding = true);
            var layout = new ShortcutsLayout(ContentWidth);

            var screens = new List<ShortcutsGroupState>();
            foreach (var screen in Settings.RigScreens())
            {
                if (screen == null) continue;
                if (screen.IsFace) screens.Add(BuildShortcutsFace(screen, layout));
                else if (screen.IsPitWall && PanelShortcuts.PitWallGlances(screen.Width, screen.Height)) screens.Add(BuildShortcutsPitWall(screen, layout));
                else if (screen.IsCompanion) screens.Add(BuildShortcutsCompanion(screen, layout));
                // A round screen cycles nothing, and a portrait pit wall has no zone a glance can swap, so
                // neither has a card.
            }
            var lights = BuildShortcutsLights(layout);
            var alerts = BuildShortcutsAlerts(layout);
            var groups = PanelShortcuts.CardOrder(screens, lights, alerts);

            if (screens.Count > 0) Ui.Anchor(screens[0].Card, PanelShortcuts.AnchorScreens);
            Ui.Anchor(lights.Card, PanelShortcuts.AnchorRig);
            Ui.Anchor(alerts.Card, PanelShortcuts.AnchorAlerts);

            var banner = new StackPanel { Orientation = Orientation.Vertical };
            var empty = Ui.Caption(string.Empty, BodyWidth);
            Action evaluate = null;
            var filter = BuildSegmented(PanelShortcuts.FilterValues, PanelShortcuts.FilterLabels, shortcutsFilter, value =>
            {
                shortcutsFilter = value;
                if (evaluate != null) evaluate();
            }, padding: PanelKit.SegmentedPaddingShortcuts);
            var header = BuildShortcutsHeader(filter);

            evaluate = () => ShortcutsEvaluate(groups, filter, banner, empty);

            // An editor replaces its model when it loads and whenever a binding is made, so a burst of changes
            // is read once, after it.
            var pending = false;
            var dropped = false;
            OnDrop(() => dropped = true);
            Action changed = () =>
            {
                if (pending || dropped) return;
                pending = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    pending = false;
                    if (!dropped) evaluate();
                }));
            };
            foreach (var row in groups.SelectMany(group => group.Rows))
            {
                var editor = row.Editor as ControlsEditor;
                if (editor != null) ShortcutsWatch(editor, changed);
            }
            evaluate();

            var sections = new List<UIElement> { header, banner };
            sections.AddRange(groups.Select(group => (UIElement)group.Card));
            sections.Add(empty);
            return ShortcutsTitleTagged(PageLayout(PanelShortcuts.Title, null, sections.ToArray()));
        }

        /// <summary>
        /// The page's title with the New tag beside it, as the Rig page draws its own: Map.dc.html tags "Every
        /// button in one list" New, and a thing new only on the Map carries the tag on its control (ruling 7).
        /// PageLayout draws the title alone, so its first child is put in a row with the tag.
        /// </summary>
        private static FrameworkElement ShortcutsTitleTagged(FrameworkElement page)
        {
            var stack = page as StackPanel;
            if (stack == null || stack.Children.Count == 0) return page;
            var title = stack.Children[0] as TextBlock;
            if (title == null) return page;
            stack.Children.RemoveAt(0);
            stack.Children.Insert(0, Ui.HStack(12, title, Ui.NewTag()));
            return page;
        }

        /// <summary>
        /// The line under the title and the filter at its right, sharing a foot as the header's flex-end draws
        /// them; the filter goes under the line when the page has no room for two columns.
        /// </summary>
        private FrameworkElement BuildShortcutsHeader(FrameworkElement filter)
        {
            var caption = Ui.Caption(PanelShortcuts.IntroCaption, BodyWidth);
            FrameworkElement header;
            if (TwoColumns)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                caption.VerticalAlignment = VerticalAlignment.Bottom;
                filter.VerticalAlignment = VerticalAlignment.Bottom;
                filter.HorizontalAlignment = HorizontalAlignment.Right;
                filter.Margin = new Thickness(PanelShell.RowGap, -PanelShortcuts.FilterRaise, 0, 0);
                Grid.SetColumn(filter, 1);
                grid.Children.Add(caption);
                grid.Children.Add(filter);
                header = grid;
            }
            else
            {
                filter.HorizontalAlignment = HorizontalAlignment.Left;
                filter.Margin = new Thickness(0, PanelShortcuts.FilterGapStacked, 0, 0);
                header = Ui.VStack(0, caption, filter);
            }
            // The caption sits 8 under the title, where PageLayout sets every section the page's gap under the
            // one before it.
            header.Margin = new Thickness(0, PanelShortcuts.IntroGap - PanelShell.SectionGapFor(PanelPage.Shortcuts), 0, 0);
            return header;
        }

        /// <summary>
        /// A face's card: each zone's next page, Zone A to Band D, each zone's previous page, and the quick
        /// glance held on a button.
        /// </summary>
        /// <remarks>
        /// Per screen, which is what lets a second face stay still while the one in front of the driver
        /// cycles (ADR 0017).
        /// </remarks>
        private ShortcutsGroupState BuildShortcutsFace(ScreenInstance screen, ShortcutsLayout layout)
        {
            var group = ShortcutsCard(screen.Name, PanelShortcuts.GroupDetail(screen.Name, screen.Kind, screen.Width, screen.Height), null);
            foreach (var binding in PanelShortcuts.FaceBindings(screen.Namespace, screen.Name))
            {
                ShortcutsBinding(group, screen.Name, binding, BuildBinder(binding.Action, binding.BinderName), null, layout);
            }
            var glance = PanelShortcuts.GlanceBinding(screen.Kind, screen.Namespace, screen.Name);
            ShortcutsBinding(group, screen.Name, glance, BuildBinder(glance.Action, glance.BinderName, hold: true), Ui.Caption(PanelCopy.FaceGlance), layout);
            return group;
        }

        /// <summary>A landscape pit wall's card: the glance and nothing else. A key beside the monitor is the
        /// likelier gesture, since nobody drives a pit wall.</summary>
        private ShortcutsGroupState BuildShortcutsPitWall(ScreenInstance screen, ShortcutsLayout layout)
        {
            var group = ShortcutsCard(screen.Name, PanelShortcuts.GroupDetail(screen.Name, screen.Kind, screen.Width, screen.Height), null);
            var glance = PanelShortcuts.GlanceBinding(screen.Kind, screen.Namespace, screen.Name);
            ShortcutsBinding(group, screen.Name, glance, BuildBinder(glance.Action, glance.BinderName, hold: true), Ui.Caption(PanelCopy.PitWallGlance), layout);
            return group;
        }

        /// <summary>A companion's card: where SimHub binds its paging, which is not OpenDash's to bind, and
        /// the glance, which is.</summary>
        private ShortcutsGroupState BuildShortcutsCompanion(ScreenInstance screen, ShortcutsLayout layout)
        {
            var crumbs = Ui.Crumbs(PanelShortcuts.PagingCrumbs());
            crumbs.Margin = new Thickness(0, PanelShortcuts.LeadGap, 0, 0);
            var lead = new Border
            {
                Padding = new Thickness(PanelShortcuts.LeadPaddingX, PanelShortcuts.LeadPaddingY, PanelShortcuts.LeadPaddingX, PanelShortcuts.LeadPaddingY),
                Child = Ui.VStack(0, Ui.Caption(PanelCopy.CompanionPaging, BodyWidth), crumbs),
            };
            var group = ShortcutsCard(screen.Name, PanelShortcuts.GroupDetail(screen.Name, screen.Kind, screen.Width, screen.Height), lead);
            var glance = PanelShortcuts.GlanceBinding(screen.Kind, screen.Namespace, screen.Name);
            ShortcutsBinding(group, screen.Name, glance, BuildBinder(glance.Action, glance.BinderName, hold: true), Ui.Caption(PanelCopy.CompanionGlance), layout);
            return group;
        }

        /// <summary>The rig's own actions (Contract.RigActionNames), and the Rig test, which is coming.</summary>
        private ShortcutsGroupState BuildShortcutsLights(ShortcutsLayout layout)
        {
            var group = ShortcutsCard(PanelShortcuts.RigGroupTitle, PanelShortcuts.RigGroupDetail, null);
            foreach (var binding in PanelShortcuts.LightsBindings())
            {
                ShortcutsBinding(group, null, binding, BuildBinder(binding.Action, binding.BinderName), null, layout);
            }
            ShortcutsSoon(group, PanelSoon.RigTest, layout);
            return group;
        }

        private ShortcutsGroupState BuildShortcutsAlerts(ShortcutsLayout layout)
        {
            var group = ShortcutsCard(PanelShortcuts.AlertsGroupTitle, null, null);
            ShortcutsSoon(group, PanelSoon.AlertDismissal, layout);
            return group;
        }

        /// <summary>The artboard's .card with its .gh header: the name, the line beside it, and the count on the
        /// right, over a rule; then the lead line, if any, and the rows.</summary>
        private static ShortcutsGroupState ShortcutsCard(string title, string detail, FrameworkElement lead)
        {
            // A wrap panel, so the name is measured at the header's width (a horizontal stack panel measured it
            // at infinity, and the ellipsis never came): a long name ends in one, and the kind and size go
            // under it rather than past the count.
            var name = Ui.Text(title ?? string.Empty, PanelShortcuts.GroupTitleSize, FontWeights.SemiBold, Theme.TextPrimary);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.VerticalAlignment = VerticalAlignment.Center;
            var titleLine = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleLine.Children.Add(name);
            if (!string.IsNullOrEmpty(detail))
            {
                name.Margin = new Thickness(0, 0, PanelShortcuts.GroupDetailGap, 0);
                var line = Ui.Text(detail, Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);
                line.VerticalAlignment = VerticalAlignment.Center;
                titleLine.Children.Add(line);
            }
            var count = new Border { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(PanelShortcuts.RowGap, 0, 0, 0) };
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(count, 1);
            head.Children.Add(titleLine);
            head.Children.Add(count);

            var body = new StackPanel { Orientation = Orientation.Vertical };
            body.Children.Add(new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, 0, 0, PanelMetrics.BorderWeight),
                Padding = new Thickness(PanelShortcuts.HeaderPaddingX, PanelShortcuts.HeaderPaddingY, PanelShortcuts.HeaderPaddingX, PanelShortcuts.HeaderPaddingY),
                Child = head,
            });
            if (lead != null) body.Children.Add(lead);
            return new ShortcutsGroupState { Card = Ui.CardBox(body, 0), Body = body, CountHost = count, HasLead = lead != null };
        }

        /// <summary>A live row: the binding's name and its New tag, the press, SimHub's editor, and the glance's
        /// caption under the name. Anchored at the binding, where every other page's chip lands.</summary>
        private static void ShortcutsBinding(ShortcutsGroupState group, string place, PanelShortcuts.Binding binding, FrameworkElement editor, FrameworkElement caption, ShortcutsLayout layout)
        {
            // SimHub's editor fills the row's binder slot, and its own name column goes: the row's name beside it
            // already says what it binds, and the column's 2* share, empty or not, squeezed the bindings.
            var control = editor as ControlsEditor;
            if (control != null)
            {
                control.FriendlyName = PanelShortcuts.EditorName;
                control.Loaded += (sender, args) => ShortcutsDropNameColumn(control);
            }
            var fallback = editor as TextBlock;
            if (fallback != null) fallback.TextWrapping = TextWrapping.Wrap;
            if (editor != null) editor.HorizontalAlignment = HorizontalAlignment.Stretch;
            var tags = binding.IsNew ? new FrameworkElement[] { Ui.NewTag() } : new FrameworkElement[0];
            var row = ShortcutsRow(binding.Label, binding.Press, editor, layout, caption, tags);
            Ui.Anchor(row, PanelBindings.Anchor(binding.Action));
            group.Rows.Add(new ShortcutsRowState { Action = binding.Action, Editor = editor, Row = row, Shown = row, Bindable = true, Place = place, Does = binding.Does });
            group.Body.Children.Add(row);
        }

        /// <summary>A greyed row: the registry's entry, tapped, and a Not bound key that nothing answers.</summary>
        private static void ShortcutsSoon(ShortcutsGroupState group, SoonItem item, ShortcutsLayout layout)
        {
            // The key sits at the slot's left, where the artboard's .key starts in every row, and where SimHub's
            // bindings start once its name column is dropped.
            var chip = Ui.BindingChip(Ui.NotBound, false, key: true);
            chip.HorizontalAlignment = HorizontalAlignment.Left;
            var row = ShortcutsRow(item.Title, PanelShortcuts.Tap, chip, layout, null);
            var shown = Ui.Soon(row, item);
            group.Rows.Add(new ShortcutsRowState { Row = row, Shown = shown, Bindable = false });
            group.Body.Children.Add(shown);
        }

        /// <summary>
        /// The artboard's .r: the name with its tags, the press in a 90 px column and the binder in a slot of
        /// fixed width after it, 16 apart, padded 10 by 16 under a rule. The columns are fixed rather than
        /// sized by each row's control, so press and binder line up down a card, greyed rows included. Where
        /// the content cannot give the name room beside them, the binder goes under the name and the press.
        /// </summary>
        /// <remarks>The border's Tag carries the row's parts, so a Soon appends its tag after the name.</remarks>
        private static Border ShortcutsRow(string label, string press, FrameworkElement control, ShortcutsLayout layout, FrameworkElement caption, params FrameworkElement[] tags)
        {
            var nameLine = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var name = Ui.Text(label, PanelShortcuts.RowNameSize, FontWeights.Normal, Theme.TextPrimary);
            name.TextWrapping = TextWrapping.Wrap;
            name.VerticalAlignment = VerticalAlignment.Center;
            nameLine.Children.Add(name);
            foreach (var tag in tags ?? new FrameworkElement[0])
            {
                if (tag == null) continue;
                tag.Margin = new Thickness(PanelShortcuts.TagGap, 0, 0, 0);
                tag.VerticalAlignment = VerticalAlignment.Center;
                nameLine.Children.Add(tag);
            }
            var left = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(nameLine);
            if (caption != null)
            {
                caption.Margin = new Thickness(0, PanelShortcuts.CaptionGap, 0, 0);
                left.Children.Add(caption);
            }
            var pressText = Ui.Text(press, Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);
            pressText.VerticalAlignment = VerticalAlignment.Center;
            pressText.Margin = new Thickness(PanelShortcuts.RowGap, 0, 0, 0);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelShortcuts.RowGap + PanelShortcuts.PressWidth) });
            Grid.SetColumn(pressText, 1);
            grid.Children.Add(left);
            grid.Children.Add(pressText);
            if (control != null)
            {
                // The gap is the slot's and the control keeps no margin: SimHub's editor template binds its own
                // border's Margin to the control's, so a margin set on the editor is drawn twice.
                control.Margin = new Thickness(0);
                control.VerticalAlignment = VerticalAlignment.Center;
                // BuildBinder's floor gives way to a slot narrower than it, so SimHub's template is laid out in
                // the room it has, its Change and Clear included, rather than clipped at the slot's edge.
                control.MinWidth = Math.Min(control.MinWidth, layout.Binder);
                var slot = new Border { Child = control, Width = layout.Binder, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                if (layout.Stacks)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    slot.Margin = new Thickness(0, PanelShortcuts.StackGap, 0, 0);
                    Grid.SetRow(slot, 1);
                    Grid.SetColumnSpan(slot, 2);
                }
                else
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelShortcuts.RowGap + layout.Binder) });
                    slot.Margin = new Thickness(PanelShortcuts.RowGap, 0, 0, 0);
                    Grid.SetColumn(slot, 2);
                }
                grid.Children.Add(slot);
            }
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(PanelShortcuts.RowPaddingX, PanelShortcuts.RowPaddingY, PanelShortcuts.RowPaddingX, PanelShortcuts.RowPaddingY),
                Child = grid,
                Tag = new RowParts(nameLine, control),
            };
        }

        /// <summary>
        /// Drops the name column from SimHub's editor, so its bindings have the whole slot and start at its
        /// left. SimHub 9.12.6's template (themes/generic.baml) is a Border "brd" around a Grid of two star
        /// columns, 2* and 3*: the name alone in the first, "Click to configure" and the bindings in the
        /// second. A star column keeps its share when its content is empty, so an empty name still took two
        /// fifths of the slot and left the bindings the rest, clipping a bound key. Only a template of that
        /// shape is touched; any other keeps its column, empty, as before.
        /// </summary>
        private static void ShortcutsDropNameColumn(ControlsEditor editor)
        {
            try
            {
                editor.ApplyTemplate();
                var border = editor.Template == null ? null : editor.Template.FindName("brd", editor) as Border;
                var grid = border == null ? null : border.Child as Grid;
                if (grid == null || grid.ColumnDefinitions.Count != 2) return;
                if (!grid.ColumnDefinitions[0].Width.IsStar || !grid.ColumnDefinitions[1].Width.IsStar) return;
                var named = grid.Children.OfType<UIElement>().Where(child => Grid.GetColumn(child) == 0).ToList();
                if (named.Any(child => !(child is TextBlock) && !(child is Label))) return;
                if (named.Any(child => Grid.GetColumnSpan(child) != 1)) return;
                // The name goes too, so its line no longer sets the editor's height.
                foreach (var child in named) child.Visibility = Visibility.Collapsed;
                grid.ColumnDefinitions[0].Width = new GridLength(0);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not drop the name column of SimHub's binding editor: " + ex.Message);
            }
        }

        /// <summary>
        /// Reads every editor's bindings and redraws what depends on them: the filter's rows and cards, each
        /// card's count, the clash lines and the empty line. PanelShortcuts decides each: a row's state, whether
        /// the page is readable, what the filter shows, each count and each clash. When any live row cannot be
        /// read, the filter, the counts and the clash lines are hidden and every row shows.
        /// </summary>
        private void ShortcutsEvaluate(IList<ShortcutsGroupState> groups, FrameworkElement filter, StackPanel banner, TextBlock empty)
        {
            var uses = new Dictionary<ShortcutsRowState, IList<PanelShortcuts.BindingUse>>();
            var states = new Dictionary<ShortcutsRowState, PanelShortcuts.RowState>();
            foreach (var row in groups.SelectMany(group => group.Rows))
            {
                var read = row.Bindable ? ShortcutsUses(row) : null;
                uses[row] = read;
                states[row] = PanelShortcuts.StateOf(row.Bindable, read == null ? (int?)null : read.Count);
            }
            var readable = PanelShortcuts.Readable(states.Values);
            var chosen = PanelShortcuts.FilterFor(shortcutsFilter, null, readable);
            filter.Visibility = readable ? Visibility.Visible : Visibility.Collapsed;

            // The row holding keyboard focus, which a binding made or cleared there can take out of the filter.
            var focused = groups.SelectMany(group => group.Rows).FirstOrDefault(row => row.Shown.IsKeyboardFocusWithin);

            var anyShown = false;
            foreach (var group in groups)
            {
                var firstShown = true;
                foreach (var row in group.Rows)
                {
                    var shows = PanelShortcuts.Shows(chosen, states[row]);
                    row.Shown.Visibility = shows ? Visibility.Visible : Visibility.Collapsed;
                    if (!shows) continue;
                    row.Row.BorderThickness = new Thickness(0, PanelShortcuts.RuleAbove(firstShown, group.HasLead) ? PanelMetrics.BorderWeight : 0, 0, 0);
                    firstShown = false;
                }
                var groupShown = PanelShortcuts.CardShows(group.Rows.Select(row => states[row]), chosen);
                group.Card.Visibility = groupShown ? Visibility.Visible : Visibility.Collapsed;
                anyShown |= groupShown;
                var count = PanelShortcuts.CardCount(group.Rows.Select(row => states[row]), readable);
                group.CountHost.Child = count == null ? null : Ui.Numeral(count, PanelShortcuts.CountSize, Theme.TextSecondary);
                group.CountHost.Visibility = count == null ? Visibility.Collapsed : Visibility.Visible;
            }

            if (focused != null && focused.Shown.Visibility != Visibility.Visible) ShortcutsKeepFocus(groups, focused, filter);

            banner.Children.Clear();
            if (readable)
            {
                var all = groups.SelectMany(group => group.Rows).Where(row => row.Bindable).SelectMany(row => uses[row]);
                foreach (var clash in PanelShortcuts.Clashes(all))
                {
                    var line = ShortcutsClashLine(clash);
                    if (banner.Children.Count > 0) line.Margin = new Thickness(0, PanelShortcuts.BannerStackGap, 0, 0);
                    banner.Children.Add(line);
                }
            }
            banner.Visibility = banner.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            var emptyText = PanelShortcuts.EmptyLine(chosen, anyShown);
            empty.Text = emptyText ?? string.Empty;
            empty.Visibility = emptyText == null ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// Keeps keyboard focus on the page when the filter hides the row that held it: a row bound under Not
        /// bound, or cleared under Bound, collapses once SimHub's picker closes, with its card if it was the
        /// last. Nothing between it and the panel takes focus, so WPF would let it leave the panel. It goes to
        /// the next row still shown, or the one before, else to the filter, as the shell keeps focus in place
        /// across a rebuild.
        /// </summary>
        private static void ShortcutsKeepFocus(IList<ShortcutsGroupState> groups, ShortcutsRowState from, FrameworkElement filter)
        {
            var rows = groups.SelectMany(group => group.Rows).ToList();
            var at = rows.IndexOf(from);
            var near = rows.Skip(at + 1).Concat(rows.Take(Math.Max(0, at)).Reverse());
            foreach (var row in near.Where(row => row.Bindable && row.Shown.Visibility == Visibility.Visible))
            {
                if (row.Row.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)) && row.Row.IsKeyboardFocusWithin) return;
            }
            filter.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }

        /// <summary>The artboard's role=status line: a button bound to two things, in caution, its name strong.</summary>
        private static Border ShortcutsClashLine(PanelShortcuts.Clash clash)
        {
            var icon = Ui.NavIcon(PanelIcons.Warning, Theme.Caution, PanelShortcuts.BannerIconSize);
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, 1, PanelShortcuts.BannerGap, 0);
            var text = Ui.Text(string.Empty, PanelShortcuts.BannerTextSize, FontWeights.Normal, Theme.TextSecondary);
            text.TextWrapping = TextWrapping.Wrap;
            text.Inlines.Add(new Run(clash.Lead) { FontWeight = FontWeights.SemiBold, Foreground = Ui.Brush(Theme.TextPrimary) });
            text.Inlines.Add(new Run(" " + clash.Rest));
            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(icon, Dock.Left);
            dock.Children.Add(icon);
            dock.Children.Add(text);
            var line = new Border
            {
                BorderBrush = Ui.Brush(Theme.CautionDeep),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                Background = Ui.Tint(Theme.Caution, 0.06),
                CornerRadius = new CornerRadius(Theme.Radius),
                Padding = new Thickness(PanelShortcuts.BannerPaddingX, PanelShortcuts.BannerPaddingY, PanelShortcuts.BannerPaddingX, PanelShortcuts.BannerPaddingY),
                Child = dock,
            };
            System.Windows.Automation.AutomationProperties.SetName(line, clash.Text);
            return line;
        }

        /// <summary>
        /// The bindings of a row's action, each with the presses it answers: from its editor's own model,
        /// which SimHub's Bind, Change and Clear write into, or from the shell's read before the editor has
        /// one, which knows no press type and so is taken to answer every press. Null when the editor could
        /// not be made (BuildBinder's fallback text) or SimHub cannot be read.
        /// </summary>
        private IList<PanelShortcuts.BindingUse> ShortcutsUses(ShortcutsRowState row)
        {
            var editor = row.Editor as ControlsEditor;
            if (editor == null) return null;
            try
            {
                var model = editor.Model;
                if (model == null || model.Triggers == null)
                {
                    var read = TriggersOf(row.Action);
                    return read == null ? null : read.Select(trigger => new PanelShortcuts.BindingUse(trigger, row.Place, row.Does)).ToList();
                }
                return model.Triggers
                    .Where(mapping => mapping != null && !string.IsNullOrWhiteSpace(mapping.Trigger))
                    .Select(mapping => new PanelShortcuts.BindingUse(mapping.Trigger, row.Place, row.Does, PanelShortcuts.FiresOn(mapping.PressType.ToString())))
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read the bindings of " + row.Action + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Calls <paramref name="changed"/> whenever the editor's bindings move: a model replaced (SimHub makes
        /// a new one each time the editor loads), a mapping added or removed, or a mapping's trigger or press
        /// type changed in place (SimHub's Change edits both on the mapping already there).
        /// </summary>
        /// <remarks>
        /// The mappings are SimHub's for the whole session, and a mapping's PropertyChanged holds its handlers
        /// strongly, so the watch is let go of when the editor leaves the tree and when the page is dropped, as
        /// HoldWhilePressed lets go of its own; Loaded takes it up again.
        /// </remarks>
        private void ShortcutsWatch(ControlsEditor editor, Action changed)
        {
            ControlsEditorModel watched = null;
            System.Collections.ObjectModel.ObservableCollection<InputMapping> watchedTriggers = null;
            var watchedMappings = new List<InputMapping>();
            PropertyChangedEventHandler mappingChanged = null;
            PropertyChangedEventHandler modelChanged = null;
            NotifyCollectionChangedEventHandler collectionChanged = null;

            Action rewatchMappings = () =>
            {
                foreach (var mapping in watchedMappings) mapping.PropertyChanged -= mappingChanged;
                watchedMappings.Clear();
                if (watchedTriggers == null) return;
                foreach (var mapping in watchedTriggers)
                {
                    if (mapping == null) continue;
                    mapping.PropertyChanged += mappingChanged;
                    watchedMappings.Add(mapping);
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
            mappingChanged = (sender, args) =>
            {
                if (args.PropertyName == null || args.PropertyName == "Trigger" || args.PropertyName == "PressType") changed();
            };
            collectionChanged = (sender, args) =>
            {
                rewatchMappings();
                changed();
            };
            modelChanged = (sender, args) =>
            {
                if (args.PropertyName != null && args.PropertyName != "Triggers") return;
                rewatchTriggers();
                changed();
            };
            Action attach = () =>
            {
                var model = editor.Model;
                if (ReferenceEquals(model, watched)) return;
                if (watched != null) watched.PropertyChanged -= modelChanged;
                watched = model;
                if (watched != null) watched.PropertyChanged += modelChanged;
                rewatchTriggers();
                changed();
            };
            Action detach = () =>
            {
                foreach (var mapping in watchedMappings) mapping.PropertyChanged -= mappingChanged;
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
            OnDrop(detach);
            attach();
        }
    }
}
