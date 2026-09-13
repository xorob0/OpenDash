using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace IrsdkEmulator
{
    /// <summary>One entry of the "timeline" array.</summary>
    public sealed class TimelineEvent
    {
        public double At;
        public string LogText;
        public Dictionary<string, object> Set;
        public Dictionary<string, object> Pin;
        public List<object> Unpin;
        public Dictionary<string, object> Yaml;
        public string SessionYamlPath;
        public List<Dictionary<string, object>> DriverUpdates = new List<Dictionary<string, object>>();
        public bool Fired;
    }

    /// <summary>
    /// A scenario = variable definitions + initial values/pins + session YAML template + drivers + timeline.
    /// Loaded from JSON (see README.md for the format). "extends" merges another scenario file underneath.
    /// </summary>
    public sealed class Scenario
    {
        public string Name = "";
        public string Path;
        public string BaseDir;
        public int TickRate = 60;
        public int NumBuf = 3;
        public int SessionInfoCapacity = 128 * 1024;
        public long MapSize = 2 * 1024 * 1024;
        public int Seed = 12345;
        public string SessionYamlPath;
        public Dictionary<string, string> YamlValues = new Dictionary<string, string>(StringComparer.Ordinal);
        public List<VarDef> VarDefs = new List<VarDef>();
        /// <summary>Initial values per variable as parsed JSON (scalar/array/object). Missing == catalogue default.</summary>
        public Dictionary<string, object> InitialValues = new Dictionary<string, object>(StringComparer.Ordinal);
        public Dictionary<string, double> CatalogDefaults = new Dictionary<string, double>(StringComparer.Ordinal);
        public HashSet<string> PinnedVars = new HashSet<string>(StringComparer.Ordinal);
        public List<Dictionary<string, object>> DriverSpecs = new List<Dictionary<string, object>>();
        public List<TimelineEvent> Timeline = new List<TimelineEvent>();

        /// <summary>Seconds after which the timeline starts again, or 0 to run it once.</summary>
        public double LoopSeconds;
        public List<string> Warnings = new List<string>();

        public static Scenario Load(string path)
        {
            path = System.IO.Path.GetFullPath(path);
            var merged = LoadMerged(path, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            var sc = new Scenario { Path = path, BaseDir = System.IO.Path.GetDirectoryName(path) };
            sc.Apply(merged);
            return sc;
        }

        /// <summary>Loads a JSON file and merges "extends" chains. Relative paths inside are resolved to absolute here.</summary>
        private static Dictionary<string, object> LoadMerged(string path, HashSet<string> seen)
        {
            if (!seen.Add(path)) throw new InvalidOperationException("scenario 'extends' cycle at " + path);
            var d = Json.ParseObject(File.ReadAllText(path));
            string dir = System.IO.Path.GetDirectoryName(path);
            ResolvePaths(d, dir);
            string ext = Json.GetString(d, "extends");
            if (string.IsNullOrEmpty(ext)) return d;
            var baseDict = LoadMerged(ext, seen);
            return Merge(baseDict, d);
        }

        private static void ResolvePaths(Dictionary<string, object> d, string dir)
        {
            foreach (var key in new[] { "extends", "sessionYaml" })
            {
                string p = Json.GetString(d, key);
                if (!string.IsNullOrEmpty(p) && !System.IO.Path.IsPathRooted(p)) d[key] = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, p));
            }
            var tl = Json.GetList(d, "timeline");
            if (tl == null) return;
            foreach (var o in tl)
            {
                var e = o as Dictionary<string, object>;
                if (e == null) continue;
                string p = Json.GetString(e, "sessionYaml");
                if (!string.IsNullOrEmpty(p) && !System.IO.Path.IsPathRooted(p)) e["sessionYaml"] = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, p));
            }
        }

        /// <summary>child on top of base: "variables"/"exclude" are concatenated, "yaml" merged, everything else replaced.</summary>
        private static Dictionary<string, object> Merge(Dictionary<string, object> b, Dictionary<string, object> child)
        {
            var r = new Dictionary<string, object>(b, StringComparer.Ordinal);
            foreach (var kv in child)
            {
                switch (kv.Key)
                {
                    case "extends":
                        break;
                    case "variables":
                    case "exclude":
                        {
                            var l = new List<object>(Json.GetList(b, kv.Key) ?? new List<object>());
                            l.AddRange(kv.Value as List<object> ?? new List<object>());
                            r[kv.Key] = l;
                            break;
                        }
                    case "yaml":
                        {
                            var m = new Dictionary<string, object>(Json.GetObject(b, kv.Key) ?? new Dictionary<string, object>(), StringComparer.Ordinal);
                            foreach (var y in kv.Value as Dictionary<string, object> ?? new Dictionary<string, object>()) m[y.Key] = y.Value;
                            r[kv.Key] = m;
                            break;
                        }
                    default:
                        r[kv.Key] = kv.Value;
                        break;
                }
            }
            return r;
        }

        private void Apply(Dictionary<string, object> d)
        {
            Name = Json.GetString(d, "name", System.IO.Path.GetFileNameWithoutExtension(Path));
            TickRate = Json.GetInt(d, "tickRate", 60);
            NumBuf = Json.GetInt(d, "numBuf", 3);
            SessionInfoCapacity = Json.GetInt(d, "sessionInfoCapacity", 128 * 1024);
            MapSize = (long)Json.GetDouble(d, "mapSize", 2 * 1024 * 1024);
            Seed = Json.GetInt(d, "seed", 12345);
            SessionYamlPath = Json.GetString(d, "sessionYaml");
            if (TickRate < 1 || TickRate > 1000) throw new FormatException("tickRate must be 1..1000");

            var yaml = Json.GetObject(d, "yaml");
            if (yaml != null) foreach (var kv in yaml) YamlValues[kv.Key] = Json.ToText(kv.Value);

            // Variables: catalogue first (optional), then explicit list, then exclusions.
            var defs = new Dictionary<string, VarDef>(StringComparer.Ordinal);
            var order = new List<string>();
            if (Json.GetBool(d, "includeCatalog", true))
            {
                foreach (var e in Catalog.Entries)
                {
                    defs[e.Def.Name] = e.Def.Clone();
                    order.Add(e.Def.Name);
                    CatalogDefaults[e.Def.Name] = e.Default;
                }
            }
            var vars = Json.GetList(d, "variables") ?? new List<object>();
            foreach (var o in vars)
            {
                var vd = o as Dictionary<string, object>;
                if (vd == null) { Warnings.Add("ignoring non-object entry in variables"); continue; }
                string name = Json.GetString(vd, "name");
                if (string.IsNullOrEmpty(name)) throw new FormatException("variable entry without \"name\"");
                var cat = Catalog.Find(name);
                if (!defs.TryGetValue(name, out var def))
                {
                    def = cat != null ? cat.Def.Clone() : new VarDef { Name = name };
                    defs[name] = def;
                    order.Add(name);
                    if (cat != null) CatalogDefaults[name] = cat.Default;
                }
                if (vd.ContainsKey("type")) def.Type = IrType.Parse(Json.GetString(vd, "type"));
                if (vd.ContainsKey("count")) def.Count = Json.GetInt(vd, "count", 1);
                if (vd.ContainsKey("unit")) def.Unit = Json.GetString(vd, "unit", "");
                if (vd.ContainsKey("desc")) def.Desc = Json.GetString(vd, "desc", "");
                if (vd.ContainsKey("countAsTime")) def.CountAsTime = Json.GetBool(vd, "countAsTime", false);
                if (cat == null && !vd.ContainsKey("type")) Warnings.Add("variable " + name + " is not in the catalogue and has no \"type\", assuming float");
                if (vd.ContainsKey("value")) InitialValues[name] = vd["value"];
                if (Json.GetBool(vd, "pin", false)) PinnedVars.Add(name);
            }
            var excl = Json.GetList(d, "exclude") ?? new List<object>();
            foreach (var o in excl)
            {
                string n = Json.ToText(o);
                if (defs.Remove(n)) order.Remove(n);
                InitialValues.Remove(n);
                PinnedVars.Remove(n);
            }
            foreach (var n in order) VarDefs.Add(defs[n]);

            var drivers = Json.GetList(d, "drivers") ?? new List<object>();
            foreach (var o in drivers)
            {
                var dd = o as Dictionary<string, object>;
                if (dd == null) { Warnings.Add("ignoring non-object entry in drivers"); continue; }
                DriverSpecs.Add(dd);
            }

            var tl = Json.GetList(d, "timeline") ?? new List<object>();
            foreach (var o in tl)
            {
                var e = o as Dictionary<string, object>;
                if (e == null) { Warnings.Add("ignoring non-object entry in timeline"); continue; }
                var ev = new TimelineEvent
                {
                    At = Json.GetDouble(e, "at", 0),
                    LogText = Json.GetString(e, "log"),
                    Set = Json.GetObject(e, "set"),
                    Pin = Json.GetObject(e, "pin"),
                    Unpin = Json.GetList(e, "unpin"),
                    Yaml = Json.GetObject(e, "yaml"),
                    SessionYamlPath = Json.GetString(e, "sessionYaml"),
                };
                var drv = Json.TryGet(e, "driver", out var dv) ? dv : null;
                if (drv is Dictionary<string, object> one) ev.DriverUpdates.Add(one);
                else if (drv is List<object> many) foreach (var m in many) if (m is Dictionary<string, object> md) ev.DriverUpdates.Add(md);
                Timeline.Add(ev);
            }
            Timeline = Timeline.OrderBy(x => x.At).ToList();
            // A scenario that walks a catalogue wants to keep walking it, so that somebody can watch
            // the whole thing twice without restarting the emulator. `loop` is the period in seconds;
            // 0, the default, runs the timeline once.
            LoopSeconds = Json.GetDouble(d, "loop", 0);
        }

        /// <summary>Creates the live variable table with initial values and pins applied.</summary>
        public VarTable CreateVarTable()
        {
            var t = new VarTable();
            foreach (var def in VarDefs)
            {
                var slot = t.Add(def);
                if (CatalogDefaults.TryGetValue(def.Name, out var dv)) for (int i = 0; i < slot.Values.Length; i++) slot.Values[i] = dv;
            }
            foreach (var kv in InitialValues) t.Apply(kv.Key, kv.Value, PinnedVars.Contains(kv.Key));
            foreach (var n in PinnedVars) if (!InitialValues.ContainsKey(n)) t.Pin(n, -1, true);
            return t;
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
    }
}
