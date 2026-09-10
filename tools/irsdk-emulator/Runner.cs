using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace IrsdkEmulator
{
    /// <summary>
    /// Owns the shared-memory image for one scenario: writes the header, the var headers and the session YAML,
    /// then produces one telemetry buffer per <see cref="Step"/> (rotating through the buffers like the sim does),
    /// bumps the buffer's tickCount and signals the data-valid event.
    /// </summary>
    public sealed class Runner : IDisposable
    {
        public readonly Scenario Scenario;
        public readonly VarTable Vars;
        public readonly ShmLayout Layout;
        public readonly List<Driver> Drivers;
        public readonly YamlTemplate Yaml = new YamlTemplate();
        public readonly SimContext Ctx = new SimContext();
        public IShmSink Sink;
        public Action<string> Log = s => { };

        private readonly byte[] _header = new byte[Irsdk.HeaderSize];
        private readonly byte[] _buf;
        private readonly int[] _tickCounts;
        private readonly Encoding _yamlEncoding = Encoding.GetEncoding(28591); // Latin-1: SimHub decodes with cp1252, ASCII-compatible
        private int _sessionInfoUpdate;
        private int _nextEvent;
        private string _currentYaml = "";
        private bool _started;

        public long Tick { get; private set; }
        public double Time => Tick / (double)Scenario.TickRate;
        public int SessionInfoUpdate => _sessionInfoUpdate;
        public int CurrentYamlLength => _currentYaml.Length;

        public Runner(Scenario sc, IShmSink sink)
        {
            Scenario = sc;
            Sink = sink;
            Vars = sc.CreateVarTable();
            Layout = ShmLayout.Build(Vars.Defs(), sc.TickRate, sc.NumBuf, sc.SessionInfoCapacity);
            if (sink.Capacity < Layout.TotalSize)
                throw new InvalidOperationException("shared memory too small: need " + Layout.TotalSize + " bytes, have " + sink.Capacity + " (raise \"mapSize\" in the scenario)");
            _buf = new byte[Layout.BufLen];
            _tickCounts = new int[Irsdk.MaxBufs];
            Drivers = sc.CreateDrivers();
            Ctx.Vars = Vars;
            Ctx.Yaml = Yaml;
            Ctx.Rng = new Random(sc.Seed);
            Ctx.Log = s => Log(s);
            Yaml.Log = s => Log(s);
            if (!string.IsNullOrEmpty(sc.SessionYamlPath)) Yaml.LoadFile(sc.SessionYamlPath);
            else Yaml.SetTemplate(MinimalYaml());
            foreach (var kv in sc.YamlValues) Yaml.Set(kv.Key, kv.Value);
        }

        /// <summary>Writes everything into the sink and flips status to connected. Call once before Step().</summary>
        public void Start()
        {
            if (_started) return;
            _started = true;
            Ctx.Dt = 1.0 / Scenario.TickRate;
            Ctx.Time = 0;
            Ctx.Tick = 0;
            foreach (var d in Drivers) d.Init(Ctx);

            // 1. header with status = 0 while we fill the rest (an attached reader sees "disconnected" meanwhile)
            _sessionInfoUpdate = 0;
            Layout.WriteHeader(_header, 0, _sessionInfoUpdate, _tickCounts);
            Sink.Write(0, _header, 0, _header.Length);
            // 2. var headers
            var vh = Layout.BuildVarHeaderTable();
            if (vh.Length > 0) Sink.Write(Layout.VarHeaderOffset, vh, 0, vh.Length);
            // 3. session info YAML
            Yaml.RenderIfChanged(out var yaml);
            WriteSessionInfo(yaml ?? "");
            // 4. first telemetry sample in buffer 0 (tickCount 1) so FindLatestBuf has something to pick
            Tick = 1;
            Ctx.Tick = Tick;
            Vars.Serialize(_buf);
            Sink.Write(Layout.BufOffsets[0], _buf, 0, _buf.Length);
            _tickCounts[0] = (int)Tick;
            // 5. header with status = connected
            Layout.WriteHeader(_header, Irsdk.StatusConnected, _sessionInfoUpdate, _tickCounts);
            Sink.Write(0, _header, 0, _header.Length);
            Sink.Signal();
        }

        private void WriteSessionInfo(string yaml)
        {
            var bytes = _yamlEncoding.GetBytes(yaml);
            if (bytes.Length + 1 > Layout.SessionInfoCapacity)
                throw new InvalidOperationException("session YAML is " + bytes.Length + " bytes but sessionInfoCapacity is " + Layout.SessionInfoCapacity + "; raise \"sessionInfoCapacity\" in the scenario");
            Sink.Write(Layout.SessionInfoOffset, bytes, 0, bytes.Length);
            Sink.Write(Layout.SessionInfoOffset + bytes.Length, new byte[1], 0, 1); // NUL terminator (SimHub stops at the first 0 byte)
            _currentYaml = yaml;
            _sessionInfoUpdate++;
            Sink.WriteInt32(Irsdk.OffSessionInfoUpdate, _sessionInfoUpdate);
        }

        /// <summary>One 1/tickRate step: drivers, timeline, YAML refresh, buffer write, signal.</summary>
        public void Step()
        {
            if (!_started) Start();
            Tick++;
            Ctx.Tick = Tick;
            Ctx.Dt = 1.0 / Scenario.TickRate;
            Ctx.Time = Time;

            foreach (var d in Drivers)
            {
                if (!d.Enabled) continue;
                d.Tick(Ctx);
            }
            ApplyTimeline();

            if (Yaml.Dirty && Yaml.RenderIfChanged(out var yaml))
            {
                WriteSessionInfo(yaml);
                Log("session info updated (#" + _sessionInfoUpdate + ", " + yaml.Length + " chars)");
            }

            Vars.Serialize(_buf);
            int bi = (int)(Tick % Layout.NumBuf);
            Sink.Write(Layout.BufOffsets[bi], _buf, 0, _buf.Length);
            _tickCounts[bi] = (int)Tick;
            Sink.WriteInt32(Irsdk.VarBufTickCountOffset(bi), (int)Tick);
            Sink.Signal();
        }

        private void ApplyTimeline()
        {
            var tl = Scenario.Timeline;
            while (_nextEvent < tl.Count && tl[_nextEvent].At <= Ctx.Time)
            {
                var ev = tl[_nextEvent++];
                if (ev.Fired) continue;
                ev.Fired = true;
                ApplyEvent(ev);
            }
        }

        public void ApplyEvent(TimelineEvent ev)
        {
            string prefix = "timeline @" + ev.At.ToString("0.##", CultureInfo.InvariantCulture) + "s: ";
            if (!string.IsNullOrEmpty(ev.LogText)) Log(prefix + ev.LogText);
            if (ev.Set != null)
                foreach (var kv in ev.Set)
                    if (!Vars.Apply(kv.Key, kv.Value, false)) Log(prefix + "set: unknown variable " + kv.Key);
                    else Log(prefix + "set " + kv.Key + " = " + Json.Serialize(kv.Value));
            if (ev.Pin != null)
                foreach (var kv in ev.Pin)
                    if (!Vars.Apply(kv.Key, kv.Value, true)) Log(prefix + "pin: unknown variable " + kv.Key);
                    else Log(prefix + "pin " + kv.Key + " = " + Json.Serialize(kv.Value));
            if (ev.Unpin != null)
                foreach (var o in ev.Unpin)
                {
                    string r = Json.ToText(o);
                    if (VarTable.ParseRef(r, out var n, out var i) && Vars.Pin(n, i, false)) Log(prefix + "unpin " + r);
                    else Log(prefix + "unpin: unknown variable " + r);
                }
            if (ev.Yaml != null)
                foreach (var kv in ev.Yaml)
                {
                    Yaml.Set(kv.Key, Json.ToText(kv.Value));
                    Log(prefix + "yaml " + kv.Key + " = " + Json.ToText(kv.Value));
                }
            if (!string.IsNullOrEmpty(ev.SessionYamlPath))
            {
                Yaml.LoadFile(ev.SessionYamlPath);
                Log(prefix + "session YAML template switched to " + ev.SessionYamlPath);
            }
            foreach (var du in ev.DriverUpdates)
            {
                string target = Json.GetString(du, "name") ?? Json.GetString(du, "type");
                bool found = false;
                foreach (var d in Drivers)
                {
                    if (d.Name == target || d.Type == target)
                    {
                        d.Merge(du);
                        found = true;
                        Log(prefix + "driver " + d.Name + " updated (" + d.Describe() + (d.Enabled ? "" : ", disabled") + ")");
                    }
                }
                if (!found)
                {
                    string type = Json.GetString(du, "type");
                    if (!string.IsNullOrEmpty(type))
                    {
                        var nd = Driver.Create(type);
                        nd.Name = Json.GetString(du, "name", type);
                        nd.Merge(du);
                        nd.Init(Ctx);
                        Drivers.Add(nd);
                        Log(prefix + "driver " + nd.Name + " added (" + nd.Describe() + ")");
                    }
                    else Log(prefix + "driver update: no driver named " + target);
                }
            }
        }

        /// <summary>Marks the sim as disconnected (status &amp; 1 == 0). Called on shutdown.</summary>
        public void Stop()
        {
            if (!_started) return;
            try
            {
                Sink.WriteInt32(Irsdk.OffStatus, 0);
                Sink.Signal();
            }
            catch { }
        }

        public string StatusLine()
        {
            var v = Vars;
            var inv = CultureInfo.InvariantCulture;
            double rem = v.Get("SessionTimeRemain");
            string remS = rem >= Irsdk.UnlimitedTime ? "unl" : rem.ToString("0", inv);
            var sb = new StringBuilder();
            sb.Append('[').Append(Time.ToString("0.0", inv).PadLeft(7)).Append("s] ");
            sb.Append("tick ").Append(Tick).Append(" | ");
            sb.Append("t ").Append(v.Get("SessionTime").ToString("0.0", inv)).Append(" rem ").Append(remS).Append(" | ");
            sb.Append("rpm ").Append(v.Get("RPM").ToString("0", inv)).Append(" G").Append(VarTable.ToInt(v.Get("Gear")));
            sb.Append(' ').Append((v.Get("Speed") * 3.6).ToString("0", inv)).Append(" km/h | ");
            sb.Append("lap ").Append(VarTable.ToInt(v.Get("Lap"))).Append('/').Append(VarTable.ToInt(v.Get("RaceLaps")));
            sb.Append(" cur ").Append(v.Get("LapCurrentLapTime").ToString("0.00", inv));
            sb.Append(" last ").Append(v.Get("LapLastLapTime").ToString("0.000", inv));
            sb.Append(" best ").Append(v.Get("LapBestLapTime").ToString("0.000", inv)).Append(" | ");
            sb.Append("fuel ").Append(v.Get("FuelLevel").ToString("0.0", inv)).Append(" | ");
            sb.Append("flags ").Append(v.Format("SessionFlags")).Append(" | ");
            sb.Append("pos ").Append(VarTable.ToInt(v.Get("PlayerCarPosition"))).Append('/').Append(VarTable.ToInt(v.Get("PlayerCarClassPosition"))).Append(" | ");
            sb.Append("TC ").Append(v.Format("dcTractionControl", "0")).Append(" ABS ").Append(v.Format("dcABS", "0")).Append(" | ");
            sb.Append("warn ").Append(v.Format("EngineWarnings"));
            if (v.Has("LFtempCM")) sb.Append(" | tyre ").Append(v.Get("LFtempCM").ToString("0", inv)).Append("C");
            return sb.ToString();
        }

        public void Dispose()
        {
            Sink?.Dispose();
            Sink = null;
        }

        private static string MinimalYaml()
        {
            return "---\nWeekendInfo:\n TrackName: emulator\n TrackID: 1\n TrackLength: 5.00 km\n TrackDisplayName: Emulator\n TrackConfigName: \n EventType: Race\n" +
                   "SessionInfo:\n Sessions:\n - SessionNum: 0\n   SessionLaps: unlimited\n   SessionTime: unlimited\n   SessionType: Race\n" +
                   "DriverInfo:\n DriverCarIdx: 0\n DriverCarRedLine: 8000.000\n Drivers:\n - CarIdx: 0\n   UserName: Emulator\n   CarNumber: \"1\"\n   CarNumberRaw: 1\n   CarPath: emulator\n   CarClassID: 0\n   CarScreenName: Emulator\n   CarClassShortName: \n" +
                   "SplitTimeInfo:\n Sectors:\n - SectorNum: 0\n   SectorStartPct: 0.000000\n...\n";
        }
    }
}
