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

        [Fact]
        public void The_rewrite_moves_the_bars_own_names_two_fields_and_nothing_else()
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
