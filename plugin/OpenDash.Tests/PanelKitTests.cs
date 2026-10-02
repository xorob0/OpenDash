// PanelKitTests.cs: the kit's shared controls, held to the CSS classes the #503 artboards draw them with.
//
// The literals are read off the artboards' style blocks (Screens.dc.html's .scard, .chip and .fix,
// Shortcuts.dc.html's .key, AddScreen.dc.html's .kind, .tile, .step and .stepn, AddLeds.dc.html's .dev and
// .seg button), as PanelShellTests reads the frame's. A card, a chip or a step that is the wrong size is caught
// here rather than on the VM. The second half holds Widgets.Kit.cs to drawing them from PanelKit, since the
// kit is WPF and no test compiles it.
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelKitTests
    {
        [Fact]
        public void The_device_card_is_the_artboards_scard()
        {
            // .scard{gap:8px;padding:10px}; the name 14/600 over the kind at 12, 2 apart; the state 11 led by a
            // 6 px dot, 6 apart; the grid's gap 10.
            Assert.Equal(10, PanelKit.CardPadding);
            Assert.Equal(8, PanelKit.CardGap);
            Assert.Equal(14, PanelKit.CardNameSize);
            Assert.Equal(12, PanelKit.CardMetaSize);
            Assert.Equal(2, PanelKit.CardMetaGap);
            Assert.Equal(11, PanelKit.CardStateSize);
            Assert.Equal(6, PanelKit.CardStateDot);
            Assert.Equal(6, PanelKit.CardStateGap);
            Assert.Equal(2, PanelKit.CardFootBar);
            Assert.Equal(10, PanelKit.CardGridGap);
        }

        /// <summary>
        /// Screens.dc.html lays its cards out as repeat(6, minmax(0,1fr)) 10 apart, so at its own 1200 px --
        /// the full sidebar, the gutter, and the scroll bar taken out -- six cards fit, and a five-screen rig
        /// keeps "Add a screen" on the first row. At 150 the grid took five there.
        /// </summary>
        /// <summary>A picture drawn at a fixed size shrinks to a narrower column and never grows past its own
        /// size, set against the column's left edge: grown, the 846 px face filled a 4K column four times over;
        /// centred, it stood in the middle of an empty band.</summary>
        [Fact]
        public void A_fixed_picture_shrinks_to_the_column_and_never_grows()
        {
            var fit = Factory("FitWidth");
            Assert.Contains("new Viewbox", fit);
            Assert.Contains("StretchDirection = StretchDirection.DownOnly", fit);
            Assert.Contains("HorizontalAlignment = HorizontalAlignment.Left", fit);
            Assert.Contains("Stretch = Stretch.Uniform", fit);
        }

        [Fact]
        public void The_card_grid_holds_the_artboards_six_at_its_own_width()
        {
            Assert.Equal(138, PanelKit.CardMinWidth);
            Assert.Equal(6, PanelShell.Columns(PanelShell.ContentWidth(1200, 17), PanelKit.CardMinWidth, PanelKit.CardGridGap, 6));
            Assert.Equal(6, PanelShell.Columns(PanelShell.ContentWidth(1200, 0), PanelKit.CardMinWidth, PanelKit.CardGridGap, 6));
        }

        /// <summary>
        /// The selected card's cue, which PanelMetrics.Card held for the old card before it was deleted: at
        /// rest the one pixel Rule border and no foot bar; selected, the border and the 2 px foot bar in the
        /// accent. Screens.dc.html's card style: 'border: 1px solid #33D9F2; box-shadow: inset 0 -2px 0
        /// #33D9F2;' when on, 'border: 1px solid #1C1F24;' at rest.
        /// </summary>
        [Fact]
        public void A_selected_card_is_edged_and_barred_in_the_accent()
        {
            // Every card is one press, CardButton, which the screen, strip and matrix cards all return.
            foreach (var factory in new[] { "DeviceCard", "StripCard", "MatrixCard" }) Assert.Contains("return CardButton(", Factory(factory));
            var card = Factory("CardButton");
            Assert.Contains("BorderBrush = Brush(selected ? Theme.Accent : Theme.Rule),", card);
            Assert.Contains("Fill = selected ? Brush(Theme.Accent) : System.Windows.Media.Brushes.Transparent", card);
            Assert.Contains("BorderThickness = new Thickness(PanelMetrics.BorderWeight),", card);
            Assert.Contains("Background = Brush(Theme.SurfaceZone),", card);
        }

        /// <summary>A state that does not fit a card at the grid's narrowest wraps to a second line rather than
        /// being trimmed or clipped (#524, ruling 1), and its dot stays on the first line.</summary>
        [Fact]
        public void A_cards_state_line_wraps_and_is_never_trimmed()
        {
            var card = Factory("DeviceCard");
            Assert.Contains("word.TextWrapping = TextWrapping.Wrap;", card);
            Assert.DoesNotContain("word.TextTrimming", card);
            Assert.Contains("VerticalAlignment = VerticalAlignment.Top };", card);
            Assert.Contains("dot.Margin = new Thickness(0, PanelKit.CardStateDotTop, PanelKit.CardStateGap, 0);", card);
            Assert.Equal((PanelKit.CardStateLineHeight - PanelKit.CardStateDot) / 2, PanelKit.CardStateDotTop);
        }

        [Fact]
        public void The_add_tile_is_the_artboards_dashed_link()
        {
            // The dashed <a> beside the cards: gap 6px, font 500 13px.
            Assert.Equal(6, PanelKit.AddTileGap);
            Assert.Equal(13, PanelKit.AddTileTextSize);
            // The artboard sizes the tile from the grid row and gives no minimum; 96 is the kit's own, what
            // the tile holds in a row of cards without pictures.
            Assert.Equal(96, PanelKit.AddTileMinHeight);
            // Matrix.dc.html's "Add a matrix <span class="num" style="font-size: 13px">2 / 4</span>": the
            // button's gap 8px, the .num face at 13.
            Assert.Equal(13, PanelKit.AddTileDetailSize);
            Assert.Equal(8, PanelKit.AddTileDetailGap);
        }

        /// <summary>The Matrix tile's "n / 4" is the kit's, and a tile that cannot add another fades as a
        /// disabled button does, so Matrix at four does not hand-build a tile of its own.</summary>
        [Fact]
        public void The_add_tile_carries_a_count_and_fades_when_it_cannot_add()
        {
            var tile = Factory("DashedAddCard");
            Assert.Contains("string detail = null", tile);
            Assert.Contains("PanelKit.AddTileDetailSize", tile);
            var widgets = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "Widgets.cs"));
            var start = widgets.IndexOf("private static ControlTemplate DashedCardTemplate()", StringComparison.Ordinal);
            var body = widgets.Substring(start, widgets.IndexOf("private static FrameworkElementFactory DashedFrame()", start, StringComparison.Ordinal) - start);
            Assert.Contains("template.Triggers.Add(DisabledFade());", body);
        }

        /// <summary>
        /// LEDs and Matrix draw cards of their own shape, and their add tile on one line, so the kit has them
        /// rather than each page re-implementing a card or taking Screens' numbers. Leds.dc.html's .dcard
        /// {gap:10px;padding:12px 14px}, its name 15/600 beside the shape's .num at 14, and its state 12 led
        /// by a 7 px dot; Matrix.dc.html's .mcard{gap:12px;padding:12px 14px}, the words' column gap 3.
        /// </summary>
        [Fact]
        public void The_light_cards_are_the_artboards_dcard_and_mcard()
        {
            Assert.Equal(14, PanelKit.LightCardPaddingX);
            Assert.Equal(12, PanelKit.LightCardPaddingY);
            Assert.Equal(15, PanelKit.LightCardNameSize);
            Assert.Equal(12, PanelKit.LightCardStateSize);
            Assert.Equal(10, PanelKit.StripCardGap);
            Assert.Equal(14, PanelKit.StripCardShapeSize);
            Assert.Equal(7, PanelKit.StripCardStateDot);
            Assert.Equal(12, PanelKit.MatrixCardGap);
            Assert.Equal(3, PanelKit.MatrixCardTextGap);
            // The dashed tile on one line: "gap: 8px ... font-size: 14px; font-weight: 500", the plus an
            // 18 px svg on LEDs and 16 on Matrix, and no height of its own.
            Assert.Equal(14, PanelKit.InlineAddTextSize);
            Assert.Equal(8, PanelKit.InlineAddGap);
            Assert.Equal(18, PanelKit.StripAddIcon);
            Assert.Equal(16, PanelKit.MatrixAddIcon);
            Assert.DoesNotContain("MinHeight", Factory("InlineAddCard"));
            Assert.Contains("Template = DashedCardTemplate(),", Factory("InlineAddCard"));
            // The pictures inside them are the kit's in the cards' own styles.
            Assert.Equal(9, StripStyle.Card.Led);
            Assert.Equal(5, MatrixStyle.Card.Cell);
        }

        /// <summary>
        /// Where one page's artboard draws a shared control at other numbers than the kit's, the number is
        /// named here and the page passes or sets it, rather than the kit fitting one artboard and the others
        /// typing theirs. Matrix's .seg is 28 high; Screens pads options 13 and Shortcuts 14; LEDs' and
        /// Matrix's .chip pad 12 and their .fix 16; LEDs' .row gap is 20 and Updates' .row pads 14; Settings'
        /// h2 is 19 with 12 under, and its &lt;main&gt; ends 40 down.
        /// </summary>
        [Fact]
        public void A_page_draws_a_shared_control_at_its_own_artboards_numbers()
        {
            Assert.Equal(28, PanelKit.SegmentedHeightMatrix);
            Assert.Equal(13, PanelKit.SegmentedPaddingScreens);
            Assert.Equal(14, PanelKit.SegmentedPaddingShortcuts);
            Assert.Equal(12, PanelKit.ChipPaddingXLights);
            Assert.Equal(20, PanelKit.RowGapLeds);
            Assert.Equal(14, PanelKit.RowPaddingYUpdates);
            Assert.Equal(16, PanelKit.FixPaddingYLights);
            Assert.Equal(14, PanelKit.SectionHeadingGap);
            Assert.Equal(12, PanelKit.SectionHeadingGapSettings);
            Assert.Equal(19, PanelShell.HeadingLargeSize);
            Assert.Equal(40, PanelShell.MainPaddingBottomFor(PanelPage.Settings));
            Assert.Equal(32, PanelShell.MainPaddingBottomFor(PanelPage.Leds));

            var root = Path.Combine(RepoPaths.Root(), "plugin", "OpenDash");
            var segmented = RepoPaths.Code(Path.Combine(root, "Segmented.cs"));
            Assert.Contains("public const double BarHeight = 30;", segmented);
            Assert.Contains("public const double OptionPadding = 12;", segmented);
            Assert.Contains("double height = BarHeight, double padding = OptionPadding", segmented);
            Assert.Contains("Height = height;", segmented);
            Assert.Contains("text.Margin = new Thickness(optionPadding, 0, optionPadding, 0);", segmented);
            var shell = RepoPaths.Code(Path.Combine(root, "SettingsControl.cs"));
            Assert.Contains("private static FrameworkElement PageSection(string heading, bool large, double gapBelow, params UIElement[] children)", shell);
            Assert.Contains("PanelShell.MainPaddingBottomFor(route.Page)", shell);
        }

        [Fact]
        public void The_chips_are_the_artboards_chip_and_key()
        {
            // Rig's .chip{height:30px;padding:0 11px;font:500 13px}.
            Assert.Equal(30, PanelKit.ChipHeight);
            Assert.Equal(11, PanelKit.ChipPaddingX);
            Assert.Equal(13, PanelKit.ChipTextSize);
            // Rig's .sw{width:10px;height:10px} inside the .chip's gap:6px.
            Assert.Equal(10, PanelKit.ChipSwatch);
            Assert.Equal(6, PanelKit.ChipSwatchGap);
            // Screens' .chip{height:26px;padding:0 9px}: the binding chip on a zone and a glance.
            Assert.Equal(26, PanelKit.BindingChipHeight);
            Assert.Equal(9, PanelKit.BindingChipPaddingX);
            // Shortcuts' and Settings' .key{height:28px;padding:0 10px}: the same chip as a row's subject.
            Assert.Equal(28, PanelKit.KeyHeight);
            Assert.Equal(10, PanelKit.KeyPaddingX);
        }

        [Fact]
        public void The_fix_box_is_the_artboards_fix()
        {
            // Screens' .fix{gap:16px;padding:14px 18px}.
            Assert.Equal(18, PanelKit.FixPaddingX);
            Assert.Equal(14, PanelKit.FixPaddingY);
            Assert.Equal(16, PanelKit.FixIconGap);
            // Inside the .fix's text column: the detail 3 under the title, and the steps' column gap of 10.
            Assert.Equal(3, PanelKit.FixDetailGap);
            Assert.Equal(10, PanelKit.FixStepsGap);
        }

        [Fact]
        public void The_sheet_choices_are_the_artboards_dev_kind_and_tile()
        {
            // AddLeds' .dev{gap:12px;padding:10px 12px}.
            Assert.Equal(12, PanelKit.RadioRowPaddingX);
            Assert.Equal(10, PanelKit.RadioRowPaddingY);
            Assert.Equal(12, PanelKit.RadioRowGap);
            // The .dev's ring is 14px, its name 14 and its caption 12.
            Assert.Equal(14, PanelKit.RadioRing);
            Assert.Equal(14, PanelKit.RadioNameSize);
            Assert.Equal(12, PanelKit.RadioMetaSize);
            // AddScreen's .kind and AddLeds' .hw{padding:12px}; .tile{padding:12px 8px 10px}.
            Assert.Equal(12, PanelKit.ChoiceTilePadding);
            Assert.Equal(12, PanelKit.SizeTilePaddingTop);
            Assert.Equal(8, PanelKit.SizeTilePaddingX);
            Assert.Equal(10, PanelKit.SizeTilePaddingBottom);
            // A chosen .kind or .tile is edged "2px solid" in the accent.
            Assert.Equal(2, PanelKit.ChoiceTileChosenEdge);
            // AddLeds' .seg button{min-width:40px}: the ends buttons.
            Assert.Equal(40, PanelKit.SegmentMinWidth);
        }

        [Fact]
        public void A_step_is_the_artboards_step_with_its_title_at_sixteen()
        {
            // .step{gap:12px;padding:20px 0}; .stepn{width:22px;height:22px;font:600 12px}; every step title
            // on both Add sheets is 16/600.
            Assert.Equal(20, PanelKit.StepPaddingY);
            Assert.Equal(12, PanelKit.StepBodyGap);
            Assert.Equal(16, PanelKit.StepTitleSize);
            Assert.Equal(22, PanelKit.StepNumberSize);
            Assert.Equal(12, PanelKit.StepNumberTextSize);
            // The step's head: the .stepn 10 before the title.
            Assert.Equal(10, PanelKit.StepHeadGap);
        }

        /// <summary>
        /// The slider is the kit's own: the artboards draw the browser's range input in the accent and give
        /// no numbers, so these are pinned as the kit chose them rather than joined to an artboard, so that a
        /// page cannot move one without a red test saying so.
        /// </summary>
        [Fact]
        public void The_slider_is_drawn_at_the_kits_own_numbers()
        {
            Assert.Equal(4, PanelKit.SliderTrack);
            Assert.Equal(14, PanelKit.SliderThumb);
            Assert.Equal(22, PanelKit.SliderHeight);
            Assert.Equal(120, PanelKit.SliderMinWidth);
            Assert.Equal(12, PanelKit.SliderValueGap);
            Assert.Equal(15, PanelKit.SliderValueSize);
            Assert.Equal(40, PanelKit.SliderValueMinWidth);
        }

        /// <summary>The progress bar is headed by the word its caller passes, "Installing" unless told otherwise,
        /// so a page whose run is a download says so without a copy of the bar (#523).</summary>
        [Fact]
        public void The_progress_bar_takes_its_head_word()
        {
            var widgets = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "Widgets.cs"));
            Assert.Contains("public static FrameworkElement Progress(double fraction, double width = PanelMetrics.ProgressWidth, string word = PanelCopy.Installing)", widgets);
            Assert.Contains("var head = Row(Label(word, Theme.TextPrimary),", widgets);
        }

        /// <summary>A strip's picture is repainted in place beside the tree that draws it (#523): every LED is
        /// collected before any is set, a tree of another shape is refused whole, and an unlit LED takes the
        /// style's unlit colour.</summary>
        [Fact]
        public void A_strip_is_relit_in_place_by_the_kit_that_drew_it()
        {
            var lights = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "Widgets.Lights.cs"));
            var flat = Regex.Replace(lights, @"\s+", " ");
            Assert.Contains("public static bool Relight(Border picture, IList<string[]> frame, StripStyle style)", flat);
            Assert.Contains("var row = picture == null ? null : picture.Child as Panel; if (row == null || frame == null || row.Children.Count != frame.Count) return false;", flat);
            Assert.Contains("if (group == null || group.Children.Count != lit.Length) return false;", flat);
            Assert.Contains("foreach (var hex in lit ?? new string[0]) leds[at++].Background = Brush(hex ?? style.UnlitHex); } return true; }", flat);
        }

        /// <summary>A press's label neither wraps nor trims unless the caller asks it to trim, and then it is a
        /// text ending in an ellipsis, so no page sets its own content to cap a name (#523).</summary>
        [Fact]
        public void A_press_trims_its_label_only_when_asked()
        {
            var button = Regex.Replace(Factory("Button"), @"\s+", " ");
            Assert.Contains("string iconPath = null, bool trims = false)", button);
            Assert.Contains(": trims ? new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis } : (object)text,", button);
        }

        /// <summary>A crumb longer than its row wraps inside its box rather than being cut at the row's edge, and
        /// one that fits keeps the 22 px box (#523).</summary>
        [Fact]
        public void A_crumb_longer_than_its_row_wraps()
        {
            var crumbs = Regex.Replace(Factory("Crumbs"), @"\s+", " ");
            Assert.Contains("words.TextWrapping = TextWrapping.Wrap;", crumbs);
            Assert.Contains("MinHeight = PanelShell.CrumbHeight,", crumbs);
            Assert.Contains("Padding = new Thickness(PanelShell.CrumbPaddingX, PanelShell.CrumbPaddingY, PanelShell.CrumbPaddingX, PanelShell.CrumbPaddingY),", crumbs);
            Assert.DoesNotContain(" Height = PanelShell.CrumbHeight", crumbs);
            Assert.DoesNotContain("TextTrimming", crumbs);
        }

        /// <summary>
        /// The kit's controls that were bare panels have automation peers, so a name a page gives them reaches a
        /// screen reader (#523): a greyed row's Soon wrapper is a named group, the slider's surface a slider with
        /// the RangeValue pattern, the segmented bar a group with the Value pattern, and a swatch chip is named by
        /// its label and says whether it is pressed. The net48 controls cannot be built here, so the peers are
        /// held by their source.
        /// </summary>
        [Fact]
        public void The_kits_bare_panels_have_automation_peers()
        {
            var root = Path.Combine(RepoPaths.Root(), "plugin", "OpenDash");
            var peers = Regex.Replace(RepoPaths.Code(Path.Combine(root, "Widgets.Automation.cs")), @"\s+", " ");
            Assert.Contains("internal sealed class GroupBorder : Border { protected override AutomationPeer OnCreateAutomationPeer() { return new GroupBorderPeer(this); }", peers);
            Assert.Contains("protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.Group; }", peers);
            Assert.Contains("private sealed class SliderSurfacePeer : FrameworkElementAutomationPeer, IRangeValueProvider", peers);
            Assert.Contains("protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.Slider; }", peers);
            Assert.Contains("return patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);", peers);
            Assert.Contains("internal sealed class SegmentedPeer : FrameworkElementAutomationPeer, IValueProvider", peers);
            Assert.Contains("return patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);", peers);

            Assert.Contains("var wrapper = new GroupBorder", Factory("SoonWith"));
            Assert.Contains("if (title != null) AutomationName(wrapper, title);", Factory("SoonWith"));
            var slider = Regex.Replace(Factory("Slider"), @"\s+", " ");
            Assert.Contains("var surface = new SliderSurface {", slider);
            Assert.Contains("surface.Report(current);", slider);
            Assert.Contains("surface.Set = next =>", slider);
            var chip = Regex.Replace(Factory("Chip"), @"\s+", " ");
            Assert.Contains("if (swatchHex != null) { AutomationName(button, text); System.Windows.Automation.AutomationProperties.SetItemStatus(button, pressed ? \"checked\" : \"unchecked\"); }", chip);
            Assert.Contains("return new SegmentedPeer(this);", RepoPaths.Code(Path.Combine(root, "Segmented.cs")));
            // The two assemblies the patterns live in are referenced, or the plugin does not build.
            var project = File.ReadAllText(Path.Combine(root, "OpenDash.csproj"));
            Assert.Contains("<Reference Include=\"UIAutomationProvider\" />", project);
            Assert.Contains("<Reference Include=\"UIAutomationTypes\" />", project);
        }

        /// <summary>The kit as code alone, so a comment naming a constant or a call cannot hold a pin up.</summary>
        private static string Kit() => RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "Widgets.Kit.cs"));

        /// <summary>The body of one kit factory, from its signature to the next member declared beside it. The
        /// next doc comment was the end until the comments were stripped, and without one a factory's body ran
        /// to the end of the file, where every other factory's constants would have held its pins.</summary>
        private static string Factory(string name)
        {
            var kit = Kit();
            var start = Regex.Match(kit, @"(?:public|private) static \w+ " + name + @"\(");
            Assert.True(start.Success, name + " is a kit factory");
            var next = Regex.Match(kit.Substring(start.Index + start.Length), @"\n {8}(?:public|private|internal|protected)\b");
            Assert.True(next.Success, name + " is followed by another member");
            return kit.Substring(start.Index, start.Length + next.Index);
        }

        [Theory]
        [InlineData("DeviceCard", "PanelKit.CardPadding")]
        [InlineData("CardButton", "PanelKit.CardFootBar")]
        [InlineData("StripCard", "PanelKit.LightCardPaddingX")]
        [InlineData("StripCard", "PanelKit.LightCardPaddingY")]
        [InlineData("StripCard", "PanelKit.LightCardNameSize")]
        [InlineData("StripCard", "PanelKit.LightCardStateSize")]
        [InlineData("StripCard", "PanelKit.StripCardGap")]
        [InlineData("StripCard", "PanelKit.StripCardShapeSize")]
        [InlineData("StripCard", "PanelKit.StripCardStateDot")]
        [InlineData("MatrixCard", "PanelKit.LightCardPaddingX")]
        [InlineData("MatrixCard", "PanelKit.LightCardNameSize")]
        [InlineData("MatrixCard", "PanelKit.LightCardStateSize")]
        [InlineData("MatrixCard", "PanelKit.MatrixCardGap")]
        [InlineData("MatrixCard", "PanelKit.MatrixCardTextGap")]
        [InlineData("InlineAddCard", "PanelKit.InlineAddTextSize")]
        [InlineData("InlineAddCard", "PanelKit.InlineAddGap")]
        [InlineData("InlineAddCard", "PanelKit.AddTileDetailSize")]
        [InlineData("InlineAddCard", "PanelKit.AddTileDetailGap")]
        [InlineData("DashedAddCard", "PanelKit.AddTileGap")]
        [InlineData("Chip", "PanelKit.ChipHeight")]
        [InlineData("BindingChip", "PanelKit.BindingChipHeight")]
        [InlineData("BindingChip", "PanelKit.KeyHeight")]
        [InlineData("FixBox", "PanelKit.FixPaddingY")]
        [InlineData("RadioRow", "PanelKit.RadioRowPaddingY")]
        [InlineData("ChoiceTile", "PanelKit.SizeTilePaddingTop")]
        [InlineData("Step", "PanelKit.StepTitleSize")]
        [InlineData("Slider", "PanelKit.SliderThumb")]
        [InlineData("Slider", "PanelKit.SliderTrack")]
        [InlineData("Slider", "PanelKit.SliderHeight")]
        [InlineData("Slider", "PanelKit.SliderMinWidth")]
        [InlineData("DashedAddCard", "PanelKit.AddTileMinHeight")]
        public void The_kit_draws_each_control_from_its_constants(string factory, string constant)
        {
            Assert.Contains(constant, Factory(factory));
        }

        /// <summary>The binding chip is a press the keyboard can reach: a Button with the kit's focus ring and
        /// a Click, as Screens.dc.html draws it, and not a Border answering MouseUp.</summary>
        [Fact]
        public void The_binding_chip_is_a_button()
        {
            var body = Factory("BindingChip");
            Assert.Contains("new Button", body);
            Assert.Contains("FocusRing()", body);
            Assert.Contains(".Click +=", body);
            Assert.DoesNotContain("MouseLeftButtonUp", body);
        }

        /// <summary>
        /// Nothing in the panel acts on a release it did not see pressed. A segmented option chose on any
        /// MouseLeftButtonUp over it, so the click that dismissed a sheet's dim -- pressed on the dim, released
        /// on what the collapsed layer had covered -- rewrote the setting under the pointer. Every handler of
        /// the release sits in a file that takes the mouse on the press, and reads that capture on the release.
        /// </summary>
        [Fact]
        public void No_release_acts_without_a_press_on_the_same_control()
        {
            var sources = Directory.GetFiles(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash"), "*.cs");
            var handlers = 0;
            foreach (var path in sources)
            {
                var text = RepoPaths.Code(path);
                foreach (Match up in Regex.Matches(text, @"(\w+)\.MouseLeftButtonUp \+="))
                {
                    handlers++;
                    var control = up.Groups[1].Value;
                    var name = Path.GetFileName(path) + " " + control;
                    Assert.True(Regex.IsMatch(text, control + @"\.MouseLeftButtonDown \+="), name + " is pressed before it is released");
                    Assert.True(Regex.IsMatch(text, @"\b" + control + @"\.CaptureMouse\(\)"), name + " takes the mouse on the press");
                    var body = text.Substring(up.Index, Math.Min(400, text.Length - up.Index));
                    Assert.True(body.Contains(control + ".IsMouseCaptured"), name + " acts only on the release that ends its own press");
                }
            }
            Assert.True(handlers >= 2, "the segmented option and the sheet's dim answer a release");
        }

        /// <summary>The segmented bar is one of them by name, and the sheet's dim closes on the matching
        /// release and marks the press handled, so the release that dismisses the sheet reaches nothing.</summary>
        [Fact]
        public void A_segmented_option_and_the_dim_act_on_their_own_press_and_release()
        {
            var root = Path.Combine(RepoPaths.Root(), "plugin", "OpenDash");
            var segmented = RepoPaths.Code(Path.Combine(root, "Segmented.cs"));
            Assert.Contains("box.MouseLeftButtonDown +=", segmented);
            Assert.Contains("box.CaptureMouse()", segmented);
            Assert.Contains("if (!box.IsMouseCaptured) return;", segmented);
            var sheet = RepoPaths.Code(Path.Combine(root, "SettingsControl.Sheet.cs"));
            Assert.DoesNotContain("dim.MouseLeftButtonDown += (sender, args) => CloseSheet();", sheet);
            Assert.Contains("dim.CaptureMouse()", sheet);
            Assert.Contains("if (!dim.IsMouseCaptured) return;", sheet);
        }
    }
}
