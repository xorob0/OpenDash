// Widgets.cs: small factories for the panel's building blocks (brushes, text styles, the tracked label,
// rows, sections, icons), so that SettingsControl reads like the canvas it reproduces.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OpenDashPlugin
{
    internal static partial class Ui
    {
        // Every path lives in PanelIcons, which a test holds against PluginComponents.dc.html
        // character for character; this file is the one no test compiles, so a path written here
        // was a path nothing checked. The names stay, because the call sites read better for them.
        public const string WarningIcon = PanelIcons.Alert;
        public const string ExternalLinkIcon = PanelIcons.External;
        public const string RefreshIcon = PanelIcons.Refresh;
        /// <summary>The chevron a drop-down carries, as the canvas draws it.</summary>
        public const string ChevronIcon = PanelIcons.Chevron;

        // The kind icons and the plus, which live in PanelMetrics.cs so that a test can hold them against
        // the canvas character for character; a path is geometry, and this file is the one no test compiles.
        public const string DisplayIcon = PanelMetrics.DisplayIcon;
        public const string GridIcon = PanelMetrics.GridIcon;
        public const string PhoneIcon = PanelMetrics.PhoneIcon;
        public const string PlusIcon = PanelMetrics.PlusIcon;

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

        /// <summary>.ui on the canvas: Barlow 14, text.primary, line height 1.45.</summary>
        public static TextBlock Body(string text)
        {
            var block = Text(text, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary);
            block.LineHeight = Math.Round(Theme.SizeBody * 1.45, 1);
            block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            return block;
        }

        /// <summary>.cap on the canvas: Barlow 13, text.secondary, line height 1.45, wrapping at the 620 px
        /// a section's own caption is given. A caption inside a settings row is narrower and says so, the
        /// row capping its whole text column. The cap is what keeps the page readable once it fills a
        /// window wider than the canvas: prose that runs the width of a desk is a line nobody finishes.</summary>
        public static TextBlock Caption(string text, double maxWidth = 620)
        {
            var block = Text(text, Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);
            block.TextWrapping = TextWrapping.Wrap;
            block.MaxWidth = maxWidth;
            block.LineHeight = Math.Round(Theme.SizeSmall * 1.45, 1);
            block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            block.HorizontalAlignment = HorizontalAlignment.Left;
            return block;
        }

        /// <summary>.num on the canvas: openDash Display SemiBold, which is Barlow Condensed, tracked
        /// -0.01 em and on the tabular figures the face carries, so that a value that ticks does not
        /// shift the ones beside it.</summary>
        public static StackPanel Numeral(string text, double size, string hex)
        {
            var panel = Tracked(text, size, FontWeights.SemiBold, hex, Theme.TrackingNumeral, PanelFonts.Data);
            Typography.SetNumeralAlignment(panel, FontNumeralAlignment.Tabular);
            return panel;
        }

        /// <summary>.lbl on the canvas: Barlow Medium 12 at the label tracking, drawn in the case it is
        /// written in. A label is sentence case and the capitals that remain, an acronym or a flag, are typed
        /// in the string, as docs/design/brand.md says; the tracking was sized for capitals and is the
        /// canvas's to re-size, which brand.md records as owed.</summary>
        public static StackPanel Label(string text, string hex = Theme.TextLabel, double size = Theme.SizeLabel)
        {
            return Tracked(text, size, FontWeights.Medium, hex, Theme.TrackingLabel);
        }

        /// <summary>The tracking WPF gives no property for: every character is its own TextBlock and carries
        /// the spacing as a right margin, negative where the token is. A stack of glyphs cannot trim or wrap,
        /// so this is for the short fixed runs the canvas tracks -- a label, a numeral, the wordmark.</summary>
        public static StackPanel Tracked(string text, double size, FontWeight weight, string hex, double tracking, FontFamily family = null)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var spacing = Math.Round(size * tracking, 2);
            foreach (var character in text)
            {
                var glyph = Text(character.ToString(), size, weight, hex, family);
                glyph.Margin = new Thickness(0, 0, spacing, 0);
                panel.Children.Add(glyph);
            }
            return panel;
        }

        // Layout

        /// <summary>Vertical stack with a fixed gap between children (flex column, gap).</summary>
        /// <remarks>
        /// The gap is a bottom margin given to every child but the last as the stack is built, and it does
        /// not follow HStack's rule, because the tabs lean on the difference: a section is appended to after
        /// it is built (the Lights tab's rows, the line Announce inserts, the plugin section's progress
        /// bar), and what arrives late brings its own spacing, to which a gap owed between every two drawn
        /// children would add.
        /// </remarks>
        public static StackPanel VStack(double gap, params UIElement[] children)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            for (var i = 0; i < children.Length; i++)
            {
                var element = children[i] as FrameworkElement;
                if (element != null && i < children.Length - 1)
                {
                    var margin = element.Margin;
                    element.Margin = new Thickness(margin.Left, margin.Top, margin.Right, margin.Bottom + gap);
                }
                panel.Children.Add(children[i]);
            }
            return panel;
        }

        /// <summary>Horizontal stack with a gap between every two children that draw (flex row, gap,
        /// align-items center). A child that takes no width, an empty host or a collapsed control, has no
        /// gap beside it; PanelMetrics.RowOffsets is the rule.</summary>
        public static Panel HStack(double gap, params UIElement[] children)
        {
            var panel = new GapRow(gap) { VerticalAlignment = VerticalAlignment.Center };
            foreach (var child in children) panel.Children.Add(child);
            return panel;
        }

        /// <summary>
        /// The panel behind HStack: its children side by side, each as wide as it asks, and the gap only
        /// between two that draw.
        /// </summary>
        /// <remarks>
        /// A panel rather than a margin on each child, because whether a child draws is known when the row
        /// is laid out and not when it is built: the Update host of a strip row is filled and emptied by a
        /// press long after HStack has returned. Otherwise it measures and arranges as a horizontal
        /// StackPanel does, so a row whose children all draw is laid out exactly as before.
        /// </remarks>
        private sealed class GapRow : Panel
        {
            private readonly double gap;

            public GapRow(double gap)
            {
                this.gap = gap;
            }

            protected override Size MeasureOverride(Size available)
            {
                // Unbounded along the row, as a horizontal StackPanel measures, so that a child is as wide
                // as it asks and a wrapping caption wraps at its own MaxWidth.
                var slot = new Size(double.PositiveInfinity, available.Height);
                var height = 0.0;
                foreach (UIElement child in InternalChildren)
                {
                    child.Measure(slot);
                    height = Math.Max(height, child.DesiredSize.Height);
                }
                var offsets = PanelMetrics.RowOffsets(Widths(), gap);
                return new Size(offsets[offsets.Length - 1], height);
            }

            protected override Size ArrangeOverride(Size final)
            {
                var widths = Widths();
                var offsets = PanelMetrics.RowOffsets(widths, gap);
                for (var i = 0; i < widths.Length; i++)
                {
                    var child = InternalChildren[i];
                    child.Arrange(new Rect(offsets[i], 0, widths[i], Math.Max(final.Height, child.DesiredSize.Height)));
                }
                return final;
            }

            private double[] Widths()
            {
                var widths = new double[InternalChildren.Count];
                for (var i = 0; i < widths.Length; i++) widths[i] = InternalChildren[i].DesiredSize.Width;
                return widths;
            }
        }

        /// <summary>A settings row: title and caption on the left, the control on the right. Since #503 it is
        /// the redesign's .row, drawn by <see cref="SettingRow"/> -- a rule on top, 12 above and below, the
        /// title at 15 -- so that a page still calling it matches the pages rebuilt around it. A null or
        /// empty caption draws the title alone, which is the answer for a row whose control already says
        /// everything there is to say; see docs/design/voice.md.</summary>
        public static Border Row(string title, string caption, FrameworkElement control)
        {
            return SettingRow(title, control, caption);
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

        /// <summary>
        /// A row of the Install tab: what the thing is on the left, the state it is in and the action on
        /// the right, over a one pixel rule.
        /// </summary>
        /// <remarks>
        /// A 28 px button and a 24 px pill inside 40 leave nothing above or below them, so the row carries
        /// no vertical padding of its own and the rule is what separates one from the next.
        /// </remarks>
        public static Border InstallRow(string iconPath, string name, string caption, FrameworkElement pill, FrameworkElement button)
        {
            var text = VStack(0, Body(name), Label(caption, Theme.TextLabel));
            var left = iconPath == null
                ? (FrameworkElement)text
                : HStack(PanelMetrics.RowIconGap, Icon(iconPath, Theme.TextLabel), text);
            // A row with nothing to press is the package list, which says what is on disk and offers no
            // action: HStack of a null child would throw, so the pill stands alone. A host that is empty
            // for now is a button like any other, because HStack owes no gap beside a child that draws
            // nothing, so its pill ends on the same edge as the package list's.
            var row = Row(left, button == null ? (FrameworkElement)pill : HStack(PanelMetrics.RowRightGap, pill, button));
            row.Height = PanelMetrics.RowHeight;
            return new Border
            {
                BorderBrush = Brush(PanelMetrics.RowRule),
                BorderThickness = new Thickness(0, 0, 0, PanelMetrics.BorderWeight),
                Child = row,
            };
        }

        // Marks and status

        /// <summary>The 6 px square status dot.</summary>
        public static Rectangle Dot(string hex)
        {
            return new Rectangle
            {
                Width = PanelMetrics.DotSize,
                Height = PanelMetrics.DotSize,
                Fill = Brush(hex),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        /// <summary>A status: the dot and its tracked label, in the 24 px pill the canvas draws. The height
        /// is the whole of what a pill adds, and it is what keeps a row of them on one baseline.</summary>
        public static Panel StatusPill(string dotHex, string label, string labelHex)
        {
            var pill = HStack(PanelMetrics.PillGap, Dot(dotHex), Label(label, labelHex));
            pill.Height = PanelMetrics.PillHeight;
            return pill;
        }

        /// <summary>What a run reports while it is running: the word, the percentage it has reached, and a
        /// 4 px accent bar under them. Accent, because the app is working rather than warning.</summary>
        public static FrameworkElement Progress(double fraction, double width = PanelMetrics.ProgressWidth)
        {
            var head = Row(Label(PanelCopy.Installing, Theme.TextPrimary),
                Numeral(PanelCopy.Percent(fraction), Theme.SizeNumeral, Theme.TextSecondary));
            var track = new Border
            {
                Height = PanelMetrics.ProgressBarHeight,
                Background = Brush(Theme.SurfaceRaised),
                Child = new Rectangle
                {
                    Width = PanelMetrics.ProgressFill(fraction, width),
                    Height = PanelMetrics.ProgressBarHeight,
                    Fill = Brush(Theme.Accent),
                    HorizontalAlignment = HorizontalAlignment.Left,
                },
            };
            var stack = VStack(PanelMetrics.ProgressGap, head, track);
            stack.Width = width;
            return stack;
        }

        /// <summary>
        /// The mark: the face's own layout, two full width strips with three zones between them.
        /// </summary>
        /// <remarks>
        /// The geometry is MarkShape.PathData, which is the SVG's own `d` string rather than a transcription of it,
        /// so the two copies are one string and MarkTests compares them character for character. WPF cannot render
        /// an SVG and Markdown cannot render a Canvas, so the shape is drawn twice; this is what stops the copies
        /// drifting, and the last mark proved it is needed, because the rectangles matched and the corner radius
        /// did not. Change the SVG first.
        ///
        /// "F0" is the path mini-language's even-odd fill rule, which is what the SVG spells as
        /// fill-rule="evenodd": it is what makes each separation a hole in the housing rather than a line drawn on
        /// top of it, so the mark carries one brush and works on any ground. RenderTransform rather than Stretch,
        /// because Stretch fits the ink's bounds and would quietly drop the box's padding.
        /// </remarks>
        public static FrameworkElement Mark(double size = 24)
        {
            var scale = size / MarkShape.Box;
            var mark = new Path
            {
                Data = Geometry.Parse("F0 " + MarkShape.PathData),
                Fill = Brush(Theme.Accent),
                RenderTransform = new ScaleTransform(scale, scale),
            };
            var canvas = new Canvas { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
            canvas.Children.Add(mark);
            return canvas;
        }

        /// <summary>A stroked icon from the canvas's SVG path data, drawn at its 16 px native size.</summary>
        public static Path Icon(string pathData, string hex, double size = PanelIcons.SizeInControl)
        {
            return new Path
            {
                Data = Geometry.Parse(pathData),
                Stroke = Brush(hex),
                StrokeThickness = PanelIcons.StrokeWeight,
                StrokeStartLineCap = PenLineCap.Square,
                StrokeEndLineCap = PenLineCap.Square,
                StrokeLineJoin = PenLineJoin.Miter,
                Width = size,
                Height = size,
                Stretch = Stretch.None,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        // Focus and hover
        //
        // A control OpenDash templates itself keeps none of what WPF and SimHub draw around a control:
        // the bare Border below is the whole of the drop button and the card, so the ring a keyboard
        // needs and the answer a pointer expects are drawn here or nowhere.

        /// <summary>
        /// The focus visual: control.focusRing of ui.focus, held its own offset clear of the control.
        /// </summary>
        /// <remarks>
        /// The canvas draws it as two shadows, `0 0 0 2px surface.base, 0 0 0 4px ui.focus`, so the offset
        /// is painted rather than left clear: over the panel's own ground the two read the same, and over a
        /// card or a flyout only the painted one holds the ring off what is under it.
        ///
        /// A focus visual is an adorner, so the ring costs the layout nothing and cannot nudge the row it
        /// is in, which is what lets a 24 px control carry one without crowding its text. It is public
        /// because the controls SimHub draws for the panel -- the toggle, the list -- have to be given it
        /// by hand; the ones drawn here take it from the factories below.
        /// </remarks>
        public static Style FocusRing()
        {
            var offset = new FrameworkElementFactory(typeof(Border));
            offset.SetValue(Border.BorderBrushProperty, Brush(Theme.SurfaceBase));
            offset.SetValue(Border.BorderThicknessProperty, new Thickness(Theme.FocusRingOffset));
            offset.SetValue(Border.CornerRadiusProperty, new CornerRadius(Theme.Radius + Theme.FocusRingOffset));

            var ring = new FrameworkElementFactory(typeof(Border));
            ring.SetValue(Border.BorderBrushProperty, Brush(Theme.Focus));
            ring.SetValue(Border.BorderThicknessProperty, new Thickness(Theme.FocusRing));
            ring.SetValue(Border.CornerRadiusProperty, new CornerRadius(Theme.Radius + Theme.FocusRingOffset + Theme.FocusRing));
            ring.SetValue(FrameworkElement.MarginProperty, new Thickness(-(Theme.FocusRingOffset + Theme.FocusRing)));
            ring.AppendChild(offset);

            var style = new Style(typeof(Control));
            style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Control)) { VisualTree = ring }));
            return style;
        }

        /// <summary>The pointer's answer on such a control: the outline lightens to ui.accentHover, the
        /// one state the canvas gives the accent. SimHub's own styles bring a hover of their own, so this
        /// goes on the controls OpenDash draws and on none of theirs.</summary>
        private static Trigger HoverOutline()
        {
            var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            over.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(Theme.AccentHover), "chrome"));
            return over;
        }

        // Fields
        //
        // The select, the drop button and the text box are one shape on the canvas, so they are one
        // description here. The two paddings live in PanelMetrics, where a test holds them against the
        // sheet; design/tokens.json carries neither of them yet, and they are owed as tokens the way the
        // panel's other geometry is.

        private const double FieldPaddingLeft = PanelMetrics.FieldPaddingLeft;
        private const double FieldPaddingRight = PanelMetrics.FieldPaddingRight;

        /// <summary>
        /// The chrome every field-shaped control carries: ui.field under a one pixel ui.border at the
        /// panel's radius, the canvas's padding, and the focus ring. The value is 14 in the full height
        /// and 12 in the short one, which is the canvas's own pairing rather than a choice made here.
        /// </summary>
        /// <remarks>
        /// A TextBox is given a template as well, because Control has no CornerRadius and WPF's own
        /// template has none to bind. A ComboBox keeps SimHub's: the template that would round its box
        /// supplies the list under it too, and half a replacement would leave the box OpenDash's and the
        /// list SimHub's. That corner is therefore recorded as SimHub's rather than approximated here.
        /// </remarks>
        public static void Field(Control control, double height = Theme.ControlHeight)
        {
            control.Height = height;
            control.Padding = new Thickness(FieldPaddingLeft, 0, FieldPaddingRight, 0);
            control.Background = Brush(Theme.Field);
            control.BorderBrush = Brush(Theme.Border);
            control.BorderThickness = new Thickness(PanelMetrics.BorderWeight);
            control.Foreground = Brush(Theme.TextPrimary);
            control.FontFamily = PanelFonts.Label;
            control.FontSize = height <= Theme.ControlHeightSm ? Theme.SizeLabel : Theme.SizeBody;
            control.VerticalContentAlignment = VerticalAlignment.Center;
            control.FocusVisualStyle = FocusRing();
            var box = control as TextBox;
            if (box != null) box.Template = FieldTemplate();
        }

        /// <summary>The field's own chrome around whatever the control puts inside it, which for a TextBox
        /// is the content host WPF looks for by name.</summary>
        private static ControlTemplate FieldTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border), "chrome");
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(Theme.Radius));
            var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
            host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            host.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(host);
            var template = new ControlTemplate(typeof(TextBox)) { VisualTree = border };
            template.Triggers.Add(HoverOutline());
            return template;
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
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = row,
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
            };
            Field(button, Theme.ControlHeightSm);
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
        /// button's own brushes are the canvas's field and the popup below it is the affordance. The outline
        /// under the pointer is all a shut field has to say that it is a control.</summary>
        private static ControlTemplate DropButtonTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border), "chrome");
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(Theme.Radius));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            var template = new ControlTemplate(typeof(ToggleButton)) { VisualTree = border };
            template.Triggers.Add(HoverOutline());
            return template;
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
                    CornerRadius = new CornerRadius(Theme.Radius),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 2, 0, 0),
                    Child = content,
                },
            };
            owner.Checked += (sender, args) => popup.IsOpen = true;
            owner.Unchecked += (sender, args) => popup.IsOpen = false;
            popup.Closed += (sender, args) => owner.IsChecked = false;
            // StaysOpen false closes the popup on a click elsewhere and not on Escape, and a panel
            // of twenty-one checkboxes is exactly the thing somebody presses Escape to put away.
            popup.KeyDown += (sender, args) =>
            {
                if (args.Key != Key.Escape) return;
                popup.IsOpen = false;
                args.Handled = true;
            };
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

        // Buttons
        //
        // The canvas draws one primary per panel and asks for everything else as an outline, a link or a
        // dashed add, which is a distinction SHButtonPrimary cannot make. All of them are 32 high, or the
        // 28 of a row, padded 16 and set in Barlow Medium 14; what changes between them is the ground, the
        // outline and the ink. Leaving SimHub's style costs its hover, focus and disabled chrome, and the
        // panel disables a button for the length of a run, so these draw all four states themselves: the
        // ground changes under the pointer, focus is a ring in the adorner layer, and a disabled button
        // keeps the canvas's 40 %.

        /// <summary>The one accented action of a page: an update, and nothing beside it.</summary>
        public static Button PrimaryButton(string text, double height = PanelMetrics.ButtonHeight)
        {
            return Chrome(text, height, null, Brush(Theme.Accent), Brush(Theme.AccentHover), null, Theme.OnAccent);
        }

        /// <summary>Every other action: the panel's own ground inside an outline. The leading icon is what
        /// Reinstall carries.</summary>
        public static Button OutlineButton(string text, double height = PanelMetrics.ButtonHeight, string iconPath = null)
        {
            return Chrome(text, height, iconPath, System.Windows.Media.Brushes.Transparent, Brush(Theme.Hover), Brush(Theme.Border), Theme.TextPrimary);
        }

        /// <summary>The action that takes something away: the outline again, with danger for its ink. The
        /// colour is the whole of the warning, because a destructive action in a row of buttons has to be
        /// read before it is pressed and a heavier ground would only make it the loudest thing there.</summary>
        public static Button DestructiveButton(string text, double height = PanelMetrics.ButtonHeight)
        {
            return Chrome(text, height, null, System.Windows.Media.Brushes.Transparent, Brush(Theme.Hover), Brush(Theme.Border), Theme.Danger);
        }

        /// <summary>The action that adds one: the add card's dashed frame at a button's height, since what
        /// it adds does not exist yet and a solid outline would say that it does.</summary>
        public static Button AddButton(string text, double height = PanelMetrics.ButtonHeight)
        {
            return Chrome(text, height, null, System.Windows.Media.Brushes.Transparent, Brush(Theme.Hover), null, Theme.TextSecondary, true);
        }

        /// <summary>A text-only action, the ink carrying it: accent for a link, danger for a destructive
        /// one. No ground, no outline and no padding, so it does not read as a button in a row of them.</summary>
        public static Button LinkButton(string text, string hex = Theme.Accent)
        {
            var button = Chrome(text, PanelMetrics.ButtonHeight, null, System.Windows.Media.Brushes.Transparent, null, null, hex);
            button.Padding = new Thickness(0);
            return button;
        }

        private static Button Chrome(string text, double height, string iconPath, Brush background, Brush hover, Brush border, string ink, bool dashed = false)
        {
            return new Button
            {
                Height = height,
                Padding = new Thickness(PanelMetrics.ButtonPaddingX, 0, PanelMetrics.ButtonPaddingX, 0),
                Background = background,
                BorderBrush = border ?? System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(border == null ? 0 : PanelMetrics.BorderWeight),
                Foreground = Brush(ink),
                FontFamily = PanelFonts.Label,
                FontSize = Theme.SizeBody,
                FontWeight = FontWeights.Medium,
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                // A bare string when there is no icon, because a caller that rewrites the label for the
                // length of a run -- "Updating…" and back again -- would otherwise replace the TextBlock
                // and lose the face with it. The button's own type properties draw it.
                Content = iconPath == null
                    ? (object)text
                    : HStack(PanelMetrics.ButtonIconGap, Icon(iconPath, ink), Text(text, Theme.SizeBody, FontWeights.Medium, ink)),
                Template = ButtonTemplate(hover, dashed),
                FocusVisualStyle = FocusRing(),
            };
        }

        /// <summary>The button's own chrome: the border the control's brushes describe, and the two states
        /// a trigger can carry. A null hover is a link, which has no ground to change; a dashed button
        /// carries no border of its own and the frame over it is the outline.</summary>
        private static ControlTemplate ButtonTemplate(Brush hover, bool dashed)
        {
            var border = new FrameworkElementFactory(typeof(Border), "chrome");
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(PanelMetrics.Radius));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            FrameworkElementFactory root = border;
            if (dashed)
            {
                root = new FrameworkElementFactory(typeof(Grid));
                root.AppendChild(border);
                root.AppendChild(DashedFrame());
            }

            var template = new ControlTemplate(typeof(Button)) { VisualTree = root };
            if (hover != null)
            {
                var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                over.Setters.Add(new Setter(Border.BackgroundProperty, hover, "chrome"));
                template.Triggers.Add(over);
            }
            // The whole button rather than its chrome, so that what is drawn beside the chrome fades with
            // it: the dashed frame is a sibling of the ground it outlines.
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, PanelMetrics.DisabledOpacity));
            template.Triggers.Add(disabled);
            return template;
        }

        // Cards and groups
        //
        // The tab bar and the old screen card went with #503: the sidebar replaced the one, and the kit's
        // DeviceCard and DashedAddCard (Widgets.Kit.cs) the other. The templates below are shared by both.

        /// <summary>A border around the content and nothing else, as the drop button's template is, and with
        /// the same outline under the pointer: SimHub's button styles paint a chrome these do not want.</summary>
        private static ControlTemplate CardTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border), "chrome");
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(Theme.Radius));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            border.AppendChild(presenter);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            template.Triggers.Add(HoverOutline());
            return template;
        }

        /// <summary>
        /// The add card's frame, which is the same card drawn with a dashed edge.
        /// </summary>
        /// <remarks>
        /// Once the card at rest is transparent too, these dashes are the only thing telling a screen from
        /// the action that adds one.
        /// </remarks>
        private static ControlTemplate DashedCardTemplate()
        {
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            var host = new FrameworkElementFactory(typeof(Grid));
            host.AppendChild(presenter);
            host.AppendChild(DashedFrame());
            return new ControlTemplate(typeof(Button)) { VisualTree = host };
        }

        /// <summary>
        /// The dashed outline the add card and the add button are drawn with.
        /// </summary>
        /// <remarks>
        /// A dash cannot come from Border.BorderBrush, so the frame is a Rectangle over the content and the
        /// click target stays the control underneath it. A Rectangle centres its stroke on its own edge, so
        /// half of it would fall outside without the margin.
        /// </remarks>
        private static FrameworkElementFactory DashedFrame()
        {
            var dashes = new DoubleCollection { PanelMetrics.DashOn, PanelMetrics.DashOff };
            dashes.Freeze();
            var frame = new FrameworkElementFactory(typeof(Rectangle));
            frame.SetValue(Shape.StrokeProperty, Brush(Theme.Border));
            frame.SetValue(Shape.StrokeThicknessProperty, PanelMetrics.BorderWeight);
            frame.SetValue(Shape.StrokeDashArrayProperty, dashes);
            frame.SetValue(Rectangle.RadiusXProperty, PanelMetrics.Radius);
            frame.SetValue(Rectangle.RadiusYProperty, PanelMetrics.Radius);
            frame.SetValue(FrameworkElement.MarginProperty, new Thickness(PanelMetrics.BorderWeight / 2));
            frame.SetValue(UIElement.IsHitTestVisibleProperty, false);
            return frame;
        }

        private static ControlTemplate BareButtonTemplate()
        {
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            return new ControlTemplate(typeof(Button)) { VisualTree = presenter };
        }

        /// <summary>
        /// A group that opens and shuts, for settings that are kept but rarely wanted.
        /// </summary>
        /// <remarks>
        /// Matrix 2 to 4 is four groups of six rows for hardware almost nobody owns. The rule the panel
        /// follows is that such a setting is kept and moved out of the way, never dropped: somebody owns
        /// two flag boxes and their rig has to be configurable.
        /// </remarks>
        /// <param name="toggled">Told when a hand opens or shuts the group, so a page can remember which is
        /// open across a redraw.</param>
        public static FrameworkElement Collapsible(string title, string caption, bool open, Func<FrameworkElement> build, Action<bool> toggled = null)
        {
            var chevron = Icon(ChevronIcon, Theme.TextSecondary);
            var head = HStack(8, chevron, Text(title, Theme.SizeBody, FontWeights.Normal, Theme.TextPrimary));
            if (caption != null) head.Children.Add(Text(caption, Theme.SizeLabel, FontWeights.Normal, Theme.TextLabel));

            var button = new Button
            {
                Content = head,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 6, 0, 6),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            button.Template = BareButtonTemplate();

            var host = new ContentControl { Margin = new Thickness(24, 0, 0, 8) };
            Action apply = () =>
            {
                // Built on first open rather than built and hidden: twenty-four rows of controls that
                // nobody has asked for still cost their construction on every visit to the page.
                if (open && host.Content == null) host.Content = build();
                host.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
                chevron.RenderTransform = open ? null : new RotateTransform(-90, Theme.IconSize / 2, Theme.IconSize / 2);
            };
            button.Click += (sender, args) =>
            {
                open = !open;
                apply();
                if (toggled != null) toggled(open);
            };
            apply();
            return VStack(0, button, host);
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
