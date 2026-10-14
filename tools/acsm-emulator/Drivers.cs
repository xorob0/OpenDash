// Drivers.cs: what moves the values, tick by tick. The same drivers as the irsdk emulator's, writing AC's
// fields instead of iRacing's variables, so a scenario for one sim reads like its twin for the other.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using IrsdkEmulator;

namespace AcsmEmulator
{
    public sealed class SimContext
    {
        public Pages Pages;
        /// <summary>Loose coupling between drivers: rpmSweep publishes "rpm", lapTimer "player.pct" and so on.</summary>
        public readonly Dictionary<string, double> Signals = new Dictionary<string, double>(StringComparer.Ordinal);
        public double Time;
        public double Dt;
        public long Tick;
        public Random Rng = new Random(12345);
        public Action<string> Log = s => { };

        public double Signal(string key, double def) { return Signals.TryGetValue(key, out var v) ? v : def; }
    }

    public abstract class Driver
    {
        public string Type;
        public string Name;
        public bool Enabled = true;
        /// <summary>How many times the runner has ticked it, which the selfcheck reads.</summary>
        public long Ticks;
        public Dictionary<string, object> Params = new Dictionary<string, object>(StringComparer.Ordinal);

        protected double P(string key, double def) { return Json.GetDouble(Params, key, def); }
        protected int PI(string key, int def) { return Json.GetInt(Params, key, def); }
        protected string PS(string key, string def) { return Json.GetString(Params, key, def); }
        protected List<object> PL(string key) { return Json.GetList(Params, key); }

        public virtual void Init(SimContext ctx) { }
        public abstract void Tick(SimContext ctx);
        public virtual string Describe() { return Type; }

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

        public static readonly string[] Known = { "clock", "rpmSweep", "gearFromRpm", "lapTimer", "fuelBurn", "flagCycle", "pitLimiterToggle", "field", "sine", "ramp", "toggle" };

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
                case "ramp": return new RampDriver();
                case "toggle": return new ToggleDriver();
                default: throw new FormatException("unknown driver type '" + type + "'. Known: " + string.Join(", ", Known));
            }
        }

        public static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }
        public static double Frac(double v) { return v - Math.Floor(v); }

        /// <summary>AC's own text form of a lap time, as in its HUD: 1:38:412.</summary>
        public static string LapText(double seconds)
        {
            if (seconds <= 0) return "-:--:---";
            int ms = (int)Math.Round(seconds * 1000);
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:000}", ms / 60000, ms / 1000 % 60, ms % 1000);
        }
    }

    /// <summary>
    /// Always present and always first. Both PacketIds are the tick, which is what the trace recorder keys its
    /// frames on (DataCorePlugin.GameRawData.Graphics.PacketId), and Graphics.SessionTimeLeft counts down in
    /// milliseconds while it is above zero.
    /// </summary>
    public sealed class ClockDriver : Driver
    {
        public ClockDriver() { Type = "clock"; }

        public override void Tick(SimContext c)
        {
            var p = c.Pages;
            p.Set("Physics.PacketId", c.Tick);
            p.Set("Graphics.PacketId", c.Tick);
            double left = p.Get("Graphics.SessionTimeLeft");
            if (left > 0) p.Set("Graphics.SessionTimeLeft", Math.Max(0, left - c.Dt * 1000));
        }
    }

    /// <summary>Physics.Rpms ramps min..max over "period" seconds, ease-out like a car pulling in a gear, then drops.</summary>
    public sealed class RpmSweepDriver : Driver
    {
        public RpmSweepDriver() { Type = "rpmSweep"; }

        public override void Tick(SimContext c)
        {
            double period = Math.Max(0.05, P("period", 6));
            double min = P("min", 3000), max = P("max", 8000);
            double cycles = c.Time / period;
            double phase = Frac(cycles);
            double rpm = min + (max - min) * (1 - (1 - phase) * (1 - phase));
            c.Pages.Set("Physics.Rpms", rpm);
            c.Signals["rpmSweep.cycle"] = Math.Floor(cycles);
            c.Signals["rpm"] = rpm;
        }

        public override string Describe() { return "rpmSweep " + P("min", 3000) + ".." + P("max", 8000) + " rpm over " + P("period", 6) + " s"; }
    }

    /// <summary>
    /// One gear per rpm sweep, minGear..maxGear, and the speed that gear and those revs make. AC numbers the
    /// gears from reverse: Physics.Gear 0 is R, 1 is N and 2 is first, which SimHub turns into "R", "N", "1".
    /// </summary>
    public sealed class GearFromRpmDriver : Driver
    {
        private static readonly double[] DefaultTop = { 0, 80, 120, 155, 190, 225, 260, 290, 320, 340 };

        public GearFromRpmDriver() { Type = "gearFromRpm"; }

        public override void Tick(SimContext c)
        {
            int minGear = PI("minGear", 2), maxGear = Math.Max(PI("maxGear", 6), PI("minGear", 2));
            double redline = Math.Max(1, P("redline", 9000));
            double cycle = c.Signal("rpmSweep.cycle", Math.Floor(c.Time / 6));
            int gear = minGear + (int)(((long)cycle) % (maxGear - minGear + 1));
            double rpm = c.Pages.Get("Physics.Rpms");
            var list = PL("speedAtRedline");
            double top = list != null && list.Count > 0 ? Json.ToDouble(list[Math.Min(gear, list.Count - 1)]) : DefaultTop[Math.Min(Math.Max(gear, 0), DefaultTop.Length - 1)];
            double kmh = rpm / redline * top;
            var p = c.Pages;
            p.Set("Physics.Gear", gear + 1);
            p.Set("Physics.SpeedKmh", kmh);
            p.Set("Physics.LocalVelocity", 2, kmh / 3.6);
            c.Signals["speedKmh"] = kmh;
        }

        public override string Describe() { return "gearFromRpm gears " + PI("minGear", 2) + ".." + PI("maxGear", 6); }
    }

    /// <summary>
    /// The player's lap: Graphics.iCurrentTime counts up, and on the line CompletedLaps, iLastTime and iBestTime
    /// move. The sector index and LastSectorTime follow thirds of the lap, which is how SimHub's AC reader builds
    /// its sector splits, and the car's coordinates go round a circle the length of the track.
    /// </summary>
    public sealed class LapTimerDriver : Driver
    {
        private double _cur, _curLapTime, _last, _best, _sectorStart;
        private int _completed, _sector;
        private bool _init;

        public LapTimerDriver() { Type = "lapTimer"; }

        public override void Init(SimContext c)
        {
            var p = c.Pages;
            double lapTime = Math.Max(1, P("lapTime", 98));
            _completed = (int)p.Get("Graphics.CompletedLaps");
            _last = p.Get("Graphics.iLastTime") / 1000.0;
            _best = p.Get("Graphics.iBestTime") / 1000.0;
            double pct = p.Get("Graphics.NormalizedCarPosition");
            _cur = p.Get("Graphics.iCurrentTime") / 1000.0;
            if (_cur <= 0 && pct > 0) _cur = pct * lapTime;
            _curLapTime = lapTime;
            _sector = Math.Min(2, (int)Math.Floor(_cur / _curLapTime * 3));
            _sectorStart = _sector * _curLapTime / 3;
            _init = true;
        }

        public override void Tick(SimContext c)
        {
            if (!_init) Init(c);
            var p = c.Pages;
            double lapTime = Math.Max(1, P("lapTime", 98));
            double trackLength = P("trackLength", p.Get("StaticInfo.TrackSPlineLength"));
            _cur += c.Dt;
            if (_cur >= _curLapTime)
            {
                _cur -= _curLapTime;
                _last = _curLapTime;
                if (_best <= 0 || _last < _best) _best = _last;
                _completed++;
                p.Set("Graphics.LastSectorTime", (_curLapTime - _sectorStart) * 1000);
                _curLapTime = lapTime + P("jitter", 0.5) * (c.Rng.NextDouble() * 2 - 1);
                _sector = 0;
                _sectorStart = 0;
                c.Log(string.Format(CultureInfo.InvariantCulture, "lap {0} completed in {1:0.000} s (best {2:0.000})", _completed, _last, _best));
            }
            double pct = Clamp(_cur / _curLapTime, 0, 0.9999);
            int sector = Math.Min(2, (int)Math.Floor(pct * 3));
            if (sector != _sector)
            {
                double boundary = sector * _curLapTime / 3;
                p.Set("Graphics.LastSectorTime", (boundary - _sectorStart) * 1000);
                _sectorStart = boundary;
                _sector = sector;
            }
            p.Set("Graphics.iCurrentTime", _cur * 1000);
            p.Set("Graphics.iLastTime", _last * 1000);
            p.Set("Graphics.iBestTime", _best * 1000);
            p.SetText("Graphics.CurrentTime", LapText(_cur));
            p.SetText("Graphics.LastTime", LapText(_last));
            p.SetText("Graphics.BestTime", LapText(_best));
            p.Set("Graphics.CompletedLaps", _completed);
            p.Set("Graphics.CurrentSectorIndex", _sector);
            p.Set("Graphics.NormalizedCarPosition", pct);
            p.Set("Graphics.DistanceTraveled", (_completed + pct) * trackLength);
            double radius = trackLength / (2 * Math.PI), angle = 2 * Math.PI * pct;
            p.Set("Graphics.CarCoordinates", 0, radius * Math.Cos(angle));
            p.Set("Graphics.CarCoordinates", 2, radius * Math.Sin(angle));
            p.Set("Physics.Heading", Math.IEEERemainder(angle + Math.PI / 2, 2 * Math.PI));
            c.Signals["player.pct"] = pct;
            c.Signals["player.completed"] = _completed;
            c.Signals["player.cur"] = _cur;
            c.Signals["player.last"] = _last;
            c.Signals["player.best"] = _best;
            c.Signals["player.lapTime"] = lapTime;
        }

        public override string Describe() { return "lapTimer " + P("lapTime", 98) + " s/lap"; }
    }

    /// <summary>Physics.Fuel burns litersPerLap over the lap, and refills to where it started at minLiters.</summary>
    public sealed class FuelBurnDriver : Driver
    {
        private double _initial = -1;

        public FuelBurnDriver() { Type = "fuelBurn"; }

        public override void Tick(SimContext c)
        {
            var p = c.Pages;
            if (_initial < 0) _initial = p.Get("Physics.Fuel");
            double lapTime = Math.Max(1, c.Signal("player.lapTime", P("lapTime", 98)));
            double f = p.Get("Physics.Fuel") - P("litersPerLap", 2.9) * c.Dt / lapTime;
            if (f < P("minLiters", 0.5))
            {
                f = _initial;
                c.Log("fuelBurn: tank refilled to " + _initial.ToString("0.0", CultureInfo.InvariantCulture) + " l");
            }
            p.Set("Physics.Fuel", f);
        }

        public override string Describe() { return "fuelBurn " + P("litersPerLap", 2.9) + " l/lap"; }
    }

    /// <summary>Graphics.Flags steps through a sequence of single flags, AC showing one at a time.</summary>
    public sealed class FlagCycleDriver : Driver
    {
        private static readonly string[] DefaultSequence = { "NO_FLAG", "YELLOW_FLAG", "BLUE_FLAG", "WHITE_FLAG", "BLACK_FLAG", "CHECKERED_FLAG" };
        private int _lastIdx = -1;

        public FlagCycleDriver() { Type = "flagCycle"; }

        public override void Tick(SimContext c)
        {
            double period = Math.Max(0.05, P("period", 8));
            var seq = PL("sequence");
            int n = seq != null && seq.Count > 0 ? seq.Count : DefaultSequence.Length;
            int idx = (int)(((long)Math.Floor(c.Time / period)) % n);
            object spec = seq != null && seq.Count > 0 ? seq[idx] : DefaultSequence[idx];
            var r = c.Pages.Resolve("Graphics.Flags");
            if (c.Pages.IsPinned(r, -1)) return;
            c.Pages.Apply("Graphics.Flags", spec, false);
            if (idx != _lastIdx)
            {
                _lastIdx = idx;
                c.Log("flagCycle: Graphics.Flags = " + c.Pages.Graphics.Flags);
            }
        }

        public override string Describe() { return "flagCycle every " + P("period", 8) + " s"; }
    }

    /// <summary>
    /// Physics.PitLimiterOn toggles every "period" seconds. SimHub's GameData.PitLimiterOn does not follow it on
    /// AC: ACManager answers that one with the pit lane, which is the thing this driver makes visible.
    /// </summary>
    public sealed class PitLimiterToggleDriver : Driver
    {
        private int _last = -1;

        public PitLimiterToggleDriver() { Type = "pitLimiterToggle"; }

        public override void Tick(SimContext c)
        {
            int on = (int)(((long)Math.Floor(c.Time / Math.Max(0.05, P("period", 5)))) % 2);
            c.Pages.Set("Physics.PitLimiterOn", on);
            if (on != _last)
            {
                _last = on;
                c.Log("pitLimiterToggle: Physics.PitLimiterOn " + (on != 0 ? "ON" : "off"));
            }
        }

        public override string Describe() { return "pitLimiterToggle every " + P("period", 5) + " s"; }
    }

    /// <summary>Any value, or one element of an array, follows a sine between min and max.</summary>
    public sealed class SineDriver : Driver
    {
        public SineDriver() { Type = "sine"; }

        public override void Tick(SimContext c)
        {
            string path = PS("var", null);
            if (string.IsNullOrEmpty(path)) return;
            double min = P("min", 0), max = P("max", 1), period = Math.Max(0.05, P("period", 4)), phase = P("phase", 0);
            c.Pages.Set(path, PI("index", -1), min + (max - min) * (0.5 + 0.5 * Math.Sin(2 * Math.PI * c.Time / period + phase)));
        }

        public override string Describe() { return "sine " + PS("var", "?") + " " + P("min", 0) + ".." + P("max", 1) + " / " + P("period", 4) + " s"; }
    }

    /// <summary>Any value climbs from min to max over "period" seconds and wraps.</summary>
    public sealed class RampDriver : Driver
    {
        public RampDriver() { Type = "ramp"; }

        public override void Tick(SimContext c)
        {
            string path = PS("var", null);
            if (string.IsNullOrEmpty(path)) return;
            double min = P("min", 0), max = P("max", 1), period = Math.Max(0.05, P("period", 4));
            double frac = Frac(c.Time / period + P("phase", 0));
            c.Pages.Set(path, PI("index", -1), min + (max - min) * frac);
        }

        public override string Describe() { return "ramp " + PS("var", "?") + " " + P("min", 0) + ".." + P("max", 1) + " / " + P("period", 4) + " s"; }
    }

    /// <summary>Any value toggles between "off" and "on" every "period" seconds.</summary>
    public sealed class ToggleDriver : Driver
    {
        public ToggleDriver() { Type = "toggle"; }

        public override void Tick(SimContext c)
        {
            string path = PS("var", null);
            if (string.IsNullOrEmpty(path)) return;
            bool on = (((long)Math.Floor(c.Time / Math.Max(0.05, P("period", 2)))) % 2) == 1;
            c.Pages.Set(path, PI("index", -1), on ? P("on", 1) : P("off", 0));
        }

        public override string Describe() { return "toggle " + PS("var", "?") + " every " + P("period", 2) + " s"; }
    }

    /// <summary>
    /// The other cars, published the way SimHub's own AC app publishes them in <c>Local\acpmf_simhub_v2</c>: a
    /// name, a car, a lap count, lap times, a position on the spline and in the world, and the two pit states.
    /// That is everything SimHub's AC reader takes from the app, and it is thinner than iRacing's session string:
    /// no class, no number, no team and no rating, which is what a leaderboard on AC has to draw without.
    ///
    /// The player is the car whose "name" is StaticInfo.PlayerName, and follows the lapTimer. Every other car
    /// runs at its "pace" from its starting "lap" and "pct"; a car with "pit": true sits in its box, as does one
    /// named in "pitCars". The running order is by distance covered, published zero-based as AC does in
    /// carRealTimeLeaderboardPosition, so equal paces keep it still.
    /// </summary>
    public sealed class FieldDriver : Driver
    {
        private sealed class Car
        {
            public string Name, Model;
            public double BasePace, Pace, Pct, Last, Best, Cur;
            public int Lap;
            public bool Player;
        }

        private List<Car> _cars;

        public FieldDriver() { Type = "field"; }

        public override void Init(SimContext c)
        {
            _cars = new List<Car>();
            string player = c.Pages.StaticInfo.PlayerName;
            string playerCar = c.Pages.StaticInfo.CarModel;
            double pace = P("pace", 98);
            foreach (var o in PL("cars") ?? new List<object>())
            {
                if (!(o is Dictionary<string, object> d)) continue;
                var car = new Car
                {
                    Name = Json.GetString(d, "name", "Driver " + (_cars.Count + 1)),
                    Model = Json.GetString(d, "car", playerCar),
                    Pace = Json.GetDouble(d, "pace", pace),
                    Pct = Json.GetDouble(d, "pct", 0),
                    Lap = Json.GetInt(d, "lap", 0),
                    Best = Json.GetDouble(d, "best", 0),
                    Last = Json.GetDouble(d, "last", 0),
                };
                car.Player = car.Name == player;
                car.BasePace = car.Pace;
                car.Cur = car.Pct * car.Pace;
                _cars.Add(car);
            }
            if (!_cars.Any(x => x.Player))
            {
                _cars.Insert(0, new Car { Name = player, Model = playerCar, BasePace = pace, Pace = pace, Player = true });
                c.Log("field: the player " + player + " was not in the list of cars and was added");
            }
        }

        private bool InPit(int index)
        {
            var name = _cars[index].Name;
            var listed = PL("cars")?.OfType<Dictionary<string, object>>().FirstOrDefault(d => Json.GetString(d, "name") == name);
            if (listed != null && Json.GetBool(listed, "pit", false)) return true;
            var pitCars = PL("pitCars");
            return pitCars != null && pitCars.Any(o => Json.ToText(o) == name);
        }

        public override void Tick(SimContext c)
        {
            if (_cars == null) Init(c);
            var p = c.Pages;
            double trackLength = P("trackLength", p.Get("StaticInfo.TrackSPlineLength"));
            double jitter = P("jitter", 0);
            for (int i = 0; i < _cars.Count; i++)
            {
                var car = _cars[i];
                if (car.Player)
                {
                    // Read off the graphics page rather than the lap timer, so that a scenario which stops
                    // the timer (the car in its box) still has the player where its values put it.
                    car.Pct = p.Get("Graphics.NormalizedCarPosition");
                    car.Lap = (int)p.Get("Graphics.CompletedLaps");
                    car.Cur = p.Get("Graphics.iCurrentTime") / 1000.0;
                    car.Last = p.Get("Graphics.iLastTime") / 1000.0;
                    car.Best = p.Get("Graphics.iBestTime") / 1000.0;
                    continue;
                }
                if (InPit(i)) continue;
                car.Cur += c.Dt;
                car.Pct += c.Dt / car.Pace;
                if (car.Pct >= 1)
                {
                    car.Pct -= 1;
                    car.Lap++;
                    car.Last = car.Cur;
                    if (car.Best <= 0 || car.Last < car.Best) car.Best = car.Last;
                    car.Cur = car.Pct * car.Pace;
                    if (jitter > 0) car.Pace = car.BasePace + jitter * (c.Rng.NextDouble() * 2 - 1);
                }
            }

            var order = Enumerable.Range(0, _cars.Count).OrderByDescending(i => _cars[i].Lap + _cars[i].Pct).ToList();
            var byBest = Enumerable.Range(0, _cars.Count).OrderBy(i => _cars[i].Best > 0 ? _cars[i].Best : double.MaxValue).ToList();
            var o = p.Opponents;
            o.numVehicles = _cars.Count;
            for (int i = 0; i < _cars.Count && i < o.vehicle.Length; i++)
            {
                var car = _cars[i];
                bool pit = car.Player ? p.Get("Graphics.IsInPit") > 0 : InPit(i);
                bool pitLane = car.Player ? p.Get("Graphics.isInPitLane") > 0 : pit;
                var v = o.vehicle[i];
                v.carId = i;
                Pages.PutBytes(v.driverName, car.Name);
                Pages.PutBytes(v.carModel, car.Model);
                v.speedMS = pit ? 0 : (float)(trackLength / car.Pace);
                v.bestLapMS = (int)Math.Round(car.Best * 1000);
                v.lastLapTimeMS = (int)Math.Round(car.Last * 1000);
                v.currentLapTimeMS = (int)Math.Round(car.Cur * 1000);
                v.lapCount = car.Lap;
                double radius = trackLength / (2 * Math.PI), angle = 2 * Math.PI * car.Pct;
                v.worldPosition = new acsVec3 { x = (float)(radius * Math.Cos(angle)), y = 0, z = (float)(radius * Math.Sin(angle)) };
                v.isCarInPitline = pitLane ? 1 : 0;
                v.isCarInPit = pit ? 1 : 0;
                v.carRealTimeLeaderboardPosition = order.IndexOf(i);
                v.carLeaderboardPosition = byBest.IndexOf(i) + 1;
                v.spLineLength = (float)car.Pct;
                v.isConnected = 1;
                o.vehicle[i] = v;
                if (car.Player)
                {
                    o.focusVehicle = i;
                    // AC's own position is the running order in a race and the order of best laps otherwise.
                    bool race = p.Graphics.Session == AC_SESSION_TYPE.AC_RACE;
                    p.Set("Graphics.Position", (race ? order.IndexOf(i) : byBest.IndexOf(i)) + 1);
                }
            }
            p.Opponents = o;
            p.Set("StaticInfo.NumCars", _cars.Count);
        }

        public override string Describe() { return "field of " + (PL("cars")?.Count ?? 0) + " cars"; }
    }
}
