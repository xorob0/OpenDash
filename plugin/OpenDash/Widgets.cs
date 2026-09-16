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
    internal static class Ui
    {
        // Icons from PluginComponents.dc.html: 16 px, 1.5 stroke, square caps, mitre joins.
        public const string WarningIcon = "M8 2l6.5 11.5h-13zM8 6.5v3.5M8 11.5v.5";
        public const string ExternalLinkIcon = "M6.5 3.5H3.5v9h9V9.5M9 3h4v4M13 3l-6 6";
        public const string RefreshIcon = "M13 8a5 5 0 0 1-8.7 3.4M3 8a5 5 0 0 1 8.7-3.4M11.5 2v3h-3M4.5 14v-3h3";
        /// <summary>The chevron a drop-down carries, as the canvas draws it.</summary>
        public const string ChevronIcon = "M4 6l4 4 4-4";

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

        /// <summary>.num on the canvas: openDash Display SemiBold, which is Barlow Condensed, tracked
        /// -0.01 em and on the tabular figures the face carries, so that a value that ticks does not
        /// shift the ones beside it.</summary>
        public static StackPanel Numeral(string text, double size, string hex)
        {
            var panel = Tracked(text, size, FontWeights.SemiBold, hex, Theme.TrackingNumeral, PanelFonts.Data);
            Typography.SetNumeralAlignment(panel, FontNumeralAlignment.Tabular);
            return panel;
        }

        /// <summary>.lbl on the canvas: Barlow Medium 12, uppercase, 0.14 em tracking.</summary>
        public static StackPanel Label(string text, string hex = Theme.TextLabel, double size = Theme.SizeLabel)
        {
            return Tracked(text.ToUpperInvariant(), size, FontWeights.Medium, hex, Theme.TrackingLabel);
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

        /// <summary>A section: 1 px rule on top, 28 px of padding, a tracked label and its rows 20 px apart,
        /// which is the canvas's .sec. Every tab is built out of these, so the numbers reflow the panel.</summary>
        public static Border Section(string label, params UIElement[] rows)
        {
            var children = new List<UIElement> { Label(label) };
            children.AddRange(rows);
            return new Border
            {
                BorderBrush = Brush(PanelMetrics.SectionRule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(0, PanelMetrics.SectionPadding, 0, PanelMetrics.SectionPadding),
                Child = VStack(PanelMetrics.SectionGap, children.ToArray()),
            };
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
            var row = Row(left, HStack(PanelMetrics.RowRightGap, pill, button));
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
        public static StackPanel StatusPill(string dotHex, string label, string labelHex)
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
        /// The mark: the dash, a housing under a half circle with a tachometer needle swept up and to the right.
        /// </summary>
        /// <remarks>
        /// The geometry is MarkShape.PathData, which is the SVG's own `d` string rather than a transcription of it,
        /// so the two copies are one string and MarkTests compares them character for character. WPF cannot render
        /// an SVG and Markdown cannot render a Canvas, so the shape is drawn twice; this is what stops the copies
        /// drifting, and the last mark proved it is needed, because the rectangles matched and the corner radius
        /// did not. Change the SVG first.
        ///
        /// "F0" is the path mini-language's even-odd fill rule, which is what the SVG spells as
        /// fill-rule="evenodd": it is what makes the needle a hole in the housing rather than a second shape on
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

        // Focus and hover
        //
        // A control openDash templates itself keeps none of what WPF and SimHub draw around a control:
        // the bare Border below is the whole of the drop button and the card, so the ring a keyboard
        // needs and the answer a pointer expects are drawn here or nowhere.

        /// <summary>The focus visual: control.focusRing of ui.focus, held its own offset clear of the
        /// control. A focus visual is an adorner, so the ring costs the layout nothing and cannot nudge
        /// the row it is in, which is what lets a 24 px control carry one without crowding its text.</summary>
        private static Style FocusRing()
        {
            var ring = new FrameworkElementFactory(typeof(Border));
            ring.SetValue(Border.BorderBrushProperty, Brush(Theme.Focus));
            ring.SetValue(Border.BorderThicknessProperty, new Thickness(Theme.FocusRing));
            ring.SetValue(Border.CornerRadiusProperty, new CornerRadius(Theme.Radius + Theme.FocusRingOffset));
            ring.SetValue(FrameworkElement.MarginProperty, new Thickness(-(Theme.FocusRingOffset + Theme.FocusRing)));
            var style = new Style(typeof(Control));
            style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(Control)) { VisualTree = ring }));
            return style;
        }

        /// <summary>The pointer's answer on such a control: the outline lightens to ui.accentHover, the
        /// one state the canvas gives the accent. SimHub's own styles bring a hover of their own, so this
        /// goes on the controls openDash draws and on none of theirs.</summary>
        private static Trigger HoverOutline()
        {
            var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            over.Setters.Add(new Setter(Border.BorderBrushProperty, Brush(Theme.AccentHover), "chrome"));
            return over;
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
                FocusVisualStyle = FocusRing(),
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
        // The canvas draws one primary per panel and asks for everything else as an outline or a link,
        // which is a distinction SHButtonPrimary cannot make. Leaving SimHub's style costs its hover, focus
        // and disabled chrome, and the panel disables a button for the length of a run, so these draw all
        // four states themselves: the ground changes under the pointer, focus is a ring in the adorner
        // layer, and a disabled button keeps the canvas's 40 %.

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

        /// <summary>A text-only action, the ink carrying it: accent for a link, danger for a destructive
        /// one. No ground, no outline and no padding, so it does not read as a button in a row of them.</summary>
        public static Button LinkButton(string text, string hex = Theme.Accent)
        {
            var button = Chrome(text, PanelMetrics.ButtonHeight, null, System.Windows.Media.Brushes.Transparent, null, null, hex);
            button.Padding = new Thickness(0);
            return button;
        }

        private static Button Chrome(string text, double height, string iconPath, Brush background, Brush hover, Brush border, string ink)
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
                Template = ButtonTemplate(hover),
                FocusVisualStyle = FocusRing(),
            };
        }

        /// <summary>The button's own chrome: the border the control's brushes describe, and the two states
        /// a trigger can carry. A null hover is a link, which has no ground to change.</summary>
        private static ControlTemplate ButtonTemplate(Brush hover)
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

            var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            if (hover != null)
            {
                var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                over.Setters.Add(new Setter(Border.BackgroundProperty, hover, "chrome"));
                template.Triggers.Add(over);
            }
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, PanelMetrics.DisabledOpacity, "chrome"));
            template.Triggers.Add(disabled);
            return template;
        }

        // Tabs, cards and groups
        //
        // XOR-125 names panel.tabs, control.tab and control.screenCard as tokens. They are not in
        // design/tokens.json, and design/ is the author's rather than something a build writes into, so
        // these compose from the tokens that do exist and docs/design/plugin.md records that the three
        // are still owed. Nothing here invents a colour: every value is a Theme constant. The card's own
        // geometry is in PanelMetrics.cs, where a test can hold it against the canvas; the tab's is here,
        // because nothing but this file has asked for it yet.

        public const double TabHeight = 40;
        public const double TabUnderline = 2;

        /// <summary>
        /// One tab. Selected carries the accent underline and the primary ink; the rest are secondary.
        /// </summary>
        /// <remarks>
        /// A button rather than a ToggleButton in a group: SimHub's ToggleButton style is a switch
        /// (Widgets' drop button hit the same thing), and what is wanted here is a label with a rule
        /// under it.
        /// </remarks>
        public static Button Tab(string text, bool selected, Action clicked)
        {
            var label = Text(text, Theme.SizeBody, selected ? FontWeights.SemiBold : FontWeights.Normal,
                selected ? Theme.TextPrimary : Theme.TextSecondary);
            label.Margin = new Thickness(0, 0, 0, 8);
            var underline = new Rectangle
            {
                Height = TabUnderline,
                Fill = Brush(selected ? Theme.Accent : Theme.SurfaceBase),
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            var stack = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(underline, Dock.Bottom);
            stack.Children.Add(underline);
            stack.Children.Add(label);

            var button = new Button
            {
                Content = stack,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 10, 0, 0),
                Margin = new Thickness(0, 0, 28, 0),
                Height = TabHeight,
                Cursor = Cursors.Hand,
                VerticalContentAlignment = VerticalAlignment.Stretch,
            };
            button.Template = BareButtonTemplate();
            button.Click += (sender, args) => clicked();
            return button;
        }

        /// <summary>
        /// A screen's card: the kind as an icon, the name over the size, and a dot when something is wrong.
        /// </summary>
        /// <remarks>
        /// The card says what a rig is made of and nothing more. Everything about the screen -- the
        /// folder, the namespace, the zones -- is in the pane below it, because a row of cards is read
        /// at a glance and a card carrying four facts is not.
        ///
        /// The kind used to be a tracked word along the card's foot, which is what made the card 66 tall.
        /// It is the icon on the left now, and the two things the foot carried have moved to the size line:
        /// the dot a failed install shows, and the word for the one kind the canvas draws no icon for.
        ///
        /// Selected decides the ink as well as the brushes, so the rows are built here, where the flag is
        /// in hand, rather than in the shell.
        /// </remarks>
        public static Button Card(string name, string size, string kind, bool selected, string dot, Action clicked)
        {
            var colours = PanelMetrics.Card(selected);
            var title = Text(name, Theme.SizeBody, FontWeights.Medium, Theme.TextPrimary);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.TextWrapping = TextWrapping.NoWrap;

            UIElement line = Label(PanelCopy.SizeLine(kind, size), colours.SizeLabel);
            if (dot != null) line = HStack(6, Dot(dot), line);
            var rows = VStack(PanelMetrics.CardLineGap, title, line);

            var body = new DockPanel { LastChildFill = true };
            var kindIcon = PanelMetrics.KindIcon(kind);
            if (kindIcon != null)
            {
                var icon = Icon(kindIcon, colours.Icon);
                icon.Margin = new Thickness(0, 0, PanelMetrics.CardIconGap, 0);
                DockPanel.SetDock(icon, Dock.Left);
                body.Children.Add(icon);
            }
            body.Children.Add(rows);

            var background = colours.Fill == null ? System.Windows.Media.Brushes.Transparent : Brush(colours.Fill);
            return CardShell(body, clicked, background, Brush(colours.Border), PanelMetrics.CardMinWidth);
        }

        /// <summary>The add card: the one card that is an action rather than a thing, and the only one with
        /// a dashed frame, because nothing is installed until a screen is chosen.</summary>
        public static Button AddCard(Action clicked)
        {
            var row = HStack(PanelMetrics.AddCardGap,
                Icon(PlusIcon, Theme.Accent),
                Text(PanelCopy.AddScreen, Theme.SizeBody, FontWeights.Medium, Theme.Accent));
            row.HorizontalAlignment = HorizontalAlignment.Center;
            var card = CardShell(row, clicked, System.Windows.Media.Brushes.Transparent,
                System.Windows.Media.Brushes.Transparent, PanelMetrics.AddCardMinWidth);
            card.Template = DashedCardTemplate();
            return card;
        }

        private static Button CardShell(UIElement content, Action clicked, Brush background, Brush border, double minWidth)
        {
            var button = new Button
            {
                MinWidth = minWidth,
                Height = PanelMetrics.CardHeight,
                Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(PanelMetrics.CardPaddingX, 0, PanelMetrics.CardPaddingX, 0),
                Background = background,
                BorderBrush = border,
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                Cursor = Cursors.Hand,
                Content = content,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                FocusVisualStyle = FocusRing(),
            };
            button.Template = CardTemplate();
            button.Click += (sender, args) => clicked();
            return button;
        }

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
        /// A dash cannot come from Border.BorderBrush, so the frame is a Rectangle over the content and the
        /// click target stays the button underneath it. A Rectangle centres its stroke on its own edge, so
        /// half of it would fall outside the card without the margin. Once the card at rest is transparent
        /// too, these dashes are the only thing telling a screen from the action that adds one.
        /// </remarks>
        private static ControlTemplate DashedCardTemplate()
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

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            var host = new FrameworkElementFactory(typeof(Grid));
            host.AppendChild(presenter);
            host.AppendChild(frame);
            return new ControlTemplate(typeof(Button)) { VisualTree = host };
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
        public static FrameworkElement Collapsible(string title, string caption, bool open, Func<FrameworkElement> build)
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
