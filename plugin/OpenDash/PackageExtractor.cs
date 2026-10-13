// PackageExtractor.cs: installs a .simhubdash the way SimHub's own importer does (ImportDashWindow):
// extract, find <folder>/<folder>.djson, replace DashTemplates/<folder>, copy its fonts to DashFonts.
// Framework-only IO with an injected log, so that the tests exercise it on Linux against a fake SimHub root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace OpenDashPlugin
{
    /// <summary>
    /// The two things done to a whole dashboard folder under DashTemplates: moving it and deleting it.
    /// </summary>
    /// <remarks>
    /// An interface so that a test can refuse one where Windows would. Windows will not rename or delete a folder
    /// while a file in it is held open, and Linux, where the tests run, does both, so a held file cannot be staged
    /// there with a handle; a test hands the extractor one of these that throws instead.
    /// </remarks>
    public interface IFolderMoves
    {
        /// <summary>Directory.Move.</summary>
        void Move(string from, string to);

        /// <summary>A recursive Directory.Delete.</summary>
        void Delete(string folder);
    }

    /// <summary>The disk itself.</summary>
    public sealed class DiskFolders : IFolderMoves
    {
        public static readonly DiskFolders Instance = new DiskFolders();

        public void Move(string from, string to) => Directory.Move(from, to);

        public void Delete(string folder) => Directory.Delete(folder, true);
    }

    public static class PackageExtractor
    {
        public const string DashTemplates = "DashTemplates";
        public const string DashFonts = "DashFonts";
        public const string PackageFonts = "_SHFonts";
        public const string DashExtension = ".djson";
        public const string MetadataExtension = ".djson.metadata";
        public const string BackupSuffix = "_backup.zip";

        /// <summary>Prefix of the copy kept when a folder somebody edited is replaced. No install reclaims it; putting it
        /// back uses it up (Restore).</summary>
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
        /// <param name="disk">
        /// How the folder is moved and deleted; the disk itself when omitted. The tests hand in one that refuses a move
        /// where Windows would.
        /// </param>
        public static InstallResult Install(Stream package, string simHubRoot, IInstallLog log, bool holdsAuthoredWork = false, ScreenTarget screen = null, IFolderMoves disk = null)
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

                // Where the copy goes decides whether it survives (BackupPathFor).
                var keepAs = Directory.Exists(target) ? BackupPathFor(templates, folderName, holdsAuthoredWork) : null;
                result.BackupPath = Replace(extracted, target, keepAs, disk ?? DiskFolders.Instance, log);
                log.Info("Installed " + folderName + " " + (result.Version ?? "(no version)") + " into " + target);

                // The folder is in by now, so a font that cannot be copied is reported on the result rather than thrown:
                // a caller told "failed" would take the folder for the one that was there before (#588).
                try
                {
                    result.FontsCopied = CopyFonts(Path.Combine(target, PackageFonts), Path.Combine(simHubRoot, DashFonts), log);
                }
                catch (Exception ex)
                {
                    result.FontsError = ex.Message;
                    log.Error("Installed " + folderName + ", but could not copy its fonts into " + DashFonts + ": " + ex.Message);
                }
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

        /// <summary>Prefix of the folder an install extracts into before moving it into place.</summary>
        public const string StagingPrefix = "_OpenDash_staging_";

        /// <summary>Suffix of a copy written beside the ordinary backup, which takes the backup's name once a replace is
        /// done (Replace).</summary>
        public const string PartialSuffix = ".partial";

        /// <summary>Prefix of the folder a restore extracts a kept copy into before moving it into place.</summary>
        public const string RestorePrefix = "_OpenDash_restore_";

        /// <summary>Prefix of the folder a dashboard is moved into, under its own name, while its replacement is moved
        /// in (Replace).</summary>
        public const string AsidePrefix = "_OpenDash_aside_";

        /// <summary>
        /// Removes the working folders an earlier install or restore did not clean up, puts back a dashboard that an
        /// interrupted replace had moved aside, and reports how many folders it dealt with.
        /// </summary>
        /// <remarks>
        /// Install extracts into DashTemplates/_OpenDash_staging_&lt;guid&gt; and removes it in a finally. That finally
        /// does not run when the process exits, because a thread-pool thread is a background thread the CLR
        /// terminates without unwinding, so every update abandoned by a SimHub that closed mid-install leaves a
        /// complete extracted dashboard behind. They accumulate, they are invisible, and nothing else would ever
        /// remove them. A folder with one of these prefixes is OpenDash's own working space and is never a user's
        /// dashboard. A restore's was left out, so an interrupted Put mine back left a whole dashboard in
        /// DashTemplates for good (#580).
        ///
        /// An aside folder is not swept blindly. It holds the dashboard that was there before a replace, which is
        /// all that is left of it when the process stopped before its replacement was moved in, so it is moved back
        /// under its own name when that name is free. When the name is taken, the replace got as far as moving the
        /// new folder in, and the aside copy is deleted as the replace would have deleted it. A replace that got further
        /// renames its aside folder to a staging name before it deletes it, so a delete cut short is never put back.
        /// A copy left beside the ordinary backup is removed too, since the backup it was to replace is still there.
        /// </remarks>
        public static int RemoveOrphanedStaging(string simHubRoot, IInstallLog log)
        {
            log = log ?? NullInstallLog.Instance;
            var templates = Path.Combine(simHubRoot ?? string.Empty, DashTemplates);
            if (!Directory.Exists(templates)) return 0;
            var removed = 0;
            foreach (var aside in Directory.GetDirectories(templates, AsidePrefix + "*"))
            {
                try
                {
                    foreach (var folder in Directory.GetDirectories(aside))
                    {
                        var name = Path.GetFileName(folder);
                        var target = Path.Combine(templates, name);
                        if (Directory.Exists(target)) continue;
                        Directory.Move(folder, target);
                        log.Info("Put back " + name + ", which an interrupted install had moved aside.");
                    }
                    // What is left is spent, and is renamed as such before it is deleted, as Replace does, so that a
                    // delete cut short here is never put back by a later sweep.
                    var spent = Path.Combine(templates, StagingPrefix + Guid.NewGuid().ToString("N"));
                    Directory.Move(aside, spent);
                    Directory.Delete(spent, true);
                    removed++;
                }
                catch (Exception ex)
                {
                    log.Warn("Could not put back or remove " + aside + ": " + ex.Message);
                }
            }
            var working = Directory.GetDirectories(templates, StagingPrefix + "*").Concat(Directory.GetDirectories(templates, RestorePrefix + "*"));
            foreach (var folder in working)
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
            // A copy written beside the ordinary backup and never renamed over it, because the process stopped first
            // (Replace). The backup it was to replace is still there.
            foreach (var partial in Directory.GetFiles(templates, "*" + BackupSuffix + PartialSuffix))
            {
                try
                {
                    File.Delete(partial);
                    removed++;
                    log.Info("Removed a copy an interrupted install left unfinished: " + Path.GetFileName(partial));
                }
                catch (Exception ex)
                {
                    log.Warn("Could not remove " + partial + ": " + ex.Message);
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

        /// <summary>Puts back a copy Install set aside, and reports whether there was one to put back.</summary>
        /// <remarks>
        /// Install writes a copy before it replaces a folder, and until this existed nothing read it, so "the
        /// previous package survives" was true and "it can be put back" was not. A copy stores entries relative to
        /// the folder rather than under it, which is what ZipFile.CreateFromDirectory writes and the opposite of a
        /// .simhubdash, so it extracts straight into the target.
        ///
        /// A restore replaces a folder, so it keeps a copy of what it replaces exactly as Install does, and it uses
        /// up the copy it put back, which is the folder now (#608). It used to do neither: the copy stayed on the
        /// shelf, so the panel offered "Put mine back" for ever, and a second press put the old copy back over
        /// whatever had been edited since the first and kept nothing of it.
        /// </remarks>
        /// <param name="from">Which copy to put back; the newest kept one when omitted.</param>
        /// <param name="holdsAuthoredWork">
        /// Whether the folder being restored over is somebody's work, as Install takes it: its copy is then kept
        /// where no install reclaims it and the panel offers it back. False for a folder OpenDash wrote and nobody
        /// has touched, whose copy is the ordinary one-deep backup. Unknown is somebody's work.
        /// </param>
        /// <param name="disk">How the folder is moved and deleted; the disk itself when omitted.</param>
        public static bool Restore(string simHubRoot, string folderName, IInstallLog log, string from = null, bool holdsAuthoredWork = true, IFolderMoves disk = null)
        {
            log = log ?? NullInstallLog.Instance;
            var templates = Path.Combine(simHubRoot, DashTemplates);
            var copy = from ?? KeptCopies(simHubRoot, folderName).FirstOrDefault() ?? Path.Combine(templates, folderName + BackupSuffix);
            if (!File.Exists(copy))
            {
                log.Warn("No previous copy of " + folderName + " was kept, so there is nothing to put back.");
                return false;
            }

            var target = Path.Combine(templates, folderName);
            var staging = Path.Combine(templates, RestorePrefix + Guid.NewGuid().ToString("N"));
            try
            {
                using (var zip = ZipFile.OpenRead(copy))
                {
                    ExtractSafely(zip, staging);
                }
                if (!File.Exists(Path.Combine(staging, folderName + DashExtension)))
                {
                    throw new InvalidDataException("The kept copy of " + folderName + " has no " + folderName + DashExtension + ".");
                }
                // Replace throws when the folder cannot be moved aside or its copy cannot be taken, and the folder is
                // then left as it is.
                var keepAs = Directory.Exists(target) ? BackupPathFor(templates, folderName, holdsAuthoredWork) : null;
                var keptAs = Replace(staging, target, keepAs, disk ?? DiskFolders.Instance, log);
                log.Info("Put back the previous copy of " + folderName + " from " + copy);
                // The ordinary backup restored over an untouched folder has just been rewritten with what it
                // replaced, a swap, and is not spent.
                if (keptAs == null || !string.Equals(Path.GetFullPath(copy), Path.GetFullPath(keptAs), StringComparison.OrdinalIgnoreCase))
                {
                    Spend(copy, log);
                }
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
        /// Puts a staged folder in the place of <paramref name="target"/>, keeping a copy of what was there, so that a
        /// failure at any step leaves the dashboard that was there whole. Throws when the replace did not happen.
        /// </summary>
        /// <remarks>
        /// It used to take the copy, delete the target and move the new folder in. A recursive delete removes one
        /// file at a time and stops at the first it cannot, so a file SimHub or a virus scanner still held left the
        /// folder half deleted, and a move that failed after a delete that had worked left nothing under the name
        /// but a zip (#580, #869). Renames are what can be undone. The old folder is moved aside in one step, which
        /// Windows refuses whole while a file in it is open, so a held file now stops the replace before anything
        /// has changed. The copy is taken from the folder aside, the new folder is moved in, and only then is the old
        /// one renamed as spent (Retire) and deleted, as a best effort, since by then it is no longer a dashboard.
        /// When the copy or the move in fails, the old folder is moved back. When even that fails it is left whole in the aside folder, which
        /// RemoveOrphanedStaging puts back at the next start, and the log says where it is.
        ///
        /// A copy that would overwrite one already on the shelf, which is the ordinary one-deep backup, is written
        /// beside it and takes its name only once the new folder is in. A replace that is undone therefore leaves the
        /// shelf as it found it: the backup a restore was about to put back is still there to put back, and no copy of
        /// somebody's work is offered for a folder that still holds it.
        /// </remarks>
        /// <param name="keepAs">Where the copy of the folder being replaced goes, or null when nothing is replaced.</param>
        /// <returns>Where the copy went, or null when there was nothing to replace.</returns>
        private static string Replace(string staged, string target, string keepAs, IFolderMoves disk, IInstallLog log)
        {
            if (!Directory.Exists(target))
            {
                Retrying(() => disk.Move(staged, target));
                return null;
            }

            var name = Path.GetFileName(target);
            var holder = Path.Combine(Path.GetDirectoryName(target), AsidePrefix + Guid.NewGuid().ToString("N"));
            var aside = Path.Combine(holder, name);
            Directory.CreateDirectory(holder);
            try
            {
                Retrying(() => disk.Move(target, aside));
            }
            catch (Exception ex)
            {
                TryDeleteEmpty(holder);
                log.Error("Could not move " + name + " aside to replace it, so it was left as it is: " + ex.Message);
                throw new IOException(name + " is in use and was left as it is: " + ex.Message, ex);
            }

            var pending = keepAs == null ? null : File.Exists(keepAs) ? keepAs + PartialSuffix : keepAs;
            try
            {
                if (pending != null) Backup(aside, pending, log);
                Retrying(() => disk.Move(staged, target));
            }
            catch (Exception)
            {
                if (pending != null) TryDeleteFile(pending, log);
                try
                {
                    Retrying(() => disk.Move(aside, target));
                    TryDeleteEmpty(holder);
                    log.Warn("Put " + name + " back as it was, since its replacement could not be moved in.");
                }
                catch (Exception back)
                {
                    log.Error("Could not put " + name + " back, so it is kept whole in " + aside
                        + ", where the next start of SimHub puts it back: " + back.Message);
                }
                throw;
            }

            var kept = pending;
            if (pending != null && !string.Equals(pending, keepAs, StringComparison.Ordinal))
            {
                try
                {
                    // In one step, so that the older copy goes only as the newer one takes its name.
                    if (File.Exists(keepAs)) File.Replace(pending, keepAs, null);
                    else File.Move(pending, keepAs);
                    kept = keepAs;
                }
                catch (Exception ex)
                {
                    // The older copy is the one left on the shelf, since a refused replace takes nothing away, and the
                    // folder it is a copy of is OpenDash's own, since a copy of somebody's work always has a name of
                    // its own.
                    log.Warn("Could not keep the previous copy of " + name + " as " + keepAs + ": " + ex.Message);
                    TryDeleteFile(pending, log);
                    kept = null;
                }
            }
            if (kept != null) log.Info("Previous copy kept as " + kept);
            var spent = Retire(holder, disk, log);
            if (spent != null) Discard(spent, disk, log);
            return kept;
        }

        /// <summary>
        /// Deletes a dashboard folder, keeping a copy of it first when it holds somebody's work, and returns where that
        /// copy went. Throws when the folder could not be taken out of DashTemplates, which then holds it as it was.
        /// </summary>
        /// <remarks>
        /// The screen's Remove used to be a bare recursive delete, which a file SimHub held stopped part of the way
        /// through, leaving a folder that was no longer a dashboard and that no screen owned (#861). The folder is
        /// moved out in one step instead, which Windows refuses whole while a file in it is open, and deleted once it
        /// is out. Out means a working folder RemoveOrphanedStaging sweeps, so a delete that fails there, or that a
        /// closing SimHub cuts short, is finished at the next start.
        ///
        /// The copy is taken while the folder is still in place, so that nothing a closing SimHub interrupts can lose
        /// the work: either the folder is still there or its copy is. It is kept under a name of its own, as Install
        /// keeps one, and removed again when the folder could not be moved, since the folder still holds the work.
        /// </remarks>
        /// <param name="holdsAuthoredWork">Whether somebody has edited the folder, so that a copy of it is kept.</param>
        /// <param name="disk">How the folder is moved and deleted; the disk itself when omitted.</param>
        /// <returns>The copy kept, or null when none was.</returns>
        public static string Remove(string simHubRoot, string folderName, bool holdsAuthoredWork, IInstallLog log, IFolderMoves disk = null)
        {
            log = log ?? NullInstallLog.Instance;
            disk = disk ?? DiskFolders.Instance;
            var templates = Path.Combine(simHubRoot, DashTemplates);
            var target = Path.Combine(templates, folderName);
            if (!Directory.Exists(target)) return null;

            string kept = null;
            if (holdsAuthoredWork)
            {
                kept = BackupPathFor(templates, folderName, true);
                Backup(target, kept, log);
            }
            var holder = Path.Combine(templates, StagingPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(holder);
            try
            {
                Retrying(() => disk.Move(target, Path.Combine(holder, folderName)));
            }
            catch (Exception ex)
            {
                if (kept != null) TryDeleteFile(kept, log);
                TryDeleteEmpty(holder);
                log.Error("Could not move " + folderName + " out of DashTemplates, so it was left as it is: " + ex.Message);
                throw new IOException(folderName + " is in use and was left as it is: " + ex.Message, ex);
            }
            if (kept != null) log.Info("Your copy of " + folderName + " is kept as " + kept);
            Discard(holder, disk, log);
            return kept;
        }

        /// <summary>
        /// Zips a folder into a copy. Throws when it cannot, which stops the replace or the remove.
        /// </summary>
        /// <remarks>
        /// It used to warn and return null, and the caller deleted the folder regardless, so the one case the backup
        /// exists for, a disk that is full or a file that is locked, was also the case where it silently did nothing
        /// and the dashboard went anyway. Failing here leaves the installed dashboard where it is, which is the whole
        /// point of taking a copy first.
        /// </remarks>
        private static void Backup(string folder, string backupPath, IInstallLog log)
        {
            try
            {
                if (File.Exists(backupPath)) File.Delete(backupPath);
                ZipFile.CreateFromDirectory(folder, backupPath);
            }
            catch (Exception ex)
            {
                log.Error("Could not set aside the copy of " + Path.GetFileName(folder) + ", so it was left as it is: " + ex.Message);
                throw new IOException("Could not set aside the existing " + Path.GetFileName(folder) + ": " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Renames the aside folder of a replace that is done to a staging name, and returns that name, or null when the
        /// rename was refused.
        /// </summary>
        /// <remarks>
        /// Once the new folder is in, the old one is spent, and a delete of it that stops part of the way through must
        /// not leave anything the sweep would put back. The sweep puts an aside folder back whenever its name is free,
        /// so a half-deleted one would have come back as a dashboard of nobody's once the screen was removed or resized
        /// in the same session. A staging folder is only ever deleted. A refused rename has deleted nothing, so the
        /// aside folder is left whole, and the sweep deletes it while the name is taken.
        /// </remarks>
        private static string Retire(string holder, IFolderMoves disk, IInstallLog log)
        {
            var spent = Path.Combine(Path.GetDirectoryName(holder), StagingPrefix + Guid.NewGuid().ToString("N"));
            try
            {
                Retrying(() => disk.Move(holder, spent));
                return spent;
            }
            catch (Exception ex)
            {
                log.Warn("Could not set " + holder + " out for removal; the next start of SimHub removes it: " + ex.Message);
                return null;
            }
        }

        /// <summary>Deletes a folder a dashboard was moved into, as a best effort: it is no longer a dashboard, and
        /// what is left of it is a working folder the next start sweeps.</summary>
        private static void Discard(string holder, IFolderMoves disk, IInstallLog log)
        {
            try
            {
                Retrying(() => disk.Delete(holder));
            }
            catch (Exception ex)
            {
                log.Warn("Could not remove " + holder + " yet; the next start of SimHub does: " + ex.Message);
            }
        }

        private static void TryDeleteEmpty(string folder)
        {
            try
            {
                if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            }
            catch (Exception)
            {
                // An empty folder with a working prefix, which the next start sweeps.
            }
        }

        private static void TryDeleteFile(string path, IInstallLog log)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                log.Warn("Could not remove " + path + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Where the copy of a folder about to be replaced goes, which decides whether it survives. The routine
        /// backup is reclaimed by the next install of the same folder, which is the ordinary one-deep undo. Work
        /// somebody authored is not ordinary: it is kept under a name no later install can claim, because the
        /// consent to replace it was given on the promise that a copy is kept, and a promise the next upgrade
        /// quietly breaks is worse than no promise.
        /// </summary>
        /// <remarks>
        /// Two copies of one folder kept within a second used to share a name, and Backup deleted the first to
        /// write the second, so a kept copy takes the next second that is free.
        /// </remarks>
        private static string BackupPathFor(string templates, string folderName, bool holdsAuthoredWork)
        {
            if (!holdsAuthoredWork) return Path.Combine(templates, folderName + BackupSuffix);
            var at = DateTime.UtcNow;
            while (true)
            {
                var path = Path.Combine(templates, folderName + EditedSuffix + Stamp(at) + ".zip");
                if (!File.Exists(path)) return path;
                at = at.AddSeconds(1);
            }
        }

        /// <summary>Removes a copy that has been put back, since the folder holds it now. A copy that cannot be
        /// removed is only offered again, so it is reported rather than failing a restore that has happened.</summary>
        private static void Spend(string copy, IInstallLog log)
        {
            try
            {
                File.Delete(copy);
                log.Info("The copy put back is the folder now, so " + Path.GetFileName(copy) + " was removed");
            }
            catch (Exception ex)
            {
                log.Warn("Could not remove " + copy + " once it was put back: " + ex.Message);
            }
        }

        /// <summary>A stamp that sorts, so a person can tell which copy is which without opening them.</summary>
        /// <remarks>
        /// In UTC and in the invariant culture's calendar (#580). The current culture's wrote 2569 on a Thai system
        /// and a Hijri year on a Saudi one, which sorted a copy among the others by a year no other copy had, and
        /// local time runs back an hour every autumn, which sorted a copy taken after the change under one taken
        /// before it. The Z says which clock it is.
        /// </remarks>
        internal static string Stamp(DateTime at) => at.ToUniversalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "Z";

        /// <summary>Runs a move or a delete, once more after a quarter of a second when it is refused: SimHub may still
        /// hold a file of a dashboard it has just released, and a virus scanner one it has just been handed.</summary>
        private static void Retrying(Action step)
        {
            try
            {
                step();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Thread.Sleep(250);
                step();
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
