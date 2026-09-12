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
            PackageExtractor.Install(Package("openDash", "0.1.0", ("openDash/mine.djson", "my work")), root, null);
            PackageExtractor.Install(Package("openDash", "0.2.0"), root, null, holdsAuthoredWork: true);

            var kept = PackageExtractor.KeptCopies(root, "openDash");
            Assert.NotEmpty(kept);
            Assert.Contains(PackageExtractor.EditedSuffix, kept[0]);

            // The ordinary upgrade that follows reclaims only the ordinary backup.
            PackageExtractor.Install(Package("openDash", "0.3.0"), root, null);
            Assert.Contains(PackageExtractor.KeptCopies(root, "openDash"), path => path.Contains(PackageExtractor.EditedSuffix));

            Assert.True(PackageExtractor.Restore(root, "openDash", null, kept[0]));
            Assert.Equal("my work", File.ReadAllText(Path.Combine(Templates("openDash"), "mine.djson")));
        }

        /// <summary>
        /// The one case a backup exists for, a disk that is full or a file that is locked, used to be the one case
        /// where it silently did nothing and the dashboard was deleted anyway.
        /// </summary>
        [Fact]
        public void A_copy_that_cannot_be_taken_stops_the_install_rather_than_proceeding()
        {
            PackageExtractor.Install(Package("openDash", "0.1.0"), root, null);
            var backup = Path.Combine(root, "DashTemplates", "openDash" + PackageExtractor.BackupSuffix);

            // A directory where the zip must go: creating the file fails, as a full disk or a lock would.
            Directory.CreateDirectory(backup);
            Directory.CreateDirectory(Path.Combine(backup, "in the way"));

            Assert.Throws<IOException>(() => PackageExtractor.Install(Package("openDash", "0.2.0"), root, null));
            // The installed dashboard is still there and still the old one, which is the point.
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
        }

        /// <summary>
        /// A staging folder is a complete extracted dashboard sitting in DashTemplates. Install removes its own in a
        /// finally, and that finally does not run when the process exits, so every update abandoned by a SimHub that
        /// closed mid-install left one behind and nothing ever removed it.
        /// </summary>
        [Fact]
        public void Staging_folders_an_interrupted_install_left_behind_are_removed()
        {
            PackageExtractor.Install(Package("openDash", "0.1.0"), root, null);
            var templates = Path.Combine(root, "DashTemplates");

            var orphan = Path.Combine(templates, PackageExtractor.StagingPrefix + "deadbeef");
            Directory.CreateDirectory(Path.Combine(orphan, "openDash"));
            File.WriteAllText(Path.Combine(orphan, "openDash", "openDash.djson"), "{}");
            Directory.CreateDirectory(Path.Combine(templates, PackageExtractor.StagingPrefix + "cafe"));

            Assert.Equal(2, PackageExtractor.RemoveOrphanedStaging(root, null));
            Assert.Empty(Directory.GetDirectories(templates, PackageExtractor.StagingPrefix + "*"));

            // The dashboards themselves are not staging folders and are left alone.
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
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
            var first = PackageExtractor.Install(Package("openDash 480 round", "0.1.0", ("openDash 480 round/extra.txt", "one")), root, null);
            Assert.Null(first.BackupPath);

            var second = PackageExtractor.Install(Package("openDash 480 round", "0.2.0"), root, null);
            Assert.NotNull(second.BackupPath);
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "openDash 480 round"));
            Assert.False(File.Exists(Path.Combine(Templates("openDash 480 round"), "extra.txt")));

            Assert.True(PackageExtractor.Restore(root, "openDash 480 round", null));
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "openDash 480 round"));
            Assert.Equal("one", File.ReadAllText(Path.Combine(Templates("openDash 480 round"), "extra.txt")));
        }

        [Fact]
        public void Restore_reports_when_there_is_nothing_to_put_back()
        {
            PackageExtractor.Install(Package("openDash", "0.1.0"), root, null);
            var log = new ListLog();
            Assert.False(PackageExtractor.Restore(root, "openDash", log));
            // Still installed: a restore with no backup changes nothing rather than removing the folder.
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
            Assert.Contains(log.Lines, line => line.StartsWith("warn: No previous copy of openDash"));
        }

        [Fact]
        public void Restore_leaves_the_installed_copy_alone_when_the_backup_is_not_a_package()
        {
            PackageExtractor.Install(Package("openDash", "0.1.0"), root, null);
            PackageExtractor.Install(Package("openDash", "0.2.0"), root, null);
            var backup = Path.Combine(root, "DashTemplates", "openDash" + PackageExtractor.BackupSuffix);
            File.Delete(backup);
            using (var zip = ZipFile.Open(backup, ZipArchiveMode.Create))
            {
                Add(zip, "not-a-dashboard.txt", "nothing useful");
            }
            Assert.Throws<InvalidDataException>(() => PackageExtractor.Restore(root, "openDash", null));
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
        }

        [Fact]
        public void ReadPackageVersion_reads_folder_and_version_without_extracting()
        {
            using (var package = Package("openDash", "0.2.0"))
            {
                string folder;
                Assert.Equal("0.2.0", PackageExtractor.ReadPackageVersion(package, out folder));
                Assert.Equal("openDash", folder);
                Assert.Empty(Directory.GetDirectories(root));
            }
        }

        [BuildOutputFact]
        public void The_real_build_output_is_openDash_at_the_repository_version()
        {
            using (var zip = ZipFile.OpenRead(RepoPaths.BuildPackage()))
            {
                Assert.Equal("openDash", PackageExtractor.PackageFolderName(zip));
            }
            using (var package = File.OpenRead(RepoPaths.BuildPackage()))
            {
                string folder;
                Assert.Equal(RepoPaths.Version(), PackageExtractor.ReadPackageVersion(package, out folder));
                Assert.Equal("openDash", folder);
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
            Assert.False(PackageExtractor.IsInstalled(root, "openDash"));
            Assert.Null(PackageExtractor.ReadInstalledVersion(root, "openDash"));

            InstallResult result;
            using (var package = Package("openDash", "0.2.0")) result = PackageExtractor.Install(package, root, null);

            Assert.Equal("openDash", result.FolderName);
            Assert.Equal("0.2.0", result.Version);
            Assert.Equal(2, result.FontsCopied);
            Assert.Null(result.BackupPath);
            Assert.True(File.Exists(Path.Combine(Templates("openDash"), "openDash.djson")));
            Assert.True(File.Exists(Path.Combine(Templates("openDash"), "cards.djson")));
            Assert.True(File.Exists(Path.Combine(root, "DashFonts", "Barlow-Medium.ttf")));
            Assert.True(File.Exists(Path.Combine(root, "DashFonts", "BarlowCondensed-Bold.ttf")));
            Assert.True(PackageExtractor.IsInstalled(root, "openDash"));
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
            Assert.Empty(Directory.GetDirectories(Path.Combine(root, "DashTemplates"), "_openDash_staging_*"));
        }

        [Fact]
        public void A_folder_name_with_spaces_is_read_from_the_zip_and_installed_as_it_is()
        {
            const string folder = "openDash 1280x480";
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
            Assert.False(PackageExtractor.IsInstalled(root, "openDash"));

            using (var package = Package(folder, "0.3.0")) result = PackageExtractor.Install(package, root, null);
            Assert.Equal(Path.Combine(root, "DashTemplates", folder + "_backup.zip"), result.BackupPath);
            Assert.Equal("0.3.0", PackageExtractor.ReadInstalledVersion(root, folder));
        }

        [Fact]
        public void Two_packages_live_side_by_side_under_DashTemplates()
        {
            using (var package = Package("openDash", "0.2.0")) PackageExtractor.Install(package, root, null);
            using (var package = Package("openDash 800 round", "0.2.0")) PackageExtractor.Install(package, root, null);

            Assert.True(PackageExtractor.IsInstalled(root, "openDash"));
            Assert.True(PackageExtractor.IsInstalled(root, "openDash 800 round"));
            Assert.Equal(new[] { "openDash", "openDash 800 round" },
                Directory.GetDirectories(Path.Combine(root, "DashTemplates")).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
        }

        [Fact]
        public void Install_replaces_an_existing_folder_and_keeps_a_backup()
        {
            using (var package = Package("openDash", "0.1.0")) PackageExtractor.Install(package, root, null);
            File.WriteAllText(Path.Combine(Templates("openDash"), "user-edit.txt"), "edited in DashStudio");

            InstallResult result;
            using (var package = Package("openDash", "0.2.0")) result = PackageExtractor.Install(package, root, null);

            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
            Assert.False(File.Exists(Path.Combine(Templates("openDash"), "user-edit.txt")));
            Assert.Equal(0, result.FontsCopied);
            Assert.Equal(Path.Combine(root, "DashTemplates", "openDash_backup.zip"), result.BackupPath);
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
            using (var package = Package("openDash", "0.2.0")) result = PackageExtractor.Install(package, root, null);

            Assert.Equal(0, result.FontsCopied);
            Assert.Equal("older bytes, same name", File.ReadAllText(Path.Combine(fonts, "Barlow-Medium.ttf")));
            Assert.False(File.Exists(Path.Combine(fonts, "BarlowCondensed-Bold.ttf")));
        }

        [Fact]
        public void Install_refuses_entries_outside_the_package()
        {
            using (var package = Package("openDash", "0.2.0", ("../escape.txt", "no")))
            {
                Assert.Throws<InvalidDataException>(() => PackageExtractor.Install(package, root, null));
            }
            Assert.False(File.Exists(Path.Combine(root, "escape.txt")));
            Assert.False(PackageExtractor.IsInstalled(root, "openDash"));
            Assert.Empty(Directory.GetDirectories(Path.Combine(root, "DashTemplates"), "_openDash_staging_*"));
        }

        [Fact]
        public void Install_refuses_a_zip_that_is_not_a_dashboard()
        {
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) Add(zip, "openDash/other.djson", "{}");
            stream.Position = 0;
            Assert.Throws<InvalidDataException>(() => PackageExtractor.Install(stream, root, null));
        }

        [Fact]
        public void Install_logs_through_the_given_log()
        {
            var log = new ListLog();
            using (var package = Package("openDash", "0.2.0")) PackageExtractor.Install(package, root, log);
            Assert.Contains(log.Lines, line => line.StartsWith("info: Installed openDash 0.2.0"));
            Assert.Equal(2, log.Lines.Count(line => line.StartsWith("info: Installed font")));
        }
    }

    /// <summary>A fact that needs build/openDash.simhubdash: reported as skipped, not failed, until `bun run build` has run.</summary>
    public sealed class BuildOutputFactAttribute : FactAttribute
    {
        public BuildOutputFactAttribute()
        {
            try
            {
                if (!File.Exists(RepoPaths.BuildPackage())) Skip = "build/openDash.simhubdash is absent; run `bun run build` first.";
            }
            catch (Exception ex)
            {
                Skip = "The dash build output cannot be located: " + ex.Message;
            }
        }
    }
}
