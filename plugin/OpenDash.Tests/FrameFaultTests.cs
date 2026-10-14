// FrameFaultTests: a fault in DataUpdate is said in SimHub's log, and said once rather than sixty
// times a second (#865).
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class FrameFaultTests
    {
        private static FrameFault Lights()
        {
            return new FrameFault("Computing the car's own lights", "the strips fall back to the ladder the sim publishes");
        }

        /// <summary>Runs a part for a number of frames at sixty a second, and returns what it logged.</summary>
        private static List<FaultLine> Frames(FrameFault fault, ref long nowMs, int frames, bool fails)
        {
            var lines = new List<FaultLine>();
            for (var i = 0; i < frames; i++)
            {
                var line = fails ? fault.Failed(new KeyNotFoundException("The given key '6' was not present"), nowMs) : fault.Succeeded();
                if (line != null) lines.Add(line);
                nowMs += 16;
            }
            return lines;
        }

        [Fact]
        public void A_part_that_never_fails_never_logs()
        {
            var fault = Lights();
            long now = 0;
            Assert.Empty(Frames(fault, ref now, 600, false));
            Assert.False(fault.Failing);
        }

        [Fact]
        public void The_first_failure_is_logged_whole_and_says_what_the_driver_sees_meanwhile()
        {
            var fault = Lights();
            var line = fault.Failed(new KeyNotFoundException("The given key '6' was not present"), 1000);

            Assert.NotNull(line);
            Assert.Equal(FaultLevel.Error, line.Level);
            Assert.StartsWith("Computing the car's own lights failed, so the strips fall back to the ladder the sim publishes until it recovers.", line.Text);
            // The exception itself, type and message, so the log is enough to find the cause.
            Assert.Contains("KeyNotFoundException", line.Text);
            Assert.Contains("The given key '6' was not present", line.Text);
            Assert.True(fault.Failing);
            Assert.Equal(1, fault.Count);
        }

        [Fact]
        public void A_failure_on_every_frame_is_one_line_a_minute_with_a_count()
        {
            var fault = Lights();
            long now = 0;
            // Five minutes of a fault on every frame: 18,750 frames.
            var lines = Frames(fault, ref now, 5 * 60 * 1000 / 16, true);

            // The first, and then one a minute rather than one a frame.
            Assert.Equal(5, lines.Count);
            Assert.Equal(FaultLevel.Error, lines[0].Level);
            Assert.All(lines.Skip(1), l => Assert.Equal(FaultLevel.Warn, l.Level));
            // A minute at sixty frames a second, counted rather than written: 3750 failures at 16 ms.
            Assert.StartsWith("Computing the car's own lights failed 3750 times since the last line about it", lines[1].Text);
            Assert.EndsWith("most recently with KeyNotFoundException: The given key '6' was not present", lines[1].Text);
            Assert.Equal(5 * 60 * 1000 / 16, fault.Count);
        }

        [Fact]
        public void The_frame_that_completes_again_says_so_once()
        {
            var fault = Lights();
            long now = 0;
            Frames(fault, ref now, 100, true);
            var lines = Frames(fault, ref now, 600, false);

            var line = Assert.Single(lines);
            Assert.Equal(FaultLevel.Info, line.Level);
            Assert.Equal("Computing the car's own lights recovered after failing on 100 frames in a row", line.Text);
            Assert.False(fault.Failing);
            Assert.Equal(0, fault.Count);
        }

        [Fact]
        public void A_fault_that_comes_and_goes_every_other_frame_is_still_a_line_or_two_a_minute()
        {
            // A fault in one gear and not the next, or on alternate frames: every failure is a new run, and
            // a rule that logged the start of each run would be thirty lines a second again.
            var fault = Lights();
            long now = 0;
            var lines = new List<FaultLine>();
            for (var i = 0; i < 2 * 60 * 1000 / 16; i++)
            {
                lines.AddRange(Frames(fault, ref now, 1, i % 2 == 0));
            }

            // Each line about failing is followed by the recovery it is owed, and no more often than a minute.
            Assert.Equal(new[] { FaultLevel.Error, FaultLevel.Info, FaultLevel.Warn, FaultLevel.Info },
                lines.Select(l => l.Level).ToArray());
            // The failures in between are counted into the next line rather than dropped.
            Assert.StartsWith("Computing the car's own lights failed 1875 times since the last line about it", lines[2].Text);
        }

        [Fact]
        public void A_recovery_from_failures_the_log_was_not_told_about_is_not_announced_either()
        {
            var fault = Lights();
            Assert.NotNull(fault.Failed(new InvalidOperationException("first"), 0));
            Assert.NotNull(fault.Succeeded());
            // Inside the minute: counted, not said, and so its recovery is not said either.
            Assert.Null(fault.Failed(new InvalidOperationException("second"), 1000));
            Assert.Null(fault.Succeeded());
            // And the next line about failing says it, a minute after the last.
            var later = fault.Failed(new InvalidOperationException("third"), FrameFault.IntervalMs);
            Assert.NotNull(later);
            Assert.StartsWith("Computing the car's own lights failed 2 times since the last line about it", later.Text);
        }
    }
}
