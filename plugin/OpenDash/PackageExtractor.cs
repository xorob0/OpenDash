// PackageExtractor.cs: installs a .simhubdash the way SimHub's own importer does (ImportDashWindow):
// extract, find <folder>/<folder>.djson, replace DashTemplates/<folder>, copy its fonts to DashFonts.
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

        /// <summary>DashTemplates/<folder>/<folder>.djson, the file SimHub loads a dashboard from.</summary>
        public static string InstalledDashboard(string simHubRoot, string folderName)
        {
            return Path.Combine(InstalledFolder(simHubRoot, folderName), folderName + DashExtension);
        }

        /// <summary>True when DashTemplates/<folder>/<folder>.djson exists.</summary>
        public static bool IsInstalled(string simHubRoot, string folderName)
        {
            return File.Exists(InstalledDashboard(simHubRoot, folderName));
        }

        /// <summary>
        /// Every folder in DashTemplates that holds its dashboard (IsInstalled), by name, compared as Windows
        /// compares paths. The plugin takes this once, when Init has finished writing the rig's folders: the
        /// templates SimHub loaded at startup, which it does not read again (docs/dev-loop.md).
        /// </summary>
        public static ISet<string> InstalledFolders(string simHubRoot)
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var root = Path.Combine(simHubRoot, DashTemplates);
            if (!Directory.Exists(root)) return found;
            foreach (var folder in Directory.GetDirectories(root))
            {
                var name = Path.GetFileName(folder);
                if (IsInstalled(simHubRoot, name)) found.Add(name);
            }
            return found;
        }

        /// <summary>
        /// Whether a screen's dashboard waits for SimHub to restart: its folder is in DashTemplates now and was
        /// not when SimHub loaded its templates, so this session created it. Null when either fact is unknown.
        /// </summary>
        /// <remarks>
        /// It was the folder's and the .djson's write time against the process's start plus two minutes, which
        /// was wrong both ways: a dashboard saved in Dash Studio, Reinstall or a screen's Edit rewrites the
        /// .djson, and the screen read "not in SimHub yet" while SimHub listed it; and a screen added in the
        /// first two minutes -- a first run -- was not flagged at all. A folder SimHub loaded is loaded however
        /// often it is rewritten, and one it did not load is not, however soon it was made.
        /// </remarks>
        public static bool? WaitsForRestart(ISet<string> installedAtStart, string folderName, bool? installedNow)
        {
            if (installedNow == false) return false;
            if (installedAtStart == null || folderName == null || installedNow == null) return null;
            return !installedAtStart.Contains(folderName);
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
        /// Three things move, and only three. SimHub finds a dashboard as <c>&lt;folder&gt;/&lt;folder&gt;.djson</c> and
        /// everything that belongs to it by that file's name, so the folder, the main dashboard and every file
        /// named after it are renamed together; the sub-dashboards are referenced by bare file name and are
        /// left exactly as they are, their own sidecars with them. The Title is written because SimHub's dashboard
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
                Rename(source, renamed, Directory.Move);
                source = renamed;
                foreach (var file in FilesNamedAfter(source, folderName))
                {
                    var suffix = Path.GetFileName(file).Substring(folderName.Length);
                    Rename(file, Path.Combine(source, wanted + suffix), File.Move);
                }
            }

            if (!string.IsNullOrWhiteSpace(screen.Title)) WriteTitle(source, wanted, screen.Title, log);
            if (screen.Rewrites) RewriteNamespace(source, screen, log);
            return wanted;
        }

        /// <summary>
        /// The main dashboard and every file SimHub finds beside it by its name: <c>&lt;name&gt;.djson</c> and
        /// <c>&lt;name&gt;.djson.*</c>.
        /// </summary>
        /// <remarks>
        /// SimHub keeps no list of sidecars. Each one is the .djson's own path with something appended: the
        /// .metadata the dashboard list reads, the .jpg or .png it draws as the thumbnail, the .ressources zip
        /// the images are read from, the .carclasses overrides. Dash Studio's own clean-up reads a file the same
        /// way, taking whatever comes before ".djson." as the dashboard it belongs to. Renaming by that rule
        /// rather than by a list of suffixes is what keeps a copy whole when the build ships a sidecar it did
        /// not ship before: the list this replaced named two, and a second screen of a size was written with
        /// its thumbnail and its images still under the package's name, where SimHub never looks (#456).
        ///
        /// Case is ignored because SimHub finds these files through File.Exists on Windows, which ignores it.
        /// Only the top of the folder is read, because nothing SimHub resolves by name lives below it, and a
        /// widget's sidecars are named after the widget, so they stay where they are with it.
        /// </remarks>
        private static List<string> FilesNamedAfter(string folder, string dashboard)
        {
            var main = dashboard + DashExtension;
            return Directory
                .GetFiles(folder)
                .Where(path =>
                {
                    var name = Path.GetFileName(path);
                    return name.Equals(main, StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith(main + ".", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();
        }

        /// <summary>
        /// Renames a file or folder, including the case-only rename Windows refuses to do in one step.
        /// </summary>
        /// <remarks>
        /// A rig upgraded across #374 carries its folders spelled "openDash 850x480" in the settings
        /// while the package's own is "OpenDash 850x480", and Windows treats those as one path:
        /// Directory.Move compares them without case and throws "Source and destination path must be
        /// different" rather than doing the rename. The reinstall on the Edit panel is where that landed
        /// -- the press that is supposed to be the repair, failing on exactly the rigs that need it.
        ///
        /// Going through a name neither of them holds is what makes a case change a move Windows will
        /// perform. It is inside the staging folder, so nothing SimHub reads is ever called by it.
        /// </remarks>
        private static void Rename(string from, string to, Action<string, string> move)
        {
            if (string.Equals(from, to, StringComparison.Ordinal)) return;
            if (!string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            {
                move(from, to);
                return;
            }
            var through = to + ".case" + Guid.NewGuid().ToString("N");
            move(from, through);
            move(through, to);
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
        /// <returns>Whether either file actually changed.</returns>
        private static bool WriteTitle(string folder, string folderName, string title, IInstallLog log)
        {
            var changed = false;
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
                // Compared rather than written blind, because Retitle runs over every screen on every
                // start and the caller re-fingerprints whatever this touches. A write that changes
                // nothing would still be a folder to hash again, on a rig, at startup.
                if (string.Equals(replaced, text, StringComparison.Ordinal)) continue;
                File.WriteAllText(path, replaced);
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Puts a screen's name back into the dashboard already installed under a folder.
        /// </summary>
        /// <remarks>
        /// A stock screen's folder is a package's own and is written byte for byte, so it carries the
        /// title the design gave the package. A driver who renamed one got that title back at the next
        /// update or reinstall, and their screen left SimHub's dashboard list under the name they knew
        /// it by -- which looks exactly like the dashboard disappearing. The name lives in the settings
        /// file, so it can simply be written again afterwards, over a folder that is otherwise the
        /// package's own.
        ///
        /// The caller records the folder again when this returns true: the fingerprint taken at install
        /// is of the package's title, and leaving it would make every renamed screen read as somebody's
        /// own work and be held back from every future update.
        /// </remarks>
        /// <returns>Whether the folder was there and its title changed.</returns>
        public static bool Retitle(string simHubRoot, string folderName, string title, IInstallLog log)
        {
            log = log ?? NullInstallLog.Instance;
            if (string.IsNullOrWhiteSpace(folderName) || string.IsNullOrWhiteSpace(title)) return false;
            var folder = InstalledFolder(simHubRoot, folderName);
            if (!Directory.Exists(folder)) return false;
            return WriteTitle(folder, folderName, title, log);
        }

        /// <summary>Replaces the JSON string value of the first occurrence of a key, or null when the key is not there
        /// or its value is not a string.</summary>
        /// <remarks>
        /// Only whitespace may stand between the key and the quote that opens its value. Taking the next quote
        /// wherever it was turned <c>"Title":null,"X":"y"</c> into <c>"Title":null,"Rim":"y"</c>, the name of the
        /// key after it overwritten (#620); a Title that is not a string is now reported as no Title, which is
        /// what it is.
        /// </remarks>
        private static string ReplaceFirstJsonString(string text, string key, string value)
        {
            var at = text.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return null;
            var open = at + key.Length;
            while (open < text.Length && char.IsWhiteSpace(text[open])) open++;
            if (open >= text.Length || text[open] != '"') return null;
            var close = open + 1;
            while (close < text.Length && text[close] != '"')
            {
                if (text[close] == '\\') close++;
                close++;
            }
            if (close >= text.Length) return null;
            return text.Substring(0, open + 1) + Escape(value) + text.Substring(close);
        }

        /// <summary>A string as the inside of a JSON string literal.</summary>
        /// <remarks>
        /// A control character has to be escaped as well as the quote and the backslash: JSON forbids one raw
        /// inside a string, and a name with a tab in it was otherwise written into a file no strict reader accepts
        /// (#620).
        /// </remarks>
        private static string Escape(string value)
        {
            var escaped = new System.Text.StringBuilder(value.Length + 8);
            foreach (var c in value)
            {
                if (c == '\\' || c == '"') escaped.Append('\\').Append(c);
                else if (c < ' ') escaped.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                else escaped.Append(c);
            }
            return escaped.ToString();
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
        /// and copies the package fonts that DashFonts does not have in those bytes. Throws on failure; the caller logs and reports.</summary>
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

        /// <summary>Where a face replaced in DashFonts is kept: the folder SimHub itself moves the fonts it retires to, and
        /// one that neither SimHub's font list nor WPF looks inside.</summary>
        public const string FontBackups = "_Backups";

        /// <summary>
        /// Copies the package's TTFs into DashFonts, replacing a face of the same name whose bytes differ, and returns
        /// how many files it wrote.
        /// </summary>
        /// <remarks>
        /// A face whose bytes are already there, under any name, is skipped, which is half of the rule SimHub's
        /// importer applies and keeps DashFonts free of the duplicates SimHub would otherwise set aside at its next
        /// start. The other half, skipping any face whose name is taken, is not followed. It meant that a face whose
        /// bytes change under its name never reached a rig that had the old one, and the faces have changed under
        /// their names before (#159); every text of every package is measured against the faces that package
        /// carries, so a rig drawing an older build of one clips glyphs the tests say fit, silently.
        ///
        /// The face being replaced is moved into DashFonts/_Backups rather than overwritten. A file of that name is
        /// not necessarily ours -- Barlow is a public family, and another dashboard may have brought its own build
        /// of it -- and _Backups is where SimHub itself keeps the fonts it retires. When the move is refused the
        /// older face is left in place and the next install tries again, since a face that could not be replaced
        /// is still a face that draws. A running SimHub does not stand in the way: on the VM a face that an open
        /// dashboard was drawing from was overwritten, and moved out and back, without complaint.
        ///
        /// What is written is stamped with the time it was written. Every entry of a package carries the same
        /// fixed time, and the caches in front of a font file are keyed on its path, size and time -- SimHub's
        /// FontsAnalyzer names and DirectWrite's file references both -- so a replacement at the same path with
        /// the same time and, for a face whose names were rewritten in place, the same size would be taken for
        /// the file it replaced. The stamp is also how <see cref="FacesWrittenSince"/> tells that an install
        /// wrote a face at all.
        /// </remarks>
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
                if (existing.Any(other => SameBytes(other, font))) continue;
                var replacing = File.Exists(destination);
                if (replacing && !SetFaceAside(destination, dashFontsFolder, log)) continue;
                File.Copy(font, destination, false);
                File.SetLastWriteTimeUtc(destination, DateTime.UtcNow);
                copied++;
                log.Info((replacing ? "Replaced font " : "Installed font ") + name);
            }
            return copied;
        }

        /// <summary>
        /// How many faces in DashFonts were written at or after <paramref name="sinceUtc"/>, which is how a caller
        /// tells that the install it just ran put a font into a running SimHub.
        /// </summary>
        /// <remarks>
        /// That is worth knowing because SimHub cannot draw such a face until it restarts (#441). It reads
        /// DashFonts once per run, through a DirectWrite font collection that is kept for the life of the process
        /// and that its font refresh does not rebuild, so a face written afterwards is drawn by no dashboard,
        /// however often it is reopened. Read off the times CopyFonts stamps, so that a face replaced under its
        /// own name counts as well as a new one.
        /// </remarks>
        public static int FacesWrittenSince(string simHubRoot, DateTime sinceUtc)
        {
            var folder = Path.Combine(simHubRoot ?? string.Empty, DashFonts);
            if (!Directory.Exists(folder)) return 0;
            return Directory.GetFiles(folder, "*.ttf").Count(face => File.GetLastWriteTimeUtc(face) >= sinceUtc);
        }

        /// <summary>Moves a face out of DashFonts into its _Backups folder, and reports whether it could.</summary>
        private static bool SetFaceAside(string face, string dashFontsFolder, IInstallLog log)
        {
            var name = Path.GetFileName(face);
            try
            {
                var backups = Path.Combine(dashFontsFolder, FontBackups);
                Directory.CreateDirectory(backups);
                var kept = Path.Combine(backups, name);
                // One copy per name, which is how SimHub keeps the fonts it moves there itself.
                if (File.Exists(kept)) File.Delete(kept);
                File.Move(face, kept);
                log.Info("Set the older " + name + " aside in " + kept);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                log.Warn("Kept the older " + name + ", which could not be moved (" + ex.Message + "); the next install replaces it.");
                return false;
            }
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
