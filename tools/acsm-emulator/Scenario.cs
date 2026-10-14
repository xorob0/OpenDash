using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IrsdkEmulator;

namespace AcsmEmulator
{
    /// <summary>One entry of the "timeline" array.</summary>
    public sealed class TimelineEvent
    {
        public double At;
        public string LogText;
        public Dictionary<string, object> Set;
        public Dictionary<string, object> Pin;
        public List<object> Unpin;
        public List<Dictionary<string, object>> DriverUpdates = new List<Dictionary<string, object>>();
        public bool Fired;
    }

    /// <summary>
    /// A scenario: the initial values of the pages, the ones held still, the drivers that move the rest and a
    /// timeline. JSON with comments, like the irsdk emulator's; "extends" merges another scenario underneath.
    /// </summary>
    public sealed class Scenario
    {
        public string Name = "";
        public string Path;
        public int TickRate = 60;
        public int Seed = 12345;
        /// <summary>Initial values, in the order written, base first.</summary>
        public List<KeyValuePair<string, object>> Values = new List<KeyValuePair<string, object>>();
        /// <summary>Initial values that drivers may not move.</summary>
        public List<KeyValuePair<string, object>> Pins = new List<KeyValuePair<string, object>>();
        public List<Dictionary<string, object>> DriverSpecs = new List<Dictionary<string, object>>();
        public List<TimelineEvent> Timeline = new List<TimelineEvent>();
        public double LoopSeconds;
        /// <summary>
        /// Whether SimHub's own AC app is running inside the game, which is what publishes the other cars. It is
        /// installed by SimHub and switched on in Documents\Assetto Corsa\cfg\python.ini; without it SimHub's
        /// leaderboard is the player alone, named "Me". False leaves Local\acpmf_simhub_v2 unpublished.
        /// </summary>
        public bool App = true;
        public List<string> Warnings = new List<string>();

        public static Scenario Load(string path)
        {
            path = System.IO.Path.GetFullPath(path);
            var merged = LoadMerged(path, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            var sc = new Scenario { Path = path };
            sc.Apply(merged);
            return sc;
        }

        private static Dictionary<string, object> LoadMerged(string path, HashSet<string> seen)
        {
            if (!seen.Add(path)) throw new InvalidOperationException("scenario 'extends' cycle at " + path);
            var d = Json.ParseObject(File.ReadAllText(path));
            string ext = Json.GetString(d, "extends");
            if (string.IsNullOrEmpty(ext)) return d;
            if (!System.IO.Path.IsPathRooted(ext)) ext = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), ext));
            return Merge(LoadMerged(ext, seen), d);
        }

        /// <summary>The child on top of its base: "values" and "pin" merged key by key, everything else replaced.</summary>
        private static Dictionary<string, object> Merge(Dictionary<string, object> b, Dictionary<string, object> child)
        {
            var r = new Dictionary<string, object>(b, StringComparer.Ordinal);
            foreach (var kv in child)
            {
                if (kv.Key == "extends") continue;
                if (kv.Key == "values" || kv.Key == "pin")
                {
                    var m = new Dictionary<string, object>(Json.GetObject(b, kv.Key) ?? new Dictionary<string, object>(), StringComparer.Ordinal);
                    foreach (var e in kv.Value as Dictionary<string, object> ?? new Dictionary<string, object>()) m[e.Key] = e.Value;
                    r[kv.Key] = m;
                    continue;
                }
                r[kv.Key] = kv.Value;
            }
            return r;
        }

        private void Apply(Dictionary<string, object> d)
        {
            Name = Json.GetString(d, "name", System.IO.Path.GetFileNameWithoutExtension(Path));
            TickRate = Json.GetInt(d, "tickRate", 60);
            Seed = Json.GetInt(d, "seed", 12345);
            if (TickRate < 1 || TickRate > 1000) throw new FormatException("tickRate must be 1..1000");
            foreach (var kv in Json.GetObject(d, "values") ?? new Dictionary<string, object>()) Values.Add(kv);
            foreach (var kv in Json.GetObject(d, "pin") ?? new Dictionary<string, object>()) Pins.Add(kv);
            foreach (var o in Json.GetList(d, "drivers") ?? new List<object>())
            {
                if (o is Dictionary<string, object> dd) DriverSpecs.Add(dd);
                else Warnings.Add("ignoring non-object entry in drivers");
            }
            foreach (var o in Json.GetList(d, "timeline") ?? new List<object>())
            {
                if (!(o is Dictionary<string, object> e)) { Warnings.Add("ignoring non-object entry in timeline"); continue; }
                var ev = new TimelineEvent
                {
                    At = Json.GetDouble(e, "at", 0),
                    LogText = Json.GetString(e, "log"),
                    Set = Json.GetObject(e, "set"),
                    Pin = Json.GetObject(e, "pin"),
                    Unpin = Json.GetList(e, "unpin"),
                };
                var drv = Json.TryGet(e, "driver", out var dv) ? dv : null;
                if (drv is Dictionary<string, object> one) ev.DriverUpdates.Add(one);
                else if (drv is List<object> many) foreach (var m in many) if (m is Dictionary<string, object> md) ev.DriverUpdates.Add(md);
                Timeline.Add(ev);
            }
            Timeline = Timeline.OrderBy(x => x.At).ToList();
            LoopSeconds = Json.GetDouble(d, "loop", 0);
            App = Json.GetBool(d, "app", true);
        }

        /// <summary>The pages with every initial value written and every pin in place.</summary>
        public Pages CreatePages()
        {
            var p = new Pages();
            foreach (var kv in Values)
                if (!p.Apply(kv.Key, kv.Value, false)) throw new FormatException("unknown value " + kv.Key + " in scenario " + Name);
            foreach (var kv in Pins)
                if (!p.Apply(kv.Key, kv.Value, true)) throw new FormatException("unknown value " + kv.Key + " in scenario " + Name);
            return p;
        }

        public List<Driver> CreateDrivers()
        {
            var list = new List<Driver>();
            bool haveClock = false;
            foreach (var spec in DriverSpecs)
            {
                string type = Json.GetString(spec, "type");
                var drv = Driver.Create(type);
                drv.Name = Json.GetString(spec, "name", type);
                drv.Merge(spec);
                if (drv is ClockDriver) haveClock = true;
                list.Add(drv);
            }
            if (!haveClock) list.Insert(0, new ClockDriver { Name = "clock" });
            return list;
        }

        /// <summary>Whether the scenario publishes SimHub's AC app page, which is what a field driver fills.</summary>
        public bool HasField => App && DriverSpecs.Any(s => Json.GetString(s, "type") == "field" && Json.GetBool(s, "enabled", true));
    }
}
