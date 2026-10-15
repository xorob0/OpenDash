// TrackMapTests.cs: when the track module says its map has yet to be recorded, and when it says nothing.
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class TrackMapTests
    {
        [Fact]
        public void An_outline_the_map_item_draws_is_recorded()
        {
            Assert.True(TrackMap.Recorded(true, true, 11));
            Assert.True(TrackMap.Recorded(true, true, 2400));
        }

        [Fact]
        public void No_outline_or_one_the_map_item_ignores_is_not_recorded()
        {
            // SimHub's map item draws from more than ten points and nothing from ten or fewer, so a record of
            // ten draws the same empty box as no record at all, and the driver is told the same thing.
            Assert.False(TrackMap.Recorded(true, true, null));
            Assert.False(TrackMap.Recorded(true, true, 0));
            Assert.False(TrackMap.Recorded(true, true, TrackMap.MinimumPoints));
        }

        [Fact]
        public void Where_a_lap_would_record_nothing_it_is_null_so_the_map_draws_as_it_did()
        {
            // Null falls through to the dashboard's fallback, which draws the map item as it was drawn before
            // the plugin answered: telling a driver to drive a lap that cannot record anything would be wrong.
            // SimHub's recorder runs only with a TrackIdWithConfig and the sim's MapAllowed, so either missing
            // answers null.
            Assert.Null(TrackMap.Recorded(false, true, 2400));
            Assert.Null(TrackMap.Recorded(false, true, null));
            Assert.Null(TrackMap.Recorded(true, false, null));
            Assert.Null(TrackMap.Recorded(false, false, null));
        }

        [Fact]
        public void It_is_published_last_in_the_shared_group_and_not_offered_as_a_setting()
        {
            var shared = Contract.SharedPropertyNames().ToList();
            Assert.Equal(shared.Count - 1, shared.IndexOf(Contract.TrackMapRecorded));
            Assert.Equal(shared.IndexOf(Contract.Capabilities.Last()) + 1, shared.IndexOf(Contract.TrackMapRecorded));
            Assert.Equal("TrackMapRecorded", Contract.TrackMapRecorded);
        }
    }
}
