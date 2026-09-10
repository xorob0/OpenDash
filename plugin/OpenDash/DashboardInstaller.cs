// DashboardInstaller.cs: finds the .simhubdash packages embedded in the assembly, compares their
// DashboardVersion with what sits under SimHub's DashTemplates, and installs when missing or older.
// The IO lives in PackageExtractor; this class adds the SimHub context (base directory, log, font refresh).
// The pure part, how the files of a folder become the installed version, is in DashboardInstaller.Installed.cs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenDashPlugin
{
    public sealed partial class DashboardInstaller
    {
        public const string PackageExtension = ".simhubdash";

        private readonly IInstallLog log;

        /// <summary>SimHub's working directory is its install directory; DashTemplates and DashFonts are relative to it.</summary>
        public DashboardInstaller() : this(AppDomain.CurrentDomain.BaseDirectory, new SimHubInstallLog())
        {
        }

        public DashboardInstaller(string simHubRoot, IInstallLog log)
        {
            SimHubRoot = simHubRoot;
            this.log = log ?? NullInstallLog.Instance;
        }

        public string SimHubRoot { get; }

        public InstallStatus Status { get; private set; } = InstallStatus.NotInstalled;

        /// <summary>Folder of the first embedded package under DashTemplates ("openDash"); the panel shows this one.</summary>
        public string FolderName { get; private set; } = "openDash";

        /// <summary>Null when not installed; Versioning.UnknownVersion when installed without a readable version.</summary>
        public string InstalledVersion { get; private set; }

        /// <summary>Null when the assembly carries no package.</summary>
        public string EmbeddedVersion { get; private set; }

        public string LastError { get; private set; }

        public static IEnumerable<string> EmbeddedPackageNames()
        {
            return typeof(DashboardInstaller).Assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith(PackageExtension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.Ordinal);
        }

        public bool HasEmbeddedPackage => EmbeddedPackageNames().Any();

        private static Stream OpenPackage(string resourceName)
        {
            var stream = typeof(DashboardInstaller).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null) throw new FileNotFoundException("Embedded package not found: " + resourceName);
            return stream;
        }

        /// <summary>Recomputes the versions and the status without touching the disk.</summary>
        public void Refresh()
        {
            Run(force: false, install: false);
        }

        /// <summary>Installs every embedded package that is missing or older than the embedded copy.
        /// With force, reinstalls all of them. Never throws: failures end up in Status and LastError.</summary>
        public void EnsureInstalled(bool force)
        {
            Run(force, install: true);
        }

        private void Run(bool force, bool install)
        {
            LastError = null;
            var names = EmbeddedPackageNames().ToList();
            if (names.Count == 0)
            {
                RefreshWithoutPackage();
                return;
            }

            var worst = InstallStatus.UpToDate;
            var installedAny = false;
            var first = true;
            foreach (var name in names)
            {
                try
                {
                    string folder;
                    string embedded;
                    using (var package = OpenPackage(name))
                    {
                        embedded = PackageExtractor.ReadPackageVersion(package, out folder);
                    }
                    if (folder == null) throw new InvalidDataException(name + " has no <folder>/<folder>.djson entry.");

                    var installed = ReadInstalled(folder);
                    var status = Versioning.Decide(installed, embedded);
                    log.Info("Package " + folder + ": embedded " + (embedded ?? "(none)") + ", installed " + (installed ?? "(none)") + ", " + status);

                    if (install && (force || Versioning.NeedsInstall(status)))
                    {
                        using (var package = OpenPackage(name))
                        {
                            var result = PackageExtractor.Install(package, SimHubRoot, log);
                            log.Info("Fonts copied: " + result.FontsCopied);
                        }
                        installedAny = true;
                        installed = ReadInstalled(folder);
                        status = Versioning.Decide(installed, embedded);
                    }

                    if (first)
                    {
                        FolderName = folder;
                        EmbeddedVersion = embedded;
                        InstalledVersion = installed;
                        first = false;
                    }
                    worst = Worse(worst, status);
                }
                catch (Exception ex)
                {
                    worst = InstallStatus.Failed;
                    LastError = ex.Message;
                    log.Error("Installing " + name + " failed: " + ex);
                }
            }

            Status = worst;
            if (installedAny) RefreshSimHubFonts();
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

        /// <summary>SimHub reloads DashFonts through FontHelper.RefreshFonts(). The class is internal in SimHub 9.12.6,
        /// so it is reached by reflection; when it is missing the fonts load at the next SimHub start.</summary>
        private void RefreshSimHubFonts()
        {
            try
            {
                var assembly = typeof(SimHub.Plugins.PluginManager).Assembly;
                var type = assembly.GetType("SimHub.Plugins.OutputPlugins.GraphicalDash.FontHelper", false);
                var method = type?.GetMethod("RefreshFonts", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (method == null)
                {
                    log.Warn("FontHelper.RefreshFonts was not found; the dashboard fonts load at the next SimHub start.");
                    return;
                }
                method.Invoke(null, null);
                log.Info("SimHub fonts refreshed.");
            }
            catch (Exception ex)
            {
                log.Warn("Refreshing SimHub fonts failed; they load at the next SimHub start: " + ex.Message);
            }
        }
    }
}
