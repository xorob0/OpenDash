// PackageCatalogue.cs: what a folder under DashTemplates is, and what the plugin can make a screen from.
//
// The panel's "add a screen" needs a list of what is installable, and ADR 0017's migration needs to look
// at a folder somebody already has and say which kind and size it is. Both are answered from the packages
// the assembly embeds rather than from a fourth copy of the catalogue: the folder name comes from the zip
// (PackageExtractor.PackageFolderName) and the size from the .djson.metadata inside it, so a package that
// ships is in the list by virtue of shipping.
//
// Classify() is pure and is the fallback for a folder whose package is not embedded -- one installed by an
// older version, or one whose resource has since been dropped. A rig must not lose a screen because the
// plugin stopped carrying its package.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>One package the plugin can install, and what kind of screen it makes.</summary>
    public sealed class PackageEntry
    {
        /// <summary>The resource name in the package source, for extracting it.</summary>
        public string Package { get; set; }

        /// <summary>The folder it writes under DashTemplates, e.g. "openDash 1280x480".</summary>
        public string Folder { get; set; }

        /// <summary>One of Contract.ScreenKinds.</summary>
        public string Kind { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        /// <summary>"1280 × 480", as the panel writes a size.</summary>
        public string SizeLabel { get { return Width + " × " + Height; } }
    }

    public static class PackageCatalogue
    {
        /// <summary>The folder of the 1920x480 zone face, which is the one package whose name carries no size.</summary>
        public const string PrimaryFolder = "openDash";

        /// <summary>
        /// What kind of screen a folder name and size describe.
        /// </summary>
        /// <remarks>
        /// Order matters. The slots faces carry a size that is also a zone face's, so they are recognised
        /// by their name before the size is consulted; the round faces are not in FACE_SIZES at all and
        /// fall through to slots, which is what they are (ADR 0006 leaves them on the card model).
        /// </remarks>
        public static string Classify(string folder, int width, int height)
        {
            var name = folder ?? string.Empty;
            if (name.IndexOf("Companion", StringComparison.OrdinalIgnoreCase) >= 0) return Contract.KindCompanion;
            if (name.IndexOf("Pit wall", StringComparison.OrdinalIgnoreCase) >= 0) return Contract.KindPitWall;
            if (name.StartsWith("openDash slots ", StringComparison.OrdinalIgnoreCase)) return Contract.KindSlots;
            foreach (var size in Contract.FaceSizes)
            {
                if (size.Width == width && size.Height == height) return Contract.KindFace;
            }
            return Contract.KindSlots;
        }

        /// <summary>
        /// The size a folder name spells, for a folder whose package cannot be read.
        /// </summary>
        /// <remarks>
        /// "openDash 1280x480" and "openDash slots 1280x480" both give 1280 x 480; the bare "openDash" is
        /// the 1920x480 face, which is the one name that carries no size and the reason this is not a
        /// plain parse. Anything else gives zero, and the caller treats a zero size as unknown rather
        /// than guessing.
        /// </remarks>
        public static void SizeFromFolder(string folder, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrEmpty(folder)) return;
            if (string.Equals(folder, PrimaryFolder, StringComparison.OrdinalIgnoreCase))
            {
                width = Contract.ReferenceFace.Width;
                height = Contract.ReferenceFace.Height;
                return;
            }
            foreach (var word in folder.Split(' '))
            {
                var x = word.IndexOf('x');
                if (x <= 0 || x == word.Length - 1) continue;
                int w, h;
                if (!int.TryParse(word.Substring(0, x), NumberStyles.None, CultureInfo.InvariantCulture, out w)) continue;
                if (!int.TryParse(word.Substring(x + 1), NumberStyles.None, CultureInfo.InvariantCulture, out h)) continue;
                width = w;
                height = h;
                return;
            }
        }

        /// <summary>
        /// The size a package declares in its .djson.metadata sidecar.
        /// </summary>
        /// <remarks>
        /// SimHub's own Width and Height, which is the size the dashboard was drawn for and therefore the
        /// size the screen is. Read with a search rather than a JSON parser because this is the only
        /// thing wanted out of the file, and because parsing and reserialising a scene graph through a
        /// library we do not control is what PackageExtractor avoids for the same reason.
        /// </remarks>
        public static bool SizeFromMetadata(System.IO.Compression.ZipArchive zip, string folder, out int width, out int height)
        {
            width = 0;
            height = 0;
            var wanted = folder + "/" + folder + PackageExtractor.MetadataExtension;
            var entry = zip.Entries.FirstOrDefault(e =>
                string.Equals(e.FullName.Replace('\\', '/'), wanted, StringComparison.OrdinalIgnoreCase));
            if (entry == null) return false;
            string text;
            using (var reader = new System.IO.StreamReader(entry.Open())) text = reader.ReadToEnd();
            return Number(text, "\"Width\"", out width) && Number(text, "\"Height\"", out height) && width > 0 && height > 0;
        }

        private static bool Number(string text, string key, out int value)
        {
            value = 0;
            var at = text.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return false;
            var i = text.IndexOf(':', at + key.Length);
            if (i < 0) return false;
            i++;
            while (i < text.Length && (text[i] == ' ' || text[i] == '\t')) i++;
            var start = i;
            while (i < text.Length && text[i] >= '0' && text[i] <= '9') i++;
            return i > start && int.TryParse(text.Substring(start, i - start), NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Every package the source carries, as screens they could become.
        /// </summary>
        /// <remarks>
        /// A package that cannot be read is skipped rather than thrown over: the assembly may carry none
        /// at all (the csproj allows it, and the panel says so), and one unreadable package is not a
        /// reason to offer none.
        /// </remarks>
        public static IReadOnlyList<PackageEntry> From(IPackageSource packages, IInstallLog log = null)
        {
            var entries = new List<PackageEntry>();
            if (packages == null) return entries;
            var report = log ?? new NullInstallLog();
            foreach (var name in packages.Names)
            {
                try
                {
                    using (var stream = packages.Open(name))
                    using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read))
                    {
                        var folder = PackageExtractor.PackageFolderName(zip);
                        if (folder == null) continue;
                        int width, height;
                        // The package's own metadata, not the folder name. "openDash Companion" and
                        // "openDash 480 round" carry no size at all, and reading it off the name left
                        // their cards saying 0 x 0.
                        if (!SizeFromMetadata(zip, folder, out width, out height)) SizeFromFolder(folder, out width, out height);
                        entries.Add(new PackageEntry
                        {
                            Package = name,
                            Folder = folder,
                            Width = width,
                            Height = height,
                            Kind = Classify(folder, width, height),
                        });
                    }
                }
                catch (Exception ex)
                {
                    report.Warn("Could not read the package " + name + ": " + ex.Message);
                }
            }
            // Faces first and largest first, then the companions and the pit walls, so that the list the
            // panel offers opens on the thing most rigs are adding.
            return entries
                .OrderBy(entry => KindOrder(entry.Kind))
                .ThenByDescending(entry => entry.Width * entry.Height)
                .ThenBy(entry => entry.Folder, StringComparer.Ordinal)
                .ToList();
        }

        private static int KindOrder(string kind)
        {
            if (string.Equals(kind, Contract.KindFace, StringComparison.Ordinal)) return 0;
            if (string.Equals(kind, Contract.KindCompanion, StringComparison.Ordinal)) return 1;
            if (string.Equals(kind, Contract.KindPitWall, StringComparison.Ordinal)) return 2;
            return 3;
        }

        /// <summary>
        /// A screen made from a package, taking the stock namespace and folder when the rig has neither.
        /// </summary>
        /// <param name="taken">The namespaces the rig already holds, so a second screen at a size gets its own.</param>
        public static ScreenInstance NewScreen(PackageEntry entry, string name, IEnumerable<string> taken)
        {
            var screen = new ScreenInstance
            {
                Name = string.IsNullOrWhiteSpace(name) ? entry.SizeLabel : name.Trim(),
                Kind = entry.Kind,
                Width = entry.Width,
                Height = entry.Height,
                Package = entry.Package,
            };
            var used = new HashSet<string>(taken ?? new string[0], StringComparer.OrdinalIgnoreCase);
            if (!used.Contains(screen.StockNamespace))
            {
                // The first screen at a size takes the stock package byte for byte: same namespace, same
                // folder, nothing rewritten. This is the whole of the pre-ADR-0017 behaviour and it is
                // deliberate that the ordinary rig still produces exactly the files it always did.
                screen.Namespace = screen.StockNamespace;
                screen.Folder = entry.Folder;
            }
            else
            {
                screen.Namespace = UniqueNamespace(screen.Name, used);
                screen.Folder = PrimaryFolder + " " + screen.Name;
            }
            screen.Normalise();
            return screen;
        }

        /// <summary>
        /// A namespace nothing else on the rig holds, slugged from the name.
        /// </summary>
        /// <remarks>
        /// A name that slugs to nothing -- one written entirely in a script this drops -- falls back to
        /// "Screen", so a user whose language is not Latin gets a working screen rather than a refusal.
        /// A numeral is appended until the result is free, and a reserved namespace counts as taken.
        /// </remarks>
        public static string UniqueNamespace(string name, ICollection<string> taken)
        {
            var slug = Contract.Slug(name);
            if (slug.Length == 0) slug = "Screen";
            var candidate = slug;
            var n = 2;
            while (taken.Contains(candidate) || Contract.IsReservedNamespace(candidate))
            {
                candidate = slug + n.ToString(CultureInfo.InvariantCulture);
                n++;
            }
            return candidate;
        }

        /// <summary>A name nothing else on the rig carries, so two cards are never both "1280 × 480".</summary>
        public static string UniqueName(string wanted, IEnumerable<string> taken)
        {
            var used = new HashSet<string>(taken ?? new string[0], StringComparer.OrdinalIgnoreCase);
            if (!used.Contains(wanted)) return wanted;
            var n = 2;
            while (used.Contains(wanted + " (" + n.ToString(CultureInfo.InvariantCulture) + ")")) n++;
            return wanted + " (" + n.ToString(CultureInfo.InvariantCulture) + ")";
        }
    }
}
