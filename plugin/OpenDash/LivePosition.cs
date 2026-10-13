// LivePosition.cs: the player's place in a race as it stands on the road, for the sim whose own place
// moves only at the line.
//
// iRacing scores a race at the start/finish line: CarIdxPosition and CarIdxClassPosition change when a
// car crosses it, and SimHub hands both on unchanged as Opponent.Position and Opponent.PositionInClass.
// A driver who passes a car in the first corner reads the old place for the rest of the lap (#1063).
// SimHub's leaderboard would sort on Opponent.LivePosition if the reader filled it, and iRacing's does
// not. So the plugin works the place out from what SimHub does publish for every car,
// CurrentLapHighPrecision -- the lap and the fraction of it covered, which SimHub already keeps
// continuous across the line -- and publishes it as OpenDash.LivePosition and LiveClassPosition.
// Pure: no SimHub types, so the net8.0 tests compile it.
using System;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    /// <summary>One car of the leaderboard, as much of it as a live place needs.</summary>
    public sealed class LiveCar
    {
        /// <summary>Laps covered, the current one as a fraction: SimHub's CurrentLapHighPrecision. Null where SimHub has none.</summary>
        public double? Progress { get; set; }

        /// <summary>The sim's own place, 0 while it has not placed the car.</summary>
        public int Position { get; set; }

        /// <summary>The sim's own place in the car's class, 0 while it has not placed the car.</summary>
        public int ClassPosition { get; set; }

        /// <summary>The car's class, compared as written.</summary>
        public string CarClass { get; set; }

        public bool IsPlayer { get; set; }

        /// <summary>Whether the car is in the session; a car that has left keeps its distance but stops moving.</summary>
        public bool IsConnected { get; set; }
    }

    /// <summary>The player's place counted from the cars ahead on the road.</summary>
    /// <remarks>
    /// <para>
    /// Two cars that are both on the road with a distance are put in the order of that distance. Every
    /// other pair keeps the sim's order: a car that has disconnected, or that SimHub has no distance for,
    /// is ahead of the player exactly when the sim says it is. That is what stops a car that left the
    /// server from dropping to last while it is still classified ahead, and it costs nothing when every
    /// car is running, which is the case the ticket is about. A tie in distance also keeps the sim's
    /// order.
    /// </para>
    /// <para>
    /// The answer is null whenever the sim's own place is the right one or the only one: outside a race,
    /// where a place is a lap time rather than a distance; once the chequered flag is out, when cars stop
    /// racing and their distances stop meaning anything; and before the sim has placed the player, so
    /// that the grid still reads <c>--</c> (#992). Null rather than the sim's place, because the
    /// dashboards read this with <c>isnull()</c> and fall back to the sim's place themselves, which is
    /// also what they draw with no plugin at all.
    /// </para>
    /// </remarks>
    public static class LivePosition
    {
        /// <summary>The session name SimHub reports for a race; every other session is ranked on lap time.</summary>
        public const string RaceSession = "Race";

        /// <summary>The game SimHub names iRacing by, the one sim whose place waits for the line.</summary>
        public const string IRacing = "IRacing";

        /// <summary>Whether the live place applies to this frame at all.</summary>
        /// <param name="gameName">SimHub's name for the running game.</param>
        /// <param name="sessionType">SimHub's SessionTypeName.</param>
        /// <param name="chequered">Whether the chequered flag is out.</param>
        public static bool Applies(string gameName, string sessionType, bool chequered)
        {
            return string.Equals(gameName, IRacing, StringComparison.OrdinalIgnoreCase)
                && string.Equals(sessionType, RaceSession, StringComparison.OrdinalIgnoreCase)
                && !chequered;
        }

        /// <summary>The player's place over the whole field, or null where the sim's own place stands.</summary>
        public static int? Overall(IList<LiveCar> field)
        {
            return Place(field, false);
        }

        /// <summary>The player's place in their own class, or null where the sim's own place stands.</summary>
        public static int? InClass(IList<LiveCar> field)
        {
            return Place(field, true);
        }

        private static int? Place(IList<LiveCar> field, bool inClass)
        {
            if (field == null) return null;
            var me = -1;
            for (var i = 0; i < field.Count; i++)
            {
                if (field[i] != null && field[i].IsPlayer)
                {
                    me = i;
                    break;
                }
            }
            if (me < 0) return null;
            var player = field[me];
            var playerPlace = SimPlace(player, inClass);
            if (playerPlace <= 0 || !Running(player)) return null;

            var place = 1;
            for (var i = 0; i < field.Count; i++)
            {
                var car = field[i];
                if (i == me || car == null) continue;
                if (inClass && !string.Equals(car.CarClass ?? "", player.CarClass ?? "", StringComparison.Ordinal)) continue;
                if (Ahead(car, SimPlace(car, inClass), player, playerPlace)) place++;
            }
            return place;
        }

        /// <summary>Whether <paramref name="car"/> is ahead of the player.</summary>
        private static bool Ahead(LiveCar car, int carPlace, LiveCar player, int playerPlace)
        {
            if (Running(car) && car.Progress.Value != player.Progress.Value) return car.Progress.Value > player.Progress.Value;
            // The sim's order, where one of the two has no distance to compare or the distances tie. A car
            // the sim has not placed is behind every car it has.
            return carPlace > 0 && carPlace < playerPlace;
        }

        private static bool Running(LiveCar car)
        {
            return car.IsConnected && car.Progress.HasValue && !double.IsNaN(car.Progress.Value);
        }

        private static int SimPlace(LiveCar car, bool inClass)
        {
            return inClass ? car.ClassPosition : car.Position;
        }
    }
}
