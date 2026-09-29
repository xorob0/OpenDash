// SettingsControl.Sheet.cs: the sheet -- a panel that opens beside the cards for adding, editing or
// removing one thing, over a dim laid across the main column.
//
// Add, Edit, Remove and Duplicate used to replace the whole tab with their form, so the driver lost sight of
// the cards they were acting on. A sheet keeps them in view: 560 wide on the right of the main column, or the
// whole column below 900 px, with its title and a close button at the top, its body scrolling on its own,
// and its footer -- usually Cancel and the press -- on the inset ground at the foot. Escape and a click on the
// dim close it; Go closes it, so a page's Cancel is a Redraw.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private readonly Grid sheetLayer = new Grid { Visibility = Visibility.Collapsed };
        private readonly Border sheetPanel = new Border();
        private Action sheetClosed;

        /// <summary>What had keyboard focus when the sheet opened, which gets it back when the sheet closes.</summary>
        private IInputElement sheetOpener;

        private bool SheetOpen => sheetLayer.Visibility == Visibility.Visible;

        private UIElement BuildSheetLayer()
        {
            // Both sheet artboards dim in the inset ground and edge the sheet in the border ink.
            var dim = new Border { Background = Ui.Tint(Theme.SurfaceInset, PanelShell.SheetDimOpacity), Cursor = Cursors.Arrow };
            dim.MouseLeftButtonDown += (sender, args) => CloseSheet();
            sheetPanel.HorizontalAlignment = HorizontalAlignment.Right;
            sheetPanel.Background = Ui.Brush(Theme.SurfaceBase);
            sheetPanel.BorderBrush = Ui.Brush(Theme.Border);
            // Tab and Ctrl+Tab go round the sheet and not out into the dimmed page, whose presses still work.
            KeyboardNavigation.SetTabNavigation(sheetPanel, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetControlTabNavigation(sheetPanel, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetDirectionalNavigation(sheetPanel, KeyboardNavigationMode.Contained);
            sheetPanel.BorderThickness = new Thickness(PanelMetrics.BorderWeight, 0, 0, 0);
            sheetLayer.Children.Add(dim);
            sheetLayer.Children.Add(sheetPanel);
            return sheetLayer;
        }

        /// <summary>
        /// Opens a sheet: its title, a body that scrolls, and a footer that stays. Opening another replaces it.
        /// </summary>
        /// <param name="closed">Told when the sheet closes, however it closes.</param>
        private void ShowSheet(string title, UIElement body, UIElement footer, Action closed = null)
        {
            // A sheet replacing another keeps the first one's opener: that is where the driver was.
            var opener = SheetOpen ? sheetOpener : Keyboard.FocusedElement;
            CloseSheet(restoreFocus: false);
            sheetClosed = closed;
            sheetOpener = opener;

            var close = Ui.IconButton(PanelIcons.Close, "Close", CloseSheet);
            var head = new DockPanel { LastChildFill = true, Margin = new Thickness(PanelShell.SheetPaddingX, PanelShell.SheetHeaderPaddingTop, PanelShell.SheetPaddingX - 8, PanelShell.SheetHeaderPaddingBottom) };
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close);
            var heading = Ui.Text(title, PanelShell.SheetTitleSize, FontWeights.SemiBold, Theme.TextPrimary);
            heading.TextTrimming = TextTrimming.CharacterEllipsis;
            head.Children.Add(heading);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new Border { Padding = new Thickness(PanelShell.SheetPaddingX, 0, PanelShell.SheetPaddingX, PanelShell.SheetBodyPaddingBottom), Child = body },
            };

            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(head, Dock.Top);
            dock.Children.Add(head);
            if (footer != null)
            {
                var foot = new Border
                {
                    Background = Ui.Brush(Theme.SurfaceInset),
                    BorderBrush = Ui.Brush(Theme.Rule),
                    BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                    Padding = new Thickness(PanelShell.SheetPaddingX, PanelShell.SheetFooterPaddingTop, PanelShell.SheetPaddingX, PanelShell.SheetFooterPaddingBottom),
                    Child = footer,
                };
                DockPanel.SetDock(foot, Dock.Bottom);
                dock.Children.Add(foot);
            }
            dock.Children.Add(scroll);

            sheetPanel.Child = dock;
            SizeSheet();
            sheetLayer.Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke(new Action(() => sheetPanel.MoveFocus(new TraversalRequest(FocusNavigationDirection.First))));
        }

        /// <summary>A sheet's footer as the artboards draw it: the presses on the right, 8 apart, with an
        /// optional line on the left saying what the press will do.</summary>
        private static FrameworkElement SheetFooter(string note, params UIElement[] presses)
        {
            var right = Ui.HStack(8, presses);
            right.HorizontalAlignment = HorizontalAlignment.Right;
            if (string.IsNullOrEmpty(note)) return right;
            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(right, Dock.Right);
            dock.Children.Add(right);
            var line = Ui.Prose(note);
            line.VerticalAlignment = VerticalAlignment.Center;
            line.Margin = new Thickness(0, 0, 16, 0);
            dock.Children.Add(line);
            return dock;
        }

        private void SizeSheet()
        {
            sheetPanel.Width = PanelShell.SheetWidth(controlWidth);
        }

        /// <summary>Closes the sheet, if one is open, and gives focus back to what opened it when that is
        /// still on screen.</summary>
        private void CloseSheet()
        {
            CloseSheet(restoreFocus: true);
        }

        private void CloseSheet(bool restoreFocus)
        {
            if (!SheetOpen && sheetPanel.Child == null) return;
            var hadFocus = sheetPanel.IsKeyboardFocusWithin;
            sheetLayer.Visibility = Visibility.Collapsed;
            sheetPanel.Child = null;
            var opener = sheetOpener as UIElement;
            sheetOpener = null;
            var closed = sheetClosed;
            sheetClosed = null;
            if (closed != null) closed();
            if (restoreFocus && hadFocus && opener != null && opener.IsVisible) Keyboard.Focus(opener);
        }
    }
}
