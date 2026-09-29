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

        [Fact]
        public void The_add_tile_is_the_artboards_dashed_link()
        {
            // The dashed <a> beside the cards: gap 6px, font 500 13px.
            Assert.Equal(6, PanelKit.AddTileGap);
            Assert.Equal(13, PanelKit.AddTileTextSize);
        }

        [Fact]
        public void The_chips_are_the_artboards_chip_and_key()
        {
            // Rig's .chip{height:30px;padding:0 11px;font:500 13px}.
            Assert.Equal(30, PanelKit.ChipHeight);
            Assert.Equal(11, PanelKit.ChipPaddingX);
            Assert.Equal(13, PanelKit.ChipTextSize);
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
        }

        [Fact]
        public void The_sheet_choices_are_the_artboards_dev_kind_and_tile()
        {
            // AddLeds' .dev{gap:12px;padding:10px 12px}.
            Assert.Equal(12, PanelKit.RadioRowPaddingX);
            Assert.Equal(10, PanelKit.RadioRowPaddingY);
            Assert.Equal(12, PanelKit.RadioRowGap);
            // AddScreen's .kind and AddLeds' .hw{padding:12px}; .tile{padding:12px 8px 10px}.
            Assert.Equal(12, PanelKit.ChoiceTilePadding);
            Assert.Equal(12, PanelKit.SizeTilePaddingTop);
            Assert.Equal(8, PanelKit.SizeTilePaddingX);
            Assert.Equal(10, PanelKit.SizeTilePaddingBottom);
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
        }

        private static string Kit() => File.ReadAllText(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "Widgets.Kit.cs"));

        /// <summary>The body of one kit factory, from its signature to the next member's doc comment.</summary>
        private static string Factory(string name)
        {
            var kit = Kit();
            var start = Regex.Match(kit, @"public static \w+ " + name + @"\(");
            Assert.True(start.Success, name + " is a kit factory");
            var next = kit.IndexOf("/// <summary>", start.Index, StringComparison.Ordinal);
            return next < 0 ? kit.Substring(start.Index) : kit.Substring(start.Index, next - start.Index);
        }

        [Theory]
        [InlineData("DeviceCard", "PanelKit.CardPadding")]
        [InlineData("DeviceCard", "PanelKit.CardFootBar")]
        [InlineData("DashedAddCard", "PanelKit.AddTileGap")]
        [InlineData("Chip", "PanelKit.ChipHeight")]
        [InlineData("BindingChip", "PanelKit.BindingChipHeight")]
        [InlineData("BindingChip", "PanelKit.KeyHeight")]
        [InlineData("FixBox", "PanelKit.FixPaddingY")]
        [InlineData("RadioRow", "PanelKit.RadioRowPaddingY")]
        [InlineData("ChoiceTile", "PanelKit.SizeTilePaddingTop")]
        [InlineData("Step", "PanelKit.StepTitleSize")]
        [InlineData("Slider", "PanelKit.SliderThumb")]
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
    }
}
