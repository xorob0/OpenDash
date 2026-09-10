// TestSupport.cs: what the installer and extractor tests share: synthetic .simhubdash zips built in memory, a package
// source over such zips, and a log that records its lines.
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace OpenDashPlugin.Tests
{
    internal static class SyntheticPackage
    {
        /// <summary>A package the way the dash build writes it: <folder>/<folder>.djson, its sidecar with the version, a
        /// cards widget and two fonts, plus any extra entries.</summary>
        public static MemoryStream Zip(string folder, string version, params (string name, string content)[] extra)
        {
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                Add(zip, folder + "/" + folder + ".djson", "{\"Version\":2}");
                Add(zip, folder + "/" + folder + ".djson.metadata", "{\"Title\":\"" + folder + "\",\"DashboardVersion\":\"" + version + "\"}");
                Add(zip, folder + "/cards.djson", "{\"Version\":2}");
                Add(zip, folder + "/_SHFonts/Barlow-Medium.ttf", "font-a");
                Add(zip, folder + "/_SHFonts/BarlowCondensed-Bold.ttf", "font-b");
                foreach (var (name, content) in extra) Add(zip, name, content);
            }
            stream.Position = 0;
            return stream;
        }

        public static void Add(ZipArchive zip, string name, string content)
        {
            using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }
    }

    /// <summary>Packages held as bytes under a name, the way the assembly holds them as resources. Open hands out a
    /// fresh stream every time, as GetManifestResourceStream does.</summary>
    internal sealed class MemoryPackageSource : IPackageSource
    {
        private readonly List<string> names = new List<string>();
        private readonly Dictionary<string, byte[]> bytes = new Dictionary<string, byte[]>();

        public IReadOnlyList<string> Names => names;

        public MemoryPackageSource Add(string name, MemoryStream package)
        {
            names.Add(name);
            bytes[name] = package.ToArray();
            return this;
        }

        public MemoryPackageSource Add(string name, string notAZip)
        {
            names.Add(name);
            bytes[name] = Encoding.UTF8.GetBytes(notAZip);
            return this;
        }

        public Stream Open(string name)
        {
            return new MemoryStream(bytes[name], false);
        }
    }

    internal sealed class ListLog : IInstallLog
    {
        public List<string> Lines { get; } = new List<string>();
        public void Info(string message) => Lines.Add("info: " + message);
        public void Warn(string message) => Lines.Add("warn: " + message);
        public void Error(string message) => Lines.Add("error: " + message);
    }
}
