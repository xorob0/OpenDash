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
        /// A screen waits for a restart when this session created its folder, whatever rewrote it since, and
        /// not because a file in it is newer than SimHub: a Dash Studio save, Reinstall and Edit all rewrite
        /// the .djson of a dashboard SimHub has listed since it started.
        /// </summary>
        [Fact]
        public void A_screen_waits_for_a_restart_only_when_this_session_created_its_folder()
        {
            PackageExtractor.Install(Package("OpenDash Rim", "0.1.0"), root, null);
            Directory.CreateDirectory(Templates("Half written"));
            var atStart = PackageExtractor.InstalledFolders(root);
            Assert.Equal(new[] { "OpenDash Rim" }, atStart.ToArray());
            Assert.Contains("opendash rim", atStart);

            // Rewritten after startup, as Dash Studio's save and Reinstall do: still loaded.
            PackageExtractor.Install(Package("OpenDash Rim", "0.2.0"), root, null);
            File.SetLastWriteTimeUtc(PackageExtractor.InstalledDashboard(root, "OpenDash Rim"), DateTime.UtcNow.AddMinutes(5));
            Assert.False(PackageExtractor.WaitsForRestart(atStart, "OpenDash Rim", PackageExtractor.IsInstalled(root, "OpenDash Rim")));

            // Added in this session, however soon after SimHub started: waiting.
            PackageExtractor.Install(Package("OpenDash Wheel", "0.1.0"), root, null);
            Assert.True(PackageExtractor.WaitsForRestart(atStart, "OpenDash Wheel", PackageExtractor.IsInstalled(root, "OpenDash Wheel")));

            // Missing is its own state, and an unread fact says nothing.
            Assert.False(PackageExtractor.WaitsForRestart(atStart, "OpenDash Gone", false));
            Assert.Null(PackageExtractor.WaitsForRestart(null, "OpenDash Wheel", true));
            Assert.Null(PackageExtractor.WaitsForRestart(atStart, "OpenDash Wheel", null));
            Assert.Empty(PackageExtractor.InstalledFolders(Path.Combine(root, "nowhere")));
        }

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
        /// Putting a kept copy back uses it up: the copy is the folder now, and a copy left on the shelf kept the
        /// panel's "Put mine back" offered for ever, however many times it had been pressed (#608).
        /// </summary>
        [Fact]
        public void After_a_restore_the_same_kept_copy_is_not_offered_again()
        {
            PackageExtractor.Install(Package("OpenDash", "0.1.0", ("OpenDash/mine.djson", "my work")), root, null);
            PackageExtractor.Install(Package("OpenDash", "0.2.0"), root, null, holdsAuthoredWork: true);
            var kept = PackageExtractor.KeptCopies(root, "OpenDash").First(path => path.Contains(PackageExtractor.EditedSuffix));

            Assert.True(PackageExtractor.Restore(root, "OpenDash", null, kept));

            Assert.Equal("my work", File.ReadAllText(Path.Combine(Templates("OpenDash"), "mine.djson")));
            Assert.False(File.Exists(kept), "the copy put back is spent");
            Assert.DoesNotContain(kept, PackageExtractor.KeptCopies(root, "OpenDash"));
        }

        /// <summary>
        /// A restore replaces a folder, so it keeps a copy of what it replaces as an install does: a second restore
        /// used to put the old copy back over whatever had been edited since the first, and keep nothing (#608).
        /// </summary>
        [Fact]
        public void A_second_restore_cannot_overwrite_a_newer_folder_without_leaving_a_copy_of_it()
        {
            PackageExtractor.Install(Package("OpenDash", "0.1.0", ("OpenDash/mine.djson", "my work")), root, null);
            PackageExtractor.Install(Package("OpenDash", "0.2.0"), root, null, holdsAuthoredWork: true);
            Assert.True(PackageExtractor.Restore(root, "OpenDash", null, PackageExtractor.KeptCopies(root, "OpenDash")[0]));

            // Edited after the first restore, then a second one: whatever it puts back, the edit survives in a copy.
            File.WriteAllText(Path.Combine(Templates("OpenDash"), "mine.djson"), "later edits");
            Assert.True(PackageExtractor.Restore(root, "OpenDash", null));

            Assert.Contains(PackageExtractor.KeptCopies(root, "OpenDash"), path => Entry(path, "mine.djson") == "later edits");
        }

        /// <summary>
        /// The restore the panel makes is over OpenDash's own folder, the one an install wrote in place of somebody's
        /// work. Its copy is the ordinary one-deep backup, so once the work is back nothing is left to offer (#608).
        /// </summary>
        [Fact]
        public void Restoring_over_OpenDashs_own_folder_leaves_nothing_to_put_back()
        {
            PackageExtractor.Install(Package("OpenDash", "0.1.0", ("OpenDash/mine.djson", "my work")), root, null);
            PackageExtractor.Install(Package("OpenDash", "0.2.0"), root, null, holdsAuthoredWork: true);
            var kept = PackageExtractor.KeptCopies(root, "OpenDash")[0];

            Assert.True(PackageExtractor.Restore(root, "OpenDash", null, kept, holdsAuthoredWork: false));

            Assert.Equal("my work", File.ReadAllText(Path.Combine(Templates("OpenDash"), "mine.djson")));
            var left = PackageExtractor.KeptCopies(root, "OpenDash");
            Assert.False(PanelUpdates.ShowsKept(left));
            // What it replaced is the ordinary backup, so the undo can itself be undone.
            Assert.Equal(new[] { Path.Combine(root, "DashTemplates", "OpenDash" + PackageExtractor.BackupSuffix) }, left);
            Assert.Equal("0.2.0", Versioning.ParseDashboardVersion(Entry(left[0], "OpenDash.djson.metadata")));
        }

        /// <summary>
        /// Putting the ordinary backup back over an untouched folder swaps the two: the backup is rewritten with what
        /// it replaced rather than spent, so a second restore is the undo of the first.
        /// </summary>
        [Fact]
        public void Restoring_the_ordinary_backup_over_an_untouched_folder_swaps_them()
        {
            PackageExtractor.Install(Package("OpenDash", "0.1.0"), root, null);
            PackageExtractor.Install(Package("OpenDash", "0.2.0"), root, null);

            Assert.True(PackageExtractor.Restore(root, "OpenDash", null, holdsAuthoredWork: false));
            Assert.Equal("0.1.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
            Assert.True(PackageExtractor.Restore(root, "OpenDash", null, holdsAuthoredWork: false));
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
        }

        /// <summary>
        /// Two copies kept of one folder within a second used to share a name, and the second deleted the first.
        /// The later copy takes the next free second, so it still sorts as the newer.
        /// </summary>
        [Fact]
        public void Two_copies_kept_within_a_second_are_both_kept_and_the_later_sorts_first()
        {
            PackageExtractor.Install(Package("OpenDash", "0.1.0", ("OpenDash/mine.djson", "first")), root, null);
            PackageExtractor.Install(Package("OpenDash", "0.2.0"), root, null, holdsAuthoredWork: true);
            Directory.CreateDirectory(Templates("OpenDash"));
            File.WriteAllText(Path.Combine(Templates("OpenDash"), "mine.djson"), "second");
            PackageExtractor.Install(Package("OpenDash", "0.3.0"), root, null, holdsAuthoredWork: true);

            var kept = PackageExtractor.KeptCopies(root, "OpenDash").Where(path => path.Contains(PackageExtractor.EditedSuffix)).ToList();
            Assert.Equal(new[] { "second", "first" }, kept.Select(path => Entry(path, "mine.djson")));
        }

        /// <summary>One entry of a kept copy, which stores its entries relative to the folder; null when absent.</summary>
        private static string Entry(string zipPath, string name)
        {
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var entry = zip.GetEntry(name);
                if (entry == null) return null;
                using (var reader = new StreamReader(entry.Open())) return reader.ReadToEnd();
            }
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
        public void A_face_already_there_in_the_same_bytes_is_not_copied_again_under_any_name()
        {
            var fonts = Path.Combine(root, "DashFonts");
            Directory.CreateDirectory(fonts);
            File.WriteAllText(Path.Combine(fonts, "Barlow-Medium.ttf"), "font-a");
            File.WriteAllText(Path.Combine(fonts, "Renamed.ttf"), "font-b");

            InstallResult result;
            using (var package = Package("OpenDash", "0.2.0")) result = PackageExtractor.Install(package, root, null);

            Assert.Equal(0, result.FontsCopied);
            Assert.False(File.Exists(Path.Combine(fonts, "BarlowCondensed-Bold.ttf")));
            Assert.False(Directory.Exists(Path.Combine(fonts, PackageExtractor.FontBackups)));
        }

        /// <summary>
        /// A face whose bytes changed under its name reaches the rig, and the one it replaces is kept.
        /// </summary>
        /// <remarks>
        /// This used to be skipped, which is SimHub's importer's rule, and it meant a rig that had a face never got
        /// a newer build of it: the faces have changed under their names before, and every text is measured against
        /// the faces its package carries. The older one is moved rather than overwritten, into the folder SimHub
        /// itself retires fonts to, because a running SimHub may have it mapped.
        /// </remarks>
        [Fact]
        public void A_face_whose_bytes_changed_under_its_name_replaces_the_one_there_which_is_set_aside()
        {
            var fonts = Path.Combine(root, "DashFonts");
            Directory.CreateDirectory(fonts);
            File.WriteAllText(Path.Combine(fonts, "Barlow-Medium.ttf"), "older bytes, same name");
            var log = new ListLog();

            InstallResult result;
            using (var package = Package("OpenDash", "0.2.0")) result = PackageExtractor.Install(package, root, log);

            Assert.Equal(2, result.FontsCopied);
            Assert.Equal("font-a", File.ReadAllText(Path.Combine(fonts, "Barlow-Medium.ttf")));
            Assert.Equal("font-b", File.ReadAllText(Path.Combine(fonts, "BarlowCondensed-Bold.ttf")));
            Assert.Equal("older bytes, same name", File.ReadAllText(Path.Combine(fonts, PackageExtractor.FontBackups, "Barlow-Medium.ttf")));
            Assert.Contains("info: Replaced font Barlow-Medium.ttf", log.Lines);
            Assert.Contains("info: Installed font BarlowCondensed-Bold.ttf", log.Lines);
        }

        /// <summary>
        /// A face that cannot be moved out of the way is kept, and the rest of the install carries on.
        /// </summary>
        /// <remarks>
        /// What a rig meets is SimHub holding the file; what a runner here can stage is a backup folder it may not
        /// write into, which fails the same move. A face that could not be replaced still draws, so it is not a
        /// reason to fail a dashboard over.
        /// </remarks>
        [DeniedPathFact]
        public void A_face_that_cannot_be_set_aside_is_kept_and_the_other_faces_still_arrive()
        {
            var fonts = Path.Combine(root, "DashFonts");
            var backups = Path.Combine(fonts, PackageExtractor.FontBackups);
            Directory.CreateDirectory(backups);
            File.WriteAllText(Path.Combine(fonts, "Barlow-Medium.ttf"), "older bytes, same name");
            var log = new ListLog();

            InstallResult result;
            using (var denied = new DeniedPaths())
            {
                denied.Deny(backups);
                using (var package = Package("OpenDash", "0.2.0")) result = PackageExtractor.Install(package, root, log);
            }

            Assert.Equal(1, result.FontsCopied);
            Assert.Equal("older bytes, same name", File.ReadAllText(Path.Combine(fonts, "Barlow-Medium.ttf")));
            Assert.Equal("font-b", File.ReadAllText(Path.Combine(fonts, "BarlowCondensed-Bold.ttf")));
            Assert.Contains(log.Lines, line => line.StartsWith("warn: Kept the older Barlow-Medium.ttf", StringComparison.Ordinal));
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
        }

        /// <summary>
        /// An install that wrote a face is told from one that did not, a face replaced under its name included.
        /// </summary>
        /// <remarks>
        /// That is what decides whether the panel may say "close and reopen", since a face written into a running
        /// SimHub is not drawn until it restarts.
        /// </remarks>
        [Fact]
        public void The_faces_an_install_wrote_are_counted_and_the_ones_already_there_are_not()
        {
            var first = DateTime.UtcNow;
            using (var package = Package("OpenDash", "0.1.0")) PackageExtractor.Install(package, root, null);
            Assert.Equal(2, PackageExtractor.FacesWrittenSince(root, first));

            var second = DateTime.UtcNow;
            using (var package = Package("OpenDash", "0.2.0")) PackageExtractor.Install(package, root, null);
            Assert.Equal(0, PackageExtractor.FacesWrittenSince(root, second));

            var newer = new MemoryStream();
            using (var zip = new ZipArchive(newer, ZipArchiveMode.Create, true))
            {
                Add(zip, "OpenDash/OpenDash.djson", "{\"Version\":2}");
                Add(zip, "OpenDash/OpenDash.djson.metadata", "{\"DashboardVersion\":\"0.3.0\"}");
                Add(zip, "OpenDash/_SHFonts/Barlow-Medium.ttf", "a newer build of font-a");
                Add(zip, "OpenDash/_SHFonts/BarlowCondensed-Bold.ttf", "font-b");
            }
            newer.Position = 0;
            var third = DateTime.UtcNow;
            PackageExtractor.Install(newer, root, null);
            Assert.Equal(1, PackageExtractor.FacesWrittenSince(root, third));

            Assert.Equal(0, PackageExtractor.FacesWrittenSince(Path.Combine(root, "no SimHub here"), first));
        }

        /// <summary>
        /// A face is stamped with the time it was written, not the time the package gave it.
        /// </summary>
        /// <remarks>
        /// Every entry of a built package carries one fixed time, which extraction and the copy both keep. The
        /// caches in front of a font file are keyed on its path, size and time, so a newer face written over an
        /// older one of the same size would otherwise look to them like the file they already know.
        /// </remarks>
        [Fact]
        public void A_face_is_stamped_with_the_time_it_was_written()
        {
            var packaged = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                Add(zip, "OpenDash/OpenDash.djson", "{\"Version\":2}");
                Add(zip, "OpenDash/OpenDash.djson.metadata", "{\"DashboardVersion\":\"0.2.0\"}");
                var face = zip.CreateEntry("OpenDash/_SHFonts/openDashDisplay-Light.ttf");
                face.LastWriteTime = packaged;
                using (var writer = new StreamWriter(face.Open())) writer.Write("light");
            }
            stream.Position = 0;
            var before = DateTime.UtcNow.AddMinutes(-1);

            PackageExtractor.Install(stream, root, null);

            var written = File.GetLastWriteTimeUtc(Path.Combine(root, "DashFonts", "openDashDisplay-Light.ttf"));
            Assert.True(written > before, "the face kept the package's time, " + written.ToString("o"));
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

        /// <summary>
        /// A package shaped like a real one: bindings in the main dashboard and in a widget, and the sidecars
        /// SimHub finds by each one's name -- the main dashboard's thumbnail, images and car classes, and the
        /// widget's own metadata and images.
        /// </summary>
        private static MemoryStream Instanceable(string folder, string ns)
        {
            return SyntheticPackage.Instanceable(folder, ns);
        }

        /// <summary>Every file below a folder whose name begins with the given one, whatever its case.</summary>
        private static string[] NamedAfter(string folder, string name)
        {
            return Directory
                .GetFiles(folder, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(file => file.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                .ToArray();
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
        /// Everything SimHub finds by the dashboard's name follows the dashboard to the screen's name.
        /// </summary>
        /// <remarks>
        /// Seen on the VM (#456): a second 850x480 was written with its .djson and its .metadata renamed, and
        /// its thumbnail and its images left under the package's name, so Dash Studio listed it with an empty
        /// box and the trend arrows had no file to be read from. SimHub resolves every sidecar as the .djson's
        /// own path with a suffix appended, so what is asserted is that rule rather than a list of suffixes:
        /// nothing in the copy may still be named after the folder it was made from.
        /// </remarks>
        [Fact]
        public void An_instanced_folder_holds_no_file_named_after_the_package()
        {
            PackageExtractor.Install(Instanceable("OpenDash 1280x480", "Face1280x480"), root, null, false, new PackageExtractor.ScreenTarget
            {
                Folder = "OpenDash Rim",
                Title = "Rim",
                FromNamespace = "Face1280x480",
                ToNamespace = "Rim",
            });
            var folder = Templates("OpenDash Rim");

            Assert.Empty(NamedAfter(folder, "OpenDash 1280x480"));
            Assert.True(File.Exists(Path.Combine(folder, "OpenDash Rim.djson.png")));
            Assert.True(File.Exists(Path.Combine(folder, "OpenDash Rim.djson.carclasses")));
            // SimHub looks an image up inside the zip by the image's own name, never by the dashboard's, so
            // the sidecar moves and nothing inside it has to.
            using (var resources = ZipFile.OpenRead(Path.Combine(folder, "OpenDash Rim.djson.ressources")))
            {
                Assert.Equal(new[] { "trend-down.png", "trend-up.png" }, resources.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal));
            }
            // A widget's sidecars are named after the widget, and stay with it.
            Assert.True(File.Exists(Path.Combine(folder, "zoneface-module.djson.metadata")));
            Assert.True(File.Exists(Path.Combine(folder, "zoneface-module.djson.ressources")));

            // The renames happen in staging, before the folder is moved into place, so the fingerprint
            // the installer records afterwards is of the renamed folder. The next start retitles every
            // screen and finds nothing to change here, so the screen still reads as the one OpenDash wrote
            // rather than as somebody's edit the update path would have to hold back.
            var recorded = FolderFingerprint.Of(folder);
            Assert.False(PackageExtractor.Retitle(root, "OpenDash Rim", "Rim", null));
            Assert.True(FolderFingerprint.LooksUntouched(folder, recorded));
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
        /// A Title that holds no string is left alone and reported, rather than written over the next key.
        /// </summary>
        /// <remarks>
        /// The rewrite took the first quote after <c>"Title":</c> wherever it was, so <c>"Title":null,"X":"y"</c>
        /// had the name of the key after it overwritten and the file was no longer JSON SimHub could read (#620).
        /// </remarks>
        [Fact]
        public void A_Title_that_holds_no_string_is_left_alone_and_reported_as_no_Title()
        {
            var folder = Templates("OpenDash Rim");
            Directory.CreateDirectory(folder);
            const string text = "{\"Title\":null,\"X\":\"y\"}";
            var spaced = "{\"Title\" : \n\t null , \"X\":\"y\"}";
            File.WriteAllText(Path.Combine(folder, "OpenDash Rim.djson"), text);
            File.WriteAllText(Path.Combine(folder, "OpenDash Rim.djson.metadata"), spaced);
            var log = new ListLog();

            Assert.False(PackageExtractor.Retitle(root, "OpenDash Rim", "Rim", log));

            Assert.Equal(text, File.ReadAllText(Path.Combine(folder, "OpenDash Rim.djson")));
            Assert.Equal(spaced, File.ReadAllText(Path.Combine(folder, "OpenDash Rim.djson.metadata")));
            Assert.Contains("warn: No Title in OpenDash Rim.djson; SimHub will list this screen under its folder name.", log.Lines);
            Assert.Contains("warn: No Title in OpenDash Rim.djson.metadata; SimHub will list this screen under its folder name.", log.Lines);
        }

        /// <summary>
        /// A title is written as JSON whatever it holds: a quote, a backslash, a tab, a newline or any other
        /// control character.
        /// </summary>
        /// <remarks>
        /// Only the quote and the backslash were escaped, so a name with a tab in it was written raw into the
        /// string, which JSON forbids (#620). Json.NET, which SimHub reads these files with, happens to let a raw
        /// control character through, so the files are read back twice: by System.Text.Json, which holds to the
        /// standard and refused what was written before, and by Json.NET, which has to read the name exactly.
        /// The space after the colon is how a file Dash Studio has saved spells the key, so it is the spelling
        /// the rewrite has to find.
        /// </remarks>
        [Fact]
        public void A_title_with_a_tab_or_a_newline_is_written_as_valid_JSON()
        {
            var folder = Templates("OpenDash Rim");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "OpenDash Rim.djson"), "{\"Version\":2,\"Metadata\":{\"Title\": \"OpenDash 1280x480\"}}");
            File.WriteAllText(Path.Combine(folder, "OpenDash Rim.djson.metadata"), "{\"Title\":\"OpenDash 1280x480\",\"DashboardVersion\":\"1.0.0\"}");
            const string title = "Rim\tleft\nside \"A\" \\ \u0001\u001f\u007f";

            Assert.True(PackageExtractor.Retitle(root, "OpenDash Rim", title, null));

            foreach (var name in new[] { "OpenDash Rim.djson", "OpenDash Rim.djson.metadata" })
            {
                var text = File.ReadAllText(Path.Combine(folder, name));
                using (var strict = System.Text.Json.JsonDocument.Parse(text))
                {
                    var holder = name.EndsWith(".metadata", StringComparison.Ordinal) ? strict.RootElement : strict.RootElement.GetProperty("Metadata");
                    Assert.Equal(title, holder.GetProperty("Title").GetString());
                }
            }
            var main = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(folder, "OpenDash Rim.djson")));
            var metadata = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(folder, "OpenDash Rim.djson.metadata")));
            Assert.Equal(title, (string)main["Metadata"]["Title"]);
            Assert.Equal(2, (int)main["Version"]);
            Assert.Equal(title, (string)metadata["Title"]);
            Assert.Equal("1.0.0", (string)metadata["DashboardVersion"]);
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
            string[] sidecars;
            using (var zip = ZipFile.OpenRead(package))
            {
                before = zip.Entries
                    .Where(entry => entry.FullName.EndsWith(PackageExtractor.DashExtension, StringComparison.OrdinalIgnoreCase))
                    .Sum(entry =>
                    {
                        using (var reader = new StreamReader(entry.Open())) return Count(reader.ReadToEnd(), "OpenDash.Face1280x480");
                    });
                // Whatever the build ships beside the main dashboard under its name, read off the package
                // rather than listed here, so that a sidecar added later is held to the same rule.
                sidecars = zip.Entries
                    .Select(entry => entry.FullName)
                    .Where(name => name.StartsWith("OpenDash 1280x480/OpenDash 1280x480" + PackageExtractor.DashExtension, StringComparison.Ordinal))
                    .Select(name => name.Substring("OpenDash 1280x480/OpenDash 1280x480".Length))
                    .ToArray();
            }
            Assert.True(before > 0, "the built package should read its own namespace");
            // The two #456 found left behind, so that the rule below is never checked against nothing.
            Assert.Contains(".djson.png", sidecars);
            Assert.Contains(".djson.ressources", sidecars);

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
            foreach (var suffix in sidecars) Assert.True(File.Exists(Path.Combine(folder, "OpenDash Rim" + suffix)), "OpenDash Rim" + suffix + " is missing");
            Assert.Empty(NamedAfter(folder, "OpenDash 1280x480"));
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
