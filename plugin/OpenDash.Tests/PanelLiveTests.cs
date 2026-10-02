// PanelLiveTests.cs: the live card's three states.
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelLiveTests
    {
        [Fact]
        public void No_game_is_a_grey_dot_and_one_line()
        {
            var card = PanelLive.Card(LiveStatus.None);
            Assert.Equal("No game running", card.Eyebrow);
            Assert.Null(card.Line1);
            Assert.Null(card.Line2);
            Assert.Equal(Theme.StatusNotInstalled, card.DotHex);
            Assert.False(card.Live);
            Assert.Equal("No game running", PanelLive.Card((LiveStatus)null).Eyebrow);
        }

        [Fact]
        public void A_closed_sim_is_no_game_running_whatever_SimHub_has_selected()
        {
            // SimHub names the selected game on every frame, running or not.
            var card = PanelLive.Card(new LiveStatus("iRacing", false, null, null, null, null));
            Assert.Equal("No game running", card.Eyebrow);
            Assert.Equal(Theme.StatusNotInstalled, card.DotHex);
            Assert.False(card.Live);
            // Even with a car and a track left over from the last session.
            Assert.Equal("No game running", PanelLive.Card(new LiveStatus("iRacing", false, "x", "Radical SR8", "Monza", "Race")).Eyebrow);
        }

        [Fact]
        public void A_game_running_with_nothing_in_it_is_its_name()
        {
            var card = PanelLive.Card(new LiveStatus("iRacing", true, null, null, null, null));
            Assert.Equal("iRacing", card.Eyebrow);
            Assert.Equal(Theme.TextSecondary, card.EyebrowHex);
            Assert.Null(card.Line1);
            Assert.Null(card.Line2);
            Assert.False(card.Live);
            // A session type alone is not a session to name.
            Assert.False(PanelLive.Card(new LiveStatus("iRacing", true, null, null, null, "Practice")).Live);
        }

        [Fact]
        public void The_name_is_the_one_a_person_reads_and_is_not_shortened_here()
        {
            // The plugin hands the display name; the sidebar trims what does not fit its 151 px.
            var card = PanelLive.Card(new LiveStatus("Assetto Corsa Competizione", true, "ferrari_296_gt3", "Ferrari 296 GT3", "Spa", "Race"));
            Assert.Equal("Live · Assetto Corsa Competizione", card.Eyebrow);
            Assert.True(card.Live);
        }

        [Fact]
        public void A_session_is_live_the_car_and_the_track_and_session()
        {
            var card = PanelLive.Card(new LiveStatus("iRacing", true, "porsche992cup", "Porsche 911 GT3 R (992)", "Spa-Francorchamps", "Practice"));
            Assert.Equal("Live · iRacing", card.Eyebrow);
            Assert.Equal(Theme.StatusUpToDate, card.EyebrowHex);
            Assert.Equal("Porsche 911 GT3 R (992)", card.Line1);
            Assert.Equal("Spa-Francorchamps · Practice", card.Line2);
            Assert.Equal(Theme.StatusUpToDate, card.DotHex);
            Assert.True(card.Live);
            Assert.Equal("Live · iRacing\nPorsche 911 GT3 R (992)\nSpa-Francorchamps · Practice", card.Tooltip);
        }

        [Fact]
        public void What_the_sim_did_not_say_is_left_out()
        {
            var noModel = PanelLive.Card(new LiveStatus("iRacing", true, "radicalsr8", null, "Monza", null));
            Assert.Equal("radicalsr8", noModel.Line1);
            Assert.Equal("Monza", noModel.Line2);
            var noTrack = PanelLive.Card(new LiveStatus("iRacing", true, null, "Radical SR8", " ", "Race"));
            Assert.Equal("Race", noTrack.Line2);
        }
    }
}
