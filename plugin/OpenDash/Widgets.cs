// Widgets.cs: small factories for the panel's building blocks (brushes, text styles, the tracked label,
// rows, sections, icons), so that SettingsControl reads like the canvas it reproduces.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OpenDashPlugin
{
    internal static class Ui
    {
        // Icons from PluginComponents.dc.html: 16 px, 1.5 stroke, square caps, mitre joins.
        public const string WarningIcon = "M8 2l6.5 11.5h-13zM8 6.5v3.5M8 11.5v.5";
        public const string ExternalLinkIcon = "M6.5 3.5H3.5v9h9V9.5M9 3h4v4M13 3l-6 6";
        public const string RefreshIcon = "M13 8a5 5 0 0 1-8.7 3.4M3 8a5 5 0 0 1 8.7-3.4M11.5 2v3h-3M4.5 14v-3h3";
        /// <summary>The chevron a drop-down carries, as the canvas draws it.</summary>
        public const string ChevronIcon = "M4 6l4 4 4-4";

        private static readonly Dictionary<string, SolidColorBrush> Brushes = new Dictionary<string, SolidColorBrush>();

        /// <summary>A frozen brush for a #RRGGBB token value, cached per colour.</summary>
        public static SolidColorBrush Brush(string hex)
        {
            lock (Brushes)
            {
                SolidColorBrush brush;
                if (Brushes.TryGetValue(hex, out brush)) return brush;
                var color = (Color)ColorConverter.ConvertFromString(hex);
                brush = new SolidColorBrush(color);
                brush.Freeze();
                Brushes[hex] = brush;
                return brush;
            }
        }

        // Text

        public static TextBlock Text(string text, double size, FontWeight weight, string hex, FontFamily family = null)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = weight,
                Foreground = Brush(hex),
                FontFamily = family ?? PanelFonts.Label,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        /// <summary>.ui on the canvas: Barlow 14, text.primary.</summary>
        public static TextBlock Body(string text)
        {
            return Text(text, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary);
        }

        /// <summary>.cap on the canvas: Barlow 13, text.secondary, wrapping at 460 px, line height 1.45.</summary>
        public static TextBlock Caption(string text, double maxWidth = 460)
        {
            var block = Text(text, Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);
            block.TextWrapping = TextWrapping.Wrap;
            block.MaxWidth = maxWidth;
            block.LineHeight = Math.Round(Theme.SizeSmall * 1.45, 1);
            block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            block.HorizontalAlignment = HorizontalAlignment.Left;
            return block;
        }

        /// <summary>.num on the canvas: openDash Display SemiBold, which is Barlow Condensed.</summary>
        public static TextBlock Numeral(string text, double size, string hex)
        {
            return Text(text, size, FontWeights.SemiBold, hex, PanelFonts.Data);
        }

        /// <summary>.lbl on the canvas: Barlow Medium 12, uppercase, 0.14 em tracking. WPF has no letter spacing,
        /// so every character is its own TextBlock followed by the tracking as a right margin.</summary>
        public static StackPanel Label(string text, string hex = Theme.TextLabel, double size = Theme.SizeLabel)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var tracking = Math.Round(size * Theme.TrackingLabel, 2);
            foreach (var character in text.ToUpperInvariant())
            {
                var glyph = Text(character.ToString(), size, FontWeights.Medium, hex);
                glyph.Margin = new Thickness(0, 0, tracking, 0);
                panel.Children.Add(glyph);
            }
            return panel;
        }

        // Layout

        /// <summary>Vertical stack with a fixed gap between children (flex column, gap).</summary>
        public static StackPanel VStack(double gap, params UIElement[] children)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            AddWithGap(panel, gap, true, children);
            return panel;
        }

        /// <summary>Horizontal stack with a fixed gap between children (flex row, gap, align-items center).</summary>
        public static StackPanel HStack(double gap, params UIElement[] children)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            AddWithGap(panel, gap, false, children);
            return panel;
        }

        private static void AddWithGap(Panel panel, double gap, bool vertical, IList<UIElement> children)
        {
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var element = child as FrameworkElement;
                if (element != null && i < children.Count - 1)
                {
                    var margin = element.Margin;
                    element.Margin = vertical
                        ? new Thickness(margin.Left, margin.Top, margin.Right, margin.Bottom + gap)
                        : new Thickness(margin.Left, margin.Top, margin.Right + gap, margin.Bottom);
                }
                panel.Children.Add(child);
            }
        }

        /// <summary>A settings row: title and caption on the left, the control on the right, 32 px apart.</summary>
        public static Grid Row(string title, string caption, FrameworkElement control)
        {
            var text = VStack(4, Body(title), Caption(caption));
            text.MaxWidth = 460;
            text.HorizontalAlignment = HorizontalAlignment.Left;
            return Row(text, control);
        }

        public static Grid Row(FrameworkElement left, FrameworkElement right)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            left.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(left, 0);
            right.VerticalAlignment = VerticalAlignment.Center;
            right.HorizontalAlignment = HorizontalAlignment.Right;
            var margin = right.Margin;
            right.Margin = new Thickness(margin.Left + 32, margin.Top, margin.Right, margin.Bottom);
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);
            return grid;
        }

        /// <summary>A section: 1 px rule on top, 24 px vertical padding, a tracked label and its rows 14 px apart.</summary>
        public static Border Section(string label, params UIElement[] rows)
        {
            var children = new List<UIElement> { Label(label) };
            children.AddRange(rows);
            return new Border
            {
                BorderBrush = Brush(Theme.Rule),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 24, 0, 24),
                Child = VStack(14, children.ToArray()),
            };
        }

        // Marks

        /// <summary>The 6 px square status dot.</summary>
        public static Rectangle Dot(string hex)
        {
            return new Rectangle { Width = 6, Height = 6, Fill = Brush(hex), VerticalAlignment = VerticalAlignment.Center };
        }

        /// <summary>The segment mark beside the wordmark: 28 x 8, radius 2, brand accent.</summary>
        public static Rectangle Mark()
        {
            return new Rectangle
            {
                Width = 28,
                Height = 8,
                RadiusX = Theme.Radius,
                RadiusY = Theme.Radius,
                Fill = Brush(Theme.Accent),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        /// <summary>A stroked icon from the canvas's SVG path data, drawn at its 16 px native size.</summary>
        public static Path Icon(string pathData, string hex, double size = Theme.IconSize)
        {
            return new Path
            {
                Data = Geometry.Parse(pathData),
                Stroke = Brush(hex),
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Square,
                StrokeEndLineCap = PenLineCap.Square,
                StrokeLineJoin = PenLineJoin.Miter,
                Width = size,
                Height = size,
                Stretch = Stretch.None,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        // Drop-downs

        /// <summary>
        /// The field-styled box the canvas draws for every choice on the panel: zone fill, a one pixel
        /// border, the text on the left and a chevron on the right. A ComboBox is the control for a
        /// choice from a list; this is for the two choices a list cannot carry -- twenty-one checkboxes,
        /// and the pair of fields at one end of the bar -- which open a panel instead.
        /// </summary>
        public static ToggleButton DropButton(double width, string text, string tooltip = null)
        {
            var caption = Text(text, Theme.SizeLabel, FontWeights.Normal, Theme.TextPrimary);
            caption.TextTrimming = TextTrimming.CharacterEllipsis;
            caption.TextWrapping = TextWrapping.NoWrap;
            var chevron = Icon(ChevronIcon, Theme.TextSecondary);
            chevron.HorizontalAlignment = HorizontalAlignment.Right;
            var row = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(chevron, Dock.Right);
            row.Children.Add(chevron);
            row.Children.Add(caption);

            var button = new ToggleButton
            {
                Width = width,
                Height = Theme.ControlHeightSm,
                Padding = new Thickness(10, 0, 8, 0),
                Background = Brush(Theme.Field),
                BorderBrush = Brush(Theme.Border),
                BorderThickness = new Thickness(1),
                Foreground = Brush(Theme.TextPrimary),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = row,
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
            };
            // SimHub's ToggleButton style is a switch, so the drop button keeps the plain one and paints
            // itself; asking for the styled template here would draw a slider where a box belongs.
            button.Template = DropButtonTemplate();
            button.Tag = caption;
            return button;
        }

        /// <summary>Rewrites the caption of a drop button built above.</summary>
        public static void SetDropText(ToggleButton button, string text)
        {
            var caption = button.Tag as TextBlock;
            if (caption != null) caption.Text = text;
        }

        /// <summary>A border around the content and nothing else: no chrome, no checked state, because the
        /// button's own brushes are the canvas's field and the popup below it is the affordance.</summary>
        private static ControlTemplate DropButtonTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(Theme.Radius));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            return new ControlTemplate(typeof(ToggleButton)) { VisualTree = border };
        }

        /// <summary>The panel a drop button opens: raised surface, a border, 12 px of padding, and it
        /// closes when the mouse goes elsewhere.</summary>
        public static Popup Flyout(ToggleButton owner, UIElement content)
        {
            var popup = new Popup
            {
                PlacementTarget = owner,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = new Border
                {
                    Background = Brush(Theme.SurfaceRaised),
                    BorderBrush = Brush(Theme.Border),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 2, 0, 0),
                    Child = content,
                },
            };
            owner.Checked += (sender, args) => popup.IsOpen = true;
            owner.Unchecked += (sender, args) => popup.IsOpen = false;
            popup.Closed += (sender, args) => owner.IsChecked = false;
            return popup;
        }

        /// <summary>A drop button and the flyout it opens, in one element that can go in a layout.</summary>
        public static FrameworkElement Drop(ToggleButton button, UIElement content)
        {
            var host = new Grid { Width = button.Width, Height = button.Height, HorizontalAlignment = HorizontalAlignment.Left };
            host.Children.Add(button);
            host.Children.Add(Flyout(button, content));
            return host;
        }

        // SimHub integration

        /// <summary>Applies a style from SimHub's application resources when it exists; false otherwise.</summary>
        public static bool TryStyle(FrameworkElement element, object key)
        {
            try
            {
                var style = Application.Current?.TryFindResource(key) as Style;
                if (style == null) return false;
                element.Style = style;
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("Style " + key + " could not be applied: " + ex.Message);
                return false;
            }
        }

        public static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Warn("Could not open " + url + ": " + ex.Message);
            }
        }
    }
}
