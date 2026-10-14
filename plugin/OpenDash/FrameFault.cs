// FrameFault.cs: what one part of DataUpdate says in SimHub's log when it fails, and how often.
//
// DataUpdate runs at SimHub's data rate, and a fault in it is rarely a single frame: a table that
// throws in third gear throws on every frame in third gear. Logging each one would be sixty lines a
// second; logging none, which is what the plugin did until #865, left a strip drawing the published
// ladder with nothing anywhere to say why. So the first failure is logged whole, with its stack, a
// failure that keeps happening is counted and summed up once a minute, and the frame that completes
// again says so once. A cause the log has not yet been given whole is given whole when its line comes
// round, so a part that fails again half an hour later for another reason leaves that reason's stack
// as well.
//
// One of these per part of the frame, so that the class best lap failing is not reported as the lights
// failing, and so that a fault in one part is counted without silencing another's.
//
// Pure: no SimHub types and the clock is a parameter, so OpenDash.Tests compiles it and pins the
// rate. The caller writes the lines it is handed.
using System;

namespace OpenDashPlugin
{
    /// <summary>How loud a <see cref="FaultLine"/> is in SimHub's log.</summary>
    public enum FaultLevel
    {
        Info,
        Warn,
        Error,
    }

    /// <summary>One line for SimHub's log, which the caller writes as it is.</summary>
    public sealed class FaultLine
    {
        public FaultLine(FaultLevel level, string text)
        {
            Level = level;
            Text = text;
        }

        public FaultLevel Level { get; private set; }

        public string Text { get; private set; }
    }

    /// <summary>The failures of one part of the per-frame work, and the lines they are owed.</summary>
    public sealed class FrameFault
    {
        /// <summary>
        /// The fewest milliseconds between two lines about failing. A minute: often enough that a log
        /// read after a session shows when a fault was happening, rarely enough that a fault on every
        /// frame is one line among SimHub's own.
        /// </summary>
        public const long IntervalMs = 60000;

        private readonly string what;
        private readonly string fallback;

        /// <summary>When the last line about failing was handed out, or null before the first.</summary>
        private long? lastLineMs;

        /// <summary>The cause the log was last given whole, as type and message, or null before the first.</summary>
        private string loggedWhole;

        /// <summary>Failures since the last line about failing, which the next one reports.</summary>
        private long unreported;

        /// <summary>Whether the failures since the part last completed have been said in the log at all,
        /// so that a recovery from failures nobody was told about is not announced either.</summary>
        private bool announced;

        /// <param name="what">The part, as the start of a sentence: "Computing the car's own lights".</param>
        /// <param name="fallback">What a driver sees meanwhile, as the rest of one: "the strips fall back to
        /// the ladder the sim publishes".</param>
        public FrameFault(string what, string fallback)
        {
            this.what = what;
            this.fallback = fallback;
        }

        /// <summary>Whether the part failed on the last frame it ran.</summary>
        public bool Failing { get; private set; }

        /// <summary>How many frames in a row the part has failed, zero while it completes.</summary>
        public long Count { get; private set; }

        /// <summary>
        /// A frame on which the part threw. The line to log, or null when this failure is only counted.
        /// </summary>
        /// <param name="error">What it threw.</param>
        /// <param name="nowMs">Monotonic milliseconds.</param>
        public FaultLine Failed(Exception error, long nowMs)
        {
            Failing = true;
            Count++;
            unreported++;
            if (lastLineMs.HasValue && nowMs - lastLineMs.Value < IntervalMs) return null;

            FaultLine line;
            var cause = Describe(error);
            if (!lastLineMs.HasValue)
            {
                // The first is the one worth reading, so it carries the whole exception, stack and all.
                line = new FaultLine(FaultLevel.Error, what + " failed, so " + fallback + " until it recovers. " +
                    "A failure that repeats is counted and logged once a minute: " + error);
                loggedWhole = cause;
            }
            else if (cause != loggedWhole)
            {
                // A cause the log has only been told by name, or not at all: its stack is worth reading too.
                line = new FaultLine(FaultLevel.Error, what + " failed " + Times(unreported) +
                    " since the last line about it, most recently for a reason not logged before, so " + fallback +
                    " until it recovers: " + error);
                loggedWhole = cause;
            }
            else
            {
                line = new FaultLine(FaultLevel.Warn, what + " failed " + Times(unreported) +
                    " since the last line about it, most recently with " + cause);
            }
            lastLineMs = nowMs;
            unreported = 0;
            announced = true;
            return line;
        }

        /// <summary>
        /// A frame on which the part completed. The line to log once it recovers from failures the log
        /// was told about, and null otherwise, which is every frame of a part that is working.
        /// </summary>
        public FaultLine Succeeded()
        {
            if (!Failing) return null;
            var count = Count;
            var said = announced;
            Failing = false;
            Count = 0;
            announced = false;
            if (!said) return null;
            return new FaultLine(FaultLevel.Info, what + " recovered after failing on " + Frames(count) + " in a row");
        }

        private static string Describe(Exception error)
        {
            if (error == null) return "no exception";
            return error.GetType().Name + ": " + error.Message;
        }

        private static string Times(long count)
        {
            if (count == 1) return "once";
            if (count == 2) return "twice";
            return count + " times";
        }

        private static string Frames(long count)
        {
            return count == 1 ? "1 frame" : count + " frames";
        }
    }
}
