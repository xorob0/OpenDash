// FolderFingerprint.cs: whether an installed dashboard is still the one OpenDash wrote.
//
// Dash Studio edits a dashboard in place, and OpenDash replaces a folder by deleting it. Until this existed there
// was no way to tell the two apart, so an update destroyed a person's work without anyone being asked. A hash taken
// when the folder is written, and compared before it is replaced, is enough to ask first.
//
// Only the .djson files and their sidecars are hashed. They are what Dash Studio writes; the fonts beside them are
// copied rather than authored, and a font that differs is not a reason to stop and ask about someone's work.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OpenDashPlugin
{
    public static class FolderFingerprint
    {
        /// <summary>The extensions that carry what a person can edit.</summary>
        private static readonly string[] Authored = { ".djson", ".metadata" };

        /// <summary>
        /// A hash of a dashboard folder's authored files, or null when the folder does not exist.
        /// </summary>
        /// <remarks>
        /// Paths are relative, ordinal-sorted and lowercased so that the value does not depend on where the folder
        /// is or on how the filesystem happens to order it, and the path goes into the hash as well as the content
        /// so that renaming a file is a change.
        /// </remarks>
        public static string Of(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;
            var root = Path.GetFullPath(folder);
            var files = Directory
                .GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(IsAuthored)
                .Select(path => new { Relative = Relative(root, path), Path = path })
                .OrderBy(entry => entry.Relative, StringComparer.Ordinal)
                .ToList();

            using (var hash = SHA256.Create())
            {
                foreach (var entry in files)
                {
                    var name = Encoding.UTF8.GetBytes(entry.Relative + "\n");
                    hash.TransformBlock(name, 0, name.Length, null, 0);
                    byte[] content;
                    try
                    {
                        content = File.ReadAllBytes(entry.Path);
                    }
                    catch (IOException)
                    {
                        // A file we cannot read is a folder we cannot vouch for, which is the asking case.
                        return null;
                    }
                    hash.TransformBlock(content, 0, content.Length, null, 0);
                }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                return "sha256:" + BitConverter.ToString(hash.Hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Whether the folder still holds what OpenDash put there.
        /// </summary>
        /// <remarks>
        /// Biased towards asking. No record, an unreadable folder, or a record that does not match all read as
        /// "cannot prove it is untouched", because the cost of asking unnecessarily is one extra click and the cost
        /// of not asking is somebody's afternoon.
        /// </remarks>
        public static bool LooksUntouched(string folder, string recorded)
        {
            if (string.IsNullOrWhiteSpace(recorded)) return false;
            var current = Of(folder);
            return current != null && string.Equals(current, recorded, StringComparison.Ordinal);
        }

        private static bool IsAuthored(string path)
        {
            var name = Path.GetFileName(path);
            return Authored.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }

        private static string Relative(string root, string path)
        {
            var full = Path.GetFullPath(path);
            var trimmed = full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return trimmed.Replace(Path.DirectorySeparatorChar, '/').ToLowerInvariant();
        }
    }
}
