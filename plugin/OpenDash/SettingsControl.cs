// SettingsControl.cs: the openDash page in SimHub's left menu, built in code from design/canvas/Plugin.dc.html:
// header (mark, wordmark, plugin version), then General, Data, Layout and Dashboard sections, then the footer.
// Every change writes the settings object and saves it at once; the attached properties read the same object.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using SimHub.Plugins.Styles;

namespace OpenDashPlugin
{
    public class SettingsControl : UserControl
    {
        public const string DocumentationUrl = "https://github.com/xorob0/OpenDash#readme";
        public const string IssuesUrl = "https://github.com/xorob0/OpenDash/issues";

        private const double PageWidth = 960;
        private const double PagePadding = 32;

        private readonly OpenDash plugin;
        private TextBlock warningText;
        private FrameworkElement warningRow;
        private TextBlock dashboardTitle;
        private Border statusHost;
        private Button reinstallButton;

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
            RefreshWarning();
            RefreshStatus();
        }

        // Page

        private UIElement BuildPage()
        {
            var page = new DockPanel { LastChildFill = true, MaxWidth = PageWidth, HorizontalAlignment = HorizontalAlignment.Left };
            var header = BuildHeader();
            DockPanel.SetDock(header, Dock.Top);
            var footer = BuildFooter();
            DockPanel.SetDock(footer, Dock.Bottom);
            var body = Ui.VStack(0, BuildGeneral(), BuildData(), BuildLayout(), BuildCompanion(), BuildPitWall(), BuildDashboard());
            body.Margin = new Thickness(PagePadding, 0, PagePadding, 0);
            body.VerticalAlignment = VerticalAlignment.Top;
            page.Children.Add(header);
            page.Children.Add(footer);
            page.Children.Add(body);

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
                Ui.Text("open", Theme.SizeWordmark, FontWeights.Light, Theme.TextPrimary, PanelFonts.Data),
                Ui.Text("Dash", Theme.SizeWordmark, FontWeights.Bold, Theme.TextPrimary, PanelFonts.Data));
            var left = Ui.HStack(12, Ui.Mark(), wordmark);
            var right = Ui.HStack(8, Ui.Label("Plugin"), Ui.Numeral(OpenDash.Version, Theme.SizeNumeral, Theme.TextSecondary));
            var grid = Ui.Row(left, right);
            grid.Height = 72;
            grid.Margin = new Thickness(PagePadding, 0, PagePadding, 0);
            return grid;
        }

        // General

        private FrameworkElement BuildGeneral()
        {
            var toggle = BuildToggle(Settings.ShiftLights, on =>
            {
                Settings.ShiftLights = on;
                plugin.SaveSettings();
            });
            return Ui.Section("General",
                Ui.Row("Shift lights on the dash", "Turn off if your DDU has physical LEDs. The rev bar stays.", toggle));
        }

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

        // Data

        private FrameworkElement BuildData()
        {
            var position = BuildSegmented(Contract.PositionModes, new[] { "Overall", "Class" }, Settings.PositionMode, value =>
            {
                Settings.PositionMode = value;
                plugin.SaveSettings();
            });
            var delta = BuildSegmented(Contract.DeltaReferences, new[] { "Session best", "All-time best" }, Settings.DeltaReference, value =>
            {
                Settings.DeltaReference = value;
                plugin.SaveSettings();
            });
            var session = BuildSegmented(Contract.SessionProgressModes, new[] { "Auto", "Laps", "Time" }, Settings.SessionProgress, value =>
            {
                Settings.SessionProgress = value;
                plugin.SaveSettings();
            });
            return Ui.Section("Data",
                Ui.Row("Position", "Overall, or within your class.", position),
                Ui.Row("Delta reference", "Which lap the delta compares against.", delta),
                Ui.Row("Session progress", "Auto shows laps when the session declares a lap count, time otherwise.", session));
        }

        private static Segmented BuildSegmented(string[] values, string[] labels, string selected, Action<string> changed)
        {
            var options = values.Select((value, i) => new Segmented.Option(value, labels[i]));
            var control = new Segmented(options, selected);
            control.Changed += changed;
            return control;
        }

        // Layout

        private FrameworkElement BuildLayout()
        {
            var caption = Ui.Caption("Any card in any slot. The same card may be assigned twice; the panel says so and does not prevent it.", 846);
            var sizes = Ui.Caption("The picture is the 1920 x 480 face. A face with fewer slots uses the first ones: a 6-slot face shows Slot 01 to Slot 06.", 846);
            return Ui.Section("Layout", caption, BuildSlotPicker(), sizes, BuildWarning());
        }

        /// <summary>A picture of the dash: the left 3 x 2 grid, the hero column, the right 3 x 2 grid (846 x 165).</summary>
        private FrameworkElement BuildSlotPicker()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(BuildSlotGrid(1));
            row.Children.Add(BuildHeroCell());
            row.Children.Add(BuildSlotGrid(7));
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = row,
            };
        }

        /// <summary>Six slots, three per row, separated by 1 px rules drawn by the grid's background.</summary>
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

        /// <summary>"SLOT 01" at the top, the card select at the bottom, 6 px padding.</summary>
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
            box.SelectionChanged += OnSlotChanged;
            return box;
        }

        private void OnSlotChanged(object sender, SelectionChangedEventArgs args)
        {
            var box = (ComboBox)sender;
            if (box.SelectedIndex < 0) return;
            Settings.SetSlot((int)box.Tag, box.SelectedIndex);
            plugin.SaveSettings();
            RefreshWarning();
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

        // Companion

        /// <summary>The 21 companion modules as toggles in three columns, in page order.</summary>
        private FrameworkElement BuildCompanion()
        {
            var caption = Ui.Caption(
                "Each module is a page of the companion dashboard. A module that is off is skipped when you page with a wheel button. "
                + "Energy, Damage and Track rivals are off because iRacing publishes none of their data; switch them on for a sim that does.",
                846);
            return Ui.Section("Companion", caption, BuildModuleGrid());
        }

        private FrameworkElement BuildModuleGrid()
        {
            const int columns = 3;
            var rows = (Modules.Count + columns - 1) / columns;
            var grid = new Grid { Width = 846, HorizontalAlignment = HorizontalAlignment.Left };
            for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var i = 0; i < Modules.Count; i++)
            {
                var module = Modules.All[i];
                var cell = BuildModuleRow(module);
                Grid.SetColumn(cell, i / rows);
                Grid.SetRow(cell, i % rows);
                grid.Children.Add(cell);
            }
            return grid;
        }

        /// <summary>"07 Tyres" and its toggle; the description is the tooltip, so the grid stays readable.</summary>
        private FrameworkElement BuildModuleRow(Module module)
        {
            var name = Ui.Text(module.Number.ToString("00") + "  " + module.Name, Theme.SizeLabel, FontWeights.Normal, Theme.TextSecondary);
            name.VerticalAlignment = VerticalAlignment.Center;
            var toggle = BuildToggle(Settings.Module(module.Number), on =>
            {
                Settings.SetModule(module.Number, on);
                plugin.SaveSettings();
            });
            toggle.HorizontalAlignment = HorizontalAlignment.Right;
            var row = Ui.Row(name, toggle);
            row.Margin = new Thickness(0, 0, 24, 8);
            row.ToolTip = module.Description;
            return row;
        }

        // Pit wall

        /// <summary>The four zone pages, the wide zone page and the web view address.</summary>
        private FrameworkElement BuildPitWall()
        {
            var caption = Ui.Caption("Each pit wall page carries four data zones and, on the tower page, one wide zone. Choose what each one shows.", 846);
            var rows = new List<UIElement> { caption };
            foreach (var letter in Contract.PitWallZoneLetters)
            {
                var captured = letter;
                var select = BuildZoneSelect(ZonePages.Standard, Settings.Zone(captured), index =>
                {
                    Settings.SetZone(captured, index);
                    plugin.SaveSettings();
                });
                rows.Add(Ui.Row("Zone " + captured, ZoneDescription(captured), select));
            }
            var wide = BuildZoneSelect(ZonePages.Wide, Settings.WideZone, index =>
            {
                Settings.WideZone = Contract.NormaliseWideZonePage(index);
                plugin.SaveSettings();
            });
            rows.Add(Ui.Row("Wide zone", "The full-width zone on the tower page.", wide));
            rows.Add(Ui.Row("Web view address", "The page the Web view zone shows. http or https only; leave empty for none.", BuildWebViewBox()));
            return Ui.Section("Pit wall", rows.ToArray());
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
        private FrameworkElement BuildWebViewBox()
        {
            var box = new TextBox
            {
                Width = 320,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Text = Settings.WebViewUrl ?? string.Empty,
                ToolTip = "An http or https address; anything else is ignored.",
            };
            Action commit = () =>
            {
                Settings.WebViewUrl = Contract.NormaliseUrl(box.Text);
                plugin.SaveSettings();
                if (box.Text != Settings.WebViewUrl) box.Text = Settings.WebViewUrl;
            };
            box.LostFocus += (sender, args) => commit();
            box.KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter) commit();
            };
            return box;
        }

        /// <summary>The duplicate warning: triangle icon and caution text, hidden while every card is unique.</summary>
        private FrameworkElement BuildWarning()
        {
            warningText = Ui.Text("", Theme.SizeSmall, FontWeights.Normal, Theme.Caution);
            warningText.TextWrapping = TextWrapping.Wrap;
            var icon = Ui.Icon(Ui.WarningIcon, Theme.Caution);
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, 1, 0, 0);
            warningRow = Ui.HStack(10, icon, warningText);
            warningRow.Visibility = Visibility.Collapsed;
            return warningRow;
        }

        private void RefreshWarning()
        {
            var message = DuplicateAssignment.Warning(Settings.Slots);
            warningText.Text = message;
            warningRow.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        // Dashboard

        private FrameworkElement BuildDashboard()
        {
            dashboardTitle = Ui.Body("openDash");
            var caption = Ui.Caption("One dashboard per screen size, installed in SimHub DashTemplates. Reinstall restores every embedded copy; settings are kept.");
            var text = Ui.VStack(4, dashboardTitle, caption);
            text.MaxWidth = 460;
            text.HorizontalAlignment = HorizontalAlignment.Left;

            statusHost = new Border { VerticalAlignment = VerticalAlignment.Center };
            reinstallButton = BuildReinstallButton();
            var right = Ui.HStack(24, statusHost, reinstallButton);
            return Ui.Section("Dashboard", Ui.Row(text, right));
        }

        /// <summary>SimHub's primary button (SHButtonPrimary); a plain button when the type cannot be created.</summary>
        private Button BuildReinstallButton()
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
            button.Content = "Reinstall";
            button.MinWidth = 96;
            button.ToolTip = "Extract every embedded dashboard into DashTemplates again. Your settings are kept.";
            button.Click += (sender, args) => Reinstall();
            return button;
        }

        private void Reinstall()
        {
            reinstallButton.IsEnabled = false;
            try
            {
                plugin.Installer.EnsureInstalled(true);
            }
            catch (Exception ex)
            {
                Log.Error("Reinstall failed", ex);
            }
            finally
            {
                reinstallButton.IsEnabled = true;
                RefreshStatus();
            }
        }

        /// <summary>Title "openDash <installed version> · <n> dashboards" and the status pill: a 6 px dot and a tracked
        /// label showing the worst status across the packages; the tooltip lists every dashboard with its own status.</summary>
        private void RefreshStatus()
        {
            var installer = plugin.Installer;
            var version = installer.InstalledVersion ?? installer.EmbeddedVersion ?? OpenDash.Version;
            dashboardTitle.Text = DashboardInstaller.Summary(version, installer.PackageCount);

            string dot;
            var label = Theme.TextPrimary;
            switch (installer.Status)
            {
                case InstallStatus.UpToDate:
                    dot = Theme.StatusUpToDate;
                    break;
                case InstallStatus.UpdateAvailable:
                    dot = Theme.StatusUpdateAvailable;
                    break;
                case InstallStatus.Failed:
                    dot = Theme.StatusFailed;
                    break;
                default:
                    dot = Theme.StatusNotInstalled;
                    label = Theme.TextLabel;
                    break;
            }
            statusHost.Child = Ui.HStack(8, Ui.Dot(dot), Ui.Label(installer.Status.Label(), label));
            statusHost.ToolTip = installer.HasEmbeddedPackage
                ? (installer.Packages.Count > 0 ? installer.PackageReport() : installer.LastError)
                : "This build of the plugin carries no dashboard package.";
        }

        // Footer

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
