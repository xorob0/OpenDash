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
    /// spaces included (only the folder part is turned into an identifier), so Resources/openDash 1280x480.simhubdash is
    /// "OpenDashPlugin.Resources.openDash 1280x480.simhubdash". GetManifestResourceStream takes that exact name back.</summary>
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

    /// <summary>What the installer found out about one package on its last run.</summary>
    public sealed class PackageStatus
    {
        /// <summary>The name in the package source (the embedded resource name).</summary>
        public string Name { get; set; }

        /// <summary>The folder under DashTemplates, e.g. "openDash 1280x480"; null when the package could not be read.</summary>
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

        /// <summary>"openDash 1280x480: Up to date", with the error appended when there is one.</summary>
        public string Describe()
        {
            var line = (FolderName ?? Name) + ": " + Status.Label();
            return Error == null ? line : line + " (" + Error + ")";
        }
    }

    public sealed partial class DashboardInstaller
    {
        public const string PackageExtension = ".simhubdash";

        /// <summary>The folder of the reference layout (1920 x 480). The panel's version is read from this package when it is
        /// embedded, from the first package otherwise; every package carries the same version, so it rarely matters.</summary>
        public const string PrimaryFolder = "openDash";

        private static readonly IReadOnlyList<PackageStatus> NoPackages = new PackageStatus[0];

        private readonly IInstallLog log;
        private readonly IPackageSource packages;

        public DashboardInstaller(string simHubRoot, IInstallLog log, IPackageSource packages)
        {
            if (packages == null) throw new ArgumentNullException(nameof(packages));
            SimHubRoot = simHubRoot;
            this.log = log ?? NullInstallLog.Instance;
            this.packages = packages;
        }

        public string SimHubRoot { get; }

        /// <summary>The worst status across the packages (Failed over NotInstalled over UpdateAvailable over UpToDate).</summary>
        public InstallStatus Status { get; private set; } = InstallStatus.NotInstalled;

        /// <summary>One entry per package after Refresh or EnsureInstalled, in install order; empty before and when
        /// nothing is embedded.</summary>
        public IReadOnlyList<PackageStatus> Packages { get; private set; } = NoPackages;

        /// <summary>How many packages the plugin carries, which is how many dashboards it installs.</summary>
        public int PackageCount => packages.Names.Count;

        public bool HasEmbeddedPackage => PackageCount > 0;

        /// <summary>Folder of the primary package under DashTemplates ("openDash"); the panel reads its version.</summary>
        public string FolderName { get; private set; } = PrimaryFolder;

        /// <summary>Installed version of the primary package. Null when not installed; Versioning.UnknownVersion when
        /// installed without a readable version.</summary>
        public string InstalledVersion { get; private set; }

        /// <summary>Embedded version of the primary package; null when the assembly carries no package.</summary>
        public string EmbeddedVersion { get; private set; }

        /// <summary>The first failure of the last run; null when every package went through.</summary>
        public string LastError { get; private set; }

        /// <summary>The Dashboard section's title: "openDash 0.1.0 · 10 dashboards". Just "openDash 0.1.0" when nothing
        /// is embedded, so that a build without packages does not announce zero dashboards.</summary>
        public static string Summary(string version, int packageCount)
        {
            var title = "openDash " + version;
            if (packageCount <= 0) return title;
            return title + " · " + packageCount + (packageCount == 1 ? " dashboard" : " dashboards");
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
        public void EnsureInstalled(bool force)
        {
            Run(force, install: true);
        }

        private void Run(bool force, bool install)
        {
            LastError = null;
            var names = packages.Names;
            if (names.Count == 0)
            {
                Packages = NoPackages;
                RefreshWithoutPackage();
                return;
            }

            var results = names.Select(name => Process(name, force, install)).ToList();
            Packages = results;
            Status = results.Aggregate(InstallStatus.UpToDate, (worst, result) => Worse(worst, result.Status));
            LastError = results.Select(result => result.Error).FirstOrDefault(error => error != null);

            var primary = results.FirstOrDefault(result => result.FolderName == PrimaryFolder) ?? results[0];
            FolderName = primary.FolderName ?? PrimaryFolder;
            EmbeddedVersion = primary.EmbeddedVersion;
            InstalledVersion = primary.InstalledVersion;

            if (results.Any(result => result.Extracted)) RefreshSimHubFonts();
        }

        /// <summary>Reads one package, decides, and installs it when asked and needed. Never throws: a failure becomes
        /// the entry's Error and Failed status, and the other packages are still processed.</summary>
        private PackageStatus Process(string name, bool force, bool install)
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

                if (install && (force || Versioning.NeedsInstall(entry.Status)))
                {
                    using (var package = packages.Open(name))
                    {
                        var result = PackageExtractor.Install(package, SimHubRoot, log);
                        log.Info("Fonts copied: " + result.FontsCopied);
                    }
                    entry.Extracted = true;
                    entry.InstalledVersion = ReadInstalled(folder);
                    entry.Status = Versioning.Decide(entry.InstalledVersion, entry.EmbeddedVersion);
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
