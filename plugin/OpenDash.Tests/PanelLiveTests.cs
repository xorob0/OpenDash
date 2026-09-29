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
        public void A_game_with_nothing_running_is_its_name()
        {
            var card = PanelLive.Card(new LiveStatus("iRacing", false, null, null, null, null));
            Assert.Equal("iRacing", card.Eyebrow);
            Assert.Equal(Theme.TextSecondary, card.EyebrowHex);
            Assert.Null(card.Line1);
            Assert.False(card.Live);
            // Running with nothing to name is still no session.
            Assert.False(PanelLive.Card(new LiveStatus("iRacing", true, null, null, null, null)).Live);
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
