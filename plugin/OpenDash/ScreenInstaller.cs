// ScreenInstaller.cs: writes and removes the DashTemplates folder one screen owns.
//
// DashboardInstaller's job is the embedded packages -- which of them there are, what version each is,
// and the update path. That is a catalogue. This is the rig: one screen, its own folder, its own
// namespace, written from whichever package it was made from. The two meet only at the stock screen,
// whose folder is the package's own and which DashboardInstaller therefore keeps current by itself.
//
// ADR 0017 is why a screen has a folder at all.
using System;
using System.IO;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>What became of a screen's folder.</summary>
    public sealed class ScreenInstallResult
    {
        public bool Ok { get { return Error == null; } }

        /// <summary>The folder written or removed.</summary>
        public string Folder { get; set; }

        /// <summary>Null when it worked; the reason otherwise, in the words the panel shows.</summary>
        public string Error { get; set; }

        /// <summary>True when this call actually wrote the folder, false when it was already there.</summary>
        public bool Written { get; set; }
    }

    public static class ScreenInstaller
    {
        /// <summary>
        /// Writes the screen's package into its own folder, unless it is already there.
        /// </summary>
        /// <remarks>
        /// A stock screen's folder is the package's own and is written unchanged, which is the
        /// pre-ADR-0017 behaviour; only a screen with a namespace of its own has anything rewritten.
        /// The fingerprint recorded afterwards is of what was actually written, not of the embedded
        /// package, or an instanced folder would read as somebody's own work on the very next start and
        /// the update path would refuse to touch it for ever.
        /// </remarks>
        public static ScreenInstallResult Write(
            ScreenInstance screen,
            IPackageSource packages,
            string simHubRoot,
            IFolderRecord record,
            IInstallLog log,
            bool force = false)
        {
            log = log ?? NullInstallLog.Instance;
            var result = new ScreenInstallResult { Folder = screen?.Folder };
            if (screen == null || string.IsNullOrEmpty(screen.Folder))
            {
                result.Error = "That screen has no folder to write.";
                return result;
            }
            if (!force && PackageExtractor.IsInstalled(simHubRoot, screen.Folder)) return result;

            var name = PackageNameFor(screen, packages, log);
            if (name == null)
            {
                result.Error = "This build ships no package for a " + screen.SizeLabel + " " + screen.Kind + ".";
                return result;
            }

            try
            {
                using (var package = packages.Open(name))
                {
                    PackageExtractor.Install(package, simHubRoot, log, false, new PackageExtractor.ScreenTarget
                    {
                        Folder = screen.Folder,
                        Title = screen.Name,
                        FromNamespace = screen.StockNamespace,
                        ToNamespace = screen.Namespace,
                    });
                }
                if (record != null)
                {
                    record.Set(screen.Folder, FolderFingerprint.Of(PackageExtractor.InstalledFolder(simHubRoot, screen.Folder)));
                }
                result.Written = true;
                log.Info("Wrote the screen " + screen.Name + " into " + screen.Folder + ".");
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                log.Error("Writing the screen " + screen.Name + " failed: " + ex);
            }
            return result;
        }

        /// <summary>
        /// Deletes the folder a screen owns.
        /// </summary>
        /// <remarks>
        /// Only the folder. The screen's settings are the rig's to drop, and a wheel button bound to its
        /// actions is SimHub's; the panel says both before it asks. A folder that is not there already
        /// is a success, because the thing the caller wanted is true.
        /// </remarks>
        public static ScreenInstallResult Remove(ScreenInstance screen, string simHubRoot, IInstallLog log)
        {
            log = log ?? NullInstallLog.Instance;
            var result = new ScreenInstallResult { Folder = screen?.Folder };
            if (screen == null || string.IsNullOrEmpty(screen.Folder)) return result;
            var folder = PackageExtractor.InstalledFolder(simHubRoot, screen.Folder);
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                    result.Written = true;
                    log.Info("Removed " + screen.Folder + ".");
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                log.Error("Removing " + screen.Folder + " failed: " + ex);
            }
            return result;
        }

        /// <summary>
        /// The embedded package a screen is written from.
        /// </summary>
        /// <remarks>
        /// The one the screen remembers, when that package is still carried. A screen migrated from a
        /// settings file written before ADR 0017 remembers none, and one whose package has been dropped
        /// from the build remembers a name that is gone, so both fall back to matching on kind and size.
        /// </remarks>
        public static string PackageNameFor(ScreenInstance screen, IPackageSource packages, IInstallLog log)
        {
            if (packages == null) return null;
            if (!string.IsNullOrEmpty(screen.Package) && packages.Names.Contains(screen.Package)) return screen.Package;
            var match = PackageCatalogue.From(packages, log).FirstOrDefault(entry =>
                string.Equals(entry.Kind, screen.Kind, StringComparison.Ordinal)
                && entry.Width == screen.Width
                && entry.Height == screen.Height);
            return match?.Package;
        }
    }
}
