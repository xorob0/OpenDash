// Segmented.cs: the two-or-three-way choice control of PluginComponents.dc.html ("segmented"): a 32 px bar
// with a 1 px ui.border, radius 2, options padded 16 px, the active one in text.primary on surface.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OpenDashPlugin
{
    internal sealed class Segmented : Border
    {
        public sealed class Option
        {
            public Option(string value, string label)
            {
                Value = value;
                Label = label;
            }

            public string Value { get; }
            public string Label { get; }
        }

        /// <summary>One option's three parts: the box that carries the ground, the ring that says where the
        /// keyboard is, and the word.</summary>
        private sealed class Cell
        {
            public Cell(Border box, Border ring, TextBlock text)
            {
                Box = box;
                Ring = ring;
                Text = text;
            }

            public Border Box { get; }
            public Border Ring { get; }
            public TextBlock Text { get; }
        }

        private readonly List<Cell> cells = new List<Cell>();
        private readonly List<string> values = new List<string>();
        private string selected;

        public event Action<string> Changed;

        public Segmented(IEnumerable<Option> options, string selectedValue)
        {
            BorderBrush = Ui.Brush(Theme.Border);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(Theme.Radius);
            Height = Theme.ControlHeight;
            HorizontalAlignment = HorizontalAlignment.Right;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            // The bar takes the keyboard as one control and the arrows move between its options, which is
            // what a row of two or three mutually exclusive choices is. WPF's own dotted rectangle is
            // dropped because the canvas draws the focus inside the chosen option instead.
            Focusable = true;
            FocusVisualStyle = null;
            KeyDown += OnKey;
            GotKeyboardFocus += (sender, args) => Paint();
            LostKeyboardFocus += (sender, args) => Paint();

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            Child = panel;

            var list = options.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                var cell = BuildCell(list[i], i == 0, i == list.Count - 1);
                cells.Add(cell);
                values.Add(list[i].Value);
                panel.Children.Add(cell.Box);
            }

            selected = values.Contains(selectedValue) ? selectedValue : values.FirstOrDefault();
            Paint();
        }

        public string Selected => selected;

        /// <summary>
        /// One option: the ground and the word, with the focus ring over both.
        /// </summary>
        /// <remarks>
        /// The padding is the word's margin rather than the box's, so that the ring can sit at the option's
        /// own edge. The canvas draws it as an inset shadow, which is what an outer ring cannot be here: a
        /// bar of three options has no room between them, and a ring around the control would say the bar
        /// has the keyboard without saying which option it is on.
        /// </remarks>
        private Cell BuildCell(Option option, bool first, bool last)
        {
            var corners = new CornerRadius(first ? 1 : 0, last ? 1 : 0, last ? 1 : 0, first ? 1 : 0);
            var text = Ui.Text(option.Label, Theme.SizeBody, FontWeights.Medium, Theme.TextSecondary);
            text.Margin = new Thickness(Theme.PaddingX, 0, Theme.PaddingX, 0);
            var ring = new Border
            {
                BorderThickness = new Thickness(Theme.FocusRing),
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                CornerRadius = corners,
                IsHitTestVisible = false,
            };
            var content = new Grid();
            content.Children.Add(text);
            content.Children.Add(ring);

            var box = new Border
            {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = Ui.Brush(Theme.Border),
                BorderThickness = new Thickness(0, 0, last ? 0 : 1, 0),
                CornerRadius = corners,
                Cursor = Cursors.Hand,
                Child = content,
            };
            var value = option.Value;
            box.MouseLeftButtonUp += (sender, args) =>
            {
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
            return new Cell(box, ring, text);
        }

        public void Select(string value, bool notify)
        {
            if (!values.Contains(value) || value == selected) return;
            selected = value;
            Paint();
            if (notify) Changed?.Invoke(value);
        }

        /// <summary>Left and right move the choice itself, so that the bar answers the keyboard the way it
        /// answers the pointer. An arrow at either end stays there rather than falling out of the control,
        /// which is what an unhandled arrow key would do.</summary>
        private void OnKey(object sender, KeyEventArgs args)
        {
            var step = args.Key == Key.Left ? -1 : args.Key == Key.Right ? 1 : 0;
            if (step == 0) return;
            args.Handled = true;
            var next = values.IndexOf(selected) + step;
            if (next < 0 || next >= values.Count) return;
            Select(values[next], true);
        }

        /// <summary>
        /// The whole of the control's state: which option is chosen, and whether the keyboard is on it.
        /// </summary>
        /// <remarks>
        /// The focused option and the chosen one are the same index, because the arrows choose as they
        /// move; the ring is therefore drawn on the chosen option and only while the bar holds the
        /// keyboard, which is the state the canvas draws.
        /// </remarks>
        private void Paint()
        {
            var focused = IsKeyboardFocused;
            for (var i = 0; i < cells.Count; i++)
            {
                var active = values[i] == selected;
                cells[i].Box.Background = active ? Ui.Brush(Theme.TextPrimary) : System.Windows.Media.Brushes.Transparent;
                cells[i].Text.Foreground = Ui.Brush(active ? Theme.OnAccent : Theme.TextSecondary);
                cells[i].Ring.BorderBrush = active && focused
                    ? Ui.Brush(Theme.Accent)
                    : System.Windows.Media.Brushes.Transparent;
            }
        }
    }
}
