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

        private readonly List<Border> cells = new List<Border>();
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

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            Child = panel;

            var list = options.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                var cell = BuildCell(list[i], i == 0, i == list.Count - 1);
                cells.Add(cell);
                values.Add(list[i].Value);
                panel.Children.Add(cell);
            }

            selected = values.Contains(selectedValue) ? selectedValue : values.FirstOrDefault();
            Paint();
        }

        public string Selected => selected;

        private Border BuildCell(Option option, bool first, bool last)
        {
            var cell = new Border
            {
                Padding = new Thickness(Theme.PaddingX, 0, Theme.PaddingX, 0),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = Ui.Brush(Theme.Border),
                BorderThickness = new Thickness(0, 0, last ? 0 : 1, 0),
                CornerRadius = new CornerRadius(first ? 1 : 0, last ? 1 : 0, last ? 1 : 0, first ? 1 : 0),
                Cursor = Cursors.Hand,
                Child = Ui.Text(option.Label, Theme.SizeBody, FontWeights.Medium, Theme.TextSecondary),
            };
            var value = option.Value;
            cell.MouseLeftButtonUp += (sender, args) => Select(value, true);
            cell.MouseEnter += (sender, args) =>
            {
                if (value != selected) cell.Background = Ui.Brush(Theme.Hover);
            };
            cell.MouseLeave += (sender, args) => Paint();
            return cell;
        }

        public void Select(string value, bool notify)
        {
            if (!values.Contains(value) || value == selected) return;
            selected = value;
            Paint();
            if (notify) Changed?.Invoke(value);
        }

        private void Paint()
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var active = values[i] == selected;
                cells[i].Background = active ? Ui.Brush(Theme.TextPrimary) : System.Windows.Media.Brushes.Transparent;
                var text = (TextBlock)cells[i].Child;
                text.Foreground = Ui.Brush(active ? Theme.OnAccent : Theme.TextSecondary);
            }
        }
    }
}
