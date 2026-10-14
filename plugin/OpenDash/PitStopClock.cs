// PitStopClock.cs: how long the car has stood in its pit box, on a sim whose own pit timer counts something else. #1130.
//
// SimHub times a stop for every game: GameManagerBase notes the frame IsInPit goes above 0, publishes the
// seconds since as IsInPitSince while it stays there, and keeps the last of them as LastPitStopDuration when
// it drops. On iRacing, Assetto Corsa and ACC IsInPit is the pit box, and that is the stop. On Le Mans
// Ultimate the reader answers IsInPit with the player's mInPits, which is the whole pit lane, so the pit view
// drew the time from the entry line, and a 30 s stop in a lane that takes 25 s to drive read about 55 s.
//
// So the plugin keeps the same clock with the box in place of IsInPit, on a sim whose row of
// SimCapabilities.cs answers InPitBox, and publishes it as Contract.PitStopSeconds and
// Contract.LastPitStopSeconds. Where the row has no answer both are null and every dashboard reads SimHub's
// own members, which is what it read before and with no plugin. A session restart forgets the last stop, as
// SimHub's RaiseSessionRestart zeroes its LastPitStopDuration, and the stop under way with it, since a stop
// begun in the session before is none of this one's. The restart is marked by SimHub's SessionRestart event
// and carried out on the next frame, so the clock is only ever moved by Update. Pure: no SimHub types, so the
// net8.0 tests compile it.
using System;
using System.Threading;

namespace OpenDashPlugin
{
    /// <summary>
    /// SimHub's pit stop timer, run on the plugin's own answer to whether the car is in its box. Written by the
    /// data thread on every frame; read by SimHub's property delegates, each of which reads one value.
    /// </summary>
    public sealed class PitStopClock
    {
        private DateTime? enteredBox;
        private double inBoxSeconds;
        private double lastStopSeconds;

        // What the delegates read, written last on every frame. A reader reads one of the two, so it never
        // matters that it may see this frame's and the next one's; nothing is allocated per frame.
        private double publishedSeconds;
        private double publishedLast;
        private bool timing;

        // A restart marked by SimHub's event and not yet carried out; taken by the next Update.
        private int restartPending;

        /// <summary>The seconds the car has stood in its box on this stop, 0 outside it; null where the plugin does not time stops.</summary>
        public double? PitStopSeconds => Volatile.Read(ref timing) ? Volatile.Read(ref publishedSeconds) : (double?)null;

        /// <summary>How long the last stop lasted, 0 before the first; null where the plugin does not time stops.</summary>
        public double? LastPitStopSeconds => Volatile.Read(ref timing) ? Volatile.Read(ref publishedLast) : (double?)null;

        /// <summary>One frame: whether the car is in its box, null where the running sim's row does not say, and the frame's time.</summary>
        /// <remarks>
        /// A null publishes null for the frame and leaves the clock as it stood: on a sim whose row never
        /// answers that is every frame, and on Le Mans Ultimate a frame with a reading missing says nothing
        /// about the stop, so the next one that does carries on from it rather than from nothing.
        /// </remarks>
        public void Update(bool? inBox, DateTime now)
        {
            if (Interlocked.Exchange(ref restartPending, 0) != 0) Forget();
            if (!inBox.HasValue)
            {
                Volatile.Write(ref timing, false);
                return;
            }
            if (inBox.Value)
            {
                if (!enteredBox.HasValue) enteredBox = now;
                // Never negative: a frame stamped before the one the car stopped on is the moment it stopped.
                inBoxSeconds = Math.Max(0.0, (now - enteredBox.Value).TotalSeconds);
            }
            else
            {
                // As SimHub does: the stop's length is the last in-box reading, taken on the frame the car leaves.
                if (inBoxSeconds > 0.0) lastStopSeconds = inBoxSeconds;
                enteredBox = null;
                inBoxSeconds = 0.0;
            }
            Volatile.Write(ref publishedSeconds, inBoxSeconds);
            Volatile.Write(ref publishedLast, lastStopSeconds);
            Volatile.Write(ref timing, true);
        }

        /// <summary>
        /// A session restart: the last stop forgotten, as SimHub forgets its own, and the stop under way too, so a
        /// car still in its box counts from the restart. Only marked here, from whatever thread SimHub raises its
        /// event on, and carried out by the next Update, which is when what is published moves.
        /// </summary>
        public void Restart()
        {
            Interlocked.Exchange(ref restartPending, 1);
        }

        /// <summary>No game, or a frame the clock failed on: both readings null, and the last stop forgotten.</summary>
        public void Reset()
        {
            Volatile.Write(ref timing, false);
            Interlocked.Exchange(ref restartPending, 0);
            Forget();
        }

        private void Forget()
        {
            enteredBox = null;
            inBoxSeconds = 0.0;
            lastStopSeconds = 0.0;
        }
    }
}
