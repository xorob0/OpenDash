// SettingsControl.cs: the OpenDash page in SimHub's left menu. The shell -- header, the tab bar, the
// body the selected tab fills, and the footer -- plus the controls every tab uses.
//
// Four tabs, and a screen is the unit: Rig, Data, Lights, Install. docs/design/plugin.md is the design
// and is ahead of the canvas on two points, both said there. What replaced was one scrolling page of
// nine sections grouped by the kind of setting rather than by anything a user was trying to do, with
// the zones somebody actually came to change sitting between a legacy slot grid and a module list.
//
// Every change writes the settings object and saves it at once; the attached properties read the same
// object, so a change reaches a running dashboard on the next frame (ADR 0003).
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using SimHub.Plugins;
using SimHub.Plugins.Styles;
using SimHub.Plugins.UI;

namespace OpenDashPlugin
{
    public partial class SettingsControl : UserControl
    {
        public const string DocumentationUrl = "https://github.com/xorob0/OpenDash#readme";
        public const string IssuesUrl = "https://github.com/xorob0/OpenDash/issues";

        private const double PageWidth = 960;
        private const double PagePadding = 32;
        private const double BodyWidth = PageWidth - 2 * PagePadding;

        private const string TabRig = "Rig";
        private const string TabData = "Data";
        private const string TabLights = "Lights";
        private const string TabInstall = "Install";
        private static readonly string[] Tabs = { TabRig, TabData, TabLights, TabInstall };

        private readonly OpenDash plugin;

        /// <summary>
        /// Which tab is showing.
        /// </summary>
        /// <remarks>
        /// Held for the life of the panel and not persisted. Somebody who came to change a zone should
        /// find themselves where they left off within one sitting; somebody coming back next week should
        /// land on Rig, which is the answer to "what is this".
        /// </remarks>
        private string tab = TabRig;

        private readonly ContentControl bodyHost = new ContentControl();
        private StackPanel tabBar;

        private OpenDashSettings Settings => plugin.Settings;

        public SettingsControl(OpenDash plugin)
        {
            this.plugin = plugin;
            Background = Ui.Brush(Theme.SurfaceBase);
            Foreground = Ui.Brush(Theme.TextPrimary);
            FontFamily = PanelFonts.Label;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            Content = BuildPage();
            ShowTab(tab);
            // Opening the page is the earliest a check may run: never on the startup path, and never at
            // all unless the setting says so. A background check that finds nothing shows nothing.
            Check(manual: false);
        }

        private UIElement BuildPage()
        {
            var page = new DockPanel { LastChildFill = true, MaxWidth = PageWidth, HorizontalAlignment = HorizontalAlignment.Left };
            var header = BuildHeader();
            DockPanel.SetDock(header, Dock.Top);
            var tabs = BuildTabBar();
            DockPanel.SetDock(tabs, Dock.Top);
            var footer = BuildFooter();
            DockPanel.SetDock(footer, Dock.Bottom);

            bodyHost.Margin = new Thickness(PagePadding, 0, PagePadding, 0);
            bodyHost.VerticalAlignment = VerticalAlignment.Top;

            page.Children.Add(header);
            page.Children.Add(tabs);
            page.Children.Add(footer);
            page.Children.Add(bodyHost);

            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = page,
            };
            // The footer sits at the bottom of the viewport when the content is shorter (margin-top: auto).
            page.SetBinding(MinHeightProperty, new Binding("ViewportHeight") { Source = scroller });
            return scroller;
        }

        /// <summary>72 px: the segment mark and the wordmark on the left, "PLUGIN" and the version on the right.</summary>
        private FrameworkElement BuildHeader()
        {
            var wordmark = Ui.HStack(0,
                Ui.Tracked("open", Theme.SizeWordmark, FontWeights.Light, Theme.TextPrimary, Theme.TrackingNumeral, PanelFonts.Data),
                Ui.Tracked("Dash", Theme.SizeWordmark, FontWeights.Bold, Theme.TextPrimary, Theme.TrackingNumeral, PanelFonts.Data));
            var left = Ui.HStack(12, Ui.Mark(), wordmark);
            var right = Ui.HStack(8, Ui.Label("Plugin"), Ui.Numeral(OpenDash.Version, Theme.SizeNumeral, Theme.TextSecondary));
            var grid = Ui.Row(left, right);
            grid.Height = 72;
            grid.Margin = new Thickness(PagePadding, 0, PagePadding, 0);
            return grid;
        }

        private FrameworkElement BuildTabBar()
        {
            tabBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(PagePadding, 0, PagePadding, 0) };
            FillTabBar();
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = tabBar,
            };
        }

        private void FillTabBar()
        {
            tabBar.Children.Clear();
            foreach (var name in Tabs)
            {
                var captured = name;
                tabBar.Children.Add(Ui.Tab(captured, captured == tab, () => ShowTab(captured)));
            }
        }

        /// <summary>
        /// Draws a tab, rebuilding it from the settings rather than re-showing what was there.
        /// </summary>
        /// <remarks>
        /// A tab is thrown away when it is left, so nothing has to be kept in step while it is not on
        /// screen: adding a screen on Install and switching to Rig shows the screen, and every cached
        /// control a tab holds belongs to the one build that created it. The refresh methods each guard
        /// against a null control for that reason -- they are called from the constructor, from a
        /// background check that lands after a tab has gone, and from a build that is still in progress.
        /// </remarks>
        private void ShowTab(string name)
        {
            tab = name;
            FillTabBar();
            ForgetTabControls();
            switch (name)
            {
                case TabData:
                    bodyHost.Content = BuildDataTab();
                    break;
                case TabLights:
                    bodyHost.Content = BuildLightsTab();
                    break;
                case TabInstall:
                    bodyHost.Content = BuildInstallTab();
                    break;
                default:
                    bodyHost.Content = BuildRigTab();
                    break;
            }
        }

        /// <summary>Drops every control the tab being left owned, so a refresh cannot write into a
        /// control that is no longer on screen and read as having done something.</summary>
        private void ForgetTabControls()
        {
            faceWarningText = null;
            faceWarningRow = null;
            stripCaption = null;
            zoneSelects.Clear();
            zoneMaskButtons.Clear();
            zoneMaskBoxes.Clear();
            zoneClassBoxes.Clear();
            barEndButtons.Clear();
            flagBoxLine = null;
            flagBoxButton = null;
            flagBoxCopyButton = null;
            flagBoxPath = null;
            updateLine = null;
            updateButton = null;
            restoreButton = null;
            checkButton = null;
            reinstallButton = null;
            statusHost = null;
            dashboardTitle = null;
        }

        /// <summary>Redraws the tab that is showing, after something changed the rig under it.</summary>
        private void Redraw()
        {
            ShowTab(tab);
        }

        private void Save()
        {
            plugin.SaveSettings();
        }

        // --- The controls every tab uses --------------------------------------------------------

        /// <summary>SimHub's own switch (SHToggleButton), so that it looks like every other toggle in SimHub.</summary>
        private static ToggleButton BuildToggle(bool isOn, Action<bool> changed)
        {
            ToggleButton toggle;
            try
            {
                toggle = new SHToggleButton();
            }
            catch (Exception ex)
            {
                Log.Warn("SHToggleButton is unavailable; using a plain toggle: " + ex.Message);
                toggle = new ToggleButton();
            }
            toggle.IsChecked = isOn;
            toggle.Checked += (sender, args) => changed(true);
            toggle.Unchecked += (sender, args) => changed(false);
            return toggle;
        }

        private static Segmented BuildSegmented(string[] values, string[] labels, string selected, Action<string> changed)
        {
            var options = values.Select((value, i) => new Segmented.Option(value, labels[i]));
            var control = new Segmented(options, selected);
            control.Changed += changed;
            return control;
        }

        /// <summary>One value of a value set, as a drop-down: the control for a list longer than the two
        /// or three options Segmented.cs is drawn for. The labels are positional, so values[i] is what
        /// labels[i] names.</summary>
        private static ComboBox BuildChoice(string[] values, string[] labels, string selected, double width, Action<string> changed)
        {
            var box = new ComboBox
            {
                Width = width,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            foreach (var label in labels) box.Items.Add(label);
            var index = Array.IndexOf(values, selected);
            box.SelectedIndex = index >= 0 ? index : 0;
            box.SelectionChanged += (sender, args) =>
            {
                if (box.SelectedIndex < 0 || box.SelectedIndex >= values.Length) return;
                changed(values[box.SelectedIndex]);
            };
            return box;
        }

        /// <summary>A page picker in page-number order, so that SelectedIndex is the page number.</summary>
        private static ComboBox BuildPageSelect(System.Collections.Generic.IReadOnlyList<ZonePage> pages, int selected, double width, Action<int> changed)
        {
            var box = new ComboBox
            {
                Width = width,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
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

        private static FrameworkElement BuildPercentBox(int value, Action<int> changed) => BuildNumberBox(value, 0, 100, changed);

        /// <summary>A small number field that repairs whatever is typed into it rather than refusing it.</summary>
        private static FrameworkElement BuildNumberBox(int value, int min, int max, Action<int> changed)
        {
            var box = new TextBox
            {
                Width = 80,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                HorizontalAlignment = HorizontalAlignment.Right,
                Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            Action commit = () =>
            {
                int parsed;
                if (!int.TryParse(box.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out parsed)) parsed = value;
                if (parsed < min) parsed = min;
                if (parsed > max) parsed = max;
                box.Text = parsed.ToString(System.Globalization.CultureInfo.InvariantCulture);
                changed(parsed);
            };
            box.LostFocus += (sender, args) => commit();
            box.KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter) commit();
            };
            return box;
        }

        private static Button BuildSecondaryButton(string content, string tooltip)
        {
            Button button;
            try
            {
                button = new SHButtonPrimary();
            }
            catch (Exception ex)
            {
                Log.Warn("SHButtonPrimary is unavailable; using a plain button: " + ex.Message);
                button = new Button();
            }
            button.Content = content;
            button.MinWidth = 96;
            button.ToolTip = tooltip;
            return button;
        }

        /// <summary>
        /// SimHub's own control for binding an input to an action, so a wheel button is bound here
        /// rather than by sending the driver to Controls and events to find the name.
        ///
        /// It is a SimHub UserControl and SimHub is not always there -- the panel is constructed in
        /// tests and could be constructed by a host that does not carry the style -- so a failure
        /// falls back to naming the action, which is exactly what somebody binding it by hand needs.
        /// </summary>
        private static FrameworkElement BuildBinder(string action, string friendlyName, bool hold = false)
        {
            try
            {
                var editor = new ControlsEditor { ActionName = Contract.FullActionName(action), FriendlyName = friendlyName, MinWidth = 260 };
                editor.HorizontalAlignment = HorizontalAlignment.Right;
                if (hold) HoldWhilePressed(editor);
                return editor;
            }
            catch (Exception ex)
            {
                Log.Warn("ControlsEditor is unavailable; naming the action instead: " + ex.Message);
                var text = Ui.Text(friendlyName + " — bind " + Contract.FullActionName(action) + " in Controls and events", Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);
                text.HorizontalAlignment = HorizontalAlignment.Right;
                return text;
            }
        }

        /// <summary>
        /// Forces a glance binding to the one press type that can hold anything.
        ///
        /// SimHub only calls an action's start on press and its end on release when the mapping's
        /// press type is `During`; every other type goes through TriggerAction, which fires start and
        /// end back to back. The binding dialog offers ShortAndLongPress by default, so a driver who
        /// binds the glance the obvious way gets a page that appears and vanishes in one frame.
        ///
        /// The glance is only meaningful as a hold, so any binding to it is corrected rather than
        /// second-guessed. The dialog writes into Model.Triggers; this watches that collection.
        /// </summary>
        private static void HoldWhilePressed(ControlsEditor editor)
        {
            var model = editor.Model;
            if (model == null) return;
            Action apply = () =>
            {
                foreach (var mapping in model.Triggers)
                {
                    if (mapping != null && mapping.PressType != PressType.During) mapping.PressType = PressType.During;
                }
            };
            model.Triggers.CollectionChanged += (sender, args) => apply();
            apply();
        }

        // --- Footer -----------------------------------------------------------------------------

        /// <summary>56 px, rule on top: Documentation and Report an issue links, "MIT LICENCE" on the right.</summary>
        private FrameworkElement BuildFooter()
        {
            var links = Ui.HStack(24, BuildLink("Documentation", DocumentationUrl), BuildLink("Report an issue", IssuesUrl));
            var grid = Ui.Row(links, Ui.Label("MIT licence"));
            grid.Height = 56;
            grid.Margin = new Thickness(PagePadding, 0, PagePadding, 0);
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = grid,
            };
        }

        /// <summary>SimHub's link button (SHLinkButton) carrying the canvas's accent text and external-link icon.</summary>
        private static Button BuildLink(string text, string url)
        {
            Button button;
            try
            {
                button = new SHLinkButton();
            }
            catch (Exception ex)
            {
                Log.Warn("SHLinkButton is unavailable; using a plain button: " + ex.Message);
                button = new Button();
            }
            button.Content = Ui.HStack(6,
                Ui.Text(text, Theme.SizeBody, FontWeights.Medium, Theme.Accent),
                Ui.Icon(Ui.ExternalLinkIcon, Theme.Accent));
            button.Padding = new Thickness(0);
            button.Cursor = Cursors.Hand;
            button.ToolTip = url;
            button.Click += (sender, args) => Ui.OpenUrl(url);
            return button;
        }
    }
}
