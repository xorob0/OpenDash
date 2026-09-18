// PanelLightRowsTests.cs: the rows the Install tab's Lights section draws, and the one thing about them
// that is mirrored out of the dash build rather than read back from it.
//
// Two different jobs here. The first is the shape of the section: eight rows over a full build, in the
// canvas's order, with the grouped ones counting their own members so that a length added to
// packages/dash/src/leds/strip.ts cannot leave a caption saying "five" over six profiles. The second is
// the device captions, which are the only thing PanelLightRows carries that no embedded artefact does --
// a strip profile's JSON has a Name and nothing else we could caption from -- and which are therefore
// held against strip.ts in both directions.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelLightRowsTests
    {
        /// <summary>See FlagBoxInstallPlanTests: on CI the dash artifact has already been downloaded into
        /// Resources/, so an empty folder there is the contract having parted company.</summary>
        private static bool OnCI =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        /// <summary>Every shape a full build emits, as the panel sees it: the id off the file name and the
        /// Name the generator wrote inside the profile. Deliberately in the assembly's name order rather
        /// than the generator's, because that is the order FlagBoxProfile.StripResourceNames hands over
        /// and the row order must not depend on it.</summary>
        private static IList<LightProfile> FullBuild()
        {
            var ids = new List<string>();
            foreach (var side in new[] { 0, 1, 2, 3, 4 })
            {
                for (var centre = 4; centre <= 12; centre++) ids.Add(side + "-" + centre + "-" + side);
            }
            for (var centre = 13; centre <= 25; centre++) ids.Add("0-" + centre + "-0");
            // 3-10-3 is in the grid's own range and the legacy row spells it, so it appears once.
            ids.Remove("3-10-3");
            ids.AddRange(new[] { "4-14-4", "4-14-4-reversed", "3-10-3", "3-9-3-fanalab", "5-10-5" });
            // Shuffled, because FlagBoxProfile.StripResourceNames hands them over in the assembly's own
            // order and the row order must not depend on it.
            return ids
                .OrderBy(id => id, StringComparer.Ordinal)
                .Select(id => new LightProfile(id, "openDash " + PanelLightRows.Label(new LightProfile(id, null))))
                .ToList();
        }

        [Fact]
        public void A_full_build_draws_a_row_per_named_device_and_a_row_per_side_length()
        {
            var rows = PanelLightRows.Rows(FullBuild());
            Assert.Equal(
                new[]
                {
                    "openDash 4/14/4",
                    "openDash 4/14/4 reversed",
                    "openDash 3/9/3 Fanalab",
                    "openDash 3/10/3",
                    "openDash 0/4/0 … 0/25/0",
                    "openDash 1/4/1 … 1/12/1",
                    "openDash 2/4/2 … 2/12/2",
                    "openDash 3/4/3 … 3/12/3",
                    "openDash 4/4/4 … 4/12/4",
                    "openDash 5/10/5",
                },
                rows.Select(r => r.Name));
            Assert.Equal(
                new[]
                {
                    "strip · SimRep MLD, Ascher",
                    "strip · SimRep MLD, wired from the far end",
                    "strip · Fanatec through Fanalab",
                    // Eight and not nine: 3/10/3 is a named shape and has a row of its own above.
                    "strip · GridSim Lab GTSL Pro",
                    "bare runs and brows, 22 lengths",
                    "strips, one LED at each end, nine lengths",
                    "strips, two LEDs at each end, nine lengths",
                    "strips, three LEDs at each end, eight lengths",
                    "strips, four LEDs at each end, nine lengths",
                    "strip, five LEDs at each end",
                },
                rows.Select(r => r.Caption));

            // Every profile in exactly one row: a shape offered twice, or not at all, is the failure
            // this counts against.
            var members = rows.SelectMany(r => r.ShapeIds).ToList();
            var all = FullBuild().Select(p => p.ShapeId).ToList();
            Assert.Equal(all.Count, members.Count);
            Assert.Equal(all.Count, members.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(all.OrderBy(x => x, StringComparer.Ordinal), members.OrderBy(x => x, StringComparer.Ordinal));
        }

        [Fact]
        public void The_grouped_rows_are_named_by_their_own_ends()
        {
            // The name is read off the members rather than written down, so a length added to strip.ts
            // moves the end of the range instead of leaving it a length short.
            var rows = PanelLightRows.Rows(FullBuild().Concat(new[] { new LightProfile("0-30-0", "openDash 0/30/0") }));
            Assert.Contains(rows, r => r.Name == "openDash 0/4/0 … 0/30/0" && r.Caption == "bare runs and brows, 23 lengths");
        }

        [Fact]
        public void A_shape_the_build_did_not_embed_has_no_row()
        {
            // The census is what is embedded. A row for a profile that is not in this build could only
            // offer a press that does nothing.
            var rows = PanelLightRows.Rows(FullBuild().Where(p => p.ShapeId != "4-14-4" && !p.ShapeId.StartsWith("brow-", StringComparison.Ordinal)));
            Assert.DoesNotContain(rows, r => r.ShapeIds.Contains("4-14-4"));
            Assert.DoesNotContain(rows, r => r.Caption.StartsWith("brow", StringComparison.Ordinal));
            // The reversed 4/14/4 keeps its own row: it is a second profile, not a second name for one.
            Assert.Contains(rows, r => r.Name == "openDash 4/14/4 reversed");
        }

        [Fact]
        public void One_profile_alone_is_a_row_and_not_a_range()
        {
            // What the test project's own resources are, and what a partial artifact would be. A range of
            // one would read "openDash 0/10/0 … 0/10/0", and "bare runs, one lengths" is not English.
            var rows = PanelLightRows.Rows(new[] { new LightProfile("0-10-0", "openDash 0/10/0") });
            Assert.Equal("openDash 0/10/0", Assert.Single(rows).Name);
            Assert.Equal("bare run", rows[0].Caption);
        }

        [Fact]
        public void A_row_is_called_what_the_build_called_the_profile()
        {
            // rpmStripProfileName() wrote it and FlagBoxProfile.ProfileNameOf read it back, so a rename
            // there arrives here without an edit.
            var renamed = PanelLightRows.Rows(new[] { new LightProfile("4-14-4", "openDash the wide one") });
            Assert.Equal("openDash the wide one", Assert.Single(renamed).Name);

            // And when the Name cannot be read at all, the id is spelled the way the generator spells it
            // rather than shown raw.
            var unnamed = PanelLightRows.Rows(new[] { new LightProfile("4-14-4", null), new LightProfile("brow-9", null) });
            Assert.Equal(new[] { "openDash 4/14/4", "openDash brow 9" }, unnamed.Select(r => r.Name));
        }

        [Fact]
        public void An_id_that_cannot_be_read_gets_its_own_row_rather_than_a_guess()
        {
            Assert.Null(LightShape.Parse("openDash"));
            Assert.Null(LightShape.Parse("4-14"));
            Assert.Null(LightShape.Parse("4-x-4"));
            Assert.Null(LightShape.Parse("brow-x"));
            Assert.Null(LightShape.Parse("-1-9-1"));

            var rows = PanelLightRows.Rows(new[] { new LightProfile("something-else", "openDash something else") });
            Assert.Equal("openDash something else", Assert.Single(rows).Name);
            Assert.Equal("strip", rows[0].Caption);
        }

        [Fact]
        public void A_shape_id_reads_back_as_the_geometry_the_generator_wrote_it_from()
        {
            var wide = LightShape.Parse("4-14-4");
            Assert.Equal(PanelLightRows.Wheel, wide.Placement);
            Assert.Equal(4, wide.Left);
            Assert.Equal(14, wide.Centre);
            Assert.Equal(4, wide.Right);
            Assert.False(wide.Reversed);
            Assert.False(wide.Bare);

            Assert.True(LightShape.Parse("4-14-4-reversed").Reversed);
            Assert.True(LightShape.Parse("0-16-0").Bare);

            var brow = LightShape.Parse("brow-25");
            Assert.Equal(PanelLightRows.Brow, brow.Placement);
            Assert.Equal(25, brow.Centre);
            Assert.True(brow.Bare);
        }

        [Fact]
        public void The_dot_never_disagrees_with_the_words_beside_it()
        {
            // Read off PanelCopy.LightRow rather than from a table of its own, except for the one pairing
            // that table cannot carry: the uninstalled dot is status.notInstalled and its label text.label.
            Assert.Equal(Theme.StatusUpToDate, PanelLightRows.DotHex(FlagBoxInstallState.UpToDate));
            Assert.Equal(Theme.StatusUpToDate, PanelLightRows.DotHex(FlagBoxInstallState.Outdated));
            Assert.Equal(Theme.StatusFailed, PanelLightRows.DotHex(FlagBoxInstallState.Failed));
            Assert.Equal(Theme.StatusNotInstalled, PanelLightRows.DotHex(FlagBoxInstallState.NotInstalled));
            Assert.Equal(Theme.StatusNotInstalled, PanelLightRows.DotHex(FlagBoxInstallState.Unavailable));
            Assert.Equal(Theme.StatusNotInstalled, PanelLightRows.DotHex(FlagBoxInstallState.NotEmbedded));
        }

        [Fact]
        public void An_unreachable_driver_does_not_promise_a_file_that_was_never_written()
        {
            // The whole reason a strip row has a tooltip of its own. FlagBoxInstallPlan.Summary offers the
            // copy in the openDash folder, and FlagBoxProfile.Extract writes only the flag box there, so
            // that sentence over a strip row sends a driver looking for a file nothing ever created.
            var strip = PanelLightRows.Tooltip(5, FlagBoxInstallState.Unavailable, null);
            Assert.Equal(PanelLightRows.Unavailable, strip);
            Assert.DoesNotContain("openDash folder", strip, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("by hand", strip, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("openDash folder", FlagBoxInstallPlan.Summary(new FlagBoxPlan { State = FlagBoxInstallState.Unavailable }, null), StringComparison.Ordinal);
        }

        [Fact]
        public void A_grouped_tooltip_claims_nothing_about_the_members_it_cannot_speak_for()
        {
            // Combine reports the WORST member, so "not installed" over seven brows means at least one of
            // them is, and the sentence has to say so rather than that none of them is.
            var group = PanelLightRows.Tooltip(7, FlagBoxInstallState.NotInstalled, null);
            Assert.Contains("At least one of these seven", group, StringComparison.Ordinal);
            Assert.Contains("them all", group, StringComparison.Ordinal);

            var one = PanelLightRows.Tooltip(1, FlagBoxInstallState.NotInstalled, null);
            Assert.StartsWith("Not installed.", one, StringComparison.Ordinal);
            Assert.DoesNotContain("At least one", one, StringComparison.Ordinal);

            // A press that replaces a copy the user may have edited carries the same warning the flag box
            // carries, and is the one sentence neither file writes twice.
            Assert.Contains(FlagBoxInstallPlan.Replaces, PanelLightRows.Tooltip(3, FlagBoxInstallState.UpToDate, null), StringComparison.Ordinal);
            Assert.Contains(FlagBoxInstallPlan.Replaces, PanelLightRows.Tooltip(1, FlagBoxInstallState.Outdated, "0.4.0"), StringComparison.Ordinal);
            Assert.DoesNotContain(FlagBoxInstallPlan.Replaces, PanelLightRows.Tooltip(1, FlagBoxInstallState.NotInstalled, null), StringComparison.Ordinal);

            // Installing adds a profile; it does not switch to one.
            Assert.Contains("Select", PanelLightRows.Tooltip(1, FlagBoxInstallState.UpToDate, "0.4.0"), StringComparison.Ordinal);
            Assert.Contains("0.4.0", PanelLightRows.Tooltip(1, FlagBoxInstallState.UpToDate, "0.4.0"), StringComparison.Ordinal);
            // A strip profile carries no version marker, so the row says "Installed" and invents none.
            Assert.DoesNotContain("(", PanelLightRows.Tooltip(1, FlagBoxInstallState.UpToDate, null), StringComparison.Ordinal);
        }

        [Fact]
        public void A_count_is_written_as_a_word_and_falls_back_to_digits()
        {
            Assert.Equal("five", PanelLightRows.Word(5));
            Assert.Equal("seven", PanelLightRows.Word(7));
            Assert.Equal("twelve", PanelLightRows.Word(12));
            Assert.Equal("13", PanelLightRows.Word(13));
        }

        // --- The mirror ------------------------------------------------------------------------------
        //
        // PanelLightRows.NamedShapes is the only thing the panel carries that is not read back out of a
        // built artefact, because a strip profile's JSON has a Name and no device list. These two tests
        // are what stops it drifting: strip.ts is parsed here and held against it both ways.

        private sealed class GeneratedShape
        {
            public string Id;
            public IList<string> Devices;
        }

        private const string GenericRun = "generic WS2812b runs";

        private static string StripTs()
        {
            return Path.Combine(RepoPaths.Root(), "packages", "dash", "src", "leds", "strip.ts");
        }

        /// <summary>The shapes the generator still names a device for, read off the `wheel(...)` and
        /// `fanalab(...)` rows of LEGACY_SHAPES. The grid names none: it is sides against centres and the
        /// panel groups it by side, so there is nothing left to caption by hardware.</summary>
        /// <remarks>
        /// A row's id is its geometry plus whatever suffix its spelling adds, which is the one thing about
        /// strip.ts this has to know twice. `wheel(..., { reversed: true })` and `fanalab(...)` are the two
        /// wirings that make a second profile of one geometry, so both are read here or the caption mirror
        /// below would see two shapes claiming the same id.
        /// </remarks>
        private static IList<GeneratedShape> GeneratedWheels()
        {
            var text = File.ReadAllText(StripTs());
            var shapes = new List<GeneratedShape>();
            foreach (Match row in Regex.Matches(text, @"^\s*(wheel|fanalab)\((\d+), (\d+), (\d+)(?:, \{(.*)\})?\),\s*$", RegexOptions.Multiline))
            {
                var options = row.Groups[5].Value;
                shapes.Add(new GeneratedShape
                {
                    Id = row.Groups[2].Value + "-" + row.Groups[3].Value + "-" + row.Groups[4].Value
                        + (options.Contains("reversed: true") ? "-reversed" : string.Empty)
                        + (row.Groups[1].Value == "fanalab" ? "-fanalab" : string.Empty),
                    Devices = Devices(options),
                });
            }
            Assert.NotEmpty(shapes);
            return shapes;
        }

        private static IList<string> Devices(string options)
        {
            var list = Regex.Match(options, @"devices: \[([^\]]*)\]");
            if (!list.Success) return new string[0];
            return Regex.Matches(list.Groups[1].Value, @"'([^']*)'").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        }

        [Fact]
        public void Every_shape_the_generator_names_a_device_for_has_a_caption_and_the_rest_are_grouped()
        {
            var generated = GeneratedWheels();
            var captioned = new HashSet<string>(PanelLightRows.NamedShapes.Select(p => p.Key), StringComparer.Ordinal);

            foreach (var shape in generated)
            {
                var generic = shape.Devices.Count == 1 && shape.Devices[0] == GenericRun;
                Assert.True(
                    generic != captioned.Contains(shape.Id),
                    generic
                        ? shape.Id + " is a generic run in strip.ts and must not have a caption of its own in PanelLightRows.NamedShapes."
                        : shape.Id + " names the device family " + string.Join(", ", shape.Devices)
                            + " in strip.ts and has no caption in PanelLightRows.NamedShapes, so the panel would group it under \"strips\" and say nothing about the hardware it is for.");
            }

            // And nothing captioned here has left the generator, which would draw a row for a profile no
            // build emits.
            var ids = new HashSet<string>(generated.Select(s => s.Id), StringComparer.Ordinal);
            Assert.All(captioned, id => Assert.True(ids.Contains(id), id + " is captioned in PanelLightRows.NamedShapes and is no longer a shape in strip.ts."));
        }

        [Fact]
        public void No_caption_names_hardware_the_generator_does_not()
        {
            // The captions abbreviate what strip.ts spells out, because the caption is drawn as tracked
            // uppercase and the full device families are wider than the row. Every word of one still has
            // to appear in the generator's own list, so a device renamed there fails here rather than
            // leaving the panel naming hardware that no longer exists.
            var devices = GeneratedWheels().ToDictionary(s => s.Id, s => string.Join(" | ", s.Devices), StringComparer.Ordinal);
            foreach (var named in PanelLightRows.NamedShapes)
            {
                var spelled = devices[named.Key];
                var words = named.Value.Split(new[] { ' ', ',', '·' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w != "strip");
                foreach (var word in words)
                {
                    Assert.True(
                        spelled.IndexOf(word, StringComparison.Ordinal) >= 0,
                        "the caption for " + named.Key + " says \"" + word + "\", which is in none of strip.ts's devices for it: " + spelled);
                }
            }
        }

        [Fact]
        public void The_grid_is_one_row_per_side_length_over_a_range_of_centres()
        {
            // Sixty-odd shapes joined into one row with middle dots is a line nobody can read. What a
            // driver picks between is how many LEDs sit at the ends, so that is the row, and the centres
            // under it are a range rather than a list.
            var ids = new List<string>();
            foreach (var side in new[] { 0, 1, 2 })
            {
                for (var centre = 4; centre <= 12; centre++) ids.Add(side + "-" + centre + "-" + side);
            }
            for (var centre = 13; centre <= 25; centre++) ids.Add("0-" + centre + "-0");

            var rows = PanelLightRows.Rows(ids.Select(id => new LightProfile(id, null)));
            Assert.Equal(3, rows.Count);
            Assert.Equal("openDash 0/4/0 … 0/25/0", rows[0].Name);
            Assert.Equal("bare runs and brows, 22 lengths", rows[0].Caption);
            Assert.Equal("openDash 1/4/1 … 1/12/1", rows[1].Name);
            Assert.Equal("strips, one LED at each end, nine lengths", rows[1].Caption);
            Assert.Equal("strips, two LEDs at each end, nine lengths", rows[2].Caption);
            // Every shape is in exactly one row, which is what stops a profile being offered twice or
            // not at all.
            var members = rows.SelectMany(r => r.ShapeIds).ToList();
            Assert.Equal(ids.Count, members.Count);
            Assert.Equal(ids.OrderBy(x => x, StringComparer.Ordinal), members.OrderBy(x => x, StringComparer.Ordinal));
        }

        [Fact]
        public void The_rows_a_real_build_produces_are_the_rows_the_canvas_draws()
        {
            // The whole census end to end, off the files the plugin actually embeds: the file name gives
            // the shape id and the profile's own Name gives the row its title, exactly as
            // SettingsControl.Install.Lights.cs reads them out of the assembly.
            var built = BuiltProfiles();
            if (built == null)
            {
                Assert.False(
                    OnCI,
                    "no *" + FlagBoxProfile.ProfileExtension + " in " + RepoPaths.EmbeddedResources() + " or " + RepoPaths.BuildOutput()
                        + ". CI downloads the dash artifact into Resources/ before `dotnet test`.");
                return;
            }

            var rows = PanelLightRows.Rows(built);
            Assert.Equal(
                new[]
                {
                    "openDash 4/14/4",
                    "openDash 4/14/4 reversed",
                    "openDash 3/9/3 Fanalab",
                    "openDash 3/10/3",
                    "openDash 0/4/0 … 0/25/0",
                    "openDash 1/4/1 … 1/12/1",
                    "openDash 2/4/2 … 2/12/2",
                    "openDash 3/4/3 … 3/12/3",
                    "openDash 4/4/4 … 4/12/4",
                    "openDash 5/10/5",
                },
                rows.Select(r => r.Name));
            // Sixty-two: five sides of nine centres and thirteen longer bare runs, less the one the
            // legacy list already spells, plus the five that shipped before the grid.
            Assert.Equal(62, rows.SelectMany(r => r.ShapeIds).Count());
            // The flag box is not one of them: it is its own row and its own driver, and handing a strip
            // to the matrix driver is the hazard FlagBoxProfile exists to prevent.
            Assert.DoesNotContain(rows, r => r.Name == FlagBoxProfile.ProfileName);
        }

        /// <summary>Every embedded profile that is not the flag box, from the folder the plugin embeds if
        /// it has been filled and from the dash build output otherwise, or null when nothing has been
        /// built in this checkout at all.</summary>
        private static IList<LightProfile> BuiltProfiles()
        {
            foreach (var folder in new[] { RepoPaths.EmbeddedResources(), RepoPaths.BuildOutput() })
            {
                if (!Directory.Exists(folder)) continue;
                var files = Directory.GetFiles(folder, "*" + FlagBoxProfile.ProfileExtension)
                    .Where(f => FlagBoxProfile.ShapeIdOf(Path.GetFileName(f)) != null)
                    .ToList();
                if (files.Count == 0) continue;
                return files
                    .Select(f => new LightProfile(
                        FlagBoxProfile.ShapeIdOf(Path.GetFileName(f)),
                        FlagBoxProfile.ProfileNameOf(FlagBoxProfile.ReadFile(f))))
                    .ToList();
            }
            return null;
        }
    }
}
