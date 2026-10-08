// TraceRecorderPlugin.cs: the SimHub side of `bun run record`.
//
// SimHub's normalised view of a sim -- DataCorePlugin.GameData.* and everything computed on top of
// it -- is SimHub's own code, and no amount of reading the emulator tells you what it produces. So
// the recording is taken from inside a running SimHub: this plugin reads a request left next to it,
// samples the named properties frame by frame and writes what it saw. The file it leaves is
// transposed into the committed columnar trace by scripts/record.ts; nothing here knows that format.
//
// Beside the properties it records calls (#257): `drivername(3)`, `getopponentleaderboardposition_
// aheadbehind(-1)` and the rest answer from SimHub's in-memory leaderboard rather than from a
// property, so no amount of reading properties sees them. The recorder evaluates each call text
// with SimHub's own NCalc engine, the one a dashboard binding goes through, and writes the answer
// under that text.
//
// Frames are taken on the emulator's own tick rather than on the wall clock. The emulator is a
// function of its tick, so a frame keyed to tick 300 holds the same telemetry on every run, and a
// re-recording of an unchanged scenario differs only where SimHub itself carries history.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GameReaderCommon;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimHub.Plugins;
using SimHub.Plugins.OutputPlugins.Dash.GLCDTemplating;
using SimHub.Plugins.OutputPlugins.Dash.TemplatingCommon;

namespace OpenDashTraceRecorder
{
    [PluginName("OpenDash trace recorder")]
    [PluginAuthor("OpenDash contributors")]
    [PluginDescription("Development tool: records SimHub's property values to a file for the OpenDash telemetry traces.")]
    public class TraceRecorderPlugin : IPlugin, IDataPlugin
    {
        /// <summary>The request, beside this DLL. No request means the plugin does nothing at all.</summary>
        public const string RequestFileName = "opendash-trace-request.json";

        /// <summary>The property carrying the emulator's tick. Frames are taken on it, not on the clock.</summary>
        private const string TickProperty = "DataCorePlugin.GameRawData.Telemetry.SessionTick";

        public PluginManager PluginManager { get; set; }

        private TraceRequest request;
        private StreamWriter writer;
        private int framesWritten;
        private long nextTick;
        private bool finished;
        private int updatesWithoutTick;
        private bool warnedAboutTick;
        /// <summary>The first tick seen with the game running; the warm-up and the grid start here.</summary>
        private long? firstTick;

        /// <summary>
        /// SimHub's NCalc engine, our own instance of it, for the calls. Null when the request names
        /// none. Built with the parsing cache, so that a call is parsed once rather than on every
        /// frame, and with the result cache off: <c>ParseValue</c> otherwise keeps one answer per
        /// expression text in a dictionary shared with every dashboard, cleared once per update, and
        /// there is no reason to write into SimHub's cache or read a dashboard's answer back out of it.
        /// </summary>
        private NCalcEngineBase engine;

        /// <summary>Properties whose .NET type a JSON scalar would not carry; written as a trailer.</summary>
        private readonly Dictionary<string, string> valueTypes = new Dictionary<string, string>();

        public void Init(PluginManager pluginManager)
        {
            // SimHub's own directory, which is where a plugin DLL is dropped and where the
            // caller leaves the request. The same idiom the OpenDash plugin uses to find SimHub.
            var requestFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, RequestFileName);
            if (!File.Exists(requestFile))
            {
                SimHub.Logging.Current.Info("OpenDash trace recorder: no " + RequestFileName + " beside the plugin, recording nothing");
                return;
            }

            try
            {
                request = TraceRequest.Read(requestFile);
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error("OpenDash trace recorder: " + requestFile + " is unusable", ex);
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.Out));
                // The done marker is what `bun run record` waits on, so a stale one from an earlier
                // run would make it pull a file that is still being written.
                File.Delete(request.DoneFile);
                writer = new StreamWriter(new FileStream(request.Out, FileMode.Create, FileAccess.Write, FileShare.Read));
                writer.NewLine = "\n";
                // Every frame reaches the file as it is taken. `bun run record` stops SimHub with
                // Stop-Process, which runs no shutdown code at all, so a buffered recording
                // interrupted that way would come back empty rather than short.
                writer.AutoFlush = true;
                WriteHeader();
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error("OpenDash trace recorder: cannot write " + request.Out, ex);
                Close();
                return;
            }

            if (request.Calls.Count > 0)
            {
                // Logging off: a call that throws (a null compared, a probe asking what NCalc does
                // with a bad operand) answers null, which is what the dash draws, and SimHub would
                // otherwise log it on every frame, since each frame evaluates a fresh value.
                engine = new NCalcEngineBase(true) { UseCache = false, AllowLogging = false };
            }

            SimHub.Logging.Current.Info(string.Format(CultureInfo.InvariantCulture,
                "OpenDash trace recorder: {0} frames of {1} properties and {2} calls every {3} ticks, {4} ticks after the game appears, into {5}",
                request.Frames, request.Properties.Count, request.Calls.Count, request.Step, request.WarmUpTicks, request.Out));
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            if (writer == null || finished) return;
            // A sample taken while the game is not running is SimHub's defaults, not the scenario.
            if (data == null || !data.GameRunning) return;

            var tick = AsLong(pluginManager.GetPropertyValue(TickProperty));
            if (tick == null)
            {
                // Without the tick nothing is ever recorded, and the caller would only see a wait
                // that timed out. Said once, after five seconds of updates, so the log names the
                // property rather than repeating it sixty times a second.
                if (!warnedAboutTick && ++updatesWithoutTick > 300)
                {
                    warnedAboutTick = true;
                    SimHub.Logging.Current.Error("OpenDash trace recorder: " + TickProperty + " is not published; nothing will be recorded");
                }
                return;
            }
            if (firstTick == null)
            {
                firstTick = tick.Value;
                nextTick = tick.Value + request.WarmUpTicks;
                SimHub.Logging.Current.Info("OpenDash trace recorder: the game appeared at tick " + tick.Value + "; the first frame is tick " + nextTick);
            }
            if (tick.Value < nextTick) return;

            WriteFrame(pluginManager, tick.Value);
            framesWritten++;
            AdvanceToNextFrame(tick.Value);
            if (framesWritten >= request.Frames) Finish();
        }

        public void End(PluginManager pluginManager)
        {
            // A SimHub that closes on its own mid-recording leaves a short file, which is worth
            // having: the done marker is not written either way, so `bun run record` reports the
            // truncation rather than committing it.
            if (!finished) Close();
        }

        /// <summary>
        /// Moves the target on by whole steps from the tick asked for, and past the tick that
        /// arrived. Counting from the target keeps the frames on the grid when a sample is a tick
        /// late, which is the ordinary case. Skipping past the arrival is what matters when the
        /// first one is far late, which happens whenever SimHub takes a while to call the game
        /// running: without it the target would already be in the past and the frames it stands for
        /// would all be written on the one tick, a dozen copies of the same telemetry.
        /// </summary>
        private void AdvanceToNextFrame(long tick)
        {
            nextTick += request.Step;
            if (nextTick > tick) return;
            nextTick += ((tick - nextTick) / request.Step + 1) * request.Step;
        }

        private void WriteHeader()
        {
            var header = new JObject
            {
                ["recorder"] = 2,
                ["scenario"] = request.Scenario,
                ["hz"] = request.Hz,
                ["frames"] = request.Frames,
                ["warmUpTicks"] = request.WarmUpTicks,
                ["step"] = request.Step,
                ["cars"] = request.Cars,
                ["started"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            };
            writer.WriteLine(header.ToString(Formatting.None));
        }

        private void WriteFrame(PluginManager pluginManager, long tick)
        {
            var values = new JObject();
            foreach (var property in request.Properties) values[property] = Convert(property, pluginManager.GetPropertyValue(property));
            var frame = new JObject { ["tick"] = tick, ["v"] = values };
            if (engine != null)
            {
                var calls = new JObject();
                foreach (var call in request.Calls) calls[call] = Convert(call, Evaluate(call));
                frame["c"] = calls;
            }
            writer.WriteLine(frame.ToString(Formatting.None));
        }

        /// <summary>
        /// One call's value on this frame, as SimHub's engine answers it to a dashboard.
        ///
        /// DataUpdate runs after the PluginManager has set <c>NCalcEngineBase.lastData</c> to this
        /// update's GameData and raised PreUpdate, so the leaderboard a call reads is the finished
        /// frame the properties above were read from.
        ///
        /// A fresh ExpressionValue every frame. The value object is where the engine keeps what it
        /// learnt about an expression: <c>IsDeterminist</c>, which once true makes ParseValue return
        /// the first answer for ever, and <c>Invalid</c>, which after one exception makes it answer
        /// null for thirty seconds without evaluating. No call recorded today is determinist, since
        /// any function call makes an expression not so, but a fresh value keeps a column from ever
        /// depending on what the frame before it did. The parse is not repeated: the engine's
        /// parsing cache is keyed by the text.
        /// </summary>
        private object Evaluate(string call)
        {
            try
            {
                return engine.ParseValue((ExpressionValue)call);
            }
            catch (Exception)
            {
                // ParseValue catches what NCalc throws and answers null; anything that escapes it is
                // the same answer as far as a dashboard is concerned.
                return null;
            }
        }

        private void Finish()
        {
            finished = true;
            var types = new JObject();
            foreach (var pair in valueTypes) types[pair.Key] = pair.Value;
            writer.WriteLine(new JObject { ["types"] = types }.ToString(Formatting.None));
            Close();
            try
            {
                File.WriteAllText(request.DoneFile, framesWritten.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error("OpenDash trace recorder: cannot write " + request.DoneFile, ex);
            }
            SimHub.Logging.Current.Info("OpenDash trace recorder: wrote " + framesWritten + " frames to " + request.Out);
        }

        private void Close()
        {
            if (writer == null) return;
            try
            {
                writer.Flush();
                writer.Dispose();
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error("OpenDash trace recorder: cannot close the trace", ex);
            }
            writer = null;
        }

        /// <summary>
        /// A property or call value as JSON. Numbers are rounded, because SimHub's doubles carry noise far
        /// below anything a dashboard draws and an unrounded trace would diff on every re-recording.
        /// A TimeSpan and a DateTime travel as strings and are noted in the trailer, so that a reader
        /// can tell them from a property that really is text.
        /// </summary>
        private JToken Convert(string column, object value)
        {
            if (value == null) return JValue.CreateNull();
            if (value is string s) return new JValue(s);
            if (value is bool b) return new JValue(b);
            if (value is TimeSpan ts)
            {
                valueTypes[column] = "timespan";
                return new JValue(ts.ToString("c", CultureInfo.InvariantCulture));
            }
            if (value is DateTime dt)
            {
                valueTypes[column] = "datetime";
                return new JValue(dt.ToString("o", CultureInfo.InvariantCulture));
            }
            if (value is float || value is double || value is decimal)
            {
                return new JValue(Math.Round(System.Convert.ToDouble(value, CultureInfo.InvariantCulture), TraceRequest.Decimals));
            }
            if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong)
            {
                return new JValue(System.Convert.ToInt64(value, CultureInfo.InvariantCulture));
            }
            // An enum, a struct or anything else SimHub publishes: the string is what a formula sees.
            return new JValue(System.Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static long? AsLong(object value)
        {
            if (value == null) return null;
            try
            {
                return System.Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
