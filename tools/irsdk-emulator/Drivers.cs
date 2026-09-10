using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace IrsdkEmulator
{
    /// <summary>Everything a driver can see and touch during a tick.</summary>
    public sealed class SimContext
    {
        public VarTable Vars;
        public YamlTemplate Yaml;
        /// <summary>Loose coupling between drivers (rpmSweep publishes "rpmSweep.cycle", lapTimer "player.pct", ...).</summary>
        public readonly Dictionary<string, double> Signals = new Dictionary<string, double>(StringComparer.Ordinal);
        /// <summary>Simulated seconds since the emulator started.</summary>
        public double Time;
        public double Dt;
        public long Tick;
        public Random Rng = new Random(12345);
        public Action<string> Log = s => { };

        public int PlayerCarIdx => (int)Vars.Get("PlayerCarIdx");

        public double Signal(string key, double def)
        {
            return Signals.TryGetValue(key, out var v) ? v : def;
        }

        public bool HasSignal(string key) { return Signals.ContainsKey(key); }
    }

    /// <summary>A scripted behaviour that updates variables every tick. Parameters are re-read each tick so the
    /// timeline can change them on the fly.</summary>
    public abstract class Driver
    {
        public string Type;
        public string Name;
        public bool Enabled = true;
        public Dictionary<string, object> Params = new Dictionary<string, object>(StringComparer.Ordinal);

        protected double P(string key, double def) { return Json.GetDouble(Params, key, def); }
        protected int PI(string key, int def) { return Json.GetInt(Params, key, def); }
        protected string PS(string key, string def) { return Json.GetString(Params, key, def); }
        protected bool PB(string key, bool def) { return Json.GetBool(Params, key, def); }
        protected List<object> PL(string key) { return Json.GetList(Params, key); }

        public virtual void Init(SimContext ctx) { }
        public abstract void Tick(SimContext ctx);

        public virtual string Describe() { return Type; }

        /// <summary>Merges a JSON object into the parameters ("enabled" toggles the driver).</summary>
        public void Merge(Dictionary<string, object> p)
        {
            if (p == null) return;
            foreach (var kv in p)
            {
                if (kv.Key == "type" || kv.Key == "name" || kv.Key == "driver") continue;
                if (kv.Key == "enabled") { Enabled = Json.ToDouble(kv.Value) != 0; continue; }
                Params[kv.Key] = kv.Value;
            }
        }

        public static Driver Create(string type)
        {
            switch ((type ?? "").Trim())
            {
                case "clock": return new ClockDriver();
                case "rpmSweep": return new RpmSweepDriver();
                case "gearFromRpm": return new GearFromRpmDriver();
                case "lapTimer": return new LapTimerDriver();
                case "fuelBurn": return new FuelBurnDriver();
                case "flagCycle": return new FlagCycleDriver();
                case "pitLimiterToggle": return new PitLimiterToggleDriver();
                case "field": return new FieldDriver();
                case "sine": return new SineDriver();
                case "toggle": return new ToggleDriver();
                default: throw new FormatException("unknown driver type '" + type + "'. Known: clock, rpmSweep, gearFromRpm, lapTimer, fuelBurn, flagCycle, pitLimiterToggle, field, sine, toggle");
            }
        }

        public static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static double Frac(double v) { return v - Math.Floor(v); }
    }

    /// <summary>Advances SessionTime / SessionTick / SessionTimeRemain / SessionTimeOfDay. Always present.</summary>
    public sealed class ClockDriver : Driver
    {
        public ClockDriver() { Type = "clock"; }

        public override void Tick(SimContext c)
        {
            var v = c.Vars;
            v.Set("SessionTime", v.Get("SessionTime") + c.Dt);
            v.Set("SessionTick", v.Get("SessionTick") + 1);
            if (v.Has("SessionTimeRemain"))
            {
                double rem = v.Get("SessionTimeRemain");
                if (rem < Irsdk.UnlimitedTime) v.Set("SessionTimeRemain", Math.Max(0, rem - c.Dt));
            }
            if (v.Has("SessionTimeOfDay")) v.Set("SessionTimeOfDay", v.Get("SessionTimeOfDay") + c.Dt);
            if (v.Has("ReplaySessionTime")) v.Set("ReplaySessionTime", v.Get("SessionTime"));
            if (v.Has("ReplaySessionNum")) v.Set("ReplaySessionNum", v.Get("SessionNum"));
        }
    }

    /// <summary>RPM ramps min..max over "period" seconds (ease-out, like accelerating in a gear), then drops.
    /// Publishes rpmSweep.cycle / rpmSweep.phase and drives ShiftIndicatorPct.</summary>
    public sealed class RpmSweepDriver : Driver
    {
        public RpmSweepDriver() { Type = "rpmSweep"; }

        public override void Tick(SimContext c)
        {
            double period = Math.Max(0.05, P("period", 6));
            double min = P("min", 3000), max = P("max", 8000);
            double cycles = c.Time / period;
            double phase = Frac(cycles);
            double shaped = 1 - (1 - phase) * (1 - phase);
            double rpm = min + (max - min) * shaped;
            string var = PS("var", "RPM");
            c.Vars.Set(var, rpm);
            if (var == "RPM" && c.Vars.Has("Engine0_RPM")) c.Vars.Set("Engine0_RPM", rpm);
            c.Signals["rpmSweep.cycle"] = Math.Floor(cycles);
            c.Signals["rpmSweep.phase"] = phase;
            c.Signals["rpm"] = rpm;
            double slFirst = P("slFirst", 0);
            double slBlink = P("slBlink", max);
            if (slFirst <= 0) slFirst = max * 0.87;
            if (slBlink <= slFirst) slBlink = slFirst + 1;
            c.Vars.Set("ShiftIndicatorPct", Clamp((rpm - slFirst) / (slBlink - slFirst), 0, 1));
        }

        public override string Describe() { return "rpmSweep " + P("min", 3000) + ".." + P("max", 8000) + " rpm over " + P("period", 6) + " s"; }
    }

    /// <summary>Gear steps through minGear..maxGear, one gear per rpm sweep; Speed follows gear x rpm.</summary>
    public sealed class GearFromRpmDriver : Driver
    {
        private static readonly double[] DefaultTop = { 0, 80, 120, 155, 190, 225, 260, 290, 320, 340 };

        public GearFromRpmDriver() { Type = "gearFromRpm"; }

        public override void Tick(SimContext c)
        {
            int minGear = PI("minGear", 2), maxGear = PI("maxGear", 6);
            if (maxGear < minGear) maxGear = minGear;
            double redline = Math.Max(1, P("redline", 9000));
            double cycle = c.Signal("rpmSweep.cycle", Math.Floor(c.Time / Math.Max(0.05, P("period", 6))));
            int n = maxGear - minGear + 1;
            int gear = minGear + (int)(((long)cycle) % n);
            double rpm = c.Vars.Get(PS("rpmVar", "RPM"));
            double top;
            var list = PL("speedAtRedline");
            if (list != null && gear >= 0 && gear < list.Count) top = Json.ToDouble(list[gear]);
            else if (list != null && list.Count > 0) top = Json.ToDouble(list[list.Count - 1]);
            else top = DefaultTop[Math.Min(Math.Max(gear, 0), DefaultTop.Length - 1)];
            double kmh = rpm / redline * top;
            double ms = kmh / 3.6;
            var v = c.Vars;
            v.Set("Gear", gear);
            v.Set("Speed", ms);
            v.Set("VelocityX", ms);
            int p = c.PlayerCarIdx;
            v.Set("CarIdxGear", p, gear);
            v.Set("CarIdxRPM", p, rpm);
            c.Signals["gear"] = gear;
            c.Signals["speedKmh"] = kmh;
        }

        public override string Describe() { return "gearFromRpm gears " + PI("minGear", 2) + ".." + PI("maxGear", 6); }
    }

    /// <summary>Player lap timing: LapCurrentLapTime counts up, on lap completion Lap/LapCompleted increment and
    /// LapLastLapTime/LapBestLapTime are updated. Also LapDistPct/LapDist and the delta vars.</summary>
    public sealed class LapTimerDriver : Driver
    {
        private double _cur, _curLapTime, _last, _best;
        private int _lap, _completed, _bestLap;
        private bool _init;

        public LapTimerDriver() { Type = "lapTimer"; }

        private double Jitter(SimContext c) { return P("jitter", 0.5) * (c.Rng.NextDouble() * 2 - 1); }

        public override void Init(SimContext c)
        {
            var v = c.Vars;
            double lapTime = Math.Max(1, P("lapTime", 98));
            _lap = (int)v.Get("Lap"); if (_lap <= 0) _lap = 1;
            _completed = (int)v.Get("LapCompleted"); if (_completed < 0) _completed = _lap - 1;
            _last = v.Get("LapLastLapTime");
            _best = v.Get("LapBestLapTime");
            _bestLap = (int)v.Get("LapBestLap");
            _cur = v.Get("LapCurrentLapTime");
            double pct = v.Get("LapDistPct");
            if (_cur <= 0 && pct > 0) _cur = pct * lapTime;
            _curLapTime = lapTime;
            _init = true;
        }

        public override void Tick(SimContext c)
        {
            if (!_init) Init(c);
            var v = c.Vars;
            double lapTime = Math.Max(1, P("lapTime", 98));
            double trackLength = P("trackLength", 7004);
            bool justCompleted = false;
            _cur += c.Dt;
            if (_cur >= _curLapTime)
            {
                _cur -= _curLapTime;
                _last = _curLapTime;
                if (_best <= 0 || _last < _best) { _best = _last; _bestLap = _lap; }
                _lap++;
                _completed++;
                _curLapTime = lapTime + Jitter(c);
                justCompleted = true;
                c.Log(string.Format(CultureInfo.InvariantCulture, "lap {0} completed in {1:0.000} s (best {2:0.000})", _completed, _last, _best));
            }
            double p = Clamp(_cur / _curLapTime, 0, 0.9999);
            v.Set("LapCurrentLapTime", _cur);
            v.Set("LapDistPct", p);
            v.Set("LapDist", p * trackLength);
            v.Set("Lap", _lap);
            v.Set("LapCompleted", _completed);
            v.Set("LapLastLapTime", _last);
            v.Set("LapBestLapTime", _best);
            v.Set("LapBestLap", _bestLap);
            v.Set("LapLastNLapTime", _last);
            v.Set("LapBestNLapTime", _best);
            v.Set("LapBestNLapLap", _bestLap);
            v.Set("LapLasNLapSeq", _completed);
            double sessionBest = c.Signal("field.sessionBest", _best);
            SetDelta(v, "LapDeltaToBestLap", _best, p);
            SetDelta(v, "LapDeltaToOptimalLap", _best, p);
            SetDelta(v, "LapDeltaToSessionBestLap", sessionBest, p);
            SetDelta(v, "LapDeltaToSessionOptimalLap", sessionBest, p);
            SetDelta(v, "LapDeltaToSessionLastlLap", _last, p);
            int idx = c.PlayerCarIdx;
            v.Set("CarIdxLap", idx, _lap);
            v.Set("CarIdxLapCompleted", idx, _completed);
            v.Set("CarIdxLapDistPct", idx, p);
            v.Set("CarIdxLastLapTime", idx, _last);
            v.Set("CarIdxBestLapTime", idx, _best);
            v.Set("CarIdxBestLapNum", idx, _bestLap);
            v.Set("CarIdxEstTime", idx, p * _curLapTime);
            c.Signals["player.pct"] = p;
            c.Signals["player.lap"] = _lap;
            c.Signals["player.completed"] = _completed;
            c.Signals["player.cur"] = _cur;
            c.Signals["player.last"] = _last;
            c.Signals["player.best"] = _best;
            c.Signals["player.bestLap"] = _bestLap;
            c.Signals["player.lapTime"] = lapTime;
            c.Signals["player.curLapTime"] = _curLapTime;
            c.Signals["player.justCompleted"] = justCompleted ? 1 : 0;
        }

        private void SetDelta(VarTable v, string name, double reference, double pct)
        {
            bool ok = reference > 0;
            v.Set(name, ok ? _cur - reference * pct : 0);
            v.Set(name + "_DD", ok ? (_curLapTime - reference) / _curLapTime : 0);
            v.SetBool(name + "_OK", ok);
        }

        public override string Describe() { return "lapTimer " + P("lapTime", 98) + " s/lap"; }
    }

    /// <summary>FuelLevel burns litersPerLap spread over the lap; FuelLevelPct and FuelUsePerHour follow.</summary>
    public sealed class FuelBurnDriver : Driver
    {
        private double _initial = -1;

        public FuelBurnDriver() { Type = "fuelBurn"; }

        public override void Tick(SimContext c)
        {
            var v = c.Vars;
            if (_initial < 0) _initial = v.Get("FuelLevel");
            double lapTime = Math.Max(1, c.Signal("player.lapTime", P("lapTime", 98)));
            double lpl = P("litersPerLap", 2.9);
            double max = Math.Max(0.001, P("maxLiters", 100));
            double kg = P("kgPerLiter", 0.75);
            double f = v.Get("FuelLevel") - lpl * c.Dt / lapTime;
            if (f < P("minLiters", 0.5))
            {
                f = _initial;
                c.Log("fuelBurn: tank refilled to " + _initial.ToString("0.0", CultureInfo.InvariantCulture) + " l");
            }
            v.Set("FuelLevel", f);
            v.Set("FuelLevelPct", Clamp(f / max, 0, 1));
            v.Set("FuelUsePerHour", lpl / lapTime * 3600 * kg);
        }

        public override string Describe() { return "fuelBurn " + P("litersPerLap", 2.9) + " l/lap"; }
    }

    /// <summary>SessionFlags cycles through a sequence of flag states every "period" seconds.</summary>
    public sealed class FlagCycleDriver : Driver
    {
        public static readonly Dictionary<string, uint> FlagBits = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            { "none", 0 }, { "checkered", 0x1 }, { "white", 0x2 }, { "green", 0x4 }, { "yellow", 0x8 }, { "red", 0x10 },
            { "blue", 0x20 }, { "debris", 0x40 }, { "crossed", 0x80 }, { "yellowWaving", 0x100 }, { "oneLapToGreen", 0x200 },
            { "greenHeld", 0x400 }, { "tenToGo", 0x800 }, { "fiveToGo", 0x1000 }, { "randomWaving", 0x2000 }, { "caution", 0x4000 },
            { "cautionWaving", 0x8000 }, { "black", 0x10000 }, { "disqualify", 0x20000 }, { "servicible", 0x40000 }, { "furled", 0x80000 },
            { "repair", 0x100000 }, { "startHidden", 0x10000000 }, { "startReady", 0x20000000 }, { "startSet", 0x40000000 }, { "startGo", 0x80000000 },
        };

        private static readonly string[] DefaultSequence = { "none", "yellow", "blue", "white", "green", "black", "checkered" };
        private int _lastIdx = -1;

        public FlagCycleDriver() { Type = "flagCycle"; }

        public static uint ParseFlags(object spec)
        {
            if (spec == null) return 0;
            if (spec is double d) return unchecked((uint)(long)d);
            if (spec is bool b) return b ? 1u : 0u;
            if (spec is List<object> l) { uint acc = 0; foreach (var o in l) acc |= ParseFlags(o); return acc; }
            string s = spec.ToString().Trim();
            if (s.Length == 0) return 0;
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || char.IsDigit(s[0])) return unchecked((uint)(long)Json.ParseNumber(s));
            uint r = 0;
            foreach (var part in s.Split('|', '+', ','))
            {
                string p = part.Trim();
                if (p.Length == 0) continue;
                if (!FlagBits.TryGetValue(p, out var bit)) throw new FormatException("unknown session flag '" + p + "'");
                r |= bit;
            }
            return r;
        }

        public static string FlagNames(uint flags)
        {
            if (flags == 0) return "none";
            var sb = new StringBuilder();
            foreach (var kv in FlagBits)
            {
                if (kv.Value != 0 && (flags & kv.Value) == kv.Value)
                {
                    if (sb.Length > 0) sb.Append('|');
                    sb.Append(kv.Key);
                }
            }
            return sb.ToString();
        }

        public override void Tick(SimContext c)
        {
            double period = Math.Max(0.05, P("period", 8));
            var seq = PL("sequence");
            int n = seq != null && seq.Count > 0 ? seq.Count : DefaultSequence.Length;
            int idx = (int)(((long)Math.Floor(c.Time / period)) % n);
            object spec = seq != null && seq.Count > 0 ? seq[idx] : DefaultSequence[idx];
            uint flags = ParseFlags(spec) | ParseFlags(Params.TryGetValue("base", out var b) ? b : null);
            c.Vars.Set(PS("var", "SessionFlags"), flags);
            if (idx != _lastIdx)
            {
                _lastIdx = idx;
                c.Log("flagCycle: SessionFlags = 0x" + flags.ToString("X8") + " (" + FlagNames(flags) + ")");
            }
        }

        public override string Describe() { return "flagCycle every " + P("period", 8) + " s"; }
    }

    /// <summary>Toggles the pit speed limiter bit (0x10) in EngineWarnings every "period" seconds.</summary>
    public sealed class PitLimiterToggleDriver : Driver
    {
        private int _last = -1;

        public PitLimiterToggleDriver() { Type = "pitLimiterToggle"; }

        public override void Tick(SimContext c)
        {
            double period = Math.Max(0.05, P("period", 5));
            int on = (int)(((long)Math.Floor(c.Time / period)) % 2);
            uint bit = unchecked((uint)(long)P("bit", 0x10));
            string var = PS("var", "EngineWarnings");
            uint w = unchecked((uint)VarTable.ToInt(c.Vars.Get(var)));
            w = on != 0 ? (w | bit) : (w & ~bit);
            c.Vars.Set(var, w);
            if (c.Vars.Has("dcPitSpeedLimiterToggle")) c.Vars.SetBool("dcPitSpeedLimiterToggle", on != 0);
            if (on != _last)
            {
                _last = on;
                c.Log("pitLimiterToggle: limiter " + (on != 0 ? "ON" : "off") + " (EngineWarnings=0x" + w.ToString("X") + ")");
            }
        }

        public override string Describe() { return "pitLimiterToggle every " + P("period", 5) + " s"; }
    }

    /// <summary>Any variable (or array element) follows a sine wave between min and max.</summary>
    public sealed class SineDriver : Driver
    {
        public SineDriver() { Type = "sine"; }

        public override void Tick(SimContext c)
        {
            string var = PS("var", null);
            if (string.IsNullOrEmpty(var)) return;
            double min = P("min", 0), max = P("max", 1), period = Math.Max(0.05, P("period", 4)), phase = P("phase", 0);
            double v = min + (max - min) * (0.5 + 0.5 * Math.Sin(2 * Math.PI * c.Time / period + phase));
            c.Vars.Set(var, PI("index", 0), v);
        }

        public override string Describe() { return "sine " + PS("var", "?") + " " + P("min", 0) + ".." + P("max", 1) + " / " + P("period", 4) + " s"; }
    }

    /// <summary>Any variable toggles between "off" and "on" every "period" seconds.</summary>
    public sealed class ToggleDriver : Driver
    {
        public ToggleDriver() { Type = "toggle"; }

        public override void Tick(SimContext c)
        {
            string var = PS("var", null);
            if (string.IsNullOrEmpty(var)) return;
            double period = Math.Max(0.05, P("period", 2));
            bool on = (((long)Math.Floor(c.Time / period)) % 2) == 1;
            c.Vars.Set(var, PI("index", 0), on ? P("on", 1) : P("off", 0));
        }

        public override string Describe() { return "toggle " + PS("var", "?") + " every " + P("period", 2) + " s"; }
    }

    /// <summary>
    /// Multi-car field: every listed car runs around the track at its pace; positions, class positions, gaps,
    /// laps and CarIdx* arrays are recomputed every tick. The player car follows the lapTimer driver when present.
    /// Also renders the ResultsPositions / ResultsFastestLap YAML blocks (placeholders {{ResultsPositions}} and
    /// {{ResultsFastestLap}} in the session template) every "resultsInterval" seconds.
    /// </summary>
    public sealed class FieldDriver : Driver
    {
        private sealed class Car
        {
            public int Idx;
            public int ClassId;
            public double Pace;
            public double Pct;
            public int Lap;
            public int Completed;
            public double Last;
            public double Best;
            public int BestLap;
            public bool Pit;
            public bool PitInitial;
            public double CurLapTime;
            public int Pos;
            public int ClassPos;
            public double Dist;
            public double Gap;
            public bool IsPlayer;
        }

        private readonly List<Car> _cars = new List<Car>();
        private bool _init;
        private double _nextResults = -1;
        private string _resultsSignature = "";

        public FieldDriver() { Type = "field"; }

        public override void Init(SimContext c)
        {
            _cars.Clear();
            double pace = Math.Max(1, P("pace", 98));
            double jitter = P("jitter", 0.5);
            int player = c.PlayerCarIdx;
            var list = PL("cars") ?? new List<object>();
            foreach (var o in list)
            {
                var d = o as Dictionary<string, object>;
                if (d == null) continue;
                var car = new Car
                {
                    Idx = Json.GetInt(d, "idx", -1),
                    ClassId = Json.GetInt(d, "class", (int)c.Vars.Get("PlayerCarClass")),
                    Pace = Math.Max(1, Json.GetDouble(d, "pace", pace)),
                    Pct = Json.GetDouble(d, "pct", 0),
                    Lap = Json.GetInt(d, "lap", 1),
                    Pit = Json.GetBool(d, "pit", false),
                };
                if (car.Idx < 0 || car.Idx >= 64) throw new FormatException("field: car idx must be 0..63");
                car.IsPlayer = car.Idx == player;
                car.PitInitial = car.Pit;
                car.Completed = Math.Max(0, car.Lap - 1);
                car.Last = Json.GetDouble(d, "last", car.Pace + jitter * (c.Rng.NextDouble() * 2 - 1));
                car.Best = Json.GetDouble(d, "best", car.Pace - 0.2 - c.Rng.NextDouble() * 0.6);
                car.BestLap = Json.GetInt(d, "bestLap", Math.Max(1, car.Completed - c.Rng.Next(0, 5)));
                car.CurLapTime = car.Pace + jitter * (c.Rng.NextDouble() * 2 - 1);
                _cars.Add(car);
            }
            if (!_cars.Any(x => x.IsPlayer))
            {
                c.Log("field: player car (idx " + player + ") not listed, adding it");
                _cars.Add(new Car { Idx = player, IsPlayer = true, ClassId = (int)c.Vars.Get("PlayerCarClass"), Pace = pace, Lap = 1, CurLapTime = pace, Best = pace, Last = pace, BestLap = 1 });
            }
            _init = true;
            // Render the initial standings so the very first session string already carries ResultsPositions.
            foreach (var car in _cars) car.Dist = car.Completed + car.Pct;
            RenderResults(c, ComputeOrder());
            _nextResults = Math.Max(0.5, P("resultsInterval", 5));
        }

        /// <summary>Sorts by distance covered and fills Pos / ClassPos / Gap. Returns the running order.</summary>
        private List<Car> ComputeOrder()
        {
            var ordered = _cars.OrderByDescending(x => x.Dist).ToList();
            var classCount = new Dictionary<int, int>();
            var leader = ordered.Count > 0 ? ordered[0] : null;
            for (int i = 0; i < ordered.Count; i++)
            {
                var car = ordered[i];
                car.Pos = i + 1;
                classCount.TryGetValue(car.ClassId, out var n);
                n++;
                classCount[car.ClassId] = n;
                car.ClassPos = n;
                car.Gap = leader != null ? (leader.Dist - car.Dist) * car.Pace : 0;
            }
            return ordered;
        }

        public override void Tick(SimContext c)
        {
            if (!_init) Init(c);
            var v = c.Vars;
            double jitter = P("jitter", 0.5);
            bool havePlayerSignals = c.HasSignal("player.pct");
            // "pitCars": [idx, ...] (settable from the timeline) parks those cars in their pit stall.
            var pitList = PL("pitCars");
            foreach (var car in _cars)
            {
                if (car.IsPlayer) continue;
                bool pit = car.PitInitial;
                if (pitList != null) foreach (var o in pitList) if ((int)Json.ToDouble(o) == car.Idx) pit = true;
                if (pit != car.Pit) c.Log("field: car " + car.Idx + (pit ? " enters the pit stall" : " leaves the pits"));
                car.Pit = pit;
            }

            foreach (var car in _cars)
            {
                if (car.IsPlayer && havePlayerSignals)
                {
                    car.Pct = c.Signal("player.pct", car.Pct);
                    car.Lap = (int)c.Signal("player.lap", car.Lap);
                    car.Completed = (int)c.Signal("player.completed", car.Completed);
                    car.Last = c.Signal("player.last", car.Last);
                    car.Best = c.Signal("player.best", car.Best);
                    car.BestLap = (int)c.Signal("player.bestLap", car.BestLap);
                    car.CurLapTime = c.Signal("player.curLapTime", car.CurLapTime);
                    car.Pit = v.GetBool("OnPitRoad");
                }
                else if (!car.Pit)
                {
                    car.Pct += c.Dt / car.CurLapTime;
                    if (car.Pct >= 1)
                    {
                        car.Pct -= 1;
                        car.Last = car.CurLapTime;
                        if (car.Best <= 0 || car.Last < car.Best) { car.Best = car.Last; car.BestLap = car.Lap; }
                        car.Lap++;
                        car.Completed++;
                        car.CurLapTime = car.Pace + jitter * (c.Rng.NextDouble() * 2 - 1);
                    }
                }
                car.Dist = car.Completed + car.Pct;
            }

            // Positions by distance covered, class positions within class.
            var ordered = ComputeOrder();
            var leader = ordered.Count > 0 ? ordered[0] : null;

            double sessionBest = double.MaxValue;
            foreach (var car in _cars)
            {
                int i = car.Idx;
                v.Set("CarIdxLap", i, car.Lap);
                v.Set("CarIdxLapCompleted", i, car.Completed);
                v.Set("CarIdxLapDistPct", i, car.Pct);
                v.Set("CarIdxTrackSurface", i, car.Pit ? 1 : 3);
                v.Set("CarIdxTrackSurfaceMaterial", i, car.Pit ? 0 : 1);
                v.SetBool("CarIdxOnPitRoad", i, car.Pit);
                v.Set("CarIdxPosition", i, car.Pos);
                v.Set("CarIdxClassPosition", i, car.ClassPos);
                v.Set("CarIdxClass", i, car.ClassId);
                v.Set("CarIdxF2Time", i, car.Gap);
                v.Set("CarIdxEstTime", i, car.Pct * car.CurLapTime);
                v.Set("CarIdxLastLapTime", i, car.Last);
                v.Set("CarIdxBestLapTime", i, car.Best);
                v.Set("CarIdxBestLapNum", i, car.BestLap);
                v.Set("CarIdxTireCompound", i, 0);
                v.Set("CarIdxQualTireCompound", i, 0);
                if (!car.IsPlayer)
                {
                    double k = car.Pct * 23 + car.Idx;
                    v.Set("CarIdxGear", i, car.Pit ? 0 : 3 + ((int)Math.Floor(k)) % 4);
                    v.Set("CarIdxRPM", i, car.Pit ? 1500 : 6200 + 2200 * Frac(k));
                    v.Set("CarIdxSteer", i, 0.2 * Math.Sin(k));
                }
                if (car.Best > 0 && car.Best < sessionBest) sessionBest = car.Best;
                if (car.IsPlayer)
                {
                    v.Set("PlayerCarPosition", car.Pos);
                    v.Set("PlayerCarClassPosition", car.ClassPos);
                    v.Set("PlayerCarClass", car.ClassId);
                }
            }
            if (leader != null)
            {
                v.Set("RaceLaps", leader.Lap);
                int total = (int)v.Get("SessionLapsTotal");
                if (total > 0 && total != Irsdk.UnlimitedLaps)
                {
                    v.Set("SessionLapsRemain", Math.Max(0, total - leader.Completed));
                    v.Set("SessionLapsRemainEx", Math.Max(0, total - leader.Completed));
                }
                c.Signals["field.leaderLap"] = leader.Lap;
            }
            if (sessionBest < double.MaxValue) c.Signals["field.sessionBest"] = sessionBest;

            if (c.Time >= _nextResults)
            {
                _nextResults = c.Time + Math.Max(0.5, P("resultsInterval", 5));
                RenderResults(c, ordered);
            }
        }

        private void RenderResults(SimContext c, List<Car> ordered)
        {
            if (c.Yaml == null) return;
            // Only rewrite the session string when standings/laps/lap times changed, not for the continuously
            // moving gap / LapsDriven fields (each rewrite makes SimHub re-parse ~40 KB of YAML).
            var sig = new StringBuilder();
            foreach (var car in ordered)
                sig.Append(car.Idx).Append(':').Append(car.Pos).Append(':').Append(car.ClassPos).Append(':').Append(car.Completed).Append(':')
                   .Append(car.Last.ToString("0.0000", CultureInfo.InvariantCulture)).Append(':').Append(car.Best.ToString("0.0000", CultureInfo.InvariantCulture)).Append(':').Append(car.BestLap).Append(';');
            string signature = sig.ToString();
            if (signature == _resultsSignature) return;
            _resultsSignature = signature;
            string ind = new string(' ', PI("yamlIndent", 3));
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            foreach (var car in ordered)
            {
                sb.Append(ind).Append("- Position: ").Append(car.Pos).Append('\n');
                sb.Append(ind).Append("  ClassPosition: ").Append(car.ClassPos - 1).Append('\n'); // iRacing: 0-based in YAML
                sb.Append(ind).Append("  CarIdx: ").Append(car.Idx).Append('\n');
                sb.Append(ind).Append("  Lap: ").Append(car.Completed).Append('\n');
                sb.Append(ind).Append("  Time: ").Append(car.Gap.ToString("0.0000", inv)).Append('\n');
                sb.Append(ind).Append("  FastestLap: ").Append(car.BestLap).Append('\n');
                sb.Append(ind).Append("  FastestTime: ").Append(car.Best.ToString("0.0000", inv)).Append('\n');
                sb.Append(ind).Append("  LastTime: ").Append(car.Last.ToString("0.0000", inv)).Append('\n');
                sb.Append(ind).Append("  LapsLed: ").Append(car.Pos == 1 ? car.Completed : 0).Append('\n');
                sb.Append(ind).Append("  LapsComplete: ").Append(car.Completed).Append('\n');
                sb.Append(ind).Append("  JokerLapsComplete: 0\n");
                sb.Append(ind).Append("  LapsDriven: ").Append(car.Dist.ToString("0.000", inv)).Append('\n');
                sb.Append(ind).Append("  Incidents: 0\n");
                sb.Append(ind).Append("  ReasonOutId: 0\n");
                sb.Append(ind).Append("  ReasonOutStr: Running\n");
            }
            c.Yaml.Set("ResultsPositions", sb.ToString().TrimEnd('\n'));

            Car fastest = null;
            foreach (var car in _cars) if (car.Best > 0 && (fastest == null || car.Best < fastest.Best)) fastest = car;
            var fl = new StringBuilder();
            if (fastest != null)
            {
                fl.Append(ind).Append("- CarIdx: ").Append(fastest.Idx).Append('\n');
                fl.Append(ind).Append("  FastestLap: ").Append(fastest.BestLap).Append('\n');
                fl.Append(ind).Append("  FastestTime: ").Append(fastest.Best.ToString("0.0000", inv));
            }
            c.Yaml.Set("ResultsFastestLap", fl.ToString());
        }

        public override string Describe() { return "field with " + (PL("cars")?.Count ?? 0) + " cars, pace " + P("pace", 98) + " s"; }
    }
}
