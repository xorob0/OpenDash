// LedBarColourTests.cs: a strip's own colours (#794) -- what a bar stores, and the rewrite that writes them
// into the profile it installs.
//
// The rewrite is held against the profile the build wrote, as LedBarTests holds the namespace rewrite,
// because what it has to find is the generator's own containers: an effect's label as a description, the
// suffixed forms of it, and the default as the lit colour beside an off phase or a second colour that must
// not move.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class LedBarColourTests
    {
        private static string BuiltProfile(string shape)
        {
            foreach (var folder in new[] { RepoPaths.EmbeddedResources(), RepoPaths.BuildOutput() })
            {
                if (!Directory.Exists(folder)) continue;
                var file = Directory.GetFiles(folder, "*" + FlagBoxProfile.ProfileExtension)
                    .FirstOrDefault(f => FlagBoxProfile.ShapeIdOf(Path.GetFileName(f)) == shape);
                if (file != null) return FlagBoxProfile.ReadFile(file);
            }
            return null;
        }

        /// <summary>The colour fields of every container a description opens, label and suffixed forms alike.</summary>
        private static List<string> ColoursOf(string json, string label)
        {
            var found = new List<string>();
            var pattern = new Regex("\"Description\": \"" + Regex.Escape(label) + "(\"|, [^\"]*\")(?<body>(?:(?!\"Description\"|\"ContainerType\").)*)", RegexOptions.Singleline);
            foreach (Match m in pattern.Matches(json))
            {
                foreach (Match c in Regex.Matches(m.Groups["body"].Value, "\"(Color|BlinkingColor)\": \"(?<hex>#[0-9A-F]{6})\""))
                {
                    found.Add(c.Groups[1].Value + "=" + c.Groups["hex"].Value);
                }
            }
            return found;
        }

        [Fact]
        public void A_bar_draws_the_default_until_it_has_a_colour_of_its_own()
        {
            var bar = new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3" };
            Assert.Equal("#FF6A00", bar.ColourOf("abs"));
            Assert.False(bar.HasOwnColour("abs"));
            Assert.True(bar.SetColour("abs", "#33d9f2"));
            Assert.Equal("#33D9F2", bar.ColourOf("abs"));
            Assert.True(bar.HasOwnColour("abs"));
            // The same again changes nothing, so nothing is installed again.
            Assert.False(bar.SetColour("abs", "#33D9F2"));
            // The default, or null, is the default back, and leaves nothing stored.
            Assert.True(bar.SetColour("abs", "#FF6A00"));
            Assert.False(bar.Colours.ContainsKey("abs"));
            Assert.True(bar.SetColour("abs", "#00D96A"));
            Assert.True(bar.SetColour("abs", null));
            Assert.Empty(bar.Colours);
            // A key no setting has, and a colour that is not one, change nothing.
            Assert.False(bar.SetColour("kers", "#FFFFFF"));
            Assert.False(bar.SetColour("abs", "orange"));
            Assert.False(bar.SetColour("abs", "#80FF6A00"));
            Assert.Null(bar.ColourOf("kers"));
        }

        [Fact]
        public void Normalise_keeps_only_a_colour_a_profile_can_draw_and_copy_keeps_it_apart()
        {
            var bar = new LedBar
            {
                Name = "Rim",
                Shape = "3-9-3",
                Colours = new Dictionary<string, string> { { "abs", "#00d96a" }, { "tc", "#2E7BFF" }, { "kers", "#FFFFFF" }, { "spotter", "purple" } },
            };
            bar.Normalise();
            // Upper case, the default dropped, an unknown key and an unreadable colour dropped.
            Assert.Equal(new Dictionary<string, string> { { "abs", "#00D96A" } }, bar.Colours);
            var copy = bar.Copy();
            copy.SetColour("abs", "#FFFFFF");
            Assert.Equal("#00D96A", bar.ColourOf("abs"));
        }

        [Fact]
        public void Every_colour_setting_is_a_readable_default_and_a_key_once()
        {
            Assert.Equal(Contract.LedColours.Count, Contract.LedColours.Select(c => c.Key).Distinct().Count());
            foreach (var colour in Contract.LedColours)
            {
                Assert.Equal(colour.DefaultHex, Contract.NormaliseLedHex(colour.DefaultHex));
                Assert.NotEmpty(colour.Effects);
            }
            Assert.Equal("#ABCDEF", Contract.NormaliseLedHex(" #abcdef "));
            Assert.Null(Contract.NormaliseLedHex("#ABCDEG"));
            Assert.Null(Contract.NormaliseLedHex("ABCDEF"));
        }

        [Fact]
        public void A_bar_with_no_colours_of_its_own_installs_what_the_build_wrote()
        {
            var embedded = BuiltProfile("3-9-3");
            if (embedded == null) return;
            var bar = new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3", Colours = new Dictionary<string, string>() };
            Assert.Equal(LedBarProfile.For(new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3" }, embedded), LedBarProfile.For(bar, embedded));
            Assert.Equal(embedded, LedBarProfile.Recolour(embedded, bar));
        }

        [Fact]
        public void A_colour_of_its_own_reaches_every_container_of_its_effect_and_no_other()
        {
            var embedded = BuiltProfile("4-14-4");
            if (embedded == null) return;
            var bar = new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "4-14-4" };
            bar.SetColour("abs", "#33D9F2");
            bar.SetColour("flag.yellow", "#FFFFFF");
            bar.SetColour("flag.debris", "#B14BFF");
            var mine = LedBarProfile.For(bar, embedded);

            // ABS is the only orange that moved: low fuel and the meatball share its default and stay.
            Assert.NotEmpty(ColoursOf(mine, "ABS active"));
            Assert.All(ColoursOf(mine, "ABS active"), c => Assert.Equal("Color=#33D9F2", c));
            Assert.Contains("Color=#FF6A00", ColoursOf(mine, "Low fuel"));
            Assert.DoesNotContain("Color=#33D9F2", ColoursOf(mine, "Low fuel"));
            Assert.Contains("Color=#FF6A00", ColoursOf(mine, "Meatball flag"));

            // The yellow flag moves in every form it is drawn in -- its lamp, held, and spread over the side --
            // while the full course yellow, the same yellow by default, does not.
            var yellow = ColoursOf(mine, "Yellow flag");
            Assert.Contains("Color=#FFFFFF", yellow);
            Assert.DoesNotContain("Color=#FFD400", yellow);
            Assert.Contains("\"Description\": \"Yellow flag, spread\"", mine);
            Assert.Contains("Color=#FFD400", ColoursOf(mine, "Full course yellow"));

            // The debris flag's yellow moves and its stripes' red does not: the second colour is what tells it
            // from the yellow flag, and is not offered.
            var debris = ColoursOf(mine, "Debris flag");
            Assert.Contains("Color=#B14BFF", debris);
            Assert.Contains("BlinkingColor=#FF2D46", debris);
            Assert.DoesNotContain("Color=#FFD400", debris);
        }

        [Fact]
        public void The_chequer_moves_its_lit_half_and_keeps_its_dark_one()
        {
            var embedded = BuiltProfile("3-9-3");
            if (embedded == null) return;
            var bar = new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3" };
            bar.SetColour("flag.chequered", "#2E7BFF");
            var chequer = ColoursOf(LedBarProfile.For(bar, embedded), "Chequered flag");
            // Moving: a dark ground blinking the chequer's white, now the bar's blue. Held: the blue steady.
            Assert.Contains("BlinkingColor=#2E7BFF", chequer);
            Assert.Contains("Color=#0A0B0D", chequer);
            Assert.Contains("Color=#2E7BFF", chequer);
            Assert.DoesNotContain("BlinkingColor=#F5F7FA", chequer);
            // The black flag, the same white by default, is untouched.
            Assert.Contains("Color=#F5F7FA", ColoursOf(LedBarProfile.For(bar, embedded), "Black flag"));
        }
    }
}
