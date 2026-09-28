// ScreenInstaller.cs: what one screen's DashTemplates folder is made from, and the two things done to it
// that are not an install -- putting the screen's name back into it, and removing it.
//
// DashboardInstaller writes every folder the rig owns through one routine, the first screen of a size and
// the second alike (#455). This file answers what that routine asks of a screen: which embedded package it
// is written from, and what the package is turned into on the way in -- the folder, the title SimHub lists,
// and for a second screen of a size the namespace its properties are rewritten to.
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

        /// <summary>True when this call actually wrote or removed the folder.</summary>
        public bool Written { get; set; }
    }

    public static class ScreenInstaller
    {
        /// <summary>
        /// What a screen's package becomes on the way into DashTemplates: its folder, its name and its namespace.
        /// </summary>
        /// <remarks>
        /// One answer for every screen, which is the point of it. The first screen of a size holds the
        /// stock namespace, so nothing is rewritten and only the title differs from what is embedded; a
        /// second has a folder of its own and every property reference repointed at its own namespace.
        /// The installer writes both from this, so a start, a reinstall and a press on the Rig tab cannot
        /// disagree about what a screen's folder should hold.
        ///
        /// The folder is the screen's as it stands, so the installer spells a stock screen's folder as its
        /// package does before asking (ScreenInstance.SpellFolderAs); a copy in the settings spelled the way
        /// it was before #374 is otherwise a folder renamed in case at every update (#467).
        /// </remarks>
        public static PackageExtractor.ScreenTarget TargetFor(ScreenInstance screen)
        {
            return new PackageExtractor.ScreenTarget
            {
                Folder = screen.Folder,
                Title = screen.Name,
                FromNamespace = screen.StockNamespace,
                ToNamespace = screen.Namespace,
            };
        }

        /// <summary>
        /// Writes the screen's name into the dashboard SimHub already has, and remembers what it wrote.
        /// </summary>
        /// <remarks>
        /// The repair for a folder whose title is not its screen's name. The installer writes the name
        /// in whenever it writes a screen's folder, but the update path installs what it downloaded under
        /// each package's own folder, title and all, and a folder an older plugin wrote carries whatever
        /// that plugin gave it; either way the screen left SimHub's dashboard list under a name its owner
        /// had never chosen. This runs after them and puts the name back, over a folder that is otherwise
        /// exactly what was shipped.
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
        /// from the build remembers a name that is gone, so both fall back: first to the package whose own
        /// folder the screen holds, which is every stock screen and was the only match the installer made
        /// while it walked the packages rather than the rig, and then to the kind and the size.
        /// </remarks>
        public static string PackageNameFor(ScreenInstance screen, IPackageSource packages, IInstallLog log)
        {
            if (packages == null || screen == null) return null;
            if (!string.IsNullOrEmpty(screen.Package) && packages.Names.Contains(screen.Package)) return screen.Package;
            var catalogue = PackageCatalogue.From(packages, log);
            var match = catalogue.FirstOrDefault(entry => string.Equals(entry.Folder, screen.Folder, StringComparison.OrdinalIgnoreCase))
                ?? catalogue.FirstOrDefault(entry =>
                    string.Equals(entry.Kind, screen.Kind, StringComparison.Ordinal)
                    && entry.Width == screen.Width
                    && entry.Height == screen.Height);
            return match?.Package;
        }
    }
}
