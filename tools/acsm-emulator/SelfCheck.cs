// SelfCheck.cs: what can be proved without Windows. The layouts against numbers worked out by hand from
// SimHub's declarations, every page through the marshaller and back, the fake install against what
// ACManager reads from it, and the scenario simulated for a few seconds against the traps SimHub has.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using IrsdkEmulator;

namespace AcsmEmulator
{
    public static class SelfCheck
    {
        private static int _failures;

        private static void Check(bool ok, string what)
        {
            Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
            if (!ok) _failures++;
        }

        public static bool Run(string scenarioPath)
        {
            _failures = 0;
            Console.WriteLine("layouts (sizes and offsets from SimHub 9.12.6's ACSharedMemory structs):");
            Size<Physics>(580);
            Size<Graphics>(288);
            Size<StaticInfo>(684);
            Size<acsVehicleInfo>(228);
            Size<SPageFileCrewChief>(30252);
            Offset<Physics>("Rpms", 20);
            Offset<Physics>("TyreCoreTemperature", 152);
            Offset<Physics>("CarDamage", 224);
            Offset<Physics>("PitLimiterOn", 248);
            Offset<Physics>("Clutch", 364);
            Offset<Physics>("BrakeBias", 564);
            Offset<Graphics>("Status", 4);
            Offset<Graphics>("CompletedLaps", 132);
            Offset<Graphics>("TyreCompound", 176);
            Offset<Graphics>("ReplayTimeMultiplier", 244);
            Offset<Graphics>("Flags", 268);
            Offset<Graphics>("isInPitLane", 276);
            Offset<StaticInfo>("CarModel", 68);
            Offset<StaticInfo>("SectorCount", 400);
            Offset<StaticInfo>("MaxRpm", 412);
            Offset<StaticInfo>("AidAllowTyreBlankets", 480);
            Offset<StaticInfo>("TrackSPlineLength", 520);
            Offset<StaticInfo>("ErsMaxJ", 592);
            Offset<StaticInfo>("PitWindowEnd", 680);
            Offset<acsVehicleInfo>("worldPosition", 156);
            Offset<acsVehicleInfo>("isConnected", 188);
            Offset<SPageFileCrewChief>("vehicle", 520);
            Offset<SPageFileCrewChief>("isInternalMemoryModuleLoaded", 30216);

            if (scenarioPath == null)
            {
                Console.WriteLine("no scenario given and scenarios/race.json not found; layouts only");
                return Done();
            }

            Console.WriteLine("scenario " + scenarioPath + ":");
            Scenario sc;
            try { sc = Scenario.Load(scenarioPath); }
            catch (Exception ex) { Check(false, "loads: " + ex.Message); return Done(); }
            foreach (var w in sc.Warnings) Check(false, "warning: " + w);

            var sink = new MemorySink();
            var runner = new Runner(sc, sink);
            var log = new List<string>();
            runner.Log = s => log.Add(s);
            // A driver the timeline turns off at 0 is off from the first frame. One that ran once first would
            // leave its mark for the whole run: pit's stopped lap timer put the car its lap time round the
            // track, P3 where the scenario places it P9 (#1138).
            var offFromStart = new HashSet<string>(sc.Timeline.Where(e => e.At <= 0).SelectMany(e => e.DriverUpdates)
                .Where(du => du.ContainsKey("enabled") && Json.ToDouble(du["enabled"]) == 0)
                .Select(du => Json.GetString(du, "name") ?? Json.GetString(du, "type")), StringComparer.Ordinal);
            try
            {
                runner.Start();
                var ran = runner.Drivers.Where(d => (offFromStart.Contains(d.Name) || offFromStart.Contains(d.Type)) && d.Ticks > 0).Select(d => d.Name).ToList();
                Check(ran.Count == 0, offFromStart.Count == 0 ? "the timeline turns no driver off at 0"
                    : "the drivers the timeline turns off at 0 never run (" + string.Join(", ", offFromStart) + ")" + (ran.Count == 0 ? "" : ": " + string.Join(", ", ran) + " ran on the first frame"));
                for (int i = 0; i < 3 * sc.TickRate; i++) runner.Step();
            }
            catch (Exception ex) { Check(false, "simulates three seconds: " + ex.Message); return Done(); }
            Check(true, "simulates three seconds (" + runner.Tick + " ticks, " + log.Count + " log lines)");

            var pages = runner.Pages;
            var physicsBytes = sink.Images[Pages.PhysicsMap];
            var graphicsBytes = sink.Images[Pages.GraphicsMap];
            var staticBytes = sink.Images[Pages.StaticMap];
            var ph = Pages.FromBytes<Physics>(physicsBytes);
            var g = Pages.FromBytes<Graphics>(graphicsBytes);
            var st = Pages.FromBytes<StaticInfo>(staticBytes);

            Console.WriteLine("the pages, read back as SimHub reads them:");
            Check(ph.Rpms == pages.Physics.Rpms && BitConverter.ToInt32(physicsBytes, 20) == ph.Rpms, "Physics.Rpms " + ph.Rpms + " round-trips and sits at byte 20");
            Check(g.PacketId == runner.Tick && ph.PacketId == runner.Tick, "both PacketIds are the tick (" + runner.Tick + "), which the trace recorder keys frames on");
            Check(BitConverter.ToInt32(graphicsBytes, 268) == (int)g.Flags, "Graphics.Flags " + g.Flags + " sits at byte 268");
            Check(st.CarModel == pages.StaticInfo.CarModel && st.Track == pages.StaticInfo.Track && st.PlayerName == pages.StaticInfo.PlayerName,
                  "StaticInfo strings round-trip: " + st.CarModel + ", " + st.Track + ", " + st.PlayerName);
            Check(Enumerable.Range(0, 4).All(i => ph.TyreCoreTemperature[i] == pages.Physics.TyreCoreTemperature[i]), "Physics.TyreCoreTemperature round-trips");

            Console.WriteLine("what SimHub's AC reader needs:");
            Check(g.Status == AC_STATUS.AC_LIVE, "Graphics.Status is AC_LIVE, so IsGameInRace and not paused (it is " + g.Status + ")");
            Check(ph.Rpms > 300, "Physics.Rpms above 300, so SimHub's EngineStarted is true (" + ph.Rpms + ")");
            Check(ph.Gear >= 0, "Physics.Gear is AC's numbering, 0 for R and 1 for N (" + ph.Gear + ")");
            Check(!string.IsNullOrEmpty(st.PlayerName), "StaticInfo.PlayerName is set, since SimHub finds the player by it");
            Check(st.TrackSPlineLength > 0, "StaticInfo.TrackSPlineLength is set, which SimHub publishes as TrackLength");
            Check(st.MaxRpm > 0, "StaticInfo.MaxRpm is set, which SimHub's shift lights are computed from");
            string installDir = Program.InstallDir();
            string problem = Program.InstallProblem(installDir, pages);
            Check(problem == null, "the fake install has the car and the track" + (problem == null ? " (" + installDir + ")" : ": " + problem));
            if (problem == null) CheckCar(Path.Combine(installDir, "content", "cars", st.CarModel), ph);
            if (problem == null) CheckTrack(Path.Combine(installDir, "content", "tracks", st.Track.ToLowerInvariant()));

            if (sc.HasField)
            {
                Console.WriteLine("SimHub's AC app page (Local\\acpmf_simhub_v2):");
                var o = Pages.FromBytes<SPageFileCrewChief>(sink.Images[Pages.OpponentsMap]);
                Check(o.numVehicles > 1 && o.numVehicles <= 128, "it carries a field (" + o.numVehicles + " cars)");
                var connected = o.vehicle.Take(o.numVehicles).Where(v => v.isConnected > 0).ToList();
                Check(connected.Count == o.numVehicles, "every car is connected, or SimHub drops it");
                var player = connected.Where(v => Name(v.driverName) == st.PlayerName).ToList();
                Check(player.Count == 1, "exactly one car is named StaticInfo.PlayerName, which is how SimHub finds the player");
                var positions = connected.Select(v => v.carRealTimeLeaderboardPosition).OrderBy(x => x).ToList();
                Check(positions.SequenceEqual(Enumerable.Range(0, connected.Count)), "the running order is zero-based and has no gap or tie");
                // SimHub takes the running order in a race and carLeaderboardPosition otherwise.
                bool race = g.Session == AC_SESSION_TYPE.AC_RACE;
                if (player.Count == 1)
                    Check(g.Position == (race ? player[0].carRealTimeLeaderboardPosition + 1 : player[0].carLeaderboardPosition),
                          "Graphics.Position is the player's place in it, " + (race ? "on the road" : "by best lap") + " (P" + g.Position + ")");
            }
            else Console.WriteLine("no field: Local\\acpmf_simhub_v2 is not published, as without SimHub's AC app, and the leaderboard is the player alone");

            return Done();
        }

        private static bool Done()
        {
            Console.WriteLine(_failures == 0 ? "selfcheck passed" : "selfcheck FAILED: " + _failures + " problem(s)");
            return _failures == 0;
        }

        private static void Size<T>(int expected)
        {
            int size = Marshal.SizeOf(typeof(T));
            Check(size == expected, typeof(T).Name + " is " + size + " bytes (expected " + expected + ")");
        }

        private static void Offset<T>(string field, int expected)
        {
            int at = (int)Marshal.OffsetOf(typeof(T), field);
            Check(at == expected, typeof(T).Name + "." + field + " at " + at + " (expected " + expected + ")");
        }

        private static string Name(byte[] b)
        {
            string s = System.Text.Encoding.UTF8.GetString(b);
            int nul = s.IndexOf('\0');
            return (nul >= 0 ? s.Substring(0, nul) : s).Trim();
        }

        /// <summary>The car's files, read the way CarInfo, CarPhysicsManager and LutManager read them.</summary>
        private static void CheckCar(string dir, Physics ph)
        {
            var ui = Path.Combine(dir, "ui", "ui_car.json");
            string name = null;
            try { name = Json.GetString(Json.ParseObject(File.ReadAllText(ui)), "name"); } catch { }
            Check(!string.IsNullOrEmpty(name), "ui\\ui_car.json names the car, which SimHub publishes as CarModel (" + name + ")");
            var gears = Ini(Path.Combine(dir, "data", "drivetrain.ini"), "GEARS", "COUNT");
            Check(gears != null && int.TryParse(gears, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0, "data\\drivetrain.ini [GEARS] COUNT is a whole number, CarSettings_MaxGears (" + gears + ")");
            var up = Ini(Path.Combine(dir, "data", "ai.ini"), "GEARS", "UP");
            // SimHub parses it with the current culture, so a whole number is the only safe spelling.
            Check(up != null && int.TryParse(up, NumberStyles.Integer, CultureInfo.InvariantCulture, out _), "data\\ai.ini [GEARS] UP is a whole number, the redline SimHub syncs (" + up + ")");
            foreach (var (section, amount, label) in new[] { ("TRACTION_CONTROL", ph.TC, "TCLevel"), ("ABS", ph.Abs, "ABSLevel") })
            {
                var curve = Ini(Path.Combine(dir, "data", "electronics.ini"), section, "CURVE");
                var lut = curve == null ? null : Lut(Path.Combine(dir, "data", curve));
                Check(lut != null && lut.Count > 0, "data\\electronics.ini [" + section + "] CURVE names a curve that parses (" + (curve ?? "none") + ", " + (lut?.Count ?? 0) + " presets)");
                if (lut == null || lut.Count == 0) continue;
                int preset = amount == 0 ? 0 : lut.OrderBy(kv => Math.Abs(amount - kv.Value)).First().Key + 1;
                Console.WriteLine("         SimHub will read " + label + " " + preset + " for " + amount.ToString("0.###", CultureInfo.InvariantCulture));
            }
        }

        private static void CheckTrack(string dir)
        {
            string name = null;
            try { name = Json.GetString(Json.ParseObject(File.ReadAllText(Path.Combine(dir, "ui", "ui_track.json"))), "name"); } catch { }
            Check(!string.IsNullOrEmpty(name), "the track's ui\\ui_track.json names it, raw Track.TrackInfo.name (" + name + "); GameData.TrackName is the folder");
        }

        private static string Ini(string file, string section, string key)
        {
            if (!File.Exists(file)) return null;
            string current = null;
            foreach (var raw in File.ReadAllLines(file))
            {
                string line = raw.Split(';')[0].Trim();
                if (line.StartsWith("[") && line.EndsWith("]")) { current = line.Substring(1, line.Length - 2); continue; }
                int eq = line.IndexOf('=');
                if (current == section && eq > 0 && line.Substring(0, eq).Trim() == key) return line.Substring(eq + 1).Trim();
            }
            return null;
        }

        private static Dictionary<int, double> Lut(string file)
        {
            if (!File.Exists(file)) return null;
            var d = new Dictionary<int, double>();
            foreach (var line in File.ReadAllLines(file))
            {
                if (!line.Contains("|")) continue;
                var parts = line.Trim().Split('|');
                d.Add(int.Parse(parts[0], CultureInfo.InvariantCulture), double.Parse(parts[1], CultureInfo.InvariantCulture));
            }
            return d;
        }
    }
}
