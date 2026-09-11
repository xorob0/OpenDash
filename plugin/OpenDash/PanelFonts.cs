// PanelFonts.cs: Barlow and openDash Display (Barlow Condensed renamed) for the settings panel. The TTFs are embedded in the assembly
// (the project has no WPF resource pipeline, so pack:// URIs are unavailable); they are written once to
// %LocalAppData%\openDash\Fonts and loaded from there through a directory-based FontFamily. Any failure
// falls back to Segoe UI, which is SimHub's own UI font, and is logged once.
using System;
using System.IO;
using System.Windows.Media;

namespace OpenDashPlugin
{
    internal static class PanelFonts
    {
        private const string ResourcePrefix = "OpenDash.Fonts.";
        private const string FallbackFamily = "Segoe UI";

        // The condensed faces are openDash's own build of Barlow Condensed, renamed so that WPF files them
        // as a family of their own instead of folding them into Barlow as a stretch; the panel asks for the
        // same family the dash face does. See packages/dash/src/design/fontFiles.ts.
        private static readonly string[] Files =
        {
            "Barlow-Regular.ttf",
            "Barlow-Medium.ttf",
            "openDashDisplay-Light.ttf",
            "openDashDisplay-SemiBold.ttf",
            "openDashDisplay-Bold.ttf",
            "OFL.txt",
        };

        private static readonly object Gate = new object();
        private static bool loaded;
        private static FontFamily label;
        private static FontFamily data;

        /// <summary>Barlow: labels, body copy and captions.</summary>
        public static FontFamily Label
        {
            get { Ensure(); return label; }
        }

        /// <summary>openDash Display, which is Barlow Condensed: the wordmark and numerals.</summary>
        public static FontFamily Data
        {
            get { Ensure(); return data; }
        }

        /// <summary>False when the panel runs on the Segoe UI fallback.</summary>
        public static bool IsEmbedded { get; private set; }

        private static void Ensure()
        {
            lock (Gate)
            {
                if (loaded) return;
                loaded = true;
                label = new FontFamily(FallbackFamily);
                data = new FontFamily(FallbackFamily);
                try
                {
                    var folder = Extract();
                    var baseUri = new Uri(folder + Path.DirectorySeparatorChar);
                    label = new FontFamily(baseUri, "./#" + Theme.FontLabel);
                    data = new FontFamily(baseUri, "./#" + Theme.FontData);
                    IsEmbedded = true;
                }
                catch (Exception ex)
                {
                    Log.Warn("Barlow could not be loaded; the panel uses " + FallbackFamily + ": " + ex.Message);
                }
            }
        }

        /// <summary>Writes the embedded files to the private font folder when missing or of a different size.</summary>
        private static string Extract()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "openDash", "Fonts");
            Directory.CreateDirectory(folder);
            var assembly = typeof(PanelFonts).Assembly;
            foreach (var file in Files)
            {
                using (var source = assembly.GetManifestResourceStream(ResourcePrefix + file))
                {
                    if (source == null)
                    {
                        if (file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new FileNotFoundException("Embedded font missing: " + file);
                        }
                        continue;
                    }
                    var target = Path.Combine(folder, file);
                    if (File.Exists(target) && new FileInfo(target).Length == source.Length) continue;
                    using (var destination = File.Create(target))
                    {
                        source.CopyTo(destination);
                    }
                }
            }
            return folder;
        }
    }
}
