// ScreenInstallerTests.cs: the folder one screen owns -- how it is spelled, and, mostly, the repair that puts
// the driver's name for it back into SimHub's dashboard list after an install has handed SimHub the package's.
//
// Reported from a rig: after an update, the screens were listed under names their owner had never
// chosen, which from the outside is indistinguishable from the dashboards having disappeared.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

        /// <summary>
        /// A folder SimHub holds a file of is not removed at all, rather than part of the way (#861): the remove fails
        /// with the dashboard whole, so the panel can keep the screen that owns it.
        /// </summary>
        [Fact]
        public void A_held_folder_is_left_whole_and_its_remove_reported_as_failed()
        {
            var record = new MemoryFolderRecord();
            InstallStock(record);
            var folder = PackageExtractor.InstalledFolder(root, Folder);
            var disk = RefusingDisk.Holding(folder);

            var result = ScreenInstaller.Remove(Screen("Main dash"), root, record, null, disk);

            Assert.False(result.Ok);
            Assert.False(result.Written);
            Assert.True(disk.Refused > 0, "the held folder was reached");
            Assert.Equal("1.0.0", PackageExtractor.ReadInstalledVersion(root, Folder));
            Assert.Empty(PackageExtractor.KeptCopies(root, Folder));
            Assert.Equal(new[] { Folder }, Directory.GetDirectories(Path.Combine(root, PackageExtractor.DashTemplates)).Select(Path.GetFileName));
        }

        /// <summary>
        /// Removing a screen whose dashboard somebody edited in Dash Studio used to delete the edit with it (#861). A copy
        /// is kept first, under a name no install reclaims, as a reinstall over the edit keeps one.
        /// </summary>
        [Fact]
        public void An_edited_dashboard_is_kept_as_a_copy_when_its_screen_is_removed()
        {
            var record = new MemoryFolderRecord();
            InstallStock(record);
            var main = Path.Combine(PackageExtractor.InstalledFolder(root, Folder), Folder + ".djson");
            File.WriteAllText(main, "{\"Version\":2,\"Title\":\"Mine\",\"Mine\":true}");

            var result = ScreenInstaller.Remove(Screen("Main dash"), root, record, null);

            Assert.True(result.Ok, result.Error);
            Assert.True(result.Written);
            Assert.False(Directory.Exists(PackageExtractor.InstalledFolder(root, Folder)));
            Assert.NotNull(result.KeptCopy);
            Assert.Contains(PackageExtractor.EditedSuffix, result.KeptCopy);
            using (var zip = ZipFile.OpenRead(result.KeptCopy))
            using (var reader = new StreamReader(zip.GetEntry(Folder + ".djson").Open()))
            {
                Assert.Contains("\"Mine\":true", reader.ReadToEnd());
            }
            Assert.Empty(Directory.GetDirectories(Path.Combine(root, PackageExtractor.DashTemplates)));
        }

        /// <summary>A dashboard nobody touched is OpenDash's own and goes without a copy, as it would at a reinstall
        /// (which keeps only the ordinary backup the next install reclaims).</summary>
        [Fact]
        public void An_untouched_dashboard_is_removed_without_a_copy()
        {
            var record = new MemoryFolderRecord();
            InstallStock(record);

            var result = ScreenInstaller.Remove(Screen("Main dash"), root, record, null);

            Assert.True(result.Ok, result.Error);
            Assert.Null(result.KeptCopy);
            Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(root, PackageExtractor.DashTemplates)));
            // And a folder already gone is a remove that worked, with nothing written.
            var again = ScreenInstaller.Remove(Screen("Main dash"), root, record, null);
            Assert.True(again.Ok);
            Assert.False(again.Written);
        }

        /// <summary>
        /// The panel takes a screen off the rig before it removes the folder, and puts it back where it was when the
        /// folder stayed (#861), with everything set on it, since RemoveScreen dropped nothing else.
        /// </summary>
        [Fact]
        public void A_screen_whose_folder_stayed_goes_back_where_it_was_on_the_rig()
        {
            var settings = new OpenDashSettings();
            var catalogue = new[]
            {
                new PackageEntry { Package = "a", Folder = "OpenDash 1280x480", Kind = Contract.KindFace, Width = 1280, Height = 480 },
                new PackageEntry { Package = "b", Folder = "OpenDash 850x480", Kind = Contract.KindFace, Width = 850, Height = 480 },
            };
            var first = settings.AddScreen(catalogue[0], "First");
            var second = settings.AddScreen(catalogue[1], "Second");
            var third = settings.AddScreen(catalogue[0], "Third");

            var at = settings.Rig.IndexOf(second);
            Assert.True(settings.RemoveScreen(second.Namespace));
            settings.PutScreenBack(second, at);
            Assert.Equal(new[] { first, second, third }, settings.Rig);

            // Put back once only, whatever happened meanwhile.
            settings.PutScreenBack(second, 0);
            Assert.Equal(3, settings.Rig.Count);
            settings.RemoveScreen(third.Namespace);
            settings.PutScreenBack(third, 99);
            Assert.Same(third, settings.Rig.Last());
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

        /// <summary>
        /// A screen takes its package's spelling of the folder it holds, and of no other folder (#467).
        /// </summary>
        /// <remarks>
        /// The second screen is the half that must not move. Its folder is its own rather than a package's, a rig
        /// that made one before #374 spells it "openDash Rim", and SimHub reopens it under that spelling.
        /// </remarks>
        [Fact]
        public void A_screen_takes_its_packages_spelling_of_its_own_folder_and_of_no_other()
        {
            var wheel = new ScreenInstance { Kind = Contract.KindFace, Width = 850, Height = 480, Namespace = "Face850x480", Folder = "openDash 850x480" };
            Assert.True(wheel.SpellFolderAs("OpenDash 850x480"));
            Assert.Equal("OpenDash 850x480", wheel.Folder);
            Assert.Equal("Face850x480", wheel.Namespace);
            Assert.False(wheel.SpellFolderAs("OpenDash 850x480"));

            var rim = new ScreenInstance { Kind = Contract.KindFace, Width = 850, Height = 480, Namespace = "Rim", Folder = "openDash Rim" };
            Assert.False(rim.SpellFolderAs("OpenDash 850x480"));
            Assert.Equal("openDash Rim", rim.Folder);
            Assert.Equal("Rim", rim.Namespace);

            // Nor does a card face's namespace, which comes from its kind and its size rather than its folder (#474),
            // whether it is the one its size spells or one a migration took from the folder when no size was known.
            var round = new ScreenInstance { Kind = Contract.KindSlots, Width = 480, Height = 480, Folder = "openDash 480 round" };
            round.Namespace = round.StockNamespace;
            Assert.True(round.SpellFolderAs("OpenDash 480 round"));
            Assert.Equal("Slots480x480", round.Namespace);
            Assert.True(round.IsStock);
            var migrated = new ScreenInstance { Kind = Contract.KindSlots, Width = 480, Height = 480, Namespace = "SlotsopenDash480Round", Folder = "openDash 480 round" };
            Assert.True(migrated.SpellFolderAs("OpenDash 480 round"));
            Assert.Equal("OpenDash 480 round", migrated.Folder);
            Assert.Equal("SlotsopenDash480Round", migrated.Namespace);

            Assert.False(new ScreenInstance { Folder = null }.SpellFolderAs("OpenDash 850x480"));
            Assert.False(wheel.SpellFolderAs(null));
        }

        /// <summary>
        /// A screen added from the Rig tab brings its package's faces with it, onto a SimHub that has none of them.
        /// </summary>
        /// <remarks>
        /// #441 asked whether this path, which writes every screen a rig adds and never asks SimHub to reload its
        /// fonts, was where a face went missing. It copies them, and says so (ScreenInstallResult.WroteFonts): a
        /// running SimHub does not draw a face written after it started, so the panel asks for a restart after an
        /// add that wrote one. It asks for none otherwise, since SimHub lists the new folder by itself (#664). The
        /// same face a stock screen already brought is not copied twice, and the second add says no font.
        /// </remarks>
        [Fact]
        public void A_screen_added_from_the_rig_tab_copies_the_faces_its_package_carries()
        {
            const string package = "OpenDash 1280x480.simhubdash";
            var zip = new MemoryStream();
            using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, true))
            {
                SyntheticPackage.Add(archive, Folder + "/" + Folder + ".djson", "{\"Version\":2,\"A\":\"isnull([OpenDash.Face1280x480ZoneA],0)\"}");
                SyntheticPackage.Add(archive, Folder + "/" + Folder + ".djson.metadata", "{\"Title\":\"" + Folder + "\",\"DashboardVersion\":\"1.0.0\"}");
                SyntheticPackage.Add(archive, Folder + "/_SHFonts/openDashDisplay-Bold.ttf", "font-bold");
                SyntheticPackage.Add(archive, Folder + "/_SHFonts/openDashDisplay-Light.ttf", "font-light");
            }
            zip.Position = 0;
            var packages = new MemoryPackageSource().Add(package, zip);
            var rim = new ScreenInstance
            {
                Kind = Contract.KindFace, Width = 1280, Height = 480, Folder = "OpenDash Rim", Name = "Rim", Namespace = "Rim", Package = package,
            };
            var fonts = Path.Combine(root, PackageExtractor.DashFonts);
            Assert.False(Directory.Exists(fonts));

            var installer = new DashboardInstaller(root, null, packages, new MemoryFolderRecord());
            var written = installer.Write(rim);

            Assert.True(written.Ok, written.Error);
            Assert.True(written.Written);
            Assert.True(written.WroteFonts);
            Assert.Equal("font-bold", File.ReadAllText(Path.Combine(fonts, "openDashDisplay-Bold.ttf")));
            Assert.Equal("font-light", File.ReadAllText(Path.Combine(fonts, "openDashDisplay-Light.ttf")));

            var second = new ScreenInstance
            {
                Kind = Contract.KindFace, Width = 1280, Height = 480, Folder = "OpenDash Pod", Name = "Pod", Namespace = "Pod", Package = package,
            };
            var again = installer.Write(second);
            Assert.True(again.Written);
            Assert.False(again.WroteFonts);
            Assert.Equal(2, Directory.GetFiles(fonts, "*.ttf").Length);

            // A face that has gone from DashFonts since is written again, and said again.
            File.Delete(Path.Combine(fonts, "openDashDisplay-Light.ttf"));
            Assert.True(installer.Write(rim).WroteFonts);
            Assert.False(installer.Write(rim).WroteFonts);
        }
    }
}
