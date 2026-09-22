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
        /// <param name="holdsAuthoredWork">
        /// True when the folder about to be replaced is one somebody has edited, so the copy set aside
        /// outlives the next install rather than being reclaimed by it. The panel passes what its own
        /// fingerprint check found; the startup path, which only ever writes a folder that is missing,
        /// has nothing to replace and leaves it false.
        /// </param>
        public static ScreenInstallResult Write(
            ScreenInstance screen,
            IPackageSource packages,
            string simHubRoot,
            IFolderRecord record,
            IInstallLog log,
            bool force = false,
            bool holdsAuthoredWork = false)
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
                    PackageExtractor.Install(package, simHubRoot, log, holdsAuthoredWork, new PackageExtractor.ScreenTarget
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
        /// Writes the screen's name into the dashboard SimHub already has, and remembers what it wrote.
        /// </summary>
        /// <remarks>
        /// The repair for the one thing Write does not own: a stock screen's folder belongs to a package
        /// and DashboardInstaller writes it byte for byte, title and all, so every update replaced the
        /// driver's name for that screen with the package's and the screen left SimHub's dashboard list
        /// under a name its owner had never chosen. This runs after the installer has had its turn and
        /// puts the name back, over a folder that is otherwise exactly what was shipped.
        ///
        /// Only over a folder we can vouch for, which is what makes it safe to run on every start. A
        /// folder whose fingerprint has moved holds somebody's own work -- their own title, quite
        /// possibly -- and editing one string of it and then recording the result as ours would turn
        /// "ask before replacing your work" into replacing it silently at the next update. There is
        /// nothing to repair there in any case: the reinstall on the Edit panel writes the whole folder.
        /// </remarks>
        /// <returns>Whether the title on disk changed.</returns>
        public static bool Retitle(ScreenInstance screen, string simHubRoot, IFolderRecord record, IInstallLog log)
        {
            log = log ?? NullInstallLog.Instance;
            if (screen == null || string.IsNullOrEmpty(screen.Folder)) return false;
            try
            {
                var folder = PackageExtractor.InstalledFolder(simHubRoot, screen.Folder);
                var recorded = record == null ? null : record.Get(screen.Folder);
                // Null on either side is "we cannot vouch for this", which is the same bias
                // PackageStatus.Edited takes and for the same reason.
                if (recorded == null || !string.Equals(recorded, FolderFingerprint.Of(folder), StringComparison.Ordinal))
                {
                    return false;
                }
                if (!PackageExtractor.Retitle(simHubRoot, screen.Folder, screen.Name, log)) return false;
                // Recorded again, because the fingerprint taken at install is of the package's title.
                // Leaving it would make every renamed screen read as somebody's own work from the next
                // start on, and the update path would hold it back for ever rather than ask.
                record.Set(screen.Folder, FolderFingerprint.Of(folder));
                log.Info("Listed " + screen.Folder + " under " + screen.Name + " again.");
                return true;
            }
            catch (Exception ex)
            {
                log.Warn("Could not write the name " + screen.Name + " into " + screen.Folder + ": " + ex.Message);
                return false;
            }
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
