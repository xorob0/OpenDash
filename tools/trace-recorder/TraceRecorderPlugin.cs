// TraceRecorderPlugin.cs: the SimHub side of `bun run record`.
//
// SimHub's normalised view of a sim -- DataCorePlugin.GameData.* and everything computed on top of
// it -- is SimHub's own code, and no amount of reading the emulator tells you what it produces. So
// the recording is taken from inside a running SimHub: this plugin reads a request left next to it,
// samples the named properties frame by frame and writes what it saw. The file it leaves is
// transposed into the committed columnar trace by scripts/record.ts; nothing here knows that format.
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

namespace OpenDashTraceRecorder
{
    [PluginName("OpenDash trace recorder")]
    [PluginAuthor("OpenDash contributors")]
    [PluginDescription("Development tool: records SimHub's property values to a file for the openDash telemetry traces.")]
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

        /// <summary>Properties whose .NET type a JSON scalar would not carry; written as a trailer.</summary>
        private readonly Dictionary<string, string> valueTypes = new Dictionary<string, string>();

        public void Init(PluginManager pluginManager)
        {
            // SimHub's own directory, which is where a plugin DLL is dropped and where the
            // caller leaves the request. The same idiom the openDash plugin uses to find SimHub.
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

            SimHub.Logging.Current.Info(string.Format(CultureInfo.InvariantCulture,
                "OpenDash trace recorder: {0} frames of {1} properties every {2} ticks, {3} ticks after the game appears, into {4}",
                request.Frames, request.Properties.Count, request.Step, request.WarmUpTicks, request.Out));
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
                ["recorder"] = 1,
                ["scenario"] = request.Scenario,
                ["hz"] = request.Hz,
                ["frames"] = request.Frames,
                ["warmUpTicks"] = request.WarmUpTicks,
                ["step"] = request.Step,
                ["started"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            };
            writer.WriteLine(header.ToString(Formatting.None));
        }

        private void WriteFrame(PluginManager pluginManager, long tick)
        {
            var values = new JObject();
            foreach (var property in request.Properties) values[property] = Convert(property, pluginManager.GetPropertyValue(property));
            var frame = new JObject { ["tick"] = tick, ["v"] = values };
            writer.WriteLine(frame.ToString(Formatting.None));
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
        /// A property value as JSON. Numbers are rounded, because SimHub's doubles carry noise far
        /// below anything a dashboard draws and an unrounded trace would diff on every re-recording.
        /// A TimeSpan and a DateTime travel as strings and are noted in the trailer, so that a reader
        /// can tell them from a property that really is text.
        /// </summary>
        private JToken Convert(string property, object value)
        {
            if (value == null) return JValue.CreateNull();
            if (value is string s) return new JValue(s);
            if (value is bool b) return new JValue(b);
            if (value is TimeSpan ts)
            {
                valueTypes[property] = "timespan";
                return new JValue(ts.ToString("c", CultureInfo.InvariantCulture));
            }
            if (value is DateTime dt)
            {
                valueTypes[property] = "datetime";
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
