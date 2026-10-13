// LivePosition.cs: the race as it stands on the road, for the sim whose own order moves only at the line.
//
// iRacing scores a race at the start/finish line: CarIdxPosition and CarIdxClassPosition change when a
// car crosses it, and SimHub hands both on unchanged as Opponent.Position and Opponent.PositionInClass.
// Its leaderboard is sorted on them, so every place, every list and every gap on a dashboard moved a lap
// after the pass that changed it (#1063). SimHub would sort on Opponent.LivePosition if the reader filled
// it, and iRacing's does not. So the plugin ranks the field on what SimHub does publish for every car,
// CurrentLapHighPrecision -- the lap and the fraction of it covered, which SimHub already keeps continuous
// across the line -- and publishes each leaderboard row's place and each place's row (Contract.LivePosition).
// Pure: no SimHub types, so the net8.0 tests compile it.
using System;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    /// <summary>One row of SimHub's leaderboard, as much of it as a live order needs.</summary>
    public sealed class LiveCar
    {
        /// <summary>Laps covered, the current one as a fraction: SimHub's CurrentLapHighPrecision. Null where SimHub has none.</summary>
        public double? Progress { get; set; }

        /// <summary>The sim's own place, 0 while it has not placed the car.</summary>
        public int Position { get; set; }

        /// <summary>The car's class, compared as written.</summary>
        public string CarClass { get; set; }

        public bool IsPlayer { get; set; }

        /// <summary>Whether the car is in the session; a car that has left keeps its distance but stops moving.</summary>
        public bool IsConnected { get; set; }
    }

    /// <summary>
    /// What the plugin publishes for one frame: four lists of <see cref="LivePosition.Slots"/>, each counted
    /// from one at index 0, and null wherever there is nothing to say.
    /// </summary>
    public sealed class LiveRace
    {
        /// <summary>The place over the field of the car on each leaderboard row.</summary>
        public int?[] Place { get; } = new int?[LivePosition.Slots];

        /// <summary>The place in its own class of the car on each leaderboard row.</summary>
        public int?[] ClassPlace { get; } = new int?[LivePosition.Slots];

        /// <summary>The leaderboard row of the car in each place over the field.</summary>
        public int?[] Order { get; } = new int?[LivePosition.Slots];

        /// <summary>The leaderboard row of the car in each place of the player's own class.</summary>
        public int?[] ClassOrder { get; } = new int?[LivePosition.Slots];

        /// <summary>One of the four, by <see cref="Contract.LiveFamilies"/> name, at a one-based row or place; null past the end.</summary>
        public int? Read(string family, int n)
        {
            if (n < 1 || n > LivePosition.Slots) return null;
            switch (family)
            {
                case Contract.LivePosition: return Place[n - 1];
                case Contract.LiveClassPosition: return ClassPlace[n - 1];
                case Contract.LiveOrder: return Order[n - 1];
                case Contract.LiveClassOrder: return ClassOrder[n - 1];
                default: return null;
            }
        }
    }

    /// <summary>The field ranked by the distance each car has covered.</summary>
    /// <remarks>
    /// <para>
    /// The cars that are running, connected and with a distance, are ranked on that distance, a tie going
    /// to the sim's order. Every other car keeps the place the sim gives it among them: it goes in ahead of
    /// the first running car the sim has behind it. That is what stops a car that left the server from
    /// dropping to last while it is still classified ahead, and it costs nothing when every car is
    /// running, which is the case the ticket is about. It is a merge rather than a sort on a comparison
    /// because a comparison mixing the two orders is not transitive: a running car ahead of a second on
    /// the road can be behind a stopped car the second is ahead of, and a sort on that throws or loops.
    /// A car the sim has not placed is behind every car it has, and is drawn in its row with no place.
    /// </para>
    /// <para>
    /// Nothing is published -- the dashboards then draw the sim's own order, which is also what they draw
    /// with no plugin -- while the driver has Live positions switched off; outside a race, where a place is
    /// a lap time rather than a distance; once the chequered flag is out, when cars stop racing and their
    /// distances stop meaning anything; and before the sim has placed anybody, so that the grid still reads
    /// <c>--</c> (#992).
    /// </para>
    /// </remarks>
    public static class LivePosition
    {
        /// <summary>How many rows and places are published: iRacing's largest field.</summary>
        public const int Slots = 64;

        /// <summary>The session name SimHub reports for a race; every other session is ranked on lap time.</summary>
        public const string RaceSession = "Race";

        /// <summary>The game SimHub names iRacing by, the one sim whose order waits for the line.</summary>
        public const string IRacing = "IRacing";

        /// <summary>Whether the live order applies to this frame at all.</summary>
        /// <param name="wanted">The driver's Live positions switch, OpenDashSettings.LivePositions.</param>
        /// <param name="gameName">SimHub's name for the running game.</param>
        /// <param name="sessionType">SimHub's SessionTypeName.</param>
        /// <param name="chequered">Whether the chequered flag is out.</param>
        public static bool Applies(bool wanted, string gameName, string sessionType, bool chequered)
        {
            return wanted
                && string.Equals(gameName, IRacing, StringComparison.OrdinalIgnoreCase)
                && string.Equals(sessionType, RaceSession, StringComparison.OrdinalIgnoreCase)
                && !chequered;
        }

        /// <summary>The leaderboard as it stands on the road, or null where the sim's own order stands.</summary>
        /// <param name="field">SimHub's leaderboard, in its order: entry 0 is row 1.</param>
        public static LiveRace Rank(IList<LiveCar> field)
        {
            if (field == null || field.Count == 0) return null;
            var placed = false;
            for (var i = 0; i < field.Count; i++) placed |= field[i] != null && field[i].Position > 0;
            if (!placed) return null;

            var order = Order(field);
            var race = new LiveRace();
            LiveCar player = null;
            for (var i = 0; i < field.Count; i++)
            {
                if (field[i] != null && field[i].IsPlayer)
                {
                    player = field[i];
                    break;
                }
            }

            var inClass = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var place = 1; place <= order.Count; place++)
            {
                var row = order[place - 1];
                var car = field[row];
                var carClass = car.CarClass ?? "";
                int classPlace;
                inClass.TryGetValue(carClass, out classPlace);
                inClass[carClass] = ++classPlace;

                if (place <= Slots) race.Order[place - 1] = row + 1;
                if (row < Slots && car.Position > 0)
                {
                    race.Place[row] = place;
                    race.ClassPlace[row] = classPlace;
                }
                if (player != null && classPlace <= Slots && string.Equals(carClass, player.CarClass ?? "", StringComparison.Ordinal))
                    race.ClassOrder[classPlace - 1] = row + 1;
            }
            return race;
        }

        /// <summary>The rows of the field, zero-based, from first place to last.</summary>
        private static List<int> Order(IList<LiveCar> field)
        {
            var running = new List<int>();
            var stopped = new List<int>();
            for (var i = 0; i < field.Count; i++)
            {
                if (field[i] == null) continue;
                (Running(field[i]) ? running : stopped).Add(i);
            }
            running.Sort((a, b) =>
            {
                var byDistance = field[b].Progress.Value.CompareTo(field[a].Progress.Value);
                return byDistance != 0 ? byDistance : BySim(field, a, b);
            });
            stopped.Sort((a, b) => BySim(field, a, b));

            // Each stopped car in ahead of the first car the sim has behind it. The stopped cars go in in the
            // sim's order, so one never goes in ahead of a stopped car the sim has ahead of it.
            var order = new List<int>(running);
            foreach (var car in stopped)
            {
                var at = order.Count;
                for (var j = 0; j < order.Count; j++)
                {
                    if (SimKey(field[order[j]]) > SimKey(field[car]))
                    {
                        at = j;
                        break;
                    }
                }
                order.Insert(at, car);
            }
            return order;
        }

        /// <summary>The sim's order, a car it has not placed behind every car it has, then the leaderboard's.</summary>
        private static int BySim(IList<LiveCar> field, int a, int b)
        {
            var bySim = SimKey(field[a]).CompareTo(SimKey(field[b]));
            return bySim != 0 ? bySim : a.CompareTo(b);
        }

        private static int SimKey(LiveCar car)
        {
            return car.Position > 0 ? car.Position : int.MaxValue;
        }

        private static bool Running(LiveCar car)
        {
            return car.IsConnected && car.Progress.HasValue && !double.IsNaN(car.Progress.Value) && !double.IsInfinity(car.Progress.Value);
        }
    }
}
