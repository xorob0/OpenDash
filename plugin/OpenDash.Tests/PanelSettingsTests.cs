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
            // Each threshold names its own default, which the artboard draws as the field's placeholder.
            Assert.Equal("0 uses 120 °C (248 °F).", PanelSettings.OilTempCaption);
            Assert.Equal("0 uses 110 °C (230 °F).", PanelSettings.WaterTempCaption);
            Assert.Equal("Brightness", PanelSettings.BrightnessTitle);
            Assert.Equal("SimHub's device brightness applies on top.", PanelSettings.BrightnessCaption);
            Assert.Equal("Night brightness", PanelSettings.NightBrightnessTitle);
            Assert.Equal("Used while night mode is on.", PanelSettings.NightBrightnessCaption);
            Assert.Equal("Night mode", PanelSettings.NightModeTitle);
        }

        [Fact]
        public void Every_caption_is_a_sentence()
        {
            foreach (var caption in new[] { PanelSettings.LowFuelCaption, PanelSettings.OilTempCaption, PanelSettings.WaterTempCaption, PanelSettings.BrightnessCaption, PanelSettings.NightBrightnessCaption })
            {
                Assert.EndsWith(".", caption, StringComparison.Ordinal);
            }
        }

        /// <summary>The page's anchor ids, which search, Home's fix rows and the capture scripts route to: a
        /// renamed one sends each of them to the page's top, so every id is pinned, and a new one is added here.</summary>
        [Fact]
        public void Its_anchor_ids_are_pinned()
        {
            Assert.Equal(new[]
            {
                "AnchorAlerts = settings.alerts",
                "AnchorFlags = settings.flags",
                "AnchorLighting = settings.lighting",
                "AnchorRaceData = settings.race-data",
            }, AnchorTable.Of(typeof(PanelSettings)));
        }
    }
}
