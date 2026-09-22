// PackageExtractor.cs: installs a .simhubdash the way SimHub's own importer does (ImportDashWindow):
// extract, find <folder>/<folder>.djson, replace DashTemplates/<folder>, copy missing fonts to DashFonts.
// Framework-only IO with an injected log, so that the tests exercise it on Linux against a fake SimHub root.
using System;
using System.Collections.Generic;
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

        /// <summary>Prefix of the copy kept when a folder somebody edited is replaced; never reclaimed.</summary>
        public const string EditedSuffix = "_yours_";

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

        /// <summary>
        /// Turns a freshly extracted package into one screen's copy of it, in the staging folder.
        /// </summary>
        /// <remarks>
        /// Three things move, and only three. SimHub finds a dashboard as <c>&lt;folder&gt;/&lt;folder&gt;.djson</c>, so the
        /// folder and the main dashboard are renamed together; the sub-dashboards are referenced by bare
        /// file name and are left exactly as they are. The Title is written because SimHub's dashboard
        /// list shows it, and two screens of one size were otherwise two entries a user could not tell
        /// apart. Then every <c>OpenDash.&lt;namespace&gt;</c> in every .djson becomes this screen's.
        ///
        /// The rewrite is checked rather than trusted: a package that still mentions the namespace it
        /// came from, or that gained a different number of references than it lost, is a package that
        /// would silently read another screen's settings. That is the failure this whole mechanism
        /// exists to prevent, so it throws rather than installing.
        /// </remarks>
        /// <returns>The folder name inside staging after the rename.</returns>
        public static string Instantiate(string staging, string folderName, ScreenTarget screen, IInstallLog log)
        {
            log = log ?? NullInstallLog.Instance;
            var wanted = string.IsNullOrWhiteSpace(screen.Folder) ? folderName : screen.Folder.Trim();
            var source = Path.Combine(staging, folderName);

            if (!string.Equals(wanted, folderName, StringComparison.Ordinal))
            {
                var renamed = Path.Combine(staging, wanted);
                Directory.Move(source, renamed);
                source = renamed;
                foreach (var suffix in new[] { DashExtension, MetadataExtension })
                {
                    var from = Path.Combine(source, folderName + suffix);
                    if (File.Exists(from)) File.Move(from, Path.Combine(source, wanted + suffix));
                }
            }

            if (!string.IsNullOrWhiteSpace(screen.Title)) WriteTitle(source, wanted, screen.Title, log);
            if (screen.Rewrites) RewriteNamespace(source, screen, log);
            return wanted;
        }

        /// <summary>The token a property carries in a built binding: "OpenDash." and the screen's namespace.</summary>
        private static string Token(string ns)
        {
            return Contract.Prefix + "." + ns;
        }

        /// <summary>
        /// Repoints every property reference in the package at this screen's namespace.
        /// </summary>
        /// <remarks>
        /// A plain string replacement, and it is safe because the token is distinctive: a binding reads
        /// <c>[OpenDash.Face1280x480ZoneA]</c>, and "OpenDash.Face1280x480" cannot be a prefix of any
        /// other screen's property. Nothing is parsed, so nothing about the scene graph can be disturbed
        /// by it -- no box moves and no text is re-measured, which is what keeps the textFit guarantees
        /// true of the copy.
        /// </remarks>
        private static void RewriteNamespace(string folder, ScreenTarget screen, IInstallLog log)
        {
            var from = Token(screen.FromNamespace);
            var to = Token(screen.ToNamespace);
            var rewritten = 0;
            var files = 0;
            foreach (var path in Directory.GetFiles(folder, "*" + DashExtension, SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(path);
                var before = Occurrences(text, from);
                if (before == 0) continue;
                var already = Occurrences(text, to);
                var updated = text.Replace(from, to);
                var after = Occurrences(updated, to);
                if (Occurrences(updated, from) != 0 || after != already + before)
                {
                    throw new InvalidDataException(
                        "Rewriting " + Path.GetFileName(path) + " for " + screen.ToNamespace + " did not account for every reference: "
                        + before + " to replace, " + (after - already) + " replaced. The copy was not installed.");
                }
                File.WriteAllText(path, updated);
                rewritten += before;
                files++;
            }
            if (rewritten == 0)
            {
                throw new InvalidDataException(
                    "The package mentions " + from + " nowhere, so a copy of it would read " + screen.FromNamespace
                    + "'s settings rather than " + screen.ToNamespace + "'s. The copy was not installed.");
            }
            log.Info("Pointed " + rewritten + " references in " + files + " file(s) at " + screen.ToNamespace + ".");
        }

        private static int Occurrences(string text, string token)
        {
            var count = 0;
            var at = text.IndexOf(token, StringComparison.Ordinal);
            while (at >= 0)
            {
                count++;
                at = text.IndexOf(token, at + token.Length, StringComparison.Ordinal);
            }
            return count;
        }

        /// <summary>
        /// Writes the screen's name into the dashboard's Title, in both places SimHub reads one.
        /// </summary>
        /// <remarks>
        /// The .metadata sidecar is what the dashboard list reads; the copy inside the .djson is what
        /// Dash Studio shows once it is open. They are edited as text rather than parsed and rewritten,
        /// because reserialising a scene graph through a JSON library we do not control is how
        /// formatting, number precision and property order quietly change underneath SimHub.
        /// </remarks>
        private static void WriteTitle(string folder, string folderName, string title, IInstallLog log)
        {
            foreach (var name in new[] { folderName + MetadataExtension, folderName + DashExtension })
            {
                var path = Path.Combine(folder, name);
                if (!File.Exists(path)) continue;
                var text = File.ReadAllText(path);
                var replaced = ReplaceFirstJsonString(text, "\"Title\":", title);
                if (replaced == null)
                {
                    log.Warn("No Title in " + name + "; SimHub will list this screen under its folder name.");
                    continue;
                }
                File.WriteAllText(path, replaced);
            }
        }

        /// <summary>Replaces the first JSON string value after a key, or null when the key is not there.</summary>
        private static string ReplaceFirstJsonString(string text, string key, string value)
        {
            var at = text.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return null;
            var open = text.IndexOf('"', at + key.Length);
            if (open < 0) return null;
            var close = open + 1;
            while (close < text.Length && text[close] != '"')
            {
                if (text[close] == '\\') close++;
                close++;
            }
            if (close >= text.Length) return null;
            return text.Substring(0, open + 1) + Escape(value) + text.Substring(close);
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
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

        /// <summary>
        /// What a package is being turned into: one screen's folder, name and namespace.
        /// </summary>
        /// <remarks>
        /// Null for the stock install, which writes the package byte for byte under its own folder. A
        /// second screen at a size needs its own copy, because a package carries its property names as
        /// literals and SimHub properties are global; ADR 0017 records why that is the only shape a fix
        /// can take, and why a rewrite of one distinctive token in an already-built scene graph is not
        /// the local regeneration ADR 0011 refuses.
        /// </remarks>
        public sealed class ScreenTarget
        {
            /// <summary>The folder to write under DashTemplates, e.g. "OpenDash Rim".</summary>
            public string Folder { get; set; }

            /// <summary>The screen's name, which becomes the dashboard's Title in SimHub's own list.</summary>
            public string Title { get; set; }

            /// <summary>The namespace the package was built with, e.g. "Face1280x480".</summary>
            public string FromNamespace { get; set; }

            /// <summary>The namespace this screen owns, e.g. "Rim".</summary>
            public string ToNamespace { get; set; }

            /// <summary>Whether anything actually has to be rewritten.</summary>
            public bool Rewrites
            {
                get
                {
                    return !string.IsNullOrEmpty(FromNamespace)
                        && !string.IsNullOrEmpty(ToNamespace)
                        && !string.Equals(FromNamespace, ToNamespace, StringComparison.Ordinal);
                }
            }
        }

        /// <summary>Extracts the package into DashTemplates, replacing an existing folder (kept as <folder>_backup.zip),
        /// and copies the package fonts that DashFonts does not have yet. Throws on failure; the caller logs and reports.</summary>
        /// <param name="holdsAuthoredWork">
        /// True when the folder being replaced is one somebody has edited, so the copy set aside must outlive the
        /// next install rather than being reclaimed by it.
        /// </param>
        /// <param name="screen">
        /// The screen this copy belongs to, or null to write the package as it is embedded.
        /// </param>
        public static InstallResult Install(Stream package, string simHubRoot, IInstallLog log, bool holdsAuthoredWork = false, ScreenTarget screen = null)
        {
            log = log ?? NullInstallLog.Instance;
            var templates = Path.Combine(simHubRoot, DashTemplates);
            Directory.CreateDirectory(templates);
            var staging = Path.Combine(templates, StagingPrefix + Guid.NewGuid().ToString("N"));
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

                if (screen != null) folderName = Instantiate(staging, folderName, screen, log);
                var extracted = Path.Combine(staging, folderName);
                var target = Path.Combine(templates, folderName);
                var result = new InstallResult
                {
                    FolderName = folderName,
                    Version = ReadVersionFile(Path.Combine(extracted, folderName + MetadataExtension)),
                };

                if (Directory.Exists(target))
                {
                    // Where the copy goes decides whether it survives. The routine backup is reclaimed by the next
                    // install of the same folder, which is the ordinary one-deep undo. Work somebody authored is not
                    // ordinary: it is kept under a name no later install can claim, because the consent to replace
                    // it was given on the promise that a copy is kept, and a promise the next upgrade quietly breaks
                    // is worse than no promise.
                    var backupPath = holdsAuthoredWork
                        ? Path.Combine(templates, folderName + EditedSuffix + Stamp() + ".zip")
                        : Path.Combine(templates, folderName + BackupSuffix);
                    result.BackupPath = Backup(target, backupPath, log);
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
        /// <summary>Prefix of the folder an install extracts into before moving it into place.</summary>
        public const string StagingPrefix = "_OpenDash_staging_";

        /// <summary>
        /// Removes staging folders an earlier install did not clean up, and reports how many.
        /// </summary>
        /// <remarks>
        /// Install extracts into DashTemplates/_OpenDash_staging_&lt;guid&gt; and removes it in a finally. That finally
        /// does not run when the process exits, because a thread-pool thread is a background thread the CLR
        /// terminates without unwinding, so every update abandoned by a SimHub that closed mid-install leaves a
        /// complete extracted dashboard behind. They accumulate, they are invisible, and nothing else would ever
        /// remove them. A folder with this prefix is OpenDash's own working space and is never a user's dashboard.
        /// </remarks>
        public static int RemoveOrphanedStaging(string simHubRoot, IInstallLog log)
        {
            log = log ?? NullInstallLog.Instance;
            var templates = Path.Combine(simHubRoot ?? string.Empty, DashTemplates);
            if (!Directory.Exists(templates)) return 0;
            var removed = 0;
            foreach (var folder in Directory.GetDirectories(templates, StagingPrefix + "*"))
            {
                try
                {
                    Directory.Delete(folder, true);
                    removed++;
                    log.Info("Removed a staging folder an interrupted install left behind: " + Path.GetFileName(folder));
                }
                catch (Exception ex)
                {
                    log.Warn("Could not remove " + folder + ": " + ex.Message);
                }
            }
            return removed;
        }

        /// <summary>
        /// Every copy of this folder that could be put back, newest first: the copies kept when somebody's edited
        /// work was replaced, and then the ordinary one-deep backup.
        /// </summary>
        public static IReadOnlyList<string> KeptCopies(string simHubRoot, string folderName)
        {
            var templates = Path.Combine(simHubRoot, DashTemplates);
            if (string.IsNullOrEmpty(folderName) || !Directory.Exists(templates)) return new string[0];
            var kept = Directory
                .GetFiles(templates, folderName + EditedSuffix + "*.zip")
                .OrderByDescending(path => path, StringComparer.Ordinal)
                .ToList();
            var ordinary = Path.Combine(templates, folderName + BackupSuffix);
            if (File.Exists(ordinary)) kept.Add(ordinary);
            return kept;
        }

        /// <param name="from">Which copy to put back; the newest kept one when omitted.</param>
        public static bool Restore(string simHubRoot, string folderName, IInstallLog log, string from = null)
        {
            log = log ?? NullInstallLog.Instance;
            var templates = Path.Combine(simHubRoot, DashTemplates);
            var backupPath = from ?? KeptCopies(simHubRoot, folderName).FirstOrDefault() ?? Path.Combine(templates, folderName + BackupSuffix);
            if (!File.Exists(backupPath))
            {
                log.Warn("No previous copy of " + folderName + " was kept, so there is nothing to put back.");
                return false;
            }

            var target = Path.Combine(templates, folderName);
            var staging = Path.Combine(templates, "_OpenDash_restore_" + Guid.NewGuid().ToString("N"));
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

        /// <summary>
        /// Sets the existing folder aside. Throws when it cannot, which stops the install.
        /// </summary>
        /// <remarks>
        /// It used to warn and return null, and the caller deleted the folder regardless, so the one case the backup
        /// exists for, a disk that is full or a file that is locked, was also the case where it silently did nothing
        /// and the dashboard went anyway. Failing here leaves the installed dashboard where it is, which is the whole
        /// point of taking a copy first.
        /// </remarks>
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
                log.Error("Could not set aside the copy of " + folder + ", so it was left as it is: " + ex.Message);
                throw new IOException("Could not set aside the existing " + Path.GetFileName(folder) + ": " + ex.Message, ex);
            }
        }

        /// <summary>A stamp that sorts, so a person can tell which copy is which without opening them.</summary>
        private static string Stamp() => DateTime.Now.ToString("yyyyMMdd-HHmmss");

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
