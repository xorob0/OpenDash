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

            var bar = settings.AddLedBar("3-9-3", "Rim", LedBar.ArduinoDevice);
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
            var rim = settings.AddLedBar("3-9-3", "Rim", LedBar.ArduinoDevice);
            var brow = settings.AddLedBar("brow-15", "Brow", LedBar.ArduinoDevice);

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
            var first = settings.AddLedBar("3-9-3", "Rim", LedBar.ArduinoDevice);
            var second = settings.AddLedBar("3-9-3", "Rim", LedBar.ArduinoDevice);
            Assert.NotEqual(first.Namespace, second.Namespace);
            Assert.NotEqual(first.Name, second.Name);
            Assert.NotEqual(LedBarProfile.IdFor(first.Namespace), LedBarProfile.IdFor(second.Namespace));
        }

        [Fact]
        public void A_removed_bar_leaves_the_rig_reading_its_own_answer_again()
        {
            var settings = new OpenDashSettings { LedCentre = "fuel" };
            settings.Normalise();
            var bar = settings.AddLedBar("3-9-3", "Rim", LedBar.ArduinoDevice);
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

        /// <summary>
        /// A bar is looked for by the id it derives, on every LED device, and the best reading wins.
        /// </summary>
        /// <remarks>
        /// Every device because SimHub keeps a list per device and a bar moved from the Arduino to a wheel
        /// is installed on the wheel; the install takes the copy out of every other list, so the Arduino
        /// holding nothing is the expected answer there and not a contradiction. A device whose list could
        /// not be read is a null entry, and any device that could be read outranks it.
        /// </remarks>
        [Fact]
        public void A_bar_is_found_by_its_own_id_on_whichever_device_holds_it()
        {
            const string current = "Built by OpenDash 0.3.0; do not edit here.";
            var bar = new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3" };
            var arduino = new List<InstalledProfile> { new InstalledProfile { ProfileId = Guid.NewGuid(), Name = "Rim" } };
            var wheel = new List<InstalledProfile> { new InstalledProfile { ProfileId = LedBarProfile.IdFor("LedRim"), Name = "Rim", Description = current } };

            var found = LedBarProfile.Plan(bar, current, new IEnumerable<InstalledProfile>[] { arduino, null, wheel });
            Assert.Equal(FlagBoxInstallState.UpToDate, found.State);
            Assert.Equal(LedBarProfile.IdFor("LedRim"), found.Existing);
            Assert.Equal("0.3.0", found.InstalledVersion);

            // A profile of the same name is not the bar's: the name is the user's to change, the id is not.
            Assert.Equal(FlagBoxInstallState.NotInstalled, LedBarProfile.Plan(bar, current, new IEnumerable<InstalledProfile>[] { arduino }).State);
            // Nothing that could be read, or no device at all, is unavailable rather than absent.
            Assert.Equal(FlagBoxInstallState.Unavailable, LedBarProfile.Plan(bar, current, new IEnumerable<InstalledProfile>[] { null }).State);
            Assert.Equal(FlagBoxInstallState.Unavailable, LedBarProfile.Plan(bar, current, new IEnumerable<InstalledProfile>[0]).State);
            Assert.Equal("0.3.0", LedBarProfile.Plan(bar, current, null).EmbeddedVersion);

            // Installed by an older build, or before strips carried a version: older, either way.
            wheel[0].Description = "Built by OpenDash 0.3.0-rc.8; do not edit here.";
            Assert.Equal(FlagBoxInstallState.Outdated, LedBarProfile.Plan(bar, current, new[] { wheel }).State);
            wheel[0].Description = null;
            Assert.Equal(FlagBoxInstallState.Outdated, LedBarProfile.Plan(bar, current, new[] { wheel }).State);
        }

        /// <summary>
        /// The strip the build wrote says who built it and at which version, and a bar's copy of it still
        /// does after the rewrite.
        /// </summary>
        /// <remarks>
        /// The cross-language half of the version contract for strips, as FlagBoxInstallPlanTests holds it
        /// for the flag box: rpmStripDescription() in packages/dash/src/leds/rpmStrip.ts writes the marker
        /// and FlagBoxInstallPlan.VersionOf reads it back out of the copy in SimHub. A strip that carried
        /// none read as current whatever built it (#457).
        /// </remarks>
        [Fact]
        public void A_bars_profile_carries_the_version_the_build_stamped()
        {
            var embedded = BuiltStripProfile();
            if (embedded == null)
            {
                Assert.False(OnCI, "no built 3-9-3 strip in " + RepoPaths.EmbeddedResources() + " or " + RepoPaths.BuildOutput());
                return;
            }

            var mine = LedBarProfile.For(new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3" }, embedded);
            Assert.Equal(FlagBoxInstallPlan.Author, FlagBoxProfile.AuthorOf(mine));
            Assert.Equal(RepoPaths.Version(), FlagBoxInstallPlan.VersionOf(FlagBoxProfile.DescriptionOf(mine)));
            Assert.Equal(FlagBoxProfile.DescriptionOf(embedded), FlagBoxProfile.DescriptionOf(mine));
        }

        /// <summary>See FlagBoxInstallPlanTests: on CI the dash artifact is in Resources/ before the tests
        /// run, so a missing strip there is the contract having parted company rather than a local
        /// checkout that has built nothing.</summary>
        private static bool OnCI =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        [Fact]
        public void The_rewrite_moves_the_bars_own_names_two_fields_and_nothing_else()
        {
            var embedded = BuiltStripProfile();
            if (embedded == null) return;

            var bar = new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3" };
            var mine = LedBarProfile.For(bar, embedded);

            Assert.Equal("Rim", FlagBoxProfile.ProfileNameOf(mine));
            Assert.Contains("\"ProfileId\": \"" + LedBarProfile.IdFor("LedRim").ToString("D") + "\"", mine);
            // Every name a bar owns is moved wherever the profile reads it. The four a strip has always
            // read are all read by the 3-9-3 profile this rewrites, though not by every profile: a bare
            // run has no ends and no LedSpotterWhole. The brightness and the switches (#503) are read
            // where the generator draws them, and a profile built before them reads none, which is no
            // name left rig-wide either.
            var always = new[] { Contract.LedCentre, Contract.LedRpmStyle, Contract.LedFlagAnimation, Contract.LedSpotterWhole };
            foreach (var setting in LedBarProfile.BarSettings)
            {
                Assert.DoesNotContain("[OpenDash." + setting + "]", mine);
                if (always.Contains(setting) || embedded.Contains("[OpenDash." + setting + "]"))
                {
                    Assert.Contains("[OpenDash.LedRim" + setting + "]", mine);
                }
            }
            // What stays the rig's stays the rig's: the rig's fallback brightness and night mode, the
            // low-fuel threshold and the car's own shift pattern are not a strip's business.
            foreach (var shared in new[] { "LightsBrightness", "LightsNightMode", "LightsLowFuelLaps", "LedMirrorReady" })
            {
                if (!embedded.Contains("[OpenDash." + shared + "]")) continue;
                Assert.Contains("[OpenDash." + shared + "]", mine);
            }
            // And the file is otherwise what the build wrote: undoing the bar's own names and dropping the
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

        [Fact]
        public void A_bar_owns_its_brightness_and_a_switch_per_effect()
        {
            // The four it always had, then its own brightness and the fifteen switches, in the order the
            // contract declares them. #503.
            Assert.Equal(20, LedBarProfile.BarSettings.Length);
            Assert.Equal(
                new[] { Contract.LedCentre, Contract.LedRpmStyle, Contract.LedFlagAnimation, Contract.LedSpotterWhole, Contract.LedBrightness }
                    .Concat(Contract.LedEffectSettings()),
                LedBarProfile.BarSettings);
            // Every one of them is the rig's too, so a strip nobody added reads the rig's answer.
            foreach (var setting in LedBarProfile.BarSettings) Assert.Contains(setting, Contract.LedPropertyNames());
            Assert.Equal(LedBarProfile.BarSettings.Select(s => "LedRim" + s), LedBarProfile.Properties("LedRim"));
            // And the rig's stays the rig's: night mode and the fallback brightness are not a bar's.
            Assert.DoesNotContain(Contract.LightsBrightness, LedBarProfile.BarSettings);
            Assert.DoesNotContain(Contract.LightsNightMode, LedBarProfile.BarSettings);

            var settings = new OpenDashSettings();
            settings.Normalise();
            var bar = settings.AddLedBar("3-9-3", "Rim", LedBar.ArduinoDevice);
            var names = settings.DeclaredProperties().ToList();
            foreach (var setting in LedBarProfile.BarSettings) Assert.Contains("LedRim" + setting, names);
            Assert.Equal(names.Count, names.Distinct().Count());
        }

        [Fact]
        public void A_bar_follows_the_rigs_brightness_until_it_has_its_own()
        {
            var settings = new OpenDashSettings { LightsBrightness = 70 };
            settings.Normalise();
            var bar = settings.AddLedBar("3-9-3", "Rim", LedBar.ArduinoDevice);
            Assert.Null(bar.Brightness);
            Assert.Null(settings.BarBrightness(bar.Namespace));
            settings.SetBarBrightness(bar.Namespace, 40);
            Assert.Equal(40, settings.BarBrightness(bar.Namespace));
            settings.SetBarBrightness(bar.Namespace, 180);
            Assert.Equal(100, settings.BarBrightness(bar.Namespace));
            settings.SetBarBrightness(bar.Namespace, null);
            Assert.Null(settings.BarBrightness(bar.Namespace));
            // A hand-edited value is clamped, and a bar that has gone reads the rig's.
            bar.Brightness = -10;
            settings.Normalise();
            Assert.Equal(0, settings.BarBrightness(bar.Namespace));
            Assert.Null(settings.BarBrightness("Gone"));
            settings.SetBarBrightness("Gone", 50);
        }

        [Fact]
        public void Every_effect_is_on_until_it_is_switched_off_and_a_flag_is_every_flag()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            var bar = settings.AddLedBar("3-9-3", "Rim", LedBar.ArduinoDevice);
            Assert.Empty(bar.EffectsOff);
            foreach (var id in Contract.LedEffectIds()) Assert.True(settings.BarEffectEnabled(bar.Namespace, id));

            settings.SetBarEffect(bar.Namespace, "tc", false);
            settings.SetBarEffect(bar.Namespace, "flag.yellow", false);
            Assert.False(settings.BarEffectEnabled(bar.Namespace, "tc"));
            Assert.True(settings.BarEffectEnabled(bar.Namespace, "abs"));
            // One switch for the flags: turning off the yellow turned off the chequer with it.
            Assert.False(settings.BarEffectEnabled(bar.Namespace, "flag.chequered"));
            Assert.Equal(new[] { "tc", "flag.black" }, bar.EffectsOff);
            settings.SetBarEffect(bar.Namespace, "flag.blue", true);
            Assert.True(settings.BarEffectEnabled(bar.Namespace, "flag.yellow"));
            Assert.Equal(new[] { "tc" }, bar.EffectsOff);

            // An id no switch answers is dropped from a file, and a repeat is stored once.
            bar.EffectsOff = new List<string> { "sparkles", "abs", "abs", "flag.white", "flag.green", null };
            settings.Normalise();
            Assert.Equal(new[] { "abs", "flag.black" }, settings.LedBarByNamespace(bar.Namespace).EffectsOff);
            // And a bar that has gone draws everything, which is what a strip nobody configured draws.
            Assert.True(settings.BarEffectEnabled("Gone", "tc"));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetBarEffect(bar.Namespace, "sparkles", false));

            // The copy carries them and does not share the list.
            var copy = new OpenDashSettings();
            copy.CopyFrom(settings);
            Assert.False(copy.BarEffectEnabled(bar.Namespace, "abs"));
            copy.SetBarEffect(bar.Namespace, "abs", true);
            Assert.False(settings.BarEffectEnabled(bar.Namespace, "abs"));
        }

        [Fact]
        public void An_effect_switch_costs_nothing_to_read_with_effects_off()
        {
            // Every gated effect in a strip profile reads its switch first, so a profile asks all fifteen
            // on every LED frame: with a switch off that must still be compares and nothing else. #503.
            var settings = new OpenDashSettings();
            settings.Normalise();
            var bar = settings.AddLedBar("3-9-3", "Rim", LedBar.ArduinoDevice);
            settings.SetBarEffect(bar.Namespace, "tc", false);
            settings.SetBarEffect(bar.Namespace, "flag.yellow", false);
            settings.SetBarEffect(bar.Namespace, "pit.lane", false);
            var ns = bar.Namespace;
            var effects = Contract.LedEffectSettings().Select(Contract.LedEffectPrimaryId).ToArray();
            foreach (var effect in effects) settings.BarEffectEnabled(ns, effect);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var frame = 0; frame < 100; frame++)
            {
                for (var i = 0; i < effects.Length; i++) settings.BarEffectEnabled(ns, effects[i]);
            }
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.False(settings.BarEffectEnabled(ns, "tc"));
            Assert.False(settings.BarEffectEnabled(ns, "flag.chequered"));
            Assert.True(settings.BarEffectEnabled(ns, "abs"));
        }

        [Fact]
        public void A_reversed_shape_is_the_plain_one_wired_from_the_far_end()
        {
            // The 4/14/4 that shipped as a shape of its own loads as its sibling with the switch on, and
            // still installs the same profile.
            var settings = new OpenDashSettings
            {
                LedBars = new List<LedBar> { new LedBar { Name = "MLD", Namespace = "LedMLD", Shape = "4-14-4-reversed" } },
            };
            settings.Normalise();
            var bar = settings.LedBarByNamespace("LedMLD");
            Assert.Equal("4-14-4", bar.Shape);
            Assert.True(bar.Reversed);
            Assert.True(settings.BarReversed("LedMLD"));
            Assert.Equal("4-14-4-reversed", bar.ProfileShapeId);
            // The namespace is frozen, so the profile's id and every bound name are where they were.
            Assert.Equal("LedMLD", bar.Namespace);

            // Round trip: off is the plain profile, on is the twin, and a change is reported so the
            // caller reinstalls.
            Assert.True(settings.SetBarReversed("LedMLD", false));
            Assert.Equal("4-14-4", bar.ProfileShapeId);
            Assert.False(settings.SetBarReversed("LedMLD", false));
            Assert.True(settings.SetBarReversed("LedMLD", true));
            Assert.Equal("4-14-4-reversed", bar.ProfileShapeId);
            Assert.False(settings.SetBarReversed("Gone", true));

            // Any plain shape has a twin, a brow included.
            var brow = settings.AddLedBar("brow-15", "Brow", LedBar.ArduinoDevice);
            Assert.False(brow.Reversed);
            Assert.True(settings.SetBarReversed(brow.Namespace, true));
            Assert.Equal("brow-15-reversed", brow.ProfileShapeId);

            // Added as a reversed id, a bar arrives reversed.
            var added = settings.AddLedBar("4-14-4-reversed", "Second", LedBar.ArduinoDevice);
            Assert.Equal("4-14-4", added.Shape);
            Assert.True(added.Reversed);

            // The copy carries the switch.
            var copy = bar.Copy();
            Assert.True(copy.Reversed);
            Assert.Equal(bar.ProfileShapeId, copy.ProfileShapeId);
        }

        [Fact]
        public void A_shape_with_a_wiring_of_its_own_cannot_be_reversed()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            var wheel = settings.AddLedBar("3-9-3-fanatec", "Wheel", LedBar.ArduinoDevice);
            Assert.False(wheel.SupportsReversal);
            Assert.False(settings.SetBarReversed(wheel.Namespace, true));
            Assert.False(wheel.Reversed);
            Assert.Equal("3-9-3-fanatec", wheel.ProfileShapeId);
            // And a file claiming otherwise is repaired.
            wheel.Reversed = true;
            settings.Normalise();
            Assert.False(settings.LedBarByNamespace(wheel.Namespace).Reversed);
            Assert.False(settings.BarReversed(wheel.Namespace));
        }

        [Fact]
        public void A_bars_retired_rev_look_loads_as_left_to_right()
        {
            var settings = new OpenDashSettings
            {
                LedBars = new List<LedBar> { new LedBar { Name = "Rim", Namespace = "LedRim", Shape = "3-9-3", RpmStyle = "f1" } },
            };
            settings.Normalise();
            Assert.Equal("leftToRight", settings.LedBarByNamespace("LedRim").RpmStyle);
            Assert.Equal("leftToRight", settings.BarRpmStyle("LedRim"));
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

        /// <summary>
        /// A profile installed onto a device that is listing its maker's built-in profiles carries the
        /// sentence saying so.
        /// </summary>
        /// <remarks>
        /// The state behind the "my new profile does not appear in SimHub" report. SimHub's dropdown is
        /// bound to `AvailableProfiles`, which is `BuiltInProfiles` while that switch is on, so a correct
        /// install leaves the driver with nothing to select and no reason given. The note is what gives
        /// the reason; `FlagBoxInstaller` puts it on every plan of an install made in that state.
        /// </remarks>
        [Fact]
        public void Built_in_mode_is_a_note_rather_than_a_failure()
        {
            Assert.True(FlagBoxInstallPlan.BuiltInModeOf(true, true));
            // Either half off and the device lists the saved profiles, which is where ours goes.
            Assert.False(FlagBoxInstallPlan.BuiltInModeOf(true, false));
            Assert.False(FlagBoxInstallPlan.BuiltInModeOf(false, true));
            Assert.False(FlagBoxInstallPlan.BuiltInModeOf(false, false));
            // It names the switch rather than describing the symptom, because the switch is the fix.
            Assert.Contains("built-in profiles", FlagBoxInstallPlan.BuiltInModeNote, StringComparison.Ordinal);
            // A plan carrying it is still a plan that worked: nothing to repeat, nothing to undo.
            var plan = new FlagBoxPlan { State = FlagBoxInstallState.UpToDate, Note = FlagBoxInstallPlan.BuiltInModeNote };
            Assert.Equal(FlagBoxInstallState.UpToDate, plan.State);
            Assert.False(plan.WouldChange);
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

        /// <summary>
        /// The name box opens on the product's own name for the shape, which is right in SimHub's
        /// profile list and wrong in a property name.
        /// </summary>
        /// <remarks>
        /// `OpenDash 0/9/0` slugs to `LedOpenDash090`, which is what a driver would have to find in
        /// SimHub's property list to bind anything to their own strip. The prefix comes off first.
        /// </remarks>
        [Fact]
        public void A_bar_named_after_the_product_does_not_carry_the_product_into_its_properties()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            var bar = settings.AddLedBar("0-9-0", "OpenDash 0/9/0", LedBar.ArduinoDevice);
            Assert.Equal("OpenDash 0/9/0", bar.Name);
            Assert.Equal("Led090", bar.Namespace);

            // And a name of the driver's own is simply slugged.
            var rim = settings.AddLedBar("3-9-3", "Rim", LedBar.ArduinoDevice);
            Assert.Equal("LedRim", rim.Namespace);
        }
        /// <summary>
        /// A bar remembers which of SimHub's LED devices its profile went to.
        /// </summary>
        /// <remarks>
        /// SimHub keeps one profile list per LED device, in that device's own file, and a profile in one
        /// is invisible in every other. OpenDash installed into the Arduino RGB LEDs device whatever the
        /// strip was, so a 3-9-3 bar added for a wheel was written, saved and verified correctly into a
        /// list the wheel does not read -- reported from a rig as the profile simply not being there.
        /// These pin the vocabulary the settings file holds; LedTargets resolves it against SimHub.
        /// </remarks>
        [Fact]
        public void A_bar_records_the_device_its_profile_went_to()
        {
            var settings = new OpenDashSettings();
            settings.Normalise();
            var wheel = Guid.Parse("0f8b4c1e-6d2a-4f3b-9c17-2a5e8d4b7c60");

            var bar = settings.AddLedBar("3-9-3", "Wheel", LedBar.DeviceId(wheel));
            Assert.Equal("device:0f8b4c1e-6d2a-4f3b-9c17-2a5e8d4b7c60", bar.Device);
            Assert.Equal(bar.Device, settings.BarDevice(bar.Namespace));
            Assert.Equal(wheel, LedBar.DeviceInstanceOf(bar.Device));
            Assert.Equal(bar.Device, bar.Copy().Device);
        }

        /// <summary>A bar written before OpenDash knew there was more than one device is read as the
        /// Arduino's, because that is where those bars were actually installed. It is a statement about
        /// the past, not a preference.</summary>
        [Fact]
        public void A_bar_with_no_device_is_the_arduinos()
        {
            Assert.Equal(LedBar.ArduinoDevice, LedBar.NormaliseDevice(null));
            Assert.Equal(LedBar.ArduinoDevice, LedBar.NormaliseDevice("   "));
            Assert.Equal(LedBar.ArduinoDevice, LedBar.NormaliseDevice(LedBar.ArduinoDevice));

            var bar = new LedBar { Shape = "3-9-3" };
            bar.Normalise();
            Assert.Equal(LedBar.ArduinoDevice, bar.Device);
        }

        /// <summary>An id OpenDash cannot read is not kept. A bar pointed at nothing would install
        /// nowhere and say it had, which is the shape of the bug this field exists to close.</summary>
        [Fact]
        public void An_unreadable_device_id_falls_back_rather_than_being_kept()
        {
            Assert.Equal(LedBar.ArduinoDevice, LedBar.NormaliseDevice("device:not-a-guid"));
            Assert.Equal(LedBar.ArduinoDevice, LedBar.NormaliseDevice("wheel"));
            Assert.Null(LedBar.DeviceInstanceOf("wheel"));
            Assert.Null(LedBar.DeviceInstanceOf(null));
            // Round trips, and the spelling is the one a settings file already holds.
            var id = LedBar.DeviceId(Guid.Empty);
            Assert.Equal("device:00000000-0000-0000-0000-000000000000", id);
            Assert.Equal(id, LedBar.NormaliseDevice(" " + id + " "));
        }

    }
}
