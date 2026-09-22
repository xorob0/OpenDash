// ScreenInstallerTests.cs: the folder one screen owns -- and, mostly, the repair that puts the driver's
// name for it back into SimHub's dashboard list after an install has handed SimHub the package's.
//
// Reported from a rig: after an update, the screens were listed under names their owner had never
// chosen, which from the outside is indistinguishable from the dashboards having disappeared.
using System;
using System.IO;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class ScreenInstallerTests : IDisposable
    {
        private const string Folder = "OpenDash 1280x480";

        private readonly string root;

        public ScreenInstallerTests()
        {
            root = Path.Combine(Path.GetTempPath(), "opendash-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            try { Directory.Delete(root, true); } catch { }
        }

        /// <summary>The stock install: the package as it is embedded, with the title the design gave it.</summary>
        private void InstallStock(IFolderRecord record)
        {
            PackageExtractor.Install(SyntheticPackage.Zip(Folder, "1.0.0"), root, null);
            record.Set(Folder, FolderFingerprint.Of(PackageExtractor.InstalledFolder(root, Folder)));
        }

        private string Metadata()
        {
            return File.ReadAllText(Path.Combine(PackageExtractor.InstalledFolder(root, Folder), Folder + ".djson.metadata"));
        }

        private static ScreenInstance Screen(string name)
        {
            return new ScreenInstance { Kind = Contract.KindFace, Width = 1280, Height = 480, Folder = Folder, Name = name };
        }

        /// <summary>
        /// A screen the driver renamed is listed under their name again, and the folder still reads as ours.
        /// </summary>
        /// <remarks>
        /// The second half is the half that is easy to forget. The fingerprint taken at install is of the
        /// package's title, so a folder retitled and not recorded again would read as somebody's own work
        /// from the next start on, and the update path would hold it back for ever rather than ask.
        /// </remarks>
        [Fact]
        public void A_renamed_screen_is_listed_under_its_own_name_again()
        {
            var record = new MemoryFolderRecord();
            InstallStock(record);
            Assert.Contains("\"Title\":\"" + Folder + "\"", Metadata());

            Assert.True(ScreenInstaller.Retitle(Screen("Main dash"), root, record, null));
            Assert.Contains("\"Title\":\"Main dash\"", Metadata());
            Assert.Equal(
                FolderFingerprint.Of(PackageExtractor.InstalledFolder(root, Folder)),
                record.Get(Folder));

            // Asked for again it writes nothing, which is what lets this run over every screen on every
            // start: no write, and so no folder to fingerprint a second time either.
            Assert.False(ScreenInstaller.Retitle(Screen("Main dash"), root, record, null));
        }

        /// <summary>
        /// A folder somebody has edited is left entirely alone.
        /// </summary>
        /// <remarks>
        /// Their own title is in there as likely as not, and editing one string of it and then recording
        /// the result as ours would turn "ask before replacing your work" into replacing it silently at
        /// the next update. Nothing is lost by standing back: the Edit panel's reinstall writes the whole
        /// folder, which is the press that says the driver meant it.
        /// </remarks>
        [Fact]
        public void A_dashboard_somebody_has_edited_is_not_retitled()
        {
            var record = new MemoryFolderRecord();
            InstallStock(record);
            var main = Path.Combine(PackageExtractor.InstalledFolder(root, Folder), Folder + ".djson");
            File.WriteAllText(main, "{\"Version\":2,\"Title\":\"Mine\",\"Mine\":true}");

            Assert.False(ScreenInstaller.Retitle(Screen("Main dash"), root, record, null));
            Assert.Contains("\"Title\":\"Mine\"", File.ReadAllText(main));
            Assert.Contains("\"Title\":\"" + Folder + "\"", Metadata());

            // And a rig with no record at all is the same case: no record is "we cannot vouch for this",
            // which is the bias PackageStatus.Edited already takes.
            Assert.False(ScreenInstaller.Retitle(Screen("Main dash"), root, new MemoryFolderRecord(), null));
            Assert.False(ScreenInstaller.Retitle(Screen("Main dash"), root, null, null));
        }

        /// <summary>Nothing to put a name into is not a failure to report, on any of the three ways a
        /// screen can have no folder on disk.</summary>
        [Fact]
        public void A_screen_with_no_folder_is_left_alone()
        {
            var record = new MemoryFolderRecord();
            InstallStock(record);
            Assert.False(ScreenInstaller.Retitle(null, root, record, null));
            Assert.False(ScreenInstaller.Retitle(new ScreenInstance { Folder = null, Name = "Rim" }, root, record, null));
            Assert.False(ScreenInstaller.Retitle(new ScreenInstance { Folder = "OpenDash Rim", Name = "Rim" }, root, record, null));
        }
    }
}
