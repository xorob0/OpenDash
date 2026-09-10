using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace IrsdkEmulator
{
    /// <summary>irsdk_VarType, values are the wire values used in the var headers.</summary>
    public enum IrVarType
    {
        Char = 0,
        Bool = 1,
        Int = 2,
        BitField = 3,
        Float = 4,
        Double = 5,
    }

    public static class IrType
    {
        public static int Size(IrVarType t)
        {
            switch (t)
            {
                case IrVarType.Char:
                case IrVarType.Bool: return 1;
                case IrVarType.Double: return 8;
                default: return 4;
            }
        }

        public static IrVarType Parse(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "char": case "0": return IrVarType.Char;
                case "bool": case "1": return IrVarType.Bool;
                case "int": case "int32": case "2": return IrVarType.Int;
                case "bitfield": case "bits": case "3": return IrVarType.BitField;
                case "float": case "single": case "4": return IrVarType.Float;
                case "double": case "5": return IrVarType.Double;
                default: throw new FormatException("unknown irsdk var type '" + s + "'");
            }
        }

        public static string Name(IrVarType t)
        {
            switch (t)
            {
                case IrVarType.Char: return "char";
                case IrVarType.Bool: return "bool";
                case IrVarType.Int: return "int";
                case IrVarType.BitField: return "bitField";
                case IrVarType.Float: return "float";
                case IrVarType.Double: return "double";
                default: return t.ToString();
            }
        }
    }

    /// <summary>One telemetry variable definition (== one irsdk_varHeader).</summary>
    public sealed class VarDef
    {
        public string Name;
        public string Desc = "";
        public string Unit = "";
        public IrVarType Type = IrVarType.Float;
        public int Count = 1;
        public bool CountAsTime;
        /// <summary>Byte offset inside a telemetry buffer, assigned by <see cref="ShmLayout.Build"/>.</summary>
        public int Offset;

        public int ByteSize => IrType.Size(Type) * Count;

        public VarDef Clone()
        {
            return new VarDef { Name = Name, Desc = Desc, Unit = Unit, Type = Type, Count = Count, CountAsTime = CountAsTime, Offset = Offset };
        }
    }

    /// <summary>
    /// Constants and byte offsets of the iRacing shared memory (irsdk_defines.h), cross-checked against the
    /// [StructLayout(Sequential)] classes SimHub marshals with Marshal.PtrToStructure (iRSDKHeader, VarBuf, VarHeader).
    /// </summary>
    public static class Irsdk
    {
        public const string MemMapName = "Local\\IRSDKMemMapFileName";
        public const string DataValidEventName = "Local\\IRSDKDataValidEvent";

        public const int Version = 2;
        public const int StatusConnected = 1;
        public const int MaxBufs = 4;
        public const int MaxString = 32;
        public const int MaxDesc = 64;
        public const int UnlimitedLaps = 32767;
        public const double UnlimitedTime = 604800.0;

        // irsdk_header: 10 ints, 2 pad ints, 4 x irsdk_varBuf (4 ints each) == 112 bytes
        public const int HeaderSize = 112;
        public const int OffVer = 0;
        public const int OffStatus = 4;
        public const int OffTickRate = 8;
        public const int OffSessionInfoUpdate = 12;
        public const int OffSessionInfoLen = 16;
        public const int OffSessionInfoOffset = 20;
        public const int OffNumVars = 24;
        public const int OffVarHeaderOffset = 28;
        public const int OffNumBuf = 32;
        public const int OffBufLen = 36;
        public const int OffPad = 40;
        public const int OffVarBuf = 48;
        public const int VarBufSize = 16; // int tickCount, int bufOffset, int pad[2]

        // irsdk_varHeader: int type, int offset, int count, bool countAsTime + pad[3], char name[32], char desc[64], char unit[32] == 144 bytes
        public const int VarHeaderSize = 144;
        public const int VhOffType = 0;
        public const int VhOffOffset = 4;
        public const int VhOffCount = 8;
        public const int VhOffCountAsTime = 12;
        public const int VhOffName = 16;
        public const int VhOffDesc = 48;
        public const int VhOffUnit = 112;

        public static int VarBufTickCountOffset(int bufIndex) { return OffVarBuf + bufIndex * VarBufSize; }
        public static int VarBufOffsetOffset(int bufIndex) { return OffVarBuf + bufIndex * VarBufSize + 4; }
    }

    /// <summary>Little-endian primitive writers/readers on byte arrays (no per-call allocations).</summary>
    public static class LE
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct FloatUnion
        {
            [FieldOffset(0)] public float F;
            [FieldOffset(0)] public int I;
        }

        public static void WriteInt32(byte[] b, int off, int v)
        {
            b[off] = (byte)v;
            b[off + 1] = (byte)(v >> 8);
            b[off + 2] = (byte)(v >> 16);
            b[off + 3] = (byte)(v >> 24);
        }

        public static int ReadInt32(byte[] b, int off)
        {
            return b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24);
        }

        public static void WriteInt64(byte[] b, int off, long v)
        {
            for (int i = 0; i < 8; i++) b[off + i] = (byte)(v >> (8 * i));
        }

        public static long ReadInt64(byte[] b, int off)
        {
            long v = 0;
            for (int i = 7; i >= 0; i--) v = (v << 8) | b[off + i];
            return v;
        }

        public static void WriteSingle(byte[] b, int off, float f)
        {
            var u = new FloatUnion { F = f };
            WriteInt32(b, off, u.I);
        }

        public static float ReadSingle(byte[] b, int off)
        {
            var u = new FloatUnion { I = ReadInt32(b, off) };
            return u.F;
        }

        public static void WriteDouble(byte[] b, int off, double d)
        {
            WriteInt64(b, off, BitConverter.DoubleToInt64Bits(d));
        }

        public static double ReadDouble(byte[] b, int off)
        {
            return BitConverter.Int64BitsToDouble(ReadInt64(b, off));
        }

        /// <summary>Writes an ANSI string into a fixed-size, NUL padded field (truncating to size-1).</summary>
        public static void WriteFixedString(byte[] b, int off, int size, string s)
        {
            for (int i = 0; i < size; i++) b[off + i] = 0;
            if (string.IsNullOrEmpty(s)) return;
            var bytes = Encoding.ASCII.GetBytes(s);
            int n = Math.Min(bytes.Length, size - 1);
            Array.Copy(bytes, 0, b, off, n);
        }

        public static string ReadFixedString(byte[] b, int off, int size)
        {
            int n = 0;
            while (n < size && b[off + n] != 0) n++;
            return Encoding.ASCII.GetString(b, off, n);
        }
    }

    /// <summary>
    /// Computes where everything lives inside the shared memory block and knows how to render
    /// the header and the var-header table into bytes.
    /// </summary>
    public sealed class ShmLayout
    {
        public int TickRate = 60;
        public int NumBuf = 3;
        public List<VarDef> Vars = new List<VarDef>();
        public int VarHeaderOffset;
        public int SessionInfoOffset;
        /// <summary>Bytes reserved for the YAML (written to header.sessionInfoLen, YAML is NUL terminated inside).</summary>
        public int SessionInfoCapacity;
        public int BufLen;
        public int[] BufOffsets;
        public int TotalSize;

        public static ShmLayout Build(IList<VarDef> vars, int tickRate, int numBuf, int sessionInfoCapacity)
        {
            if (numBuf < 1 || numBuf > Irsdk.MaxBufs) throw new ArgumentOutOfRangeException("numBuf", "numBuf must be 1..4");
            var l = new ShmLayout { TickRate = tickRate, NumBuf = numBuf, SessionInfoCapacity = Align(sessionInfoCapacity, 16) };
            int off = 0;
            foreach (var v in vars)
            {
                if (string.IsNullOrEmpty(v.Name)) throw new ArgumentException("variable without a name");
                if (v.Name.Length > Irsdk.MaxString - 1) throw new ArgumentException("variable name too long (max 31): " + v.Name);
                if (v.Count < 1) throw new ArgumentException("variable count must be >= 1: " + v.Name);
                int sz = IrType.Size(v.Type);
                off = Align(off, sz);
                v.Offset = off;
                off += v.ByteSize;
                l.Vars.Add(v);
            }
            l.BufLen = Align(Math.Max(off, 16), 16);
            l.VarHeaderOffset = Align(Irsdk.HeaderSize, 16);
            l.SessionInfoOffset = Align(l.VarHeaderOffset + l.Vars.Count * Irsdk.VarHeaderSize, 16);
            l.BufOffsets = new int[Irsdk.MaxBufs];
            int bufStart = Align(l.SessionInfoOffset + l.SessionInfoCapacity, 16);
            for (int i = 0; i < Irsdk.MaxBufs; i++) l.BufOffsets[i] = i < numBuf ? bufStart + i * l.BufLen : 0;
            l.TotalSize = bufStart + numBuf * l.BufLen;
            return l;
        }

        public static int Align(int v, int a) { return (v + a - 1) / a * a; }

        public VarDef Find(string name)
        {
            foreach (var v in Vars) if (v.Name == name) return v;
            return null;
        }

        /// <summary>Renders the 112-byte irsdk_header.</summary>
        public void WriteHeader(byte[] dst, int status, int sessionInfoUpdate, int[] tickCounts)
        {
            for (int i = 0; i < Irsdk.HeaderSize; i++) dst[i] = 0;
            LE.WriteInt32(dst, Irsdk.OffVer, Irsdk.Version);
            LE.WriteInt32(dst, Irsdk.OffStatus, status);
            LE.WriteInt32(dst, Irsdk.OffTickRate, TickRate);
            LE.WriteInt32(dst, Irsdk.OffSessionInfoUpdate, sessionInfoUpdate);
            LE.WriteInt32(dst, Irsdk.OffSessionInfoLen, SessionInfoCapacity);
            LE.WriteInt32(dst, Irsdk.OffSessionInfoOffset, SessionInfoOffset);
            LE.WriteInt32(dst, Irsdk.OffNumVars, Vars.Count);
            LE.WriteInt32(dst, Irsdk.OffVarHeaderOffset, VarHeaderOffset);
            LE.WriteInt32(dst, Irsdk.OffNumBuf, NumBuf);
            LE.WriteInt32(dst, Irsdk.OffBufLen, BufLen);
            for (int i = 0; i < Irsdk.MaxBufs; i++)
            {
                int tc = (tickCounts != null && i < tickCounts.Length) ? tickCounts[i] : 0;
                LE.WriteInt32(dst, Irsdk.VarBufTickCountOffset(i), i < NumBuf ? tc : 0);
                LE.WriteInt32(dst, Irsdk.VarBufOffsetOffset(i), BufOffsets[i]);
            }
        }

        /// <summary>Renders all irsdk_varHeader records (numVars * 144 bytes) into a new array.</summary>
        public byte[] BuildVarHeaderTable()
        {
            var b = new byte[Vars.Count * Irsdk.VarHeaderSize];
            for (int i = 0; i < Vars.Count; i++)
            {
                var v = Vars[i];
                int o = i * Irsdk.VarHeaderSize;
                LE.WriteInt32(b, o + Irsdk.VhOffType, (int)v.Type);
                LE.WriteInt32(b, o + Irsdk.VhOffOffset, v.Offset);
                LE.WriteInt32(b, o + Irsdk.VhOffCount, v.Count);
                b[o + Irsdk.VhOffCountAsTime] = (byte)(v.CountAsTime ? 1 : 0);
                LE.WriteFixedString(b, o + Irsdk.VhOffName, Irsdk.MaxString, v.Name);
                LE.WriteFixedString(b, o + Irsdk.VhOffDesc, Irsdk.MaxDesc, v.Desc);
                LE.WriteFixedString(b, o + Irsdk.VhOffUnit, Irsdk.MaxString, v.Unit);
            }
            return b;
        }
    }
}
