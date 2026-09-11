// Modules.cs: the companion module catalogue and the pit wall zone pages, mirroring
// MODULE_CATALOGUE, ZONE_PAGES and WIDE_ZONE_PAGES in packages/dash/src/contract.ts.
// A test compares the two files line by line, so keep one entry per line.
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>One page of the companion dashboard, which is also what a pit wall zone can show.</summary>
    public sealed class Module
    {
        public Module(int number, string id, string name, string description, bool enabled)
        {
            Number = number;
            Id = id;
            Name = name;
            Description = description;
            Enabled = enabled;
        }

        /// <summary>1-based page number: the companion header counts "n / 21".</summary>
        public int Number { get; }

        /// <summary>Module id, which is also the screen name in the companion dashboard.</summary>
        public string Id { get; }

        public string Name { get; }

        public string Description { get; }

        /// <summary>Whether the module is on before the user has configured anything.</summary>
        public bool Enabled { get; }

        public override string ToString() => Name;
    }

    public static class Modules
    {
        public const int Count = 21;

        /// <summary>
        /// The 21 modules in page order. Energy, Damage and Track rivals are off by default because
        /// iRacing carries none of their data; they ship as honest "not available" pages so that a
        /// user on another sim can switch them on. Module 17 draws the gear alone: a module shows one
        /// thing, and the speed has the speedo module.
        /// </summary>
        public static readonly IReadOnlyList<Module> All = new[]
        {
            new Module(1, "lapTimes", "Lap times", "Last, session best and your best, with laps, estimate and delta.", true),
            new Module(2, "delta", "Delta", "Live delta to the reference lap on a centre-zero bar.", true),
            new Module(3, "sectors", "Sectors", "The three sectors of the last lap with their deltas.", true),
            new Module(4, "speedo", "Speedo", "Speed, RPM, redline and the shift bar.", true),
            new Module(5, "fuel", "Fuel", "Fuel left, time left, what to add and the per-lap use.", true),
            new Module(6, "energy", "Energy", "Virtual energy. Le Mans Ultimate only; iRacing has none.", false),
            new Module(7, "tyres", "Tyres", "Temperature, pressure, wear and compound per corner.", true),
            new Module(8, "pitView", "Pit view", "The pit service order: fuel, tyres, repairs and tear-off.", true),
            new Module(9, "carSettings", "Car settings", "TC, ABS, brake bias, mixture and anti-roll bars.", true),
            new Module(10, "inputs", "Inputs", "Throttle, brake and clutch traces with bar gauges.", true),
            new Module(11, "session", "Session", "Session type, position, class, lap and time left.", true),
            new Module(12, "radar", "Radar", "Proximity radar with the spotter on both sides.", true),
            new Module(13, "track", "Track", "The track map with every car on it.", true),
            new Module(14, "leaderboard", "Leaderboard", "Position, driver, class, gap, best and last.", true),
            new Module(15, "relative", "Relative", "The cars around you on track, you in the middle.", true),
            new Module(16, "opponents", "Opponents", "The car ahead and the car behind, in detail.", true),
            new Module(17, "gear", "Gear", "The gear, as large as the screen allows.", true),
            new Module(18, "stint", "Stint", "Stint laps and time, stops and the last stop.", true),
            new Module(19, "lapHistory", "Lap history", "Your last laps with the delta to the session best.", true),
            new Module(20, "damage", "Damage", "Body and suspension damage. iRacing reports none.", false),
            new Module(21, "trackRivals", "Track rivals", "Segment comparison against the field. Not a SimHub value.", false),
        };

        public static bool IsValidNumber(int number) => number >= 1 && number <= Count;

        public static Module ByNumber(int number) => IsValidNumber(number) ? All[number - 1] : null;

        public static string DisplayName(int number) => ByNumber(number)?.Name ?? ("Module " + number);

        /// <summary>The default on/off state of every module, index 0 is module 1.</summary>
        public static bool[] Defaults() => All.Select(m => m.Enabled).ToArray();
    }

    /// <summary>A page a pit wall zone can show. The number is the value the zone setting takes.</summary>
    public sealed class ZonePage
    {
        public ZonePage(int number, string id, string name)
        {
            Number = number;
            Id = id;
            Name = name;
        }

        public int Number { get; }

        /// <summary>The module the page draws, or "web" for the browser page, which is no module.</summary>
        public string Id { get; }

        public string Name { get; }

        public override string ToString() => Name;
    }

    public static class ZonePages
    {
        /// <summary>The eleven standard pages, for the 639 px zones.</summary>
        public static readonly IReadOnlyList<ZonePage> Standard = new[]
        {
            new ZonePage(0, "fuel", "Fuel"),
            new ZonePage(1, "tyres", "Tyres"),
            new ZonePage(2, "opponents", "Opponents"),
            new ZonePage(3, "pitView", "Pit view"),
            new ZonePage(4, "relative", "Relative"),
            new ZonePage(5, "leaderboard", "Leaderboard"),
            new ZonePage(6, "lapHistory", "Lap history"),
            new ZonePage(7, "web", "Web view"),
            new ZonePage(8, "inputs", "Inputs"),
            new ZonePage(9, "radar", "Radar"),
            new ZonePage(10, "sectors", "Sectors"),
        };

        /// <summary>The six wide pages, for the zone that spans a column.</summary>
        public static readonly IReadOnlyList<ZonePage> Wide = new[]
        {
            new ZonePage(0, "inputs", "Inputs"),
            new ZonePage(1, "web", "Web view"),
            new ZonePage(2, "lapHistory", "Lap history"),
            new ZonePage(3, "opponents", "Opponents"),
            new ZonePage(4, "tyres", "Tyres"),
            new ZonePage(5, "carTelemetry", "Car telemetry"),
        };

        public static bool IsValidStandard(int number) => number >= 0 && number < Standard.Count;

        public static bool IsValidWide(int number) => number >= 0 && number < Wide.Count;

        public static string StandardName(int number) => IsValidStandard(number) ? Standard[number].Name : ("Page " + number);

        public static string WideName(int number) => IsValidWide(number) ? Wide[number].Name : ("Page " + number);
    }
}
