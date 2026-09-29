// PanelSettingsTests.cs: the Settings page's headings and captions.
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelSettingsTests
    {
        [Fact]
        public void Settings_says_what_the_artboard_says()
        {
            Assert.Equal("Settings", PanelSettings.Title);
            Assert.Equal("Race data", PanelDataTab.SectionTitle);
            Assert.Equal("Flags", PanelSettings.FlagsTitle);
            Assert.Equal("Alerts", PanelSettings.AlertsTitle);
            Assert.Equal("Lighting", PanelSettings.LightingTitle);
            Assert.Equal("Flags in the pit lane", PanelSettings.FlagsInPitLaneTitle);
            Assert.Equal("Low fuel", PanelSettings.LowFuelTitle);
            Assert.Equal("Laps of fuel left.", PanelSettings.LowFuelCaption);
            Assert.Equal("Oil temperature", PanelSettings.OilTempTitle);
            Assert.Equal("Water temperature", PanelSettings.WaterTempTitle);
            Assert.Equal("In SimHub's unit. 0 uses the default.", PanelSettings.TempCaption);
            Assert.Equal("Brightness", PanelSettings.BrightnessTitle);
            Assert.Equal("SimHub's device brightness applies on top.", PanelSettings.BrightnessCaption);
            Assert.Equal("Night brightness", PanelSettings.NightBrightnessTitle);
            Assert.Equal("Used while night mode is on.", PanelSettings.NightBrightnessCaption);
            Assert.Equal("Night mode", PanelSettings.NightModeTitle);
        }

        [Fact]
        public void Every_caption_is_a_sentence()
        {
            foreach (var caption in new[] { PanelSettings.LowFuelCaption, PanelSettings.TempCaption, PanelSettings.BrightnessCaption, PanelSettings.NightBrightnessCaption })
            {
                Assert.EndsWith(".", caption, StringComparison.Ordinal);
            }
        }
    }
}
