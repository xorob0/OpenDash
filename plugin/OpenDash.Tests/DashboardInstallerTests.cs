// DashboardInstallerTests.cs: the installer against a temporary SimHub root and synthetic packages: every embedded
// package is installed, only the ones that need it unless forced, the worst status wins, a broken package does not stop
// the others, a second screen of a size is kept current and held back on the same terms as the first, a stock folder the
// settings spell in another case is written as its package spells it, a card face is written whether it is the first of
// its package or a second, with nothing rewritten, a leftover folder outside the rig is asked about by nothing, the
// panel's summary text, and the embedded resource naming (spaces in a file name survive). Also the pure
// InstalledVersionFrom: an absent folder is not installed, a folder without a usable sidecar is reinstalled.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class DashboardInstallerTests : IDisposable
    {
        private const string Sidecar = "{\"Title\":\"OpenDash\",\"DashboardVersion\":\"0.1.0\"}";

        // Resource names the way MSBuild embeds Resources/*.simhubdash: ordinal order puts the spaced name first.
        private const string WideName = "OpenDashPlugin.Resources.OpenDash.simhubdash";
        private const string SmallName = "OpenDashPlugin.Resources.OpenDash 1280x480.simhubdash";
        private const string SmallFolder = "OpenDash 1280x480";

        private readonly string root;
        private readonly DeniedPaths denied = new DeniedPaths();

        public DashboardInstallerTests()
        {
            root = Path.Combine(Path.GetTempPath(), "opendash-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            denied.Dispose();
            try { Directory.Delete(root, true); } catch { }
        }

        private static MemoryPackageSource TwoPackages(string version = "0.2.0")
        {
            return new MemoryPackageSource()
                .Add(SmallName, SyntheticPackage.Zip(SmallFolder, version))
                .Add(WideName, SyntheticPackage.Zip("OpenDash", version));
        }

        private DashboardInstaller Installer(IPackageSource packages, IInstallLog log = null, IFolderRecord record = null)
        {
            return new DashboardInstaller(root, log, packages, record);
        }

        /// <summary>The first screen of a size, which holds the package's own folder and namespace.</summary>
        private static ScreenInstance Stock(string folder, int width, int height, string name = null)
        {
            return new ScreenInstance
            {
                Kind = Contract.KindFace,
                Width = width,
                Height = height,
                Namespace = "Face" + width + "x" + height,
                Name = name ?? folder,
                Folder = folder,
            };
        }

        // What the "This plugin" pill says, which is a question about the rig and not about the build

        /// <summary>
        /// A size nobody added does not make the plugin "not installed".
        /// </summary>
        /// <remarks>
        /// Since ADR 0017 a screen exists because somebody added it, so a package outside the rig is
        /// never written -- and the pill aggregated over every package the build embeds, which meant it
        /// read NOT INSTALLED for ever on a rig whose every screen was installed and current. Seen on the
        /// test rig with the portrait sizes nobody owns.
        /// </remarks>
        [Fact]
        public void The_pill_ignores_a_package_the_rig_never_asked_for()
        {
            var installer = Installer(TwoPackages());
            installer.Rig = () => new[] { Stock(SmallFolder, 1280, 480) };
            installer.EnsureInstalled(force: true);

            Assert.Equal(InstallStatus.UpToDate, installer.Status);
            // The package outside the rig still reports itself honestly on its own row; it simply does
            // not speak for the rig.
            Assert.Equal(InstallStatus.NotInstalled, installer.Packages.Single(p => p.FolderName == "OpenDash").Status);
            Assert.Equal(InstallStatus.UpToDate, installer.Packages.Single(p => p.FolderName == SmallFolder).Status);
        }

        /// <summary>A screen the rig does want and has not got still turns the pill, which is the whole
        /// reason the pill exists.</summary>
        [Fact]
        public void The_pill_still_answers_for_a_screen_the_rig_wants()
        {
            var installer = Installer(TwoPackages());
            installer.Rig = () => new[] { Stock(SmallFolder, 1280, 480), Stock("OpenDash", 1920, 480) };
            installer.Refresh();
            Assert.Equal(InstallStatus.NotInstalled, installer.Status);
        }

        /// <summary>A new user has an empty rig: nothing installed and nothing outstanding, so the pill
        /// is not red at them before they have added anything.</summary>
        [Fact]
        public void An_empty_rig_has_nothing_outstanding()
        {
            var installer = Installer(TwoPackages());
            installer.Rig = () => new ScreenInstance[0];
            installer.EnsureInstalled(force: true);
            Assert.Equal(InstallStatus.UpToDate, installer.Status);
        }

        // A second screen of a size, which owns a folder no package carries (#455)

        private const string FaceName = "OpenDashPlugin.Resources.OpenDash 850x480.simhubdash";
        private const string FaceFolder = "OpenDash 850x480";
        private const string RimFolder = "OpenDash Rim (2)";

        /// <summary>The rig the ticket was measured on, as far as it matters here: the stock 850x480, and a second
        /// screen of that size with a namespace of its own.</summary>
        private static ScreenInstance[] TwoOfASize()
        {
            var rim = Stock(RimFolder, 850, 480, "Rim (2)");
            rim.Namespace = "Rim2";
            rim.Package = FaceName;
            var wheel = Stock(FaceFolder, 850, 480, "Tim wheel");
            wheel.Package = FaceName;
            return new[] { wheel, rim };
        }

        private static MemoryPackageSource Face(string version)
        {
            return new MemoryPackageSource().Add(FaceName, SyntheticPackage.Instanceable(FaceFolder, "Face850x480", version));
        }

        private DashboardInstaller OverRig(IPackageSource packages, IFolderRecord record, IReadOnlyList<ScreenInstance> rig, IInstallLog log = null)
        {
            return new DashboardInstaller(root, log, packages, record) { Rig = () => rig };
        }

        /// <summary>How many times a token appears across every dashboard file of an installed folder.</summary>
        private int Occurrences(string folder, string token)
        {
            var total = 0;
            foreach (var path in Directory.GetFiles(PackageExtractor.InstalledFolder(root, folder), "*.djson", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(path);
                for (var at = text.IndexOf(token, StringComparison.Ordinal); at >= 0; at = text.IndexOf(token, at + token.Length, StringComparison.Ordinal)) total++;
            }
            return total;
        }

        /// <summary>
        /// The ticket's own case: a plugin whose package is newer than a second screen's folder starts, and the
        /// folder is written again with its namespace, its title and a fingerprint of what was written.
        /// </summary>
        /// <remarks>
        /// Measured on the VM before this: the start replaced the stock 850x480 and left "OpenDash Rim (2)" at
        /// rc.7 without a word, because the installer walked the packages and only a missing folder was ever
        /// written for a second screen. The pill then read up to date over it.
        /// </remarks>
        [Fact]
        public void A_start_over_a_second_screen_of_a_size_at_an_older_version_writes_it_again()
        {
            var record = new MemoryFolderRecord();
            var rig = TwoOfASize();
            OverRig(Face("0.3.0-rc.7"), record, rig).EnsureInstalled(false);
            Assert.Equal("0.3.0-rc.7", PackageExtractor.ReadInstalledVersion(root, RimFolder));

            // Before the start writes anything, the pill and its tooltip already account for it.
            var installer = OverRig(Face("0.3.0-rc.8"), record, rig);
            installer.Refresh();
            Assert.Equal(InstallStatus.UpdateAvailable, installer.Status);
            Assert.Contains(RimFolder + ": Update available", installer.PackageReport());

            installer.EnsureInstalled(false);

            var rim = installer.Packages.Single(p => p.FolderName == RimFolder);
            Assert.True(rim.Extracted);
            Assert.Null(rim.Error);
            Assert.Equal("0.3.0-rc.8", rim.InstalledVersion);
            Assert.Equal(InstallStatus.UpToDate, installer.Status);
            Assert.Contains(RimFolder + ": Up to date", installer.PackageReport());
            Assert.Equal("0.3.0-rc.8", PackageExtractor.ReadInstalledVersion(root, FaceFolder));
            Assert.Equal("0.3.0-rc.8", PackageExtractor.ReadInstalledVersion(root, RimFolder));

            // Its own namespace as many times as the package names the stock one, and the stock one nowhere,
            // while the first screen keeps the package's own.
            var stock = Occurrences(FaceFolder, "OpenDash.Face850x480");
            Assert.True(stock > 0);
            Assert.Equal(0, Occurrences(RimFolder, "OpenDash.Face850x480"));
            Assert.Equal(stock, Occurrences(RimFolder, "OpenDash.Rim2"));

            // Listed in SimHub under each screen's own name, since the installer writes it rather than the package's.
            Assert.Contains("\"Title\":\"Rim (2)\"", File.ReadAllText(PackageExtractor.InstalledSidecar(root, RimFolder)));
            Assert.Contains("\"Title\":\"Tim wheel\"", File.ReadAllText(PackageExtractor.InstalledSidecar(root, FaceFolder)));

            // A copy of what it replaced, as for any folder, and a fingerprint of what it wrote, so that the next
            // start takes the folder for OpenDash's own and finds nothing to do.
            Assert.Equal(Path.Combine(root, "DashTemplates", RimFolder + PackageExtractor.BackupSuffix), rim.KeptCopy);
            Assert.Equal(FolderFingerprint.Of(PackageExtractor.InstalledFolder(root, RimFolder)), record.Get(RimFolder));
            var next = OverRig(Face("0.3.0-rc.8"), record, rig);
            next.EnsureInstalled(false);
            Assert.All(next.Packages, p => Assert.False(p.Extracted || p.Edited));
        }

        /// <summary>
        /// A second screen somebody has edited in Dash Studio is held back on the same terms as the first, and
        /// never reads as up to date while it is.
        /// </summary>
        [Fact]
        public void A_second_screen_somebody_edited_is_held_back_exactly_as_the_first_would_be()
        {
            var record = new MemoryFolderRecord();
            var rig = TwoOfASize();
            OverRig(Face("0.3.0-rc.7"), record, rig).EnsureInstalled(false);
            var theirs = Path.Combine(PackageExtractor.InstalledFolder(root, RimFolder), RimFolder + ".djson");
            File.WriteAllText(theirs, "{\"Version\":2,\"mine\":true}");

            var log = new ListLog();
            var installer = OverRig(Face("0.3.0-rc.8"), record, rig, log);
            installer.EnsureInstalled(false);

            var rim = installer.Packages.Single(p => p.FolderName == RimFolder);
            Assert.True(rim.Edited);
            Assert.True(rim.HeldBack);
            Assert.False(rim.Extracted);
            Assert.Equal("0.3.0-rc.7", rim.InstalledVersion);
            Assert.Equal("{\"Version\":2,\"mine\":true}", File.ReadAllText(theirs));
            Assert.Contains(log.Lines, line => line.Contains(RimFolder + " has changed since OpenDash wrote it"));
            // Holding one folder back holds back nothing else.
            Assert.True(installer.Packages.Single(p => p.FolderName == FaceFolder).Extracted);
            Assert.Equal(InstallStatus.UpdateAvailable, installer.Status);
            Assert.Contains(RimFolder + ": Update available (you have edited this one, so it was left alone)", installer.PackageReport());

            // A yes replaces it, and the copy of their work goes where no later install reclaims it.
            installer.EnsureInstalled(false, replaceEdited: true);
            rim = installer.Packages.Single(p => p.FolderName == RimFolder);
            Assert.True(rim.Extracted);
            Assert.Contains(PackageExtractor.EditedSuffix, rim.KeptCopy);
            Assert.Equal(0, Occurrences(RimFolder, "OpenDash.Face850x480"));
            Assert.Equal(InstallStatus.UpToDate, installer.Status);
        }

        /// <summary>
        /// The Rig tab's presses write through the same routine as a start: the folder with its namespace and its
        /// title, and the fingerprint of what was written, whatever version was there.
        /// </summary>
        /// <remarks>
        /// ADR 0017's warning, pinned where the panel now reaches it: a fingerprint of the embedded package rather
        /// than of the rewritten copy would make a second screen read as somebody's work from the next start on,
        /// and it would be held back from every update for ever.
        /// </remarks>
        [Fact]
        public void A_press_on_the_Rig_tab_writes_a_second_screen_through_the_same_routine()
        {
            var record = new MemoryFolderRecord();
            var rig = TwoOfASize();
            var installer = OverRig(Face("0.3.0-rc.8"), record, rig);

            var written = installer.Write(rig[1]);

            Assert.True(written.Ok);
            Assert.True(written.Written);
            Assert.Equal(0, Occurrences(RimFolder, "OpenDash.Face850x480"));
            Assert.True(Occurrences(RimFolder, "OpenDash.Rim2") > 0);
            Assert.Equal(FolderFingerprint.Of(PackageExtractor.InstalledFolder(root, RimFolder)), record.Get(RimFolder));

            // Written again although it is current, because a press is somebody asking.
            Assert.True(installer.Write(rig[1]).Written);

            installer.Refresh();
            Assert.False(installer.Packages.Single(p => p.FolderName == RimFolder).Edited);

            // And a screen whose size this build does not ship is said to have no package, not written blind.
            var gone = Stock("OpenDash Gone", 640, 480, "Gone");
            gone.Namespace = "Gone";
            Assert.Contains("ships no package", installer.Write(gone).Error);
        }

        // A stock folder the settings still spell as they did before #374 (#467)

        private const string RoundName = "OpenDashPlugin.Resources.OpenDash 480 round.simhubdash";
        private const string RoundFolder = "OpenDash 480 round";

        private static MemoryPackageSource FaceAndRound(string version)
        {
            return new MemoryPackageSource()
                .Add(FaceName, SyntheticPackage.Instanceable(FaceFolder, "Face850x480", version))
                .Add(RoundName, SyntheticPackage.Zip(RoundFolder, version));
        }

        /// <summary>The folders under DashTemplates as the filesystem spells them, which a lookup by name cannot tell
        /// where the filesystem ignores case.</summary>
        private string[] SpelledFolders()
        {
            return Directory.GetDirectories(Path.Combine(root, PackageExtractor.DashTemplates))
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        private bool HasDashboardSpelled(string folder)
        {
            return Directory.GetFiles(PackageExtractor.InstalledFolder(root, folder))
                .Select(Path.GetFileName)
                .Contains(folder + PackageExtractor.DashExtension, StringComparer.Ordinal);
        }

        /// <summary>
        /// The ticket's rig: settings written before #374 spell the stock folders "openDash", while the packages, the
        /// folders on disk and the dashboard SimHub reopens spell them "OpenDash". An update start writes each folder as
        /// its package spells it and corrects the settings, and neither a plain restart nor a reinstall moves it after.
        /// </summary>
        /// <remarks>
        /// Measured on the VM before this: since #455 every update start wrote a stock folder under the settings'
        /// spelling, and SimHub, which matches the dashboard it reopens against the folder name with regard to case,
        /// did not reopen it at the restart that followed. The rig is built the way such a rig came to be, by the
        /// migration of ADR 0017 reading the folders off a record written before #374. The round face is here because
        /// its package has no namespace to rewrite: while a card face's namespace was spelled from its folder, a folder
        /// corrected under it made it read as a copy, and its package was refused (#474).
        /// </remarks>
        [Fact]
        public void A_start_over_settings_that_spell_the_stock_folder_the_old_way_writes_it_as_its_package_does()
        {
            PackageExtractor.Install(SyntheticPackage.Instanceable(FaceFolder, "Face850x480", "0.3.0-rc.7"), root, null);
            PackageExtractor.Install(SyntheticPackage.Zip(RoundFolder, "0.3.0-rc.7"), root, null);
            var settings = new OpenDashSettings
            {
                FolderFingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "openDash 850x480", FolderFingerprint.Of(PackageExtractor.InstalledFolder(root, FaceFolder)) },
                    { "openDash 480 round", FolderFingerprint.Of(PackageExtractor.InstalledFolder(root, RoundFolder)) },
                },
            };
            settings.Normalise();
            var wheel = settings.RigScreens().Single(screen => screen.IsFace);
            var round = settings.RigScreens().Single(screen => !screen.IsFace);
            Assert.Equal("openDash 850x480", wheel.Folder);
            Assert.Equal("openDash 480 round", round.Folder);
            Assert.Equal("SlotsopenDash480Round", round.Namespace);

            var record = new SettingsFolderRecord(() => settings);
            var update = new DashboardInstaller(root, null, FaceAndRound("0.3.0-rc.8"), record) { Rig = () => settings.RigScreens() };
            update.EnsureInstalled(false);

            Assert.All(update.Packages, entry => Assert.Null(entry.Error));
            Assert.All(update.Packages, entry => Assert.True(entry.Extracted));
            Assert.Equal(new[] { RoundFolder, FaceFolder }, SpelledFolders());
            Assert.True(HasDashboardSpelled(FaceFolder));
            Assert.True(HasDashboardSpelled(RoundFolder));
            Assert.Equal("0.3.0-rc.8", PackageExtractor.ReadInstalledVersion(root, FaceFolder));
            Assert.Equal("0.3.0-rc.8", PackageExtractor.ReadInstalledVersion(root, RoundFolder));

            // The settings are corrected, the round face keeps the namespace the migration gave it, since a folder
            // spelled anew moves no namespace, and the record is kept under the spelling that was written, so nothing
            // reads the old one again.
            Assert.Equal(FaceFolder, wheel.Folder);
            Assert.Equal(RoundFolder, round.Folder);
            Assert.Equal("SlotsopenDash480Round", round.Namespace);
            Assert.Equal(new[] { RoundFolder, FaceFolder }, settings.FolderFingerprints.Keys.OrderBy(key => key, StringComparer.Ordinal));
            Assert.Equal(FolderFingerprint.Of(PackageExtractor.InstalledFolder(root, FaceFolder)), record.Get(FaceFolder));

            // A plain restart finds nothing to do and nothing edited.
            var restart = new DashboardInstaller(root, null, FaceAndRound("0.3.0-rc.8"), record) { Rig = () => settings.RigScreens() };
            restart.EnsureInstalled(false);
            Assert.All(restart.Packages, entry => Assert.False(entry.Extracted || entry.Edited));
            Assert.Equal(new[] { RoundFolder, FaceFolder }, SpelledFolders());

            // Nor does a reinstall, or a press on the Rig tab, spell it otherwise.
            restart.EnsureInstalled(true);
            Assert.All(restart.Packages, entry => Assert.True(entry.Extracted && !entry.HeldBack));
            Assert.Equal(FaceFolder, restart.Write(wheel).Folder);
            Assert.Equal(new[] { RoundFolder, FaceFolder }, SpelledFolders());
            Assert.True(HasDashboardSpelled(FaceFolder));
        }

        // A card face, whose package carries no namespace of its own (#474)

        private const string LargeRoundName = "OpenDashPlugin.Resources.OpenDash 800 round.simhubdash";
        private const string LargeRoundFolder = "OpenDash 800 round";
        private const string SecondRoundFolder = "OpenDash Round (2)";

        /// <summary>The two card faces a release carries, the round ones, sized in their sidecars as the build writes them.</summary>
        private static MemoryPackageSource Rounds(string version)
        {
            return new MemoryPackageSource()
                .Add(RoundName, SyntheticPackage.CardFace(RoundFolder, 480, 480, version))
                .Add(LargeRoundName, SyntheticPackage.CardFace(LargeRoundFolder, 800, 800, version));
        }

        private DashboardInstaller OverSettings(IPackageSource packages, OpenDashSettings settings)
        {
            return new DashboardInstaller(root, null, packages, new SettingsFolderRecord(() => settings)) { Rig = () => settings.RigScreens() };
        }

        /// <summary>
        /// The ticket's own presses: on a fresh rig, "Card face", "480 round" added from the Rig tab, and then the same
        /// again. The first is written as its package's stock screen and the second as a copy in a folder of its own,
        /// and a start afterwards finds both current.
        /// </summary>
        /// <remarks>
        /// Measured on the VM before this: the first press read "Added Round, but its dashboard could not be installed:
        /// The package mentions OpenDash.SlotsOpenDash480Round nowhere", and Install it again said the same. NewScreen
        /// gave the screen its namespace before its folder, a card face's stock namespace was spelled from its folder,
        /// and the installer took the difference for a second screen whose copy had to be rewritten, in a package that
        /// names no namespace at all. The presses are made the way the Rig tab makes them: AddScreen, which is
        /// PackageCatalogue.NewScreen, from the entry the catalogue reads off the package, and then Write.
        /// </remarks>
        [Fact]
        public void A_card_face_added_to_a_fresh_rig_is_written_and_so_is_a_second_one()
        {
            var packages = Rounds("0.3.0-rc.8");
            var entry = PackageCatalogue.From(packages).Single(package => package.Folder == RoundFolder);
            Assert.Equal(Contract.KindSlots, entry.Kind);
            var settings = new OpenDashSettings();
            settings.Normalise();
            Assert.Empty(settings.RigScreens());
            var installer = OverSettings(packages, settings);

            var first = settings.AddScreen(entry, PanelAddScreen.DefaultName(entry));
            var written = installer.Write(first);

            Assert.Null(written.Error);
            Assert.True(written.Written);
            Assert.Equal("Round", first.Name);
            Assert.Equal(RoundFolder, first.Folder);
            Assert.Equal("Slots480x480", first.Namespace);
            Assert.True(first.IsStock);
            Assert.True(HasDashboardSpelled(RoundFolder));
            Assert.Equal("0.3.0-rc.8", PackageExtractor.ReadInstalledVersion(root, RoundFolder));
            Assert.Contains("\"Title\":\"Round\"", File.ReadAllText(PackageExtractor.InstalledSidecar(root, RoundFolder)));

            var second = settings.AddScreen(entry, PanelAddScreen.DefaultName(entry));
            var again = installer.Write(second);

            Assert.Null(again.Error);
            Assert.True(again.Written);
            Assert.Equal("Round (2)", second.Name);
            Assert.Equal(SecondRoundFolder, second.Folder);
            Assert.NotEqual(first.Namespace, second.Namespace);
            Assert.True(HasDashboardSpelled(SecondRoundFolder));
            Assert.Contains("\"Title\":\"Round (2)\"", File.ReadAllText(PackageExtractor.InstalledSidecar(root, SecondRoundFolder)));
            // The copy reads the slots every card face shares, as the first does, since the package held nothing to
            // point elsewhere.
            Assert.Equal(1, Occurrences(SecondRoundFolder, "OpenDash.Slot1"));
            Assert.Equal(0, Occurrences(SecondRoundFolder, "OpenDash." + second.Namespace));

            // A start afterwards writes nothing, finds nothing edited and reads the rig as up to date.
            installer.EnsureInstalled(false);
            Assert.All(installer.Packages, package => Assert.Null(package.Error));
            Assert.All(installer.Packages, package => Assert.False(package.Extracted || package.Edited));
            Assert.Equal(InstallStatus.UpToDate, installer.Status);
            settings.Normalise();
            Assert.Equal(new[] { first, second }, settings.RigScreens());
        }

        /// <summary>
        /// A card face resized on the Edit panel to the other round is written in a folder of its own, as a face resized
        /// to another size is.
        /// </summary>
        /// <remarks>
        /// The same comparison was reached from here: the screen kept its namespace, its stock namespace followed the new
        /// size, and the copy was refused for want of anything to rewrite. The panel removes the old folder first, as
        /// the Rig tab does before it asks the settings.
        /// </remarks>
        [Fact]
        public void A_card_face_resized_to_the_other_round_is_written_in_a_folder_of_its_own()
        {
            var packages = Rounds("0.3.0-rc.8");
            var catalogue = PackageCatalogue.From(packages);
            var settings = new OpenDashSettings();
            settings.Normalise();
            var installer = OverSettings(packages, settings);
            var round = settings.AddScreen(catalogue.Single(entry => entry.Folder == RoundFolder), "Round");
            Assert.True(installer.Write(round).Written);

            ScreenInstaller.Remove(round, root, null);
            settings.ResizeScreen(round, catalogue.Single(entry => entry.Folder == LargeRoundFolder));
            var written = installer.Write(round);

            Assert.Null(written.Error);
            Assert.True(written.Written);
            Assert.Equal("OpenDash Round", round.Folder);
            Assert.Equal("Slots480x480", round.Namespace);
            Assert.Equal(800, round.Width);
            Assert.True(HasDashboardSpelled("OpenDash Round"));
            Assert.False(PackageExtractor.IsInstalled(root, LargeRoundFolder));
        }

        /// <summary>
        /// A rig that met this before it was fixed holds the card face it could not write, and a second one if the
        /// driver tried again. The next start writes both, Install it again works on either, and neither moves in the
        /// settings.
        /// </summary>
        /// <remarks>
        /// The Rig tab keeps a screen whose dashboard could not be written, so such a rig holds exactly what the failed
        /// presses made: the first card face on the namespace its size spells and the package's folder, the second on a
        /// namespace and a folder of its own, and nothing under DashTemplates for either.
        /// </remarks>
        [Fact]
        public void A_card_face_a_rig_could_not_write_before_is_written_at_the_next_start()
        {
            var settings = new OpenDashSettings
            {
                Rig = new List<ScreenInstance>
                {
                    new ScreenInstance { Kind = Contract.KindSlots, Width = 480, Height = 480, Namespace = "Slots480x480", Name = "Round", Folder = RoundFolder, Package = RoundName },
                    new ScreenInstance { Kind = Contract.KindSlots, Width = 480, Height = 480, Namespace = "Round2", Name = "Round (2)", Folder = SecondRoundFolder, Package = RoundName },
                },
            };
            settings.Normalise();
            var installer = OverSettings(Rounds("0.3.0-rc.8"), settings);
            installer.Refresh();
            Assert.Equal(InstallStatus.NotInstalled, installer.Status);

            installer.EnsureInstalled(false);

            Assert.All(installer.Packages, package => Assert.Null(package.Error));
            Assert.True(installer.Packages.Single(package => package.FolderName == RoundFolder).Extracted);
            Assert.True(installer.Packages.Single(package => package.FolderName == SecondRoundFolder).Extracted);
            Assert.Equal(InstallStatus.UpToDate, installer.Status);
            Assert.True(HasDashboardSpelled(RoundFolder));
            Assert.True(HasDashboardSpelled(SecondRoundFolder));
            Assert.Equal(new[] { "Slots480x480", "Round2" }, settings.RigScreens().Select(screen => screen.Namespace));
            Assert.Equal(new[] { RoundFolder, SecondRoundFolder }, settings.RigScreens().Select(screen => screen.Folder));
            Assert.True(settings.RigScreens()[0].IsStock);

            Assert.True(installer.Write(settings.RigScreens()[0]).Written);
            Assert.True(installer.Write(settings.RigScreens()[1]).Written);
        }

        /// <summary>
        /// A rig migrated from a settings file older than ADR 0017 keeps both round faces apart, and a round face added
        /// to it is a second screen with a folder of its own rather than a second claim on the one it has.
        /// </summary>
        /// <remarks>
        /// The migration reads a screen's size off its folder's name, and the round faces' names carry none, so it tells
        /// them apart by their folders and gives each a namespace from its folder, once. The start then gives them their
        /// sizes from their packages, and from there such a face's namespace is not the one its size spells, so only the
        /// folder the rig already holds can say that the package is on it. Before this, the face added here was handed
        /// "OpenDash 480 round" as well, and writing either would have overwritten the other.
        /// </remarks>
        [Fact]
        public void A_round_face_added_to_a_rig_upgraded_with_one_is_a_second_screen()
        {
            PackageExtractor.Install(SyntheticPackage.CardFace(RoundFolder, 480, 480, "0.3.0-rc.8"), root, null);
            PackageExtractor.Install(SyntheticPackage.CardFace(LargeRoundFolder, 800, 800, "0.3.0-rc.8"), root, null);
            var settings = new OpenDashSettings
            {
                FolderFingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { RoundFolder, FolderFingerprint.Of(PackageExtractor.InstalledFolder(root, RoundFolder)) },
                    { LargeRoundFolder, FolderFingerprint.Of(PackageExtractor.InstalledFolder(root, LargeRoundFolder)) },
                },
            };
            settings.Normalise();
            Assert.Equal(new[] { "SlotsOpenDash480Round", "SlotsOpenDash800Round" }, settings.RigScreens().Select(screen => screen.Namespace));

            // What OpenDash.RepairScreenSizes does at the start, which is compiled by neither check.
            var packages = Rounds("0.3.0-rc.8");
            var catalogue = PackageCatalogue.From(packages);
            foreach (var screen in settings.RigScreens())
            {
                var package = catalogue.Single(entry => entry.Folder == screen.Folder);
                screen.Width = package.Width;
                screen.Height = package.Height;
                screen.Package = package.Package;
            }

            var added = settings.AddScreen(catalogue.Single(entry => entry.Folder == RoundFolder), "Round");
            var installer = OverSettings(packages, settings);
            var written = installer.Write(added);

            Assert.Null(written.Error);
            Assert.Equal("OpenDash Round", added.Folder);
            Assert.Equal(3, settings.RigScreens().Select(screen => screen.Folder).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(3, settings.RigScreens().Select(screen => screen.Namespace).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            installer.EnsureInstalled(false);
            Assert.All(installer.Packages, package => Assert.Null(package.Error));
            Assert.Equal(InstallStatus.UpToDate, installer.Status);
        }

        // A folder outside the rig, which a run reads and never writes (#468)

        private const string CompanionName = "OpenDashPlugin.Resources.OpenDash Companion.simhubdash";
        private const string CompanionFolder = "OpenDash Companion";

        /// <summary>
        /// A leftover folder somebody edited, which no screen on the rig is written from, is named in no question
        /// before a reinstall, and a rig with nothing of its own edited has nothing to be asked.
        /// </summary>
        /// <remarks>
        /// Seen on the VM with the rig of "Tim wheel" and "Rim (2)" and an edited `OpenDash Companion` outside it: the
        /// first press on Reinstall read "You have edited 2 dashboards: OpenDash Rim (2), OpenDash Companion.", and
        /// "Replace anyway" wrote Rim (2) and left the Companion at rc.7, so the driver had been asked to consent to
        /// replacing work that was never touched. The question is EditedFolders, which the Update button asks as well.
        /// </remarks>
        [Fact]
        public void A_leftover_folder_somebody_edited_is_not_named_before_a_reinstall()
        {
            var record = new MemoryFolderRecord();
            // A plugin from before ADR 0017 wrote every package it carried, so the Companion is on the disk, with a
            // fingerprint in the record, although no screen of this rig is written from it.
            var older = Face("0.3.0-rc.7").Add(CompanionName, SyntheticPackage.Zip(CompanionFolder, "0.3.0-rc.7"));
            Installer(older, record: record).EnsureInstalled(false);
            var rig = TwoOfASize();
            var installer = OverRig(Face("0.3.0-rc.8").Add(CompanionName, SyntheticPackage.Zip(CompanionFolder, "0.3.0-rc.8")), record, rig);
            installer.EnsureInstalled(false);
            var theirs = Path.Combine(PackageExtractor.InstalledFolder(root, CompanionFolder), CompanionFolder + ".djson");
            File.WriteAllText(theirs, "{\"Version\":2,\"mine\":true}");

            installer.Refresh();

            Assert.Empty(installer.EditedFolders);
            var companion = installer.Packages.Single(p => p.FolderName == CompanionFolder);
            Assert.True(companion.OutsideRig);
            Assert.False(companion.Edited);
            Assert.All(installer.Packages.Where(p => p != companion), p => Assert.False(p.OutsideRig));
            // It still reports itself in the pill's tooltip, which names what the build carries beyond the rig, and
            // it does not turn the pill, which answers for the rig.
            Assert.Contains(CompanionFolder + ": Update available", installer.PackageReport());
            Assert.Equal(InstallStatus.UpToDate, installer.Status);

            // With a screen of the rig edited as well, that screen is the whole of the question.
            File.WriteAllText(Path.Combine(PackageExtractor.InstalledFolder(root, RimFolder), RimFolder + ".djson"), "{\"Version\":2,\"rim\":true}");
            installer.Refresh();
            Assert.Equal(new[] { RimFolder }, installer.EditedFolders);

            // "Replace anyway" replaces it and leaves the leftover exactly as it was.
            installer.EnsureInstalled(force: true, replaceEdited: true);
            Assert.True(installer.Packages.Single(p => p.FolderName == RimFolder).Extracted);
            Assert.False(installer.Packages.Single(p => p.FolderName == CompanionFolder).Extracted);
            Assert.Equal("{\"Version\":2,\"mine\":true}", File.ReadAllText(theirs));
            Assert.Equal("0.3.0-rc.7", PackageExtractor.ReadInstalledVersion(root, CompanionFolder));
            Assert.Empty(installer.EditedFolders);
        }

        // Somebody's Dash Studio work, and whether an install destroys it

        /// <summary>
        /// The first run after this shipped must not hold everything back. No record means OpenDash has never looked
        /// at the folder, not that it was edited, so the folder is adopted and watched from then on.
        /// </summary>
        [Fact]
        public void A_folder_with_nothing_remembered_is_adopted_rather_than_held_back()
        {
            var record = new MemoryFolderRecord();
            PackageExtractor.Install(SyntheticPackage.Zip("OpenDash", "0.1.0"), root, null);
            Assert.Empty(record.Entries);

            var installer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("OpenDash", "0.2.0")), record: record);
            installer.EnsureInstalled(false);

            var entry = installer.Packages.Single();
            Assert.False(entry.Edited);
            Assert.False(entry.HeldBack);
            Assert.True(entry.Extracted);
            Assert.Equal("0.2.0", entry.InstalledVersion);
            Assert.NotNull(record.Get("OpenDash"));
        }

        /// <summary>
        /// The case #169 creates, and the one it says can hurt somebody.
        ///
        /// A user on rc.2 has DashTemplates/OpenDash holding the twelve-slot card face, and their settings carry no
        /// fingerprint for it, because fingerprints only began in rc.3. The rename puts the zone face under that
        /// name, so the update replaces their dashboard with a different design rather than a newer version of the
        /// same one. Nothing can ask them first, since no record exists to tell an edit from an untouched folder, so
        /// the whole of the protection is that a copy is kept.
        /// </summary>
        [Fact]
        public void A_folder_replaced_under_a_name_it_did_not_have_before_is_still_copied_first()
        {
            var record = new MemoryFolderRecord();
            PackageExtractor.Install(SyntheticPackage.Zip("OpenDash", "0.1.0-rc.2"), root, null);
            var theirs = Path.Combine(root, "DashTemplates", "OpenDash", "OpenDash.djson");
            File.WriteAllText(theirs, "{\"theirs\":true}");
            Assert.Empty(record.Entries);

            var installer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("OpenDash", "0.2.0-rc.1")), record: record);
            installer.EnsureInstalled(false);

            var entry = installer.Packages.Single();
            Assert.True(entry.Extracted);
            Assert.Equal("0.2.0-rc.1", entry.InstalledVersion);

            // The copy exists, holds what they had, and is named so that a person can find it.
            var backup = Path.Combine(root, "DashTemplates", "OpenDash" + PackageExtractor.BackupSuffix);
            Assert.Equal(backup, entry.KeptCopy);
            Assert.True(File.Exists(backup));
            using (var zip = ZipFile.OpenRead(backup))
            {
                var djson = zip.Entries.Single(e => e.FullName.EndsWith("OpenDash.djson", StringComparison.Ordinal));
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
            PackageExtractor.Install(SyntheticPackage.Zip("OpenDash", "0.2.0"), root, null);

            var installer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("OpenDash", "0.2.0")), record: record);
            installer.EnsureInstalled(false);

            Assert.False(installer.Packages.Single().Extracted);
            Assert.NotNull(record.Get("OpenDash"));
        }

        [Fact]
        public void A_folder_edited_after_OpenDash_wrote_it_is_left_alone_and_said_so()
        {
            var record = new MemoryFolderRecord();
            var installer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("OpenDash", "0.1.0")), record: record);
            installer.EnsureInstalled(false);
            Assert.NotNull(record.Get("OpenDash"));

            // What Dash Studio does: it rewrites the dashboard in place.
            var djson = Path.Combine(root, "DashTemplates", "OpenDash", "OpenDash.djson");
            File.WriteAllText(djson, "{\"Version\":2,\"mine\":true}");

            var log = new ListLog();
            var newer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("OpenDash", "0.2.0")), log, record);
            newer.EnsureInstalled(false);

            var entry = newer.Packages.Single();
            Assert.True(entry.Edited);
            Assert.True(entry.HeldBack);
            Assert.False(entry.Extracted);
            Assert.Equal("0.1.0", entry.InstalledVersion);
            Assert.Equal("{\"Version\":2,\"mine\":true}", File.ReadAllText(djson));
            Assert.Contains(log.Lines, line => line.Contains("has changed since OpenDash wrote it"));
        }

        /// <summary>
        /// A folder that was refused is not up to date. Reporting it as such is how Reinstall came to look as
        /// though it had worked while doing nothing at all.
        /// </summary>
        [Fact]
        public void A_folder_left_alone_says_so_rather_than_reporting_up_to_date()
        {
            var record = new MemoryFolderRecord();
            Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("OpenDash", "0.1.0")), record: record).EnsureInstalled(false);
            File.WriteAllText(Path.Combine(root, "DashTemplates", "OpenDash", "OpenDash.djson"), "{\"mine\":true}");

            var installer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("OpenDash", "0.2.0")), record: record);
            installer.EnsureInstalled(false);

            var described = installer.Packages.Single().Describe();
            Assert.Contains("you have edited this one", described);
            Assert.DoesNotContain("Up to date (", described.Replace("you have edited this one, so it was left alone", ""));
        }

        [Fact]
        public void An_edited_folder_is_replaced_when_a_person_says_so()
        {
            var record = new MemoryFolderRecord();
            Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("OpenDash", "0.1.0")), record: record).EnsureInstalled(false);
            var djson = Path.Combine(root, "DashTemplates", "OpenDash", "OpenDash.djson");
            File.WriteAllText(djson, "{\"Version\":2,\"mine\":true}");

            var installer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("OpenDash", "0.2.0")), record: record);
            installer.EnsureInstalled(false, replaceEdited: true);

            var entry = installer.Packages.Single();
            Assert.True(entry.Extracted);
            Assert.False(entry.HeldBack);
            Assert.False(entry.Edited);
            Assert.Equal("0.2.0", entry.InstalledVersion);
            // And what was there is recoverable, because Install kept it.
            Assert.True(PackageExtractor.Restore(root, "OpenDash", null));
            Assert.Equal("{\"Version\":2,\"mine\":true}", File.ReadAllText(djson));
        }

        /// <summary>
        /// A folder put in place is recorded as OpenDash's own even when its fonts then could not be copied (#593).
        /// </summary>
        /// <remarks>
        /// The fonts are copied after the new folder has replaced the old one, and a copy that threw used to throw out
        /// of the install, past the line that records the new folder's fingerprint. The record kept the old folder's,
        /// so the next run read a folder nobody had touched as edited: it was held back from every later install, and
        /// Reinstall and Update asked "You have edited 1 dashboard" about it for good. DashFonts being a file is the
        /// simplest way to make the copy fail on every platform; a folder SimHub's account may not write is the real one.
        /// </remarks>
        [Fact]
        public void A_folder_whose_fonts_could_not_be_copied_is_recorded_and_not_read_as_edited()
        {
            var record = new MemoryFolderRecord();
            Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("OpenDash", "0.1.0")), record: record).EnsureInstalled(false);
            var fonts = Path.Combine(root, PackageExtractor.DashFonts);
            Directory.Delete(fonts, true);
            File.WriteAllText(fonts, "not a folder");

            var newer = new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("OpenDash", "0.2.0"));
            var installer = Installer(newer, record: record);
            installer.EnsureInstalled(false);

            var entry = installer.Packages.Single();
            Assert.True(entry.Extracted);
            Assert.Equal("0.2.0", entry.InstalledVersion);
            Assert.Equal(InstallStatus.UpToDate, entry.Status);
            // Said rather than swallowed, and not as a failure: the dashboard is in place, drawn in other faces.
            Assert.Null(entry.Error);
            Assert.Contains("already exists", entry.FontsError);
            Assert.Contains("installed without its fonts", entry.Describe());
            Assert.Equal(entry.FontsError, installer.LastError);
            Assert.True(record.Get("OpenDash") == FolderFingerprint.Of(PackageExtractor.InstalledFolder(root, "OpenDash")),
                "the record should hold the fingerprint of the folder just put in place");

            // What every page reads the next time it looks.
            var next = Installer(newer, record: record);
            next.Refresh();
            // Reinstall everything asks its question of these folders; the message is the line it would have put up.
            Assert.True(next.EditedFolders.Count == 0, "Reinstall would ask: " + PanelUpdates.ReinstallQuestion(next.EditedFolders));
            Assert.False(next.Packages.Single().Edited);
        }

        /// <summary>
        /// A folder OpenDash cannot read is one it cannot vouch for, which is the asking case rather than a failure.
        /// It used to be a failure that never went away, since the fingerprint is taken where Edited is assigned: the
        /// exception an unreadable file raises landed in the blanket catch of Process, the package was reported Failed,
        /// and every later run reached the same line and did the same thing, so the dashboard was never installed
        /// again and nobody was ever asked about the folder either.
        /// </summary>
        [DeniedPathFact]
        public void A_folder_that_cannot_be_read_is_asked_about_rather_than_marked_failed()
        {
            var record = new MemoryFolderRecord();
            Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("OpenDash", "0.1.0")), record: record).EnsureInstalled(false);

            // A file beside the dashboard rather than its sidecar: the version is read from the sidecar before the
            // fingerprint is taken, and a sidecar that cannot be read is a failure the installer reports on purpose.
            denied.Deny(Path.Combine(root, "DashTemplates", "OpenDash", "cards.djson"));

            var log = new ListLog();
            var installer = Installer(new MemoryPackageSource().Add(WideName, SyntheticPackage.Zip("OpenDash", "0.2.0")), log, record);
            installer.EnsureInstalled(false);

            var entry = installer.Packages.Single();
            Assert.Null(entry.Error);
            Assert.NotEqual(InstallStatus.Failed, entry.Status);
            Assert.True(entry.Edited);
            Assert.True(entry.HeldBack);
            Assert.False(entry.Extracted);
            Assert.Contains(log.Lines, line => line.Contains("has changed since OpenDash wrote it"));
        }

        // The install run

        [Fact]
        public void Every_embedded_package_is_installed_and_the_rig_is_named_by_its_version()
        {
            var log = new ListLog();
            var installer = Installer(TwoPackages(), log);
            Assert.Equal(2, installer.PackageCount);
            Assert.True(installer.HasEmbeddedPackage);

            installer.EnsureInstalled(false);

            Assert.Equal(InstallStatus.UpToDate, installer.Status);
            Assert.Null(installer.LastError);
            Assert.True(PackageExtractor.IsInstalled(root, "OpenDash"));
            Assert.True(PackageExtractor.IsInstalled(root, SmallFolder));
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, SmallFolder));

            Assert.Equal(new[] { SmallFolder, "OpenDash" }, installer.Packages.Select(p => p.FolderName));
            Assert.All(installer.Packages, p => Assert.Equal(InstallStatus.UpToDate, p.Status));
            Assert.All(installer.Packages, p => Assert.True(p.Extracted));
            Assert.All(installer.Packages, p => Assert.Null(p.Error));

            Assert.All(installer.Packages, p => Assert.Equal("0.2.0", p.InstalledVersion));
            Assert.All(installer.Packages, p => Assert.Equal("0.2.0", p.EmbeddedVersion));
            // Named by the dashboards the rig holds, and here they are the half that is behind the plugin.
            var rig = new[] { SmallFolder, "OpenDash" };
            Assert.Equal("OpenDash 0.2.0", DashboardInstaller.Summary(UpdateCheck.RigVersion(installer.Packages, rig, "0.3.0")));

            // Both packages carry the same fonts; the second install finds them in DashFonts already.
            Assert.Equal(2, Directory.GetFiles(Path.Combine(root, "DashFonts"), "*.ttf").Length);
            Assert.Contains("info: Installed OpenDash 1280x480 0.2.0 into " + PackageExtractor.InstalledFolder(root, SmallFolder), log.Lines);
            Assert.Contains("info: Installed OpenDash 0.2.0 into " + PackageExtractor.InstalledFolder(root, "OpenDash"), log.Lines);
        }

        [Fact]
        public void Only_the_packages_that_need_it_are_installed_unless_forced()
        {
            using (var package = SyntheticPackage.Zip(SmallFolder, "0.2.0")) PackageExtractor.Install(package, root, null);
            var installer = Installer(TwoPackages());

            installer.EnsureInstalled(false);

            Assert.Equal(InstallStatus.UpToDate, installer.Status);
            Assert.False(installer.Packages.Single(p => p.FolderName == SmallFolder).Extracted);
            Assert.True(installer.Packages.Single(p => p.FolderName == "OpenDash").Extracted);
            Assert.False(File.Exists(Path.Combine(root, "DashTemplates", SmallFolder + PackageExtractor.BackupSuffix)));

            installer.EnsureInstalled(true);

            Assert.Equal(InstallStatus.UpToDate, installer.Status);
            Assert.All(installer.Packages, p => Assert.True(p.Extracted));
            Assert.True(File.Exists(Path.Combine(root, "DashTemplates", SmallFolder + PackageExtractor.BackupSuffix)));
            Assert.True(File.Exists(Path.Combine(root, "DashTemplates", "OpenDash" + PackageExtractor.BackupSuffix)));
        }

        /// <summary>
        /// What the panel's bar is allowed to assume: a report before the first package, one after each package, and
        /// nothing in between, so a bar drawn from this moves in whole dashboards and arrives exactly at the end.
        /// </summary>
        [Fact]
        public void A_reinstall_reports_one_package_at_a_time_and_finishes_at_the_end()
        {
            var installer = Installer(TwoPackages());
            var reported = new List<double>();

            installer.EnsureInstalled(true, progress: reported.Add);

            Assert.Equal(new[] { 0d, 0.5d, 1d }, reported);
        }

        [Fact]
        public void A_reinstall_the_bar_is_not_watched_for_still_writes_every_dashboard()
        {
            // The panel marshals each report onto the UI thread, and a settings page closed mid-run makes that
            // throw. Stopping between two packages would leave the rig half written, which is the one outcome this
            // installer exists to prevent, so a report that throws is dropped and the run carries on.
            var installer = Installer(TwoPackages());

            installer.EnsureInstalled(true, progress: _ => throw new InvalidOperationException("the panel has gone"));

            Assert.Equal(InstallStatus.UpToDate, installer.Status);
            Assert.All(installer.Packages, p => Assert.True(p.Extracted));
            Assert.True(PackageExtractor.IsInstalled(root, "OpenDash"));
            Assert.True(PackageExtractor.IsInstalled(root, SmallFolder));
        }

        /// <summary>Reading is not a run, so it says nothing to a bar that is not drawn for it.</summary>
        [Fact]
        public void Refresh_reports_nothing_because_it_writes_nothing()
        {
            var installer = Installer(TwoPackages());
            var reported = new List<double>();
            installer.EnsureInstalled(true, progress: reported.Add);
            reported.Clear();

            installer.Refresh();

            Assert.Empty(reported);
        }

        [Fact]
        public void An_older_installed_copy_is_updated_and_a_newer_one_left_alone()
        {
            using (var package = SyntheticPackage.Zip("OpenDash", "0.1.0")) PackageExtractor.Install(package, root, null);
            using (var package = SyntheticPackage.Zip(SmallFolder, "0.3.0")) PackageExtractor.Install(package, root, null);
            var installer = Installer(TwoPackages("0.2.0"));

            installer.Refresh();
            var wide = installer.Packages.Single(p => p.FolderName == "OpenDash");
            Assert.Equal(InstallStatus.UpdateAvailable, installer.Status);
            Assert.Equal("0.1.0", wide.InstalledVersion);
            Assert.Equal("0.2.0", wide.EmbeddedVersion);

            installer.EnsureInstalled(false);

            Assert.Equal(InstallStatus.UpToDate, installer.Status);
            Assert.Equal("0.2.0", PackageExtractor.ReadInstalledVersion(root, "OpenDash"));
            Assert.Equal("0.3.0", PackageExtractor.ReadInstalledVersion(root, SmallFolder));
            Assert.Equal("0.2.0", installer.Packages.Single(p => p.FolderName == "OpenDash").InstalledVersion);
            Assert.Equal("0.3.0", installer.Packages.Single(p => p.FolderName == SmallFolder).InstalledVersion);
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
            Assert.All(installer.Packages, p => Assert.Equal("0.2.0", p.EmbeddedVersion));
            Assert.False(Directory.Exists(Path.Combine(root, "DashTemplates")));
        }

        // Status aggregation

        [Fact]
        public void The_worst_status_wins_and_a_broken_package_does_not_stop_the_others()
        {
            var log = new ListLog();
            var packages = new MemoryPackageSource()
                .Add("OpenDashPlugin.Resources.broken.simhubdash", "this is not a zip")
                .Add(WideName, SyntheticPackage.Zip("OpenDash", "0.2.0"));
            var installer = Installer(packages, log);

            installer.EnsureInstalled(false);

            Assert.Equal(InstallStatus.Failed, installer.Status);
            Assert.NotNull(installer.LastError);
            Assert.True(PackageExtractor.IsInstalled(root, "OpenDash"));

            var broken = installer.Packages[0];
            Assert.Equal(InstallStatus.Failed, broken.Status);
            Assert.Null(broken.FolderName);
            Assert.Equal(installer.LastError, broken.Error);
            Assert.False(broken.Extracted);
            Assert.StartsWith("OpenDashPlugin.Resources.broken.simhubdash: Install failed (", broken.Describe());

            Assert.Equal(InstallStatus.UpToDate, installer.Packages[1].Status);
            Assert.Equal("OpenDash: Up to date", installer.Packages[1].Describe());

            // The rig is still named by its version even though a package failed: one that could not be read has
            // no folder, so it is no screen of anybody's and does not count.
            Assert.Equal("0.2.0", UpdateCheck.RigVersion(installer.Packages, new[] { "OpenDash" }, "0.3.0"));
            Assert.Equal(broken.Describe() + "\n" + "OpenDash: Up to date", installer.PackageReport());
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
            using (var package = SyntheticPackage.Zip("OpenDash", "0.1.0")) PackageExtractor.Install(package, root, null);
            var installer = Installer(TwoPackages("0.2.0"));

            installer.Refresh();

            Assert.Equal(InstallStatus.NotInstalled, installer.Status);
            Assert.Equal(InstallStatus.NotInstalled, installer.Packages.Single(p => p.FolderName == SmallFolder).Status);
            Assert.Equal(InstallStatus.UpdateAvailable, installer.Packages.Single(p => p.FolderName == "OpenDash").Status);
        }

        /// <summary>
        /// A rig that never added the 1920 x 480 face, which since ADR 0017 is most of them, is told the version
        /// it runs (#458).
        /// </summary>
        /// <remarks>
        /// The panel read the version off the folder `OpenDash` alone and named it as it found it. Seen on the VM
        /// on a rig of one 850 x 480 face, the second start of the day: the Install tab read "Version 0.3.0-rc.7
        /// is available. You have an unknown version." under a title reading 0.3.0-rc.6, while the check and the
        /// idle screen's mark had compared the plugin's version all along. The line, the title, the check and the
        /// mark are handed one value now, measured over the rig's own screens.
        /// </remarks>
        [Fact]
        public void A_rig_without_the_1920_face_is_told_the_version_it_runs()
        {
            using (var package = SyntheticPackage.Zip(SmallFolder, "0.3.0-rc.6")) PackageExtractor.Install(package, root, null);
            var installer = Installer(TwoPackages("0.3.0-rc.6"));
            installer.Refresh();
            Assert.False(PackageExtractor.IsInstalled(root, "OpenDash"));

            var running = UpdateCheck.RigVersion(installer.Packages, new[] { SmallFolder }, "0.3.0-rc.6");
            Assert.Equal("0.3.0-rc.6", running);

            // The second start of the day: no check has answered in this session, so the panel opens on the offer
            // the idle screen's mark draws, remembered from the first.
            var offered = UpdateMark.Offered("0.3.0-rc.7", running, pluginStaged: false);
            var opening = UpdateMark.Opening(true, null, offered, running);
            Assert.Equal("Version 0.3.0-rc.7 is available. You have 0.3.0-rc.6.", opening.Line);
            Assert.Equal("OpenDash 0.3.0-rc.6", DashboardInstaller.Summary(running));
            // And an answer the check brings back names what it was handed to compare, which is the same value.
            Assert.Equal("Could not reach GitHub. You have 0.3.0-rc.6.", UpdateCheck.Conclude(running, new ReleaseInfo[0], manual: true).Line);
        }

        /// <summary>
        /// A build that embeds no package installs nothing and says so.
        /// </summary>
        /// <remarks>
        /// It used to read the 1920 x 480 folder and call itself up to date when that folder was there, which is
        /// the one package no longer standing for the rig (#458). A folder is measured against the package that
        /// writes it, and this build carries none, so a folder on the disk changes nothing it can say.
        /// </remarks>
        [Fact]
        public void A_build_without_packages_installs_nothing_and_vouches_for_nothing()
        {
            var log = new ListLog();
            var installer = Installer(new MemoryPackageSource(), log);
            Assert.False(installer.HasEmbeddedPackage);

            installer.EnsureInstalled(true);

            Assert.Equal(InstallStatus.NotInstalled, installer.Status);
            Assert.Empty(installer.Packages);
            Assert.False(Directory.Exists(Path.Combine(root, "DashTemplates")));
            Assert.Contains(log.Lines, line => line.StartsWith("warn: No .simhubdash is embedded"));

            using (var package = SyntheticPackage.Zip("OpenDash", "0.1.0")) PackageExtractor.Install(package, root, null);
            installer.Refresh();
            Assert.Equal(InstallStatus.NotInstalled, installer.Status);
            Assert.Null(installer.LastError);
            // With nothing read, the rig is named by the plugin that is running.
            Assert.Equal("0.2.0", UpdateCheck.RigVersion(installer.Packages, new[] { "OpenDash" }, "0.2.0"));
        }

        // The panel's texts

        [Theory]
        [InlineData("0.1.0", "OpenDash 0.1.0")]
        [InlineData("(unknown version)", "OpenDash (unknown version)")]
        public void Summary_is_the_wordmark_and_the_version(string version, string expected)
        {
            // The lowercase d is how the product spells itself, and the count of dashboards belongs to the
            // pill's tooltip rather than to the title, which is why the summary no longer takes one.
            Assert.Equal(expected, DashboardInstaller.Summary(version));
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
            Assert.Throws<FileNotFoundException>(() => source.Open("OpenDashPlugin.Resources.OpenDash 1280x480.simhubdash"));
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
        [InlineData("{\"Title\":\"OpenDash\",\"Width\":1920}")] // no DashboardVersion
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
