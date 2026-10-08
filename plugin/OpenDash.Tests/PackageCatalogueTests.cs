// PackageCatalogueTests.cs: what the Install tab calls each package, what it captions it with, and the
// order it offers them in.
//
// The names are pinned verbatim for the reason PanelCopyTests pins the panel's sentences: they are the
// first thing a user reads on the tab. The folders are pinned twice over, because the table that carries
// the names is a third place knowing them -- PackageCatalogue.PrimaryFolder and the layouts' own `folder`
// fields are the others -- and a rename on one side only would leave a row that is silently unnamed rather
// than a build that fails.
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
        /// not here either. The_packages_a_release_carries_are_the_ones_this_file_lists holds this
        /// table, with the themed packages below it, against what the dash actually built.
        /// </remarks>
        private static readonly (string Folder, int Width, int Height)[] Release =
        {
            ("OpenDash", 1920, 480),
            ("OpenDash 1280x480", 1280, 480),
            ("OpenDash 1280x400", 1280, 400),
            ("OpenDash 850x480", 850, 480),
            ("OpenDash 800x480", 800, 480),
            ("OpenDash 1280x720", 1280, 720),
            ("OpenDash 800x286", 800, 286),
            ("OpenDash 600x686", 600, 686),
            ("OpenDash 480 round", 480, 480),
            ("OpenDash 800 round", 800, 800),
            ("OpenDash Companion", 850, 480),
            ("OpenDash Companion portrait", 480, 850),
            ("OpenDash Pit wall", 1920, 1080),
            ("OpenDash Pit wall portrait", 1080, 1920),
        };

        /// <summary>
        /// The themed packages a release embeds beside those fourteen, which are written only when a
        /// driver picks one (ADR 0016) and are therefore kept out of the table the design's names and
        /// order are asserted against.
        /// </summary>
        private static readonly (string Folder, int Width, int Height)[] Themed =
        {
            ("OpenDash Porsche 1920x480", 1920, 480),
            ("OpenDash Porsche 1280x480", 1280, 480),
            ("OpenDash Porsche 1280x400", 1280, 400),
            ("OpenDash Porsche 850x480", 850, 480),
            ("OpenDash Porsche 800x480", 800, 480),
            ("OpenDash Porsche 1280x720", 1280, 720),
            ("OpenDash Porsche 800x286", 800, 286),
            ("OpenDash Porsche 600x686", 600, 686),
            ("OpenDash AiM 1920x480", 1920, 480),
            ("OpenDash AiM 1280x480", 1280, 480),
            ("OpenDash AiM 1280x400", 1280, 400),
            ("OpenDash AiM 850x480", 850, 480),
            ("OpenDash AiM 800x480", 800, 480),
            ("OpenDash AiM 1280x720", 1280, 720),
            ("OpenDash AiM 800x286", 800, 286),
            ("OpenDash AiM 600x686", 600, 686),
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
            Assert.Equal("Main DDU", Entry("OpenDash", 1920, 480).DisplayName);
            Assert.Equal("1920 × 480", Entry("OpenDash", 1920, 480).SizeCaption);

            Assert.Equal("Rim", Entry("OpenDash 850x480", 850, 480).DisplayName);
            Assert.Equal("850 × 480", Entry("OpenDash 850x480", 850, 480).SizeCaption);

            Assert.Equal("Pit wall", Entry("OpenDash Pit wall", 1920, 1080).DisplayName);
            Assert.Equal("1920 × 1080", Entry("OpenDash Pit wall", 1920, 1080).SizeCaption);

            Assert.Equal("Phone", Entry("OpenDash Companion", 850, 480).DisplayName);
            Assert.Equal("850 × 480", Entry("OpenDash Companion", 850, 480).SizeCaption);

            Assert.Equal("Nano", Entry("OpenDash 800x286", 800, 286).DisplayName);
            Assert.Equal("800 × 286", Entry("OpenDash 800x286", 800, 286).SizeCaption);

            // The one caption that is not a size. The package is 480 × 480 pixels and the screen it is
            // drawn for is round, which is the fact somebody shopping for a DDU recognises.
            Assert.Equal("Round", Entry("OpenDash 480 round", 480, 480).DisplayName);
            Assert.Equal("480 round", Entry("OpenDash 480 round", 480, 480).SizeCaption);
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
            var face = Entry("OpenDash 1280x480", 1280, 480);
            Assert.Equal("OpenDash 1280x480", face.DisplayName);
            Assert.Null(face.SizeCaption);

            var portrait = Entry("OpenDash Pit wall portrait", 1080, 1920);
            Assert.Equal("OpenDash Pit wall portrait", portrait.DisplayName);
            Assert.Null(portrait.SizeCaption);

            // The larger round is the near miss: the design names one round face and not the other.
            var round = Entry("OpenDash 800 round", 800, 800);
            Assert.Equal("OpenDash 800 round", round.DisplayName);
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
            //
            // Spelled as a plain call on Enumerable rather than as `Release.Reverse()`, because from
            // C# 13 the span extensions in System.MemoryExtensions become applicable to an array and
            // beat the LINQ ones on it. `MemoryExtensions.Reverse(Span<T>)` reverses in place and
            // returns void, so the extension-method spelling compiles on an SDK 8 machine and fails
            // on the runner with CS1579. This spelling means one thing on every compiler.
            foreach (var package in Enumerable.Reverse(Release))
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
                    "OpenDash 1280x720",
                    "OpenDash 1280x480",
                    "OpenDash 1280x400",
                    "OpenDash 600x686",
                    "OpenDash 800x480",
                    "OpenDash Companion portrait",
                    "OpenDash Pit wall portrait",
                    "OpenDash 800 round",
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
                .Add("OpenDashPlugin.Resources.OpenDash 850x480.simhubdash", Package("OpenDash 850x480", 850, 480))
                .Add("OpenDashPlugin.Resources.OpenDash Companion.simhubdash", Package("OpenDash Companion", 850, 480));

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
        /// The packages a release embeds are the fourteen this file lists and the themed ones after them.
        /// </summary>
        /// <remarks>
        /// The other half of the pin above: the table of folders is what the order and the captions are
        /// asserted against, so it has to be held against what the build writes. The card faces are
        /// excluded here because OpenDash.csproj excludes them from the assembly, which is what makes the
        /// released catalogue fourteen rows rather than twenty-two.
        /// </remarks>
        [Fact]
        public void The_packages_a_release_carries_are_the_ones_this_file_lists()
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
            var carried = Release.Concat(Themed).ToArray();
            Assert.Equal(
                carried.Select(package => package.Folder).OrderBy(folder => folder, StringComparer.Ordinal).ToArray(),
                catalogue.Select(entry => entry.Folder).OrderBy(folder => folder, StringComparer.Ordinal).ToArray());
            foreach (var package in carried)
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

        /// <summary>
        /// A screen's folder never ends in a dot or a space, whatever its name ends in.
        /// </summary>
        /// <remarks>
        /// Windows drops a trailing dot or space from the last segment of a path, so a screen named "Rim." was
        /// given the folder "OpenDash Rim." and written into "OpenDash Rim", with its main dashboard renamed to
        /// "OpenDash Rim..djson" inside it. SimHub looks for "OpenDash Rim/OpenDash Rim.djson", found nothing and
        /// did not list the screen, and where the rig already had a Rim the copy was written over that screen's
        /// folder (#620). A folder recorded with a trailing dot is the same folder on disk as the one without, so
        /// it is taken under that spelling too.
        /// </remarks>
        [Fact]
        public void A_folder_never_ends_in_a_dot_or_a_space()
        {
            var none = new string[0];
            Assert.Equal("OpenDash Rim", PackageCatalogue.UniqueFolder("Rim.", none, "OpenDash 1280x480"));
            Assert.Equal("OpenDash Rim", PackageCatalogue.UniqueFolder("Rim . . ", none, "OpenDash 1280x480"));
            Assert.Equal("OpenDash screen", PackageCatalogue.UniqueFolder("...", none, "OpenDash 1280x480"));
            Assert.Equal("OpenDash St. Rim", PackageCatalogue.UniqueFolder("St. Rim", none, "OpenDash 1280x480"));
            foreach (var name in new[] { "Rim.", "Rim. ", "Rim ..", "Rim\t.", "Rim.\n" })
            {
                var folder = PackageCatalogue.UniqueFolder(name, none, "OpenDash 1280x480");
                Assert.False(folder.EndsWith(".", StringComparison.Ordinal) || char.IsWhiteSpace(folder[folder.Length - 1]), name + " gave \"" + folder + "\"");
            }

            // Two folders that differ only by a trailing dot are one folder on Windows.
            Assert.Equal("OpenDash Rim 2", PackageCatalogue.UniqueFolder("Rim.", new[] { "OpenDash Rim" }, "OpenDash 1280x480"));
            Assert.Equal("OpenDash Rim 2", PackageCatalogue.UniqueFolder("Rim", new[] { "OpenDash Rim." }, "OpenDash 1280x480"));
        }

        /// <summary>
        /// A themed package is a face of its size and of its theme, named as ADR 0016 spells it.
        /// </summary>
        /// <remarks>
        /// Read from the catalogue at the sizes each theme claims and at nothing looser, because a second
        /// screen's folder is slugged from a name its owner typed, and "OpenDash Porsche 1366x768" names
        /// no package, being no size the Porsche claims.
        /// </remarks>
        [Fact]
        public void A_themed_folder_is_a_face_of_its_theme_and_its_size()
        {
            const string porsche = "OpenDash Porsche 1280x480";
            Assert.Equal("porsche", PackageCatalogue.ThemeOf(porsche).Id);
            Assert.Equal("porsche", PackageCatalogue.ThemeOf("opendash porsche 1280X480").Id);
            Assert.Equal(Contract.KindFace, PackageCatalogue.Classify(porsche, 1280, 480));
            int width, height;
            PackageCatalogue.SizeFromFolder(porsche, out width, out height);
            Assert.Equal(1280, width);
            Assert.Equal(480, height);

            foreach (var folder in new[] { "OpenDash", "OpenDash 1280x480", "OpenDash Porsche", "OpenDash Porsche 1366x768", "OpenDash Companion", "OpenDash Rim", null, "" })
            {
                Assert.Null(PackageCatalogue.ThemeOf(folder));
            }

            // Every size of every theme in the catalogue, and the default at none.
            foreach (var theme in Contract.Themes.Where(t => !t.IsDefault))
            {
                foreach (var size in theme.Sizes)
                {
                    var folder = theme.FolderAt(size.Width, size.Height);
                    Assert.Same(theme, PackageCatalogue.ThemeOf(folder));
                    Assert.Equal(Contract.KindFace, PackageCatalogue.Classify(folder, size.Width, size.Height));
                }
            }
            Assert.Throws<InvalidOperationException>(() => Contract.Themes[0].FolderAt(1280, 480));
        }

        /// <summary>A theme's name is the one word of its folder this file does not choose, so a theme that
        /// happened to be called something Classify reads as another kind is still a face.</summary>
        [Fact]
        public void A_theme_is_found_by_its_whole_folder_whatever_its_name_contains()
        {
            var companion = new Contract.ThemeEntry("companion-car", "Companion", new string[0], new string[0], new[] { Contract.FaceOf(850, 480).Value });
            var themes = new[] { Contract.Themes[0], companion };
            Assert.Same(companion, PackageCatalogue.ThemeOf("OpenDash Companion 850x480", themes));
            Assert.Null(PackageCatalogue.ThemeOf("OpenDash Companion", themes));
        }

        [Fact]
        public void The_catalogue_says_which_embedded_package_is_themed()
        {
            var packages = new MemoryPackageSource()
                .Add("OpenDashPlugin.Resources.OpenDash 1280x480.simhubdash", SyntheticPackage.Zip("OpenDash 1280x480", "0.2.0"))
                .Add("OpenDashPlugin.Resources.OpenDash Porsche 1280x480.simhubdash", SyntheticPackage.Zip("OpenDash Porsche 1280x480", "0.2.0"));
            var entries = PackageCatalogue.From(packages);
            var themed = entries.Single(e => e.Folder == "OpenDash Porsche 1280x480");
            Assert.Equal("porsche", themed.Theme);
            Assert.Equal(Contract.KindFace, themed.Kind);
            Assert.Equal(1280, themed.Width);
            Assert.Null(entries.Single(e => e.Folder == "OpenDash 1280x480").Theme);
        }

        /// <summary>The packages a release would embed, from the folder the plugin embeds if it has been
        /// filled and from the dash build output otherwise, or null when neither holds any.</summary>
        private static IPackageSource TheBuiltPackages()
        {
            foreach (var folder in new[] { RepoPaths.EmbeddedResources(), RepoPaths.BuildOutput() })
            {
                if (!Directory.Exists(folder)) continue;
                var files = Directory.GetFiles(folder, "*" + DashboardInstaller.PackageExtension)
                    .Where(file => !Path.GetFileName(file).StartsWith("OpenDash slots ", StringComparison.Ordinal))
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
