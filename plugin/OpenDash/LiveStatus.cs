// LiveStatus.cs: what SimHub says the rig is doing right now, reduced to what the settings panel names.
//
// The Home page says what each device is showing, and the LEDs page whether the car in the session is one
// Lovely Car Data has measured (#503). Both need the game, the car and the track, and the panel cannot read
// SimHub's GameData itself: it lives on the data thread, and the panel on the interface one. So the plugin
// copies these six values out of the frame it is handed, only when one of them changes, and the panel reads
// the copy. Nothing is computed from them: this is naming, not telemetry.
//
// Pure: no SimHub types, so OpenDash.Tests compiles it and holds its equality.
using System;

namespace OpenDashPlugin
{
    /// <summary>The game, the car and the track SimHub last reported, as one immutable value.</summary>
    public sealed class LiveStatus : IEquatable<LiveStatus>
    {
        public LiveStatus(string gameName, bool gameRunning, string carId, string carModel, string trackName, string sessionType)
        {
            GameName = gameName;
            GameRunning = gameRunning;
            CarId = carId;
            CarModel = carModel;
            TrackName = trackName;
            SessionType = sessionType;
        }

        /// <summary>Nothing reported yet: no game, not running, and no car or track to name.</summary>
        public static readonly LiveStatus None = new LiveStatus(null, false, null, null, null, null);

        /// <summary>SimHub's name for the game it is set to read, which it knows with the game closed.</summary>
        public string GameName { get; private set; }

        /// <summary>Whether that game is running and sending data.</summary>
        public bool GameRunning { get; private set; }

        /// <summary>The sim's own id for the car, which is what the car light tables are keyed by.</summary>
        public string CarId { get; private set; }

        /// <summary>The car as a person would name it.</summary>
        public string CarModel { get; private set; }

        public string TrackName { get; private set; }

        /// <summary>The session as SimHub names it: "Race", "Practice", "Offline Testing".</summary>
        public string SessionType { get; private set; }

        public bool Equals(LiveStatus other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            return GameRunning == other.GameRunning
                && string.Equals(GameName, other.GameName, StringComparison.Ordinal)
                && string.Equals(CarId, other.CarId, StringComparison.Ordinal)
                && string.Equals(CarModel, other.CarModel, StringComparison.Ordinal)
                && string.Equals(TrackName, other.TrackName, StringComparison.Ordinal)
                && string.Equals(SessionType, other.SessionType, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LiveStatus);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (GameName == null ? 0 : StringComparer.Ordinal.GetHashCode(GameName));
                hash = hash * 31 + (GameRunning ? 1 : 0);
                hash = hash * 31 + (CarId == null ? 0 : StringComparer.Ordinal.GetHashCode(CarId));
                hash = hash * 31 + (CarModel == null ? 0 : StringComparer.Ordinal.GetHashCode(CarModel));
                hash = hash * 31 + (TrackName == null ? 0 : StringComparer.Ordinal.GetHashCode(TrackName));
                hash = hash * 31 + (SessionType == null ? 0 : StringComparer.Ordinal.GetHashCode(SessionType));
                return hash;
            }
        }

        public static bool operator ==(LiveStatus left, LiveStatus right)
        {
            return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.Equals(right);
        }

        public static bool operator !=(LiveStatus left, LiveStatus right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return (GameName ?? "no game") + (GameRunning ? " running" : " idle")
                + (CarModel == null ? string.Empty : ", " + CarModel)
                + (TrackName == null ? string.Empty : " at " + TrackName)
                + (SessionType == null ? string.Empty : ", " + SessionType);
        }
    }
}
