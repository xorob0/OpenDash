// ThemedScreenInstallTests.cs: a themed screen through the installer (#198), in the style of DashboardInstallerTests
// and UpdatePlanTests. A plugin update brings a themed screen the driver added to the new version at the next start,
// exactly as it brings a default one, and a themed package nobody added stays unwritten; and a themed screen whose
// package a later build no longer carries keeps its folder rather than being written from the default package of
// its size, which is the path ADR 0016 says the plugin must close.
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class ThemedScreenInstallTests : IDisposable
    {
        private const string DefaultName = "OpenDashPlugin.Resources.OpenDash 1280x480.simhubdash";
        private const string DefaultFolder = "OpenDash 1280x480";
        private const string ThemedName = "OpenDashPlugin.Resources.OpenDash Porsche 1280x480.simhubdash";
        private const string ThemedFolder = "OpenDash Porsche 1280x480";
        private const string OtherThemedName = "OpenDashPlugin.Resources.OpenDash Porsche 1920x480.simhubdash";
        private const string OtherThemedFolder = "OpenDash Porsche 1920x480";

        private readonly string root;

        public ThemedScreenInstallTests()
        {
            root = Path.Combine(Path.GetTempPath(), "opendash-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            try { Directory.Delete(root, true); } catch { }
        }

        private static MemoryPackageSource Build(string version, bool carriesTheme = true)
        {
            var packages = new MemoryPackageSource().Add(DefaultName, SyntheticPackage.Zip(DefaultFolder, version));
            if (carriesTheme)
            {
                packages.Add(ThemedName, SyntheticPackage.Zip(ThemedFolder, version));
                packages.Add(OtherThemedName, SyntheticPackage.Zip(OtherThemedFolder, version));
            }
            return packages;
        }

        /// <summary>The screen the Add sheet makes when the Porsche is picked at 1280 x 480 on an empty rig.</summary>
        private static ScreenInstance Porsche(IPackageSource packages)
        {
            var entry = PackageCatalogue.From(packages).Single(e => e.Folder == ThemedFolder);
            return PackageCatalogue.NewScreen(entry, "Porsche 1280 × 480", new string[0]);
        }

        [Fact]
        public void A_themed_screen_is_made_from_its_package_and_remembers_its_theme()
        {
            var screen = Porsche(Build("0.3.0"));
            Assert.Equal("porsche", screen.Theme);
            Assert.Equal(ThemedName, screen.Package);
            Assert.Equal(ThemedFolder, screen.Folder);
            Assert.Equal("Face1280x480", screen.Namespace);
        }

        /// <summary>
        /// The update path: the plugin carrying a newer version starts, and the themed screen on the rig is written
        /// again at that version, while the themed package nobody added is still not written.
        /// </summary>
        [Fact]
        public void An_update_rewrites_the_themed_screen_the_driver_added_and_no_other_theme()
        {
            var record = new MemoryFolderRecord();
            var rig = new[] { Porsche(Build("0.3.0")) };
            new DashboardInstaller(root, null, Build("0.3.0"), record) { Rig = () => rig }.EnsureInstalled(false);
            Assert.Equal("0.3.0", PackageExtractor.ReadInstalledVersion(root, ThemedFolder));

            var updated = new DashboardInstaller(root, null, Build("0.4.0"), record) { Rig = () => rig };
            updated.EnsureInstalled(false);

            Assert.Equal("0.4.0", PackageExtractor.ReadInstalledVersion(root, ThemedFolder));
            Assert.False(Directory.Exists(PackageExtractor.InstalledFolder(root, OtherThemedFolder)));
            Assert.False(Directory.Exists(PackageExtractor.InstalledFolder(root, DefaultFolder)));
            Assert.True(updated.Packages.Single(p => p.FolderName == OtherThemedFolder).OutsideRig);
            Assert.Equal(InstallStatus.UpToDate, updated.Status);
        }

        /// <summary>
        /// ADR 0016's rule: a themed screen whose package the build no longer carries keeps its folder untouched, and
        /// is never written from the default package of its size.
        /// </summary>
        [Fact]
        public void A_themed_screen_whose_package_is_gone_keeps_its_folder_and_is_not_written_from_the_default()
        {
            var record = new MemoryFolderRecord();
            var rig = new[] { Porsche(Build("0.3.0")) };
            new DashboardInstaller(root, null, Build("0.3.0"), record) { Rig = () => rig }.EnsureInstalled(false);

            var later = Build("0.4.0", carriesTheme: false);
            Assert.Null(ScreenInstaller.PackageNameFor(rig[0], later, null));
            Assert.Null(PackageCatalogue.EntryFor(PackageCatalogue.From(later), rig[0]));
            var installer = new DashboardInstaller(root, null, later, record) { Rig = () => rig };
            installer.EnsureInstalled(false);
            Assert.Equal("0.3.0", PackageExtractor.ReadInstalledVersion(root, ThemedFolder));
            Assert.False(Directory.Exists(PackageExtractor.InstalledFolder(root, DefaultFolder)));

            // And the panel's own write, a reinstall, refuses rather than writing the default in.
            var write = installer.Write(rig[0]);
            Assert.False(write.Ok);
            Assert.Equal("0.3.0", PackageExtractor.ReadInstalledVersion(root, ThemedFolder));
        }

        /// <summary>A themed screen whose resource was renamed still finds its own theme's package by its folder, as a
        /// default screen does; it is only ever matched within its theme.</summary>
        [Fact]
        public void A_themed_screen_falls_back_within_its_theme_and_a_default_screen_within_the_default()
        {
            var catalogue = PackageCatalogue.From(Build("0.3.0"));
            var themed = Porsche(Build("0.3.0"));
            themed.Package = "OpenDashPlugin.Resources.renamed.simhubdash";
            Assert.Equal(ThemedName, PackageCatalogue.EntryFor(catalogue, themed).Package);

            // A default screen at 1280 x 480 that remembers no package, and a second one whose folder is its own: both
            // find the default package, never the themed one beside it.
            var stock = new ScreenInstance { Kind = Contract.KindFace, Width = 1280, Height = 480, Folder = DefaultFolder, Namespace = "Face1280x480" };
            var second = new ScreenInstance { Kind = Contract.KindFace, Width = 1280, Height = 480, Folder = "OpenDash Rim", Namespace = "Rim" };
            var themedFirst = catalogue.OrderByDescending(e => e.Theme != null).ToList();
            Assert.Equal(DefaultName, PackageCatalogue.EntryFor(themedFirst, stock).Package);
            Assert.Equal(DefaultName, PackageCatalogue.EntryFor(themedFirst, second).Package);
        }
    }
}
