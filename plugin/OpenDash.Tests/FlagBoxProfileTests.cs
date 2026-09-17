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
        public void FindsTheEmbeddedProfileAmongTheOtherLightingProfiles()
        {
            // The pin. A release embeds nineteen RPM strips beside the one matrix profile and SimHub
            // gives both families the same extension, so ResourceNames is a census of two different
            // kinds of file and its first entry means nothing. This assembly is arranged the same way:
            // "openDash 0-10-0.ledsprofile" sorts ahead of the flag box ('0' is 0x30, 'F' is 0x46).
            var names = FlagBoxProfile.ResourceNames(Self);
            Assert.Equal(2, names.Count);
            Assert.EndsWith("openDash 0-10-0.ledsprofile", names[0], StringComparison.Ordinal);

            // Taking names[0] would take the strip. Taking it by name takes the flag box.
            var resource = FlagBoxProfile.ResourceName(Self);
            Assert.EndsWith(FlagBoxProfile.FileName, resource, StringComparison.Ordinal);
            Assert.NotEqual(names[0], resource);
        }

        [Fact]
        public void AnRpmStripIsNeverMistakenForTheFlagBox()
        {
            // What the ordinal-first bug actually did: the plugin would deserialise a ten-LED strip
            // as an RGBMatrixProfile and push it into SimHub's matrix profile collection. The name
            // written out and the Name inside it both have to be the matrix profile's.
            using (var root = new TempDir())
            {
                var result = FlagBoxProfile.Extract(root.Path, Self);

                Assert.Equal(FlagBoxStatus.Extracted, result.Status);
                Assert.Equal(FlagBoxProfile.FileName, Path.GetFileName(result.Path));
                Assert.Equal(FlagBoxProfile.ProfileName, result.ProfileName);
                Assert.Equal(FlagBoxProfile.ProfileName, FlagBoxProfile.ProfileNameOf(result.Json));
                // And nothing else came out with it: the strips are embedded to be offered, not extracted.
                Assert.Equal(new[] { FlagBoxProfile.FileName }, Directory.GetFiles(FlagBoxProfile.FolderPath(root.Path)).Select(Path.GetFileName).ToArray());
            }
        }

        [Fact]
        public void TheNameIsTheDiscriminatorAndTheExtensionIsNot()
        {
            // FileName is the whole of the identity, and it is the same string on both sides of the
            // build: FLAG_BOX_PROFILE_NAME in packages/dash/src/leds/profile.ts writes the file, this
            // reads it back. An assembly with profiles but not that one has no flag box, which is a
            // different answer from an assembly with no profiles at all and a much better one than
            // whichever profile happened to sort first.
            Assert.Equal("openDash Flag box.ledsprofile", FlagBoxProfile.FileName);
            Assert.Equal(FlagBoxProfile.ProfileName + FlagBoxProfile.ProfileExtension, FlagBoxProfile.FileName);
            Assert.Null(FlagBoxProfile.ResourceName(typeof(string).Assembly));
        }

        [Fact]
        public void TheSelectionIsBySpellingAndNotByOrder()
        {
            const string prefix = "OpenDashPlugin.Resources.";
            // Every shape of list a release can hand it, with the flag box never first.
            var release = new[]
            {
                prefix + "openDash 0-10-0.ledsprofile",
                prefix + "openDash 4-14-4-reversed.ledsprofile",
                prefix + "openDash Flag box.ledsprofile",
                prefix + "openDash brow-25.ledsprofile",
            };
            Assert.Equal(prefix + "openDash Flag box.ledsprofile", FlagBoxProfile.SelectResource(release, FlagBoxProfile.FileName));
            Assert.Equal(prefix + "openDash Flag box.ledsprofile", FlagBoxProfile.SelectResource(Enumerable.Reverse(release), FlagBoxProfile.FileName));

            // Strips only: no flag box, rather than the nearest thing to one.
            Assert.Null(FlagBoxProfile.SelectResource(new[] { prefix + "openDash 0-10-0.ledsprofile", prefix + "openDash brow-9.ledsprofile" }, FlagBoxProfile.FileName));
            Assert.Null(FlagBoxProfile.SelectResource(new string[0], FlagBoxProfile.FileName));

            // A case-only variant is taken when it is all there is, because copying the built file
            // over an older "openDash flag box.ledsprofile" on Windows replaces the bytes and keeps
            // the old casing; it loses to the exact spelling whenever both are present, whichever
            // way round the list holds them.
            var lower = prefix + "openDash flag box.ledsprofile";
            Assert.Equal(lower, FlagBoxProfile.SelectResource(new[] { lower }, FlagBoxProfile.FileName));
            Assert.Equal(prefix + "openDash Flag box.ledsprofile", FlagBoxProfile.SelectResource(new[] { lower, prefix + "openDash Flag box.ledsprofile" }, FlagBoxProfile.FileName));
        }

        [Fact]
        public void AShapeSelectsItsOwnResourceAndNeverTheMatrixOne()
        {
            // The inverse of the pin above, and the half that was missing while only the flag box was
            // ever installed. Now that a strip is handed to SimHub's LED driver the hazard runs both
            // ways: a shape must never collect the 8x8 matrix profile, which would paint sixty-four
            // pixels onto a ten-LED strip, and it must never collect a neighbouring shape's either.
            const string prefix = "OpenDashPlugin.Resources.";
            var release = new[]
            {
                prefix + "openDash 0-10-0.ledsprofile",
                prefix + "openDash 4-14-4-reversed.ledsprofile",
                prefix + "openDash 4-14-4.ledsprofile",
                prefix + "openDash Flag box.ledsprofile",
                prefix + "openDash brow-25.ledsprofile",
            };

            foreach (var id in new[] { "0-10-0", "4-14-4", "4-14-4-reversed", "brow-25" })
            {
                var chosen = FlagBoxProfile.SelectResource(release, FlagBoxProfile.StripFileName(id));
                Assert.Equal(prefix + "openDash " + id + FlagBoxProfile.ProfileExtension, chosen);
                Assert.NotEqual(prefix + FlagBoxProfile.FileName, chosen);
            }

            // "4-14-4" is a prefix of "4-14-4-reversed": a match that was not the whole file name would
            // hand the reversed wiring to a strip wired the usual way, which lights the wrong end.
            Assert.Equal(
                prefix + "openDash 4-14-4.ledsprofile",
                FlagBoxProfile.SelectResource(new[] { prefix + "openDash 4-14-4-reversed.ledsprofile", prefix + "openDash 4-14-4.ledsprofile" }, FlagBoxProfile.StripFileName("4-14-4")));

            // A shape this build did not emit is absent, not the nearest thing to it.
            Assert.Null(FlagBoxProfile.SelectResource(release, FlagBoxProfile.StripFileName("7-7-7")));
            Assert.Null(FlagBoxProfile.StripFileName(null));
        }

        [Fact]
        public void TheStripsAreEveryEmbeddedProfileExceptTheFlagBox()
        {
            // How the panel knows which shapes this build carries: the embedded files themselves, not a
            // copy of STRIP_SHAPES and BROW_SHAPES transcribed into C#. A shape added in
            // packages/dash/src/leds/strip.ts therefore arrives with its row and no edit here.
            var strips = FlagBoxProfile.StripResourceNames(Self);
            Assert.Equal(new[] { "0-10-0" }, strips.Select(FlagBoxProfile.ShapeIdOf).ToArray());
            Assert.DoesNotContain(FlagBoxProfile.ResourceName(Self), strips);

            // The flag box is not a shape, and neither is a file that is not ours at all.
            Assert.Null(FlagBoxProfile.ShapeIdOf("OpenDashPlugin.Resources." + FlagBoxProfile.FileName));
            Assert.Null(FlagBoxProfile.ShapeIdOf("OpenDashPlugin.Resources.openDash flag box.ledsprofile"));
            Assert.Null(FlagBoxProfile.ShapeIdOf("OpenDashPlugin.Resources.somebody else.ledsprofile"));
            Assert.Null(FlagBoxProfile.ShapeIdOf("OpenDashPlugin.Resources.openDash .ledsprofile"));
            Assert.Null(FlagBoxProfile.ShapeIdOf(null));
            Assert.Equal("brow-9", FlagBoxProfile.ShapeIdOf("OpenDashPlugin.Resources.openDash brow-9.ledsprofile"));

            // And the round trip, which is the contract with rpmStripFileName() on the dash side.
            Assert.Equal("4-14-4", FlagBoxProfile.ShapeIdOf("x.Resources." + FlagBoxProfile.StripFileName("4-14-4")));
        }

        [Fact]
        public void AShapesProfileIsReadWithoutBeingWrittenToDisk()
        {
            // Installing hands SimHub the embedded text directly; only the flag box is also left on
            // disk, because only it has a by-hand import path to fall back to.
            var strip = FlagBoxProfile.StripResourceNames(Self).Single();
            var json = FlagBoxProfile.ResourceText(Self, strip);
            Assert.Equal("openDash 0/10/0", FlagBoxProfile.ProfileNameOf(json));
            Assert.Null(FlagBoxProfile.ResourceText(Self, "OpenDashPlugin.Resources.not embedded.ledsprofile"));
            Assert.Null(FlagBoxProfile.ResourceText(Self, null));
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
        public void ReadsTheProfilesOwnFieldsAndNotAContainersOwn()
        {
            // The bug this exists for: every container in the tree has a Description of its own, and
            // they appear BEFORE the profile's in the file, so a first-match search returns a
            // container's and the version marker silently disappears.
            const string json = @"{
  ""LedContainers"": [
    { ""Description"": ""Brightness"", ""LedContainers"": [ { ""Description"": ""Racing"", ""Name"": ""nope"" } ] }
  ],
  ""Name"": ""openDash Flag box"",
  ""Author"": ""openDash"",
  ""Description"": ""Built by openDash 9.9.9; do not edit here.""
}";
            Assert.Equal("openDash Flag box", FlagBoxProfile.ProfileNameOf(json));
            Assert.Equal("openDash", FlagBoxProfile.AuthorOf(json));
            Assert.Equal("Built by openDash 9.9.9; do not edit here.", FlagBoxProfile.DescriptionOf(json));
        }

        [Fact]
        public void ReadingAFieldSurvivesBracesAndQuotesInsideStrings()
        {
            // An NCalc expression or a description could carry a brace; counting depth without
            // tracking strings would then lose the top level entirely.
            const string json = @"{
  ""LedContainers"": [ { ""TriggerFormula"": { ""Expression"": ""isnull([X], 1) = {weird}"" } } ],
  ""Description"": ""Built by openDash 1.2.3; a \""quoted\"" word and a } brace.""
}";
            Assert.Equal(@"Built by openDash 1.2.3; a ""quoted"" word and a } brace.", FlagBoxProfile.DescriptionOf(json));
        }

        [Fact]
        public void AMissingOrMalformedFieldReadsAsNullRatherThanThrowing()
        {
            Assert.Null(FlagBoxProfile.DescriptionOf("{}"));
            Assert.Null(FlagBoxProfile.DescriptionOf(null));
            Assert.Null(FlagBoxProfile.DescriptionOf(""));
            Assert.Null(FlagBoxProfile.DescriptionOf(@"{ ""Description"": 42 }"));
            Assert.Null(FlagBoxProfile.DescriptionOf(@"{ ""Description"": "));
            Assert.Null(FlagBoxProfile.AuthorOf(@"{ ""LedContainers"": [ { ""Author"": ""someone else"" } ] }"));
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
        public void TheImportFolderIsWhereSimHubsOwnDialogOpens()
        {
            // ProfilesManager.importProfile_Click sets InitialDirectory to
            // Path.Combine(GetFolderPath(SpecialFolder.Personal), "SimHub"), so a copy put there is
            // already in front of the user when the dialog opens.
            Assert.Equal(Path.Combine("D:\\docs", "SimHub"), FlagBoxProfile.ImportFolder("D:\\docs"));
            // A host with no Documents folder -- this test runs on Linux, where SpecialFolder.Personal
            // is empty -- gets null rather than a path rooted at nowhere. CopyForImport reports that
            // instead of throwing, which is the only reason this branch is reachable at all.
            Assert.Null(FlagBoxProfile.ImportFolder(""));
            var real = FlagBoxProfile.ImportFolder();
            if (real != null) Assert.EndsWith("SimHub", real, StringComparison.Ordinal);
        }

        [Fact]
        public void CopyingForImportWritesTheProfileWhereTheDialogOpens()
        {
            using (var root = new TempDir())
            using (var docs = new TempDir())
            {
                var extracted = FlagBoxProfile.Extract(root.Path, Self);
                var copied = FlagBoxProfile.CopyForImport(extracted, docs.Path);

                Assert.Equal(FlagBoxStatus.Extracted, copied.Status);
                Assert.Equal(Path.Combine(docs.Path, "SimHub", "openDash Flag box.ledsprofile"), copied.Path);
                Assert.Equal(File.ReadAllText(extracted.Path), File.ReadAllText(copied.Path));
            }
        }

        [Fact]
        public void NothingIsWrittenToDocumentsUnlessAsked()
        {
            // Extract() is what runs at startup, for everybody. A stray file in the user's Documents
            // for a fallback most people never need would be a poor trade.
            using (var root = new TempDir())
            using (var docs = new TempDir())
            {
                FlagBoxProfile.Extract(root.Path, Self);
                Assert.Empty(Directory.GetFileSystemEntries(docs.Path));
            }
        }

        [Fact]
        public void CopyingForImportSaysSoWhenThereIsNothingToCopy()
        {
            using (var docs = new TempDir())
            {
                Assert.Equal(FlagBoxStatus.NotEmbedded, FlagBoxProfile.CopyForImport(null, docs.Path).Status);
                Assert.Equal(FlagBoxStatus.NotEmbedded, FlagBoxProfile.CopyForImport(new FlagBoxResult(), docs.Path).Status);
                // No Documents folder at all: reported, not thrown.
                using (var root = new TempDir())
                {
                    Assert.Equal(FlagBoxStatus.Failed, FlagBoxProfile.CopyForImport(FlagBoxProfile.Extract(root.Path, Self), "").Status);
                }
            }
        }

        [Fact]
        public void TheExtractedResultCarriesTheProfileSoThePanelNeedNotReadItBack()
        {
            using (var root = new TempDir())
            {
                var result = FlagBoxProfile.Extract(root.Path, Self);
                Assert.False(string.IsNullOrEmpty(result.Json));
                Assert.Equal(File.ReadAllText(result.Path), result.Json);
                // And again when it is already current, or the panel would lose the profile on restart.
                var second = FlagBoxProfile.Extract(root.Path, Self);
                Assert.Equal(FlagBoxStatus.UpToDate, second.Status);
                Assert.Equal(result.Json, second.Json);
            }
        }

        [Fact]
        public void TheSummaryPointsAtTheButtonThatInstallsIt()
        {
            // A user who is not told where to go will wait for something that never happens.
            using (var root = new TempDir())
            {
                var summary = FlagBoxProfile.Summary(FlagBoxProfile.Extract(root.Path, Self));
                Assert.Contains("Lights", summary, StringComparison.Ordinal);
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
