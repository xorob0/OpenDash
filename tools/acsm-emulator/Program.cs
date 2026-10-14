using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace AcsmEmulator
{
    public static class Program
    {
        private static volatile bool _stop;

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint ms);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint ms);

        /// <summary>The first line of a run, which `bun run emulator` and `bun run record` look for in the log.</summary>
        public const string Banner = "AcsmEmulator - scenario";

        private const string Usage =
@"acs.exe - synthetic Assetto Corsa shared-memory telemetry for SimHub (AcsmEmulator)

usage: acs.exe [scenario.json | name] [options]

  scenario          path to a scenario JSON, or a name resolved as scenarios/<name>.json next to the exe
                    (default: scenarios/race.json)
  --duration <s>    stop after <s> simulated seconds (default: run until Ctrl+C)
  --stop-file <p>   stop cleanly as soon as file <p> exists (checked once per second)
  --log <file>      also append all console output to <file>
  --dry-run         do not create the shared memory, just simulate (any OS)
  --fast            with --dry-run: do not pace to real time
  --quiet           no per-second status line
  --dump            print every value of the three pages after one tick and exit
  --selfcheck       check the page layouts against SimHub's, round-trip every page through the marshaller,
                    check the fake install beside the exe and simulate the scenario for a few seconds
  --help            this text

The executable is called acs.exe because that is the process SimHub's Assetto Corsa reader looks for, and
the folder it runs from is taken as the game's install: content\cars and content\tracks travel beside it.
";

        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            string scenarioArg = null, stopFile = null, logFile = null;
            bool selfcheck = false, dryRun = false, fast = false, quiet = false, dump = false;
            double duration = -1;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                switch (a)
                {
                    case "--selfcheck": selfcheck = true; break;
                    case "--dry-run": dryRun = true; break;
                    case "--fast": fast = true; break;
                    case "--quiet": quiet = true; break;
                    case "--dump": dump = true; break;
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
                return Run(scenarioPath, dryRun, fast, quiet, dump, duration, stopFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
                if (Environment.GetEnvironmentVariable("ACSM_EMULATOR_DEBUG") == "1") Console.WriteLine(ex.ToString());
                return 1;
            }
            finally
            {
                tee?.Dispose();
            }
        }

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

        public static string ResolveScenario(string arg)
        {
            var candidates = new List<string>();
            if (string.IsNullOrEmpty(arg))
            {
                candidates.Add(Path.Combine(ExeDir, "scenarios", "race.json"));
                candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "scenarios", "race.json"));
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

        /// <summary>
        /// The fake install SimHub reads: the folder holding content\, next to the exe or, for a devcheck build run
        /// from the source tree, in the working directory.
        /// </summary>
        public static string InstallDir()
        {
            foreach (var dir in new[] { ExeDir, Directory.GetCurrentDirectory() })
                if (Directory.Exists(Path.Combine(dir, "content", "tracks"))) return Path.GetFullPath(dir);
            return null;
        }

        /// <summary>
        /// What SimHub's AC reader needs on disk before it publishes a frame, or null when it is all there.
        /// ACManager.GetNewRawData lists content\tracks (and throws without it) and reads the car from
        /// content\cars\StaticInfo.CarModel; a missing car with no Documents\Assetto Corsa\cfg\race.ini to fall back
        /// on is a NullReferenceException on every frame, which SimHub logs and shows nothing for.
        /// </summary>
        public static string InstallProblem(string installDir, Pages pages)
        {
            if (installDir == null) return "no content\\tracks folder next to the executable; copy content\\ beside acs.exe";
            string car = pages.StaticInfo.CarModel, track = pages.StaticInfo.Track;
            if (string.IsNullOrWhiteSpace(car)) return "StaticInfo.CarModel is empty; SimHub reads no car and publishes nothing";
            if (string.IsNullOrWhiteSpace(track)) return "StaticInfo.Track is empty; SimHub reads no track";
            if (!Directory.Exists(Path.Combine(installDir, "content", "cars", car))) return "no content\\cars\\" + car + " in " + installDir;
            if (!Directory.Exists(Path.Combine(installDir, "content", "tracks", track.ToLowerInvariant()))) return "no content\\tracks\\" + track.ToLowerInvariant() + " in " + installDir;
            return null;
        }

        private static int Run(string scenarioPath, bool dryRun, bool fast, bool quiet, bool dump, double duration, string stopFile)
        {
            if (stopFile != null && File.Exists(stopFile))
            {
                try { File.Delete(stopFile); } catch { }
                Console.WriteLine("removed stale stop file " + stopFile);
            }
            var sc = Scenario.Load(scenarioPath);
            foreach (var w in sc.Warnings) Console.WriteLine("warning: " + w);

            bool needShm = !dryRun && !dump;
            IPageSink sink = needShm ? (IPageSink)new MmfSink(Runner.MapsFor(sc)) : new MemorySink();
            using (var runner = new Runner(sc, sink))
            {
                runner.Log = s => Console.WriteLine("  " + s);
                if (dump)
                {
                    runner.Log = s => Console.Error.WriteLine("  " + s);
                    runner.Start();
                    foreach (var line in runner.Pages.Describe()) Console.WriteLine(line);
                    return 0;
                }

                string problem = InstallProblem(InstallDir(), runner.Pages);
                if (problem != null && !dryRun) throw new InvalidOperationException(problem);
                if (problem != null) Console.WriteLine("warning: " + problem);

                Console.WriteLine(Banner + " '" + sc.Name + "' (" + scenarioPath + ")");
                Console.WriteLine("  memory : " + sink.Describe());
                Console.WriteLine("  install: " + (InstallDir() ?? "(none)") + ", car " + runner.Pages.StaticInfo.CarModel + ", track " + runner.Pages.StaticInfo.Track);
                Console.WriteLine("  drivers: " + string.Join("; ", Describe(runner.Drivers)));
                if (sc.Timeline.Count > 0) Console.WriteLine("  timeline: " + sc.Timeline.Count + " event(s), first at " + sc.Timeline[0].At.ToString("0.##", CultureInfo.InvariantCulture) + " s");

                runner.Start();
                Console.WriteLine("  status " + runner.Pages.Graphics.Status + ". " + (dryRun ? "Dry run." : "SimHub (game: Assetto Corsa) can connect now.") + " Ctrl+C to stop.");

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
                    Console.WriteLine("stopped after " + runner.Tick + " ticks (" + runner.Time.ToString("0.0", CultureInfo.InvariantCulture) + " s); status set to AC_OFF, shared memory released.");
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
                double wait = due - sw.Elapsed.TotalSeconds;
                if (wait < -1.0)
                {
                    if (lateWarnings++ < 5) Console.WriteLine("  warning: " + (-wait).ToString("0.00", CultureInfo.InvariantCulture) + " s behind real time, resynchronising");
                    anchor = sw.Elapsed.TotalSeconds;
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

        private static IEnumerable<string> Describe(List<Driver> drivers)
        {
            foreach (var d in drivers) yield return d.Describe() + (d.Enabled ? "" : " (disabled)");
        }
    }
}
