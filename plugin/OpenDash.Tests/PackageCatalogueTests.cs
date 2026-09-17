// PackageCatalogueTests.cs: what the Install tab calls each package, what it captions it with, and the
// order it offers them in.
//
// The names are pinned verbatim for the reason PanelCopyTests pins the panel's sentences: they are the
// first thing a user reads on the tab. The folders are pinned twice over, because the table that carries
// the names is a fourth place knowing them -- PackageCatalogue.PrimaryFolder,
// DashboardInstaller.PrimaryFolder and the layouts' own `folder` fields are the others -- and a rename on
// one side only would leave a row that is silently unnamed rather than a build that fails.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PackageCatalogueTests
    {
        /// <summary>Whether this run is CI, where the dash artifact has already been downloaded into
        /// plugin/OpenDash/Resources/ and its absence is a broken contract rather than an unbuilt
        /// checkout. Read the same way FlagBoxInstallPlanTests reads it, and for the same reason.</summary>
        private static bool OnCI =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        /// <summary>
        /// Every package a release embeds, with the size its metadata declares.
        /// </summary>
        /// <remarks>
        /// Fourteen: the eight zone faces, the two rounds, and the landscape and portrait of each second
        /// screen. The card faces are built but excluded from the assembly (OpenDash.csproj), so they are
        /// not here either. The_packages_a_release_carries_are_the_fourteen_this_file_lists holds this
        /// table against what the dash actually built.
        /// </remarks>
        private static readonly (string Folder, int Width, int Height)[] Release =
        {
            ("openDash", 1920, 480),
            ("openDash 1280x480", 1280, 480),
            ("openDash 1280x400", 1280, 400),
            ("openDash 850x480", 850, 480),
            ("openDash 800x480", 800, 480),
            ("openDash 1280x720", 1280, 720),
            ("openDash 800x286", 800, 286),
            ("openDash 600x686", 600, 686),
            ("openDash 480 round", 480, 480),
            ("openDash 800 round", 800, 800),
            ("openDash Companion", 850, 480),
            ("openDash Companion portrait", 480, 850),
            ("openDash Pit wall", 1920, 1080),
            ("openDash Pit wall portrait", 1080, 1920),
        };

        private static PackageEntry Entry(string folder, int width, int height)
        {
            return new PackageEntry
            {
                Package = "OpenDashPlugin.Resources." + folder + ".simhubdash",
                Folder = folder,
                Width = width,
                Height = height,
                Kind = PackageCatalogue.Classify(folder, width, height),
            };
        }

        /// <summary>The six rows the design draws, in its order, word for word.</summary>
        [Fact]
        public void The_design_names_six_packages_and_captions_them()
        {
            Assert.Equal("Main DDU", Entry("openDash", 1920, 480).DisplayName);
            Assert.Equal("1920 × 480", Entry("openDash", 1920, 480).SizeCaption);

            Assert.Equal("Rim", Entry("openDash 850x480", 850, 480).DisplayName);
            Assert.Equal("850 × 480", Entry("openDash 850x480", 850, 480).SizeCaption);

            Assert.Equal("Pit wall", Entry("openDash Pit wall", 1920, 1080).DisplayName);
            Assert.Equal("1920 × 1080", Entry("openDash Pit wall", 1920, 1080).SizeCaption);

            Assert.Equal("Phone", Entry("openDash Companion", 850, 480).DisplayName);
            Assert.Equal("850 × 480", Entry("openDash Companion", 850, 480).SizeCaption);

            Assert.Equal("Nano", Entry("openDash 800x286", 800, 286).DisplayName);
            Assert.Equal("800 × 286", Entry("openDash 800x286", 800, 286).SizeCaption);

            // The one caption that is not a size. The package is 480 × 480 pixels and the screen it is
            // drawn for is round, which is the fact somebody shopping for a DDU recognises.
            Assert.Equal("Round", Entry("openDash 480 round", 480, 480).DisplayName);
            Assert.Equal("480 round", Entry("openDash 480 round", 480, 480).SizeCaption);
        }

        /// <summary>
        /// Every caption but the round's says exactly what the package's own metadata says.
        /// </summary>
        /// <remarks>
        /// The captions are written out rather than derived, so a package redrawn at another size would
        /// leave its row claiming the old one. This is the guard on that, and it is why the table above
        /// carries the sizes as well as the folders.
        /// </remarks>
        [Fact]
        public void A_caption_that_states_a_size_states_the_size_the_package_is()
        {
            foreach (var package in Release)
            {
                var entry = Entry(package.Folder, package.Width, package.Height);
                if (entry.SizeCaption == null || entry.SizeCaption == "480 round") continue;
                Assert.Equal(entry.SizeLabel, entry.SizeCaption);
            }
        }

        /// <summary>A package the design has not named keeps the folder, which is the word SimHub's own
        /// dashboard list shows, and keeps whatever size line its row already wrote.</summary>
        [Fact]
        public void A_package_the_design_does_not_name_keeps_its_folder()
        {
            var face = Entry("openDash 1280x480", 1280, 480);
            Assert.Equal("openDash 1280x480", face.DisplayName);
            Assert.Null(face.SizeCaption);

            var portrait = Entry("openDash Pit wall portrait", 1080, 1920);
            Assert.Equal("openDash Pit wall portrait", portrait.DisplayName);
            Assert.Null(portrait.SizeCaption);

            // The larger round is the near miss: the design names one round face and not the other.
            var round = Entry("openDash 800 round", 800, 800);
            Assert.Equal("openDash 800 round", round.DisplayName);
            Assert.Null(round.SizeCaption);

            // A package whose folder could not be read is still a row rather than a crash.
            Assert.Equal(string.Empty, new PackageEntry().DisplayName);
            Assert.Null(new PackageEntry().SizeCaption);
        }

        /// <summary>
        /// The design's six first, in its order, and everything it does not name behind them.
        /// </summary>
        /// <remarks>
        /// The design's order is neither by kind nor by size: it opens on the main dash and puts the pit
        /// wall third, between the two screens a driver looks at, where the kind order this replaces for
        /// the named half put it last. Behind them the old rule still holds, which is faces largest first,
        /// then the companions, then the pit walls, then the card model.
        /// </remarks>
        [Fact]
        public void The_named_packages_come_first_in_the_designs_order()
        {
            var source = new MemoryPackageSource();
            // Added back to front so that the order asserted below is the catalogue's own work and not
            // the order the packages happened to arrive in.
            foreach (var package in Release.Reverse())
            {
                source.Add("OpenDashPlugin.Resources." + package.Folder + ".simhubdash",
                    Package(package.Folder, package.Width, package.Height));
            }

            var catalogue = PackageCatalogue.From(source);

            Assert.Equal(
                new[]
                {
                    "Main DDU",
                    "Rim",
                    "Pit wall",
                    "Phone",
                    "Nano",
                    "Round",
                    "openDash 1280x720",
                    "openDash 1280x480",
                    "openDash 1280x400",
                    "openDash 600x686",
                    "openDash 800x480",
                    "openDash Companion portrait",
                    "openDash Pit wall portrait",
                    "openDash 800 round",
                },
                catalogue.Select(entry => entry.DisplayName).ToArray());
        }

        /// <summary>
        /// The rim face and the phone are 850 × 480 apiece and are two rows, not one.
        /// </summary>
        /// <remarks>
        /// The pair that decided the whole shape of the row: before the names, the two were told apart by
        /// their kind alone, and a row keyed on a size would have spoken for both.
        /// </remarks>
        [Fact]
        public void The_rim_and_the_phone_are_two_rows_at_one_size()
        {
            var source = new MemoryPackageSource()
                .Add("OpenDashPlugin.Resources.openDash 850x480.simhubdash", Package("openDash 850x480", 850, 480))
                .Add("OpenDashPlugin.Resources.openDash Companion.simhubdash", Package("openDash Companion", 850, 480));

            var catalogue = PackageCatalogue.From(source);
            Assert.Equal(2, catalogue.Count);

            var rim = catalogue.Single(entry => entry.DisplayName == "Rim");
            var phone = catalogue.Single(entry => entry.DisplayName == "Phone");
            Assert.Equal(rim.SizeCaption, phone.SizeCaption);
            Assert.Equal(Contract.KindFace, rim.Kind);
            Assert.Equal(Contract.KindCompanion, phone.Kind);
            Assert.NotEqual(rim.Package, phone.Package);
        }

        /// <summary>
        /// Every folder the name table knows is a folder the dash actually builds.
        /// </summary>
        /// <remarks>
        /// Read out of the TypeScript rather than out of the build output, so that it holds in a checkout
        /// that has built nothing: a rename in packages/dash fails this test, and a package unnamed in
        /// the panel is the failure it exists to prevent.
        /// </remarks>
        [Fact]
        public void Every_folder_the_design_names_is_a_folder_the_dash_builds()
        {
            var built = FoldersTheDashDeclares();
            Assert.True(built.Count >= Release.Length, "only " + built.Count + " folders found in packages/dash/src");
            foreach (var folder in PackageCatalogue.NamedFolders())
            {
                Assert.True(built.Contains(folder), "no package in packages/dash/src is built into the folder \"" + folder + "\"");
            }
        }

        /// <summary>
        /// The fourteen packages a release embeds are the fourteen this file lists.
        /// </summary>
        /// <remarks>
        /// The other half of the pin above: the table of folders is what the order and the captions are
        /// asserted against, so it has to be held against what the build writes. The card faces are
        /// excluded here because OpenDash.csproj excludes them from the assembly, which is what makes the
        /// released catalogue fourteen rows rather than twenty-two.
        /// </remarks>
        [Fact]
        public void The_packages_a_release_carries_are_the_fourteen_this_file_lists()
        {
            var packages = TheBuiltPackages();
            if (packages == null)
            {
                // A local checkout that has built nothing. On CI the dash artifact is downloaded into
                // Resources/ before `dotnet test`, so an empty folder there means the artifact moved,
                // and returning without asserting would be the silent skip this test exists to refuse.
                Assert.False(
                    OnCI,
                    "no *.simhubdash in " + RepoPaths.EmbeddedResources() + " or " + RepoPaths.BuildOutput()
                        + ". CI downloads the dash artifact into Resources/ before `dotnet test`.");
                return;
            }

            var catalogue = PackageCatalogue.From(packages);
            Assert.Equal(
                Release.Select(package => package.Folder).OrderBy(folder => folder, StringComparer.Ordinal).ToArray(),
                catalogue.Select(entry => entry.Folder).OrderBy(folder => folder, StringComparer.Ordinal).ToArray());
            foreach (var package in Release)
            {
                var entry = catalogue.Single(row => string.Equals(row.Folder, package.Folder, StringComparison.Ordinal));
                Assert.Equal(package.Width, entry.Width);
                Assert.Equal(package.Height, entry.Height);
            }
        }

        /// <summary>A package with the one entry PackageFolderName looks for and the sizes the panel reads
        /// off the sidecar. Thinner than SyntheticPackage.Zip on purpose: the catalogue reads a folder
        /// name and a size and nothing else.</summary>
        private static MemoryStream Package(string folder, int width, int height)
        {
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                SyntheticPackage.Add(zip, folder + "/" + folder + PackageExtractor.DashExtension, "{\"Version\":2}");
                SyntheticPackage.Add(zip, folder + "/" + folder + PackageExtractor.MetadataExtension,
                    "{\"Title\":\"" + folder + "\",\"Width\":" + width + ",\"Height\":" + height + "}");
            }
            stream.Position = 0;
            return stream;
        }

        /// <summary>Every folder any layout, zone face or second screen in packages/dash declares.</summary>
        private static HashSet<string> FoldersTheDashDeclares()
        {
            var folders = new HashSet<string>(StringComparer.Ordinal);
            var source = Path.Combine(RepoPaths.Root(), "packages", "dash", "src");
            foreach (var file in Directory.GetFiles(source, "*.ts", SearchOption.AllDirectories))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"folder:\s*'([^']+)'"))
                {
                    folders.Add(match.Groups[1].Value);
                }
            }
            return folders;
        }

        /// <summary>The packages a release would embed, from the folder the plugin embeds if it has been
        /// filled and from the dash build output otherwise, or null when neither holds any.</summary>
        private static IPackageSource TheBuiltPackages()
        {
            foreach (var folder in new[] { RepoPaths.EmbeddedResources(), RepoPaths.BuildOutput() })
            {
                if (!Directory.Exists(folder)) continue;
                var files = Directory.GetFiles(folder, "*" + DashboardInstaller.PackageExtension)
                    .Where(file => !Path.GetFileName(file).StartsWith("openDash slots ", StringComparison.Ordinal))
                    .ToArray();
                if (files.Length == 0) continue;
                var source = new MemoryPackageSource();
                foreach (var file in files) source.Add(Path.GetFileName(file), new MemoryStream(File.ReadAllBytes(file)));
                return source;
            }
            return null;
        }
    }
}
