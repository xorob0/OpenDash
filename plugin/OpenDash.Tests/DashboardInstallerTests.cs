// DashboardInstallerTests.cs: the installer against a temporary SimHub root and synthetic packages: every embedded
// package is installed, only the ones that need it unless forced, the worst status wins, a broken package does not stop
// the others, the panel's summary text, and the embedded resource naming (spaces in a file name survive). Also the pure
// InstalledVersionFrom: an absent folder is not installed, a folder without a usable sidecar is reinstalled.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class DashboardInstallerTests : IDisposable
    {
        private const string Sidecar = "{\"Title\":\"openDash\",\"DashboardVersion\":\"0.1.0\"}";

        // Resource names the way MSBuild embeds Resources/*.simhubdash: ordinal order puts the spaced name first.
        private const string WideName = "OpenDashPlugin.Resources.openDash.simhubdash";
        private const string SmallName = "OpenDashPlugin.Resources.openDash 1280x480.simhubdash";
        private const string SmallFolder = "openDash 1280x480";

        private readonly string root;

        public DashboardInstallerTests()
        {
            root = Path.Combine(Path.GetTempPath(), "opendash-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            try { Directory.Delete(root, true); } catch { }
        }

        private static MemoryPackageSource TwoPackages(string version = "0.2.0")
        {
            return new MemoryPackageSource()
                .Add(SmallName, SyntheticPackage.Zip(SmallFolder, version))
                .Add(WideName, SyntheticPackage.Zip("openDash", version));
        }

        private DashboardInstaller Installer(IPackageSource packages, IInstallLog log = null, IFolderRecord record = null)
        {
            return new DashboardInstaller(root, log, packages, record);
        }

        // Somebody's Dash Studio work, and whether an install destroys it

        /// <summary>
        /// The first run after this shipped must not hold everything back. No record means openDash has never looked
        /// at the folder, not that it was edited, so the folder is adopted and watched from then on.
        /// </summary>
        [Fact]
        public void A_folder_with_nothing_remembered_is_adopted_rather_than_held_back()
        {
            var record = new MemoryFolderRecord();
            PackageExtractor.Install(SyntheticPackage.Zip("openDash", "0.1.0"), root, null);
            Assert.Empty(record.Entries);

            var installer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("openDash", "0.2.0")), record: record);
            installer.EnsureInstalled(false);

            var entry = installer.Packages.Single();
            Assert.False(entry.Edited);
            Assert.False(entry.HeldBack);
            Assert.True(entry.Extracted);
            Assert.Equal("0.2.0", entry.InstalledVersion);
            Assert.NotNull(record.Get("openDash"));
        }

        /// <summary>
        /// The case XOR-118 creates, and the one it says can hurt somebody.
        ///
        /// A user on rc.2 has DashTemplates/openDash holding the twelve-slot card face, and their settings carry no
        /// fingerprint for it, because fingerprints only began in rc.3. The rename puts the zone face under that
        /// name, so the update replaces their dashboard with a different design rather than a newer version of the
        /// same one. Nothing can ask them first, since no record exists to tell an edit from an untouched folder, so
        /// the whole of the protection is that a copy is kept.
        /// </summary>
        [Fact]
        public void A_folder_replaced_under_a_name_it_did_not_have_before_is_still_copied_first()
        {
            var record = new MemoryFolderRecord();
            PackageExtractor.Install(SyntheticPackage.Zip("openDash", "0.1.0-rc.2"), root, null);
            var theirs = Path.Combine(root, "DashTemplates", "openDash", "openDash.djson");
            File.WriteAllText(theirs, "{\"theirs\":true}");
            Assert.Empty(record.Entries);

            var installer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("openDash", "0.2.0-rc.1")), record: record);
            installer.EnsureInstalled(false);

            var entry = installer.Packages.Single();
            Assert.True(entry.Extracted);
            Assert.Equal("0.2.0-rc.1", entry.InstalledVersion);

            // The copy exists, holds what they had, and is named so that a person can find it.
            var backup = Path.Combine(root, "DashTemplates", "openDash" + PackageExtractor.BackupSuffix);
            Assert.Equal(backup, entry.KeptCopy);
            Assert.True(File.Exists(backup));
            using (var zip = ZipFile.OpenRead(backup))
            {
                var djson = zip.Entries.Single(e => e.FullName.EndsWith("openDash.djson", StringComparison.Ordinal));
                using (var reader = new StreamReader(djson.Open()))
                {
                    Assert.Equal("{\"theirs\":true}", reader.ReadToEnd());
                }
            }
            // And what is installed now is the new one, not what was copied away.
            Assert.NotEqual("{\"theirs\":true}", File.ReadAllText(theirs));
        }

        [Fact]
        public void A_folder_that_is_current_is_still_adopted_so_the_next_run_can_tell()
        {
            var record = new MemoryFolderRecord();
            PackageExtractor.Install(SyntheticPackage.Zip("openDash", "0.2.0"), root, null);

            var installer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("openDash", "0.2.0")), record: record);
            installer.EnsureInstalled(false);

            Assert.False(installer.Packages.Single().Extracted);
            Assert.NotNull(record.Get("openDash"));
        }

        [Fact]
        public void A_folder_edited_after_openDash_wrote_it_is_left_alone_and_said_so()
        {
            var record = new MemoryFolderRecord();
            var installer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("openDash", "0.1.0")), record: record);
            installer.EnsureInstalled(false);
            Assert.NotNull(record.Get("openDash"));

            // What Dash Studio does: it rewrites the dashboard in place.
            var djson = Path.Combine(root, "DashTemplates", "openDash", "openDash.djson");
            File.WriteAllText(djson, "{\"Version\":2,\"mine\":true}");

            var log = new ListLog();
            var newer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("openDash", "0.2.0")), log, record);
            newer.EnsureInstalled(false);

            var entry = newer.Packages.Single();
            Assert.True(entry.Edited);
            Assert.True(entry.HeldBack);
            Assert.False(entry.Extracted);
            Assert.Equal("0.1.0", entry.InstalledVersion);
            Assert.Equal("{\"Version\":2,\"mine\":true}", File.ReadAllText(djson));
            Assert.Contains(log.Lines, line => line.Contains("has changed since openDash wrote it"));
        }

        /// <summary>
        /// A folder that was refused is not up to date. Reporting it as such is how Reinstall came to look as
        /// though it had worked while doing nothing at all.
        /// </summary>
        [Fact]
        public void A_folder_left_alone_says_so_rather_than_reporting_up_to_date()
        {
            var record = new MemoryFolderRecord();
            Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("openDash", "0.1.0")), record: record).EnsureInstalled(false);
            File.WriteAllText(Path.Combine(root, "DashTemplates", "openDash", "openDash.djson"), "{\"mine\":true}");

            var installer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("openDash", "0.2.0")), record: record);
            installer.EnsureInstalled(false);

            var described = installer.Packages.Single().Describe();
            Assert.Contains("you have edited this one", described);
            Assert.DoesNotContain("Up to date (", described.Replace("you have edited this one, so it was left alone", ""));
        }

        [Fact]
        public void An_edited_folder_is_replaced_when_a_person_says_so()
        {
            var record = new MemoryFolderRecord();
            Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("openDash", "0.1.0")), record: record).EnsureInstalled(false);
            var djson = Path.Combine(root, "DashTemplates", "openDash", "openDash.djson");
            File.WriteAllText(djson, "{\"Version\":2,\"mine\":true}");

            var installer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("openDash", "0.2.0")), record: record);
            installer.EnsureInstalled(false, replaceEdited: true);

            var entry = installer.Packages.Single();
            Assert.True(entry.Extracted);
            Assert.False(entry.HeldBack);
            Assert.False(entry.Edited);
            Assert.Equal("0.2.0", entry.InstalledVersion);
            // And what was there is recoverable, because Install kept it.
            Assert.True(PackageExtractor.Restore(root, "openDash", null));
            Assert.Equal("{\"Version\":2,\"mine\":true}", File.ReadAllText(djson));
        }

        // The install run

        [Fact]
        public void Every_embedded_package_is_installed_and_the_primary_one_names_the_version()
        {
            var log = new ListLog();
            var installer = Installer(TwoPackages(), log);
            Assert.Equal(2, installer.PackageCount);
            Assert.True(installer.HasEmbeddedPackage);

            installer.EnsureInstalled(false);

            Assert.Equal(InstallStatus.UpToDate, installer.Status);
            Assert.Null(installer.LastError);
            Assert.True(PackageExtractor.IsInstalled(root, "openDash"));
            Assert.True(PackageExtractor.IsInstalled(root, SmallFolder));
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, SmallFolder));

            Assert.Equal(new[] { SmallFolder, "openDash" }, installer.Packages.Select(p => p.FolderName));
            Assert.All(installer.Packages, p => Assert.Equal(InstallStatus.UpToDate, p.Status));
            Assert.All(installer.Packages, p => Assert.True(p.Extracted));
            Assert.All(installer.Packages, p => Assert.Null(p.Error));

            Assert.Equal("openDash", installer.FolderName);
            Assert.Equal("0.2.0", installer.InstalledVersion);
            Assert.Equal("0.2.0", installer.EmbeddedVersion);
            Assert.Equal("openDash 0.2.0 · 2 dashboards", DashboardInstaller.Summary(installer.InstalledVersion, installer.PackageCount));

            // Both packages carry the same fonts; the second install finds them in DashFonts already.
            Assert.Equal(2, Directory.GetFiles(Path.Combine(root, "DashFonts"), "*.ttf").Length);
            Assert.Contains("info: Installed openDash 1280x480 0.2.0 into " + PackageExtractor.InstalledFolder(root, SmallFolder), log.Lines);
            Assert.Contains("info: Installed openDash 0.2.0 into " + PackageExtractor.InstalledFolder(root, "openDash"), log.Lines);
        }

        [Fact]
        public void Only_the_packages_that_need_it_are_installed_unless_forced()
        {
            using (var package = SyntheticPackage.Zip(SmallFolder, "0.2.0")) PackageExtractor.Install(package, root, null);
            var installer = Installer(TwoPackages());

            installer.EnsureInstalled(false);

            Assert.Equal(InstallStatus.UpToDate, installer.Status);
            Assert.False(installer.Packages.Single(p => p.FolderName == SmallFolder).Extracted);
            Assert.True(installer.Packages.Single(p => p.FolderName == "openDash").Extracted);
            Assert.False(File.Exists(Path.Combine(root, "DashTemplates", SmallFolder + PackageExtractor.BackupSuffix)));

            installer.EnsureInstalled(true);

            Assert.Equal(InstallStatus.UpToDate, installer.Status);
            Assert.All(installer.Packages, p => Assert.True(p.Extracted));
            Assert.True(File.Exists(Path.Combine(root, "DashTemplates", SmallFolder + PackageExtractor.BackupSuffix)));
            Assert.True(File.Exists(Path.Combine(root, "DashTemplates", "openDash" + PackageExtractor.BackupSuffix)));
        }

        [Fact]
        public void An_older_installed_copy_is_updated_and_a_newer_one_left_alone()
        {
            using (var package = SyntheticPackage.Zip("openDash", "0.1.0")) PackageExtractor.Install(package, root, null);
            using (var package = SyntheticPackage.Zip(SmallFolder, "0.3.0")) PackageExtractor.Install(package, root, null);
            var installer = Installer(TwoPackages("0.2.0"));

            installer.Refresh();
            Assert.Equal(InstallStatus.UpdateAvailable, installer.Status);
            Assert.Equal("0.1.0", installer.InstalledVersion);
            Assert.Equal("0.2.0", installer.EmbeddedVersion);

            installer.EnsureInstalled(false);

            Assert.Equal(InstallStatus.UpToDate, installer.Status);
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "openDash"));
            Assert.Equal("0.3.0", PackageExtractor.ReadInstalledVersion(root, SmallFolder));
            Assert.Equal("0.2.0", installer.InstalledVersion);
        }

        [Fact]
        public void Refresh_reports_without_touching_the_disk()
        {
            var installer = Installer(TwoPackages());

            installer.Refresh();

            Assert.Equal(InstallStatus.NotInstalled, installer.Status);
            Assert.Equal(2, installer.Packages.Count);
            Assert.All(installer.Packages, p => Assert.Equal(InstallStatus.NotInstalled, p.Status));
            Assert.All(installer.Packages, p => Assert.Null(p.InstalledVersion));
            Assert.Null(installer.InstalledVersion);
            Assert.Equal("0.2.0", installer.EmbeddedVersion);
            Assert.False(Directory.Exists(Path.Combine(root, "DashTemplates")));
        }

        // Status aggregation

        [Fact]
        public void The_worst_status_wins_and_a_broken_package_does_not_stop_the_others()
        {
            var log = new ListLog();
            var packages = new MemoryPackageSource()
                .Add("OpenDashPlugin.Resources.broken.simhubdash", "this is not a zip")
                .Add(WideName, SyntheticPackage.Zip("openDash", "0.2.0"));
            var installer = Installer(packages, log);

            installer.EnsureInstalled(false);

            Assert.Equal(InstallStatus.Failed, installer.Status);
            Assert.NotNull(installer.LastError);
            Assert.True(PackageExtractor.IsInstalled(root, "openDash"));

            var broken = installer.Packages[0];
            Assert.Equal(InstallStatus.Failed, broken.Status);
            Assert.Null(broken.FolderName);
            Assert.Equal(installer.LastError, broken.Error);
            Assert.False(broken.Extracted);
            Assert.StartsWith("OpenDashPlugin.Resources.broken.simhubdash: Install failed (", broken.Describe());

            Assert.Equal(InstallStatus.UpToDate, installer.Packages[1].Status);
            Assert.Equal("openDash: Up to date", installer.Packages[1].Describe());

            // The primary package still names the version even though another one failed.
            Assert.Equal("openDash", installer.FolderName);
            Assert.Equal("0.2.0", installer.InstalledVersion);
            Assert.Equal(broken.Describe() + "\n" + "openDash: Up to date", installer.PackageReport());
            Assert.Contains(log.Lines, line => line.StartsWith("error: Installing OpenDashPlugin.Resources.broken.simhubdash failed"));
        }

        [Fact]
        public void A_zip_without_a_dashboard_is_a_failed_package()
        {
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) SyntheticPackage.Add(zip, "readme.txt", "hi");
            var installer = Installer(new MemoryPackageSource().Add("OpenDashPlugin.Resources.empty.simhubdash", stream));

            installer.EnsureInstalled(false);

            Assert.Equal(InstallStatus.Failed, installer.Status);
            Assert.Contains("has no <folder>/<folder>.djson entry", installer.LastError);
        }

        [Fact]
        public void Not_installed_outranks_update_available()
        {
            using (var package = SyntheticPackage.Zip("openDash", "0.1.0")) PackageExtractor.Install(package, root, null);
            var installer = Installer(TwoPackages("0.2.0"));

            installer.Refresh();

            Assert.Equal(InstallStatus.NotInstalled, installer.Status);
            Assert.Equal(InstallStatus.NotInstalled, installer.Packages.Single(p => p.FolderName == SmallFolder).Status);
            Assert.Equal(InstallStatus.UpdateAvailable, installer.Packages.Single(p => p.FolderName == "openDash").Status);
        }

        [Fact]
        public void Without_a_primary_package_the_first_one_names_the_version()
        {
            var installer = Installer(new MemoryPackageSource().Add(SmallName, SyntheticPackage.Zip(SmallFolder, "0.2.0")));

            installer.EnsureInstalled(false);

            Assert.Equal(SmallFolder, installer.FolderName);
            Assert.Equal("0.2.0", installer.InstalledVersion);
            Assert.Equal("openDash 0.2.0 · 1 dashboard", DashboardInstaller.Summary(installer.InstalledVersion, installer.PackageCount));
        }

        [Fact]
        public void A_build_without_packages_installs_nothing_and_reports_what_is_there()
        {
            var log = new ListLog();
            var installer = Installer(new MemoryPackageSource(), log);
            Assert.False(installer.HasEmbeddedPackage);

            installer.EnsureInstalled(true);

            Assert.Equal(InstallStatus.NotInstalled, installer.Status);
            Assert.Empty(installer.Packages);
            Assert.Null(installer.EmbeddedVersion);
            Assert.False(Directory.Exists(Path.Combine(root, "DashTemplates")));
            Assert.Contains(log.Lines, line => line.StartsWith("warn: No .simhubdash is embedded"));

            using (var package = SyntheticPackage.Zip("openDash", "0.1.0")) PackageExtractor.Install(package, root, null);
            installer.Refresh();
            Assert.Equal(InstallStatus.UpToDate, installer.Status);
            Assert.Equal("0.1.0", installer.InstalledVersion);
        }

        // The panel's texts

        [Theory]
        [InlineData("0.1.0", 10, "openDash 0.1.0 · 10 dashboards")]
        [InlineData("0.1.0", 1, "openDash 0.1.0 · 1 dashboard")]
        [InlineData("0.1.0", 0, "openDash 0.1.0")]
        [InlineData("(unknown version)", 2, "openDash (unknown version) · 2 dashboards")]
        public void Summary_names_the_version_and_counts_the_dashboards(string version, int count, string expected)
        {
            Assert.Equal(expected, DashboardInstaller.Summary(version, count));
        }

        [Theory]
        [InlineData(InstallStatus.UpToDate, "Up to date")]
        [InlineData(InstallStatus.UpdateAvailable, "Update available")]
        [InlineData(InstallStatus.Failed, "Install failed")]
        [InlineData(InstallStatus.NotInstalled, "Not installed")]
        public void Status_labels_are_the_panel_texts(InstallStatus status, string label)
        {
            Assert.Equal(label, status.Label());
        }

        // Embedded resources

        [Fact]
        public void MSBuild_keeps_the_spaces_of_an_embedded_file_name()
        {
            // Fixtures/resource name with spaces.txt, embedded by OpenDash.Tests.csproj exactly as the plugin embeds
            // Resources/*.simhubdash: root namespace, folder, then the file name untouched.
            var assembly = typeof(DashboardInstallerTests).Assembly;
            var names = AssemblyPackageSource.ResourceNames(assembly, ".txt");
            Assert.Equal(new[] { "OpenDashPlugin.Tests.Fixtures.resource name with spaces.txt" }, names);

            using (var reader = new StreamReader(new AssemblyPackageSource(assembly).Open(names[0])))
            {
                Assert.StartsWith("Embedded by OpenDash.Tests.csproj.", reader.ReadToEnd());
            }
        }

        [Fact]
        public void The_test_assembly_embeds_no_package_and_a_missing_name_is_an_error()
        {
            var source = new AssemblyPackageSource(typeof(DashboardInstallerTests).Assembly);
            Assert.Empty(source.Names);
            Assert.Throws<FileNotFoundException>(() => source.Open("OpenDashPlugin.Resources.openDash 1280x480.simhubdash"));
        }

        // The pure interpretation of a folder's files

        [Theory]
        [InlineData(null)]
        [InlineData(Sidecar)]
        public void Folder_absent_is_not_installed_whatever_the_sidecar_says(string sidecar)
        {
            Assert.Null(DashboardInstaller.InstalledVersionFrom(false, sidecar));
            Assert.Equal(InstallStatus.NotInstalled, Versioning.Decide(DashboardInstaller.InstalledVersionFrom(false, sidecar), "0.1.0"));
        }

        [Fact]
        public void Sidecar_with_a_version_is_the_installed_version()
        {
            Assert.Equal("0.1.0", DashboardInstaller.InstalledVersionFrom(true, Sidecar));
            Assert.Equal(InstallStatus.UpToDate, Versioning.Decide(DashboardInstaller.InstalledVersionFrom(true, Sidecar), "0.1.0"));
            Assert.Equal(InstallStatus.UpdateAvailable, Versioning.Decide(DashboardInstaller.InstalledVersionFrom(true, Sidecar), "0.2.0"));
        }

        [Theory]
        [InlineData(null)] // sidecar file missing
        [InlineData("")]
        [InlineData("{\"Title\":\"openDash\",\"Width\":1920}")] // no DashboardVersion
        [InlineData("{\"DashboardVersion\":\"\"}")]
        [InlineData("{ this is not json")]
        [InlineData("{\"DashboardVersion\": 0.1.0}")] // unquoted: not the shape the generator writes
        [InlineData("\0ÿ binary junk")]
        public void Sidecar_without_a_usable_version_is_older_than_any_embedded_version(string sidecar)
        {
            var installed = DashboardInstaller.InstalledVersionFrom(true, sidecar);
            Assert.Equal(Versioning.UnknownVersion, installed);
            Assert.Equal(InstallStatus.UpdateAvailable, Versioning.Decide(installed, "0.1.0"));
            Assert.Equal(InstallStatus.UpdateAvailable, Versioning.Decide(installed, "0.0.0"));
            Assert.Equal(InstallStatus.UpdateAvailable, Versioning.Decide(installed, "0.0.0-rc1"));
            Assert.True(Versioning.NeedsInstall(Versioning.Decide(installed, "0.1.0")));
        }

        [Fact]
        public void Unknown_installed_version_with_nothing_embedded_is_left_alone()
        {
            var installed = DashboardInstaller.InstalledVersionFrom(true, null);
            Assert.Equal(InstallStatus.UpToDate, Versioning.Decide(installed, null));
        }
    }
}
