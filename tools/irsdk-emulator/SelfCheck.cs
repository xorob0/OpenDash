using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace IrsdkEmulator
{
    /// <summary>
    /// --selfcheck: builds a full layout into a byte array and reads it back exactly the way SimHub's
    /// iRacingSDK wrapper does (Marshal.PtrToStructure with the same [StructLayout]/[MarshalAs] declarations,
    /// FindLatestBuf by max tickCount, values via ReadInt32/ReadSingle/ReadDouble/ReadBoolean at bufOffset + var.offset,
    /// YAML up to the first NUL). Then loads the scenario and simulates a few seconds in memory.
    /// </summary>
    public static class SelfCheck
    {
        // ---- replicas of the decompiled SimHub structs (iRacingSDK.iRSDKHeader / VarBuf / VarHeader) ----

        [StructLayout(LayoutKind.Sequential)]
        public struct VarBuf
        {
            public int tickCount;
            public int bufOffset;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public int[] pad;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class iRSDKHeader
        {
            public int ver;
            public int status;
            public int tickRate;
            public int sessionInfoUpdate;
            public int sessionInfoLen;
            public int sessionInfoOffset;
            public int numVars;
            public int varHeaderOffset;
            public int numBuf;
            public int bufLen;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public int[] pad1;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public VarBuf[] varBuf;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct VarHeader
        {
            public int type;
            public int offset;
            public int count;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public int[] pad;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string desc;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string unit;
        }

        private static int _failures;
        private static int _checks;

        private static void Check(bool ok, string what)
        {
            _checks++;
            if (!ok) { _failures++; Console.WriteLine("  FAIL: " + what); }
        }

        private static void Section(string s) { Console.WriteLine("== " + s); }

        public static bool Run(string scenarioPath)
        {
            _failures = 0; _checks = 0;
            try
            {
                CheckStructSizes();
                CheckLayoutRoundTrip();
                CheckJson();
                CheckYamlTemplate();
                if (scenarioPath != null) CheckScenario(scenarioPath);
                else Console.WriteLine("== scenario check skipped (no scenario found)");
            }
            catch (Exception ex)
            {
                _failures++;
                Console.WriteLine("  EXCEPTION: " + ex);
            }
            Console.WriteLine();
            Console.WriteLine(_failures == 0 ? "SELFCHECK PASSED (" + _checks + " checks)" : "SELFCHECK FAILED: " + _failures + " of " + _checks + " checks failed");
            return _failures == 0;
        }

        private static void CheckStructSizes()
        {
            Section("struct sizes (Marshal.SizeOf of SimHub's declarations)");
            int h = Marshal.SizeOf(typeof(iRSDKHeader)), vb = Marshal.SizeOf(typeof(VarBuf)), vh = Marshal.SizeOf(typeof(VarHeader));
            Console.WriteLine("  iRSDKHeader=" + h + " VarBuf=" + vb + " VarHeader=" + vh);
            Check(h == Irsdk.HeaderSize, "iRSDKHeader size " + h + " != " + Irsdk.HeaderSize);
            Check(vb == Irsdk.VarBufSize, "VarBuf size " + vb + " != " + Irsdk.VarBufSize);
            Check(vh == Irsdk.VarHeaderSize, "VarHeader size " + vh + " != " + Irsdk.VarHeaderSize);
        }

        private static T Marshal_Read<T>(byte[] image, int offset)
        {
            var handle = GCHandle.Alloc(image, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(IntPtr.Add(handle.AddrOfPinnedObject(), offset), typeof(T));
            }
            finally { handle.Free(); }
        }

        private static void CheckLayoutRoundTrip()
        {
            Section("layout round trip (full catalogue + custom vars, 3 buffers)");
            var table = new VarTable();
            var defs = new List<VarDef>();
            foreach (var e in Catalog.Entries) defs.Add(e.Def.Clone());
            defs.Add(new VarDef { Name = "CustomDouble", Type = IrVarType.Double, Count = 3, Unit = "u", Desc = "custom double array" });
            defs.Add(new VarDef { Name = "CustomBits", Type = IrVarType.BitField, Count = 1, Unit = "irsdk_Flags", Desc = "custom bits" });
            defs.Add(new VarDef { Name = "CustomChar", Type = IrVarType.Char, Count = 5, Unit = "", Desc = "custom char" });
            foreach (var d in defs) table.Add(d);

            // distinct, type-appropriate values per element
            int k = 1;
            foreach (var s in table.Slots)
            {
                for (int i = 0; i < s.Values.Length; i++, k++)
                {
                    switch (s.Def.Type)
                    {
                        case IrVarType.Bool: s.Values[i] = (k % 3 == 0) ? 1 : 0; break;
                        case IrVarType.Char: s.Values[i] = 65 + (k % 26); break;
                        case IrVarType.Int: s.Values[i] = (k % 2 == 0) ? -k * 7 : k * 7; break;
                        case IrVarType.BitField: s.Values[i] = (k % 4 == 0) ? 0x80000000u : (uint)(k * 0x1001); break;
                        case IrVarType.Float: s.Values[i] = (k % 2 == 0 ? -1 : 1) * (k + 0.25); break;
                        case IrVarType.Double: s.Values[i] = k * 1000.125; break;
                    }
                }
            }
            table.Force("SessionFlags", 0, 0x80000004u);
            table.Force("CarIdxLapDistPct", 63, -1);

            var layout = ShmLayout.Build(table.Defs(), 60, 3, 4096);
            var sink = new MemorySink(layout.TotalSize);
            var header = new byte[Irsdk.HeaderSize];
            int[] ticks = { 41, 42, 40, 0 };
            layout.WriteHeader(header, 1, 7, ticks);
            sink.Write(0, header, 0, header.Length);
            var vh = layout.BuildVarHeaderTable();
            sink.Write(layout.VarHeaderOffset, vh, 0, vh.Length);
            string yaml = "---\nWeekendInfo:\n TrackName: selfcheck\n...\n";
            var yb = Encoding.ASCII.GetBytes(yaml);
            sink.Write(layout.SessionInfoOffset, yb, 0, yb.Length);
            var buf = new byte[layout.BufLen];
            table.Serialize(buf);
            sink.Write(layout.BufOffsets[1], buf, 0, buf.Length); // buffer 1 has the highest tickCount (42)
            var image = sink.Image;

            // --- header via Marshal.PtrToStructure, like DataFeed.ReadHeader ---
            var h = Marshal_Read<iRSDKHeader>(image, 0);
            Check(h.ver == 2, "ver");
            Check((h.status & 1) == 1, "status connected bit");
            Check(h.tickRate == 60, "tickRate");
            Check(h.sessionInfoUpdate == 7, "sessionInfoUpdate");
            Check(h.sessionInfoLen == layout.SessionInfoCapacity, "sessionInfoLen");
            Check(h.sessionInfoOffset == layout.SessionInfoOffset, "sessionInfoOffset");
            Check(h.numVars == table.Count, "numVars " + h.numVars + " != " + table.Count);
            Check(h.varHeaderOffset == layout.VarHeaderOffset, "varHeaderOffset");
            Check(h.numBuf == 3, "numBuf");
            Check(h.bufLen == layout.BufLen, "bufLen");
            Check(h.varBuf != null && h.varBuf.Length == 4, "varBuf array");
            for (int i = 0; i < 3; i++)
            {
                Check(h.varBuf[i].tickCount == ticks[i], "varBuf[" + i + "].tickCount");
                Check(h.varBuf[i].bufOffset == layout.BufOffsets[i], "varBuf[" + i + "].bufOffset");
            }
            Console.WriteLine("  header ok: numVars=" + h.numVars + " varHeaderOffset=" + h.varHeaderOffset + " sessionInfoOffset=" + h.sessionInfoOffset + " bufLen=" + h.bufLen + " total=" + layout.TotalSize);

            // --- FindLatestBuf (iRSDKHeaderExtensions) ---
            int latest = -1, latestTick = 0;
            for (int i = 0; i < h.numBuf; i++) if (h.varBuf[i].tickCount > latestTick) { latestTick = h.varBuf[i].tickCount; latest = i; }
            Check(latest == 1 && latestTick == 42, "FindLatestBuf picked buffer " + latest + " tick " + latestTick);
            int bufOffset = h.varBuf[latest].bufOffset;

            // --- var headers via Marshal.PtrToStructure, like DataFeed.ReadVariableHeaders ---
            int bad = 0;
            var slots = table.Slots;
            for (int i = 0; i < h.numVars; i++)
            {
                var v = Marshal_Read<VarHeader>(image, h.varHeaderOffset + i * Marshal.SizeOf(typeof(VarHeader)));
                var d = slots[i].Def;
                bool ok = v.name == d.Name && v.type == (int)d.Type && v.offset == d.Offset && v.count == d.Count && v.unit == d.Unit && v.desc == d.Desc;
                if (!ok) { bad++; Console.WriteLine("  var header mismatch #" + i + ": " + v.name + "/" + v.type + "/" + v.offset + "/" + v.count + "/" + v.unit + " vs " + d.Name + "/" + (int)d.Type + "/" + d.Offset + "/" + d.Count + "/" + d.Unit); }
                if (v.pad[0] != 0 && d.CountAsTime == false) { bad++; Console.WriteLine("  countAsTime/pad not zero for " + d.Name); }
            }
            Check(bad == 0, bad + " var header mismatches");
            Console.WriteLine("  " + h.numVars + " var headers read back identically");

            // --- values, like DataFeed.ReadAllValues (maps / arryMaps) ---
            int badVals = 0;
            foreach (var s in slots)
            {
                var d = s.Def;
                int off = bufOffset + d.Offset;
                for (int i = 0; i < d.Count; i++)
                {
                    double expected = s.Values[i];
                    double got;
                    switch (d.Type)
                    {
                        case IrVarType.Bool: got = image[off + i] != 0 ? 1 : 0; expected = expected != 0 ? 1 : 0; break;
                        case IrVarType.Char: got = image[off + i]; break;
                        case IrVarType.Int: got = LE.ReadInt32(image, off + 4 * i); break;
                        case IrVarType.BitField: got = unchecked((uint)LE.ReadInt32(image, off + 4 * i)); break;
                        case IrVarType.Float: got = LE.ReadSingle(image, off + 4 * i); expected = (float)expected; break;
                        default: got = LE.ReadDouble(image, off + 8 * i); break;
                    }
                    if (got != expected) { badVals++; if (badVals < 10) Console.WriteLine("  value mismatch " + d.Name + "[" + i + "]: got " + got + " expected " + expected); }
                }
            }
            Check(badVals == 0, badVals + " value mismatches");
            // spot checks with the exact SimHub conversions
            Check((uint)LE.ReadInt32(image, bufOffset + layout.Find("SessionFlags").Offset) == 0x80000004u, "SessionFlags bitfield 0x80000004 via ReadInt32 cast");
            Check(LE.ReadSingle(image, bufOffset + layout.Find("CarIdxLapDistPct").Offset + 63 * 4) == -1f, "CarIdxLapDistPct[63] == -1");
            Check(LE.ReadDouble(image, bufOffset + layout.Find("SessionTime").Offset) == slots[0].Values[0], "SessionTime double");
            Console.WriteLine("  all " + slots.Sum(x => x.Def.Count) + " values read back identically");

            // --- overlap check: no two variables share bytes, all inside bufLen ---
            var covered = new bool[layout.BufLen];
            int overlaps = 0;
            foreach (var d in layout.Vars)
                for (int b = d.Offset; b < d.Offset + d.ByteSize; b++) { if (covered[b]) overlaps++; covered[b] = true; }
            Check(overlaps == 0, overlaps + " overlapping bytes between variables");
            Check(layout.SessionInfoOffset >= layout.VarHeaderOffset + h.numVars * Irsdk.VarHeaderSize, "session info after var headers");
            Check(layout.BufOffsets[0] >= layout.SessionInfoOffset + layout.SessionInfoCapacity, "buffers after session info");

            // --- YAML, like DataFeed.ReadSessionInfo (first NUL terminates) ---
            var sib = new byte[h.sessionInfoLen];
            Array.Copy(image, h.sessionInfoOffset, sib, 0, sib.Length);
            int nul = Array.IndexOf(sib, (byte)0);
            Check(nul == yb.Length, "NUL terminator position");
            Check(Encoding.ASCII.GetString(sib, 0, nul) == yaml, "session YAML round trip");
        }

        private static void CheckJson()
        {
            Section("json reader");
            var o = Json.ParseObject("{ // comment\n \"a\": 1.5, \"b\": [1, 2, \"0x10\", true, null,], \"c\": {\"x\": \"s\\u0041\\n\"}, \"d\": -3e2, /* c */ \"e\": false }");
            Check(Json.GetDouble(o, "a", 0) == 1.5, "number");
            var l = Json.GetList(o, "b");
            Check(l != null && l.Count == 5 && Json.ToDouble(l[2]) == 16 && (bool)l[3] && l[4] == null, "array with hex string, bool, null, trailing comma");
            Check(Json.GetString(Json.GetObject(o, "c"), "x") == "sA\n", "string escapes");
            Check(Json.GetDouble(o, "d", 0) == -300, "exponent");
            Check(Json.GetBool(o, "e", true) == false, "bool");
            Check(VarTable.ParseRef("CarIdxGear[12]", out var n, out var i) && n == "CarIdxGear" && i == 12, "variable reference parsing");
            Check(FlagCycleDriver.ParseFlags("green|startHidden") == 0x10000004u, "flag name parsing");
        }

        private static void CheckYamlTemplate()
        {
            Section("yaml template");
            var t = new YamlTemplate();
            t.SetTemplate("A: {{a}}\nB: {{ b }}\n");
            t.Set("a", "1");
            t.Set("b", "x");
            string r = t.Render();
            Check(r == "A: 1\nB: x\n", "placeholder substitution: " + r.Replace("\n", "\\n"));
            t.Set("a", "1");
            Check(!t.Dirty, "unchanged value keeps template clean");
            t.Set("a", "2");
            Check(t.Dirty && t.RenderIfChanged(out var r2) && r2.StartsWith("A: 2"), "changed value re-renders");
        }

        private static readonly string[] RequiredKeys =
        {
            // read with a throwing cast in SimHub's Telemetry / IRacingManager
            "SessionTime", "SessionNum", "SessionUniqueID", "SessionFlags", "SessionTimeRemain", "IsReplayPlaying", "IsOnTrack",
            "PlayerCarPosition", "PlayerCarIdx", "LapDistPct", "Gear", "LapCompleted", "LapDeltaToSessionBestLap_OK", "EngineWarnings",
            "OnPitRoad", "RaceLaps", "CarIdxLap", "CarIdxLapCompleted", "CarIdxLapDistPct", "CarIdxTrackSurface", "CarIdxOnPitRoad",
            "CarIdxPosition", "CarIdxClassPosition", "CarIdxF2Time", "CarIdxEstTime", "CarIdxRPM", "CarIdxGear",
            // read with GetSingleValueOrDefault but needed for a meaningful dash
            "RPM", "Speed", "Throttle", "Brake", "Clutch", "FuelLevel", "FuelLevelPct", "LapCurrentLapTime", "LapLastLapTime", "LapBestLapTime",
            "WaterTemp", "OilTemp", "OilPress", "Voltage", "LFtempCL", "LFtempCM", "LFtempCR", "RFtempCL", "RFtempCM", "RFtempCR",
            "LRtempCL", "LRtempCM", "LRtempCR", "RRtempCL", "RRtempCM", "RRtempCR", "LFcoldPressure", "RFcoldPressure", "LRcoldPressure", "RRcoldPressure",
            "LFwearM", "RFwearM", "LRwearL", "LRwearM", "LRwearR", "RRwearM", "Yaw", "YawNorth", "Pitch", "Roll", "LapDist", "VelocityX", "VelocityY", "VelocityZ",
            "LatAccel", "LongAccel", "VertAccel", "SessionState", "PlayerCarClassPosition", "LapBestLap", "SessionLapsRemain", "SessionLapsRemainEx",
        };

        private static void CheckScenario(string path)
        {
            Section("scenario " + path);
            var sc = Scenario.Load(path);
            foreach (var w in sc.Warnings) Console.WriteLine("  warning: " + w);
            var probe = sc.CreateVarTable();
            var layout = ShmLayout.Build(probe.Defs(), sc.TickRate, sc.NumBuf, sc.SessionInfoCapacity);
            var sink = new MemorySink((int)Math.Max(sc.MapSize, layout.TotalSize));
            using (var runner = new Runner(sc, sink))
            {
                var logs = new List<string>();
                runner.Log = s => logs.Add(s);
                runner.Start();
                var image = sink.Image;
                var h0 = Marshal_Read<iRSDKHeader>(image, 0);
                Check((h0.status & 1) == 1, "status connected after Start()");
                Check(h0.sessionInfoUpdate >= 1, "sessionInfoUpdate >= 1");
                int nul = Array.IndexOf(image, (byte)0, h0.sessionInfoOffset, h0.sessionInfoLen) - h0.sessionInfoOffset;
                string yaml = Encoding.ASCII.GetString(image, h0.sessionInfoOffset, nul);
                Console.WriteLine("  session YAML: " + yaml.Length + " chars, " + yaml.Split('\n').Length + " lines");
                foreach (var key in new[] { "WeekendInfo:", "SessionInfo:", "DriverInfo:", "SplitTimeInfo:", "TrackLength:", "DriverCarIdx:", "DriverCarRedLine:", "Sessions:", "Drivers:" })
                    Check(yaml.Contains(key), "YAML contains " + key);
                Check(!yaml.Contains("{{"), "no unresolved {{placeholders}} in YAML");
                Check(yaml.IndexOf('\r') < 0, "YAML uses \\n line endings");

                int missing = 0;
                foreach (var k in RequiredKeys) if (!runner.Vars.Has(k)) { missing++; Console.WriteLine("  missing variable: " + k); }
                Check(missing == 0, missing + " required variables missing");
                foreach (var k in new[] { "CarIdxLap", "CarIdxLapCompleted", "CarIdxLapDistPct", "CarIdxTrackSurface", "CarIdxOnPitRoad", "CarIdxPosition", "CarIdxClassPosition", "CarIdxF2Time", "CarIdxEstTime", "CarIdxRPM", "CarIdxGear", "CarIdxLastLapTime", "CarIdxBestLapTime" })
                {
                    var s = runner.Vars.Find(k);
                    Check(s != null && s.Def.Count == 64, k + " has 64 elements");
                }

                int ticks = sc.TickRate * 3;
                for (int i = 0; i < ticks; i++) runner.Step();
                var h = Marshal_Read<iRSDKHeader>(image, 0);
                int latest = -1, latestTick = 0;
                for (int i = 0; i < h.numBuf; i++) if (h.varBuf[i].tickCount > latestTick) { latestTick = h.varBuf[i].tickCount; latest = i; }
                Check(latest >= 0 && latestTick == runner.Tick, "latest buffer tickCount == " + runner.Tick + " (got " + latestTick + ")");
                int bo = h.varBuf[latest].bufOffset;
                double st = LE.ReadDouble(image, bo + layout.Find("SessionTime").Offset);
                double st0 = Json.ToDouble(sc.InitialValues.ContainsKey("SessionTime") ? sc.InitialValues["SessionTime"] : (object)0.0);
                Check(Math.Abs(st - st0 - 3.0) < 0.01, "SessionTime advanced by 3 s (" + st0 + " -> " + st + ")");
                var v = runner.Vars;
                int player = v.Has("PlayerCarIdx") ? (int)v.Get("PlayerCarIdx") : 0;
                Check(v.Get("RPM") > 0, "RPM > 0 after 3 s (SimHub treats RPM==0 on track as corrupt)");
                Check(v.Get("LRwearL") > 0 || v.Get("LRwearM") > 0 || v.Get("LRwearR") > 0, "LR wear non-zero (SimHub treats all-zero wear as corrupt)");
                Check(v.Get("Voltage") > 0, "Voltage > 0 (ignition on)");
                Check((VarTable.ToInt(v.Get("EngineWarnings")) & 0x8) == 0, "EngineStalled bit clear (engine started)");
                Check(v.Get("CarIdxTrackSurface", player) != -1, "player car has track surface data (not spectating)");
                Check(v.Get("CarIdxLapDistPct", player) >= 0, "player CarIdxLapDistPct >= 0");
                Check(v.Get("SessionTime") != 0 || v.Get("SessionUniqueID") != 0, "SessionTime or SessionUniqueID non-zero (IRacing_NewData accepts sample)");
                Check(yaml.Contains("DriverCarIdx: " + player), "YAML DriverCarIdx matches PlayerCarIdx " + player);
                Console.WriteLine("  after 3 s: " + runner.StatusLine());
                Console.WriteLine("  " + v.Count + " variables, bufLen " + layout.BufLen + ", total image " + layout.TotalSize + " bytes, " + logs.Count + " log lines");
                foreach (var l in logs.Take(6)) Console.WriteLine("    " + l);
            }
        }
    }
}
