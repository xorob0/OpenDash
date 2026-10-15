// TrackMap.cs: whether SimHub holds an outline of the track the car is on, which is what its map item
// draws and what Contract.TrackMapRecorded publishes.
//
// SimHub records the outline itself, from the car's own position over one lap driven from the line to
// the line outside the pit lane, and keeps it per track and layout. The map item draws nothing until it
// has one, and no property says so: the readiness the item keeps is not one. The plugin reads SimHub's
// record in DataUpdate and hands this class the count of its points, so the rule is tested here without
// SimHub. docs/research/simhub-dash-format.md records how the rule was read (#555).

namespace OpenDashPlugin
{
    /// <summary>What <see cref="Contract.TrackMapRecorded"/> carries.</summary>
    public static class TrackMap
    {
        /// <summary>
        /// The most points a record can hold and still be ignored. SimHub's map item draws an outline from more
        /// than this many and nothing from this many or fewer, and its recorder saves none with so few.
        /// </summary>
        public const int MinimumPoints = 10;

        /// <summary>
        /// True where SimHub holds an outline its map item will draw, false where it holds none for the track
        /// the car is on, and null where driving a lap would not record one: SimHub's recorder runs only when
        /// the frame names a track and the sim allows a map, and the caller answers null with no game running.
        /// </summary>
        /// <param name="trackNamed">Whether the frame's TrackIdWithConfig is set, which SimHub's recorder needs.</param>
        /// <param name="mapAllowed">The sim's own MapAllowed for the frame.</param>
        /// <param name="points">How many points the map SimHub would draw holds, or null where it holds none.</param>
        public static bool? Recorded(bool trackNamed, bool mapAllowed, int? points)
        {
            if (!trackNamed || !mapAllowed) return null;
            return points.HasValue && points.Value > MinimumPoints;
        }
    }
}
