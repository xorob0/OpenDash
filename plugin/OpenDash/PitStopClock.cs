// PitStopClock.cs: how long the car has been stopped in its pit box, and how long its last stop was, on a
// sim whose own timer counts something else (#1130).
//
// SimHub times a stop from its IsInPit: GameData.IsInPitSince is the seconds since it became true, and
// LastPitStopDuration the length of the last spell it was true. Le Mans Ultimate's reader answers IsInPit
// with the player's mInPits, the whole pit lane, so its timer runs from the entry line, and a 30 s stop in a
// lane that takes 25 s to drive reads about 55 s. The plugin times the box instead, from the running sim's
// InPitBox in SimCapabilities.cs, and publishes what this clock reads as Contract.PitStopSeconds and
// Contract.LastPitStopSeconds. Where the row leaves the box to SimHub, the clock reads null and a dashboard
// reads SimHub's own members, as it does with no plugin.
//
// Pure: no SimHub types, so the net8.0 tests compile it. The plugin hands it each frame's answer and the
// time of the frame; it is used from SimHub's data thread only.
using System;

namespace OpenDashPlugin
{
    /// <summary>Times the stops in the box, frame by frame, as SimHub times its IsInPit.</summary>
    public sealed class PitStopClock
    {
        /// <summary>Whether the last frame had an answer: false with no game, a game with no row, or a row that leaves it to SimHub.</summary>
        private bool timing;

        /// <summary>The time of the last frame, in seconds.</summary>
        private double now;

        /// <summary>When the stop under way began, or null while the car is not in its box.</summary>
        private double? stoppedAt;

        /// <summary>How long the last stop to end was, or null before one has.</summary>
        private double? last;

        /// <summary>
        /// The seconds the car has been stopped in its box on this stop, as SimHub's IsInPitSince is for its
        /// IsInPit: 0 while it is not in the box, and null while the clock has no answer to time.
        /// </summary>
        public double? Seconds
        {
            get
            {
                if (!timing) return null;
                return stoppedAt.HasValue ? Math.Max(0, now - stoppedAt.Value) : 0;
            }
        }

        /// <summary>
        /// How long the last stop in the box was, as SimHub's LastPitStopDuration is for its IsInPit: kept through
        /// the next stop until that one ends, 0 before a first stop, and null while the clock has no answer to time.
        /// </summary>
        public double? LastSeconds
        {
            get
            {
                if (!timing) return null;
                return last ?? 0;
            }
        }

        /// <summary>
        /// One frame: whether the car is in its box, true, false or null where the sim's row does not say, and the
        /// frame's time in seconds on a clock that only goes forward. A null forgets every stop, since a frame with
        /// no answer is a game that stopped or one SimHub times itself.
        /// </summary>
        public void Update(bool? inBox, double nowSeconds)
        {
            if (!inBox.HasValue)
            {
                Reset();
                return;
            }
            timing = true;
            now = nowSeconds;
            if (inBox.Value)
            {
                if (!stoppedAt.HasValue) stoppedAt = nowSeconds;
            }
            else if (stoppedAt.HasValue)
            {
                last = Math.Max(0, nowSeconds - stoppedAt.Value);
                stoppedAt = null;
            }
        }

        /// <summary>Forgets every stop, so that the clock reads null until a frame answers again.</summary>
        public void Reset()
        {
            timing = false;
            stoppedAt = null;
            last = null;
        }
    }
}
