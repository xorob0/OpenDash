using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using IrsdkEmulator;

namespace AcsmEmulator
{
    /// <summary>
    /// Owns the pages for one scenario: writes them once on <see cref="Start"/>, then once a tick, physics first
    /// and the static page last, the order AC itself refreshes them in (the static page changes only between
    /// sessions, but writing it every tick costs nothing and means a timeline change to it is seen at once).
    /// </summary>
    public sealed class Runner : IDisposable
    {
        public readonly Scenario Scenario;
        public readonly Pages Pages;
        public readonly List<Driver> Drivers;
        public readonly SimContext Ctx = new SimContext();
        public IPageSink Sink;
        public Action<string> Log = s => { };

        private int _nextEvent;
        private double _loopStart;
        private bool _started;

        public long Tick { get; private set; }
        public double Time => Tick / (double)Scenario.TickRate;

        /// <summary>The mappings this scenario publishes. SimHub's app page only when there is a field to fill it.</summary>
        public static IEnumerable<string> MapsFor(Scenario sc)
        {
            yield return Pages.PhysicsMap;
            yield return Pages.GraphicsMap;
            yield return Pages.StaticMap;
            if (sc.HasField) yield return Pages.OpponentsMap;
        }

        public Runner(Scenario sc, IPageSink sink)
        {
            Scenario = sc;
            Sink = sink;
            Pages = sc.CreatePages();
            Drivers = sc.CreateDrivers();
            Ctx.Pages = Pages;
            Ctx.Rng = new Random(sc.Seed);
            Ctx.Log = s => Log(s);
        }

        public void Start()
        {
            if (_started) return;
            _started = true;
            Ctx.Dt = 1.0 / Scenario.TickRate;
            Ctx.Time = 0;
            Tick = 1;
            Ctx.Tick = Tick;
            foreach (var d in Drivers) d.Init(Ctx);
            foreach (var d in Drivers) if (d.Enabled) d.Tick(Ctx);
            ApplyTimeline();
            Write();
        }

        public void Step()
        {
            if (!_started) Start();
            Tick++;
            Ctx.Tick = Tick;
            Ctx.Dt = 1.0 / Scenario.TickRate;
            Ctx.Time = Time;
            foreach (var d in Drivers) if (d.Enabled) d.Tick(Ctx);
            ApplyTimeline();
            Write();
        }

        private void Write()
        {
            foreach (var map in MapsFor(Scenario)) Sink.Write(map, Pages.Bytes(map));
        }

        private void ApplyTimeline()
        {
            var tl = Scenario.Timeline;
            if (Scenario.LoopSeconds > 0 && Ctx.Time >= _loopStart + Scenario.LoopSeconds)
            {
                _loopStart += Scenario.LoopSeconds;
                _nextEvent = 0;
                foreach (var e in tl) e.Fired = false;
                Log("timeline: looping after " + Scenario.LoopSeconds.ToString("0.##", CultureInfo.InvariantCulture) + "s");
            }
            double elapsed = Ctx.Time - _loopStart;
            while (_nextEvent < tl.Count && tl[_nextEvent].At <= elapsed)
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
                    Log(prefix + (Pages.Apply(kv.Key, kv.Value, false) ? "set " + kv.Key + " = " + Json.Serialize(kv.Value) : "set: unknown value " + kv.Key));
            if (ev.Pin != null)
                foreach (var kv in ev.Pin)
                    Log(prefix + (Pages.Apply(kv.Key, kv.Value, true) ? "pin " + kv.Key + " = " + Json.Serialize(kv.Value) : "pin: unknown value " + kv.Key));
            if (ev.Unpin != null)
                foreach (var o in ev.Unpin)
                    Log(prefix + (Pages.Pin(Json.ToText(o), false) ? "unpin " + Json.ToText(o) : "unpin: unknown value " + Json.ToText(o)));
            foreach (var du in ev.DriverUpdates)
            {
                string target = Json.GetString(du, "name") ?? Json.GetString(du, "type");
                bool found = false;
                foreach (var d in Drivers)
                {
                    if (d.Name != target && d.Type != target) continue;
                    d.Merge(du);
                    found = true;
                    Log(prefix + "driver " + d.Name + " updated (" + d.Describe() + (d.Enabled ? "" : ", disabled") + ")");
                }
                if (found) continue;
                string type = Json.GetString(du, "type");
                if (string.IsNullOrEmpty(type)) { Log(prefix + "driver update: no driver named " + target); continue; }
                var nd = Driver.Create(type);
                nd.Name = Json.GetString(du, "name", type);
                nd.Merge(du);
                nd.Init(Ctx);
                Drivers.Add(nd);
                Log(prefix + "driver " + nd.Name + " added (" + nd.Describe() + ")");
            }
        }

        /// <summary>
        /// Says the session is over: Graphics.Status AC_OFF, which SimHub reads as "not in race" on its next poll,
        /// before the process exits and its AC reader stops finding acs.exe at all.
        /// </summary>
        public void Stop()
        {
            if (!_started) return;
            try
            {
                Pages.Apply("Graphics.Status", "AC_OFF", false);
                Sink.Write(Pages.GraphicsMap, Pages.Bytes(Pages.GraphicsMap));
            }
            catch { }
        }

        public string StatusLine()
        {
            var p = Pages;
            var g = p.Graphics;
            var ph = p.Physics;
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append('[').Append(Time.ToString("0.0", inv).PadLeft(7)).Append("s] ");
            sb.Append("packet ").Append(g.PacketId).Append(" | ");
            sb.Append(g.Status).Append(' ').Append(g.Session).Append(" left ").Append((g.SessionTimeLeft / 1000).ToString("0", inv)).Append(" s | ");
            sb.Append("rpm ").Append(ph.Rpms).Append(" G").Append(ph.Gear - 1).Append(' ').Append(ph.SpeedKmh.ToString("0", inv)).Append(" km/h | ");
            sb.Append("lap ").Append(g.CompletedLaps + 1).Append('/').Append(g.NumberOfLaps);
            sb.Append(" cur ").Append((g.iCurrentTime / 1000.0).ToString("0.00", inv));
            sb.Append(" last ").Append((g.iLastTime / 1000.0).ToString("0.000", inv));
            sb.Append(" best ").Append((g.iBestTime / 1000.0).ToString("0.000", inv)).Append(" | ");
            sb.Append("fuel ").Append(ph.Fuel.ToString("0.0", inv)).Append(" | ");
            sb.Append("flag ").Append(g.Flags).Append(" | ");
            sb.Append("pos ").Append(g.Position).Append('/').Append(p.StaticInfo.NumCars).Append(" | ");
            sb.Append("TC ").Append(ph.TC.ToString("0.###", inv)).Append(" ABS ").Append(ph.Abs.ToString("0.###", inv)).Append(" | ");
            sb.Append("pit ").Append(g.IsInPit).Append(g.isInPitLane).Append(" limiter ").Append(ph.PitLimiterOn).Append(" | ");
            sb.Append("tyre ").Append(ph.TyreCoreTemperature[0].ToString("0", inv)).Append('C');
            return sb.ToString();
        }

        public void Dispose()
        {
            Sink?.Dispose();
            Sink = null;
        }
    }
}
