// ClassBestLap.cs: the session best of the player's own class, copied out of the frame SimHub has
// finished so that a dashboard never has to look it up in the one SimHub is still building.
//
// This is the one other telemetry value the plugin reads besides the car's own lights (ADR 0018), and
// it computes nothing: SimHub has already chosen the car and its time. It is republished because SimHub
// never declares it as a property of its own; Contract.ClassBestLap says why, and what is wrong
// with the lookup a dashboard would otherwise make.
using System;

namespace OpenDashPlugin
{
    /// <summary>What <see cref="Contract.ClassBestLap"/> carries, from the time SimHub holds for the car.</summary>
    public static class ClassBestLap
    {
        /// <summary>
        /// The time a dashboard is handed: the car's best lap, or null where there is no car or the car
        /// has no lap yet.
        /// </summary>
        /// <remarks>
        /// SimHub clamps a negative best to zero and treats zero as "no lap", which is what a sim hands
        /// back for a car that has not completed one. Null rather than zero, so the dashboard's
        /// <c>isnull()</c> falls through exactly as it does with no plugin attached and the field draws
        /// its placeholder instead of <c>0:00.000</c>.
        /// </remarks>
        public static TimeSpan? Of(TimeSpan? bestLapTime)
        {
            return bestLapTime.HasValue && bestLapTime.Value > TimeSpan.Zero ? bestLapTime : null;
        }
    }
}
