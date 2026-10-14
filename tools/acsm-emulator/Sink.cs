// Sink.cs: where the pages go. Four named mappings on Windows, a dictionary of byte arrays anywhere else.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace AcsmEmulator
{
    public interface IPageSink : IDisposable
    {
        void Write(string map, byte[] data);
        string Describe();
    }

    /// <summary>Used by --selfcheck and --dry-run, on any OS.</summary>
    public sealed class MemorySink : IPageSink
    {
        public readonly Dictionary<string, byte[]> Images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        public long Writes;

        public void Write(string map, byte[] data)
        {
            Images[map] = (byte[])data.Clone();
            Writes++;
        }

        public string Describe() { return "in-memory pages, no shared memory"; }

        public void Dispose() { }
    }

    /// <summary>
    /// The real thing: <c>Local\acpmf_physics</c>, <c>Local\acpmf_graphics</c>, <c>Local\acpmf_static</c> and, when
    /// the scenario has a field, <c>Local\acpmf_simhub_v2</c>. SimHub opens each one with OpenExisting and a view
    /// of exactly Marshal.SizeOf the struct, so a mapping at least that large is all it needs.
    /// </summary>
    public sealed class MmfSink : IPageSink
    {
        private readonly Dictionary<string, MemoryMappedFile> _maps = new Dictionary<string, MemoryMappedFile>(StringComparer.Ordinal);
        private readonly Dictionary<string, MemoryMappedViewAccessor> _views = new Dictionary<string, MemoryMappedViewAccessor>(StringComparer.Ordinal);
        private readonly List<string> _notes = new List<string>();

        public MmfSink(IEnumerable<string> maps)
        {
            foreach (var name in maps)
            {
                long size = Pages.SizeOf(name);
                // Rounded up to whole pages, which is what Windows allocates anyway. ACC's layouts under the
                // same names are longer than AC's, so a SimHub that once read ACC may hold a larger mapping
                // open; CreateOrOpen then attaches to it, and only its first bytes are AC's.
                long capacity = (size + 4095) / 4096 * 4096;
                MemoryMappedFile mmf;
                try
                {
                    mmf = MemoryMappedFile.CreateOrOpen(name, capacity, MemoryMappedFileAccess.ReadWrite);
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new InvalidOperationException(
                        "The mapping " + name + " exists but cannot be opened (" + ex.Message + "). Run the emulator and SimHub as the same, " +
                        "non-elevated user in the same desktop session.", ex);
                }
                var view = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
                if (view.Capacity < size)
                {
                    view.Dispose();
                    mmf.Dispose();
                    throw new InvalidOperationException("An existing mapping " + name + " is only " + view.Capacity + " bytes but " + size +
                                                        " are needed. Close the process holding it (SimHub) and start the emulator again.");
                }
                _maps[name] = mmf;
                _views[name] = view;
                _notes.Add(name + " (" + view.Capacity + " bytes)");
            }
        }

        public void Write(string map, byte[] data)
        {
            if (_views.TryGetValue(map, out var view)) view.WriteArray(0, data, 0, data.Length);
        }

        public string Describe() { return string.Join(", ", _notes); }

        public void Dispose()
        {
            foreach (var v in _views.Values) { try { v.Flush(); } catch { } v.Dispose(); }
            foreach (var m in _maps.Values) m.Dispose();
            _views.Clear();
            _maps.Clear();
        }
    }
}
