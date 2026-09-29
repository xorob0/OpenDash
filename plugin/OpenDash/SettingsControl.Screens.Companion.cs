// SettingsControl.Screens.Companion.cs: what a companion shows under its card on the Screens page -- its
// rotation of modules, its flag display, and the two moments OpenDash still chooses the module.
//
// Re-hosted from the old Rig tab by the #503 foundation; the Screens page agent owns it. The glance's
// binding moved to Shortcuts, and the paging caption stays here, beside the rotation it pages.
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

        private FrameworkElement BuildCompanionPane(ScreenInstance screen)
        {
            var columns = PanelCompanionPlan.ModuleColumns;
            var rows = (Modules.Count + columns - 1) / columns;
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var i = 0; i < Modules.Count; i++)
            {
                var module = Modules.All[i];
                var cell = BuildModuleRow(screen, module);
                Grid.SetColumn(cell, i / rows);
                Grid.SetRow(cell, i % rows);
                grid.Children.Add(cell);
            }
            var intro = Ui.Caption(
                "Turn off the ones you never use. Energy, Damage and Track rivals need a sim other "
                + "than iRacing.",
                BodyWidth);
            intro.Margin = new Thickness(0, 0, 0, 12);
            return Ui.VStack(0,
                Ui.Anchor(PageSection("Modules", intro, grid), PanelScreens.AnchorModules),
                BuildCompanionFlagRow(screen),
                BuildCompanionPaging(screen));
        }

        /// <summary>One module of the catalogue, numbered as the panel numbers them.</summary>
        private ComboBox BuildModuleSelect(ScreenInstance screen, int selected, string tooltip, Action<int> chosen)
        {
            var select = new ComboBox
            {
                Width = PanelPitWallPlan.SelectWidth,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = tooltip,
            };
            Ui.Field(select, Theme.ControlHeightSm);
            foreach (var module in Modules.All) select.Items.Add(module.Number.ToString("00") + " \u00b7 " + module.Name);
            select.SelectedIndex = selected >= 0 && selected < Modules.Count ? selected : 0;
            select.SelectionChanged += (sender, args) =>
            {
                if (select.SelectedIndex < 0 || select.SelectedIndex >= Modules.Count) return;
                chosen(select.SelectedIndex);
                Save(screen);
            };
            return select;
        }

        /// <summary>
        /// How a companion is paged, which is SimHub's, and the two moments OpenDash still chooses the module.
        /// </summary>
        /// <remarks>
        /// There used to be a binder for OpenDash's own next-module action here. It needed OpenDash to be
        /// the thing choosing which screen was up, and that is exactly what stopped a tap working:
        /// SimHub's only touch gesture maps a tap to the previous or next screen, and its navigation
        /// walks the screens whose expression is true, so with one of twenty-one enabled there was
        /// nothing to walk. It is replaced by the sentence saying where the controls went:
        /// PanelCopy.CompanionPaging, which names the device's Controls and events, NextScreen and
        /// PreviousScreen, and that the binding belongs to the device.
        ///
        /// What OpenDash still chooses, it chooses for a moment: the First module, the one a session
        /// opens on, and the Quick glance, held on a button and let go on release (#362). Both force one
        /// screen enabled so SimHub selects it, and then hand the paging back.
        ///
        /// It is prose and not a picture (#435). A drawn diagram in the panel's own hand would be a
        /// drawing of SimHub's dialog, which goes stale at SimHub's next release as surely as a
        /// photograph does and cannot be checked from here; a bitmap is machinery the panel does not
        /// carry, and ADR 0020 (#398) is where that would be decided. The photograph of the real dialog
        /// belongs on the site's install page, taken with the other captures in the #430 pass, whose
        /// ticket carries it as a comment.
        /// </remarks>
        private FrameworkElement BuildCompanionPaging(ScreenInstance screen)
        {
            var paging = Ui.Caption(PanelCopy.CompanionPaging, BodyWidth);
            paging.Margin = new Thickness(0, 0, 0, 12);
            // The glance's binding is on Shortcuts, with every other one; the module it shows is set here.
            var chip = BindingChipFor(Contract.HoldQuickGlanceActionFor(screen.Namespace));
            var section = PageSection("Module paging",
                paging,
                Ui.Anchor(Ui.Row("First module", "Shown when a session starts.", BuildModuleSelect(screen, Settings.ScreenCompanionStart(screen.Namespace), "The module a session starts on", value =>
                {
                    screen.CompanionStart = value;
                    // And force it now, so the screen in front of you moves rather than waiting for the
                    // next SimHub start. Somebody choosing where it opens is looking at the thing. The
                    // force lets go by itself after the same window Init's does, so the taps come back.
                    screen.OpenOnStartModule(DateTime.UtcNow);
                })), PanelScreens.AnchorFirstModule),
                // Any module, the ones the rotation has off included: a glance is asked for by holding a
                // button, and the rotation is about what a tap steps through.
                Ui.Row("Quick glance", null, Ui.HStack(8,
                    BuildModuleSelect(screen, Settings.ScreenCompanionQuickGlance(screen.Namespace), "The module a held button shows", value =>
                    {
                        screen.CompanionQuickGlance = value;
                    }),
                    chip)));
            section.Margin = new Thickness(0, 24, 0, 0);
            return section;
        }

        /// <summary>How this companion draws a flag. Full screen by default, which is what a phone on a
        /// stand beside the wheel is for: a 12 px strip at that distance says nothing.</summary>
        private FrameworkElement BuildCompanionFlagRow(ScreenInstance screen)
        {
            var segmented = BuildSegmented(
                Contract.CompanionFlagFormats,
                new[] { "Off", "Bar", "Full screen" },
                Settings.ScreenCompanionFlagFormat(screen.Namespace),
                value => { screen.CompanionFlagFormat = Contract.NormaliseCompanionFlagFormat(value); Save(screen); });
            return Ui.Row("Flag display", "Full screen covers the module. Bar is a thin strip at the foot.", segmented);
        }

        /// <summary>How this pit wall draws a flag. The bar by default, not the companion's full screen:
        /// a wall is watched *because* of the flag, and covering the board at the moment a yellow comes
        /// out hides the cars the yellow is about.</summary>
        private FrameworkElement BuildPitWallFlagRow(ScreenInstance screen)
        {
            var segmented = BuildSegmented(
                Contract.CompanionFlagFormats,
                new[] { "Off", "Bar", "Full screen" },
                Settings.ScreenPitWallFlagFormat(screen.Namespace),
                value => { screen.PitWallFlagFormat = Contract.NormalisePitWallFlagFormat(value); Save(screen); });
            return Ui.Row("Flag display", "Bar is a strip under the header. Full screen covers the rest.", segmented);
        }

        /// <summary>"Tyres" and its toggle. The number and the description are the tooltip: the grid reads
        /// as a column of names, and the header's "n / 21" can still be matched to a row.</summary>
        private FrameworkElement BuildModuleRow(ScreenInstance screen, Module module)
        {
            var name = Ui.Body(module.Name);
            var index = module.Number - 1;
            var toggle = BuildToggle(screen.Modules != null && index < screen.Modules.Length && screen.Modules[index], on =>
            {
                if (screen.Modules == null || index >= screen.Modules.Length) return;
                screen.Modules[index] = on;
                Save(screen);
            });
            toggle.HorizontalAlignment = HorizontalAlignment.Right;
            var row = Ui.Row(name, toggle);
            row.Margin = new Thickness(0, 0, PanelCompanionPlan.ModuleColumnGap, PanelCompanionPlan.ModuleRowGap);
            row.ToolTip = module.Number.ToString("00") + " · " + module.Description;
            return row;
        }
    }
}
