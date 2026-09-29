// ClassBestLapTests.cs: what the class best a dashboard reads looks like before and after a lap.
using System;
using System.Linq;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class ClassBestLapTests
    {
        [Fact]
        public void A_car_with_a_lap_publishes_that_lap()
        {
            var lap = TimeSpan.FromSeconds(101.877);
            Assert.Equal(lap, ClassBestLap.Of(lap));
        }

        [Fact]
        public void No_car_and_no_lap_yet_are_both_null_so_the_field_draws_its_placeholder()
        {
            // Null, never zero: a zero would draw as 0:00.000 where the dashboard's isnull() would have
            // fallen through to the placeholder.
            Assert.Null(ClassBestLap.Of(null));
            Assert.Null(ClassBestLap.Of(TimeSpan.Zero));
            Assert.Null(ClassBestLap.Of(TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public void It_is_published_with_the_shared_group_and_not_offered_as_a_setting()
        {
            // At the index it shipped at rather than last: the shared group is appended to, and the
            // clock format came after it (#324).
            var shared = Contract.SharedPropertyNames().ToList();
            Assert.Equal(22, shared.IndexOf(Contract.ClassBestLap));
            Assert.Equal("ClassBestLap", Contract.ClassBestLap);
        }
    }
}
