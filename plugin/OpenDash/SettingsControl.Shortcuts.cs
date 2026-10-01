// SettingsControl.Shortcuts.cs: the Shortcuts page -- every wheel button and key in one list, a card per
// screen and one for the rig's lights, drawn to Shortcuts.dc.html.
//
// Each binding is SimHub's own ControlsEditor (BuildBinder), so a button is bound here rather than by sending
// the driver to Controls and events to find an action's name; SimHub draws "Click to configure" or each
// binding with its press type, and Change, Clear and Add on hover, in a slot of fixed width that every row
// gives it, with its own name column dropped. Around it the page draws what the artboard adds: the press each action
// answers to (until a binding's own press type says it, PanelShortcuts.ShowsPress), a card's "3 of 6", the All | Bound | Not bound filter, and the line naming a button bound to two
// things. Those three read each editor's own Model.Triggers, again whenever a binding is made, changed or
// cleared, here or anywhere SimHub reports it (ShortcutsFollowSimHub), and are hidden when SimHub's mappings
// cannot be read. PanelShortcuts decides the rows, the words and
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
            /// <summary>The binder's slot, outlined in caution while a clash line names the row.</summary>
            public Border Slot;
            /// <summary>The press word, drawn while the row is not bound, and always on a held row.</summary>
            public TextBlock Press;
            public bool IsHold;
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

        /// <summary>The live row the driver last pressed or focused in, which a binding made or cleared there
        /// can hide once SimHub has taken the focus off it.</summary>
        private sealed class ShortcutsTouch
        {
            public ShortcutsRowState Row;
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
            // Read only up to where a row stops changing: from RowStackBelow up a row lies flat with the
            // fixed slot, so a resize past it leaves the page, and every SimHub editor on it, alone.
            var layout = new ShortcutsLayout(ContentWidthUpTo(PanelShortcuts.RowStackBelow));

            var screens = new List<ShortcutsGroupState>();
            foreach (var screen in Settings.RigScreens())
            {
                if (screen == null) continue;
                if (screen.IsFace) screens.Add(BuildShortcutsFace(screen, layout));
                else if (screen.IsPitWall && PanelShortcuts.PitWallCard(screen.Width, screen.Height, ShortcutsGlanceBound(screen))) screens.Add(BuildShortcutsPitWall(screen, layout));
                else if (screen.IsCompanion) screens.Add(BuildShortcutsCompanion(screen, layout));
                // A round screen cycles nothing, so it has no card, and nor has a portrait pit wall, whose
                // glance has no zone to swap, unless that glance is still bound and has to be cleared.
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
            // The artboard draws no words over the filter, and names its group "Show" for a screen reader alone:
            // with no row title above it, as every other page's segmented control has, its three choices
            // would otherwise be read out with nothing to say what they choose.
            System.Windows.Automation.AutomationProperties.SetName(filter, PanelShortcuts.FilterTitle);
            var header = BuildShortcutsHeader(filter);

            var touch = new ShortcutsTouch();
            foreach (var row in groups.SelectMany(group => group.Rows).Where(row => row.Bindable))
            {
                var touched = row;
                row.Shown.PreviewMouseDown += (sender, args) => touch.Row = touched;
                row.Shown.GotKeyboardFocus += (sender, args) => touch.Row = touched;
            }
            evaluate = () => ShortcutsEvaluate(groups, filter, banner, empty, touch);

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
            var editors = new List<ControlsEditor>();
            foreach (var row in groups.SelectMany(group => group.Rows))
            {
                var editor = row.Editor as ControlsEditor;
                if (editor != null) ShortcutsWatch(editor, changed);
                if (editor != null) editors.Add(editor);
            }
            evaluate();

            var sections = new List<UIElement> { header, banner };
            sections.AddRange(groups.Select(group => (UIElement)group.Card));
            sections.Add(empty);
            var page = ShortcutsTitleTagged(PageLayout(PanelShortcuts.Title, null, sections.ToArray()));
            ShortcutsFollowSimHub(page, editors, changed, () => dropped);
            return page;
        }

        /// <summary>
        /// Keeps every editor's bindings SimHub's own. An editor's model holds a copy of SimHub's list, taken
        /// when it loads, and SimHub's picker can delete another action's mapping from that list alone
        /// (ControlPicker's Delete on an "already used" binding): SimHub 9.12.6 wrote the handler that would
        /// refresh the editors (ControlsEditor.PluginManager_InputMappingsChanged) but only ever unsubscribes
        /// it. So whenever SimHub reports a change, once it has settled, each editor's model is brought
        /// back into line with SimHub's list in place, and the page reads again: the counts, the filter and
        /// the clash line stop naming a binding SimHub no longer has, and SimHub's editor stops drawing it.
        /// </summary>
        /// <remarks>
        /// In place rather than a new model, so that the watches on the model (this page's, and
        /// HoldWhilePressed's on a glance) see a mapping come or go as the collection's own change. After the
        /// change has settled, because SimHub's Add writes its list first and the editor's copy after it: a
        /// reconcile in between would put the new mapping in the copy twice. The event is static and SimHub's
        /// for the session, so it is held only while the page is on screen and let go of when the build is
        /// dropped, as the shell holds its own.
        /// </remarks>
        private void ShortcutsFollowSimHub(FrameworkElement page, IList<ControlsEditor> editors, Action changed, Func<bool> dropped)
        {
            var pending = false;
            EventHandler mappingsChanged = null;
            mappingsChanged = (sender, args) =>
            {
                if (pending || dropped()) return;
                pending = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    pending = false;
                    if (dropped()) return;
                    foreach (var editor in editors) ShortcutsReconcile(editor);
                    changed();
                }));
            };
            Action release = () => PluginManager.InputMappingsChanged -= mappingsChanged;
            page.Loaded += (sender, args) =>
            {
                release();
                if (!dropped()) PluginManager.InputMappingsChanged += mappingsChanged;
            };
            page.Unloaded += (sender, args) => release();
            OnDrop(release);
        }

        /// <summary>
        /// Brings one editor's model back into line with SimHub's list, in place: what SimHub no longer has is
        /// removed, and what it has that the model lacks is added. SimHub's list is internal to it, so it is
        /// read the way the shell reads it, through a new ControlsEditorModel, whose Triggers are the very
        /// mappings in that list; they are compared as objects, as SimHub's own Remove compares them.
        /// </summary>
        private static void ShortcutsReconcile(ControlsEditor editor)
        {
            try
            {
                var model = editor.Model;
                if (model == null || model.Triggers == null || string.IsNullOrEmpty(editor.ActionName)) return;
                if (PluginManager.GetInstance() == null) return;
                var simhub = new ControlsEditorModel(editor.ActionName, null).Triggers;
                if (simhub == null) return;
                foreach (var gone in model.Triggers.Where(mapping => !simhub.Any(kept => ReferenceEquals(kept, mapping))).ToList())
                {
                    model.Triggers.Remove(gone);
                }
                foreach (var added in simhub.Where(mapping => !model.Triggers.Any(held => ReferenceEquals(held, mapping))).ToList())
                {
                    model.Triggers.Add(added);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read SimHub's bindings for " + editor.ActionName + " again: " + ex.Message);
            }
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

        /// <summary>Whether a pit wall's glance is bound, taking a binding that cannot be read as one: a row
        /// is never hidden on a guess.</summary>
        private bool ShortcutsGlanceBound(ScreenInstance screen)
        {
            var triggers = TriggersOf(PanelShortcuts.GlanceBinding(screen.Kind, screen.Namespace, screen.Name).Action);
            return triggers == null || triggers.Count > 0;
        }

        /// <summary>A pit wall's card: the glance and nothing else. A key beside the monitor is the likelier
        /// gesture, since nobody drives a pit wall. On a portrait wall, drawn only while the glance is bound,
        /// its caption says the glance does nothing there.</summary>
        private ShortcutsGroupState BuildShortcutsPitWall(ScreenInstance screen, ShortcutsLayout layout)
        {
            var group = ShortcutsCard(screen.Name, PanelShortcuts.GroupDetail(screen.Name, screen.Kind, screen.Width, screen.Height), null);
            var glance = PanelShortcuts.GlanceBinding(screen.Kind, screen.Namespace, screen.Name);
            var caption = PanelShortcuts.PitWallGlances(screen.Width, screen.Height) ? Ui.Caption(PanelCopy.PitWallGlance) : Ui.Caption(PanelShortcuts.PortraitGlanceCaption);
            ShortcutsBinding(group, screen.Name, glance, BuildBinder(glance.Action, glance.BinderName, hold: true), caption, layout);
            return group;
        }

        /// <summary>A companion's card: where SimHub binds its paging, which is not OpenDash's to bind, and
        /// the glance, which is.</summary>
        private ShortcutsGroupState BuildShortcutsCompanion(ScreenInstance screen, ShortcutsLayout layout)
        {
            var crumbs = Ui.Crumbs(PanelShortcuts.PagingCrumbs);
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
            // at infinity): a long name wraps, as the artboard's .gh lets its title shrink and wrap, and the
            // kind and size go under it rather than past the count. Never an ellipsis: the name is all that
            // tells two faces' cards apart, and "... (left)" and "... (right)" cut short read alike.
            var name = Ui.Text(title ?? string.Empty, PanelShortcuts.GroupTitleSize, FontWeights.SemiBold, Theme.TextPrimary);
            name.TextWrapping = TextWrapping.Wrap;
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
            Border slot;
            TextBlock press;
            var row = ShortcutsRow(binding.Label, binding.Press, editor, layout, caption, out slot, out press, tags);
            Ui.Anchor(row, PanelBindings.Anchor(binding.Action));
            group.Rows.Add(new ShortcutsRowState { Action = binding.Action, Editor = editor, Row = row, Shown = row, Slot = slot, Press = press, IsHold = binding.IsHold, Bindable = true, Place = place, Does = binding.Does });
            group.Body.Children.Add(row);
        }

        /// <summary>A greyed row: the registry's title, a noun phrase as on every page's greyed rows ("Rig
        /// test", "Alert dismissal"), so the row reads as search lists it and as its automation name says;
        /// tapped, a Not bound key that nothing answers, and the registry's entry for its tip and ticket.</summary>
        private static void ShortcutsSoon(ShortcutsGroupState group, SoonItem item, ShortcutsLayout layout)
        {
            // The key sits at the slot's left, where the artboard's .key starts in every row, and where SimHub's
            // bindings start once its name column is dropped.
            var chip = Ui.BindingChip(Ui.NotBound, false, key: true);
            chip.HorizontalAlignment = HorizontalAlignment.Left;
            Border slot;
            TextBlock press;
            var row = ShortcutsRow(item.Title, PanelShortcuts.Tap, chip, layout, null, out slot, out press);
            var shown = Ui.Soon(row, item);
            group.Rows.Add(new ShortcutsRowState { Row = row, Shown = shown, Slot = slot, Press = press, Bindable = false });
            group.Body.Children.Add(shown);
        }

        /// <summary>
        /// The artboard's .r: the name with its tags, the press in a 90 px column and the binder in a slot of
        /// fixed width after it, 16 apart, padded 10 by 16 under a rule. The columns are fixed rather than
        /// sized by each row's control, so press and binder line up down a card, greyed rows included. Where
        /// the content cannot give the name room beside them, the binder goes under the name and the press,
        /// across the row's whole width.
        /// </summary>
        /// <remarks>The border's Tag carries the row's parts, so a Soon appends its tag after the name. The
        /// slot is handed back for the clash mark: it always keeps a border's room, empty until a clash line
        /// names the row, so marking a row moves nothing in it.</remarks>
        private static Border ShortcutsRow(string label, string press, FrameworkElement control, ShortcutsLayout layout, FrameworkElement caption, out Border slot, out TextBlock pressText, params FrameworkElement[] tags)
        {
            slot = null;
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
                caption.MaxWidth = Math.Min(caption.MaxWidth, PanelShortcuts.CaptionMaxWidth);
                left.Children.Add(caption);
            }
            pressText = Ui.Text(press, Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);
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
                // the room it has inside the clash outline's room, its Change and Clear included, rather than
                // clipped at the slot's edge.
                control.MinWidth = Math.Min(control.MinWidth, Math.Max(0, layout.Binder - 2 * PanelMetrics.BorderWeight));
                slot = new Border
                {
                    Child = control,
                    Width = layout.Binder,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                    CornerRadius = new CornerRadius(Theme.Radius),
                };
                if (layout.Stacks)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    slot.Margin = new Thickness(0, PanelShortcuts.StackGap, 0, 0);
                    Grid.SetRow(slot, 1);
                    Grid.SetColumnSpan(slot, 2);
                    // Under the name the slot is the row's whole width, whatever it is: layout.Binder counts a
                    // scroll bar the page may not have, and only lowers the editor's MinWidth.
                    slot.Width = double.NaN;
                    slot.HorizontalAlignment = HorizontalAlignment.Stretch;
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
        /// <remarks>
        /// The name carries the template's only context menu, SimHub's "Trigger now" (a Label whose content,
        /// the FriendlyName TextBlock, holds it, and ControlsEditor.OnApplyTemplate wires its item to
        /// TriggerAction). Collapsed in a 0 px column it could no longer be reached, so it moves to the
        /// editor's border first: a right-click anywhere on the binder still fires the action, to try a zone
        /// or night mode without the wheel.
        /// </remarks>
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
                var menuOwner = named.Select(ShortcutsMenuOwner).FirstOrDefault(owner => owner != null);
                if (menuOwner != null && border.ContextMenu == null)
                {
                    var menu = menuOwner.ContextMenu;
                    menuOwner.ContextMenu = null;
                    border.ContextMenu = menu;
                }
                // The name goes too, so its line no longer sets the editor's height.
                foreach (var child in named) child.Visibility = Visibility.Collapsed;
                grid.ColumnDefinitions[0].Width = new GridLength(0);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not drop the name column of SimHub's binding editor: " + ex.Message);
            }
        }

        /// <summary>The element in the editor's name column that carries a context menu: the name itself, or
        /// the element a Label shows as its content. Null when neither does.</summary>
        private static FrameworkElement ShortcutsMenuOwner(UIElement named)
        {
            var element = named as FrameworkElement;
            if (element != null && element.ContextMenu != null) return element;
            var label = named as Label;
            var content = label == null ? null : label.Content as FrameworkElement;
            return content != null && content.ContextMenu != null ? content : null;
        }

        /// <summary>
        /// Reads every editor's bindings and redraws what depends on them: the filter's rows and cards, each
        /// card's count, the clash lines and the empty line. PanelShortcuts decides each: a row's state, whether
        /// the page is readable, what the filter shows, each count and each clash. When any live row cannot be
        /// read, the filter, the counts and the clash lines are hidden and every row shows.
        /// </summary>
        private void ShortcutsEvaluate(IList<ShortcutsGroupState> groups, FrameworkElement filter, StackPanel banner, TextBlock empty, ShortcutsTouch touch)
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
            // By the time this runs SimHub has usually taken the focus off it: its picker's Closed hands the
            // focus to SimHub's main window itself (SHDialogContentBase, 9.12.6), and a Clear button leaves the
            // tree with its binding. So while the focus sits on no element but a bare window, the row last
            // pressed or focused in stands for it.
            var focused = groups.SelectMany(group => group.Rows).FirstOrDefault(row => row.Shown.IsKeyboardFocusWithin);
            if (focused == null && touch.Row != null && touch.Row.Shown.Visibility == Visibility.Visible && ShortcutsFocusOnNothing()) focused = touch.Row;

            var anyShown = false;
            foreach (var group in groups)
            {
                var firstShown = true;
                foreach (var row in group.Rows)
                {
                    // The column keeps its width, so a row with no press word still lines up with the others.
                    if (row.Press != null) row.Press.Visibility = PanelShortcuts.ShowsPress(states[row], row.IsHold) ? Visibility.Visible : Visibility.Hidden;
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

            // The record is cleared before the focus moves on, so the row that takes it records itself through
            // its own GotKeyboardFocus: cleared after, the next row's record was wiped at once, and a second
            // bind by keyboard in a row found nothing to hand on from.
            if (focused != null && focused.Shown.Visibility != Visibility.Visible)
            {
                touch.Row = null;
                ShortcutsKeepFocus(groups, focused, filter);
            }

            banner.Children.Clear();
            var clashes = readable
                ? PanelShortcuts.Clashes(groups.SelectMany(group => group.Rows).Where(row => row.Bindable).SelectMany(row => uses[row]))
                : new List<PanelShortcuts.Clash>();
            foreach (var clash in clashes)
            {
                var line = ShortcutsClashLine(clash);
                if (banner.Children.Count > 0) line.Margin = new Thickness(0, PanelShortcuts.BannerStackGap, 0, 0);
                banner.Children.Add(line);
            }
            banner.Visibility = banner.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            // Each row the lines name has its binder outlined in caution, and every other loses its outline,
            // so a row cleared or rebound, or a page no longer readable, keeps no stale mark.
            foreach (var row in groups.SelectMany(group => group.Rows).Where(row => row.Slot != null))
            {
                var marked = row.Bindable && PanelShortcuts.Marks(clashes, row.Place, row.Does);
                row.Slot.BorderBrush = marked ? Ui.Brush(Theme.CautionDeep) : null;
            }

            var emptyText = PanelShortcuts.EmptyLine(chosen, anyShown);
            empty.Text = emptyText ?? string.Empty;
            empty.Visibility = emptyText == null ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>Whether keyboard focus is on no element of the window, only on the window itself or
        /// nowhere, as SimHub leaves it when its picker closes or the focused Clear leaves the tree.</summary>
        private static bool ShortcutsFocusOnNothing()
        {
            var at = Keyboard.FocusedElement;
            return at == null || at is Window;
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
            var near = rows.Skip(at + 1).Concat(Enumerable.Reverse(rows.Take(Math.Max(0, at))));
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
            // A message line keeps a measure, as the panel's every other does: the amber box stretches with
            // the column like a card, and the sentence in it wraps at BodyWidth however wide that is.
            text.MaxWidth = BodyWidth;
            text.HorizontalAlignment = HorizontalAlignment.Left;
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
                Background = Ui.Tint(Theme.Caution, PanelShortcuts.BannerTint),
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
