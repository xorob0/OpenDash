using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace IrsdkEmulator
{
    public static class Program
    {
        private static volatile bool _stop;

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint ms);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint ms);

        private const string Usage =
@"IrsdkEmulator - synthetic iRacing shared-memory telemetry for SimHub

usage: IrsdkEmulator.exe [scenario.json | name] [options]

  scenario          path to a scenario JSON, or a name resolved as scenarios/<name>.json next to the exe
                    (default: scenarios/race.json)
  --duration <s>    stop after <s> simulated seconds (default: run until Ctrl+C)
  --stop-file <p>   stop cleanly as soon as file <p> exists (checked once per second; handy when the emulator
                    runs in the interactive desktop session and is controlled from an SSH session)
  --log <file>      also append all console output to <file>
  --dry-run         do not create the shared memory / event, just simulate (any OS)
  --fast            with --dry-run: do not pace to real time
  --quiet           no per-second status line
  --dump-vars       print the effective variable table (name, type, count, unit, offset, value) and exit
  --dump-yaml       print the rendered session YAML and exit
  --selfcheck       write header + var headers + one buffer into a byte array and read them back with the
                    same struct layouts SimHub marshals; also loads the scenario and simulates a few seconds
  --help            this text
";

        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            string scenarioArg = null;
            bool selfcheck = false, dryRun = false, fast = false, quiet = false, dumpVars = false, dumpYaml = false;
            double duration = -1;
            string stopFile = null, logFile = null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                switch (a)
                {
                    case "--selfcheck": selfcheck = true; break;
                    case "--dry-run": dryRun = true; break;
                    case "--fast": fast = true; break;
                    case "--quiet": quiet = true; break;
                    case "--dump-vars": dumpVars = true; break;
                    case "--dump-yaml": dumpYaml = true; break;
                    case "--duration":
                        if (i + 1 >= args.Length) { Console.Error.WriteLine("--duration needs a value"); return 2; }
                        duration = double.Parse(args[++i], CultureInfo.InvariantCulture);
                        break;
                    case "--stop-file":
                        if (i + 1 >= args.Length) { Console.Error.WriteLine("--stop-file needs a path"); return 2; }
                        stopFile = args[++i];
                        break;
                    case "--log":
                        if (i + 1 >= args.Length) { Console.Error.WriteLine("--log needs a path"); return 2; }
                        logFile = args[++i];
                        break;
                    case "-h": case "--help": case "/?": Console.Write(Usage); return 0;
                    default:
                        if (a.StartsWith("-")) { Console.Error.WriteLine("unknown option " + a); Console.Error.Write(Usage); return 2; }
                        scenarioArg = a;
                        break;
                }
            }

            TeeWriter tee = null;
            if (logFile != null)
            {
                tee = new TeeWriter(Console.Out, logFile);
                Console.SetOut(tee);
            }
            try
            {
                string scenarioPath = ResolveScenario(scenarioArg);
                if (selfcheck) return SelfCheck.Run(scenarioPath) ? 0 : 1;
                if (scenarioPath == null)
                {
                    Console.Error.WriteLine("no scenario given and scenarios/race.json not found next to the executable");
                    return 2;
                }
                return Run(scenarioPath, dryRun, fast, quiet, dumpVars, dumpYaml, duration, stopFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
                if (Environment.GetEnvironmentVariable("IRSDK_EMULATOR_DEBUG") == "1") Console.WriteLine(ex.ToString());
                return 1;
            }
            finally
            {
                tee?.Dispose();
            }
        }

        /// <summary>Console.Out replacement that also appends to a log file (flushed per line).</summary>
        private sealed class TeeWriter : TextWriter
        {
            private readonly TextWriter _console;
            private readonly StreamWriter _file;

            public TeeWriter(TextWriter console, string path)
            {
                _console = console;
                _file = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            }

            public override System.Text.Encoding Encoding => _console.Encoding;
            public override void Write(char value) { _console.Write(value); _file.Write(value); }
            public override void Write(string value) { _console.Write(value); _file.Write(value); }
            public override void WriteLine(string value) { _console.WriteLine(value); _file.WriteLine(value); }
            public override void Flush() { _console.Flush(); _file.Flush(); }

            protected override void Dispose(bool disposing)
            {
                if (disposing) { try { _file.Dispose(); } catch { } }
                base.Dispose(disposing);
            }
        }

        public static string ExeDir
        {
            get
            {
                try { return AppDomain.CurrentDomain.BaseDirectory; } catch { return Directory.GetCurrentDirectory(); }
            }
        }

        /// <summary>Resolves the scenario argument: explicit path, or a name under scenarios/ next to the exe or the cwd.</summary>
        public static string ResolveScenario(string arg)
        {
            var candidates = new List<string>();
            if (string.IsNullOrEmpty(arg))
            {
                candidates.Add(Path.Combine(ExeDir, "scenarios", "race.json"));
                candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "scenarios", "race.json"));
                candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "race.json"));
            }
            else
            {
                candidates.Add(arg);
                candidates.Add(arg + ".json");
                candidates.Add(Path.Combine(ExeDir, "scenarios", arg));
                candidates.Add(Path.Combine(ExeDir, "scenarios", arg + ".json"));
                candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "scenarios", arg));
                candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "scenarios", arg + ".json"));
            }
            foreach (var c in candidates) if (File.Exists(c)) return Path.GetFullPath(c);
            if (!string.IsNullOrEmpty(arg)) throw new FileNotFoundException("scenario not found: " + arg);
            return null;
        }

        private static int Run(string scenarioPath, bool dryRun, bool fast, bool quiet, bool dumpVars, bool dumpYaml, double duration, string stopFile)
        {
            if (stopFile != null && File.Exists(stopFile))
            {
                try { File.Delete(stopFile); } catch { }
                Console.WriteLine("removed stale stop file " + stopFile);
            }
            var sc = Scenario.Load(scenarioPath);
            foreach (var w in sc.Warnings) Console.WriteLine("warning: " + w);

            // Build the layout first so we know how much shared memory we need.
            var probe = sc.CreateVarTable();
            var layout = ShmLayout.Build(probe.Defs(), sc.TickRate, sc.NumBuf, sc.SessionInfoCapacity);
            long mapSize = Math.Max(sc.MapSize, layout.TotalSize);

            bool needShm = !dryRun && !dumpVars && !dumpYaml;
            IShmSink sink = needShm ? (IShmSink)new MmfSink(mapSize) : new MemorySink((int)mapSize);
            using (var runner = new Runner(sc, sink))
            {
                runner.Log = s => Console.WriteLine("  " + s);
                if (dumpVars || dumpYaml)
                {
                    runner.Log = s => Console.Error.WriteLine("  " + s);
                    foreach (var d in runner.Drivers) d.Init(runner.Ctx);
                    runner.Ctx.Dt = 1.0 / sc.TickRate;
                    foreach (var d in runner.Drivers) if (d.Enabled) d.Tick(runner.Ctx); // one tick so drivers fill their variables / results
                }

                if (dumpVars)
                {
                    Console.WriteLine("{0,-32} {1,-8} {2,5} {3,-14} {4,7}  {5}", "name", "type", "count", "unit", "offset", "value(s)");
                    foreach (var s in runner.Vars.Slots)
                    {
                        string val = s.Def.Count == 1 ? runner.Vars.Format(s.Def.Name) : ArrayPreview(s.Values);
                        string pin = Array.IndexOf(s.Pinned, true) >= 0 ? " [pinned]" : "";
                        Console.WriteLine("{0,-32} {1,-8} {2,5} {3,-14} {4,7}  {5}{6}", s.Def.Name, IrType.Name(s.Def.Type), s.Def.Count, s.Def.Unit, s.Def.Offset, val, pin);
                    }
                    Console.WriteLine();
                    Console.WriteLine(runner.Vars.Count + " variables, bufLen " + runner.Layout.BufLen + " bytes");
                    return 0;
                }
                if (dumpYaml)
                {
                    Console.Write(runner.Yaml.Render());
                    return 0;
                }

                Console.WriteLine("IrsdkEmulator - scenario '" + sc.Name + "' (" + scenarioPath + ")");
                Console.WriteLine("  memory : " + sink.Describe());
                Console.WriteLine("  layout : " + runner.Vars.Count + " vars, varHeaders @" + runner.Layout.VarHeaderOffset +
                                  ", sessionInfo @" + runner.Layout.SessionInfoOffset + " (" + runner.Layout.SessionInfoCapacity + " bytes)" +
                                  ", " + runner.Layout.NumBuf + " buffers x " + runner.Layout.BufLen + " bytes @" + runner.Layout.BufOffsets[0] +
                                  ", total " + runner.Layout.TotalSize + " bytes, " + sc.TickRate + " Hz");
                Console.WriteLine("  yaml   : " + (runner.Yaml.Path ?? "(built-in minimal template)"));
                Console.WriteLine("  drivers: " + string.Join("; ", DescribeDrivers(runner.Drivers)));
                if (sc.Timeline.Count > 0) Console.WriteLine("  timeline: " + sc.Timeline.Count + " event(s), first at " + sc.Timeline[0].At.ToString("0.##", CultureInfo.InvariantCulture) + " s");

                runner.Start();
                Console.WriteLine("  session info: " + runner.CurrentYamlLength + " chars, sessionInfoUpdate=" + runner.SessionInfoUpdate + ", status=1. " +
                                  (dryRun ? "Dry run." : "SimHub (game: iRacing) can connect now.") + " Ctrl+C to stop.");

                Console.CancelKeyPress += (s, e) => { e.Cancel = true; _stop = true; };
                bool timerBoosted = BoostTimerResolution(true);
                try
                {
                    Loop(runner, sc.TickRate, quiet, dryRun && fast, duration, stopFile);
                }
                finally
                {
                    if (timerBoosted) BoostTimerResolution(false);
                    runner.Stop();
                    Console.WriteLine("stopped after " + runner.Tick + " ticks (" + runner.Time.ToString("0.0", CultureInfo.InvariantCulture) + " s); status set to 0, shared memory released.");
                }
            }
            return 0;
        }

        private static void Loop(Runner runner, int tickRate, bool quiet, bool fast, double duration, string stopFile)
        {
            double period = 1.0 / tickRate;
            var sw = Stopwatch.StartNew();
            double anchor = sw.Elapsed.TotalSeconds;
            long baseTick = runner.Tick;
            double nextPrint = 1.0;
            int lateWarnings = 0;
            while (!_stop)
            {
                runner.Step();
                if (runner.Time >= nextPrint)
                {
                    if (!quiet) Console.WriteLine(runner.StatusLine());
                    nextPrint = Math.Floor(runner.Time) + 1.0;
                    if (stopFile != null && File.Exists(stopFile))
                    {
                        Console.WriteLine("stop file " + stopFile + " found, stopping");
                        try { File.Delete(stopFile); } catch { }
                        break;
                    }
                }
                if (duration > 0 && runner.Time >= duration) break;
                if (fast) continue;

                double due = anchor + (runner.Tick - baseTick) * period;
                double now = sw.Elapsed.TotalSeconds;
                double wait = due - now;
                if (wait < -1.0)
                {
                    if (lateWarnings++ < 5) Console.WriteLine("  warning: " + (-wait).ToString("0.00", CultureInfo.InvariantCulture) + " s behind real time, resynchronising");
                    anchor = now;
                    baseTick = runner.Tick;
                    continue;
                }
                if (wait > 0.002) Thread.Sleep((int)((wait - 0.001) * 1000));
                while (sw.Elapsed.TotalSeconds < due) Thread.SpinWait(30);
            }
        }

        private static bool BoostTimerResolution(bool on)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return false;
            try
            {
                if (on) TimeBeginPeriod(1); else TimeEndPeriod(1);
                return true;
            }
            catch { return false; }
        }

        private static IEnumerable<string> DescribeDrivers(List<Driver> drivers)
        {
            foreach (var d in drivers) yield return d.Describe() + (d.Enabled ? "" : " (disabled)");
        }

        private static string ArrayPreview(double[] v)
        {
            var inv = CultureInfo.InvariantCulture;
            int n = Math.Min(v.Length, 8);
            var parts = new string[n];
            for (int i = 0; i < n; i++) parts[i] = v[i].ToString("0.###", inv);
            return "[" + string.Join(", ", parts) + (v.Length > n ? ", ..." : "") + "] (" + v.Length + ")";
        }
    }
}
