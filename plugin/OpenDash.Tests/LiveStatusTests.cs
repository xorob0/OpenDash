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
        public void Is_answers_as_equality_does_and_allocates_nothing()
        {
            var session = Session();
            Assert.True(session.Is("IRacing", true, "mx5 mx52016", "Mazda MX-5 Cup", "Spa", "Race"));
            Assert.False(session.Is("IRacing", false, "mx5 mx52016", "Mazda MX-5 Cup", "Spa", "Race"));
            Assert.False(session.Is("AssettoCorsa", true, "mx5 mx52016", "Mazda MX-5 Cup", "Spa", "Race"));
            Assert.False(session.Is("IRacing", true, "MX5 MX52016", "Mazda MX-5 Cup", "Spa", "Race"));
            Assert.False(session.Is("IRacing", true, "mx5 mx52016", "Global MX-5", "Spa", "Race"));
            Assert.False(session.Is("IRacing", true, "mx5 mx52016", "Mazda MX-5 Cup", "Monza", "Race"));
            Assert.False(session.Is("IRacing", true, "mx5 mx52016", "Mazda MX-5 Cup", "Spa", "Practice"));
            Assert.True(LiveStatus.None.Is(null, false, null, null, null, null));
            Assert.False(LiveStatus.None.Is(null, true, null, null, null, null));

            // The frame with nothing moved is every frame but a handful, and it is asked sixty times a
            // second: it has to cost nothing but the compares.
            string game = "IRacing", car = "mx5 mx52016", model = "Mazda MX-5 Cup", track = "Spa", type = "Race";
            session.Is(game, true, car, model, track, type);
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++) session.Is(game, true, car, model, track, type);
            Assert.Equal(0, System.GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Fact]
        public void The_track_is_named_for_people_where_the_sim_gives_a_name()
        {
            // Assetto Corsa: TrackName is the track's folder, and its ui_track.json gives the name (#1124).
            Assert.Equal("Red Bull Ring", LiveStatus.TrackShown("Red Bull Ring", null, "ks_red_bull_ring"));
            // An AC track that ships no ui_track.json, or names itself with nothing.
            Assert.Equal("ks_red_bull_ring", LiveStatus.TrackShown(null, null, "ks_red_bull_ring"));
            Assert.Equal("ks_red_bull_ring", LiveStatus.TrackShown(" ", null, "ks_red_bull_ring"));
            // iRacing: TrackName is iRacing's code for the track, and the session gives its short name.
            Assert.Equal("Spa", LiveStatus.TrackShown(null, "Spa", "spa gp"));
            Assert.Equal("spa gp", LiveStatus.TrackShown(null, null, "spa gp"));
            Assert.Equal("spa gp", LiveStatus.TrackShown(null, "", "spa gp"));
            // Every other sim, where SimHub publishes neither raw name.
            Assert.Equal("Monza", LiveStatus.TrackShown(null, null, "Monza"));
            Assert.Null(LiveStatus.TrackShown(null, null, null));

            // The plugin reads the same properties the dash does, and hands the panel what this decides.
            var source = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "OpenDash.cs"));
            Assert.Contains("LiveStatus.TrackShown(", source);
            Assert.Contains("GetPropertyValue(LiveStatus.AcTrackNameProperty)", source);
            Assert.Contains("GetPropertyValue(LiveStatus.IRacingTrackNameProperty)", source);
            Assert.Equal("DataCorePlugin.GameRawData.Track.TrackInfo.name", LiveStatus.AcTrackNameProperty);
            Assert.Equal("DataCorePlugin.GameRawData.SessionData.WeekendInfo.TrackDisplayShortName", LiveStatus.IRacingTrackNameProperty);
        }

        [Fact]
        public void The_plugin_replaces_its_copy_only_when_something_moved()
        {
            var source = RepoPaths.Code(Path.Combine(RepoPaths.Root(), "plugin", "OpenDash", "OpenDash.cs"));
            Assert.Contains("private volatile LiveStatus live = LiveStatus.None;", source);
            Assert.Contains("if (!live.Is(gameName, gameRunning, carId, carModel, trackName, sessionType))", source);
            Assert.Contains("live = new LiveStatus(gameName, gameRunning, carId, carModel, trackName, sessionType);", source);
            Assert.DoesNotContain("if (frame != live)", source);
            Assert.Contains("public LiveStatus Live => live;", source);
        }
    }
}
