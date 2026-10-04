// PanelLedBarFormTests.cs: the add-strip form's two numbers and its Fanatec switch, held against the
// census the form is drawn from and AddLedBar resolves the profile through.
//
// The form is WPF and nothing here can compile it, so what is held is the pure half it is built from --
// PanelLights.BarSides, BarCentres, OffersFanatec and BarShapeId -- and, read as text, the one line of the
// form that hands the result on. The property that matters is #436's: whatever position the switch is
// in, the id the form produces is one the census carries, because an id it does not carry is a bar
// whose profile cannot be installed. With the switch on, the two numbers are asked over the shapes whose
// Fanatec wiring the build embedded, so a Fanatec rim of any of those shapes can be added.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelLedBarFormTests
    {
        /// <summary>See FlagBoxInstallPlanTests: on CI the dash artifact has already been downloaded into
        /// Resources/, so an empty folder there is the contract having parted company.</summary>
        private static bool OnCI =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        /// <summary>A small census in the generator's spelling: plain geometry, both wirings, and a shape
        /// whose ends differ, which the two numbers cannot express.</summary>
        private static readonly string[] Census =
        {
            "0-15-0", "3-9-3", "3-10-3", "4-12-4", "4-14-4", "4-14-4-reversed", "3-9-3-fanatec", "2-9-3", "4-8-4", "4-8-4-fanatec",
        };

        [Fact]
        public void The_two_numbers_are_asked_over_the_plain_shapes_alone()
        {
            Assert.Equal(new[] { 0, 3, 4 }, PanelLights.BarSides(Census));
            Assert.Equal(new[] { 9, 10 }, PanelLights.BarCentres(Census, 3));
            // The reversed 4/14/4 is the same geometry as the plain one and is not a second fourteen.
            Assert.Equal(new[] { 8, 12, 14 }, PanelLights.BarCentres(Census, 4));
            Assert.Empty(PanelLights.BarSides(new string[0]));
            Assert.Empty(PanelLights.BarSides(null));
        }

        [Fact]
        public void The_switch_is_drawn_only_where_a_Fanatec_profile_was_embedded()
        {
            Assert.Equal("3-9-3-fanatec", PanelLights.FanatecShapeId);
            Assert.True(PanelLights.OffersFanatec(Census));
            Assert.True(PanelLights.OffersFanatec(Census.Where(id => id != "3-9-3-fanatec")));
            Assert.False(PanelLights.OffersFanatec(Census.Where(id => !id.EndsWith("-fanatec", StringComparison.Ordinal))));
            // The reversed wiring does not draw it: one switch, for the one wiring that was asked for.
            Assert.False(PanelLights.OffersFanatec(new[] { "3-9-3", "4-14-4-reversed" }));
            // Nor a Fanatec profile whose plain shape the build does not carry: the numbers ask over plain shapes.
            Assert.False(PanelLights.OffersFanatec(new[] { "3-9-3-fanatec" }));
            Assert.False(PanelLights.OffersFanatec(null));
        }

        [Fact]
        public void With_the_switch_on_the_numbers_are_asked_over_the_shapes_with_a_Fanatec_wiring()
        {
            // Today's Fanatec wheels are all 3/9/3, and a rim of another shape is one the build can already serve.
            Assert.Equal(new[] { 3, 4 }, PanelLights.BarSides(Census, true));
            Assert.Equal(new[] { 9 }, PanelLights.BarCentres(Census, 3, true));
            Assert.Equal(new[] { 8 }, PanelLights.BarCentres(Census, 4, true));
            Assert.Empty(PanelLights.BarCentres(Census, 0, true));
            Assert.True(PanelLights.HasFanatecTwin(Census, "4-8-4"));
            Assert.False(PanelLights.HasFanatecTwin(Census, "4-12-4"));
            Assert.False(PanelLights.HasFanatecTwin(null, "3-9-3"));
        }

        [Fact]
        public void On_the_switch_adds_the_Fanatec_wiring_of_the_numbers_held()
        {
            Assert.Equal("3-9-3", PanelLights.BarShapeId(3, 9, false));
            Assert.Equal("4-12-4", PanelLights.BarShapeId(4, 12, false));
            Assert.Equal("3-9-3-fanatec", PanelLights.BarShapeId(3, 9, true));
            Assert.Equal("4-8-4-fanatec", PanelLights.BarShapeId(4, 8, true));
            Assert.Equal(PanelLights.FanatecShapeId, PanelLights.BarShapeId(PanelLights.FanatecSide, PanelLights.FanatecCentre) + "-fanatec");
        }

        [Fact]
        public void The_note_and_the_default_name_follow_the_switch()
        {
            Assert.Equal("15 LEDs in all, as 3 · 9 · 3.", PanelLights.BarShapeNote(3, 9, false));
            Assert.Equal("24 LEDs in all, as 4 · 16 · 4.", PanelLights.BarShapeNote(4, 16, false));
            Assert.Equal("15 LEDs in all, as 3 · 9 · 3 Fanatec.", PanelLights.BarShapeNote(3, 9, true));
            Assert.Equal("16 LEDs in all, as 4 · 8 · 4 Fanatec.", PanelLights.BarShapeNote(4, 8, true));
            // A bare run has no Fanatec wiring, so the note never says it of one.
            Assert.Equal("20 LEDs in all.", PanelLights.BarShapeNote(0, 20, true));
            // What DefaultBarName prefixes with "OpenDash ", which is the Name the generator gives the profile.
            Assert.Equal("4/8/4 Fanatec", PanelLightRows.ShapeLabel(PanelLights.BarShapeId(4, 8, true)));
            Assert.Equal("3/9/3", PanelLightRows.ShapeLabel(PanelLights.BarShapeId(3, 9, false)));
            Assert.Equal("Wheel rim", PanelLeds.DefaultName(PanelLights.BarShapeId(4, 8, true), null));
            Assert.Equal("Strip", PanelLeds.DefaultName(PanelLights.BarShapeId(4, 8, false), null));
        }

        [Fact]
        public void The_switch_row_says_what_it_is_for()
        {
            Assert.Contains("Fanatec", PanelLights.BarFanatecTitle);
            Assert.Contains("Fanatec", PanelLights.BarFanatecSwitch);
            Assert.Contains("Fanatec", PanelLights.BarFanatecCaption);
        }

        /// <summary>
        /// Every id the form can produce, in both positions of the switch, is an id a real build embedded.
        /// </summary>
        /// <remarks>
        /// Off the files the plugin actually embeds, the way SettingsControl.Lights.cs reads them out of the
        /// assembly: the id off the file name. Every pair of numbers the form offers is tried in the wiring
        /// it offers them in, with the switch off and with it on.
        /// </remarks>
        [Fact]
        public void The_id_the_form_produces_is_one_the_census_carries_in_both_positions_of_the_switch()
        {
            var census = BuiltCensus();
            if (census == null)
            {
                Assert.False(
                    OnCI,
                    "no *" + FlagBoxProfile.ProfileExtension + " in " + RepoPaths.EmbeddedResources() + " or " + RepoPaths.BuildOutput()
                        + ". CI downloads the dash artifact into Resources/ before `dotnet test`.");
                return;
            }

            Assert.True(PanelLights.OffersFanatec(census));
            var sides = PanelLights.BarSides(census);
            Assert.NotEmpty(sides);
            foreach (var fanatec in new[] { false, true })
            {
                foreach (var side in PanelLights.BarSides(census, fanatec))
                {
                    foreach (var centre in PanelLights.BarCentres(census, side, fanatec))
                    {
                        Assert.Contains(PanelLights.BarShapeId(side, centre, fanatec), census);
                    }
                }
            }
            // The Fanatec wiring reaches every side with ends, the legacy 5/10/5's included, and the 3/9/3 the
            // wheels are today.
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, PanelLights.BarSides(census, true));
            Assert.Contains(PanelLights.FanatecCentre, PanelLights.BarCentres(census, PanelLights.FanatecSide, true));
            Assert.Contains(8, PanelLights.BarCentres(census, 4, true));
            // And no wiring leaks into the two numbers: the plain 4/14/4 is offered once.
            Assert.Single(PanelLights.BarCentres(census, 4), 14);
        }

        /// <summary>The name the switch gives a bar before the driver types over it is the Name the build
        /// wrote into the Fanatec profile, so the row in SimHub's list and the bar agree.</summary>
        [Fact]
        public void A_Fanatec_bar_is_named_for_the_profile_the_build_wrote()
        {
            var folder = new[] { RepoPaths.EmbeddedResources(), RepoPaths.BuildOutput() }
                .FirstOrDefault(f => File.Exists(Path.Combine(f, FlagBoxProfile.StripFileName(PanelLights.FanatecShapeId))));
            if (folder == null)
            {
                Assert.False(OnCI, "no " + FlagBoxProfile.StripFileName(PanelLights.FanatecShapeId) + " in the build output");
                return;
            }
            var json = FlagBoxProfile.ReadFile(Path.Combine(folder, FlagBoxProfile.StripFileName(PanelLights.FanatecShapeId)));
            Assert.Equal(
                FlagBoxProfile.ProfileNameOf(json),
                FlagBoxProfile.FilePrefix + PanelLightRows.ShapeLabel(PanelLights.BarShapeId(3, 9, true)));
        }

        /// <summary>
        /// The form hands AddLedBar, and names the bar after, the id the switch decides.
        /// </summary>
        /// <remarks>
        /// Read as text because the csproj compiles no WPF file: this is the one line that joins the
        /// tested half to the form, and a form that went back to the two-argument BarShapeId would add a
        /// plain 3/9/3 with the switch on and every test above would still pass.
        /// </remarks>
        [Fact]
        public void The_form_hands_on_the_id_the_switch_decides()
        {
            var lights = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.Lights.cs"));
            Assert.Contains("AddLedBar(PanelLights.BarShapeId(side, centre, fanatec)", lights);
            Assert.Contains("DefaultBarName(PanelLights.BarShapeId(side, centre, fanatec))", lights);
            Assert.Contains("PanelLights.OffersFanatec(census)", lights);
            Assert.DoesNotContain("PanelLights.BarShapeId(side, centre)", lights);
        }

        /// <summary>Every strip id a full build embeds, from the folder the plugin embeds if it has been
        /// filled and from the dash build output otherwise, or null when nothing has been built.</summary>
        private static IList<string> BuiltCensus()
        {
            foreach (var folder in new[] { RepoPaths.EmbeddedResources(), RepoPaths.BuildOutput() })
            {
                if (!Directory.Exists(folder)) continue;
                var ids = Directory.GetFiles(folder, "*" + FlagBoxProfile.ProfileExtension)
                    .Select(f => FlagBoxProfile.ShapeIdOf(Path.GetFileName(f)))
                    .Where(id => id != null)
                    .ToList();
                if (ids.Count > 0) return ids;
            }
            return null;
        }
    }
}
