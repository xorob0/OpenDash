// TestSupport.cs: what the installer and extractor tests share: synthetic .simhubdash zips built in memory, a package
// source over such zips, a folder record in memory, and a log that records its lines.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

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

    /// <summary>A folder record in memory, standing in for the one the plugin keeps in its settings.</summary>
    internal sealed class MemoryFolderRecord : IFolderRecord
    {
        public Dictionary<string, string> Entries { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string Get(string folderName) => Entries.TryGetValue(folderName ?? string.Empty, out var value) ? value : null;
        public void Set(string folderName, string fingerprint) => Entries[folderName ?? string.Empty] = fingerprint;
    }

    /// <summary>
    /// A log that keeps its lines. Work handed to UpdateService.InBackground writes here from a thread-pool thread
    /// while the test thread reads it, so the list is guarded and Lines hands out a copy: enumerating the live list
    /// while an Add is in flight throws "Collection was modified", which would be a red run with nothing wrong.
    /// </summary>
    internal sealed class ListLog : IInstallLog
    {
        private readonly object gate = new object();
        private readonly List<string> lines = new List<string>();

        /// <summary>Set once a line has been written, so a test can wait for the log rather than for the work that
        /// produces it: work that throws finishes before the catch that logs the failure has run.</summary>
        public ManualResetEventSlim Written { get; } = new ManualResetEventSlim();

        public IReadOnlyList<string> Lines { get { lock (gate) { return lines.ToArray(); } } }

        public void Info(string message) => Write("info: " + message);
        public void Warn(string message) => Write("warn: " + message);
        public void Error(string message) => Write("error: " + message);

        private void Write(string line)
        {
            lock (gate) { lines.Add(line); }
            Written.Set();
        }
    }
}
