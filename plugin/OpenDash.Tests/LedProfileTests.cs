// LedProfileTests.cs: the LED profiles the plugin carries, and the line ADR 0013 draws around them.
// Most of these prove what does NOT happen: openDash writes nothing to a device, and nothing anywhere
// until a person asks.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenDashPlugin;
using Xunit;

namespace OpenDashPlugin.Tests
{
    /// <summary>A source of profiles made of strings, so the rules can be tested without an assembly.</summary>
    internal sealed class FakeLedProfileSource : ILedProfileSource
    {
        private readonly Dictionary<string, string> bodies = new Dictionary<string, string>(StringComparer.Ordinal);

        public FakeLedProfileSource(params string[] fileNames)
        {
            var list = new List<LedProfile>();
            foreach (var name in fileNames)
            {
                var resource = "OpenDashPlugin.Resources." + name;
                bodies[resource] = "{ \"Name\": \"" + name + "\" }";
                list.Add(new LedProfile(resource, name, LedProfiles.FamilyOf(name)));
            }

            Profiles = list;
        }

        public IReadOnlyList<LedProfile> Profiles { get; }

        public Stream Open(LedProfile profile) => new MemoryStream(Encoding.UTF8.GetBytes(bodies[profile.ResourceName]));
    }

    public class LedProfileTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "opendash-leds-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private static FakeLedProfileSource Sample() => new FakeLedProfileSource(
            "openDash 4-14-4.ledsprofile",
            "openDash 3-9-3.ledsprofile",
            "openDash brow 25.ledsprofile",
            "openDash flag box.ledsprofile");

        [Fact]
        public void A_profile_is_sorted_into_a_family_by_its_name()
        {
            Assert.Equal(LedDeviceFamily.Strip, LedProfiles.FamilyOf("openDash 4-14-4.ledsprofile"));
            Assert.Equal(LedDeviceFamily.Brow, LedProfiles.FamilyOf("openDash brow 25.ledsprofile"));
            Assert.Equal(LedDeviceFamily.FlagBox, LedProfiles.FamilyOf("openDash flag box.ledsprofile"));
            // The user opts in per family rather than per profile: somebody with a wheel owns one strip,
            // and asking them about nineteen of them is asking nineteen times.
            Assert.Equal(
                new[] { LedDeviceFamily.Strip, LedDeviceFamily.Brow, LedDeviceFamily.FlagBox },
                LedProfiles.FamiliesIn(Sample()));
        }

        [Fact]
        public void A_resource_name_keeps_the_file_name_whole_including_its_dots_and_spaces()
        {
            // MSBuild turns only the folder part into an identifier, so "openDash 4-14-4.ledsprofile" survives
            // with its space and its dots. Splitting on the last dot would give "ledsprofile".
            Assert.Equal("openDash 4-14-4.ledsprofile", AssemblyLedProfileSource.FileNameOf("OpenDashPlugin.Resources.openDash 4-14-4.ledsprofile"));
            Assert.Equal("openDash flag box.ledsprofile", AssemblyLedProfileSource.FileNameOf("OpenDashPlugin.Resources.openDash flag box.ledsprofile"));
            // A name with no Resources. marker is passed through rather than mangled.
            Assert.Equal("loose.ledsprofile", AssemblyLedProfileSource.FileNameOf("loose.ledsprofile"));
        }

        [Fact]
        public void The_summary_says_what_is_carried_and_denies_having_installed_it()
        {
            var summary = LedProfiles.Summary(Sample());
            Assert.Contains("4 LED profiles", summary);
            Assert.Contains("Nothing is installed until you choose", summary);
            Assert.Equal("No LED profiles are bundled with this build.", LedProfiles.Summary(new FakeLedProfileSource()));
        }

        [Fact]
        public void Export_writes_only_the_family_asked_for_and_only_into_its_own_folder()
        {
            var result = LedProfiles.Export(Sample(), root, LedDeviceFamily.Strip);
            Assert.True(result.Ok);
            Assert.Equal(new[] { "openDash 4-14-4.ledsprofile", "openDash 3-9-3.ledsprofile" }, result.Written);

            var folder = Path.Combine(root, LedProfiles.FolderName);
            Assert.Equal(folder, result.Folder);
            Assert.Equal(2, Directory.GetFiles(folder).Length);
            // The brow and the flag box were not asked for and are not there.
            Assert.False(File.Exists(Path.Combine(folder, "openDash brow 25.ledsprofile")));
            // Nothing was written outside the folder it owns.
            Assert.Empty(Directory.GetFiles(root));
        }

        [Fact]
        public void Export_replaces_its_own_files_and_leaves_everything_else_alone()
        {
            var folder = Path.Combine(root, LedProfiles.FolderName);
            Directory.CreateDirectory(folder);
            // A file the user put there, and a stale copy of one of ours.
            File.WriteAllText(Path.Combine(folder, "my own profile.ledsprofile"), "mine");
            File.WriteAllText(Path.Combine(folder, "openDash 3-9-3.ledsprofile"), "stale");

            LedProfiles.Export(Sample(), root, LedDeviceFamily.Strip);

            Assert.Equal("mine", File.ReadAllText(Path.Combine(folder, "my own profile.ledsprofile")));
            Assert.Contains("openDash 3-9-3", File.ReadAllText(Path.Combine(folder, "openDash 3-9-3.ledsprofile")));
        }

        [Fact]
        public void An_unwritable_folder_is_reported_rather_than_thrown()
        {
            // A path that cannot be a directory, because a file of that name is in the way.
            Directory.CreateDirectory(root);
            var blocked = Path.Combine(root, "blocked");
            File.WriteAllText(blocked, "not a directory");

            var result = LedProfiles.Export(Sample(), blocked, LedDeviceFamily.Strip);
            Assert.False(result.Ok);
            Assert.NotNull(result.Error);
            Assert.Empty(result.Written);
        }

        [Fact]
        public void The_instructions_say_the_user_drives_the_import_and_name_the_right_device()
        {
            var strips = LedProfiles.ImportInstructions(LedProfiles.Export(Sample(), root, LedDeviceFamily.Strip), LedDeviceFamily.Strip);
            Assert.Contains("RGB LEDs device", strips);
            Assert.Contains("openDash does not write to your device itself", strips);

            // The flag box is a different SimHub device, because it is a different driver.
            var box = LedProfiles.ImportInstructions(LedProfiles.Export(Sample(), root, LedDeviceFamily.FlagBox), LedDeviceFamily.FlagBox);
            Assert.Contains("RGB Matrix (8x8) device", box);

            // A family with nothing in it says so rather than claiming a successful export of nothing.
            var none = LedProfiles.Export(new FakeLedProfileSource("openDash 4-14-4.ledsprofile"), root, LedDeviceFamily.FlagBox);
            Assert.Contains("no flag box profiles", LedProfiles.ImportInstructions(none, LedDeviceFamily.FlagBox));
        }

        [BuildOutputFact]
        public void The_real_build_output_carries_a_profile_per_shape_and_the_flag_box()
        {
            // Skipped unless build/ has been built, like its sibling in PackageExtractorTests.
            var dir = Path.Combine(RepoPaths.Root(), "build");
            var files = Directory.GetFiles(dir, "*" + LedProfiles.Extension).Select(Path.GetFileName).ToList();
            Assert.NotEmpty(files);

            // Every family the panel offers is actually represented, or a button writes nothing.
            var families = files.Select(LedProfiles.FamilyOf).Distinct().ToList();
            Assert.Contains(LedDeviceFamily.Strip, families);
            Assert.Contains(LedDeviceFamily.Brow, families);
            Assert.Contains(LedDeviceFamily.FlagBox, families);

            // And the file names survive MSBuild's resource naming, which is what the reader depends on:
            // a space and two dots in "openDash 4-14-4.ledsprofile" both have to come back out.
            foreach (var name in files)
            {
                Assert.Equal(name, AssemblyLedProfileSource.FileNameOf("OpenDashPlugin.Resources." + name));
            }
        }

        [Fact]
        public void Nothing_the_plugin_does_on_its_own_touches_the_rgb_drivers_settings_file()
        {
            // The rule ADR 0013 draws, asserted as the absence it is: the only public entry point that writes
            // anything is Export, it takes the family as an argument, and the folder it writes to is openDash's
            // own. The RGB driver's own settings file — PluginsData/Common/*LedsSettings*.json — is read by the
            // driver at startup and written back on change, so a profile added underneath a running SimHub is
            // overwritten the next time anything moves. openDash never goes near it.
            // There is no Install of any kind: the one thing that writes is Export, and it takes the family
            // the user picked as an argument rather than deciding for itself.
            var writers = typeof(LedProfiles).GetMethods()
                .Where(m => m.IsStatic && m.IsPublic && m.DeclaringType == typeof(LedProfiles))
                .Select(m => m.Name)
                .ToList();
            Assert.Contains("Export", writers);
            Assert.DoesNotContain(writers, name => name.IndexOf("Install", StringComparison.OrdinalIgnoreCase) >= 0);

            // ...and the folder it writes to is openDash's own, nowhere near the driver's settings.
            Assert.DoesNotContain("PluginsData", LedProfiles.FolderName, StringComparison.OrdinalIgnoreCase);
            var result = LedProfiles.Export(Sample(), root, LedDeviceFamily.Strip);
            Assert.DoesNotContain("PluginsData", result.Folder, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(LedProfiles.FolderName, result.Folder, StringComparison.Ordinal);
        }
    }
}
