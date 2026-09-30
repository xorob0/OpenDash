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
            ids.AddRange(new[] { "4-14-4", "4-14-4-reversed", "3-10-3", "3-9-3-fanatec", "5-10-5" });
            // Shuffled, because FlagBoxProfile.StripResourceNames hands them over in the assembly's own
            // order and the row order must not depend on it.
            return ids
                .OrderBy(id => id, StringComparer.Ordinal)
                .Select(id => new LightProfile(id, "OpenDash " + PanelLightRows.Label(new LightProfile(id, null))))
                .ToList();
        }

        [Fact]
        public void A_full_build_draws_a_row_per_named_device_and_a_row_per_side_length()
        {
            var rows = PanelLightRows.Rows(FullBuild());
            Assert.Equal(
                new[]
                {
                    "OpenDash 4/14/4",
                    "OpenDash 4/14/4 reversed",
                    "OpenDash 3/9/3 Fanatec",
                    "OpenDash 3/10/3",
                    "OpenDash 0/4/0 … 0/25/0",
                    "OpenDash 1/4/1 … 1/12/1",
                    "OpenDash 2/4/2 … 2/12/2",
                    "OpenDash 3/4/3 … 3/12/3",
                    "OpenDash 4/4/4 … 4/12/4",
                    "OpenDash 5/10/5",
                },
                rows.Select(r => r.Name));
            Assert.Equal(
                new[]
                {
                    "Strip · SimRep MLD, Ascher",
                    "Strip · SimRep MLD, wired from the far end",
                    "Strip · Fanatec wheels in SimHub",
                    // Eight and not nine: 3/10/3 is a named shape and has a row of its own above.
                    "Strip · GridSim Lab GTSL Pro",
                    "Bare runs and brows, 22 lengths",
                    "Strips, one LED at each end, nine lengths",
                    "Strips, two LEDs at each end, nine lengths",
                    "Strips, three LEDs at each end, eight lengths",
                    "Strips, four LEDs at each end, nine lengths",
                    "Strip, five LEDs at each end",
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
        public void A_reversed_twin_shares_its_siblings_row_and_is_not_counted_as_a_length()
        {
            // The generator writes a reversed twin of every plain shape since #503, and reversal is a
            // switch on a bar rather than a shape to pick, so a twin rides in its sibling's row. Names and
            // captions are what they were; each row installs twice as many profiles.
            var plain = FullBuild();
            var twins = plain
                .Where(p => !p.ShapeId.EndsWith("-reversed", StringComparison.Ordinal) && !p.ShapeId.EndsWith("-fanatec", StringComparison.Ordinal) && p.ShapeId != "4-14-4")
                .Select(p => new LightProfile(p.ShapeId + "-reversed", "OpenDash " + PanelLightRows.Label(new LightProfile(p.ShapeId + "-reversed", null))))
                .ToList();
            // Shuffled in, as the assembly hands them over.
            var built = plain.Concat(twins).OrderBy(p => p.ShapeId, StringComparer.Ordinal).ToList();

            var before = PanelLightRows.Rows(plain);
            var after = PanelLightRows.Rows(built);
            Assert.Equal(before.Select(r => r.Name), after.Select(r => r.Name));
            Assert.Equal(before.Select(r => r.Caption), after.Select(r => r.Caption));
            for (var i = 0; i < before.Count; i++)
            {
                var row = before[i];
                // The 4/14/4 pair keep their own two rows, and the Fanatec wiring has no twin.
                var expected = row.ShapeIds.SelectMany(id => twins.Any(t => t.ShapeId == id + "-reversed") ? new[] { id, id + "-reversed" } : new[] { id });
                Assert.Equal(expected, after[i].ShapeIds);
            }
            Assert.Equal(new[] { "4-14-4" }, after.Single(r => r.Name == "OpenDash 4/14/4").ShapeIds);
            Assert.Equal(new[] { "4-14-4-reversed" }, after.Single(r => r.Name == "OpenDash 4/14/4 reversed").ShapeIds);
            Assert.Equal(new[] { "3-9-3-fanatec" }, after.Single(r => r.Name == "OpenDash 3/9/3 Fanatec").ShapeIds);
            Assert.Equal(new[] { "3-10-3", "3-10-3-reversed" }, after.Single(r => r.Name == "OpenDash 3/10/3").ShapeIds);
            Assert.Equal(new[] { "1-4-1", "1-4-1-reversed", "1-5-1", "1-5-1-reversed" }, after.Single(r => r.Name == "OpenDash 1/4/1 … 1/12/1").ShapeIds.Take(4));
            // Every profile in exactly one row, twins included.
            var members = after.SelectMany(r => r.ShapeIds).ToList();
            Assert.Equal(built.Count, members.Count);
            Assert.Equal(built.Select(p => p.ShapeId).OrderBy(x => x, StringComparer.Ordinal), members.OrderBy(x => x, StringComparer.Ordinal));
            // 62 profiles become 121: every plain shape but the 4/14/4, which had its twin already.
            Assert.Equal(62, plain.Count);
            Assert.Equal(121, built.Count);

            // A twin whose sibling this build does not carry is still offered, in the row its geometry
            // puts it in.
            var orphan = PanelLightRows.Rows(new[] { new LightProfile("0-10-0-reversed", "OpenDash 0/10/0 reversed") });
            Assert.Equal(new[] { "0-10-0-reversed" }, Assert.Single(orphan).ShapeIds);
        }

        [Fact]
        public void A_bare_run_has_a_reversed_twin_and_a_pre_grid_brow_has_none()
        {
            // A bare run is 0-N-0 and its twin 0-N-0-reversed, which share a row like any other pair.
            var twin = LightShape.Parse("0-15-0-reversed");
            Assert.Equal(PanelLightRows.Wheel, twin.Placement);
            Assert.Equal(15, twin.Centre);
            Assert.True(twin.Bare);
            Assert.True(twin.Reversed);
            var rows = PanelLightRows.Rows(new[] { new LightProfile("0-15-0", null), new LightProfile("0-15-0-reversed", null) });
            Assert.Equal(new[] { "0-15-0", "0-15-0-reversed" }, Assert.Single(rows).ShapeIds);
            // brow-N is from before the grid and is read only for an old profile's sake; no build wrote
            // a twin of one, so an id that claims to be one is not read as a brow.
            Assert.Equal(PanelLightRows.Brow, LightShape.Parse("brow-15").Placement);
            Assert.Null(LightShape.Parse("brow-15-reversed"));
            Assert.Null(LightShape.Parse("brow-15-fanatec"));
        }

        [Fact]
        public void The_grouped_rows_are_named_by_their_own_ends()
        {
            // The name is read off the members rather than written down, so a length added to strip.ts
            // moves the end of the range instead of leaving it a length short.
            var rows = PanelLightRows.Rows(FullBuild().Concat(new[] { new LightProfile("0-30-0", "OpenDash 0/30/0") }));
            Assert.Contains(rows, r => r.Name == "OpenDash 0/4/0 … 0/30/0" && r.Caption == "Bare runs and brows, 23 lengths");
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
            Assert.Contains(rows, r => r.Name == "OpenDash 4/14/4 reversed");
        }

        [Fact]
        public void One_profile_alone_is_a_row_and_not_a_range()
        {
            // What the test project's own resources are, and what a partial artifact would be. A range of
            // one would read "OpenDash 0/10/0 … 0/10/0", and "bare runs, one lengths" is not English.
            var rows = PanelLightRows.Rows(new[] { new LightProfile("0-10-0", "OpenDash 0/10/0") });
            Assert.Equal("OpenDash 0/10/0", Assert.Single(rows).Name);
            Assert.Equal("Bare run", rows[0].Caption);
        }

        [Fact]
        public void A_row_is_called_what_the_build_called_the_profile()
        {
            // rpmStripProfileName() wrote it and FlagBoxProfile.ProfileNameOf read it back, so a rename
            // there arrives here without an edit.
            var renamed = PanelLightRows.Rows(new[] { new LightProfile("4-14-4", "OpenDash the wide one") });
            Assert.Equal("OpenDash the wide one", Assert.Single(renamed).Name);

            // And when the Name cannot be read at all, the id is spelled the way the generator spells it
            // rather than shown raw.
            var unnamed = PanelLightRows.Rows(new[] { new LightProfile("4-14-4", null), new LightProfile("brow-9", null) });
            Assert.Equal(new[] { "OpenDash 4/14/4", "OpenDash brow 9" }, unnamed.Select(r => r.Name));
        }

        [Fact]
        public void An_id_that_cannot_be_read_gets_its_own_row_rather_than_a_guess()
        {
            Assert.Null(LightShape.Parse("OpenDash"));
            Assert.Null(LightShape.Parse("4-14"));
            Assert.Null(LightShape.Parse("4-x-4"));
            Assert.Null(LightShape.Parse("brow-x"));
            Assert.Null(LightShape.Parse("-1-9-1"));

            var rows = PanelLightRows.Rows(new[] { new LightProfile("something-else", "OpenDash something else") });
            Assert.Equal("OpenDash something else", Assert.Single(rows).Name);
            Assert.Equal("Strip", rows[0].Caption);
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
            // Read off PanelMatrix.ProfileRow, the words the Matrix pill draws beside it, rather than from a
            // table of its own, except for the one pairing that table cannot carry: the uninstalled dot is
            // status.notInstalled and its label text.label.
            Assert.Equal(Theme.StatusUpToDate, PanelLightRows.DotHex(FlagBoxInstallState.UpToDate));
            Assert.Equal(Theme.StatusUpToDate, PanelLightRows.DotHex(FlagBoxInstallState.Outdated));
            Assert.Equal(Theme.StatusFailed, PanelLightRows.DotHex(FlagBoxInstallState.Failed));
            Assert.Equal(Theme.StatusNotInstalled, PanelLightRows.DotHex(FlagBoxInstallState.NotInstalled));
            Assert.Equal(Theme.StatusNotInstalled, PanelLightRows.DotHex(FlagBoxInstallState.Unavailable));
            Assert.Equal(Theme.StatusNotInstalled, PanelLightRows.DotHex(FlagBoxInstallState.NotEmbedded));
        }

        /// <summary>
        /// The Matrix page's pill draws DotHex beside PanelMatrix.ProfileRow's words, so for every state the
        /// two agree, the uninstalled pairing excepted. The Updates table's older profile says "Update
        /// available" in amber, and moving LightRow's ink moved this dot with it once: an amber dot beside
        /// green words on a page the Updates rows do not draw.
        /// </summary>
        [Fact]
        public void The_matrix_pill_s_dot_agrees_with_its_words_in_every_state()
        {
            foreach (FlagBoxInstallState state in Enum.GetValues(typeof(FlagBoxInstallState)))
            {
                var ink = PanelMatrix.ProfileRow(state, "0.4.0").StateHex;
                var expected = ink == Theme.TextLabel ? Theme.StatusNotInstalled : ink;
                Assert.Equal(expected, PanelLightRows.DotHex(state));
            }
            var matrix = RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => System.IO.Path.GetFileName(p) == "SettingsControl.Matrix.cs"));
            if (matrix.Contains("PanelLightRows.DotHex(")) Assert.Contains("PanelMatrix.ProfileRow(", matrix);
        }

        [Fact]
        public void An_unreachable_driver_does_not_promise_a_file_that_was_never_written()
        {
            // The whole reason a strip row has a tooltip of its own. FlagBoxInstallPlan.Summary offers the
            // copy in the OpenDash folder, and FlagBoxProfile.Extract writes only the flag box there, so
            // that sentence over a strip row sends a driver looking for a file nothing ever created.
            var strip = PanelLightRows.Tooltip(5, new FlagBoxPlan { State = FlagBoxInstallState.Unavailable });
            Assert.Equal(PanelLightRows.Unavailable, strip);
            Assert.DoesNotContain("OpenDash folder", strip, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("by hand", strip, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("OpenDash folder", FlagBoxInstallPlan.Summary(new FlagBoxPlan { State = FlagBoxInstallState.Unavailable }, null), StringComparison.Ordinal);
        }

        /// <summary>
        /// What a strip row says of the rig's strips, and the one press it can have.
        /// </summary>
        /// <remarks>
        /// This replaces a test of the wording the rows had while they compared the embedded profiles
        /// with SimHub, member by member and worst first: "at least one of these seven is not installed,
        /// install them all". That wording described a press the rows lost when a strip became a bar, and
        /// a reduction #457 replaces, so it is no longer true of anything the panel draws.
        /// </remarks>
        [Fact]
        public void A_strip_tooltip_says_what_the_rigs_strips_hold_and_offers_only_the_press_the_row_has()
        {
            // No strip of the shape in SimHub: a strip is added on the LEDs page, and the row has no
            // Install press to name, so it says where to go.
            var one = PanelLightRows.Tooltip(1, new FlagBoxPlan { State = FlagBoxInstallState.NotInstalled });
            Assert.Equal("No strip of this shape is in SimHub. Add one on the LEDs page.", one);
            var group = PanelLightRows.Tooltip(9, new FlagBoxPlan { State = FlagBoxInstallState.NotInstalled });
            Assert.Equal("No strip of these shapes is in SimHub. Add one on the LEDs page.", group);

            // Current: the version it carries, and no warning, because there is no press to warn about.
            var current = PanelLightRows.Tooltip(9, new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, InstalledVersion = "0.3.0", EmbeddedVersion = "0.3.0" });
            Assert.Equal("Installed and up to date (0.3.0).", current);
            Assert.DoesNotContain(FlagBoxInstallPlan.Replaces, current, StringComparison.Ordinal);
            Assert.DoesNotContain("(", PanelLightRows.Tooltip(1, new FlagBoxPlan { State = FlagBoxInstallState.UpToDate }), StringComparison.Ordinal);

            // Older: both versions, and the warning the flag box's Update carries, since the row's Update
            // replaces the copy in SimHub by id and whatever was changed in it there goes with it.
            var older = PanelLightRows.Tooltip(1, new FlagBoxPlan { State = FlagBoxInstallState.Outdated, InstalledVersion = "0.3.0-rc.8", EmbeddedVersion = "0.3.0" });
            Assert.Equal("A newer profile is available (0.3.0-rc.8 to 0.3.0). " + FlagBoxInstallPlan.Replaces, older);
            // A strip installed before strips carried a version says the new one alone rather than
            // inventing an old one.
            var unstamped = PanelLightRows.Tooltip(1, new FlagBoxPlan { State = FlagBoxInstallState.Outdated, EmbeddedVersion = "0.3.0" });
            Assert.Equal("A newer profile is available (0.3.0). " + FlagBoxInstallPlan.Replaces, unstamped);

            Assert.Equal(PanelLightRows.Unavailable, PanelLightRows.Tooltip(9, new FlagBoxPlan { State = FlagBoxInstallState.Unavailable }));
            Assert.Equal("Install failed. See SimHub's log.", PanelLightRows.Tooltip(1, new FlagBoxPlan { State = FlagBoxInstallState.Failed }));
        }

        /// <summary>What a strip built by this checkout's VERSION says in its description.</summary>
        private const string Current = "Shift lights, flags and the spotter on one LED strip. Built by OpenDash 0.3.0; do not edit here, it is replaced on update.";

        private const string Older = "Shift lights, flags and the spotter on one LED strip. Built by OpenDash 0.3.0-rc.8; do not edit here, it is replaced on update.";

        private static IList<KeyValuePair<LedBar, FlagBoxPlan>> Census(IEnumerable<LedBar> bars, params IEnumerable<InstalledProfile>[] devices)
        {
            return bars.Select(bar => new KeyValuePair<LedBar, FlagBoxPlan>(bar, LedBarProfile.Plan(bar, Current, devices))).ToList();
        }

        /// <summary>
        /// The rig #457 was reported from: a 3/9/3 and a 3/9/3 Fanatec added on the Arduino, and every
        /// strip row saying Not installed beside them.
        /// </summary>
        /// <remarks>
        /// The two ids are the ones SimHub wrote into ArduinoRGBLedsSettings.json on that run, and they
        /// are what the two bars derive from their namespaces, which is the whole of the fix: a row asks
        /// about the ids the rig's bars derive. It used to ask about the embedded profile's own id,
        /// cf7dc3c7-... for the 3/9/3, which no bar ever carries, so it could never find one. A profile
        /// under that id is not a bar's and the row does not speak for it.
        /// </remarks>
        [Fact]
        public void A_row_finds_the_rigs_strips_by_the_ids_their_bars_derive()
        {
            var plain = new LedBar { Name = "OpenDash 3/9/3", Namespace = "Led393", Shape = "3-9-3", Device = LedBar.ArduinoDevice };
            var fanatec = new LedBar { Name = "OpenDash 3/9/3 Fanatec", Namespace = "Led393Fanatec", Shape = "3-9-3-fanatec", Device = LedBar.ArduinoDevice };
            var arduino = new List<InstalledProfile>
            {
                new InstalledProfile { ProfileId = Guid.Parse("b8000ec9-0bac-5ab5-993a-4c89f11b692c"), Name = "OpenDash 3/9/3", Description = Current },
                new InstalledProfile { ProfileId = Guid.Parse("cf2f576b-a3e3-5f53-be30-5d74ece9cf6c"), Name = "OpenDash 3/9/3 Fanatec", Description = Current },
                new InstalledProfile { ProfileId = Guid.NewGuid(), Name = "Somebody's own" },
            };
            Assert.Equal(arduino[0].ProfileId, LedBarProfile.IdFor(plain.Namespace));
            Assert.Equal(arduino[1].ProfileId, LedBarProfile.IdFor(fanatec.Namespace));

            var census = Census(new[] { plain, fanatec }, arduino);
            var rows = PanelLightRows.Rows(FullBuild());
            var sideThree = rows.Single(row => row.ShapeIds.Contains("3-9-3"));
            var fanatecRow = rows.Single(row => row.ShapeIds.Contains("3-9-3-fanatec"));
            Assert.Equal("OpenDash 3/4/3 … 3/12/3", sideThree.Name);

            var plan = PanelLightRows.RowPlan(sideThree.ShapeIds, census, true);
            Assert.Equal(FlagBoxInstallState.UpToDate, plan.State);
            Assert.Equal("0.3.0", plan.InstalledVersion);
            Assert.Equal(FlagBoxInstallState.UpToDate, PanelLightRows.RowPlan(fanatecRow.ShapeIds, census, true).State);
            // Every other shape has no strip on this rig, and says so.
            foreach (var row in rows.Where(row => row != sideThree && row != fanatecRow))
            {
                Assert.Equal(FlagBoxInstallState.NotInstalled, PanelLightRows.RowPlan(row.ShapeIds, census, true).State);
            }

            // The embedded profile's own id is not a strip of the rig's: a copy of it in SimHub reads the
            // rig-wide settings and belongs to no bar, and a row with no bar of its shape is Not installed.
            var embeddedOnly = new List<InstalledProfile>
            {
                new InstalledProfile { ProfileId = Guid.Parse("cf7dc3c7-20e7-567d-b6cf-fd9cd188b746"), Name = "OpenDash 3/9/3", Description = Current },
            };
            Assert.Equal(FlagBoxInstallState.NotInstalled, PanelLightRows.RowPlan(sideThree.ShapeIds, Census(new LedBar[0], embeddedOnly), true).State);
        }

        [Fact]
        public void A_reversed_bar_is_found_by_the_row_of_the_profile_it_installs()
        {
            // A 4/14/4 wired from the far end installs the reversed profile, whose row is its own; a
            // reversed 3/10/3 installs a twin that rides in the 3/10/3's row. #503.
            var mld = new LedBar { Name = "MLD", Namespace = "LedMLD", Shape = "4-14-4-reversed", Device = LedBar.ArduinoDevice };
            var gtsl = new LedBar { Name = "GTSL", Namespace = "LedGTSL", Shape = "3-10-3", Reversed = true, Device = LedBar.ArduinoDevice };
            mld.Normalise();
            gtsl.Normalise();
            var arduino = new List<InstalledProfile>
            {
                new InstalledProfile { ProfileId = LedBarProfile.IdFor(mld.Namespace), Name = "MLD", Description = Current },
                new InstalledProfile { ProfileId = LedBarProfile.IdFor(gtsl.Namespace), Name = "GTSL", Description = Current },
            };
            var census = Census(new[] { mld, gtsl }, arduino);
            var built = FullBuild().Concat(new[] { new LightProfile("3-10-3-reversed", "OpenDash 3/10/3 reversed") }).ToList();
            var rows = PanelLightRows.Rows(built);

            Assert.Equal(FlagBoxInstallState.UpToDate, PanelLightRows.RowPlan(rows.Single(r => r.Name == "OpenDash 4/14/4 reversed").ShapeIds, census, true).State);
            Assert.Equal(FlagBoxInstallState.NotInstalled, PanelLightRows.RowPlan(rows.Single(r => r.Name == "OpenDash 4/14/4").ShapeIds, census, true).State);
            Assert.Equal(FlagBoxInstallState.UpToDate, PanelLightRows.RowPlan(rows.Single(r => r.Name == "OpenDash 3/10/3").ShapeIds, census, true).State);
        }

        /// <summary>
        /// A row reads Outdated while any strip of its shapes that is in SimHub is older than this build,
        /// and its Update rewrites exactly those.
        /// </summary>
        [Fact]
        public void A_row_reports_an_older_strip_and_its_update_rewrites_only_that_one()
        {
            var rim = new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3" };
            var dash = new LedBar { Name = "Dash", Namespace = "LedDash", Shape = "3-12-3" };
            var unstamped = new LedBar { Name = "Old", Namespace = "LedOld", Shape = "3-4-3" };
            var gone = new LedBar { Name = "Gone", Namespace = "LedGone", Shape = "3-8-3" };
            var arduino = new List<InstalledProfile>
            {
                new InstalledProfile { ProfileId = LedBarProfile.IdFor(rim.Namespace), Description = Older },
                new InstalledProfile { ProfileId = LedBarProfile.IdFor(dash.Namespace), Description = Current },
            };
            var sideThree = new[] { "3-4-3", "3-8-3", "3-9-3", "3-12-3" };

            // Rim alone is older: the row cannot read current while it is.
            var plan = PanelLightRows.RowPlan(sideThree, Census(new[] { rim, dash, gone }, arduino), true);
            Assert.Equal(FlagBoxInstallState.Outdated, plan.State);
            Assert.Equal("0.3.0", plan.EmbeddedVersion);
            // Two strips at two versions: the pill names neither rather than one of them.
            Assert.Null(plan.InstalledVersion);
            Assert.Equal(new[] { rim }, PanelLightRows.OutdatedBars(sideThree, Census(new[] { rim, dash, gone }, arduino)));

            // Gone has no copy anywhere, and neither pulls the row down to Not installed nor is rewritten
            // by an Update: there is nothing of it in SimHub to bring forward.
            Assert.DoesNotContain(gone, PanelLightRows.OutdatedBars(sideThree, Census(new[] { rim, dash, gone }, arduino)));

            // A strip installed before strips carried a version is older too, because nothing says it is
            // current; its copy names no version and the row invents none.
            arduino.Add(new InstalledProfile { ProfileId = LedBarProfile.IdFor(unstamped.Namespace), Description = null });
            var alone = PanelLightRows.RowPlan(new[] { "3-4-3" }, Census(new[] { unstamped }, arduino), true);
            Assert.Equal(FlagBoxInstallState.Outdated, alone.State);
            Assert.Null(alone.InstalledVersion);
            Assert.Equal(new[] { unstamped }, PanelLightRows.OutdatedBars(new[] { "3-4-3" }, Census(new[] { unstamped }, arduino)));

            // And a row whose strips are all current has nothing to update.
            Assert.Empty(PanelLightRows.OutdatedBars(new[] { "3-12-3" }, Census(new[] { rim, dash, gone }, arduino)));
        }

        /// <summary>No LED device could be read: the row says so rather than Not installed.</summary>
        [Fact]
        public void A_rig_whose_LED_devices_cannot_be_read_is_unavailable_rather_than_empty()
        {
            var rim = new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3" };
            Assert.Equal(FlagBoxInstallState.Unavailable, PanelLightRows.RowPlan(new[] { "3-9-3" }, Census(new[] { rim }), false).State);
            Assert.Equal(FlagBoxInstallState.Unavailable, PanelLightRows.RowPlan(new[] { "3-9-3" }, Census(new LedBar[0]), false).State);
            // Reachable and holding nothing of ours is a rig with no such strip, which is a fact.
            Assert.Equal(FlagBoxInstallState.NotInstalled, PanelLightRows.RowPlan(new[] { "3-9-3" }, Census(new LedBar[0], new List<InstalledProfile>()), true).State);
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
        /// `fanatec(...)` rows of LEGACY_SHAPES. The grid names none: it is sides against centres and the
        /// panel groups it by side, so there is nothing left to caption by hardware.</summary>
        /// <remarks>
        /// A row's id is its geometry plus whatever suffix its spelling adds, which is the one thing about
        /// strip.ts this has to know twice. `wheel(..., { reversed: true })` and `fanatec(...)` are the two
        /// wirings that make a second profile of one geometry, so both are read here or the caption mirror
        /// below would see two shapes claiming the same id.
        /// </remarks>
        private static IList<GeneratedShape> GeneratedWheels()
        {
            var text = File.ReadAllText(StripTs());
            var shapes = new List<GeneratedShape>();
            foreach (Match row in Regex.Matches(text, @"^\s*(wheel|fanatec)\((\d+), (\d+), (\d+)(?:, \{(.*)\})?\),\s*$", RegexOptions.Multiline))
            {
                var options = row.Groups[5].Value;
                shapes.Add(new GeneratedShape
                {
                    Id = row.Groups[2].Value + "-" + row.Groups[3].Value + "-" + row.Groups[4].Value
                        + (options.Contains("reversed: true") ? "-reversed" : string.Empty)
                        + (row.Groups[1].Value == "fanatec" ? "-fanatec" : string.Empty),
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
            // The captions abbreviate what strip.ts spells out, because the caption is drawn as a tracked
            // label and the full device families are wider than the row. Every word of one still has
            // to appear in the generator's own list, so a device renamed there fails here rather than
            // leaving the panel naming hardware that no longer exists.
            var devices = GeneratedWheels().ToDictionary(s => s.Id, s => string.Join(" | ", s.Devices), StringComparer.Ordinal);
            foreach (var named in PanelLightRows.NamedShapes)
            {
                var spelled = devices[named.Key];
                var words = named.Value.Split(new[] { ' ', ',', '·' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w != "Strip");
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
            Assert.Equal("OpenDash 0/4/0 … 0/25/0", rows[0].Name);
            Assert.Equal("Bare runs and brows, 22 lengths", rows[0].Caption);
            Assert.Equal("OpenDash 1/4/1 … 1/12/1", rows[1].Name);
            Assert.Equal("Strips, one LED at each end, nine lengths", rows[1].Caption);
            Assert.Equal("Strips, two LEDs at each end, nine lengths", rows[2].Caption);
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
            // SettingsControl.Updates.Lights.cs reads them out of the assembly.
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
                    "OpenDash 4/14/4",
                    "OpenDash 4/14/4 reversed",
                    "OpenDash 3/9/3 Fanatec",
                    "OpenDash 3/10/3",
                    "OpenDash 0/4/0 … 0/25/0",
                    "OpenDash 1/4/1 … 1/12/1",
                    "OpenDash 2/4/2 … 2/12/2",
                    "OpenDash 3/4/3 … 3/12/3",
                    "OpenDash 4/4/4 … 4/12/4",
                    "OpenDash 5/10/5",
                },
                rows.Select(r => r.Name));
            // Sixty-two: five sides of nine centres and thirteen longer bare runs, less the one the
            // legacy list already spells, plus the five that shipped before the grid. And since #503 a
            // reversed twin of every plain shape but the 4/14/4, which had one: fifty-nine more, riding
            // in their siblings' rows, for 121. On CI Resources/ holds this commit's own dash build, so
            // all fifty-nine are required there: LedBar.SupportsReversal offers the switch on every plain
            // shape, and a package without the twins would leave ProfileShapeId naming a profile nothing
            // embeds. Only a stale build/ in a local checkout, from before the twins, may carry none.
            var members = rows.SelectMany(r => r.ShapeIds).ToList();
            var twins = members.Count(id => id.EndsWith("-reversed", StringComparison.Ordinal) && id != "4-14-4-reversed");
            if (OnCI) Assert.True(twins == 59, twins + " reversed twins; CI builds the dash from this commit and needs all 59");
            else Assert.True(twins == 0 || twins == 59, twins + " reversed twins");
            Assert.Equal(62 + twins, members.Count);
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
