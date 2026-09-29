// PanelLive.cs: what the sidebar's live card says about the game SimHub is reading.
//
// Three states and no more, because the card is read at a glance: no game at all, a game with nothing
// running in it, and a session with a car and a track. The plugin copies the six values out of the frame
// it is handed (LiveStatus.cs); this decides the words and the dot. Nothing here is telemetry.
//
// Pure: PanelLiveTests holds the three states.
namespace OpenDashPlugin
{
    /// <summary>The card's lines and its dot.</summary>
    public sealed class LiveCard
    {
        public LiveCard(string eyebrow, string eyebrowHex, string line1, string line2, string dotHex, bool live)
        {
            Eyebrow = eyebrow;
            EyebrowHex = eyebrowHex;
            Line1 = line1;
            Line2 = line2;
            DotHex = dotHex;
            Live = live;
        }

        /// <summary>The small tracked line beside the dot.</summary>
        public string Eyebrow { get; private set; }

        public string EyebrowHex { get; private set; }

        /// <summary>The car, or null.</summary>
        public string Line1 { get; private set; }

        /// <summary>"Track · session", or null.</summary>
        public string Line2 { get; private set; }

        public string DotHex { get; private set; }

        /// <summary>Whether a session is running, which is when the dot is green.</summary>
        public bool Live { get; private set; }

        /// <summary>The whole card in one line, for the rail's dot, which has no room for the lines.</summary>
        public string Tooltip
        {
            get
            {
                var text = Eyebrow;
                if (!string.IsNullOrEmpty(Line1)) text += "\n" + Line1;
                if (!string.IsNullOrEmpty(Line2)) text += "\n" + Line2;
                return text;
            }
        }
    }

    public static class PanelLive
    {
        public const string NoGame = "No game running";

        /// <summary>The card for what SimHub last reported. A null status reads as no game.</summary>
        public static LiveCard Card(LiveStatus status)
        {
            if (status == null) status = LiveStatus.None;
            return Card(status.GameName, status.GameRunning, status.CarModel ?? status.CarId, status.TrackName, status.SessionType);
        }

        /// <summary>
        /// The card for these values.
        /// </summary>
        /// <remarks>
        /// SimHub knows which game it is set to read before the game is started, so a game name alone is a
        /// game with nothing running and says only its name, in grey. A session is the game running with
        /// something to name in it; the eyebrow turns green and reads "Live · iRacing", and the lines are
        /// the car and "track · session", each left out where the sim did not say.
        /// </remarks>
        public static LiveCard Card(string gameName, bool gameRunning, string car, string track, string session)
        {
            var game = Clean(gameName);
            if (game == null) return new LiveCard(NoGame, Theme.TextSecondary, null, null, Theme.StatusNotInstalled, false);
            var carName = Clean(car);
            var place = Join(Clean(track), Clean(session));
            if (!gameRunning || (carName == null && place == null))
            {
                return new LiveCard(game, Theme.TextSecondary, null, null, Theme.StatusNotInstalled, false);
            }
            return new LiveCard("Live · " + game, Theme.StatusUpToDate, carName, place, Theme.StatusUpToDate, true);
        }

        private static string Clean(string value)
        {
            if (value == null) return null;
            var trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static string Join(string first, string second)
        {
            if (first == null) return second;
            return second == null ? first : first + " · " + second;
        }
    }
}
