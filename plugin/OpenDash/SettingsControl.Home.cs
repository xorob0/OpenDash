// SettingsControl.Home.cs: the Home page -- what needs fixing, in the driver's terms and with the steps in
// SimHub; what each device is; and the two controls a driver reaches for between sessions.
//
// The #503 foundation draws PanelAttention's list and the rig's counts; the Home page agent owns this file and
// rebuilds it to Main.dc.html, including "Right now" with each device's live state. What needs fixing is asked
// of SimHub on Go and on "Check again" (SettingsControl.Status.cs), never on the tick.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private FrameworkElement BuildHomePage(PanelRoute to)
        {
            DrawsLighting();
            var head = new StackPanel { Orientation = Orientation.Vertical };
            var eyebrow = Ui.Eyebrow(PanelHome.Title);
            eyebrow.Margin = new Thickness(0, 0, 0, 10);
            head.Children.Add(eyebrow);
            head.Children.Add(Ui.PageTitle(PanelAttention.Headline(issues.Count)));

            var sections = new List<UIElement>();
            if (issues.Count > 0) sections.Add(Ui.Anchor(BuildAttentionCard(), PanelHome.AnchorAttention));
            sections.Add(Ui.Anchor(BuildRigCounts(), PanelHome.AnchorRightNow));
            sections.Add(Ui.Anchor(BuildQuickControls(), PanelHome.AnchorQuickControls));

            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(head);
            foreach (var section in sections)
            {
                var element = (FrameworkElement)section;
                element.Margin = new Thickness(0, PanelShell.SectionGap, 0, 0);
                stack.Children.Add(element);
            }
            return stack;
        }

        /// <summary>The issues, one row each: an icon on the caution's tint, what is wrong and what to do, the
        /// steps in SimHub where there are some, and the one press that helps.</summary>
        private FrameworkElement BuildAttentionCard()
        {
            var rows = new StackPanel { Orientation = Orientation.Vertical };
            for (var i = 0; i < issues.Count; i++)
            {
                var row = BuildIssueRow(issues[i]);
                if (i > 0)
                {
                    row.BorderBrush = Ui.Brush(Theme.Rule);
                    row.BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0);
                }
                rows.Children.Add(row);
            }
            return Ui.CardBox(rows, 0);
        }

        private Border BuildIssueRow(PanelIssue issue)
        {
            var icon = Ui.NavIcon(IssueIcon(issue), Theme.Caution);
            var square = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(Theme.Radius),
                Background = Ui.Tint(Theme.Caution, 0.12),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 16, 0),
                Child = icon,
            };
            icon.HorizontalAlignment = HorizontalAlignment.Center;

            var text = new StackPanel { Orientation = Orientation.Vertical };
            var title = Ui.Text(issue.Title, PanelShell.RowTitleSize, FontWeights.SemiBold, Theme.TextPrimary);
            title.TextWrapping = TextWrapping.Wrap;
            text.Children.Add(title);
            if (!string.IsNullOrEmpty(issue.Detail))
            {
                var detail = Ui.Prose(issue.Detail);
                detail.Margin = new Thickness(0, 3, 0, 0);
                text.Children.Add(detail);
            }
            if (issue.Steps.Count > 0)
            {
                var steps = Ui.Steps(issue.Steps);
                steps.Margin = new Thickness(0, 10, 0, 0);
                text.Children.Add(steps);
            }

            var press = Ui.Button(issue.ActionLabel, PanelButtonKind.Outline);
            press.VerticalAlignment = VerticalAlignment.Top;
            press.Margin = new Thickness(16, 0, 0, 0);
            press.Click += (sender, args) => Act(issue);

            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(square, Dock.Left);
            DockPanel.SetDock(press, Dock.Right);
            dock.Children.Add(square);
            dock.Children.Add(press);
            dock.Children.Add(text);
            return new Border { Padding = new Thickness(18, 16, 18, 16), Child = dock };
        }

        private static string IssueIcon(PanelIssue issue)
        {
            switch (issue.Page)
            {
                case PanelPage.Leds: return PanelIcons.Leds;
                case PanelPage.Matrix: return PanelIcons.Matrix;
                case PanelPage.Updates: return PanelIcons.Restart;
                default: return issue.Action == PanelIssueAction.Reinstall ? PanelIcons.Warning : PanelIcons.Restart;
            }
        }

        /// <summary>What an issue's press does: opens the page with the thing selected, asks SimHub again, or
        /// writes a screen's dashboard back.</summary>
        private void Act(PanelIssue issue)
        {
            switch (issue.Action)
            {
                case PanelIssueAction.CheckAgain:
                    CheckAgain();
                    return;
                case PanelIssueAction.Reinstall:
                    var screen = Settings.ScreenByNamespace(issue.Subject);
                    if (screen != null)
                    {
                        InstallScreenAgain(screen);
                        return;
                    }
                    break;
            }
            Open(issue.Page, issue.Subject, issue.Anchor);
        }

        /// <summary>What the rig has, one card per kind of device, each opening its page.</summary>
        private FrameworkElement BuildRigCounts()
        {
            var screens = Settings.RigScreens();
            var strips = Settings.LedBarList();
            var matrices = Settings.MatrixPanels().ToList();
            var grid = Ui.CardGrid(220, 16, 3,
                DeviceCount(PanelNav.Label(PanelPage.Screens), screens.Select(s => s.Name), PanelPage.Screens, PanelScreens.NoScreens + "."),
                DeviceCount(PanelNav.Label(PanelPage.Leds), strips.Select(b => b.Name), PanelPage.Leds, PanelLights.NoBars),
                DeviceCount(PanelNav.Label(PanelPage.Matrix), matrices.Select(m => Settings.MatrixName(m) ?? "Matrix " + m), PanelPage.Matrix, PanelLights.NoPanels));
            return PageSection(PanelHome.RightNowTitle, grid);
        }

        /// <param name="empty">The page's own words for having none, so Home and the page say it alike.</param>
        private FrameworkElement DeviceCount(string title, IEnumerable<string> names, PanelPage page, string empty)
        {
            var list = names.ToList();
            var open = Ui.LinkButton("Open");
            open.Click += (sender, args) => Go(page);
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(Ui.Row(Ui.Eyebrow(title), open));
            if (list.Count == 0)
            {
                var none = Ui.Prose(empty);
                none.Margin = new Thickness(0, 10, 0, 0);
                stack.Children.Add(none);
            }
            foreach (var name in list)
            {
                var line = Ui.Text(name, Theme.SizeBody, FontWeights.SemiBold, Theme.TextPrimary);
                line.TextTrimming = TextTrimming.CharacterEllipsis;
                line.Margin = new Thickness(0, 10, 0, 0);
                stack.Children.Add(line);
            }
            return Ui.CardBox(stack, 14);
        }

        /// <summary>
        /// Brightness and night mode. The slider edits the value in force: the night brightness while night
        /// mode is on and the day one otherwise, which is the rule the wheel buttons follow too.
        /// </summary>
        private FrameworkElement BuildQuickControls()
        {
            var nightOn = Settings.LightsNightMode;
            var label = nightOn ? PanelSettings.NightBrightnessTitle : PanelSettings.BrightnessTitle;
            var slider = Ui.Slider(nightOn ? Settings.LightsNightBrightness : Settings.LightsBrightness, value =>
            {
                if (Settings.LightsNightMode) Settings.LightsNightBrightness = value;
                else Settings.LightsBrightness = value;
                Save();
            });
            var night = Ui.Switch(nightOn, on =>
            {
                Settings.LightsNightMode = on;
                Save();
                ShowLightingChange();
            });
            night.HorizontalAlignment = HorizontalAlignment.Left;
            var rig = Ui.Button("Open Rig", PanelButtonKind.Outline, PanelButtonSize.Small);
            rig.HorizontalAlignment = HorizontalAlignment.Left;
            rig.Click += (sender, args) => Go(PanelPage.Rig);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            var cells = new[]
            {
                Ui.VStack(12, Ui.Eyebrow(label), slider),
                Ui.VStack(12, Ui.Eyebrow(PanelSettings.NightModeTitle), night),
                Ui.VStack(12, Ui.Eyebrow("Flags and spotter"), rig),
            };
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = new Border
                {
                    Padding = new Thickness(18, 16, 18, 16),
                    BorderBrush = Ui.Brush(Theme.Rule),
                    BorderThickness = new Thickness(i == 0 ? 0 : PanelMetrics.BorderWeight, 0, 0, 0),
                    Child = cells[i],
                };
                Grid.SetColumn(cell, i);
                grid.Children.Add(cell);
            }
            return PageSection(PanelHome.QuickControlsTitle, Ui.CardBox(grid, 0));
        }
    }
}
