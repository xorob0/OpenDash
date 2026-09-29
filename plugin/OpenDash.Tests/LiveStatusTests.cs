// LiveStatusTests.cs: the names the plugin copies out of SimHub's frame for the settings panel.
//
// DataUpdate replaces the copy only when it differs from the last one, so equality is the whole of what
// keeps the interface thread from being handed a new object every frame. OpenDash.cs itself holds
// SimHub's GameData and cannot be compiled here, so the tests read the source for the one line that
// relies on it.
using System.IO;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class LiveStatusTests
    {
        private static LiveStatus Session(string track = "Spa")
        {
            return new LiveStatus("IRacing", true, "mx5 mx52016", "Mazda MX-5 Cup", track, "Race");
        }

        [Fact]
        public void Two_statuses_naming_the_same_things_are_equal()
        {
            Assert.Equal(Session(), Session());
            Assert.True(Session() == Session());
            Assert.False(Session() != Session());
            Assert.Equal(Session().GetHashCode(), Session().GetHashCode());
            Assert.True(Session().Equals((object)Session()));
        }

        [Fact]
        public void Any_field_that_moves_makes_a_different_status()
        {
            var session = Session();
            Assert.NotEqual(session, Session("Monza"));
            Assert.NotEqual(session, new LiveStatus("IRacing", false, "mx5 mx52016", "Mazda MX-5 Cup", "Spa", "Race"));
            Assert.NotEqual(session, new LiveStatus("AssettoCorsa", true, "mx5 mx52016", "Mazda MX-5 Cup", "Spa", "Race"));
            Assert.NotEqual(session, new LiveStatus("IRacing", true, "bmwm4gt3", "Mazda MX-5 Cup", "Spa", "Race"));
            Assert.NotEqual(session, new LiveStatus("IRacing", true, "mx5 mx52016", "Global MX-5", "Spa", "Race"));
            Assert.NotEqual(session, new LiveStatus("IRacing", true, "mx5 mx52016", "Mazda MX-5 Cup", "Spa", "Practice"));
            // Case counts: a sim that renames a car has renamed it.
            Assert.NotEqual(session, new LiveStatus("IRacing", true, "MX5 MX52016", "Mazda MX-5 Cup", "Spa", "Race"));
            Assert.False(session.Equals(null));
            Assert.True(session != null);
        }

        [Fact]
        public void None_is_no_game_and_nothing_to_name()
        {
            Assert.Null(LiveStatus.None.GameName);
            Assert.False(LiveStatus.None.GameRunning);
            Assert.Null(LiveStatus.None.CarId);
            Assert.Null(LiveStatus.None.CarModel);
            Assert.Null(LiveStatus.None.TrackName);
            Assert.Null(LiveStatus.None.SessionType);
            Assert.Equal(LiveStatus.None, new LiveStatus(null, false, null, null, null, null));
            Assert.NotEqual(LiveStatus.None, Session());
            LiveStatus nothing = null;
            Assert.True(nothing == null);
            Assert.False(nothing == LiveStatus.None);
            Assert.Equal("no game idle", LiveStatus.None.ToString());
            Assert.Equal("IRacing running, Mazda MX-5 Cup at Spa, Race", Session().ToString());
        }

        [Fact]
        public void The_plugin_replaces_its_copy_only_when_something_moved()
        {
            var source = File.ReadAllText(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "OpenDash.cs"));
            Assert.Contains("private volatile LiveStatus live = LiveStatus.None;", source);
            Assert.Contains("if (frame != live) live = frame;", source);
            Assert.Contains("public LiveStatus Live => live;", source);
        }
    }
}
