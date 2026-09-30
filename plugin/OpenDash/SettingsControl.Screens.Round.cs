// SettingsControl.Screens.Round.cs: a round screen's editor on the Screens page (Screens.dc.html) -- a picture
// of the disc with the cards it carries, a choice per card the package reads, the rig-wide rev ring, and the
// line naming a card shown twice among the ones it reads.
//
// The cards are the rig's twelve shared slots (Slot01 to Slot12), not this screen's: the 480 round reads the
// first two and the 800 round the first six, so every round screen shows the same cards. Its rev ring is the
// one row that writes the rig-wide rev bar, through its setter. The cards leave for zones on a ring in #146.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private FrameworkElement BuildSlotsPane(ScreenInstance screen)
        {
            var host = new ContentControl { Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            Action redraw = null;
            redraw = () => ScreensRedraw(host, () => BuildRoundEditor(screen, redraw));
            redraw();
            return Ui.Anchor(host, PanelScreens.AnchorSlots);
        }

        private FrameworkElement BuildRoundEditor(ScreenInstance screen, Action redraw)
        {
            var cards = Cards.All.Select(card => card.DisplayName).ToArray();
            var read = PanelScreens.CardsRead(screen);
            var rows = new List<UIElement>();
            for (var slot = 1; slot <= read; slot++)
            {
                var captured = slot;
                var choice = Ui.ChoiceButton(cards, Settings.Slot(captured), index =>
                {
                    Settings.SetSlot(captured, index);
                    ScreensSave(screen, redraw);
                });
                choice.Uid = "screens.card." + captured;
                rows.Add(Ui.SettingRow(PanelScreens.CardLabel(captured), choice));
            }
            var duplicates = PanelScreens.CardClash(Settings.Slots, read);
            if (duplicates.Length > 0) rows.Add(BuildScreensWarning(duplicates));
            rows.Add(Ui.Anchor(BuildRevRingRow(screen), PanelScreens.AnchorRevRing));
            rows.Add(Ui.SoonRow(PanelSoon.ZonesInsteadOfCards));
            var caption = Ui.Prose(PanelScreens.CardsCaption);
            caption.Margin = new Thickness(0, 10, 0, 0);
            rows.Add(caption);
            var list = Ui.Rows(rows.ToArray());
            // The first row sits on the picture's top edge, with no rule over it.
            var head = list.Children.Count > 0 ? list.Children[0] as Border : null;
            if (head != null) head.BorderThickness = new Thickness(0);

            // A rectangular card face a migration left is not a disc: its rows alone.
            if (!PanelScreens.DrawsDisc(screen)) return list;
            var picture = BuildRoundPicture(read);
            if (!TwoColumns) return Ui.VStack(16, picture, list);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelRoundPlan.PictureSize) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PanelRoundPlan.PictureGap) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            picture.VerticalAlignment = VerticalAlignment.Top;
            grid.Children.Add(picture);
            Grid.SetColumn(list, 2);
            grid.Children.Add(list);
            return grid;
        }

        /// <summary>
        /// The rig-wide rev ring every round screen draws, which is the rig's `RevBar` and not a screen's own.
        /// </summary>
        /// <remarks>
        /// A card face owns no properties at all -- it reads the twelve shared slots and nothing else -- so its
        /// arc reads the rig-wide name, and this row is where that name is still written from. The caption
        /// says how much further it reaches.
        /// </remarks>
        private FrameworkElement BuildRevRingRow(ScreenInstance screen)
        {
            var control = ScreensSegmented(PanelDataTab.RevBarValues, PanelDataTab.RevBarLabels, Settings.RevBarMode(), value =>
            {
                Settings.SetRevBar(value);
                ScreensSave(screen);
            });
            return Ui.SettingRow(caption: PanelScreens.RigRevBarCaption, title: PanelScreens.RevRingTitleFor(screen), control: control);
        }

        /// <summary>The disc, with the cards it carries in the order the package reads them: stacked for two,
        /// two columns of three for six.</summary>
        private FrameworkElement BuildRoundPicture(int read)
        {
            var disc = new Ellipse
            {
                Width = PanelRoundPlan.PictureSize,
                Height = PanelRoundPlan.PictureSize,
                Fill = Ui.Brush(Theme.SurfaceInset),
                Stroke = Ui.Brush(Theme.Rule),
                StrokeThickness = PanelMetrics.BorderWeight,
            };
            var columns = PanelRoundPlan.Columns(read);
            var cellWidth = PanelRoundPlan.CardWidth(columns);
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var rows = (read + columns - 1) / columns;
            for (var r = 0; r < rows; r++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var i = 0; i < read; i++)
            {
                var slot = i + 1;
                var label = Ui.Eyebrow(PanelScreens.CardLabel(slot));
                label.HorizontalAlignment = HorizontalAlignment.Center;
                var name = ScreensCellText(Cards.All[Math.Max(0, Math.Min(Cards.All.Count - 1, Settings.Slot(slot)))].DisplayName, Theme.SizeBody, FontWeights.SemiBold, Theme.TextPrimary);
                name.HorizontalAlignment = HorizontalAlignment.Center;
                name.Margin = new Thickness(0, PanelRoundPlan.CardLineGap, 0, 0);
                var cell = new Border
                {
                    Width = cellWidth,
                    Height = PanelRoundPlan.CardHeight,
                    Background = Ui.Brush(Theme.SurfaceZone),
                    BorderBrush = Ui.Brush(Theme.Rule),
                    BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                    CornerRadius = new CornerRadius(Theme.Radius),
                    Padding = new Thickness(PanelRoundPlan.CardPaddingX, PanelRoundPlan.CardPaddingY, PanelRoundPlan.CardPaddingX, PanelRoundPlan.CardPaddingY),
                    Margin = new Thickness(i % columns == 0 ? 0 : PanelRoundPlan.CardGap, i < columns ? 0 : PanelRoundPlan.CardGap, 0, 0),
                    Child = Ui.VStack(0, label, name),
                };
                Grid.SetColumn(cell, i % columns);
                Grid.SetRow(cell, i / columns);
                grid.Children.Add(cell);
            }
            var picture = new Grid { Width = PanelRoundPlan.PictureSize, Height = PanelRoundPlan.PictureSize, HorizontalAlignment = HorizontalAlignment.Left };
            picture.Children.Add(disc);
            picture.Children.Add(grid);
            return picture;
        }
    }
}
