// DashboardInstaller.Core.cs: the installer without SimHub. Which packages there are, what sits under DashTemplates for
// each of them, the per-package decision and install, the worst status across them and the summary the panel shows.
// Packages come through IPackageSource: the plugin feeds the assembly's embedded resources (DashboardInstaller.cs), the
// tests feed synthetic zips. Compiled into OpenDash.Tests: no SimHub or WPF types here.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OpenDashPlugin
{
    /// <summary>Where the .simhubdash packages come from. The names are opaque handles: they identify a package to Open
    /// and in the log, nothing is parsed out of them (the dashboard folder comes from the zip entries).</summary>
    public interface IPackageSource
    {
        /// <summary>The package names in install order.</summary>
        IReadOnlyList<string> Names { get; }

        /// <summary>A fresh readable stream of the package; the caller disposes it.</summary>
        Stream Open(string name);
    }

    /// <summary>The packages embedded in an assembly: every manifest resource whose name ends in .simhubdash.
    /// MSBuild names an embedded resource "&lt;RootNamespace&gt;.&lt;folder&gt;.&lt;file name&gt;" and keeps the file name as it is,
    /// spaces included (only the folder part is turned into an identifier), so Resources/OpenDash 1280x480.simhubdash is
    /// "OpenDashPlugin.Resources.OpenDash 1280x480.simhubdash". GetManifestResourceStream takes that exact name back.</summary>
    public sealed class AssemblyPackageSource : IPackageSource
    {
        private readonly Assembly assembly;

        public AssemblyPackageSource(Assembly assembly)
        {
            this.assembly = assembly;
            Names = ResourceNames(assembly, DashboardInstaller.PackageExtension);
        }

        public IReadOnlyList<string> Names { get; }

        /// <summary>The assembly's manifest resource names with the given extension, in ordinal order.</summary>
        public static IReadOnlyList<string> ResourceNames(Assembly assembly, string extension)
        {
            return assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        public Stream Open(string name)
        {
            var stream = assembly.GetManifestResourceStream(name);
            if (stream == null) throw new FileNotFoundException("Embedded package not found: " + name);
            return stream;
        }
    }

    /// <summary>
    /// What OpenDash wrote into a dashboard folder, remembered between runs so that a folder somebody has since
    /// edited can be told from one that is still ours. It is implemented over the plugin's settings; the installer
    /// keeps no state itself and knows nothing about where this is kept.
    /// </summary>
    public interface IFolderRecord
    {
        /// <summary>The fingerprint recorded when the folder was last written, or null when there is none.</summary>
        string Get(string folderName);

        /// <summary>Remembers the fingerprint of a folder OpenDash has just written.</summary>
        void Set(string folderName, string fingerprint);
    }

    /// <summary>Remembers nothing, so every installed folder reads as one we cannot vouch for.</summary>
    public sealed class NoFolderRecord : IFolderRecord
    {
        public static readonly NoFolderRecord Instance = new NoFolderRecord();
        public string Get(string folderName) => null;
        public void Set(string folderName, string fingerprint) { }
    }

    /// <summary>What the installer found out about one package on its last run.</summary>
    public sealed class PackageStatus
    {
        /// <summary>The name in the package source (the embedded resource name).</summary>
        public string Name { get; set; }

        /// <summary>The folder under DashTemplates, e.g. "OpenDash 1280x480"; null when the package could not be read.</summary>
        public string FolderName { get; set; }

        /// <summary>Null when the package has no readable sidecar version.</summary>
        public string EmbeddedVersion { get; set; }

        /// <summary>Null when not installed; Versioning.UnknownVersion when installed without a readable version.</summary>
        public string InstalledVersion { get; set; }

        public InstallStatus Status { get; set; }

        /// <summary>True when this run extracted the package.</summary>
        public bool Extracted { get; set; }

        /// <summary>The failure, null when the package was read (and installed, when asked) without error.</summary>
        public string Error { get; set; }

        /// <summary>True when the folder on disk is not the one OpenDash wrote, so replacing it would destroy
        /// somebody's work. Biased towards true: no record and an unreadable folder both count.</summary>
        public bool Edited { get; set; }

        /// <summary>True when this package was left alone because it was edited rather than because it was current.</summary>
        public bool HeldBack { get; set; }

        /// <summary>Where the copy of what was there went, when this run replaced something. Null otherwise.</summary>
        public string KeptCopy { get; set; }

        /// <summary>"OpenDash 1280x480: Up to date", with the error appended when there is one.</summary>
        public string Describe()
        {
            var line = (FolderName ?? Name) + ": " + Status.Label();
            // A folder that was refused is not up to date, and saying so was how Reinstall came to report success
            // for a dashboard it had deliberately not touched.
            if (HeldBack) line += " (you have edited this one, so it was left alone)";
            return Error == null ? line : line + " (" + Error + ")";
        }
    }

    public sealed partial class DashboardInstaller
    {
        public const string PackageExtension = ".simhubdash";

        /// <summary>The folder of the reference layout (1920 x 480). The panel's version is read from this package when it is
        /// embedded, from the first package otherwise; every package carries the same version, so it rarely matters.</summary>
        public const string PrimaryFolder = "OpenDash";

        private static readonly IReadOnlyList<PackageStatus> NoPackages = new PackageStatus[0];

        private readonly IInstallLog log;
        private readonly IPackageSource packages;
        private readonly IFolderRecord record;

        public DashboardInstaller(string simHubRoot, IInstallLog log, IPackageSource packages, IFolderRecord record = null)
        {
            if (packages == null) throw new ArgumentNullException(nameof(packages));
            SimHubRoot = simHubRoot;
            this.log = log ?? NullInstallLog.Instance;
            this.packages = packages;
            this.record = record ?? NoFolderRecord.Instance;
        }

        public string SimHubRoot { get; }

        /// <summary>The packages this build carries, so the panel can offer them and ScreenInstaller can write one.</summary>
        public IPackageSource PackageSource => packages;


        /// <summary>The record this installer keeps, so that an installer built over downloaded packages keeps the
        /// same one rather than starting a second, disagreeing memory of what OpenDash wrote.</summary>
        public IFolderRecord Record => record;

        /// <summary>The worst status across the packages (Failed over NotInstalled over UpdateAvailable over UpToDate).</summary>
        public InstallStatus Status { get; private set; } = InstallStatus.NotInstalled;

        /// <summary>
        /// The status the "This plugin" pill shows: the worse of what is on disk and what GitHub last answered.
        /// </summary>
        /// <remarks>
        /// One pill is answerable to two different comparisons. Status is the embedded package measured against what
        /// sits under DashTemplates, and it knows nothing of a release that exists but has never been downloaded; a
        /// check against GitHub knows of the release and nothing of the disk. Reading the pill off the first alone
        /// told a user whose disk matched the plugin that they were up to date, directly above a sentence saying a
        /// newer release was waiting. The worse of the two is what the pill owes them, so an offer from either source
        /// turns it amber.
        ///
        /// Every other update state leaves the package status standing, which is deliberate: Checking is a question
        /// still open, and Idle, Disabled and Unreachable are each an answer nobody has, so none of the four is news
        /// about the dashboards on the disk. Unreachable in particular is a state rather than a fault, since a driver
        /// whose rig has no network can do nothing about it and does not need a pill to say so.
        /// </remarks>
        public static InstallStatus PillStatus(InstallStatus installed, UpdateState update)
        {
            return Worse(installed, update == UpdateState.UpdateAvailable ? InstallStatus.UpdateAvailable : InstallStatus.UpToDate);
        }

        /// <summary>One entry per package after Refresh or EnsureInstalled, in install order; empty before and when
        /// nothing is embedded.</summary>
        public IReadOnlyList<PackageStatus> Packages { get; private set; } = NoPackages;

        /// <summary>How many packages the plugin carries, which is how many dashboards it installs.</summary>
        public int PackageCount => packages.Names.Count;

        public bool HasEmbeddedPackage => PackageCount > 0;

        /// <summary>Folder of the primary package under DashTemplates ("OpenDash"); the panel reads its version.</summary>
        public string FolderName { get; private set; } = PrimaryFolder;

        /// <summary>Installed version of the primary package. Null when not installed; Versioning.UnknownVersion when
        /// installed without a readable version.</summary>
        public string InstalledVersion { get; private set; }

        /// <summary>Embedded version of the primary package; null when the assembly carries no package.</summary>
        public string EmbeddedVersion { get; private set; }

        /// <summary>The first failure of the last run; null when every package went through.</summary>
        public string LastError { get; private set; }

        /// <summary>
        /// The "This plugin" section's title: "OpenDash 0.1.0".
        /// </summary>
        /// <remarks>
        /// The wordmark is written as the product writes it, with the lowercase d, because a section headed
        /// "OpenDash" spells the name two ways on one page and the canvas spells it one way. The count of
        /// dashboards used to follow the version and does not any more: the section is about the plugin rather
        /// than about its packages, and the pill beside this line carries a tooltip naming every package and the
        /// state each one is in, which says the same thing and says it usefully.
        /// </remarks>
        public static string Summary(string version)
        {
            return "OpenDash " + version;
        }

        /// <summary>One line per package, for the status tooltip: which dashboard is in which state, and why it failed.</summary>
        public string PackageReport()
        {
            var report = new StringBuilder();
            foreach (var package in Packages)
            {
                if (report.Length > 0) report.Append('\n');
                report.Append(package.Describe());
            }
            return report.ToString();
        }

        /// <summary>The installed version of one dashboard folder, from what its files say. Null when
        /// &lt;folder&gt;/&lt;folder&gt;.djson is absent (not installed). The sidecar's DashboardVersion when it has one.
        /// Versioning.UnknownVersion when the sidecar is missing (null text), malformed or without a DashboardVersion,
        /// which Decide ranks below every embedded version so that the package is reinstalled. Never throws.</summary>
        public static string InstalledVersionFrom(bool dashExists, string sidecarText)
        {
            if (!dashExists) return null;
            return Versioning.ParseDashboardVersion(sidecarText) ?? Versioning.UnknownVersion;
        }

        /// <summary>Recomputes the versions and the status without touching the disk.</summary>
        public void Refresh()
        {
            Run(force: false, install: false);
        }

        /// <summary>Installs every embedded package that is missing or older than the embedded copy.
        /// With force, reinstalls all of them. Never throws: failures end up in Status, LastError and Packages.</summary>
        /// <summary>
        /// Installs what needs installing.
        /// </summary>
        /// <param name="force">Install every package whether or not it is current, which is what Reinstall means.</param>
        /// <param name="replaceEdited">
        /// Replace a folder that has changed since OpenDash wrote it. False everywhere except where a person has been
        /// shown what it means and said yes, because such a folder holds work that deleting it destroys.
        /// </param>
        /// <param name="progress">
        /// Packages finished over packages to do, from 0 before the first to 1 after the last, or null for a caller
        /// with nothing to draw. A package is the finest grain there is here, because extracting one is a single
        /// call into PackageExtractor and reports nothing until it returns.
        /// </param>
        public void EnsureInstalled(bool force, bool replaceEdited = false, Action<double> progress = null)
        {
            Run(force, install: true, replaceEdited: replaceEdited, progress: progress);
        }

        /// <summary>
        /// The folders the rig actually wants installed, or null for every package the assembly carries.
        /// </summary>
        /// <remarks>
        /// Until ADR 0017 the plugin wrote every package it embeds on every start, so a user who owned
        /// one screen got fourteen dashboards in SimHub's list and no way to be rid of them. A rig is a
        /// list of what somebody has, so it is also the list of what to write; anything outside it is
        /// left exactly as it is rather than deleted, because removing a screen is something a user
        /// asks for and never something an upgrade decides.
        ///
        /// Null rather than an empty list for "everything", so that a caller which has not been taught
        /// about the rig -- the update path, and the tests -- behaves as it always did. An empty rig is
        /// an empty list and correctly installs nothing: that is a new user, and the panel teaches from
        /// exactly that state.
        /// </remarks>
        public IReadOnlyCollection<string> Wanted { get; set; }

        private void Run(bool force, bool install, bool replaceEdited = false, Action<double> progress = null)
        {
            LastError = null;
            var names = packages.Names;
            if (names.Count == 0)
            {
                Packages = NoPackages;
                RefreshWithoutPackage();
                return;
            }

            // Read for every package, so the panel can still say what is installable and at what
            // version; written only for the folders the rig wants.
            var wanted = Wanted;
            var results = new List<PackageStatus>(names.Count);
            Report(progress, 0);
            foreach (var name in names)
            {
                results.Add(Process(name, force, install && Includes(wanted, FolderOf(name)), replaceEdited));
                Report(progress, (double)results.Count / names.Count);
            }
            Packages = results;
            // Over the packages the rig wants, and not over every package the plugin embeds.
            //
            // Since ADR 0017 a screen exists because somebody added it, so a package nobody added is
            // never written -- and aggregating over all of them meant the worst was permanently
            // NotInstalled for the portrait sizes nobody owns. The pill under "This plugin" then read
            // NOT INSTALLED for ever, on a rig whose every screen was installed and current, which is
            // the one thing a status pill must not do. A package outside the rig is not missing; it is
            // not asked for, and the row for it says so on its own line.
            //
            // An empty rig aggregates to UpToDate, which is right: a new user has nothing installed and
            // nothing outstanding, and what the panel owes them is the Rig tab, not a red pill.
            var mine = results.Where(result => Includes(wanted, result.FolderName)).ToList();
            Status = (mine.Count == 0 ? results.Where(result => result.Status == InstallStatus.Failed) : mine)
                .Aggregate(InstallStatus.UpToDate, (worst, result) => Worse(worst, result.Status));
            LastError = results.Select(result => result.Error).FirstOrDefault(error => error != null);

            var primary = results.FirstOrDefault(result => result.FolderName == PrimaryFolder) ?? results[0];
            FolderName = primary.FolderName ?? PrimaryFolder;
            EmbeddedVersion = primary.EmbeddedVersion;
            InstalledVersion = primary.InstalledVersion;

            if (results.Any(result => result.Extracted)) RefreshSimHubFonts();
        }

        /// <summary>Reads one package, decides, and installs it when asked and needed. Never throws: a failure becomes
        /// the entry's Error and Failed status, and the other packages are still processed.</summary>
        private PackageStatus Process(string name, bool force, bool install, bool replaceEdited)
        {
            var entry = new PackageStatus { Name = name };
            try
            {
                string folder;
                using (var package = packages.Open(name))
                {
                    entry.EmbeddedVersion = PackageExtractor.ReadPackageVersion(package, out folder);
                }
                if (folder == null) throw new InvalidDataException(name + " has no <folder>/<folder>.djson entry.");
                entry.FolderName = folder;
                entry.InstalledVersion = ReadInstalled(folder);
                entry.Status = Versioning.Decide(entry.InstalledVersion, entry.EmbeddedVersion);
                log.Info("Package " + folder + ": embedded " + (entry.EmbeddedVersion ?? "(none)")
                    + ", installed " + (entry.InstalledVersion ?? "(none)") + ", " + entry.Status);

                var installedFolder = PackageExtractor.InstalledFolder(SimHubRoot, folder);
                var remembered = record.Get(folder);
                // Three cases, and only the third is somebody's work. Not installed: nothing to lose. Installed with
                // nothing remembered: OpenDash has never looked at this folder, which is every folder the first time
                // this runs, so it is adopted rather than held back. Installed and different from what was recorded:
                // it changed after OpenDash wrote it, and that is the case worth stopping for.
                entry.Edited = entry.InstalledVersion != null
                    && !string.IsNullOrWhiteSpace(remembered)
                    && !FolderFingerprint.LooksUntouched(installedFolder, remembered);

                if (install && (force || Versioning.NeedsInstall(entry.Status)))
                {
                    // Replacing a folder deletes it, so an edited one is left alone and reported rather than
                    // overwritten. `replaceEdited` is how a person says they have seen the warning and meant it.
                    if (entry.Edited && !replaceEdited)
                    {
                        entry.HeldBack = true;
                        log.Warn(folder + " has changed since OpenDash wrote it, so it was left alone. "
                            + "Press Reinstall a second time to replace it; the copy you have is kept.");
                        return entry;
                    }
                    var replacingAuthoredWork = entry.Edited;
                    using (var package = packages.Open(name))
                    {
                        var result = PackageExtractor.Install(package, SimHubRoot, log, replacingAuthoredWork);
                        log.Info("Fonts copied: " + result.FontsCopied);
                        entry.KeptCopy = result.BackupPath;
                    }
                    entry.Extracted = true;
                    entry.InstalledVersion = ReadInstalled(folder);
                    entry.Status = Versioning.Decide(entry.InstalledVersion, entry.EmbeddedVersion);
                    record.Set(folder, FolderFingerprint.Of(installedFolder));
                    entry.Edited = false;
                }
                else if (entry.InstalledVersion != null && string.IsNullOrWhiteSpace(remembered))
                {
                    // Adopting what is already there. An edit made before OpenDash started looking cannot be seen,
                    // and pretending otherwise would mean asking every user about every folder exactly once, for
                    // nothing. From here on the folder is watched.
                    record.Set(folder, FolderFingerprint.Of(installedFolder));
                }
            }
            catch (Exception ex)
            {
                entry.Status = InstallStatus.Failed;
                entry.Error = ex.Message;
                log.Error("Installing " + name + " failed: " + ex);
            }
            return entry;
        }

        /// <summary>Whether the rig wants this folder. A null list is "everything", which is what a caller
        /// that has not been taught about the rig means.</summary>
        private static bool Includes(IReadOnlyCollection<string> wanted, string folder)
        {
            if (wanted == null) return true;
            if (folder == null) return false;
            foreach (var name in wanted)
            {
                if (string.Equals(name, folder, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>The folder a package writes, without extracting it. Null when it cannot be read.</summary>
        private string FolderOf(string name)
        {
            try
            {
                using (var package = packages.Open(name))
                {
                    string folder;
                    PackageExtractor.ReadPackageVersion(package, out folder);
                    return folder;
                }
            }
            catch (Exception ex)
            {
                log.Warn("Could not read the folder of " + name + ": " + ex.Message);
                return null;
            }
        }

        private void RefreshWithoutPackage()
        {
            EmbeddedVersion = null;
            try
            {
                InstalledVersion = ReadInstalled(FolderName);
                Status = InstalledVersion == null ? InstallStatus.NotInstalled : InstallStatus.UpToDate;
                log.Warn("No .simhubdash is embedded in this build; nothing to install (see plugin/OpenDash/Resources/README.md).");
            }
            catch (Exception ex)
            {
                Status = InstallStatus.Failed;
                LastError = ex.Message;
                log.Error("Reading the installed dashboard failed: " + ex);
            }
        }

        /// <summary>Reads the folder's files; the interpretation is InstalledVersionFrom. An unreadable sidecar (locked,
        /// no permission) throws and the caller reports Failed, a missing or malformed one reads as UnknownVersion.</summary>
        private string ReadInstalled(string folder)
        {
            var exists = PackageExtractor.IsInstalled(SimHubRoot, folder);
            var sidecar = PackageExtractor.InstalledSidecar(SimHubRoot, folder);
            var text = exists && File.Exists(sidecar) ? File.ReadAllText(sidecar) : null;
            return InstalledVersionFrom(exists, text);
        }

        private static InstallStatus Worse(InstallStatus a, InstallStatus b)
        {
            return Severity(a) >= Severity(b) ? a : b;
        }

        /// <summary>
        /// A report the run cannot be derailed by.
        /// </summary>
        /// <remarks>
        /// The caller draws this on the UI thread through Dispatcher.Invoke, and a settings page that has been closed
        /// makes that throw. An exception between two packages would abandon the rest of the run and leave a rig half
        /// written, which is exactly what this installer exists to avoid, so a bar nobody is watching is dropped and
        /// the install carries on.
        /// </remarks>
        private static void Report(Action<double> progress, double fraction)
        {
            if (progress == null) return;
            try
            {
                progress(fraction);
            }
            catch
            {
                // Deliberately silent: log is the caller's and a failed report says nothing about the install.
            }
        }

        private static int Severity(InstallStatus status)
        {
            switch (status)
            {
                case InstallStatus.Failed: return 3;
                case InstallStatus.NotInstalled: return 2;
                case InstallStatus.UpdateAvailable: return 1;
                default: return 0;
            }
        }

        /// <summary>Reloads SimHub's DashFonts after an install. Implemented in DashboardInstaller.cs, where SimHub is
        /// available; in the tests the call compiles away.</summary>
        partial void RefreshSimHubFonts();
    }
}
