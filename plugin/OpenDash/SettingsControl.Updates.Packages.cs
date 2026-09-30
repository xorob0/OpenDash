// SettingsControl.Updates.Packages.cs: the Updates page's "In SimHub" section -- the table of what OpenDash has
// put in SimHub and at which version, its dashboards' rows, its notes and Reinstall everything.
//
// One row per dashboard on the rig, named as SimHub lists it, then one per strip and the flag box
// (SettingsControl.Updates.Lights.cs). What a row says is PanelUpdates.DashboardRow, StripRow and FlagBoxRow;
// this file is the drawing.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Shapes;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <param name="keptAnchorHere">Whether Reinstall everything carries AnchorKept too: while no kept card
        /// is drawn, so search's "Put mine back" lands on the press whose question says what it is rather
        /// than on nothing.</param>
        private FrameworkElement BuildInSimHubSection(double width, bool keptAnchorHere)
        {
            var versionWidth = PanelUpdates.VersionWidth(width);
            var rows = new StackPanel { Orientation = Orientation.Vertical };
            // Every row is a grid of its own, and the press column is as wide in all of them as in the widest,
            // so a row with an Update press keeps its version and state under the head's.
            Grid.SetIsSharedSizeScope(rows, true);
            rows.Children.Add(UpdatesTableHead(versionWidth));
            var rowFailed = false;
            foreach (var pair in UpdatesDashboardRows())
            {
                Action<UpdatesRow> paint;
                rows.Children.Add(UpdatesTableRow(pair.Value, versionWidth, null, out paint));
                rowFailed |= pair.Value.State == PanelCopy.InstallFailed;
            }
            bool stripsReachable;
            FlagBoxPlan flagBoxPlan;
            var lights = BuildLightRows(versionWidth, out stripsReachable, out flagBoxPlan);
            foreach (var row in lights) rows.Children.Add(row);
            if (rows.Children.Count == 1) rows.Children.Add(UpdatesTableEmpty());
            var table = Ui.CardBox(rows, 0);
            // Where the strip-outdated fix lands (PanelLeds.StripUpdateRoute): the first light row, whose
            // Update press is the fix, or the table when the rig has none.
            if (lights.Count > 0) Ui.Anchor(lights[0], PanelUpdates.AnchorLights);
            else Ui.Anchor(table, PanelUpdates.AnchorLights);

            var children = new List<UIElement> { table };
            var hasStrips = Settings.LedBarList().Any(bar => bar != null && bar.ProfileShapeId != null);
            var notes = PanelUpdates.TableNotes(plugin.Installer.HasEmbeddedPackage, plugin.Installer.LastError, rowFailed,
                lights.Count > 0, hasStrips, !hasStrips || EmbeddedShapeIds().Count > 0, stripsReachable);
            foreach (var note in notes) children.Add(UpdatesNote(note));
            // The by-hand route for the flag box, only when the matrix driver cannot be reached at all.
            if (flagBoxPlan != null && flagBoxPlan.State == FlagBoxInstallState.Unavailable)
            {
                var fallback = BuildFlagBoxImportFallback(flagBoxPlan);
                fallback.Margin = new Thickness(0, PanelUpdates.SectionGap, 0, 0);
                children.Add(fallback);
            }
            var reinstall = Ui.Anchor(UpdatesReinstallRow(), PanelUpdates.AnchorReinstall);
            children.Add(keptAnchorHere ? Ui.Anchor(new Border { Child = reinstall }, PanelUpdates.AnchorKept) : reinstall);
            return PageSection(PanelUpdates.InSimHubTitle, false, PanelUpdates.SectionGap, children.ToArray());
        }

        /// <summary>A sentence under the table, 12 below what is above it.</summary>
        private static FrameworkElement UpdatesNote(string text)
        {
            var note = Ui.Caption(text, BodyWidth);
            note.Margin = new Thickness(0, PanelUpdates.SectionGap, 0, 0);
            return note;
        }

        /// <summary>Every screen on the rig with its row: the installer's status for the screen's folder, and
        /// whether SimHub has it and has loaded it (the shell's facts, from the last Go).</summary>
        private IList<KeyValuePair<ScreenInstance, UpdatesRow>> UpdatesDashboardRows()
        {
            var rows = new List<KeyValuePair<ScreenInstance, UpdatesRow>>();
            var packages = plugin.Installer.Packages.Where(p => !p.OutsideRig).ToList();
            foreach (var screen in Settings.RigScreens())
            {
                if (screen == null) continue;
                var package = packages.FirstOrDefault(p => string.Equals(p.FolderName, screen.Folder, StringComparison.OrdinalIgnoreCase));
                var facts = ScreenFacts(screen.Namespace);
                var row = PanelUpdates.DashboardRow(screen, package, facts == null ? null : facts.Installed, facts == null ? null : facts.AddedSinceStart);
                rows.Add(new KeyValuePair<ScreenInstance, UpdatesRow>(screen, row));
            }
            return rows;
        }

        /// <summary>The columns every row of the table shares: the name takes what is left.</summary>
        private static Grid UpdatesTableGrid(double versionWidth)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            // Each fixed column is its content's width and the 16 before it, which each cell's left margin
            // takes (UpdatesPlace), so the version sits at 110 and the state at 150 as the artboard's do.
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelUpdates.ColumnWidth(versionWidth)) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelUpdates.ColumnWidth(PanelUpdates.TableStateWidth)) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "UpdatesPress" });
            return grid;
        }

        private static void UpdatesPlace(Grid grid, FrameworkElement element, int column, double versionWidth)
        {
            // The gap sits before each column but the first; a hidden version column takes no gap either.
            // The press column carries its gap on the press itself, so a row without one takes no room.
            var gap = column == 0 || column == 3 || (column == 1 && versionWidth <= 0) ? 0 : PanelUpdates.TableGap;
            element.Margin = new Thickness(gap, 0, 0, 0);
            element.VerticalAlignment = VerticalAlignment.Center;
            if (column == 1 && versionWidth <= 0) element.Visibility = Visibility.Collapsed;
            Grid.SetColumn(element, column);
            grid.Children.Add(element);
        }

        /// <summary>The artboard's head row: the three column names as .lbl, no rule over it, 12 on top.</summary>
        private static FrameworkElement UpdatesTableHead(double versionWidth)
        {
            var grid = UpdatesTableGrid(versionWidth);
            UpdatesPlace(grid, Ui.Eyebrow(PanelUpdates.ItemColumn), 0, versionWidth);
            UpdatesPlace(grid, Ui.Eyebrow(PanelUpdates.VersionColumn), 1, versionWidth);
            UpdatesPlace(grid, Ui.Eyebrow(PanelUpdates.StateColumn), 2, versionWidth);
            return new Border
            {
                Padding = new Thickness(PanelUpdates.TableRowPaddingX, PanelUpdates.TableHeadPaddingTop, PanelUpdates.TableRowPaddingX, PanelUpdates.TableRowPaddingY),
                Child = grid,
            };
        }

        /// <summary>What the table says when the rig has nothing OpenDash puts in SimHub.</summary>
        private static FrameworkElement UpdatesTableEmpty()
        {
            var text = Ui.Caption(PanelUpdates.NothingInSimHub, BodyWidth);
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(PanelUpdates.TableRowPaddingX, PanelUpdates.TableRowPaddingY, PanelUpdates.TableRowPaddingX, PanelUpdates.TableRowPaddingY),
                Child = text,
            };
        }

        /// <summary>
        /// One row of the table: the name with its kind after it, the version, the state beside its dot, and
        /// the row's press when it has one. <paramref name="paint"/> redraws the row from a newer answer.
        /// </summary>
        private static Border UpdatesTableRow(UpdatesRow row, double versionWidth, Border actionHost, out Action<UpdatesRow> paint)
        {
            var grid = UpdatesTableGrid(versionWidth);

            var name = Ui.Text(row.Name, PanelUpdates.TableTextSize, FontWeights.Normal, Theme.TextPrimary);
            name.TextWrapping = TextWrapping.Wrap;
            var kind = new Run(PanelUpdates.KindCaption(row.Kind))
            {
                FontSize = PanelUpdates.TableKindSize,
                Foreground = Ui.Brush(Theme.TextSecondary),
            };
            name.Inlines.Add(kind);
            UpdatesPlace(grid, name, 0, versionWidth);

            var version = Ui.Text(row.Version, PanelUpdates.TableVersionSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
            UpdatesPlace(grid, version, 1, versionWidth);

            var dot = new Ellipse
            {
                Width = PanelUpdates.TableDot,
                Height = PanelUpdates.TableDot,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, PanelUpdates.TableDotGap, 0),
            };
            var state = Ui.Text(row.State, PanelUpdates.TableStateSize, FontWeights.Normal, row.StateHex);
            state.TextWrapping = TextWrapping.Wrap;
            state.VerticalAlignment = VerticalAlignment.Center;
            // A grid rather than a stack, so the words have the cell's width left after the dot and a state too
            // long for it wraps instead of being clipped.
            var cell = new Grid();
            cell.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(dot, 0);
            Grid.SetColumn(state, 1);
            cell.Children.Add(dot);
            cell.Children.Add(state);
            UpdatesPlace(grid, cell, 2, versionWidth);

            if (actionHost != null) UpdatesPlace(grid, actionHost, 3, versionWidth);

            var border = new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(PanelUpdates.TableRowPaddingX, PanelUpdates.TableRowPaddingY, PanelUpdates.TableRowPaddingX, PanelUpdates.TableRowPaddingY),
                Child = grid,
            };
            paint = current =>
            {
                version.Text = current.Version;
                state.Text = current.State;
                state.Foreground = Ui.Brush(current.StateHex);
                dot.Fill = Ui.Brush(current.DotHex);
                border.ToolTip = current.Tooltip;
            };
            paint(row);
            return border;
        }

        /// <summary>Reinstall everything, with its line beside it for the question it asks before replacing
        /// a dashboard somebody has edited.</summary>
        private FrameworkElement UpdatesReinstallRow()
        {
            updatesReinstallLine = Ui.Caption(string.Empty, BodyWidth);
            updatesReinstallLine.Visibility = Visibility.Collapsed;
            updatesReinstallLine.VerticalAlignment = VerticalAlignment.Center;
            updatesReinstall = Ui.Button(null, PanelButtonKind.Outline);
            updatesReinstall.MinWidth = ButtonMinWidth;
            updatesReinstall.ToolTip = PanelUpdates.ReinstallTooltip;
            updatesReinstall.SetBinding(ContentControl.ContentProperty, UpdatesLabelFrom(updatesReinstallLine, ReplacingAction.Reinstall));
            updatesReinstall.Click += (sender, args) => Reinstall();
            updatesReinstall.IsEnabled = !applying;
            updatesReinstall.VerticalAlignment = VerticalAlignment.Top;

            var grid = new Grid { Margin = new Thickness(0, PanelUpdates.SectionGap, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            updatesReinstallLine.Margin = new Thickness(PanelUpdates.ReinstallLineGap, 0, 0, 0);
            Grid.SetColumn(updatesReinstall, 0);
            Grid.SetColumn(updatesReinstallLine, 1);
            grid.Children.Add(updatesReinstall);
            grid.Children.Add(updatesReinstallLine);
            return grid;
        }
    }
}
