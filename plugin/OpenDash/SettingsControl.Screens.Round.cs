// SettingsControl.Screens.Round.cs: what a card face -- the round faces, and any slots package -- shows under
// its card on the Screens page: the slot picture, the rig-wide rev bar it reads, and the duplicate warning.
//
// Re-hosted from the old Rig tab by the #503 foundation; the Screens page agent owns it. It leaves with the
// cards in #146, and its rev bar row is the one that writes the rig-wide rev bar, through its setter.
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

        /// <summary>
        /// The twelve-slot picture, for anyone running a card package.
        /// </summary>
        /// <remarks>
        /// On its own screen card rather than in a section of its own, which is how the card model
        /// survives the redesign without cluttering it: it is on screen only if you installed one. It
        /// leaves with the cards in #146.
        ///
        /// The slots are the rig's and not this screen's, but the screen is carried down all the same:
        /// changing one here is still somebody setting up the card face whose pane this is.
        /// </remarks>
        private FrameworkElement BuildSlotsPane(ScreenInstance screen)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(BuildSlotGrid(screen, 1));
            row.Children.Add(BuildHeroCell());
            row.Children.Add(BuildSlotGrid(screen, 7));
            var picture = new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = row,
            };
            OnDrop(() =>
            {
                slotWarningText = null;
                slotWarningRow = null;
            });
            var intro = Ui.Caption("Any card in any slot. A face with fewer slots uses the first ones.", BodyWidth);
            intro.Margin = new Thickness(0, 0, 0, 12);
            picture.Margin = new Thickness(0, 0, 0, 12);
            var warning = BuildSlotWarning();
            warning.Margin = new Thickness(0, 12, 0, 0);
            return Ui.Anchor(Ui.VStack(0,
                PageSection(PanelScreens.CardsTitle, intro, picture),
                BuildSlotsRevBarRow(screen),
                warning), PanelScreens.AnchorSlots);
        }

        /// <summary>
        /// What a card face carries at the top, which is the rig-wide `RevBar` and not a screen's own.
        /// </summary>
        /// <remarks>
        /// A zone face answers this on its own pane, under its own property. A card face owns no
        /// properties at all -- it reads the twelve shared slots and nothing else, which is why two of
        /// them cannot be told apart -- so its arc reads the rig-wide name, and this row is where that
        /// name is still written from. It is a property of the model being retired rather than
        /// something this fixes, and it is here so that a driver with a round face does not lose the
        /// switch when the zone faces take their own.
        /// </remarks>
        private FrameworkElement BuildSlotsRevBarRow(ScreenInstance screen)
        {
            var control = BuildSegmented(PanelDataTab.RevBarValues, PanelDataTab.RevBarLabels, Settings.RevBarMode(), value =>
            {
                Settings.SetRevBar(value);
                Save(screen);
            });
            return Ui.Row(
                PanelDataTab.RevBarTitle,
                PanelScreens.RigRevBarCaption,
                control);
        }

        private FrameworkElement BuildSlotGrid(ScreenInstance screen, int firstSlot)
        {
            var grid = new Grid { Width = 337, Height = 165, Background = Ui.Brush(Theme.Rule) };
            for (var c = 0; c < 3; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var r = 0; r < 2; r++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < 6; i++)
            {
                var column = i % 3;
                var rowIndex = i / 3;
                var cell = BuildSlotCell(screen, firstSlot + i);
                cell.Margin = new Thickness(column > 0 ? 1 : 0, rowIndex > 0 ? 1 : 0, 0, 0);
                Grid.SetColumn(cell, column);
                Grid.SetRow(cell, rowIndex);
                grid.Children.Add(cell);
            }
            return grid;
        }

        private FrameworkElement BuildSlotCell(ScreenInstance screen, int slot)
        {
            var label = Ui.Label("Slot " + slot.ToString("00"));
            var picker = BuildSlotSelect(screen, slot);
            var dock = new DockPanel { LastChildFill = false };
            DockPanel.SetDock(label, Dock.Top);
            DockPanel.SetDock(picker, Dock.Bottom);
            dock.Children.Add(label);
            dock.Children.Add(picker);
            return new Border { Background = Ui.Brush(Theme.SurfaceBase), Padding = new Thickness(6), Child = dock };
        }

        /// <summary>Every card in card-number order, so that SelectedIndex is the card number.</summary>
        private ComboBox BuildSlotSelect(ScreenInstance screen, int slot)
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
            box.SelectionChanged += (sender, args) =>
            {
                if (box.SelectedIndex < 0) return;
                Settings.SetSlot((int)box.Tag, box.SelectedIndex);
                Save(screen);
                RefreshSlotWarning();
            };
            return box;
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

        private TextBlock slotWarningText;
        private FrameworkElement slotWarningRow;

        private FrameworkElement BuildSlotWarning()
        {
            slotWarningText = Ui.Text("", Theme.SizeSmall, FontWeights.Normal, Theme.Caution);
            slotWarningText.TextWrapping = TextWrapping.Wrap;
            var icon = Ui.Icon(Ui.WarningIcon, Theme.Caution);
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, 1, 0, 0);
            slotWarningRow = Ui.HStack(10, icon, slotWarningText);
            RefreshSlotWarning();
            return slotWarningRow;
        }

        private void RefreshSlotWarning()
        {
            if (slotWarningText == null || slotWarningRow == null) return;
            var message = DuplicateAssignment.Warning(Settings.Slots);
            slotWarningText.Text = message;
            slotWarningRow.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
