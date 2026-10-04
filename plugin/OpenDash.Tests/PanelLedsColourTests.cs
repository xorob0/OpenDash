// PanelLedsColourTests.cs: the LEDs page's Colours section (#694) -- which colours a strip is offered, what
// each row offers, what the line after a press says, and that the preview draws the strip's own colours.
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelLedsColourTests
    {
        private static LedColour Colour(string key)
        {
            return Contract.FindLedColour(key);
        }

        [Fact]
        public void A_strip_is_offered_the_colours_of_what_it_draws()
        {
            var keys = PanelLeds.ColoursFor("4-14-4").Select(c => c.Key).ToList();
            Assert.Equal(Contract.LedColours.Select(c => c.Key), keys);
            // A side of two draws no aid, so it is offered no aid's colour, as it is offered no aid's switch.
            var two = PanelLeds.ColoursFor("2-10-2").Select(c => c.Key).ToList();
            foreach (var aid in new[] { "abs", "tc", "drs", "p2p" }) Assert.DoesNotContain(aid, two);
            Assert.Contains("flag.yellow", two);
            Assert.Contains("lowFuel", two);
            // A side of one draws the turn signal as the green flag, and offers no switch for it, nor a colour.
            Assert.DoesNotContain("turn", PanelLeds.ColoursFor("1-10-1").Select(c => c.Key));
            // A bare run keeps the flags, the spotter and the pit, and the fuel centre's low fuel.
            var bare = PanelLeds.ColoursFor("0-15-0").Select(c => c.Key).ToList();
            Assert.Contains("flag.red", bare);
            Assert.Contains("spotter", bare);
            Assert.Contains("pit.limiter", bare);
            Assert.Contains("lowFuel", bare);
            Assert.DoesNotContain("abs", bare);
        }

        [Fact]
        public void A_row_offers_its_default_first_and_every_other_hue_once()
        {
            var abs = PanelLeds.ColourChoicesFor(Colour("abs"));
            Assert.Equal(PanelLeds.DefaultColourName, abs[0].Name);
            Assert.Equal("#FF6A00", abs[0].Hex);
            // The palette's orange is the default's own colour, so it is not offered twice.
            Assert.DoesNotContain(abs.Skip(1), c => c.Hex == "#FF6A00");
            Assert.Equal(PanelLeds.ColourPalette.Count, abs.Count);
            Assert.Equal(abs.Count, abs.Select(c => c.Hex).Distinct().Count());
            // The black flag's default is no hue of the palette, so every hue is offered beside it.
            Assert.Equal(PanelLeds.ColourPalette.Count + 1, PanelLeds.ColourChoicesFor(Colour("flag.black")).Count);
            // Exactly one choice is pressed: the default where the strip draws it, else the hue.
            Assert.Equal(PanelLeds.DefaultColourName, PanelLeds.PressedChoice(Colour("abs"), "#FF6A00").Name);
            Assert.Equal("blue", PanelLeds.PressedChoice(Colour("abs"), "#2E7BFF").Name);
            Assert.Null(PanelLeds.PressedChoice(Colour("abs"), "#123456"));
        }

        [Fact]
        public void The_line_after_a_press_says_what_the_strip_draws_now()
        {
            var abs = Colour("abs");
            var blue = PanelLeds.ColourChoicesFor(abs).Single(c => c.Name == "blue");
            var reset = PanelLeds.ColourChoicesFor(abs)[0];
            Assert.Equal("Rim draws ABS in blue.", PanelLeds.ColourSaid("Rim", abs, blue));
            Assert.Equal("Rim draws ABS in its default colour again.", PanelLeds.ColourSaid("Rim", abs, reset));
            Assert.Equal("Rim draws ABS in blue.", PanelLeds.ColourSaid("Rim", abs, blue, true, "Wheel"));
            Assert.Equal("Rim draws ABS in blue. Select \"Rim\" on Wheel in SimHub to use it.", PanelLeds.ColourSaid("Rim", abs, blue, false, "Wheel"));
            Assert.Equal("Rim draws ABS in blue, but its profile could not be installed again. See SimHub's log.", PanelLeds.ColourNotInstalled("Rim", abs, blue));
        }

        [Fact]
        public void The_preview_draws_the_strips_own_colours()
        {
            var bar = new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3" };
            bar.SetColour("spotter", "#FFFFFF");
            bar.SetColour("flag.red", "#2E7BFF");
            bar.SetColour("lowFuel", "#00D96A");
            var options = PanelLeds.OptionsFor(false, null, null, bar.Colours);
            Assert.All(PanelEmulation.StripFrame(3, 9, PanelEmulation.CarLeft, options)[0], led => Assert.Equal("#FFFFFF", led));
            Assert.All(PanelEmulation.StripFrame(3, 9, PanelEmulation.Red, options)[2], led => Assert.Equal("#2E7BFF", led));
            Assert.Equal("#00D96A", PanelEmulation.StripFrame(3, 9, PanelEmulation.LowFuel, options)[0][2]);
            // What it has not changed is the default.
            Assert.Equal(Theme.LightTemperature, PanelEmulation.StripFrame(3, 9, PanelEmulation.Oil, options)[0][2]);
            Assert.Equal(Theme.FlagBlue, PanelEmulation.StripFrame(3, 9, PanelEmulation.Blue, options)[0][1]);
            // The options hold a copy: a pick after the preview was drawn does not move it.
            bar.SetColour("spotter", null);
            Assert.Equal("#FFFFFF", options.Colour("spotter", Theme.LightSpotter));
        }

        [Fact]
        public void The_rig_page_draws_the_strips_own_colours_too()
        {
            var bar = new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3" };
            bar.SetColour("flag.red", "#2E7BFF");
            var options = PanelRigMap.StripOptionsFor(bar);
            Assert.All(PanelEmulation.StripFrame(3, 9, PanelEmulation.Red, options)[0], led => Assert.Equal("#2E7BFF", led));
        }

        [Fact]
        public void Every_scenario_flag_has_a_colour_setting()
        {
            foreach (var id in new[] { PanelEmulation.Green, PanelEmulation.Yellow, PanelEmulation.Blue, PanelEmulation.White, PanelEmulation.Black, PanelEmulation.Chequer, PanelEmulation.Red })
            {
                var colour = Contract.FindLedColour(PanelEmulation.ColourKey(id));
                Assert.NotNull(colour);
            }
            Assert.Null(PanelEmulation.ColourKey(PanelEmulation.CarLeft));
        }
    }
}
