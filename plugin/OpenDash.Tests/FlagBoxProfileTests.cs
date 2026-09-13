// FlagBoxProfileTests: the profile is written where the user can find it and nowhere else.
//
// The point of most of these is what the plugin does *not* do. ADR 0013 decided the flag box is the
// one artefact the plugin extracts rather than installs, so the tests that matter are the ones that
// would fail if somebody made it behave like the packages.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class FlagBoxProfileTests
    {
        private static Assembly Self => typeof(FlagBoxProfileTests).Assembly;

        [Fact]
        public void FindsTheEmbeddedProfile()
        {
            var names = FlagBoxProfile.ResourceNames(Self);
            Assert.Single(names);
            Assert.EndsWith("openDash Flag box.ledsprofile", names[0], StringComparison.Ordinal);
        }

        [Fact]
        public void KeepsTheSpacesOfTheFileName()
        {
            // MSBuild turns the folder part into an identifier and leaves the file name alone, which is
            // what lets the shipped name carry a space.
            Assert.Equal("openDash Flag box.ledsprofile", FlagBoxProfile.FileNameOf("OpenDashPlugin.Tests.Resources.openDash Flag box.ledsprofile"));
            Assert.Equal("x.ledsprofile", FlagBoxProfile.FileNameOf("x.ledsprofile"));
            Assert.Null(FlagBoxProfile.FileNameOf(null));
        }

        [Fact]
        public void ReadsTheProfileName()
        {
            Assert.Equal("openDash Flag box", FlagBoxProfile.ProfileNameOf("{\n  \"Name\": \"openDash Flag box\"\n}"));
            Assert.Null(FlagBoxProfile.ProfileNameOf("{}"));
            Assert.Null(FlagBoxProfile.ProfileNameOf(null));
        }

        [Fact]
        public void WritesTheProfileIntoItsOwnFolder()
        {
            using (var root = new TempDir())
            {
                var result = FlagBoxProfile.Extract(root.Path, Self);
                Assert.Equal(FlagBoxStatus.Extracted, result.Status);
                Assert.Equal(Path.Combine(root.Path, "OpenDash", "openDash Flag box.ledsprofile"), result.Path);
                Assert.True(File.Exists(result.Path));
                Assert.Equal("openDash Flag box", result.ProfileName);
            }
        }

        [Fact]
        public void NeverTouchesSimHubsOwnMatrixSettings()
        {
            // The whole of ADR 0013's install decision. SimHub's RGBMatrixDriver rewrites this file
            // whenever anything changes, so a merge into it loses the user's other profiles.
            using (var root = new TempDir())
            {
                var settings = Path.Combine(root.Path, "PluginsData", "Common", "ArduinoRGBMatrixSettings.json");
                Directory.CreateDirectory(Path.GetDirectoryName(settings));
                File.WriteAllText(settings, "{\"Profiles\":[{\"Name\":\"the user's own\"}]}");

                FlagBoxProfile.Extract(root.Path, Self);

                Assert.Equal("{\"Profiles\":[{\"Name\":\"the user's own\"}]}", File.ReadAllText(settings));
            }
        }

        [Fact]
        public void WritesNothingOutsideItsOwnFolder()
        {
            using (var root = new TempDir())
            {
                FlagBoxProfile.Extract(root.Path, Self);
                Assert.Equal(new[] { "OpenDash" }, Directory.GetDirectories(root.Path).Select(Path.GetFileName).ToArray());
                Assert.Empty(Directory.GetFiles(root.Path));
            }
        }

        [Fact]
        public void SaysUpToDateWithoutRewritingAnUnchangedFile()
        {
            using (var root = new TempDir())
            {
                var first = FlagBoxProfile.Extract(root.Path, Self);
                var written = File.GetLastWriteTimeUtc(first.Path);

                var second = FlagBoxProfile.Extract(root.Path, Self);

                Assert.Equal(FlagBoxStatus.UpToDate, second.Status);
                Assert.Equal(written, File.GetLastWriteTimeUtc(second.Path));
            }
        }

        [Fact]
        public void RefreshesAProfileThatHasChanged()
        {
            using (var root = new TempDir())
            {
                var first = FlagBoxProfile.Extract(root.Path, Self);
                File.WriteAllText(first.Path, "{\"Name\":\"an older build\"}");

                var second = FlagBoxProfile.Extract(root.Path, Self);

                Assert.Equal(FlagBoxStatus.Extracted, second.Status);
                Assert.Contains("openDash Flag box", File.ReadAllText(second.Path), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void SaysSoWhenNothingIsEmbedded()
        {
            using (var root = new TempDir())
            {
                // A build with no profile still loads and reports it, the way one with no package does.
                var result = FlagBoxProfile.Extract(root.Path, typeof(string).Assembly);
                Assert.Equal(FlagBoxStatus.NotEmbedded, result.Status);
                Assert.False(Directory.Exists(FlagBoxProfile.FolderPath(root.Path)));
            }
        }

        [Fact]
        public void TheSummaryCarriesTheManualStep()
        {
            // A user who is not told to import it will wait for something that is never going to happen.
            using (var root = new TempDir())
            {
                var summary = FlagBoxProfile.Summary(FlagBoxProfile.Extract(root.Path, Self));
                Assert.Contains("Import it", summary, StringComparison.Ordinal);
                Assert.Contains("does not install it", summary, StringComparison.Ordinal);
                Assert.Contains("openDash Flag box.ledsprofile", summary, StringComparison.Ordinal);
            }
        }

        private sealed class TempDir : IDisposable
        {
            public TempDir()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "opendash-flagbox-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                try { Directory.Delete(Path, true); } catch (IOException) { }
            }
        }
    }
}
