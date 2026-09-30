// SettingsControl.Screens.Companion.cs: a companion's editor on the Screens page (Screens.dc.html) -- its
// rotation of modules, the module a session opens on, its flag display, its quick glance, and how it is paged.
//
// The paging is SimHub's: a tap on either half of the screen, or a wheel button bound to NextScreen on the
// device the companion runs on (PanelCopy.CompanionPaging). What OpenDash still chooses, it chooses for a
// moment: the First module, forced as soon as it is picked, and the Quick glance, held on a button (#362).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private FrameworkElement BuildCompanionPane(ScreenInstance screen)
        {
            var count = Ui.Text(PanelScreens.ModuleCount(screen.Modules), 15, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
            count.VerticalAlignment = VerticalAlignment.Center;
            var head = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(count, Dock.Right);
            head.Children.Add(count);
            head.Children.Add(Ui.Heading(PanelScreens.ModulesTitle));

            var first = Ui.ChoiceButton(PanelScreens.ModuleNames(), Settings.ScreenCompanionStart(screen.Namespace), value =>
            {
                screen.CompanionStart = value;
                // And force it now, so the screen in front of you moves rather than waiting for the next
                // SimHub start. Somebody choosing where it opens is looking at the thing. The force lets go by
                // itself after the same window Init's does, so the taps come back.
                screen.OpenOnStartModule(DateTime.UtcNow);
                Save(screen);
            });
            var flags = ScreensSegmented(Contract.CompanionFlagFormats, PanelScreens.BarFlagLabels, Settings.ScreenCompanionFlagFormat(screen.Namespace),
                value => { screen.CompanionFlagFormat = Contract.NormaliseCompanionFlagFormat(value); Save(screen); });
            // Any module, the ones the rotation has off included: a glance is asked for by holding a button,
            // and the rotation is about what a tap steps through.
            var glance = Ui.ChoiceButton(PanelScreens.ModuleNames(), Settings.ScreenCompanionQuickGlance(screen.Namespace), value =>
            {
                screen.CompanionQuickGlance = value;
                Save(screen);
            });

            var rows = Ui.Rows(
                Ui.Anchor(Ui.SettingRow(PanelScreens.FirstModuleTitle, first), PanelScreens.AnchorFirstModule),
                Ui.Anchor(Ui.SettingRow(PanelScreens.FlagDisplayTitle, flags, null, Ui.NewTag()), PanelScreens.AnchorFlagDisplay),
                Ui.Anchor(Ui.SettingRow(PanelShortcuts.QuickGlanceTitle, ScreensWrap(glance, BindingChipFor(Contract.HoldQuickGlanceActionFor(screen.Namespace))), PanelCopy.CompanionGlance), PanelScreens.AnchorGlance),
                Ui.Anchor(BuildCompanionPaging(), PanelScreens.AnchorPaging));
            return Ui.VStack(16, Ui.Anchor(Ui.VStack(16, head, BuildModuleGrid(screen, count)), PanelScreens.AnchorModules), rows);
        }

        /// <summary>The twenty-one modules as ticks, three to a row where they fit, fewer where they do not;
        /// the count above follows every tick.</summary>
        private FrameworkElement BuildModuleGrid(ScreenInstance screen, TextBlock count)
        {
            var columns = PanelCompanionPlan.ColumnsFor(ContentWidth);
            var rows = (Modules.Count + columns - 1) / columns;
            var grid = new Grid();
            for (var c = 0; c < columns; c++)
            {
                if (c > 0) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelCompanionPlan.ModuleGap) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            for (var r = 0; r < rows; r++)
            {
                if (r > 0) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(PanelCompanionPlan.ModuleGap) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            // Across and then down, as the artboard reads: Lap times, Delta, Sectors on the first row.
            for (var i = 0; i < Modules.Count; i++)
            {
                var cell = BuildModuleCell(screen, Modules.All[i], count);
                Grid.SetColumn(cell, (i % columns) * 2);
                Grid.SetRow(cell, (i / columns) * 2);
                grid.Children.Add(cell);
            }
            return grid;
        }

        /// <summary>One module (.mod): its tick and its name on the base ground, and "Not in iRacing" beside the
        /// three iRacing publishes nothing for. The description is its hover.</summary>
        private FrameworkElement BuildModuleCell(ScreenInstance screen, Module module, TextBlock count)
        {
            var index = module.Number - 1;
            var box = new CheckBox
            {
                IsChecked = screen.Modules != null && index < screen.Modules.Length && screen.Modules[index],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
            };
            Action<bool> ticked = on =>
            {
                if (screen.Modules == null || index >= screen.Modules.Length) return;
                screen.Modules[index] = on;
                Save(screen);
                count.Text = PanelScreens.ModuleCount(screen.Modules);
            };
            box.Checked += (sender, args) => ticked(true);
            box.Unchecked += (sender, args) => ticked(false);
            var line = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(box, Dock.Left);
            line.Children.Add(box);
            if (PanelScreens.IsNotInIracing(module.Id))
            {
                var none = Ui.Text(PanelScreens.NotInIracing, 11, FontWeights.Normal, Theme.TextSecondary);
                none.VerticalAlignment = VerticalAlignment.Center;
                none.Margin = new Thickness(8, 0, 0, 0);
                DockPanel.SetDock(none, Dock.Right);
                line.Children.Add(none);
            }
            line.Children.Add(ScreensCellText(module.Name, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary));
            return new Border
            {
                Height = PanelCompanionPlan.ModuleHeight,
                Padding = new Thickness(PanelCompanionPlan.ModulePaddingX, 0, PanelCompanionPlan.ModulePaddingX, 0),
                Background = Ui.Brush(Theme.SurfaceBase),
                CornerRadius = new CornerRadius(Theme.Radius),
                Child = line,
                ToolTip = PanelScreens.ModuleTooltip(module),
            };
        }

        /// <summary>
        /// How a companion is paged, which is SimHub's: the sentence naming both ways, and the path to the
        /// binding on the companion's own device.
        /// </summary>
        /// <remarks>
        /// There used to be a binder for OpenDash's own next-module action here. It needed OpenDash to be the
        /// thing choosing which screen was up, and that is exactly what stopped a tap working: SimHub's only
        /// touch gesture maps a tap to the previous or next screen, and its navigation walks the screens whose
        /// expression is true, so with one of twenty-one enabled there was nothing to walk. It is prose and
        /// crumbs, not a picture (#435): a drawing of SimHub's dialog goes stale at SimHub's next release.
        /// </remarks>
        private FrameworkElement BuildCompanionPaging()
        {
            var paging = Ui.Caption(PanelCopy.CompanionPaging, BodyWidth);
            var crumbs = Ui.Crumbs(PanelScreens.CompanionPagingCrumbs);
            crumbs.Margin = new Thickness(0, 8, 0, 0);
            var body = Ui.VStack(0, paging, crumbs);
            body.Margin = new Thickness(0, 6, 0, 0);
            var row = Ui.SettingRow(PanelScreens.NextModuleTitle, null);
            return Ui.VStack(0, row, body);
        }
    }
}
