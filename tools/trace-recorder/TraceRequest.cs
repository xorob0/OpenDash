// TraceRequest.cs: what a recording asks for, as `bun run record` leaves it beside the plugin.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;

namespace OpenDashTraceRecorder
{
    public class TraceRequest
    {
        /// <summary>
        /// Decimals a number is rounded to. Four is past anything a dashboard draws and short of
        /// the noise in a double, which is what keeps a re-recording of an unchanged scenario from
        /// diffing on every line.
        /// </summary>
        public const int Decimals = 4;

        public string Scenario { get; private set; }
        public int Hz { get; private set; }
        public int Frames { get; private set; }
        /// <summary>Emulator ticks between frames: its tick rate divided by <see cref="Hz"/>.</summary>
        public int Step { get; private set; }
        /// <summary>
        /// How many ticks to let pass after SimHub first reports the game running, before the first
        /// frame is taken. Counted from what the recorder sees rather than from an absolute tick:
        /// the emulator's SessionTick starts at the scenario's own session time, which is seventy
        /// thousand in a race half run, so no fixed number could serve every scenario.
        /// </summary>
        public long WarmUpTicks { get; private set; }
        public string Out { get; private set; }
        /// <summary>Written once the last frame is on disk; the caller waits on it.</summary>
        public string DoneFile { get; private set; }
        public IList<string> Properties { get; private set; }

        public static TraceRequest Read(string file)
        {
            var json = JObject.Parse(File.ReadAllText(file));
            var properties = new List<string>();
            var declared = json["properties"] as JArray;
            if (declared != null)
            {
                foreach (var entry in declared)
                {
                    var name = entry?.ToString();
                    if (!string.IsNullOrEmpty(name)) properties.Add(name);
                }
            }
            if (properties.Count == 0) throw new InvalidDataException("the request names no property to record");

            var request = new TraceRequest
            {
                Scenario = Text(json, "scenario", "unnamed"),
                Hz = Whole(json, "hz", 10),
                Frames = Whole(json, "frames", 200),
                Step = Whole(json, "step", 6),
                WarmUpTicks = Whole(json, "warmUpTicks", 7200),
                Out = Text(json, "out", null),
                Properties = properties,
            };
            if (string.IsNullOrEmpty(request.Out)) throw new InvalidDataException("the request says nowhere to write");
            if (request.Frames < 1) throw new InvalidDataException("frames must be at least 1");
            if (request.Step < 1) throw new InvalidDataException("step must be at least 1");
            request.DoneFile = request.Out + ".done";
            return request;
        }

        private static string Text(JObject json, string key, string fallback)
        {
            var token = json[key];
            return token == null || token.Type == JTokenType.Null ? fallback : token.ToString();
        }

        private static int Whole(JObject json, string key, int fallback)
        {
            var token = json[key];
            if (token == null || token.Type == JTokenType.Null) return fallback;
            return System.Convert.ToInt32(token.ToString(), CultureInfo.InvariantCulture);
        }
    }
}
