// PackageExtractor.cs: installs a .simhubdash the way SimHub's own importer does (ImportDashWindow):
// extract, find <folder>/<folder>.djson, replace DashTemplates/<folder>, copy missing fonts to DashFonts.
// Framework-only IO with an injected log, so that the tests exercise it on Linux against a fake SimHub root.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace OpenDashPlugin
{
    public static class PackageExtractor
    {
        public const string DashTemplates = "DashTemplates";
        public const string DashFonts = "DashFonts";
        public const string PackageFonts = "_SHFonts";
        public const string DashExtension = ".djson";
        public const string MetadataExtension = ".djson.metadata";
        public const string BackupSuffix = "_backup.zip";

        public static string InstalledFolder(string simHubRoot, string folderName)
        {
            return Path.Combine(simHubRoot, DashTemplates, folderName);
        }

        /// <summary>True when DashTemplates/<folder>/<folder>.djson exists.</summary>
        public static bool IsInstalled(string simHubRoot, string folderName)
        {
            return File.Exists(Path.Combine(InstalledFolder(simHubRoot, folderName), folderName + DashExtension));
        }

        /// <summary>DashTemplates/<folder>/<folder>.djson.metadata, the sidecar that carries DashboardVersion.</summary>
        public static string InstalledSidecar(string simHubRoot, string folderName)
        {
            return Path.Combine(InstalledFolder(simHubRoot, folderName), folderName + MetadataExtension);
        }

        /// <summary>DashboardVersion of the installed dashboard; null when the sidecar is missing or has none.</summary>
        public static string ReadInstalledVersion(string simHubRoot, string folderName)
        {
            return ReadVersionFile(InstalledSidecar(simHubRoot, folderName));
        }

        /// <summary>The dashboard folder inside a package: the top-level directory holding <dir>.djson. Null when none.</summary>
        public static string PackageFolderName(ZipArchive zip)
        {
            foreach (var entry in zip.Entries)
            {
                var parts = entry.FullName.Replace('\\', '/').Split('/');
                if (parts.Length == 2 && string.Equals(parts[1], parts[0] + DashExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return parts[0];
                }
            }
            return null;
        }

        /// <summary>Reads the folder name and the DashboardVersion of a package without extracting it.
        /// folderName is null when the zip is not a dashboard package; the version is null when the sidecar has none.</summary>
        public static string ReadPackageVersion(Stream package, out string folderName)
        {
            using (var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true))
            {
                folderName = PackageFolderName(zip);
                if (folderName == null) return null;
                var wanted = folderName + "/" + folderName + MetadataExtension;
                var entry = zip.Entries.FirstOrDefault(e =>
                    string.Equals(e.FullName.Replace('\\', '/'), wanted, StringComparison.OrdinalIgnoreCase));
                if (entry == null) return null;
                using (var reader = new StreamReader(entry.Open()))
                {
                    return Versioning.ParseDashboardVersion(reader.ReadToEnd());
                }
            }
        }

        /// <summary>Extracts the package into DashTemplates, replacing an existing folder (kept as <folder>_backup.zip),
        /// and copies the package fonts that DashFonts does not have yet. Throws on failure; the caller logs and reports.</summary>
        public static InstallResult Install(Stream package, string simHubRoot, IInstallLog log)
        {
            log = log ?? NullInstallLog.Instance;
            var templates = Path.Combine(simHubRoot, DashTemplates);
            Directory.CreateDirectory(templates);
            var staging = Path.Combine(templates, "_openDash_staging_" + Guid.NewGuid().ToString("N"));
            try
            {
                string folderName;
                using (var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true))
                {
                    folderName = PackageFolderName(zip);
                    if (folderName == null)
                    {
                        throw new InvalidDataException("The package has no <folder>/<folder>.djson entry.");
                    }
                    ExtractSafely(zip, staging);
                }

                var extracted = Path.Combine(staging, folderName);
                var target = Path.Combine(templates, folderName);
                var result = new InstallResult
                {
                    FolderName = folderName,
                    Version = ReadVersionFile(Path.Combine(extracted, folderName + MetadataExtension)),
                };

                if (Directory.Exists(target))
                {
                    result.BackupPath = Backup(target, Path.Combine(templates, folderName + BackupSuffix), log);
                    DeleteDirectory(target);
                }
                Directory.Move(extracted, target);
                log.Info("Installed " + folderName + " " + (result.Version ?? "(no version)") + " into " + target);

                result.FontsCopied = CopyFonts(Path.Combine(target, PackageFonts), Path.Combine(simHubRoot, DashFonts), log);
                return result;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(staging)) Directory.Delete(staging, true);
                }
                catch (Exception ex)
                {
                    log.Warn("Could not remove the staging folder " + staging + ": " + ex.Message);
                }
            }
        }

        /// <summary>Puts back the copy Install set aside, and reports whether there was one to put back.</summary>
        /// <remarks>
        /// Install writes <folder>_backup.zip before it replaces a folder, and until this existed nothing read it,
        /// so "the previous package survives" was true and "it can be put back" was not. The backup stores entries
        /// relative to the folder rather than under it, which is what ZipFile.CreateFromDirectory writes and the
        /// opposite of a .simhubdash, so it extracts straight into the target.
        ///
        /// The folder being restored over is not backed up in its turn. A restore is the undo, and an undo that
        /// leaves its own thing to undo is a worse answer than one that does not.
        /// </remarks>
        public static bool Restore(string simHubRoot, string folderName, IInstallLog log)
        {
            log = log ?? NullInstallLog.Instance;
            var templates = Path.Combine(simHubRoot, DashTemplates);
            var backupPath = Path.Combine(templates, folderName + BackupSuffix);
            if (!File.Exists(backupPath))
            {
                log.Warn("No previous copy of " + folderName + " was kept, so there is nothing to put back.");
                return false;
            }

            var target = Path.Combine(templates, folderName);
            var staging = Path.Combine(templates, "_openDash_restore_" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var zip = ZipFile.OpenRead(backupPath))
                {
                    ExtractSafely(zip, staging);
                }
                if (!File.Exists(Path.Combine(staging, folderName + DashExtension)))
                {
                    throw new InvalidDataException("The kept copy of " + folderName + " has no " + folderName + DashExtension + ".");
                }
                if (Directory.Exists(target)) DeleteDirectory(target);
                Directory.Move(staging, target);
                log.Info("Put back the previous copy of " + folderName + " from " + backupPath);
                return true;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(staging)) Directory.Delete(staging, true);
                }
                catch (Exception ex)
                {
                    log.Warn("Could not remove the staging folder " + staging + ": " + ex.Message);
                }
            }
        }

        private static string ReadVersionFile(string path)
        {
            if (!File.Exists(path)) return null;
            return Versioning.ParseDashboardVersion(File.ReadAllText(path));
        }

        /// <summary>Extracts every entry under root, refusing entries that would land outside it.</summary>
        private static void ExtractSafely(ZipArchive zip, string root)
        {
            Directory.CreateDirectory(root);
            var rootFull = Path.GetFullPath(root);
            if (!rootFull.EndsWith(Path.DirectorySeparatorChar.ToString())) rootFull += Path.DirectorySeparatorChar;
            foreach (var entry in zip.Entries)
            {
                var relative = entry.FullName.Replace('\\', '/');
                if (relative.Length == 0) continue;
                var destination = Path.GetFullPath(Path.Combine(rootFull, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!destination.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The package entry " + entry.FullName + " points outside the package.");
                }
                if (relative.EndsWith("/"))
                {
                    Directory.CreateDirectory(destination);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                entry.ExtractToFile(destination, true);
            }
        }

        private static string Backup(string folder, string backupPath, IInstallLog log)
        {
            try
            {
                if (File.Exists(backupPath)) File.Delete(backupPath);
                ZipFile.CreateFromDirectory(folder, backupPath);
                log.Info("Previous copy kept as " + backupPath);
                return backupPath;
            }
            catch (Exception ex)
            {
                log.Warn("Could not back up " + folder + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Deletes recursively, retrying once: SimHub may still hold a file of a dashboard it just released.</summary>
        private static void DeleteDirectory(string folder)
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException)
            {
                Thread.Sleep(250);
                Directory.Delete(folder, true);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(250);
                Directory.Delete(folder, true);
            }
        }

        /// <summary>Copies the package's TTFs into DashFonts unless a file with the same name or the same bytes is there,
        /// which is the rule SimHub's importer applies. Returns the number of files copied.</summary>
        public static int CopyFonts(string packageFontsFolder, string dashFontsFolder, IInstallLog log)
        {
            log = log ?? NullInstallLog.Instance;
            if (!Directory.Exists(packageFontsFolder)) return 0;
            Directory.CreateDirectory(dashFontsFolder);
            var existing = Directory.GetFiles(dashFontsFolder, "*.ttf");
            var copied = 0;
            foreach (var font in Directory.GetFiles(packageFontsFolder, "*.ttf"))
            {
                var name = Path.GetFileName(font);
                var destination = Path.Combine(dashFontsFolder, name);
                if (File.Exists(destination)) continue;
                if (existing.Any(other => SameBytes(other, font))) continue;
                File.Copy(font, destination, false);
                copied++;
                log.Info("Installed font " + name);
            }
            return copied;
        }

        private static bool SameBytes(string a, string b)
        {
            try
            {
                var infoA = new FileInfo(a);
                var infoB = new FileInfo(b);
                if (infoA.Length != infoB.Length) return false;
                return File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b));
            }
            catch
            {
                return false;
            }
        }
    }
}
