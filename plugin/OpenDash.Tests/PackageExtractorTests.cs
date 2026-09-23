// PackageExtractorTests.cs: installs a synthetic .simhubdash (SyntheticPackage in TestSupport.cs) into a fake SimHub
// root and checks the folder, also one with spaces in its name, the sidecar version, the backup, the font copy rules
// and the refusal of unsafe entries. When the dash has been built, the real package is read as well.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PackageExtractorTests : IDisposable
    {
        private readonly string root;

        public PackageExtractorTests()
        {
            root = Path.Combine(Path.GetTempPath(), "opendash-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            try { Directory.Delete(root, true); } catch { }
        }

        private static MemoryStream Package(string folder, string version, params (string name, string content)[] extra)
        {
            return SyntheticPackage.Zip(folder, version, extra);
        }

        private static void Add(ZipArchive zip, string name, string content)
        {
            SyntheticPackage.Add(zip, name, content);
        }

        private string Templates(string folder) => Path.Combine(root, "DashTemplates", folder);

        /// <summary>
        /// The promise made when somebody consents to replacing their own work is that a copy is kept. A copy the
        /// next routine install reclaims is not kept, so an edited folder's copy goes somewhere no install claims.
        /// </summary>
        [Fact]
        public void A_copy_of_somebody_elses_work_outlives_the_next_install()
        {
            PackageExtractor.Install(Package("OpenDash", "0.1.0", ("OpenDash/mine.djson", "my work")), root, null);
            PackageExtractor.Install(Package("OpenDash", "0.2.0"), root, null, holdsAuthoredWork: true);

            var kept = PackageExtractor.KeptCopies(root, "OpenDash");
            Assert.NotEmpty(kept);
            Assert.Contains(PackageExtractor.EditedSuffix, kept[0]);

            // The ordinary upgrade that follows reclaims only the ordinary backup.
            PackageExtractor.Install(Package("OpenDash", "0.3.0"), root, null);
            Assert.Contains(PackageExtractor.KeptCopies(root, "OpenDash"), path => path.Contains(PackageExtractor.EditedSuffix));

            Assert.True(PackageExtractor.Restore(root, "OpenDash", null, kept[0]));
            Assert.Equal("my work", File.ReadAllText(Path.Combine(Templates("OpenDash"), "mine.djson")));
        }

        /// <summary>
        /// The one case a backup exists for, a disk that is full or a file that is locked, used to be the one case
        /// where it silently did nothing and the dashboard was deleted anyway.
        /// </summary>
        [Fact]
        public void A_copy_that_cannot_be_taken_stops_the_install_rather_than_proceeding()
        {
            PackageExtractor.Install(Package("OpenDash", "0.1.0"), root, null);
            var backup = Path.Combine(root, "DashTemplates", "OpenDash" + PackageExtractor.BackupSuffix);

            // A directory where the zip must go: creating the file fails, as a full disk or a lock would.
            Directory.CreateDirectory(backup);
            Directory.CreateDirectory(Path.Combine(backup, "in the way"));

            Assert.Throws<IOException>(() => PackageExtractor.Install(Package("OpenDash", "0.2.0"), root, null));
            // The installed dashboard is still there and still the old one, which is the point.
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
        }

        /// <summary>
        /// A staging folder is a complete extracted dashboard sitting in DashTemplates. Install removes its own in a
        /// finally, and that finally does not run when the process exits, so every update abandoned by a SimHub that
        /// closed mid-install left one behind and nothing ever removed it.
        /// </summary>
        [Fact]
        public void Staging_folders_an_interrupted_install_left_behind_are_removed()
        {
            PackageExtractor.Install(Package("OpenDash", "0.1.0"), root, null);
            var templates = Path.Combine(root, "DashTemplates");

            var orphan = Path.Combine(templates, PackageExtractor.StagingPrefix + "deadbeef");
            Directory.CreateDirectory(Path.Combine(orphan, "OpenDash"));
            File.WriteAllText(Path.Combine(orphan, "OpenDash", "OpenDash.djson"), "{}");
            Directory.CreateDirectory(Path.Combine(templates, PackageExtractor.StagingPrefix + "cafe"));

            Assert.Equal(2, PackageExtractor.RemoveOrphanedStaging(root, null));
            Assert.Empty(Directory.GetDirectories(templates, PackageExtractor.StagingPrefix + "*"));

            // The dashboards themselves are not staging folders and are left alone.
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
        }

        [Fact]
        public void Removing_staging_folders_is_safe_when_there_are_none_and_when_there_is_no_root()
        {
            Assert.Equal(0, PackageExtractor.RemoveOrphanedStaging(root, null));
            Assert.Equal(0, PackageExtractor.RemoveOrphanedStaging(Path.Combine(root, "absent"), null));
            Assert.Equal(0, PackageExtractor.RemoveOrphanedStaging(null, null));
        }

        [Fact]
        public void Restore_puts_back_the_copy_Install_set_aside()
        {
            var first = PackageExtractor.Install(Package("OpenDash 480 round", "0.1.0", ("OpenDash 480 round/extra.txt", "one")), root, null);
            Assert.Null(first.BackupPath);

            var second = PackageExtractor.Install(Package("OpenDash 480 round", "0.2.0"), root, null);
            Assert.NotNull(second.BackupPath);
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash 480 round"));
            Assert.False(File.Exists(Path.Combine(Templates("OpenDash 480 round"), "extra.txt")));

            Assert.True(PackageExtractor.Restore(root, "OpenDash 480 round", null));
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash 480 round"));
            Assert.Equal("one", File.ReadAllText(Path.Combine(Templates("OpenDash 480 round"), "extra.txt")));
        }

        [Fact]
        public void Restore_reports_when_there_is_nothing_to_put_back()
        {
            PackageExtractor.Install(Package("OpenDash", "0.1.0"), root, null);
            var log = new ListLog();
            Assert.False(PackageExtractor.Restore(root, "OpenDash", log));
            // Still installed: a restore with no backup changes nothing rather than removing the folder.
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
            Assert.Contains(log.Lines, line => line.StartsWith("warn: No previous copy of OpenDash"));
        }

        [Fact]
        public void Restore_leaves_the_installed_copy_alone_when_the_backup_is_not_a_package()
        {
            PackageExtractor.Install(Package("OpenDash", "0.1.0"), root, null);
            PackageExtractor.Install(Package("OpenDash", "0.2.0"), root, null);
            var backup = Path.Combine(root, "DashTemplates", "OpenDash" + PackageExtractor.BackupSuffix);
            File.Delete(backup);
            using (var zip = ZipFile.Open(backup, ZipArchiveMode.Create))
            {
                Add(zip, "not-a-dashboard.txt", "nothing useful");
            }
            Assert.Throws<InvalidDataException>(() => PackageExtractor.Restore(root, "OpenDash", null));
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
        }

        [Fact]
        public void ReadPackageVersion_reads_folder_and_version_without_extracting()
        {
            using (var package = Package("OpenDash", "0.2.0"))
            {
                string folder;
                Assert.Equal("0.2.0", PackageExtractor.ReadPackageVersion(package, out folder));
                Assert.Equal("OpenDash", folder);
                Assert.Empty(Directory.GetDirectories(root));
            }
        }

        [BuildOutputFact]
        public void The_real_build_output_is_OpenDash_at_the_repository_version()
        {
            using (var zip = ZipFile.OpenRead(RepoPaths.BuildPackage()))
            {
                Assert.Equal("OpenDash", PackageExtractor.PackageFolderName(zip));
            }
            using (var package = File.OpenRead(RepoPaths.BuildPackage()))
            {
                string folder;
                var built = PackageExtractor.ReadPackageVersion(package, out folder);
                var expected = RepoPaths.Version();
                // Checking out another branch rewrites VERSION and leaves build/ untouched, so a mismatch here is
                // far more often a package older than the checkout than a regression in whatever writes the version.
                // A bare Assert.Equal names neither the cause nor the remedy, and the misreading costs a diagnosis
                // every time.
                Assert.True(expected == built,
                    "build/OpenDash.simhubdash carries version " + built + ", whereas VERSION says " + expected +
                    ". That package is build output which a branch switch does not refresh, so the likely cause is " +
                    "a stale build rather than a regression: run `bun run build` and try again. Should the two " +
                    "still disagree after a fresh build, then the version written into the package is genuinely wrong.");
                Assert.Equal("OpenDash", folder);
            }
        }

        [Fact]
        public void ReadPackageVersion_returns_null_folder_for_a_zip_without_dashboard()
        {
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) Add(zip, "readme.txt", "hi");
            stream.Position = 0;
            string folder;
            Assert.Null(PackageExtractor.ReadPackageVersion(stream, out folder));
            Assert.Null(folder);
        }

        [Fact]
        public void Install_extracts_into_DashTemplates_and_copies_fonts()
        {
            Assert.False(PackageExtractor.IsInstalled(root, "OpenDash"));
            Assert.Null(PackageExtractor.ReadInstalledVersion(root, "OpenDash"));

            InstallResult result;
            using (var package = Package("OpenDash", "0.2.0")) result = PackageExtractor.Install(package, root, null);

            Assert.Equal("OpenDash", result.FolderName);
            Assert.Equal("0.2.0", result.Version);
            Assert.Equal(2, result.FontsCopied);
            Assert.Null(result.BackupPath);
            Assert.True(File.Exists(Path.Combine(Templates("OpenDash"), "OpenDash.djson")));
            Assert.True(File.Exists(Path.Combine(Templates("OpenDash"), "cards.djson")));
            Assert.True(File.Exists(Path.Combine(root, "DashFonts", "Barlow-Medium.ttf")));
            Assert.True(File.Exists(Path.Combine(root, "DashFonts", "BarlowCondensed-Bold.ttf")));
            Assert.True(PackageExtractor.IsInstalled(root, "OpenDash"));
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
            Assert.Empty(Directory.GetDirectories(Path.Combine(root, "DashTemplates"), "_OpenDash_staging_*"));
        }

        [Fact]
        public void A_folder_name_with_spaces_is_read_from_the_zip_and_installed_as_it_is()
        {
            const string folder = "OpenDash 1280x480";
            using (var package = Package(folder, "0.2.0"))
            {
                string read;
                Assert.Equal("0.2.0", PackageExtractor.ReadPackageVersion(package, out read));
                Assert.Equal(folder, read);
            }

            InstallResult result;
            using (var package = Package(folder, "0.2.0")) result = PackageExtractor.Install(package, root, null);

            Assert.Equal(folder, result.FolderName);
            Assert.Equal("0.2.0", result.Version);
            Assert.True(Directory.Exists(Templates(folder)));
            Assert.True(File.Exists(Path.Combine(Templates(folder), folder + ".djson")));
            Assert.True(File.Exists(Path.Combine(Templates(folder), folder + ".djson.metadata")));
            Assert.True(PackageExtractor.IsInstalled(root, folder));
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, folder));
            Assert.False(PackageExtractor.IsInstalled(root, "OpenDash"));

            using (var package = Package(folder, "0.3.0")) result = PackageExtractor.Install(package, root, null);
            Assert.Equal(Path.Combine(root, "DashTemplates", folder + "_backup.zip"), result.BackupPath);
            Assert.Equal("0.3.0", PackageExtractor.ReadInstalledVersion(root, folder));
        }

        [Fact]
        public void Two_packages_live_side_by_side_under_DashTemplates()
        {
            using (var package = Package("OpenDash", "0.2.0")) PackageExtractor.Install(package, root, null);
            using (var package = Package("OpenDash 800 round", "0.2.0")) PackageExtractor.Install(package, root, null);

            Assert.True(PackageExtractor.IsInstalled(root, "OpenDash"));
            Assert.True(PackageExtractor.IsInstalled(root, "OpenDash 800 round"));
            Assert.Equal(new[] { "OpenDash", "OpenDash 800 round" },
                Directory.GetDirectories(Path.Combine(root, "DashTemplates")).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
        }

        [Fact]
        public void Install_replaces_an_existing_folder_and_keeps_a_backup()
        {
            using (var package = Package("OpenDash", "0.1.0")) PackageExtractor.Install(package, root, null);
            File.WriteAllText(Path.Combine(Templates("OpenDash"), "user-edit.txt"), "edited in DashStudio");

            InstallResult result;
            using (var package = Package("OpenDash", "0.2.0")) result = PackageExtractor.Install(package, root, null);

            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
            Assert.False(File.Exists(Path.Combine(Templates("OpenDash"), "user-edit.txt")));
            Assert.Equal(0, result.FontsCopied);
            Assert.Equal(Path.Combine(root, "DashTemplates", "OpenDash_backup.zip"), result.BackupPath);
            using (var backup = ZipFile.OpenRead(result.BackupPath))
            {
                Assert.Contains(backup.Entries, entry => entry.FullName.EndsWith("user-edit.txt"));
            }
        }

        [Fact]
        public void Fonts_are_skipped_when_the_same_name_or_the_same_bytes_exist()
        {
            var fonts = Path.Combine(root, "DashFonts");
            Directory.CreateDirectory(fonts);
            File.WriteAllText(Path.Combine(fonts, "Barlow-Medium.ttf"), "older bytes, same name");
            File.WriteAllText(Path.Combine(fonts, "Renamed.ttf"), "font-b");

            InstallResult result;
            using (var package = Package("OpenDash", "0.2.0")) result = PackageExtractor.Install(package, root, null);

            Assert.Equal(0, result.FontsCopied);
            Assert.Equal("older bytes, same name", File.ReadAllText(Path.Combine(fonts, "Barlow-Medium.ttf")));
            Assert.False(File.Exists(Path.Combine(fonts, "BarlowCondensed-Bold.ttf")));
        }

        [Fact]
        public void Install_refuses_entries_outside_the_package()
        {
            using (var package = Package("OpenDash", "0.2.0", ("../escape.txt", "no")))
            {
                Assert.Throws<InvalidDataException>(() => PackageExtractor.Install(package, root, null));
            }
            Assert.False(File.Exists(Path.Combine(root, "escape.txt")));
            Assert.False(PackageExtractor.IsInstalled(root, "OpenDash"));
            Assert.Empty(Directory.GetDirectories(Path.Combine(root, "DashTemplates"), "_OpenDash_staging_*"));
        }

        [Fact]
        public void Install_refuses_a_zip_that_is_not_a_dashboard()
        {
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) Add(zip, "OpenDash/other.djson", "{}");
            stream.Position = 0;
            Assert.Throws<InvalidDataException>(() => PackageExtractor.Install(stream, root, null));
        }

        [Fact]
        public void Install_logs_through_the_given_log()
        {
            var log = new ListLog();
            using (var package = Package("OpenDash", "0.2.0")) PackageExtractor.Install(package, root, log);
            Assert.Contains(log.Lines, line => line.StartsWith("info: Installed OpenDash 0.2.0"));
            Assert.Equal(2, log.Lines.Count(line => line.StartsWith("info: Installed font")));
        }

        // --- ADR 0017: a second screen at a size gets its own copy ------------------------------

        /// <summary>A package shaped like a real one: bindings in the main dashboard and in a widget.</summary>
        private static MemoryStream Instanceable(string folder, string ns)
        {
            var main = "{\"Version\":2,\"Metadata\":{\"Title\":\"" + folder + "\"},"
                + "\"A\":\"isnull([OpenDash." + ns + "ZoneA],0)\",\"B\":\"isnull([OpenDash." + ns + "ZoneBPages],0)\"}";
            var widget = "{\"Version\":2,\"C\":\"isnull([OpenDash." + ns + "ZoneC],0)\"}";
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                SyntheticPackage.Add(zip, folder + "/" + folder + ".djson", main);
                SyntheticPackage.Add(zip, folder + "/" + folder + ".djson.metadata", "{\"Title\":\"" + folder + "\",\"DashboardVersion\":\"1.0.0\"}");
                SyntheticPackage.Add(zip, folder + "/zoneface-module.djson", widget);
                SyntheticPackage.Add(zip, folder + "/_SHFonts/Barlow-Medium.ttf", "font-a");
            }
            stream.Position = 0;
            return stream;
        }

        [Fact]
        public void A_second_screen_at_a_size_gets_its_own_folder_and_its_own_properties()
        {
            {
                var target = new PackageExtractor.ScreenTarget
                {
                    Folder = "OpenDash Rim",
                    Title = "Rim",
                    FromNamespace = "Face1280x480",
                    ToNamespace = "Rim",
                };
                var result = PackageExtractor.Install(Instanceable("OpenDash 1280x480", "Face1280x480"), root, null, false, target);
                Assert.Equal("OpenDash Rim", result.FolderName);

                // SimHub finds a dashboard as <folder>/<folder>.djson, so both were renamed.
                var folder = Path.Combine(root, PackageExtractor.DashTemplates, "OpenDash Rim");
                Assert.True(File.Exists(Path.Combine(folder, "OpenDash Rim.djson")));
                Assert.True(File.Exists(Path.Combine(folder, "OpenDash Rim.djson.metadata")));
                Assert.False(File.Exists(Path.Combine(folder, "OpenDash 1280x480.djson")));
                // The sub-dashboards are referenced by bare file name and must not be renamed.
                Assert.True(File.Exists(Path.Combine(folder, "zoneface-module.djson")));

                // Every binding now reads this screen's properties, in the widget as well as the main
                // dashboard, and none of them reads the one it was built for.
                var main = File.ReadAllText(Path.Combine(folder, "OpenDash Rim.djson"));
                var widget = File.ReadAllText(Path.Combine(folder, "zoneface-module.djson"));
                Assert.Contains("[OpenDash.RimZoneA]", main);
                Assert.Contains("[OpenDash.RimZoneBPages]", main);
                Assert.Contains("[OpenDash.RimZoneC]", widget);
                Assert.DoesNotContain("OpenDash.Face1280x480", main);
                Assert.DoesNotContain("OpenDash.Face1280x480", widget);

                // And SimHub's dashboard list shows the name the user chose, which is the whole reason
                // two screens of one size were previously indistinguishable.
                Assert.Contains("\"Title\":\"Rim\"", File.ReadAllText(Path.Combine(folder, "OpenDash Rim.djson.metadata")));
                Assert.Contains("\"Title\":\"Rim\"", main);
            }
        }

        /// <summary>
        /// The name a driver gave a stock screen goes back over the title its package carries.
        /// </summary>
        /// <remarks>
        /// Reported from a rig: after an update, the screens were listed in SimHub under names their
        /// owner had never chosen, which from the outside looks exactly like the dashboards having
        /// disappeared. A stock screen's folder is a package's own and is installed byte for byte, so
        /// every update handed SimHub the package's title again. The name lives in the settings file,
        /// so it is simply written back afterwards.
        /// </remarks>
        [Fact]
        public void A_stock_screen_keeps_the_name_its_owner_gave_it()
        {
            // Installed as the stock screen is: no ScreenTarget at all, so the package's own title is
            // what SimHub would list.
            PackageExtractor.Install(Instanceable("OpenDash 1280x480", "Face1280x480"), root, null);
            var folder = Templates("OpenDash 1280x480");
            var metadata = Path.Combine(folder, "OpenDash 1280x480.djson.metadata");
            var main = Path.Combine(folder, "OpenDash 1280x480.djson");
            Assert.Contains("\"Title\":\"OpenDash 1280x480\"", File.ReadAllText(metadata));

            Assert.True(PackageExtractor.Retitle(root, "OpenDash 1280x480", "Main dash", null));
            // Both places SimHub reads one: the sidecar the dashboard list shows, and the copy inside
            // the .djson that Dash Studio shows once it is open.
            Assert.Contains("\"Title\":\"Main dash\"", File.ReadAllText(metadata));
            Assert.Contains("\"Title\":\"Main dash\"", File.ReadAllText(main));
            // And nothing else moved: the bindings are the package's own, because the folder is.
            Assert.Contains("[OpenDash.Face1280x480ZoneA]", File.ReadAllText(main));

            // Asked for again, it writes nothing. This runs over every screen on every start, and the
            // caller fingerprints whatever it touches, so "no change" has to mean no write.
            Assert.False(PackageExtractor.Retitle(root, "OpenDash 1280x480", "Main dash", null));

            // Nothing to do where there is no folder, no name, or no screen of that name.
            Assert.False(PackageExtractor.Retitle(root, "OpenDash Rim", "Rim", null));
            Assert.False(PackageExtractor.Retitle(root, "OpenDash 1280x480", "  ", null));
            Assert.False(PackageExtractor.Retitle(root, null, "Main dash", null));
        }

        /// <summary>
        /// A folder whose spelling differs from the package's only in case still installs.
        /// </summary>
        /// <remarks>
        /// A rig upgraded across #374 carries its folders spelled "openDash 850x480" in the settings
        /// while the package's own is "OpenDash 850x480". Windows treats those as one path, so
        /// Directory.Move compares them without case and throws "Source and destination path must be
        /// different" rather than renaming -- which is what the Edit panel's reinstall did on the first
        /// rig it met, the press that is meant to be the repair failing on exactly the rigs that need it.
        ///
        /// The assertion is the same on either filesystem; only Windows takes the two-step path inside,
        /// so this is a regression pin here and the real check was driven on the VM.
        /// </remarks>
        [Fact]
        public void A_folder_spelled_differently_only_in_case_still_installs()
        {
            var target = new PackageExtractor.ScreenTarget
            {
                Folder = "opendash 1280x480",
                Title = "Main dash",
                FromNamespace = "Face1280x480",
                ToNamespace = "Face1280x480",
            };
            var result = PackageExtractor.Install(Instanceable("OpenDash 1280x480", "Face1280x480"), root, null, false, target);
            Assert.Equal("opendash 1280x480", result.FolderName);

            // SimHub finds a dashboard as <folder>/<folder>.djson, so the spelling the screen asked for
            // has to be the spelling of both.
            var folder = Templates("opendash 1280x480");
            Assert.True(File.Exists(Path.Combine(folder, "opendash 1280x480.djson")));
            Assert.True(File.Exists(Path.Combine(folder, "opendash 1280x480.djson.metadata")));
            Assert.Contains("\"Title\":\"Main dash\"", File.ReadAllText(Path.Combine(folder, "opendash 1280x480.djson.metadata")));
            // And nothing is left behind under the name it went through to get there.
            Assert.Empty(Directory.GetFiles(folder, "*.case*"));
        }

        [Fact]
        public void The_stock_screen_is_written_byte_for_byte()
        {
            // The first screen at a size rewrites nothing, which is what keeps the ordinary rig
            // producing exactly the files it produced before ADR 0017.
            {
                var target = new PackageExtractor.ScreenTarget
                {
                    Folder = "OpenDash 1280x480",
                    Title = "OpenDash 1280x480",
                    FromNamespace = "Face1280x480",
                    ToNamespace = "Face1280x480",
                };
                Assert.False(target.Rewrites);
                PackageExtractor.Install(Instanceable("OpenDash 1280x480", "Face1280x480"), root, null, false, target);
                var main = Path.Combine(root, PackageExtractor.DashTemplates, "OpenDash 1280x480", "OpenDash 1280x480.djson");
                Assert.Contains("[OpenDash.Face1280x480ZoneA]", File.ReadAllText(main));
            }
        }

        [Fact]
        public void A_rewrite_that_cannot_find_the_namespace_refuses_to_install()
        {
            // A package that mentions the namespace nowhere would be a copy silently reading the first
            // screen's settings, which is the exact failure the mechanism exists to prevent.
            {
                var target = new PackageExtractor.ScreenTarget
                {
                    Folder = "OpenDash Rim",
                    Title = "Rim",
                    FromNamespace = "Face1920x480",
                    ToNamespace = "Rim",
                };
                Assert.Throws<InvalidDataException>(() =>
                    PackageExtractor.Install(Instanceable("OpenDash 1280x480", "Face1280x480"), root, null, false, target));
                Assert.False(Directory.Exists(Path.Combine(root, PackageExtractor.DashTemplates, "OpenDash Rim")));
            }
        }

        [Fact]
        public void Two_screens_of_one_size_end_up_with_disjoint_properties()
        {
            // The end-to-end statement of ADR 0017, on disk: the two folders share no property name.
            {
                PackageExtractor.Install(Instanceable("OpenDash 1280x480", "Face1280x480"), root, null, false,
                    new PackageExtractor.ScreenTarget { Folder = "OpenDash 1280x480", Title = "Main dash", FromNamespace = "Face1280x480", ToNamespace = "Face1280x480" });
                PackageExtractor.Install(Instanceable("OpenDash 1280x480", "Face1280x480"), root, null, false,
                    new PackageExtractor.ScreenTarget { Folder = "OpenDash Rim", Title = "Rim", FromNamespace = "Face1280x480", ToNamespace = "Rim" });

                var templates = Path.Combine(root, PackageExtractor.DashTemplates);
                var first = File.ReadAllText(Path.Combine(templates, "OpenDash 1280x480", "OpenDash 1280x480.djson"));
                var second = File.ReadAllText(Path.Combine(templates, "OpenDash Rim", "OpenDash Rim.djson"));
                Assert.Contains("OpenDash.Face1280x480ZoneA", first);
                Assert.DoesNotContain("OpenDash.RimZoneA", first);
                Assert.Contains("OpenDash.RimZoneA", second);
                Assert.DoesNotContain("OpenDash.Face1280x480ZoneA", second);
            }
        }

        [BuildOutputFact]
        public void The_real_package_instances_cleanly()
        {
            // The synthetic packages above prove the mechanism; this proves it against the scene graph
            // the generator actually emits, which on 2026-09-13 held 716 references across four files.
            // ADR 0017 rests on that rewrite being total, so it is checked on the real thing rather
            // than only on a package shaped like it.
            var package = Path.Combine(RepoPaths.BuildOutput(), "OpenDash 1280x480.simhubdash");
            // Asserted rather than returned on. BuildOutputFact skips this class when there is no build
            // at all, but it looks at OpenDash.simhubdash, and this test reads a different package; a
            // quiet return would have let the whole case pass having checked nothing.
            Assert.True(File.Exists(package), package + " is missing although the build output is present; run `bun run build`");

            int before;
            using (var zip = ZipFile.OpenRead(package))
            {
                before = zip.Entries
                    .Where(entry => entry.FullName.EndsWith(PackageExtractor.DashExtension, StringComparison.OrdinalIgnoreCase))
                    .Sum(entry =>
                    {
                        using (var reader = new StreamReader(entry.Open())) return Count(reader.ReadToEnd(), "OpenDash.Face1280x480");
                    });
            }
            Assert.True(before > 0, "the built package should read its own namespace");

            using (var stream = File.OpenRead(package))
            {
                PackageExtractor.Install(stream, root, null, false, new PackageExtractor.ScreenTarget
                {
                    Folder = "OpenDash Rim",
                    Title = "Rim",
                    FromNamespace = "Face1280x480",
                    ToNamespace = "Rim",
                });
            }

            var folder = Path.Combine(root, PackageExtractor.DashTemplates, "OpenDash Rim");
            Assert.True(File.Exists(Path.Combine(folder, "OpenDash Rim.djson")));
            var after = 0;
            foreach (var file in Directory.GetFiles(folder, "*" + PackageExtractor.DashExtension, SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                // Not one reference may be left behind: one that was would read the other screen's
                // settings, silently, which is the failure the whole mechanism exists to prevent.
                Assert.DoesNotContain("OpenDash.Face1280x480", text);
                after += Count(text, "OpenDash.Rim");
            }
            Assert.Equal(before, after);
        }

        private static int Count(string text, string token)
        {
            var count = 0;
            var at = text.IndexOf(token, StringComparison.Ordinal);
            while (at >= 0)
            {
                count++;
                at = text.IndexOf(token, at + token.Length, StringComparison.Ordinal);
            }
            return count;
        }
    }

    /// <summary>A fact that needs build/OpenDash.simhubdash: reported as skipped, not failed, until `bun run build` has run.</summary>
    public sealed class BuildOutputFactAttribute : FactAttribute
    {
        public BuildOutputFactAttribute()
        {
            try
            {
                if (!File.Exists(RepoPaths.BuildPackage())) Skip = "build/OpenDash.simhubdash is absent; run `bun run build` first.";
            }
            catch (Exception ex)
            {
                Skip = "The dash build output cannot be located: " + ex.Message;
            }
        }

    }
}
