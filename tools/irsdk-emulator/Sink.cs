using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace IrsdkEmulator
{
    /// <summary>Where the shared-memory image is written to.</summary>
    public interface IShmSink : IDisposable
    {
        long Capacity { get; }
        void Write(int offset, byte[] data, int index, int count);
        void WriteInt32(int offset, int value);
        /// <summary>Signals "new data" (the IRSDKDataValidEvent).</summary>
        void Signal();
        string Describe();
    }

    /// <summary>Plain byte array. Used by --selfcheck and --dry-run (works on any OS).</summary>
    public sealed class MemorySink : IShmSink
    {
        public readonly byte[] Image;
        public long Signals;

        public MemorySink(int size) { Image = new byte[size]; }

        public long Capacity => Image.Length;

        public void Write(int offset, byte[] data, int index, int count) { Array.Copy(data, index, Image, offset, count); }

        public void WriteInt32(int offset, int value) { LE.WriteInt32(Image, offset, value); }

        public void Signal() { Signals++; }

        public string Describe() { return "in-memory image (" + Image.Length + " bytes, no shared memory)"; }

        public void Dispose() { }
    }

    /// <summary>
    /// The real thing: the named memory-mapped file "Local\IRSDKMemMapFileName" plus the auto-reset event
    /// "Local\IRSDKDataValidEvent", exactly what SimHub's iRacingMemory.IsConnected() opens.
    /// </summary>
    public sealed class MmfSink : IShmSink
    {
        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _acc;
        private EventWaitHandle _evt;
        private readonly bool _attachedToExisting;
        private readonly bool _eventCreatedNew;

        public MmfSink(long capacity)
        {
            // If a previous emulator run (or an unrelated process) still has the section open, e.g. because
            // SimHub keeps its view alive after we exit, CreateNew fails; attach to the existing object instead.
            try
            {
                _mmf = MemoryMappedFile.OpenExisting(Irsdk.MemMapName, MemoryMappedFileRights.ReadWrite);
                _attachedToExisting = true;
            }
            catch (FileNotFoundException)
            {
                _mmf = MemoryMappedFile.CreateNew(Irsdk.MemMapName, capacity, MemoryMappedFileAccess.ReadWrite);
                _attachedToExisting = false;
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException(
                    "The mapping " + Irsdk.MemMapName + " exists but cannot be opened (" + ex.Message + "). It was probably created by another " +
                    "user or with a different elevation (run the emulator and SimHub as the same, non-elevated user in the same desktop session).", ex);
            }
            _acc = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
            if (_acc.Capacity < capacity)
            {
                _acc.Dispose();
                _mmf.Dispose();
                throw new InvalidOperationException(
                    "An existing mapping " + Irsdk.MemMapName + " is only " + _acc.Capacity + " bytes but " + capacity +
                    " are needed. Close the process holding it (SimHub) and start the emulator again.");
            }
            _evt = new EventWaitHandle(false, EventResetMode.AutoReset, Irsdk.DataValidEventName, out _eventCreatedNew);
        }

        public long Capacity => _acc.Capacity;

        public void Write(int offset, byte[] data, int index, int count) { _acc.WriteArray(offset, data, index, count); }

        public void WriteInt32(int offset, int value) { _acc.Write(offset, value); }

        public void Signal() { _evt.Set(); }

        public string Describe()
        {
            return Irsdk.MemMapName + " (" + (_attachedToExisting ? "attached to existing mapping" : "created") + ", " + Capacity + " bytes), " +
                   Irsdk.DataValidEventName + " (" + (_eventCreatedNew ? "created" : "opened existing") + ")";
        }

        public void Dispose()
        {
            try { _acc?.Flush(); } catch { }
            _acc?.Dispose();
            _mmf?.Dispose();
            _evt?.Dispose();
            _acc = null; _mmf = null; _evt = null;
        }
    }
}
