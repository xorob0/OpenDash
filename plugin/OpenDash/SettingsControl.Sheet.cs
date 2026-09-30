// SettingsControl.Sheet.cs: the sheet -- a panel that opens beside the cards for adding, editing or
// removing one thing, over a dim laid across the main column.
//
// Add, Edit, Remove and Duplicate used to replace the whole tab with their form, so the driver lost sight of
// the cards they were acting on. A sheet keeps them in view: 560 wide at the right edge of the main column, or the
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
            // The dim closes the sheet on the release that ends a press on it, and holds the mouse between
            // the two. It closed on the press, so the release went to whatever the collapsed layer had
            // covered, and a segmented bar under the dim took it as a choice and saved it.
            dim.MouseLeftButtonDown += (sender, args) =>
            {
                args.Handled = true;
                dim.CaptureMouse();
            };
            // While the dim holds the mouse every release comes to it, so it closes only on a release over
            // itself: a press that landed on the dim by accident and was dragged back onto the sheet keeps
            // the sheet and what was typed in it, as a segmented option keeps its choice.
            dim.MouseLeftButtonUp += (sender, args) =>
            {
                if (!dim.IsMouseCaptured) return;
                args.Handled = true;
                dim.ReleaseMouseCapture();
                // The sheet lies over the dim, and the dim holds the mouse, so over the sheet is asked of the
                // sheet by position: under capture only the dim is ever the mouse's.
                if (!Within(args.GetPosition(dim), dim) || Within(args.GetPosition(sheetPanel), sheetPanel)) return;
                CloseSheet();
            };
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

        /// <summary>Whether a point, taken relative to an element, falls inside it.</summary>
        private static bool Within(Point at, FrameworkElement element)
        {
            return at.X >= 0 && at.Y >= 0 && at.X <= element.ActualWidth && at.Y <= element.ActualHeight;
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

            var bodyHost = new Border { Padding = new Thickness(PanelShell.SheetPaddingX, 0, PanelShell.SheetPaddingX, PanelShell.SheetBodyPaddingBottom), Child = body };
            // Not a tab stop of its own: WPF's ScrollViewer is focusable by default, which put an invisible
            // stop with the default focus visual before the body's first control.
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false,
                Content = bodyHost,
            };

            // Three rows in reading order -- the title, the body, the footer -- so Tab goes the way the eye
            // does. A DockPanel had to take the footer before the body for the body to fill, which put Cancel
            // and the primary press before the first field, and Tab then Space from Close could add a screen
            // with its default answers.
            var sheet = new Grid();
            sheet.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sheet.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            sheet.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(head, 0);
            sheet.Children.Add(head);
            Grid.SetRow(scroll, 1);
            sheet.Children.Add(scroll);
            Border foot = null;
            if (footer != null)
            {
                foot = new Border
                {
                    Background = Ui.Brush(Theme.SurfaceInset),
                    BorderBrush = Ui.Brush(Theme.Rule),
                    BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                    Padding = new Thickness(PanelShell.SheetPaddingX, PanelShell.SheetFooterPaddingTop, PanelShell.SheetPaddingX, PanelShell.SheetFooterPaddingBottom),
                    Child = footer,
                };
                Grid.SetRow(foot, 2);
                sheet.Children.Add(foot);
            }

            sheetPanel.Child = sheet;
            SizeSheet();
            sheetLayer.Visibility = Visibility.Visible;
            // Focus opens on the body's first control, the first thing the sheet asks; a sheet that asks
            // nothing opens on its footer's first press, and only one with neither on Close.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (bodyHost.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)) && sheetPanel.IsKeyboardFocusWithin) return;
                if (foot != null && foot.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)) && sheetPanel.IsKeyboardFocusWithin) return;
                sheetPanel.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }));
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

        /// <summary>Sizes the sheet and lays its right edge on the main column's, so on a wide window it opens
        /// beside the cards rather than at the far edge of the control.</summary>
        private void SizeSheet()
        {
            sheetPanel.Width = PanelShell.SheetWidth(controlWidth);
            sheetPanel.Margin = new Thickness(0, 0, PanelShell.SheetRightGap(controlWidth, WidePage(route.Page)), 0);
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
