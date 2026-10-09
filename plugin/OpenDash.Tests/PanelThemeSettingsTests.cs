// PanelThemeSettingsTests.cs: the Screens page's Themes group and the setting behind it (#715).
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelThemeSettingsTests
    {
        private static ScreenInstance Face(string ns, string theme)
        {
            return new ScreenInstance { Kind = Contract.KindFace, Width = 1280, Height = 480, Folder = ns, Namespace = ns, Name = ns, Theme = theme };
        }

        private static Contract.ThemeSetting Backlight
        {
            get { return Contract.ThemeSettingsOf("aim").Single(); }
        }

        [Fact]
        public void The_group_lists_every_theme_on_the_rig_that_has_settings_whichever_screen_is_on()
        {
            // A default face, a Porsche, which has no settings of its own yet, and an AiM: the AiM has a
            // disclosure whatever the selected screen, since the car's playlist decides which one is drawn.
            var rig = new List<ScreenInstance> { Face("Face1280x480", null), Face("Porsche", "porsche"), Face("Aim", "aim") };
            Assert.Equal(new[] { "aim" }, PanelThemeSettings.Themes(rig).Select(t => t.Id));
            // Two AiM screens are one theme, and one disclosure.
            rig.Add(Face("Aim2", "aim"));
            Assert.Equal(new[] { "aim" }, PanelThemeSettings.Themes(rig).Select(t => t.Id));
            // A rig with no theme that declares settings has no group at all.
            Assert.Empty(PanelThemeSettings.Themes(new[] { Face("Face1280x480", null), Face("Porsche", "porsche") }));
            Assert.Empty(PanelThemeSettings.Themes(null));
        }

        [Fact]
        public void The_backlight_row_offers_the_eight_pairs_and_names_each_swatch()
        {
            var backlight = Backlight;
            Assert.Equal("Backlight", backlight.Name);
            Assert.Equal("ThemeAimBacklight", backlight.Property);
            Assert.Equal(new[] { "White", "Inverted", "Red", "Green", "Cyan", "Blue", "Yellow", "Purple" }, backlight.Choices.Select(c => c.Name));
            // Each a ground and an ink, the white the overlay's own pair.
            Assert.All(backlight.Choices, c => Assert.Equal(2, c.Colours.Count));
            Assert.Equal(new[] { "#D9DDDA", "#1C2124" }, backlight.Choices[0].Colours);
            Assert.Equal("Backlight, Red", PanelThemeSettings.SwatchName(backlight, backlight.Choices[2]));
            Assert.Equal("white", PanelThemeSettings.Pressed(backlight, null).Id);
            Assert.Equal("cyan", PanelThemeSettings.Pressed(backlight, "cyan").Id);
            Assert.Equal("white", PanelThemeSettings.Pressed(backlight, "magenta").Id);
        }

        [Fact]
        public void A_choice_is_the_rigs_survives_a_save_and_falls_back_to_the_default()
        {
            var settings = new OpenDashSettings();
            Assert.Equal("white", settings.ThemeSetting(Backlight));
            settings.SetThemeSetting(Backlight, "inverted");
            Assert.Equal("inverted", settings.ThemeSetting(Backlight));
            settings.SetThemeSetting(Backlight, "magenta");
            Assert.Equal("white", settings.ThemeSetting(Backlight));

            settings.SetThemeSetting(Backlight, "red");
            var read = JsonSerializer.Deserialize<OpenDashSettings>(JsonSerializer.Serialize(settings));
            read.Normalise();
            Assert.Equal("red", read.ThemeSetting(Backlight));
            Assert.Equal("red", settings.NormalisedCopy().ThemeSetting(Backlight));

            // A file from a version that offered a choice this one does not reads as the default, and a
            // setting this version does not declare is kept for the version that does.
            var later = JsonSerializer.Deserialize<OpenDashSettings>("{\"ThemeSettings\":{\"ThemeAimBacklight\":\"amber\",\"ThemePorscheTcBox\":\"blue\"}}");
            later.Normalise();
            Assert.Equal("white", later.ThemeSetting(Backlight));
            Assert.Equal("blue", later.ThemeSettings["ThemePorscheTcBox"]);
            // And a file written before there were theme settings reads every one at its default.
            var older = JsonSerializer.Deserialize<OpenDashSettings>("{\"PositionMode\":\"overall\"}");
            older.Normalise();
            Assert.Equal("white", older.ThemeSetting(Backlight));
        }
    }
}
