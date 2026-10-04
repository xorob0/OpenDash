// Widgets.Kit.cs: the rebuilt panel's kit (#791) -- type, rows, buttons, tags, chips, crumbs, steps, the fix
// box, cards and card grids, the switch, the slider, the search list, anchors -- drawn to the classes the
// redesign's artboards share (.btn, .row, .seg, .tog, .chip, .card, .lbl, .fix, .step, .crumb, .soon).
//
// The shell owns this file and the page agents build with it and do not edit it, so it is generous: a page
// should find what an artboard draws here rather than draw it again. The numbers are PanelShell's and the
// colours Theme's, both of which a test holds; nothing here invents a value. Widgets.cs keeps the older kit
// the re-hosted pages still use, and Widgets.Lights.cs the pictures of lights and screens.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OpenDashPlugin
{
    /// <summary>What a press looks like, from the artboards' button classes.</summary>
    internal enum PanelButtonKind
    {
        /// <summary>.btn.pri: the accent ground. One to a page.</summary>
        Primary,

        /// <summary>.btn: an outline on the page's own ground.</summary>
        Outline,

        /// <summary>.btn.ghost: no outline, secondary ink that lightens under the pointer.</summary>
        Ghost,

        /// <summary>.btn.danger: the outline in the soft danger ink.</summary>
        Danger,

        /// <summary>.btn.ghost.danger: no outline, the soft danger ink.</summary>
        GhostDanger,
    }

    /// <summary>How big a press is: .btn at 34, .btn.sm at 28, and the 36 of a sheet's footer.</summary>
    internal enum PanelButtonSize
    {
        Regular,
        Small,
        Large,
    }

    /// <summary>The parts of a settings row a later call reaches into: the title's line, where a tag is
    /// appended, and the control.</summary>
    internal sealed class RowParts
    {
        public RowParts(Panel titleLine, FrameworkElement control)
        {
            TitleLine = titleLine;
            Control = control;
        }

        public Panel TitleLine { get; private set; }

        public FrameworkElement Control { get; private set; }
    }

    internal static partial class Ui
    {
        // --- Type ----------------------------------------------------------------------------------

        /// <summary>A page's title: 30, SemiBold, line height 1.1.</summary>
        public static TextBlock PageTitle(string text)
        {
            var block = Text(text, PanelShell.PageTitleSize, FontWeights.SemiBold, Theme.TextPrimary);
            block.LineHeight = Math.Round(PanelShell.PageTitleSize * 1.1, 1);
            block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            block.TextWrapping = TextWrapping.Wrap;
            return block;
        }

        /// <summary>A section's heading: 17 SemiBold, or 19 for the one that leads a page's column.</summary>
        public static TextBlock Heading(string text, bool large = false)
        {
            var block = Text(text, large ? PanelShell.HeadingLargeSize : PanelShell.HeadingSize, FontWeights.SemiBold, Theme.TextPrimary);
            block.TextWrapping = TextWrapping.Wrap;
            return block;
        }

        /// <summary>The selected thing's name over its own settings: 22 SemiBold.</summary>
        public static TextBlock SubHeading(string text)
        {
            var block = Text(text, PanelShell.SubHeadingSize, FontWeights.SemiBold, Theme.TextPrimary);
            block.TextTrimming = TextTrimming.CharacterEllipsis;
            return block;
        }

        /// <summary>
        /// The artboards' .lbl: 11 SemiBold at the label tracking, in the case it is written in.
        /// </summary>
        /// <remarks>
        /// The artboards draw it uppercase through a transform; docs/design/brand.md is taking the transform
        /// away, so the text is drawn as written and the capitals an acronym needs are typed.
        /// </remarks>
        public static StackPanel Eyebrow(string text, string hex = Theme.TextSecondary)
        {
            return Tracked(text, PanelShell.EyebrowSize, FontWeights.SemiBold, hex, Theme.TrackingLabel);
        }

        /// <summary>A line of prose at a size and ink, wrapping at PanelShell.ProseMaxWidth against the left
        /// edge, as a caption does: the column has no ceiling, and a paragraph does.</summary>
        /// <remarks>Left rather than stretched, because a stretched element narrower than its slot is centred
        /// in it. A caller that wants another measure sets MaxWidth after.</remarks>
        public static TextBlock Prose(string text, double size = Theme.SizeSmall, string hex = Theme.TextSecondary)
        {
            var block = Text(text, size, FontWeights.Normal, hex);
            block.TextWrapping = TextWrapping.Wrap;
            block.LineHeight = Math.Round(size * 1.45, 1);
            block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            block.MaxWidth = PanelShell.ProseMaxWidth;
            block.HorizontalAlignment = HorizontalAlignment.Left;
            return block;
        }

        /// <summary>
        /// A picture drawn at a fixed size -- a face's zones, a round face's slots, the pit wall's pages --
        /// that shrinks to the column when the column is narrower and never grows past its own size, set
        /// against the left edge.
        /// </summary>
        /// <remarks>
        /// The column fills the window, so it is as narrow as the rail and a 20 px gutter leave it; a
        /// picture wider than that was clipped at the right. Scaled as a whole, controls included, which is
        /// what a picture of a thing is for; a page that can lay the picture out to the width instead should.
        /// </remarks>
        public static FrameworkElement FitWidth(FrameworkElement picture)
        {
            return new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = picture,
            };
        }

        // --- Icons on the twenty unit box -------------------------------------------------------------

        /// <summary>
        /// A stroked icon drawn on a box of <paramref name="box"/> units and shown at <paramref name="size"/>
        /// pixels: the redesign's paths are on twenty, the sheet's on sixteen.
        /// </summary>
        /// <remarks>
        /// Scaled as a whole, stroke included, which is what an SVG with that viewBox at that size does.
        /// The path is the canvas's only child, which is what <see cref="SetIconInk"/> reaches.
        /// </remarks>
        public static FrameworkElement Icon(string pathData, string hex, double size, double box)
        {
            var scale = size / box;
            var path = new Path
            {
                Data = Geometry.Parse(pathData),
                Stroke = Brush(hex),
                StrokeThickness = PanelIcons.StrokeWeight,
                StrokeStartLineCap = PenLineCap.Square,
                StrokeEndLineCap = PenLineCap.Square,
                StrokeLineJoin = PenLineJoin.Miter,
                RenderTransform = new ScaleTransform(scale, scale),
            };
            var canvas = new Canvas { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, SnapsToDevicePixels = false };
            canvas.Children.Add(path);
            return canvas;
        }

        /// <summary>A redesign icon at the sidebar's 18 px, or at another size.</summary>
        public static FrameworkElement NavIcon(string pathData, string hex, double size = PanelIcons.NavSize)
        {
            return Icon(pathData, hex, size, PanelIcons.NavBox);
        }

        /// <summary>Re-inks an icon built by either Icon factory.</summary>
        public static void SetIconInk(FrameworkElement icon, string hex)
        {
            var path = icon as Path;
            if (path == null)
            {
                var canvas = icon as Panel;
                if (canvas != null && canvas.Children.Count > 0) path = canvas.Children[0] as Path;
            }
            if (path != null) path.Stroke = Brush(hex);
        }

        // --- Brushes --------------------------------------------------------------------------------

        private static readonly Dictionary<string, SolidColorBrush> Tints = new Dictionary<string, SolidColorBrush>();

        /// <summary>A token colour at an opacity, frozen and cached: the artboards' rgba() grounds, such as the
        /// caution tint behind a fix box.</summary>
        public static SolidColorBrush Tint(string hex, double opacity)
        {
            var key = hex + "@" + opacity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            lock (Tints)
            {
                SolidColorBrush brush;
                if (Tints.TryGetValue(key, out brush)) return brush;
                var colour = (Color)ColorConverter.ConvertFromString(hex);
                colour.A = (byte)Math.Round(Math.Max(0, Math.Min(1, opacity)) * 255);
                brush = new SolidColorBrush(colour);
                brush.Freeze();
                Tints[key] = brush;
                return brush;
            }
        }

        // --- Rows -----------------------------------------------------------------------------------

        /// <summary>
        /// The artboards' .row: a one pixel rule on top, 12 above and below, the title at 15 Medium with any
        /// tags after it and an optional caption under it, and the control on the right 24 away.
        /// </summary>
        /// <remarks>
        /// The rows of a block stack with nothing between them: the rule and the padding are the whole of the
        /// rhythm, as they are on the artboards. The returned border's Tag carries the row's parts, which is
        /// how <see cref="Soon(FrameworkElement, SoonItem)"/> appends its tag to the title.
        /// </remarks>
        public static Border SettingRow(string title, FrameworkElement control, string caption = null, params FrameworkElement[] tags)
        {
            var titleLine = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var name = Text(title, PanelShell.RowTitleSize, FontWeights.Medium, Theme.TextPrimary);
            name.TextWrapping = TextWrapping.Wrap;
            titleLine.Children.Add(name);
            foreach (var tag in tags ?? new FrameworkElement[0])
            {
                if (tag == null) continue;
                tag.Margin = new Thickness(8, 0, 0, 0);
                tag.VerticalAlignment = VerticalAlignment.Center;
                titleLine.Children.Add(tag);
            }

            var left = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(titleLine);
            if (!string.IsNullOrEmpty(caption))
            {
                var cap = Caption(caption, PanelShell.RowCaptionMaxWidth);
                cap.Margin = new Thickness(0, PanelKit.FixDetailGap, 0, 0);
                left.Children.Add(cap);
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(left, 0);
            grid.Children.Add(left);
            if (control != null)
            {
                control.VerticalAlignment = VerticalAlignment.Center;
                control.HorizontalAlignment = HorizontalAlignment.Right;
                var margin = control.Margin;
                control.Margin = new Thickness(margin.Left + PanelShell.RowGap, margin.Top, margin.Right, margin.Bottom);
                Grid.SetColumn(control, 1);
                grid.Children.Add(control);
            }

            return new Border
            {
                BorderBrush = Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(0, PanelShell.RowPaddingY, 0, PanelShell.RowPaddingY),
                Child = grid,
                Tag = new RowParts(titleLine, control),
            };
        }

        /// <summary>The artboards' .sub: a row that belongs to the one above, indented 16 behind a rule.</summary>
        public static Border SubRow(UIElement row)
        {
            return new Border
            {
                BorderBrush = Brush(Theme.Border),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight, 0, 0, 0),
                Padding = new Thickness(PanelShell.SubRowIndent, 0, 0, 0),
                Margin = new Thickness(2, 0, 0, 0),
                Child = row,
            };
        }

        /// <summary>A block of rows, one under the other, closed by a rule under the last.</summary>
        public static StackPanel Rows(params UIElement[] rows)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            foreach (var row in rows)
            {
                if (row != null) stack.Children.Add(row);
            }
            return stack;
        }

        // --- Buttons --------------------------------------------------------------------------------

        /// <summary>
        /// A press as the artboards draw one: .btn at 34 and padded 14 in 14 px Medium, .sm at 28 and padded
        /// 10 in 13, the footer's at 36 and padded 16, in the kind's ground, outline and ink.
        /// </summary>
        /// <remarks>
        /// The label is the button's own content when it carries no icon, so a caller that rewrites it for
        /// the length of a run keeps the face; with an icon it is a row of the two. A label neither wraps nor
        /// trims, unless <paramref name="trims"/> asks for it: then it is a text that ends in an ellipsis where
        /// the press is capped narrower than its words (a name the driver typed, which nothing caps).
        /// </remarks>
        public static Button Button(string text, PanelButtonKind kind = PanelButtonKind.Outline, PanelButtonSize size = PanelButtonSize.Regular, string iconPath = null, bool trims = false)
        {
            double height, padding, font;
            switch (size)
            {
                case PanelButtonSize.Small: height = 28; padding = 10; font = 13; break;
                case PanelButtonSize.Large: height = 36; padding = 16; font = 14; break;
                default: height = 34; padding = 14; font = 14; break;
            }

            Brush ground = System.Windows.Media.Brushes.Transparent;
            Brush hover = Brush(Theme.Hover);
            Brush border = Brush(Theme.Border);
            var ink = Theme.TextPrimary;
            switch (kind)
            {
                case PanelButtonKind.Primary:
                    ground = Brush(Theme.Accent);
                    hover = Brush(Theme.AccentHover);
                    border = Brush(Theme.Accent);
                    ink = Theme.OnAccent;
                    break;
                case PanelButtonKind.Ghost:
                    border = null;
                    ink = Theme.TextSecondary;
                    break;
                case PanelButtonKind.Danger:
                    ink = Theme.DangerSoft;
                    break;
                case PanelButtonKind.GhostDanger:
                    border = null;
                    ink = Theme.DangerSoft;
                    break;
            }

            var button = new Button
            {
                Height = height,
                Padding = new Thickness(padding, 0, padding, 0),
                Background = ground,
                BorderBrush = border ?? System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(border == null ? 0 : PanelMetrics.BorderWeight),
                Foreground = Brush(ink),
                FontFamily = PanelFonts.Label,
                FontSize = font,
                FontWeight = FontWeights.Medium,
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = iconPath != null
                    ? HStack(8, NavIcon(iconPath, ink, 16), Text(text, font, FontWeights.Medium, ink))
                    : trims
                        ? new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis }
                        : (object)text,
                Template = ButtonTemplate(hover, false),
                FocusVisualStyle = FocusRing(),
            };
            if (kind == PanelButtonKind.Ghost)
            {
                button.MouseEnter += (sender, args) => button.Foreground = Brush(Theme.TextPrimary);
                button.MouseLeave += (sender, args) => button.Foreground = Brush(Theme.TextSecondary);
            }
            return button;
        }

        /// <summary>A square button carrying an icon alone, with its name as the tooltip: the sheet's close,
        /// the rail's search.</summary>
        public static Button IconButton(string iconPath, string tooltip, Action click, double size = 32, string hex = Theme.TextSecondary)
        {
            var icon = NavIcon(iconPath, hex, 18);
            var button = new Button
            {
                Width = size,
                Height = size,
                Padding = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Content = icon,
                ToolTip = tooltip,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Template = ButtonTemplate(Brush(Theme.Hover), false),
                FocusVisualStyle = FocusRing(),
            };
            button.MouseEnter += (sender, args) => SetIconInk(icon, Theme.TextPrimary);
            button.MouseLeave += (sender, args) => SetIconInk(icon, hex);
            if (click != null) button.Click += (sender, args) => click();
            return button;
        }

        /// <summary>
        /// The artboards' .btn.sm.dd: a small outline button with its value and a 12 px chevron, which opens
        /// a list of the values and says which one was chosen.
        /// </summary>
        /// <remarks>
        /// The labels are positional, as BuildChoice's are: labels[i] is what chosen(i) means. The button's
        /// text follows the choice, so the caller does not have to.
        /// </remarks>
        public static FrameworkElement ChoiceButton(string[] labels, int selected, Action<int> chosen, double minWidth = 150)
        {
            ToggleButton button = null;
            Popup flyout = null;
            var list = new StackPanel { Orientation = Orientation.Vertical, MinWidth = minWidth };
            button = ChoiceToggle(selected >= 0 && selected < labels.Length ? labels[selected] : string.Empty, minWidth);
            for (var i = 0; i < labels.Length; i++)
            {
                var index = i;
                var item = ListItem(labels[i], i == selected, () =>
                {
                    SetChoiceText(button, labels[index]);
                    if (flyout != null) flyout.IsOpen = false;
                    chosen(index);
                });
                list.Children.Add(item);
            }
            flyout = Flyout(button, list);
            ((Border)flyout.Child).Padding = new Thickness(4);
            var host = new Grid { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            host.Children.Add(button);
            host.Children.Add(flyout);
            return host;
        }

        /// <summary>The same button opening whatever the caller builds, for a choice a list cannot carry.</summary>
        public static FrameworkElement ChoiceButton(string text, Func<UIElement> content, double minWidth = 150)
        {
            var button = ChoiceToggle(text, minWidth);
            var host = new Grid { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            host.Children.Add(button);
            host.Children.Add(Flyout(button, content()));
            return host;
        }

        /// <summary>The same button doing whatever the caller says when pressed, with no list.</summary>
        public static Button ChoiceButton(string text, Action click, double minWidth = 150)
        {
            var label = Text(text, 13, FontWeights.Medium, Theme.TextPrimary);
            var row = new DockPanel { LastChildFill = true };
            var chevron = NavIcon(PanelIcons.ChevronDown, Theme.TextSecondary, 12);
            chevron.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(chevron, Dock.Right);
            row.Children.Add(chevron);
            row.Children.Add(label);
            var button = Button(text, PanelButtonKind.Outline, PanelButtonSize.Small);
            button.Content = row;
            button.MinWidth = minWidth;
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Click += (sender, args) => click();
            return button;
        }

        private static ToggleButton ChoiceToggle(string text, double minWidth)
        {
            var label = Text(text, 13, FontWeights.Medium, Theme.TextPrimary);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            var chevron = NavIcon(PanelIcons.ChevronDown, Theme.TextSecondary, 12);
            chevron.Margin = new Thickness(8, 0, 0, 0);
            var row = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(chevron, Dock.Right);
            row.Children.Add(chevron);
            row.Children.Add(label);
            var button = new ToggleButton
            {
                Height = 28,
                MinWidth = minWidth,
                Padding = new Thickness(10, 0, 10, 0),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = Brush(Theme.Border),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                Foreground = Brush(Theme.TextPrimary),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = row,
                Tag = label,
                FocusVisualStyle = FocusRing(),
            };
            button.Template = DropButtonTemplate();
            return button;
        }

        private static void SetChoiceText(ToggleButton button, string text)
        {
            var label = button.Tag as TextBlock;
            if (label != null) label.Text = text;
        }

        /// <summary>One entry of a flyout's list: 32 high, the current one in primary ink on the hover ground.</summary>
        public static Button ListItem(string text, bool current, Action click)
        {
            var label = Text(text, 13, current ? FontWeights.SemiBold : FontWeights.Normal, current ? Theme.TextPrimary : Theme.TextSecondary);
            var button = new Button
            {
                Height = 32,
                Padding = new Thickness(10, 0, 10, 0),
                Background = current ? Brush(Theme.Hover) : System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Content = label,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Template = ButtonTemplate(Brush(Theme.Hover), false),
                FocusVisualStyle = FocusRing(),
            };
            button.Click += (sender, args) => click();
            return button;
        }

        // --- Tags -----------------------------------------------------------------------------------

        /// <summary>The artboards' .new and .soontag: 18 high, padded 6, a one pixel outline, 10 px SemiBold
        /// tracked.</summary>
        public static Border Tag(string text, string borderHex, string inkHex)
        {
            return new Border
            {
                Height = PanelShell.TagHeight,
                Padding = new Thickness(PanelShell.TagPaddingX, 0, PanelShell.TagPaddingX - 1, 0),
                BorderBrush = Brush(borderHex),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                CornerRadius = new CornerRadius(Theme.Radius),
                VerticalAlignment = VerticalAlignment.Center,
                Child = Tracked(text, PanelShell.TagTextSize, FontWeights.SemiBold, inkHex, 0.12),
            };
        }

        /// <summary>What a control new in this release carries after its title, for one release.</summary>
        public static Border NewTag() { return Tag(PanelSoon.NewTag, Theme.TagNewBorder, Theme.TagNewText); }

        /// <summary>What a greyed control carries after its title. Private: a page draws it through
        /// <see cref="SoonTag(SoonItem)"/>, for a registry entry, or gets it from a kit row inside Soon.</summary>
        private static Border SoonTag() { return Tag(PanelSoon.Tag, Theme.Border, Theme.TextSecondary); }

        /// <summary>The Soon tag, for a page that lays out a greyed control of its own inside
        /// <see cref="Soon(FrameworkElement, SoonItem)"/> (Rig's Real hardware): it takes the entry, so the tag
        /// is only ever drawn for a row the registry holds.</summary>
        public static Border SoonTag(SoonItem item)
        {
            if (item == null) throw new ArgumentNullException("item");
            return SoonTag();
        }

        /// <summary>
        /// A control that is drawn and not yet built: faded, not answering, the ticket in its hover, and a
        /// Soon tag after its title when it is a row this kit built.
        /// </summary>
        /// <remarks>
        /// Only through a registry entry: the ticket is PanelSoon's, where PanelSoonTests holds every number
        /// to a ticket that is still open, so a page cannot grey a row with a number typed beside it.
        /// SoonItem's constructor is internal and PanelSoonTests fails on any "new SoonItem(" or "Coming soon"
        /// outside PanelSoon.cs, so that holds for a page file too.
        /// </remarks>
        private static Border SoonWith(FrameworkElement row, int ticket, string title)
        {
            var parts = row == null ? null : row.Tag as RowParts;
            if (parts != null && parts.TitleLine != null)
            {
                var tag = SoonTag();
                tag.Margin = new Thickness(8, 0, 0, 0);
                parts.TitleLine.Children.Add(tag);
            }
            // A GroupBorder rather than a Border, because a Border has no automation peer and the row's name set
            // on it reached no screen reader (#792): the wrapper is announced as a named group around the row.
            var wrapper = new GroupBorder
            {
                Child = row,
                Opacity = PanelShell.SoonOpacity,
                IsEnabled = false,
                Cursor = Cursors.No,
                ToolTip = PanelSoon.Tip(ticket),
                Background = System.Windows.Media.Brushes.Transparent,
            };
            // The fade is drawn once, here. The IsEnabled above passes down to every control in the row, and
            // each would add its own disabled 40 per cent on top of this 45 (0.18 in all) where the artboards
            // draw .soon>* at .45 and nothing more; InSoon, inherited, tells them to leave it to the wrapper.
            SetInSoon(wrapper, true);
            ToolTipService.SetShowOnDisabled(wrapper, true);
            if (title != null) AutomationName(wrapper, title);
            return wrapper;
        }

        /// <summary>A control that is drawn and not yet built, from the registry's entry: faded, not
        /// answering, "Coming soon · #n" in its hover, and the Soon tag after its title when it is a kit row.</summary>
        public static Border Soon(FrameworkElement row, SoonItem item)
        {
            if (item == null) throw new ArgumentNullException("item");
            // Anchored, so a search for it lands on it (PanelSearch lists every registry entry).
            return Anchor(SoonWith(row, item.Ticket, item.Title), item.Anchor);
        }

        /// <summary>A greyed switch row for a registry entry: the commonest shape a Soon takes.</summary>
        public static Border SoonRow(SoonItem item, FrameworkElement control = null)
        {
            var row = SettingRow(item.Title, control ?? Switch(false, on => { }));
            return Soon(row, item);
        }

        /// <summary>
        /// Set on a Soon's wrapper and inherited by everything in it: the wrapper draws the fade, so a control
        /// inside does not draw its own disabled fade on top.
        /// </summary>
        public static readonly DependencyProperty InSoonProperty = DependencyProperty.RegisterAttached(
            "InSoon", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        public static bool GetInSoon(DependencyObject element) { return (bool)element.GetValue(InSoonProperty); }

        public static void SetInSoon(DependencyObject element, bool value) { element.SetValue(InSoonProperty, value); }

        /// <summary>The kit's disabled fade as a template trigger: 40 per cent when the control is disabled,
        /// unless a Soon's wrapper above it has already faded it.</summary>
        internal static MultiTrigger DisabledFade()
        {
            var fade = new MultiTrigger();
            fade.Conditions.Add(new Condition(UIElement.IsEnabledProperty, false));
            fade.Conditions.Add(new Condition(InSoonProperty, false));
            fade.Setters.Add(new Setter(UIElement.OpacityProperty, PanelMetrics.DisabledOpacity));
            return fade;
        }

        private static void AutomationName(FrameworkElement element, string name)
        {
            System.Windows.Automation.AutomationProperties.SetName(element, name);
        }

        // --- Chips and crumbs -------------------------------------------------------------------------

        /// <summary>
        /// The Rig and preview pages' .chip: 30 high, an outline, secondary ink; pressed, it is the primary
        /// ink's ground with the page's ink on it. A swatch leads it when one is given.
        /// </summary>
        public static Button Chip(string text, bool pressed, Action click, string swatchHex = null)
        {
            var ink = pressed ? Theme.OnAccent : Theme.TextSecondary;
            var label = Text(text, PanelKit.ChipTextSize, FontWeights.Medium, ink);
            UIElement content = label;
            if (swatchHex != null)
            {
                var swatch = new Border { Width = PanelKit.ChipSwatch, Height = PanelKit.ChipSwatch, CornerRadius = new CornerRadius(2), Background = Brush(swatchHex), VerticalAlignment = VerticalAlignment.Center };
                if (string.Equals(swatchHex, Theme.TextPrimary, StringComparison.OrdinalIgnoreCase) && pressed)
                {
                    swatch.BorderBrush = Brush(Theme.OnAccent);
                    swatch.BorderThickness = new Thickness(1);
                }
                content = HStack(PanelKit.ChipSwatchGap, swatch, label);
            }
            var button = new Button
            {
                Height = PanelKit.ChipHeight,
                Padding = new Thickness(PanelKit.ChipPaddingX, 0, PanelKit.ChipPaddingX, 0),
                Background = pressed ? Brush(Theme.TextPrimary) : System.Windows.Media.Brushes.Transparent,
                BorderBrush = Brush(pressed ? Theme.TextPrimary : Theme.Border),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                Cursor = Cursors.Hand,
                Content = content,
                Template = ButtonTemplate(pressed ? null : System.Windows.Media.Brushes.Transparent, false),
                FocusVisualStyle = FocusRing(),
            };
            if (!pressed)
            {
                button.MouseEnter += (sender, args) => label.Foreground = Brush(Theme.TextPrimary);
                button.MouseLeave += (sender, args) => label.Foreground = Brush(Theme.TextSecondary);
            }
            // The chip is named by its label, since a swatch chip's content is a panel that gives the button no
            // name of its own; and every chip, with a swatch or without, says whether it is the pressed one: the
            // artboard's aria-pressed, said as RadioRow and ChoiceTile say theirs (#792, #534).
            AutomationName(button, text);
            System.Windows.Automation.AutomationProperties.SetItemStatus(button, pressed ? "checked" : "unchecked");
            if (click != null) button.Click += (sender, args) => click();
            return button;
        }

        /// <summary>A chip led by a colour swatch: a flag, a car alongside, the revs.</summary>
        public static Button SwatchChip(string text, string swatchHex, bool pressed, Action click)
        {
            return Chip(text, pressed, click, swatchHex);
        }

        /// <summary>
        /// One colour of a choice of colours, drawn as the colour itself: a square of it inside an outline, which
        /// is the primary ink on the pressed one and the border elsewhere. Named by <paramref name="name"/>, since
        /// a colour is not a word a screen reader can read off it, and saying whether it is the pressed one (#794).
        /// </summary>
        public static Button ColourSwatch(string name, string hex, bool pressed, Action click)
        {
            var swatch = new Border
            {
                Width = PanelKit.ColourSwatch,
                Height = PanelKit.ColourSwatch,
                CornerRadius = new CornerRadius(2),
                Background = Brush(hex),
            };
            var button = new Button
            {
                Width = PanelKit.ColourSwatch + 2 * (PanelKit.ColourSwatchInset + PanelMetrics.BorderWeight),
                Height = PanelKit.ColourSwatch + 2 * (PanelKit.ColourSwatchInset + PanelMetrics.BorderWeight),
                Padding = new Thickness(PanelKit.ColourSwatchInset),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = Brush(pressed ? Theme.TextPrimary : Theme.Border),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                Cursor = Cursors.Hand,
                Content = swatch,
                ToolTip = name,
                Template = ButtonTemplate(System.Windows.Media.Brushes.Transparent, false),
                FocusVisualStyle = FocusRing(),
            };
            AutomationName(button, name);
            System.Windows.Automation.AutomationProperties.SetItemStatus(button, pressed ? "checked" : "unchecked");
            if (click != null) button.Click += (sender, args) => click();
            return button;
        }

        /// <summary>
        /// The binding chip: what a button is bound to, on the raised ground; a dashed "Not bound" when
        /// nothing is; and, when the bindings could not be read (<paramref name="bound"/> null), a plain
        /// outlined chip carrying <paramref name="text"/> and making no claim either way. A press goes
        /// wherever the caller says, which is Shortcuts.
        /// </summary>
        /// <remarks>
        /// A button, as Screens.dc.html draws it (&lt;button class="chip"&gt;): it takes focus, wears the kit's
        /// focus ring, answers Space and Enter, and fires on a click that began on it, where the Border it was
        /// could not be reached from the keyboard and navigated on a drag released over it. Screens' .chip is
        /// 26 high and 9 in; Shortcuts' and Settings' .key, <paramref name="key"/>, is 28 and 10.
        /// </remarks>
        public static Button BindingChip(string text, bool? bound, Action click = null, bool key = false)
        {
            var known = bound.HasValue;
            var isBound = bound == true;
            var dashed = known && !isBound;
            var label = Text(dashed ? NotBound : text, PanelKit.ChipTextSize, FontWeights.Medium, isBound ? Theme.TextPrimary : Theme.TextSecondary);
            var paddingX = key ? PanelKit.KeyPaddingX : PanelKit.BindingChipPaddingX;
            var chip = new Button
            {
                Height = key ? PanelKit.KeyHeight : PanelKit.BindingChipHeight,
                Padding = new Thickness(paddingX, 0, paddingX, 0),
                Background = isBound ? Brush(Theme.SurfaceRaised) : System.Windows.Media.Brushes.Transparent,
                BorderBrush = known ? System.Windows.Media.Brushes.Transparent : Brush(Theme.Border),
                BorderThickness = new Thickness(known ? 0 : PanelMetrics.BorderWeight),
                Content = label,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Template = ButtonTemplate(null, dashed),
                FocusVisualStyle = FocusRing(),
                Focusable = click != null,
                Cursor = click != null ? Cursors.Hand : Cursors.Arrow,
            };
            AutomationName(chip, dashed ? NotBound : text);
            if (click != null) chip.Click += (sender, args) => click();
            return chip;
        }

        public const string NotBound = PanelBindings.NotBound;

        /// <summary>
        /// A path through SimHub's menus: each step a 22 px crumb on the raised ground, a chevron between
        /// them. Drawn and never typed: a "›" in a literal is a glyph standing in for an icon.
        /// </summary>
        /// <remarks>
        /// A crumb wider than the row, such as a SimHub device name of seventy characters, wraps inside its
        /// box rather than being cut at the row's edge: the WrapPanel measures each crumb at the row's width,
        /// and a crumb's words are the name a driver has to find in SimHub, so none of it may go (#792). A
        /// crumb that fits is drawn as before, one line centred in 22.
        /// </remarks>
        public static WrapPanel Crumbs(params string[] parts)
        {
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    var chevron = NavIcon(PanelIcons.ChevronRight, Theme.TextLabel, 12);
                    chevron.Margin = new Thickness(4, 0, 4, 0);
                    wrap.Children.Add(chevron);
                }
                var words = Text(parts[i], PanelShell.CrumbTextSize, FontWeights.Medium, Theme.TextPrimary);
                words.TextWrapping = TextWrapping.Wrap;
                wrap.Children.Add(new Border
                {
                    MinHeight = PanelShell.CrumbHeight,
                    Padding = new Thickness(PanelShell.CrumbPaddingX, PanelShell.CrumbPaddingY, PanelShell.CrumbPaddingX, PanelShell.CrumbPaddingY),
                    CornerRadius = new CornerRadius(Theme.Radius),
                    Background = Brush(Theme.SurfaceRaised),
                    Margin = new Thickness(0, 2, 0, 2),
                    Child = words,
                });
            }
            return wrap;
        }

        /// <summary>
        /// Numbered steps, 8 apart: a step of one string is a sentence, a step of several is a path of crumbs
        /// (PanelIssue.Steps).
        /// </summary>
        public static StackPanel Steps(IList<string[]> steps)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            if (steps == null) return stack;
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i] ?? new string[0];
                var number = Text((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".", Theme.SizeSmall, FontWeights.Normal, Theme.TextSecondary);
                number.MinWidth = 18;
                number.VerticalAlignment = VerticalAlignment.Top;
                number.Margin = new Thickness(0, step.Length > 1 ? 4 : 0, 0, 0);
                FrameworkElement body = step.Length > 1 ? (FrameworkElement)Crumbs(step) : Prose(step.Length == 1 ? step[0] : string.Empty);
                var dock = new DockPanel { LastChildFill = true, Margin = new Thickness(0, i == 0 ? 0 : 8, 0, 0) };
                DockPanel.SetDock(number, Dock.Left);
                dock.Children.Add(number);
                dock.Children.Add(body);
                stack.Children.Add(dock);
            }
            return stack;
        }

        /// <summary>
        /// The artboards' .step: a numbered stage of a sheet, a rule over it, 20 above and below, the number
        /// in a 22 px ring beside its 16 px title and the body 12 under both. The <paramref name="first"/>
        /// step of a sheet has neither the rule nor the 20 above, as both Add sheets draw their first.
        /// </summary>
        public static Border Step(int number, string title, UIElement body, bool first = false)
        {
            var ring = new Border
            {
                Width = PanelKit.StepNumberSize,
                Height = PanelKit.StepNumberSize,
                CornerRadius = new CornerRadius(PanelKit.StepNumberSize / 2),
                BorderBrush = Brush(Theme.Border),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                Child = Text(number.ToString(System.Globalization.CultureInfo.InvariantCulture), PanelKit.StepNumberTextSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data),
            };
            ((TextBlock)ring.Child).HorizontalAlignment = HorizontalAlignment.Center;
            var head = HStack(PanelKit.StepHeadGap, ring, Text(title, PanelKit.StepTitleSize, FontWeights.SemiBold, Theme.TextPrimary));
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(head);
            if (body != null)
            {
                var host = new ContentControl { Content = body, Margin = new Thickness(0, PanelKit.StepBodyGap, 0, 0) };
                stack.Children.Add(host);
            }
            return new Border
            {
                BorderBrush = Brush(Theme.Rule),
                BorderThickness = new Thickness(0, first ? 0 : PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(0, first ? 0 : PanelKit.StepPaddingY, 0, PanelKit.StepPaddingY),
                Child = stack,
            };
        }

        /// <summary>
        /// The artboards' .fix: what is wrong and the steps that fix it, in the caution's deep outline over
        /// its tint, with the one press that helps on the right.
        /// </summary>
        public static Border FixBox(string title, string detail, IList<string[]> steps, FrameworkElement action, string iconPath = PanelIcons.Warning)
        {
            var icon = NavIcon(iconPath, Theme.Caution);
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, 1, PanelKit.FixIconGap, 0);
            var body = new StackPanel { Orientation = Orientation.Vertical };
            body.Children.Add(Text(title, PanelShell.RowTitleSize, FontWeights.SemiBold, Theme.TextPrimary));
            if (!string.IsNullOrEmpty(detail))
            {
                var cap = Prose(detail);
                cap.Margin = new Thickness(0, 3, 0, 0);
                body.Children.Add(cap);
            }
            if (steps != null && steps.Count > 0)
            {
                var list = Steps(steps);
                list.Margin = new Thickness(0, PanelKit.FixStepsGap, 0, 0);
                body.Children.Add(list);
            }
            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(icon, Dock.Left);
            dock.Children.Add(icon);
            if (action != null)
            {
                action.VerticalAlignment = VerticalAlignment.Top;
                action.Margin = new Thickness(PanelKit.FixIconGap, 0, 0, 0);
                DockPanel.SetDock(action, Dock.Right);
                dock.Children.Add(action);
            }
            dock.Children.Add(body);
            return new Border
            {
                BorderBrush = Brush(Theme.CautionDeep),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                Background = Tint(Theme.Caution, 0.06),
                CornerRadius = new CornerRadius(Theme.Radius),
                Padding = new Thickness(PanelKit.FixPaddingX, PanelKit.FixPaddingY, PanelKit.FixPaddingX, PanelKit.FixPaddingY),
                Child = dock,
            };
        }

        /// <summary>A message line: 13 px in its tone's ink, wrapping (SettingsControl.Say).</summary>
        public static TextBlock Notice(PanelMessage message)
        {
            var block = Prose(message == null ? string.Empty : message.Text, PanelShell.MessageTextSize, PanelMessage.Ink(message == null ? PanelTone.Info : message.Tone));
            block.Margin = new Thickness(0, 0, 0, 8);
            return block;
        }

        // --- Cards ----------------------------------------------------------------------------------

        /// <summary>The artboards' .card: the zone ground inside a rule, at the panel's radius.</summary>
        public static Border CardBox(UIElement child, double padding = 16)
        {
            return new Border
            {
                Background = Brush(Theme.SurfaceZone),
                BorderBrush = Brush(Theme.Rule),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                CornerRadius = new CornerRadius(Theme.Radius),
                Padding = new Thickness(padding),
                Child = child,
            };
        }

        /// <summary>
        /// A screen's card, as Screens.dc.html draws its .scard: a picture, the name, a line of facts and a
        /// state with its dot. Selected, it is outlined in the accent with an accent bar along its foot. LEDs
        /// and Matrix draw cards of their own shape: <see cref="StripCard"/> and <see cref="MatrixCard"/>.
        /// </summary>
        public static Button DeviceCard(FrameworkElement thumb, string name, string meta, string state, string stateHex, bool selected, Action click)
        {
            var rows = new StackPanel { Orientation = Orientation.Vertical };
            if (thumb != null)
            {
                thumb.Margin = new Thickness(0, 0, 0, PanelKit.CardGap);
                rows.Children.Add(thumb);
            }
            var title = Text(name ?? string.Empty, PanelKit.CardNameSize, FontWeights.SemiBold, Theme.TextPrimary);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            rows.Children.Add(title);
            if (!string.IsNullOrEmpty(meta))
            {
                var facts = Text(meta, PanelKit.CardMetaSize, FontWeights.Normal, Theme.TextSecondary);
                facts.TextTrimming = TextTrimming.CharacterEllipsis;
                facts.Margin = new Thickness(0, PanelKit.CardMetaGap, 0, 0);
                rows.Children.Add(facts);
            }
            if (!string.IsNullOrEmpty(state))
            {
                rows.Children.Add(CardState(state, stateHex, PanelKit.CardStateSize, PanelKit.CardStateDot, PanelKit.CardStateLineHeight, PanelKit.CardGap));
            }

            return CardButton(rows, new Thickness(PanelKit.CardPadding), selected, click);
        }

        /// <summary>
        /// A card's state, one layout for the screen, strip and matrix cards: the dot on the first line and the
        /// words beside it, wrapping under it rather than trimmed. Each card passes its own sizes.
        /// </summary>
        /// <remarks>
        /// Docked rather than stacked, so the words have the card's width to wrap in. A card at the grid's
        /// narrowest cannot hold every state on one line, and a state is a step a driver follows ("Restart SimHub
        /// to load it", "Not shown in SimHub"), so it wraps to a second line and is never cut (#524, #541). The
        /// line height is fixed so that the dot, centred in the first line, stays there when the words wrap.
        /// </remarks>
        private static DockPanel CardState(string state, string stateHex, double textSize, double dotSize, double lineHeight, double top)
        {
            var hex = stateHex ?? Theme.TextSecondary;
            var dot = new Ellipse { Width = dotSize, Height = dotSize, Fill = Brush(hex), VerticalAlignment = VerticalAlignment.Top };
            dot.Margin = new Thickness(0, (lineHeight - dotSize) / 2, PanelKit.CardStateGap, 0);
            var word = Text(state, textSize, FontWeights.Normal, hex);
            word.TextWrapping = TextWrapping.Wrap;
            word.LineHeight = lineHeight;
            word.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            var line = new DockPanel { LastChildFill = true, Margin = new Thickness(0, top, 0, 0) };
            DockPanel.SetDock(dot, Dock.Left);
            line.Children.Add(dot);
            line.Children.Add(word);
            return line;
        }

        /// <summary>
        /// A strip's card, as Leds.dc.html draws its .dcard: the name at 15/600 with the shape's numerals at
        /// 14 in the display family on the right of the same line, the strip under it (a picture made with
        /// <see cref="Strip"/> in <see cref="StripStyle.Card"/>), and the state at 12 led by a 7 px dot, the
        /// three 10 apart inside 12 by 14. Selected as every card is, and its state laid out as every card's is
        /// (<see cref="CardState"/>).
        /// </summary>
        public static Button StripCard(string name, string shape, FrameworkElement strip, string state, string stateHex, bool selected, Action click)
        {
            var rows = new StackPanel { Orientation = Orientation.Vertical };
            var head = new DockPanel { LastChildFill = true };
            if (!string.IsNullOrEmpty(shape))
            {
                var numerals = Text(shape, PanelKit.StripCardShapeSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
                numerals.VerticalAlignment = VerticalAlignment.Center;
                numerals.Margin = new Thickness(PanelKit.StripCardGap, 0, 0, 0);
                DockPanel.SetDock(numerals, Dock.Right);
                head.Children.Add(numerals);
            }
            var title = Text(name ?? string.Empty, PanelKit.LightCardNameSize, FontWeights.SemiBold, Theme.TextPrimary);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(title);
            rows.Children.Add(head);
            if (strip != null)
            {
                strip.Margin = new Thickness(0, PanelKit.StripCardGap, 0, 0);
                strip.HorizontalAlignment = HorizontalAlignment.Left;
                rows.Children.Add(strip);
            }
            if (!string.IsNullOrEmpty(state))
            {
                rows.Children.Add(CardState(state, stateHex, PanelKit.LightCardStateSize, PanelKit.LightCardStateDot, PanelKit.LightCardStateLineHeight, PanelKit.StripCardGap));
            }
            return CardButton(rows, new Thickness(PanelKit.LightCardPaddingX, PanelKit.LightCardPaddingY, PanelKit.LightCardPaddingX, PanelKit.LightCardPaddingY), selected, click);
        }

        /// <summary>
        /// A matrix's card, as Matrix.dc.html draws its .mcard: a row, the 8x8 (a picture made with
        /// <see cref="Matrix"/> in <see cref="MatrixStyle.Card"/>) 12 before a column of the name at 15/600
        /// and, 3 under it, the state at 12 in its own ink, inside 12 by 14. The artboard draws the state with no
        /// dot; it is led by the strip card's 7 px dot here, so the three cards lay a state out alike
        /// (<see cref="CardState"/>, #541).
        /// </summary>
        public static Button MatrixCard(FrameworkElement picture, string name, string state, string stateHex, bool selected, Action click)
        {
            var row = new DockPanel { LastChildFill = true };
            if (picture != null)
            {
                picture.Margin = new Thickness(0, 0, PanelKit.MatrixCardGap, 0);
                picture.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(picture, Dock.Left);
                row.Children.Add(picture);
            }
            var words = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            var title = Text(name ?? string.Empty, PanelKit.LightCardNameSize, FontWeights.SemiBold, Theme.TextPrimary);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            words.Children.Add(title);
            if (!string.IsNullOrEmpty(state))
            {
                words.Children.Add(CardState(state, stateHex, PanelKit.LightCardStateSize, PanelKit.LightCardStateDot, PanelKit.LightCardStateLineHeight, PanelKit.MatrixCardTextGap));
            }
            row.Children.Add(words);
            return CardButton(row, new Thickness(PanelKit.LightCardPaddingX, PanelKit.LightCardPaddingY, PanelKit.LightCardPaddingX, PanelKit.LightCardPaddingY), selected, click);
        }

        /// <summary>The press every card is: the zone ground inside a rule, and when selected the accent edge
        /// and the 2 px accent bar along its foot, over the card's own content at its own padding.</summary>
        private static Button CardButton(UIElement content, Thickness padding, bool selected, Action click)
        {
            var bar = new Rectangle { Height = PanelKit.CardFootBar, Fill = selected ? Brush(Theme.Accent) : System.Windows.Media.Brushes.Transparent, VerticalAlignment = VerticalAlignment.Bottom };
            var body = new Grid();
            body.Children.Add(new Border { Padding = padding, Child = content });
            body.Children.Add(bar);

            var button = new Button
            {
                Background = Brush(Theme.SurfaceZone),
                BorderBrush = Brush(selected ? Theme.Accent : Theme.Rule),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                Content = body,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                FocusVisualStyle = FocusRing(),
                Template = CardTemplate(),
            };
            if (click != null) button.Click += (sender, args) => click();
            return button;
        }

        /// <summary>
        /// The dashed tile that adds one, beside Screens' cards: a plus over its words, in secondary ink, and an
        /// optional count after the words, in the display family at 13/600, 8 after them. LEDs and Matrix lay
        /// their tile out on one line: <see cref="InlineAddCard"/>.
        /// </summary>
        /// <remarks>
        /// A tile set IsEnabled = false (Matrix at four) fades to the kit's 40 per cent through its template,
        /// as a disabled button does, and stops answering the pointer.
        /// </remarks>
        public static Button DashedAddCard(string text, Action click, string detail = null)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var plus = NavIcon(PanelIcons.Add, Theme.TextSecondary);
            plus.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(plus);
            var label = Text(text, PanelKit.AddTileTextSize, FontWeights.Medium, Theme.TextSecondary);
            label.Margin = new Thickness(0, PanelKit.AddTileGap, 0, 0);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            TextBlock count = null;
            if (string.IsNullOrEmpty(detail))
            {
                stack.Children.Add(label);
            }
            else
            {
                label.Margin = new Thickness(0);
                count = Text(detail, PanelKit.AddTileDetailSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
                count.VerticalAlignment = VerticalAlignment.Center;
                var words = HStack(PanelKit.AddTileDetailGap, label, count);
                words.Margin = new Thickness(0, PanelKit.AddTileGap, 0, 0);
                words.HorizontalAlignment = HorizontalAlignment.Center;
                stack.Children.Add(words);
            }
            var button = new Button
            {
                MinHeight = PanelKit.AddTileMinHeight,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                Cursor = Cursors.Hand,
                Content = stack,
                Padding = new Thickness(PanelKit.CardPadding),
                FocusVisualStyle = FocusRing(),
                Template = DashedCardTemplate(),
            };
            button.MouseEnter += (sender, args) =>
            {
                if (!button.IsEnabled) return;
                label.Foreground = Brush(Theme.TextPrimary);
                if (count != null) count.Foreground = Brush(Theme.TextPrimary);
                SetIconInk(plus, Theme.TextPrimary);
            };
            button.MouseLeave += (sender, args) =>
            {
                label.Foreground = Brush(Theme.TextSecondary);
                if (count != null) count.Foreground = Brush(Theme.TextSecondary);
                SetIconInk(plus, Theme.TextSecondary);
            };
            if (click != null) button.Click += (sender, args) => click();
            return button;
        }

        /// <summary>
        /// The dashed tile as LEDs and Matrix draw theirs beside their cards: one line, the plus
        /// <paramref name="iconSize"/> wide (Leds.dc.html's 18, Matrix.dc.html's 16) 8 before its words at
        /// 14/500, and an optional count 8 after them in the display family at 13 -- "Add a matrix 2 / 4".
        /// No height of its own: the card grid lays it at its row's height, which the cards beside it set, so
        /// it never stretches them the way <see cref="DashedAddCard"/>'s 96 floor would.
        /// </summary>
        /// <remarks>
        /// A tile set IsEnabled = false (Matrix at four) fades through its template, as the stacked one does.
        /// </remarks>
        public static Button InlineAddCard(string text, double iconSize, Action click, string detail = null)
        {
            var plus = NavIcon(PanelIcons.Add, Theme.TextSecondary, iconSize);
            var label = Text(text, PanelKit.InlineAddTextSize, FontWeights.Medium, Theme.TextSecondary);
            label.VerticalAlignment = VerticalAlignment.Center;
            TextBlock count = null;
            var line = HStack(PanelKit.InlineAddGap, plus, label);
            if (!string.IsNullOrEmpty(detail))
            {
                count = Text(detail, PanelKit.AddTileDetailSize, FontWeights.SemiBold, Theme.TextSecondary, PanelFonts.Data);
                count.VerticalAlignment = VerticalAlignment.Center;
                count.Margin = new Thickness(PanelKit.AddTileDetailGap, 0, 0, 0);
                line.Children.Add(count);
            }
            line.HorizontalAlignment = HorizontalAlignment.Center;
            line.VerticalAlignment = VerticalAlignment.Center;
            var button = new Button
            {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                Cursor = Cursors.Hand,
                Content = line,
                Padding = new Thickness(PanelKit.LightCardPaddingX, PanelKit.LightCardPaddingY, PanelKit.LightCardPaddingX, PanelKit.LightCardPaddingY),
                FocusVisualStyle = FocusRing(),
                Template = DashedCardTemplate(),
            };
            button.MouseEnter += (sender, args) =>
            {
                if (!button.IsEnabled) return;
                label.Foreground = Brush(Theme.TextPrimary);
                if (count != null) count.Foreground = Brush(Theme.TextPrimary);
                SetIconInk(plus, Theme.TextPrimary);
            };
            button.MouseLeave += (sender, args) =>
            {
                label.Foreground = Brush(Theme.TextSecondary);
                if (count != null) count.Foreground = Brush(Theme.TextSecondary);
                SetIconInk(plus, Theme.TextSecondary);
            };
            if (click != null) button.Click += (sender, args) => click();
            return button;
        }

        // --- Choices in a sheet ----------------------------------------------------------------------

        /// <summary>
        /// The sheets' choice tile -- AddScreen's .kind and .tile, AddLeds' .hw: the zone ground at the
        /// panel's radius, a one pixel border at rest and two in the accent when chosen, one of a radio group.
        /// </summary>
        /// <param name="centred">The size tile's column, centred and padded 12/8/10; otherwise the kind and
        /// hardware tile's, left-aligned in 12.</param>
        /// <param name="soon">A tile for something not built yet, from the registry: faded, not answering,
        /// its ticket in the hover.</param>
        public static Button ChoiceTile(UIElement content, bool selected, Action pick, bool centred = false, SoonItem soon = null)
        {
            var edge = selected ? PanelKit.ChoiceTileChosenEdge : PanelMetrics.BorderWeight;
            // The padding takes back what the thicker border adds, so a tile does not move when it is chosen.
            var inset = centred
                ? new Thickness(PanelKit.SizeTilePaddingX, PanelKit.SizeTilePaddingTop, PanelKit.SizeTilePaddingX, PanelKit.SizeTilePaddingBottom)
                : new Thickness(PanelKit.ChoiceTilePadding);
            var less = edge - PanelMetrics.BorderWeight;
            var button = new Button
            {
                Background = Brush(Theme.SurfaceZone),
                BorderBrush = Brush(selected ? Theme.Accent : Theme.Border),
                BorderThickness = new Thickness(edge),
                Padding = new Thickness(inset.Left - less, inset.Top - less, inset.Right - less, inset.Bottom - less),
                Cursor = Cursors.Hand,
                Content = content,
                HorizontalContentAlignment = centred ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
                VerticalContentAlignment = centred ? VerticalAlignment.Bottom : VerticalAlignment.Top,
                FocusVisualStyle = FocusRing(),
                Template = CardTemplate(),
            };
            System.Windows.Automation.AutomationProperties.SetItemStatus(button, selected ? "checked" : "unchecked");
            if (soon != null)
            {
                button.Opacity = PanelShell.SoonOpacity;
                button.IsEnabled = false;
                button.Cursor = Cursors.No;
                button.ToolTip = soon.Tip;
                ToolTipService.SetShowOnDisabled(button, true);
            }
            else if (pick != null)
            {
                button.Click += (sender, args) => pick();
            }
            return button;
        }

        /// <summary>
        /// AddLeds' .dev: a radio row of a device list -- a 14 px ring, the name, and a caption at the right.
        /// Chosen, the ring is filled in the accent, the row is on the zone ground with the accent down its
        /// left edge. A row that cannot be chosen draws a dashed ring and fades to 60 %.
        /// </summary>
        public static Button RadioRow(string name, string meta, bool selected, Action pick, bool enabled = true)
        {
            var ring = new Ellipse
            {
                Width = PanelKit.RadioRing,
                Height = PanelKit.RadioRing,
                Stroke = Brush(selected ? Theme.Accent : Theme.Border),
                StrokeThickness = selected ? 4 : PanelMetrics.BorderWeight,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (!enabled)
            {
                var dashes = new DoubleCollection { 2, 2 };
                dashes.Freeze();
                ring.StrokeDashArray = dashes;
            }
            var dock = new DockPanel { LastChildFill = true };
            ring.Margin = new Thickness(0, 0, PanelKit.RadioRowGap, 0);
            DockPanel.SetDock(ring, Dock.Left);
            dock.Children.Add(ring);
            if (!string.IsNullOrEmpty(meta))
            {
                var caption = Text(meta, PanelKit.RadioMetaSize, FontWeights.Normal, Theme.TextSecondary);
                caption.Margin = new Thickness(PanelKit.RadioRowGap, 0, 0, 0);
                caption.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(caption, Dock.Right);
                dock.Children.Add(caption);
            }
            var label = Text(name ?? string.Empty, PanelKit.RadioNameSize, FontWeights.Normal, enabled ? Theme.TextPrimary : Theme.TextSecondary);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.VerticalAlignment = VerticalAlignment.Center;
            dock.Children.Add(label);

            var bar = new Rectangle { Width = 2, Fill = selected ? Brush(Theme.Accent) : System.Windows.Media.Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Left };
            var body = new Grid();
            body.Children.Add(new Border { Padding = new Thickness(PanelKit.RadioRowPaddingX, PanelKit.RadioRowPaddingY, PanelKit.RadioRowPaddingX, PanelKit.RadioRowPaddingY), Child = dock });
            body.Children.Add(bar);
            var button = new Button
            {
                Background = selected ? Brush(Theme.SurfaceZone) : System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
                Content = body,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                FocusVisualStyle = FocusRing(),
                Template = CardTemplate(),
                IsEnabled = enabled,
                Opacity = enabled ? 1 : 0.6,
            };
            System.Windows.Automation.AutomationProperties.SetName(button, name ?? string.Empty);
            System.Windows.Automation.AutomationProperties.SetItemStatus(button, selected ? "checked" : "unchecked");
            if (pick != null && enabled) button.Click += (sender, args) => pick();
            return button;
        }

        /// <summary>
        /// The pages' .inp: a 30 px text field on the base ground, 10 in from its edges, 13 px Barlow. The
        /// shell's BuildNameBox is the older 24 px field on the field ground; a page drawn to the redesign
        /// uses this.
        /// </summary>
        public static TextBox Input(string text, double width = double.NaN)
        {
            var box = new TextBox { Text = text ?? string.Empty, Width = width };
            Field(box, PanelShell.InputHeight);
            box.Background = Brush(Theme.SurfaceBase);
            box.Padding = new Thickness(PanelShell.InputPaddingX - PanelMetrics.BorderWeight, 0, PanelShell.InputPaddingX - PanelMetrics.BorderWeight, 0);
            box.FontSize = PanelShell.InputTextSize;
            return box;
        }

        /// <summary>
        /// The pages' .num-in: a 64 by 30 number field on the base ground, its value right-aligned in the
        /// display family's SemiBold at 15. It repairs what is typed on Enter or on leaving it, clamped to
        /// the range, and tells <paramref name="changed"/> the repaired value.
        /// </summary>
        public static TextBox NumberInput(int value, int min, int max, Action<int> changed)
        {
            var box = Input(value.ToString(System.Globalization.CultureInfo.InvariantCulture), PanelShell.NumberInputWidth);
            box.Padding = new Thickness(PanelShell.NumberInputPaddingX - PanelMetrics.BorderWeight, 0, PanelShell.NumberInputPaddingX - PanelMetrics.BorderWeight, 0);
            box.FontFamily = PanelFonts.Data;
            box.FontWeight = FontWeights.SemiBold;
            box.FontSize = PanelShell.NumberInputTextSize;
            box.HorizontalContentAlignment = HorizontalAlignment.Right;
            box.TextAlignment = TextAlignment.Right;
            var last = value;
            Action commit = () =>
            {
                int parsed;
                if (!int.TryParse(box.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out parsed)) parsed = last;
                if (parsed < min) parsed = min;
                if (parsed > max) parsed = max;
                box.Text = parsed.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (parsed == last) return;
                last = parsed;
                if (changed != null) changed(parsed);
            };
            box.LostFocus += (sender, args) => commit();
            box.KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter) commit();
            };
            return box;
        }

        /// <summary>The layout the shell last measured, which a card grid reads for its column cap. Set by
        /// SettingsControl on every size change; there is one panel.</summary>
        public static PanelLayout Layout = PanelLayout.Full;

        /// <summary>
        /// A grid of cards that takes as many columns as fit (PanelShell.Columns), with the gap between them
        /// and every card in a row as tall as the tallest.
        /// </summary>
        /// <param name="max">The most columns the page lays its cards in: the artboard's own count (Screens 6 at
        /// a 10 px gap, LEDs 3 and Matrix 3 at 12, Home 3 at 16; Matrix's "2 / 4" is its limit on matrices, not
        /// a column count). The column has no ceiling, so once the columns are all in use the cards stretch to
        /// fill the row rather than another column being added.</param>
        /// <remarks>
        /// A panel of its own rather than a UniformGrid with its Columns reset on SizeChanged: the count is
        /// decided at measure time from the width it is offered, so a resize never lays it out twice, and a
        /// UniformGrid has no gap.
        /// </remarks>
        public static Panel CardGrid(double minCard, double gap, int max, params UIElement[] cards)
        {
            var grid = new CardGridPanel(minCard, gap, max);
            foreach (var card in cards)
            {
                if (card != null) grid.Children.Add(card);
            }
            return grid;
        }

        private sealed class CardGridPanel : Panel
        {
            private readonly double minCard;
            private readonly double gap;
            private readonly int max;
            private int columns = 1;
            private double[] rowHeights = new double[0];

            public CardGridPanel(double minCard, double gap, int max)
            {
                this.minCard = minCard;
                this.gap = gap;
                this.max = max;
            }

            protected override Size MeasureOverride(Size available)
            {
                var width = double.IsInfinity(available.Width) ? minCard * max + gap * (max - 1) : available.Width;
                columns = PanelShell.Columns(width, minCard, gap, max, Ui.Layout);
                var cell = Math.Max(0, (width - gap * (columns - 1)) / columns);
                var count = InternalChildren.Count;
                var rows = (count + columns - 1) / columns;
                rowHeights = new double[rows];
                for (var i = 0; i < count; i++)
                {
                    var child = InternalChildren[i];
                    child.Measure(new Size(cell, double.PositiveInfinity));
                    var row = i / columns;
                    rowHeights[row] = Math.Max(rowHeights[row], child.DesiredSize.Height);
                }
                var height = rowHeights.Sum() + gap * Math.Max(0, rows - 1);
                return new Size(width, height);
            }

            protected override Size ArrangeOverride(Size final)
            {
                var cell = Math.Max(0, (final.Width - gap * (columns - 1)) / columns);
                var y = 0.0;
                for (var i = 0; i < InternalChildren.Count; i++)
                {
                    var row = i / columns;
                    var column = i % columns;
                    if (column == 0 && row > 0) y += rowHeights[row - 1] + gap;
                    var height = row < rowHeights.Length ? rowHeights[row] : 0;
                    InternalChildren[i].Arrange(new Rect(column * (cell + gap), y, cell, height));
                }
                return final;
            }
        }

        // --- Switch and slider ----------------------------------------------------------------------

        /// <summary>
        /// The artboards' .tog: a 40 by 22 pill with a 16 px knob inset 3, the border ground off and the
        /// accent on, the knob secondary off and the page's ink on.
        /// </summary>
        /// <remarks>
        /// A ToggleButton with a template of its own, so every caller that set IsChecked, a tooltip or an
        /// alignment on SimHub's switch keeps working. Space toggles it and it takes the focus ring.
        /// </remarks>
        public static ToggleButton Switch(bool on, Action<bool> changed, double width = PanelShell.SwitchWidth)
        {
            var toggle = new ToggleButton
            {
                Width = width,
                Height = PanelShell.SwitchHeight,
                IsChecked = on,
                Cursor = Cursors.Hand,
                Template = SwitchTemplate(width),
                FocusVisualStyle = FocusRing(),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (changed != null)
            {
                toggle.Checked += (sender, args) => changed(true);
                toggle.Unchecked += (sender, args) => changed(false);
            }
            return toggle;
        }

        private static ControlTemplate SwitchTemplate(double width)
        {
            var track = new FrameworkElementFactory(typeof(Border), "track");
            track.SetValue(Border.CornerRadiusProperty, new CornerRadius(PanelShell.SwitchHeight / 2));
            track.SetValue(Border.BackgroundProperty, Brush(Theme.Border));
            var knob = new FrameworkElementFactory(typeof(Ellipse), "knob");
            knob.SetValue(FrameworkElement.WidthProperty, PanelShell.SwitchKnob);
            knob.SetValue(FrameworkElement.HeightProperty, PanelShell.SwitchKnob);
            knob.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            knob.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            knob.SetValue(FrameworkElement.MarginProperty, new Thickness(PanelShell.SwitchInset, 0, 0, 0));
            knob.SetValue(Shape.FillProperty, Brush(Theme.TextSecondary));
            var root = new FrameworkElementFactory(typeof(Grid));
            root.AppendChild(track);
            root.AppendChild(knob);

            var template = new ControlTemplate(typeof(ToggleButton)) { VisualTree = root };
            var checkedTrigger = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, Brush(Theme.Accent), "track"));
            checkedTrigger.Setters.Add(new Setter(Shape.FillProperty, Brush(Theme.OnAccent), "knob"));
            checkedTrigger.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(width - PanelShell.SwitchKnob - PanelShell.SwitchInset, 0, 0, 0), "knob"));
            template.Triggers.Add(checkedTrigger);
            template.Triggers.Add(DisabledFade());
            return template;
        }

        /// <summary>
        /// A 0 to 100 slider with an accent fill and thumb, and its value as a numeral with a percent sign
        /// beside it. <paramref name="changed"/> is told when the hand lets go or a key moves it;
        /// <paramref name="preview"/>, when given, on every step of a drag.
        /// </summary>
        public static FrameworkElement Slider(int value, Action<int> changed, Action<int> preview = null, bool showValue = true)
        {
            var current = Math.Max(0, Math.Min(100, value));
            var track = new Border { Height = PanelKit.SliderTrack, CornerRadius = new CornerRadius(PanelKit.SliderTrack / 2), Background = Brush(Theme.Border), VerticalAlignment = VerticalAlignment.Center };
            var fill = new Border { Height = PanelKit.SliderTrack, CornerRadius = new CornerRadius(PanelKit.SliderTrack / 2), Background = Brush(Theme.Accent), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            var thumb = new Ellipse { Width = PanelKit.SliderThumb, Height = PanelKit.SliderThumb, Fill = Brush(Theme.Accent), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            // A SliderSurface rather than a bare Grid: a Grid has no automation peer, so the name a page gave the
            // slider reached nothing; the surface is read as a slider with its value, and can be set (#792).
            var surface = new SliderSurface { Height = PanelKit.SliderHeight, Background = System.Windows.Media.Brushes.Transparent, Cursor = Cursors.Hand, Focusable = true, FocusVisualStyle = FocusRing(), MinWidth = PanelKit.SliderMinWidth };
            surface.Children.Add(track);
            surface.Children.Add(fill);
            surface.Children.Add(thumb);
            // One block rather than the tracked Numeral, because the value is rewritten on every step of a drag;
            // the tabular figures keep the digits from shifting as it does.
            TextBlock numeral = null;
            if (showValue)
            {
                numeral = Text(current + "%", PanelKit.SliderValueSize, FontWeights.SemiBold, Theme.TextPrimary, PanelFonts.Data);
                Typography.SetNumeralAlignment(numeral, FontNumeralAlignment.Tabular);
                numeral.TextAlignment = TextAlignment.Right;
            }

            Action paint = () =>
            {
                var width = Math.Max(0, surface.ActualWidth);
                fill.Width = width * current / 100.0;
                thumb.Margin = new Thickness(Math.Max(0, Math.Min(width - thumb.Width, width * current / 100.0 - thumb.Width / 2)), 0, 0, 0);
                if (numeral != null) numeral.Text = current + "%";
                surface.Report(current);
            };
            Action<Point> seek = point =>
            {
                var width = surface.ActualWidth;
                if (width <= 0) return;
                var next = (int)Math.Round(Math.Max(0, Math.Min(1, point.X / width)) * 100);
                if (next == current) return;
                current = next;
                paint();
                if (preview != null) preview(current);
            };
            surface.SizeChanged += (sender, args) => paint();
            // A drag ends in exactly one save: on the release, or when capture is taken away mid-drag (Alt+Tab,
            // a SimHub popup), which would otherwise leave the slider drawing a value the setting never got.
            var dragging = false;
            surface.MouseLeftButtonDown += (sender, args) =>
            {
                surface.Focus();
                dragging = surface.CaptureMouse();
                seek(args.GetPosition(surface));
                args.Handled = true;
            };
            surface.MouseMove += (sender, args) =>
            {
                if (surface.IsMouseCaptured) seek(args.GetPosition(surface));
            };
            surface.LostMouseCapture += (sender, args) =>
            {
                if (!dragging) return;
                dragging = false;
                changed(current);
            };
            surface.MouseLeftButtonUp += (sender, args) =>
            {
                if (!surface.IsMouseCaptured) return;
                surface.ReleaseMouseCapture();
            };
            surface.KeyDown += (sender, args) =>
            {
                var step = args.Key == Key.Left || args.Key == Key.Down ? -5 : args.Key == Key.Right || args.Key == Key.Up ? 5 : 0;
                if (step == 0) return;
                args.Handled = true;
                current = Math.Max(0, Math.Min(100, current + step));
                paint();
                changed(current);
            };
            // A screen reader's set value lands as a key press does: painted, and saved at once.
            surface.Set = next =>
            {
                if (next == current) return;
                current = next;
                paint();
                changed(current);
            };
            surface.Report(current);

            if (numeral == null) return surface;
            var dock = new DockPanel { LastChildFill = true, VerticalAlignment = VerticalAlignment.Center };
            numeral.Margin = new Thickness(PanelKit.SliderValueGap, 0, 0, 0);
            numeral.MinWidth = PanelKit.SliderValueMinWidth;
            DockPanel.SetDock(numeral, Dock.Right);
            dock.Children.Add(numeral);
            dock.Children.Add(surface);
            return dock;
        }

        // --- Reordering -----------------------------------------------------------------------------

        /// <summary>
        /// Rows a hand can reorder by the six-dot grip each one carries: the row follows the pointer, lands
        /// where PanelReorder says, and moved(from, to) is told when it is let go somewhere new.
        /// </summary>
        public static StackPanel Reorderable(IList<FrameworkElement> rows, Action<int, int> moved)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            var hosts = new List<FrameworkElement>();
            for (var i = 0; i < rows.Count; i++)
            {
                var index = i;
                var grip = new Thumb { Width = 16, Height = 28, Cursor = Cursors.SizeNS, Template = GripTemplate(), VerticalAlignment = VerticalAlignment.Center };
                var dock = new DockPanel { LastChildFill = true, Background = System.Windows.Media.Brushes.Transparent };
                DockPanel.SetDock(grip, Dock.Left);
                dock.Children.Add(grip);
                dock.Children.Add(rows[i]);
                var shift = new TranslateTransform();
                dock.RenderTransform = shift;
                hosts.Add(dock);
                stack.Children.Add(dock);

                var travelled = 0.0;
                grip.DragStarted += (sender, args) =>
                {
                    travelled = 0;
                    Panel.SetZIndex(dock, 1);
                };
                grip.DragDelta += (sender, args) =>
                {
                    travelled += args.VerticalChange;
                    shift.Y = travelled;
                };
                grip.DragCompleted += (sender, args) =>
                {
                    shift.Y = 0;
                    Panel.SetZIndex(dock, 0);
                    // A drag the thumb gave up on (capture lost) moves nothing.
                    if (args.Canceled) return;
                    var heights = hosts.Select(h => h.ActualHeight).ToList();
                    var target = PanelReorder.TargetIndex(heights, index, travelled);
                    if (target != index && moved != null) moved(index, target);
                };
            }
            return stack;
        }

        private static ControlTemplate GripTemplate()
        {
            var host = new FrameworkElementFactory(typeof(Border));
            host.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
            var path = new FrameworkElementFactory(typeof(Path));
            path.SetValue(Path.DataProperty, Geometry.Parse(PanelIcons.DragHandle));
            path.SetValue(Shape.StrokeProperty, Brush(Theme.TextLabel));
            path.SetValue(Shape.StrokeThicknessProperty, 2.0);
            path.SetValue(FrameworkElement.WidthProperty, 20.0);
            path.SetValue(FrameworkElement.HeightProperty, 20.0);
            path.SetValue(Shape.StretchProperty, Stretch.None);
            path.SetValue(UIElement.RenderTransformProperty, new ScaleTransform(0.8, 0.8));
            path.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            path.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            host.AppendChild(path);
            return new ControlTemplate(typeof(Thumb)) { VisualTree = host };
        }

        // --- Search ---------------------------------------------------------------------------------

        /// <summary>
        /// A list of suggestions under a text box: up to eight rows, Up and Down to move, Enter to take one,
        /// Escape to put it away, and the empty line when nothing matches.
        /// </summary>
        /// <remarks>
        /// With <paramref name="popup"/> the list is a Popup that closes on a click elsewhere, for the full
        /// sidebar's field; without, it is the list itself, for the rail's flyout, which already is a popup
        /// and cannot hold a second one that closes it.
        /// </remarks>
        public static FrameworkElement Suggestions<T>(TextBox box, Func<string, IList<T>> find, Func<T, string> text, Action<T> pick, string empty, bool popup = true, double width = 320)
        {
            var list = new StackPanel { Orientation = Orientation.Vertical, Width = width };
            var frame = new Border
            {
                Background = Brush(Theme.SurfaceRaised),
                BorderBrush = Brush(Theme.Border),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                CornerRadius = new CornerRadius(Theme.Radius),
                Padding = new Thickness(4),
                Margin = new Thickness(0, 4, 0, 0),
                Child = list,
            };
            Popup host = null;
            if (popup)
            {
                host = new Popup
                {
                    PlacementTarget = box,
                    Placement = PlacementMode.Bottom,
                    StaysOpen = false,
                    AllowsTransparency = true,
                    Child = frame,
                };
            }

            IList<T> hits = new List<T>();
            var selected = -1;
            Action draw = null;
            Action<int> take = index =>
            {
                if (index < 0 || index >= hits.Count) return;
                var hit = hits[index];
                if (host != null) host.IsOpen = false;
                pick(hit);
            };
            draw = () =>
            {
                list.Children.Clear();
                if (hits.Count == 0)
                {
                    var none = Text(empty, 13, FontWeights.Normal, Theme.TextSecondary);
                    none.Margin = new Thickness(10, 8, 10, 8);
                    list.Children.Add(none);
                    return;
                }
                for (var i = 0; i < hits.Count; i++)
                {
                    var index = i;
                    list.Children.Add(ListItem(text(hits[i]), i == selected, () => take(index)));
                }
            };
            Action refresh = () =>
            {
                var query = box.Text ?? string.Empty;
                hits = query.Trim().Length == 0 ? new List<T>() : (find(query) ?? new List<T>());
                selected = hits.Count > 0 ? 0 : -1;
                draw();
                if (host != null) host.IsOpen = query.Trim().Length > 0;
                frame.Visibility = !popup && query.Trim().Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            };
            box.TextChanged += (sender, args) => refresh();
            box.PreviewKeyDown += (sender, args) =>
            {
                switch (args.Key)
                {
                    case Key.Down:
                        if (hits.Count > 0) { selected = (selected + 1) % hits.Count; draw(); }
                        args.Handled = true;
                        break;
                    case Key.Up:
                        if (hits.Count > 0) { selected = selected <= 0 ? hits.Count - 1 : selected - 1; draw(); }
                        args.Handled = true;
                        break;
                    case Key.Enter:
                        take(selected);
                        args.Handled = true;
                        break;
                    case Key.Escape:
                        // Only when there is something to put away. An Escape on an empty box with no list
                        // open is left unhandled, so it reaches whatever holds the box: the rail's flyout,
                        // which closes on it, or the sheet. Marking every Escape handled trapped a keyboard
                        // user in the rail's flyout with an empty field.
                        if ((host == null || !host.IsOpen) && string.IsNullOrEmpty(box.Text)) break;
                        if (host != null) host.IsOpen = false;
                        box.Text = string.Empty;
                        args.Handled = true;
                        break;
                }
            };
            if (host != null)
            {
                // The list goes when focus leaves the box for anything but the list itself: Tab away, and
                // the list is not left open over the page.
                box.LostKeyboardFocus += (sender, args) =>
                {
                    var to = args.NewFocus as Visual;
                    if (to != null && frame.IsAncestorOf(to)) return;
                    host.IsOpen = false;
                };
            }
            if (!popup) frame.Visibility = Visibility.Collapsed;
            return host ?? (FrameworkElement)frame;
        }

        /// <summary>The search field the sidebar draws: a lens, and a box with its placeholder over it.</summary>
        public static Border SearchField(TextBox box, string placeholder)
        {
            box.Background = System.Windows.Media.Brushes.Transparent;
            box.BorderThickness = new Thickness(0);
            box.Foreground = Brush(Theme.TextPrimary);
            box.CaretBrush = Brush(Theme.TextPrimary);
            box.FontFamily = PanelFonts.Label;
            box.FontSize = PanelShell.SearchTextSize;
            box.VerticalContentAlignment = VerticalAlignment.Center;
            box.Padding = new Thickness(0);
            box.Template = BareTextTemplate();
            var hint = Text(placeholder, PanelShell.SearchTextSize, FontWeights.Normal, Theme.TextSecondary);
            hint.IsHitTestVisible = false;
            box.TextChanged += (sender, args) => hint.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            var field = new Grid();
            field.Children.Add(hint);
            field.Children.Add(box);
            var lens = NavIcon(PanelIcons.Search, Theme.TextSecondary, PanelShell.SearchIconSize);
            lens.Margin = new Thickness(0, 0, 8, 0);
            var dock = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(lens, Dock.Left);
            dock.Children.Add(lens);
            dock.Children.Add(field);
            var border = new Border
            {
                Height = PanelShell.SearchHeight,
                Padding = new Thickness(PanelShell.SearchPaddingX, 0, PanelShell.SearchPaddingX, 0),
                Background = Brush(Theme.SurfaceBase),
                BorderBrush = Brush(Theme.Border),
                BorderThickness = new Thickness(PanelMetrics.BorderWeight),
                CornerRadius = new CornerRadius(Theme.Radius),
                Child = dock,
                Cursor = Cursors.IBeam,
            };
            border.MouseLeftButtonDown += (sender, args) => box.Focus();
            box.GotKeyboardFocus += (sender, args) => border.BorderBrush = Brush(Theme.Focus);
            box.LostKeyboardFocus += (sender, args) => border.BorderBrush = Brush(Theme.Border);
            return border;
        }

        private static ControlTemplate BareTextTemplate()
        {
            var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
            host.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            host.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            host.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            return new ControlTemplate(typeof(TextBox)) { VisualTree = host };
        }

        // --- Anchors --------------------------------------------------------------------------------

        /// <summary>The row id search scrolls to, as an attached property rather than the Tag, which several
        /// controls in this kit already use for their own parts.</summary>
        public static readonly DependencyProperty AnchorProperty =
            DependencyProperty.RegisterAttached("Anchor", typeof(string), typeof(Ui), new PropertyMetadata(null));

        /// <summary>Marks an element as the place a route's anchor scrolls to, and returns it.</summary>
        public static T Anchor<T>(T element, string id) where T : DependencyObject
        {
            if (element != null) element.SetValue(AnchorProperty, id);
            return element;
        }

        public static string AnchorOf(DependencyObject element)
        {
            return element == null ? null : element.GetValue(AnchorProperty) as string;
        }

        /// <summary>The name a rebuild in place finds a control by to give it the keyboard focus back (#645): the
        /// setting it writes, set where the row's anchor does not sit above the control, as on the alert table,
        /// whose anchors are on the name cells beside the boxes.</summary>
        public static readonly DependencyProperty FocusKeyProperty =
            DependencyProperty.RegisterAttached("FocusKey", typeof(string), typeof(Ui), new PropertyMetadata(null));

        /// <summary>Names an element for the focus a rebuild hands back, and returns it.</summary>
        public static T FocusKey<T>(T element, string key) where T : DependencyObject
        {
            if (element != null) element.SetValue(FocusKeyProperty, key);
            return element;
        }

        /// <summary>The key PanelFocus records an element by: its FocusKey, or else its search anchor, which every
        /// row of a page carries.</summary>
        public static string FocusKeyOf(DependencyObject element)
        {
            if (element == null) return null;
            return element.GetValue(FocusKeyProperty) as string ?? AnchorOf(element);
        }

        /// <summary>The first element under <paramref name="root"/> carrying that anchor, or null.</summary>
        public static FrameworkElement FindAnchor(DependencyObject root, string id)
        {
            if (root == null || string.IsNullOrEmpty(id)) return null;
            if (string.Equals(AnchorOf(root), id, StringComparison.Ordinal)) return root as FrameworkElement;
            // The logical tree, which is every panel's children, every border's child and every content
            // control's content, whether or not a template has been applied to it yet.
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                var found = FindAnchor(child, id);
                if (found != null) return found;
            }
            return null;
        }
    }
}
