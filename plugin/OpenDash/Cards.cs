// Cards.cs: the card catalogue, mirroring the catalogue in packages/dash/src/contract.ts (spec section 4).
// A bun test extracts the ids and display names from this file, so keep one card per line.
using System.Collections.Generic;

namespace OpenDashPlugin
{
    public sealed class Card
    {
        public Card(int number, string id, string label, string displayName, string description)
        {
            Number = number;
            Id = id;
            Label = label;
            DisplayName = displayName;
            Description = description;
        }

        /// <summary>The value a slot setting takes.</summary>
        public int Number { get; }

        /// <summary>The id used in the dash source and as the screen name in cards.djson.</summary>
        public string Id { get; }

        /// <summary>The label as drawn on the card. Bound labels show their default reading.</summary>
        public string Label { get; }

        /// <summary>The name shown in the plugin's slot picker.</summary>
        public string DisplayName { get; }

        public string Description { get; }

        public override string ToString() => DisplayName;
    }

    public static class Cards
    {
        public const int Count = 13;

        public static readonly IReadOnlyList<Card> All = new[]
        {
            new Card(0, "currentLap", "CURRENT", "Current lap", "Running lap time."),
            new Card(1, "lastLap", "LAST", "Last lap", "Last lap time, purple when it is the session best."),
            new Card(2, "bestLap", "BEST", "Best lap", "Session best lap time."),
            new Card(3, "delta", "DELTA", "Delta", "Live delta to the reference lap."),
            new Card(4, "position", "POSITION", "Position", "Position and car count, overall or in class."),
            new Card(5, "session", "LAP", "Session", "Lap of total, or time left."),
            new Card(6, "fuel", "FUEL", "Fuel", "Fuel remaining."),
            new Card(7, "fuelLaps", "FUEL LAPS", "Fuel laps", "Laps remaining on the fuel you have."),
            new Card(8, "tc", "TC", "TC", "Traction control level."),
            new Card(9, "abs", "ABS", "ABS", "ABS level."),
            new Card(10, "tyreTemps", "TYRES °C · LAST STOP", "Tyre temps", "Four tyre temperatures from the last stop."),
            new Card(11, "tyrePressures", "PRESSURES PSI · LAST STOP", "Tyre pressures", "Four tyre pressures from the last stop."),
            new Card(12, "speed", "SPEED", "Speed", "Current speed."),
        };

        public static bool IsValidNumber(int number) => number >= 0 && number < All.Count;

        public static Card ByNumber(int number) => IsValidNumber(number) ? All[number] : null;

        public static string DisplayName(int number) => ByNumber(number)?.DisplayName ?? ("Card " + number);
    }
}
