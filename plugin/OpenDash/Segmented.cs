// Segmented.cs: the two-to-four-way choice the redesign's artboards call .seg: a 30 px bar inside a one pixel
// ui.border at radius 2 (Matrix's is 28), each option padded 12 (Screens' 13, Shortcuts' 14) in 13 px Medium
// and secondary ink, the chosen one on the
// raised ground in primary ink with a 2 px accent line along its foot. An option can be drawn and not
// offered (Settings' "Yellow flags" scope, #504), in the dim ink with a tooltip of its own.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace OpenDashPlugin
{
    internal sealed class Segmented : Border
    {
        /// <summary>The bar's height where a page passes none: Rig, Settings, LEDs, Screens and Shortcuts draw
        /// 30. Matrix.dc.html's .seg is 28 and passes PanelKit.SegmentedHeightMatrix.</summary>
        public const double BarHeight = 30;

        /// <summary>Each option's padding either side where a page passes none, 12; Screens.dc.html pads 13
        /// and Shortcuts.dc.html 14 (PanelKit.SegmentedPaddingScreens and SegmentedPaddingShortcuts).</summary>
        public const double OptionPadding = 12;
        public const double OptionTextSize = 13;
        public const double Underline = 2;

        public sealed class Option
        {
            public Option(string value, string label, bool disabled = false, string tooltip = null, double minWidth = 0)
            {
                Value = value;
                Label = label;
                Disabled = disabled;
                Tooltip = tooltip;
                MinWidth = minWidth;
            }

            /// <summary>What the option holds even when its word is short, its word centred in it: AddLeds'
            /// ends buttons (None, 1 to 4) are 40 wide where the bare word would make them about 31.</summary>
            public double MinWidth { get; }

            public string Value { get; }
            public string Label { get; }

            /// <summary>Drawn and not offered: it cannot be chosen, and says why in its tooltip.</summary>
            public bool Disabled { get; }

            public string Tooltip { get; }
        }

        /// <summary>One option's parts: the box that carries the ground, the ring that says where the keyboard
        /// is, the word, and the line along the foot.</summary>
        private sealed class Cell
        {
            public Cell(Border box, Border ring, TextBlock text, Rectangle line, bool disabled)
            {
                Box = box;
                Ring = ring;
                Text = text;
                Line = line;
                Disabled = disabled;
            }

            public Border Box { get; }
            public Border Ring { get; }
            public TextBlock Text { get; }
            public Rectangle Line { get; }
            public bool Disabled { get; }
        }

        private readonly List<Cell> cells = new List<Cell>();
        private readonly List<string> values = new List<string>();
        private readonly List<string> labels = new List<string>();
        private string selected;
        private readonly double optionPadding;

        public event Action<string> Changed;

        /// <param name="height">The bar's height: <see cref="BarHeight"/>, or the page's artboard's.</param>
        /// <param name="padding">Each option's padding either side: <see cref="OptionPadding"/>, or the page's.</param>
        public Segmented(IEnumerable<Option> options, string selectedValue, double height = BarHeight, double padding = OptionPadding)
        {
            optionPadding = padding;
            BorderBrush = Ui.Brush(Theme.Border);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(Theme.Radius);
            Height = height;
            HorizontalAlignment = HorizontalAlignment.Right;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            // The bar takes the keyboard as one control and the arrows move between its options, which is
            // what a row of mutually exclusive choices is. WPF's own dotted rectangle is dropped because the
            // focus is drawn inside the chosen option instead.
            Focusable = true;
            FocusVisualStyle = null;
            KeyDown += OnKey;
            GotKeyboardFocus += (sender, args) => Paint();
            LostKeyboardFocus += (sender, args) => Paint();
            // A locked bar fades the way a disabled button does, to the kit's 40 per cent: see OnPropertyChanged.

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            Child = panel;

            var list = options.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                var cell = BuildCell(list[i], i == 0, i == list.Count - 1);
                cells.Add(cell);
                values.Add(list[i].Value);
                labels.Add(list[i].Label);
                panel.Children.Add(cell.Box);
            }

            var offered = list.Where(o => !o.Disabled).Select(o => o.Value).ToList();
            selected = offered.Contains(selectedValue) ? selectedValue : (values.Contains(selectedValue) ? selectedValue : offered.FirstOrDefault());
            Paint();
        }

        public string Selected => selected;

        /// <summary>The chosen option's word, which the bar's automation peer gives as its value.</summary>
        public string SelectedLabel
        {
            get
            {
                var index = values.IndexOf(selected);
                return index < 0 ? null : labels[index];
            }
        }

        /// <summary>Chooses the option whose word or value is <paramref name="word"/>, as a press would: what a
        /// screen reader's set value does.</summary>
        public void SelectByWord(string word)
        {
            var index = labels.FindIndex(label => string.Equals(label, word, StringComparison.OrdinalIgnoreCase));
            if (index < 0) index = values.FindIndex(value => string.Equals(value, word, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) Select(values[index], true);
        }

        /// <summary>A peer of its own (#523), so a page names the bar directly and a screen reader reads that
        /// name and the chosen word; a Border has none, and a name set on it reached nothing.</summary>
        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        {
            return new SegmentedPeer(this);
        }

        /// <summary>
        /// A locked bar fades the way a disabled button does, to the kit's 40 per cent, unless it sits in a
        /// Soon, whose wrapper has already faded it (Ui.InSoon). Both properties arrive when the bar joins a
        /// tree, in either order, so the fade is worked out again whenever either moves.
        /// </summary>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == IsEnabledProperty || e.Property == Ui.InSoonProperty)
            {
                Opacity = IsEnabled || Ui.GetInSoon(this) ? 1 : PanelMetrics.DisabledOpacity;
            }
        }

        /// <summary>
        /// One option: the ground, the word, the line along its foot, and the focus ring over all three.
        /// </summary>
        private Cell BuildCell(Option option, bool first, bool last)
        {
            var corners = new CornerRadius(first ? 1 : 0, last ? 1 : 0, last ? 1 : 0, first ? 1 : 0);
            var text = Ui.Text(option.Label, OptionTextSize, FontWeights.Medium, Theme.TextSecondary);
            text.Margin = new Thickness(optionPadding, 0, optionPadding, 0);
            if (option.MinWidth > 0) text.HorizontalAlignment = HorizontalAlignment.Center;
            var line = new Rectangle { Height = Underline, VerticalAlignment = VerticalAlignment.Bottom, Fill = System.Windows.Media.Brushes.Transparent, IsHitTestVisible = false };
            var ring = new Border
            {
                BorderThickness = new Thickness(Theme.FocusRing),
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                CornerRadius = corners,
                IsHitTestVisible = false,
            };
            var content = new Grid();
            content.Children.Add(text);
            content.Children.Add(line);
            content.Children.Add(ring);

            var box = new Border
            {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = Ui.Brush(Theme.Border),
                BorderThickness = new Thickness(0, 0, last ? 0 : 1, 0),
                CornerRadius = corners,
                Cursor = option.Disabled ? Cursors.No : Cursors.Hand,
                MinWidth = option.MinWidth,
                Child = content,
                ToolTip = option.Tooltip,
            };
            if (option.Tooltip != null) ToolTipService.SetShowOnDisabled(box, true);
            var value = option.Value;
            if (!option.Disabled)
            {
                // An option chooses on a release that follows a press on that same option, as a button does.
                // It chose on any release over it, so the click that dismissed a sheet's dim or a flyout -- the
                // press on the dim, the release on whatever the collapsed layer had covered -- rewrote the
                // setting under the pointer. The press takes the mouse, and only the release that ends that
                // press, inside the option, chooses.
                box.MouseLeftButtonDown += (sender, args) =>
                {
                    if (box.CaptureMouse()) args.Handled = true;
                };
                box.MouseLeftButtonUp += (sender, args) =>
                {
                    if (!box.IsMouseCaptured) return;
                    box.ReleaseMouseCapture();
                    args.Handled = true;
                    var at = args.GetPosition(box);
                    if (at.X < 0 || at.Y < 0 || at.X > box.ActualWidth || at.Y > box.ActualHeight) return;
                    // A pointer leaves the keyboard where it clicked, so that the arrows carry on from the
                    // option the hand chose rather than from the one it left.
                    Focus();
                    Select(value, true);
                };
                box.MouseEnter += (sender, args) =>
                {
                    if (value != selected) box.Background = Ui.Brush(Theme.Hover);
                };
                box.MouseLeave += (sender, args) => Paint();
            }
            return new Cell(box, ring, text, line, option.Disabled);
        }

        public void Select(string value, bool notify)
        {
            var index = values.IndexOf(value);
            if (index < 0 || cells[index].Disabled || value == selected) return;
            var was = SelectedLabel;
            selected = value;
            Paint();
            if (System.Windows.Automation.Peers.AutomationPeer.ListenerExists(System.Windows.Automation.Peers.AutomationEvents.PropertyChanged))
            {
                var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(this);
                if (peer != null) peer.RaisePropertyChangedEvent(System.Windows.Automation.ValuePatternIdentifiers.ValueProperty, was ?? string.Empty, SelectedLabel ?? string.Empty);
            }
            if (notify) Changed?.Invoke(value);
        }

        /// <summary>Left and right move the choice itself, past an option that is not offered. An arrow at
        /// either end stays there rather than falling out of the control.</summary>
        private void OnKey(object sender, KeyEventArgs args)
        {
            var step = args.Key == Key.Left ? -1 : args.Key == Key.Right ? 1 : 0;
            if (step == 0) return;
            args.Handled = true;
            var next = values.IndexOf(selected) + step;
            while (next >= 0 && next < values.Count && cells[next].Disabled) next += step;
            if (next < 0 || next >= values.Count) return;
            Select(values[next], true);
        }

        /// <summary>
        /// The whole of the control's state: which option is chosen, which are not offered, and whether the
        /// keyboard is on it. The focused option and the chosen one are the same, because the arrows choose
        /// as they move, so the ring is drawn on the chosen option while the bar holds the keyboard.
        /// </summary>
        private void Paint()
        {
            var focused = IsKeyboardFocused;
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var active = values[i] == selected;
                cell.Box.Background = active ? Ui.Brush(Theme.SurfaceRaised) : System.Windows.Media.Brushes.Transparent;
                cell.Text.Foreground = Ui.Brush(cell.Disabled ? Theme.TextDim : active ? Theme.TextPrimary : Theme.TextSecondary);
                cell.Line.Fill = active ? Ui.Brush(Theme.Accent) : System.Windows.Media.Brushes.Transparent;
                cell.Ring.BorderBrush = active && focused
                    ? Ui.Brush(Theme.Focus)
                    : System.Windows.Media.Brushes.Transparent;
            }
        }
    }
}
