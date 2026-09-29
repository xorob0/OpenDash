// SettingsControl.Shortcuts.cs: the Shortcuts page -- every wheel button and key in one list, per screen and
// for the rig.
//
// Each binding is SimHub's own ControlsEditor (BuildBinder), so a button is bound here rather than by sending
// the driver to Controls and events to find an action's name. The #503 foundation gathers what was spread
// over the old Rig tab's panes: a face's zone buttons and the new "previous page" twins, the three quick
// glances (face, pit wall, companion), and the rig's own night mode and brightness. The Shortcuts page agent
// owns this file and rebuilds it to Shortcuts.dc.html.
//
// Exactly one hold binder per kind of screen, each captioned with its glance sentence: PanelCopyTests counts
// the call sites, and the caption is what says the binding is corrected to a hold (#435).
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private FrameworkElement BuildShortcutsPage(PanelRoute to)
        {
            var intro = Ui.Caption("A wheel button, a button box or a key. Each screen has its own.", BodyWidth);
            var screens = new StackPanel { Orientation = Orientation.Vertical };
            foreach (var screen in Settings.RigScreens())
            {
                var group = BuildScreenShortcuts(screen);
                if (group == null) continue;
                group.Margin = new Thickness(0, screens.Children.Count == 0 ? 0 : PanelShell.SectionGap, 0, 0);
                screens.Children.Add(group);
            }
            var sections = new List<UIElement> { intro };
            if (screens.Children.Count > 0) sections.Add(Ui.Anchor(screens, PanelShortcuts.AnchorScreens));
            sections.Add(Ui.Anchor(BuildRigShortcuts(), PanelShortcuts.AnchorRig));
            foreach (var item in PanelSoon.For(PanelPage.Shortcuts)) sections.Add(Ui.SoonRow(item, Ui.BindingChip(Ui.NotBound, false)));
            return PageLayout(PanelShortcuts.Title, null, sections.ToArray());
        }

        /// <summary>One screen's bindings, or null for a kind that has none.</summary>
        private FrameworkElement BuildScreenShortcuts(ScreenInstance screen)
        {
            if (screen.IsCompanion) return PageSection(screen.Name, BuildCompanionShortcuts(screen));
            if (screen.IsPitWall) return PageSection(screen.Name, BuildPitWallShortcuts(screen));
            if (screen.IsFace && screen.FaceSize != null) return PageSection(screen.Name, BuildFaceShortcuts(screen, screen.FaceSize.Value));
            return null;
        }

        /// <summary>
        /// A face's buttons: each zone's next and previous page, in the order its picture draws the zones, and
        /// the quick glance held on a button.
        /// </summary>
        /// <remarks>
        /// Per screen, which is what lets a second face stay still while the one in front of the driver
        /// cycles (ADR 0017). PanelFacePlan.ZoneOrder is the picture's order; Contract.FaceZoneLetters keeps
        /// its own, because it indexes the settings.
        /// </remarks>
        private FrameworkElement BuildFaceShortcuts(ScreenInstance screen, Contract.FaceSize face)
        {
            var rows = new List<UIElement>();
            foreach (var letter in PanelFacePlan.ZoneOrder(face))
            {
                var zone = PanelFacePlan.ZoneLabel(letter);
                var next = Contract.CycleZoneAction(screen.Namespace, letter);
                var back = Contract.CycleZoneBackAction(screen.Namespace, letter);
                rows.Add(Ui.Anchor(Ui.Row(zone + " · next page", null,
                    BuildBinder(next, screen.Name + " · zone " + letter)), PanelBindings.Anchor(next)));
                rows.Add(Ui.Anchor(Ui.SettingRow(zone + " · previous page",
                    BuildBinder(back, screen.Name + " · zone " + letter + " back"),
                    null, Ui.NewTag()), PanelBindings.Anchor(back)));
            }
            var glanceAction = Contract.HoldQuickGlanceActionFor(screen.Namespace);
            var glance = Ui.Anchor(Ui.Row("Quick glance", null,
                BuildBinder(glanceAction, screen.Name + " · quick glance", hold: true)), PanelBindings.Anchor(glanceAction));
            rows.Add(glance);
            var caption = Ui.Caption(PanelCopy.FaceGlance);
            caption.Margin = new Thickness(0, 0, 0, 8);
            rows.Add(caption);
            return Ui.VStack(0, rows.ToArray());
        }

        /// <summary>A pit wall's one binding: the glance. A key beside the monitor is the likelier gesture,
        /// since nobody drives a pit wall.</summary>
        private FrameworkElement BuildPitWallShortcuts(ScreenInstance screen)
        {
            var glanceAction = Contract.HoldQuickGlanceActionFor(screen.Namespace);
            var glance = Ui.Anchor(Ui.Row("Quick glance", "A keyboard key works too.",
                BuildBinder(glanceAction, screen.Name + " · quick glance", hold: true)), PanelBindings.Anchor(glanceAction));
            var caption = Ui.Caption(PanelCopy.PitWallGlance);
            caption.Margin = new Thickness(0, 0, 0, 8);
            return Ui.VStack(0, glance, caption);
        }

        /// <summary>A companion's one binding, the glance: a tap pages it, and that belongs to SimHub
        /// (PanelCopy.CompanionPaging, on the companion's own pane).</summary>
        private FrameworkElement BuildCompanionShortcuts(ScreenInstance screen)
        {
            var glanceAction = Contract.HoldQuickGlanceActionFor(screen.Namespace);
            var glance = Ui.Anchor(Ui.Row("Quick glance", null,
                BuildBinder(glanceAction, screen.Name + " · quick glance", hold: true)), PanelBindings.Anchor(glanceAction));
            var caption = Ui.Caption(PanelCopy.CompanionGlance);
            caption.Margin = new Thickness(0, 0, 0, 8);
            return Ui.VStack(0, glance, caption);
        }

        /// <summary>The rig's own actions: night mode, and the brightness up and down (Contract.RigActionNames).</summary>
        private FrameworkElement BuildRigShortcuts()
        {
            var rows = new List<UIElement>();
            foreach (var action in Contract.RigActionNames())
            {
                var label = PanelShortcuts.RigActionLabel(action);
                var row = action == Contract.ToggleNightModeAction
                    ? Ui.Row(label, null, BuildBinder(action, label))
                    : Ui.SettingRow(label, BuildBinder(action, label), null, Ui.NewTag());
                rows.Add(Ui.Anchor(row, PanelBindings.Anchor(action)));
            }
            return PageSection(PanelShortcuts.RigGroupTitle, rows.ToArray());
        }
    }
}
