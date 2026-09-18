// LedBarTests.cs: the strips as instances -- what a bar owns, what stays the rig's, and the rewrite that
// makes one embedded profile into one bar's own.
//
// The rewrite is held against a profile the build actually wrote rather than a fixture, because what it
// has to survive is the file the generator emits: every property reference is bracketed, the two fields
// it edits appear exactly once, and a fourth rig-wide name appearing in a future profile has to fail here
// rather than quietly stay rig-wide.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class LedBarTests
    {
        /// <summary>One profile the build wrote, from the folder the plugin embeds or from the dash build
        /// output, or null when nothing has been built in this checkout.</summary>
        private static string BuiltStripProfile()
        {
            foreach (var folder in new[] { RepoPaths.EmbeddedResources(), RepoPaths.BuildOutput() })
            {
                if (!Directory.Exists(folder)) continue;
                var file = Directory.GetFiles(folder, "*" + FlagBoxProfile.ProfileExtension)
                    .FirstOrDefault(f => FlagBoxProfile.ShapeIdOf(Path.GetFileName(f)) == "3-9-3");
                if (file != null) return FlagBoxProfile.ReadFile(file);
            }
            return null;
        }

        [Fact]
        public void A_new_install_has_no_bars()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            Assert.Empty(settings.LedBarList());
            // And declares nothing of a bar's, so the pin in declared-properties.txt is what it was: the
            // rig-wide LedCentre is there and no namespaced copy of it is.
            var declared = settings.DeclaredProperties().ToList();
            Assert.Contains(Contract.LedCentre, declared);
            Assert.DoesNotContain(declared, name => name.Length > Contract.LedCentre.Length && name.EndsWith(Contract.LedCentre, StringComparison.Ordinal));
        }

        [Fact]
        public void A_bar_takes_the_rigs_own_settings_as_its_starting_point()
        {
            var settings = new OpenDashSettings { LedCentre = "brake", LedRpmStyle = Contract.LedRpmStyleCar, LedFlagAnimation = false };
            settings.Normalise();

            var bar = settings.AddLedBar("3-9-3", "Rim");
            Assert.Equal("Rim", bar.Name);
            Assert.Equal("LedRim", bar.Namespace);
            Assert.Equal("brake", settings.BarCentre(bar.Namespace));
            Assert.Equal(Contract.LedRpmStyleCar, settings.BarRpmStyle(bar.Namespace));
            Assert.False(settings.BarFlagAnimation(bar.Namespace));
        }

        /// <summary>The whole point: two bars, two answers.</summary>
        [Fact]
        public void Two_bars_are_configured_apart()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            var rim = settings.AddLedBar("3-9-3", "Rim");
            var brow = settings.AddLedBar("brow-15", "Brow");

            rim.Centre = "rpm";
            brow.Centre = "fuel";
            Assert.Equal("rpm", settings.BarCentre(rim.Namespace));
            Assert.Equal("fuel", settings.BarCentre(brow.Namespace));
            // And each declares its own three names, so the two profiles cannot read one another's.
            var names = settings.DeclaredProperties().ToList();
            Assert.Contains("LedRimLedCentre", names);
            Assert.Contains("LedBrowLedCentre", names);
            Assert.Equal(names.Count, names.Distinct().Count());
        }

        /// <summary>
        /// Two bars called the same thing get namespaces of their own.
        /// </summary>
        /// <remarks>
        /// The prefix has to be applied before the deduplication and not after, or both would be asked
        /// about "Rim", both told it was free, and both attached as "LedRim" -- one profile silently
        /// reading the other's settings.
        /// </remarks>
        [Fact]
        public void Two_bars_of_one_name_do_not_share_a_namespace()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            var first = settings.AddLedBar("3-9-3", "Rim");
            var second = settings.AddLedBar("3-9-3", "Rim");
            Assert.NotEqual(first.Namespace, second.Namespace);
            Assert.NotEqual(first.Name, second.Name);
            Assert.NotEqual(LedBarProfile.IdFor(first.Namespace), LedBarProfile.IdFor(second.Namespace));
        }

        [Fact]
        public void A_removed_bar_leaves_the_rig_reading_its_own_answer_again()
        {
            var settings = new OpenDashSettings { LedCentre = "fuel" };
            settings.Normalise();
            var bar = settings.AddLedBar("3-9-3", "Rim");
            bar.Centre = "brake";
            Assert.True(settings.RemoveLedBar(bar.Namespace));
            // A delegate outlives the bar it was attached for until SimHub restarts, so the reader has to
            // answer something rather than throw on SimHub's data thread.
            Assert.Equal("fuel", settings.BarCentre(bar.Namespace));
            Assert.False(settings.RemoveLedBar(bar.Namespace));
        }

        /// <summary>The id is how the installer recognises its own, so it has to be the same every time
        /// or a restart would add a second copy beside the first.</summary>
        [Fact]
        public void A_bars_profile_id_is_derived_and_stable()
        {
            Assert.Equal(LedBarProfile.IdFor("LedRim"), LedBarProfile.IdFor("LedRim"));
            Assert.NotEqual(LedBarProfile.IdFor("LedRim"), LedBarProfile.IdFor("LedBrow"));
            Assert.NotEqual(Guid.Empty, LedBarProfile.IdFor("LedRim"));
            // Version 5, which is what a name-based UUID says it is. Read off the string rather than off
            // ToByteArray(), whose first three groups are little-endian and so do not put the version
            // nibble where the RFC does.
            Assert.Equal('5', LedBarProfile.IdFor("LedRim").ToString("D")[14]);
        }

        [Fact]
        public void The_rewrite_moves_three_names_two_fields_and_nothing_else()
        {
            var embedded = BuiltStripProfile();
            if (embedded == null) return;

            var bar = new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3" };
            var mine = LedBarProfile.For(bar, embedded);

            Assert.Equal("Rim", FlagBoxProfile.ProfileNameOf(mine));
            Assert.Contains("\"ProfileId\": \"" + LedBarProfile.IdFor("LedRim").ToString("D") + "\"", mine);
            foreach (var setting in LedBarProfile.BarSettings)
            {
                Assert.DoesNotContain("[OpenDash." + setting + "]", mine);
                Assert.Contains("[OpenDash.LedRim" + setting + "]", mine);
            }
            // What stays the rig's stays the rig's: the brightness, the low-fuel threshold and the car's
            // own shift pattern are not a strip's business.
            foreach (var shared in new[] { "LightsBrightness", "LightsNightMode", "LightsLowFuelLaps", "LedMirrorReady" })
            {
                if (!embedded.Contains("[OpenDash." + shared + "]")) continue;
                Assert.Contains("[OpenDash." + shared + "]", mine);
            }
            // And the file is otherwise what the build wrote: undoing the three names and dropping the
            // two edited lines from both sides leaves two identical documents. A rewrite that touched
            // anything else -- a colour, a threshold, a container -- fails here.
            var undone = mine;
            foreach (var setting in LedBarProfile.BarSettings)
            {
                undone = undone.Replace("[OpenDash.LedRim" + setting + "]", "[OpenDash." + setting + "]");
            }
            Assert.Equal(WithoutEditedFields(embedded), WithoutEditedFields(undone));
        }

        /// <summary>The document less the two lines a rewrite edits, so the rest can be compared whole.</summary>
        private static string WithoutEditedFields(string json)
        {
            return string.Join(
                "\n",
                json.Split('\n').Where(line => !line.TrimStart().StartsWith("\"Name\":", StringComparison.Ordinal)
                    && !line.TrimStart().StartsWith("\"ProfileId\":", StringComparison.Ordinal)));
        }

        /// <summary>A name with a quote in it would end the JSON string early and hand SimHub a file it
        /// cannot read at all.</summary>
        [Fact]
        public void A_name_is_escaped_into_the_profile()
        {
            var embedded = BuiltStripProfile();
            if (embedded == null) return;
            var bar = new LedBar { Name = "the \"good\" one", Namespace = "LedGood", Shape = "3-9-3" };
            var mine = LedBarProfile.For(bar, embedded);
            Assert.Contains("\"Name\": \"the \\\"good\\\" one\"", mine);
            Assert.Equal("the \"good\" one", FlagBoxProfile.ProfileNameOf(mine));
        }

        [Fact]
        public void A_bar_the_rig_does_not_hold_rewrites_nothing()
        {
            Assert.Null(LedBarProfile.For(null, "{}"));
            Assert.Null(LedBarProfile.For(new LedBar { Namespace = "LedRim" }, null));
        }

        /// <summary>A hand-edited file cannot reach a profile as itself.</summary>
        [Fact]
        public void Normalise_repairs_a_bar_that_was_written_by_hand()
        {
            var settings = new OpenDashSettings
            {
                LedBars = new List<LedBar>
                {
                    null,
                    new LedBar { Name = "Rim", Shape = "3-9-3", Centre = "nonsense", RpmStyle = "nonsense" },
                    new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3" },
                },
            };
            settings.Normalise();
            Assert.Equal(2, settings.LedBarList().Count);
            Assert.Equal(Contract.DefaultLedCentre, settings.LedBarList()[0].Centre);
            Assert.Equal(Contract.DefaultLedRpmStyle, settings.LedBarList()[0].RpmStyle);
            Assert.NotEqual(settings.LedBarList()[0].Namespace, settings.LedBarList()[1].Namespace);
        }
    }
}
