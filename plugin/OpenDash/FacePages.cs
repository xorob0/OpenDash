// FacePages.cs: what each zone of the dash face can show, mirroring ZONE_A_PAGES, BAND_D_PAGES and
// BAR_FIELDS in packages/dash/src/contract.ts. A test compares the two files, so keep one entry per
// line. Zones B and C draw from the module catalogue in Modules.cs rather than a list of their own.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    public static class FacePages
    {
        /// <summary>Zone A's four pages. The one a driver reads by reflex, which is why the zone is a
        /// narrow column: the gear wants height, not width.</summary>
        public static readonly IReadOnlyList<ZonePage> ZoneA = new[]
        {
            new ZonePage(0, "gearSpeedRevs", "Gear, speed, revs"),
            new ZonePage(1, "gearAlone", "Gear alone"),
            new ZonePage(2, "speed", "Speed"),
            new ZonePage(3, "track", "Track"),
        };

        /// <summary>Band D's eight pages, across the foot of the face.</summary>
        public static readonly IReadOnlyList<ZonePage> BandD = new[]
        {
            new ZonePage(0, "fuel", "Fuel"),
            new ZonePage(1, "energy", "Energy"),
            new ZonePage(2, "stint", "Stint"),
            new ZonePage(3, "tyres", "Tyres"),
            new ZonePage(4, "weather", "Weather"),
            new ZonePage(5, "sectors", "Sectors"),
            new ZonePage(6, "relative", "Relative"),
            new ZonePage(7, "car", "Car"),
        };

        /// <summary>The ten fields an end of the bar can show. Strength of field is not offered: SimHub
        /// publishes it in no form, and ADR 0009 decided a field that can never have a value is not listed.</summary>
        public static readonly IReadOnlyList<ZonePage> BarFields = new[]
        {
            new ZonePage(0, "raceTime", "Race time"),
            new ZonePage(1, "lap", "Lap"),
            new ZonePage(2, "timeLeft", "Time left"),
            new ZonePage(3, "clock", "Clock"),
            new ZonePage(4, "simulatedTime", "Simulated time"),
            new ZonePage(5, "position", "Position"),
            new ZonePage(6, "classPosition", "Class"),
            new ZonePage(7, "incidents", "Incidents"),
            new ZonePage(8, "airTemp", "Air temperature"),
            new ZonePage(9, "trackTemp", "Track temperature"),
        };

        /// <summary>Zones B and C draw from the full module catalogue, which is no longer companion-only.
        /// The page number is the module number less one, because a zone setting counts from zero.</summary>
        public static readonly IReadOnlyList<ZonePage> ZoneBC =
            Modules.All.Select(m => new ZonePage(m.Number - 1, m.Id, m.Name)).ToArray();

        /// <summary>The catalogue a zone draws from, by its letter.</summary>
        public static IReadOnlyList<ZonePage> For(string letter)
        {
            if (letter == "A") return ZoneA;
            if (letter == "D") return BandD;
            if (letter == "B" || letter == "C") return ZoneBC;
            throw new ArgumentOutOfRangeException(nameof(letter));
        }

        /// <summary>
        /// Whether a zone's catalogue holds a page the class filter changes, which is what decides
        /// whether the panel offers the control at all.
        ///
        /// Zones B and C: the leaderboard and the relative are the two pages that list other cars as
        /// a table, and both are in the module catalogue. Zone A lists nobody. Band D's own relative
        /// page is three gaps rather than a list, so filtering it would mean asking for the car ahead
        /// in class rather than listing fewer of them -- the same idea, a different change, and not
        /// this one.
        /// </summary>
        public static bool OffersClassFilter(string letter)
        {
            return letter == "B" || letter == "C";
        }

        /// <summary>How many pages a zone can show, by its index in Contract.FaceZoneLetters.</summary>
        public static int CountAt(int zoneIndex)
        {
            if (zoneIndex < 0 || zoneIndex >= Contract.FaceZoneLetters.Length) throw new ArgumentOutOfRangeException(nameof(zoneIndex));
            return For(Contract.FaceZoneLetters[zoneIndex]).Count;
        }

        /// <summary>The name of a page of a zone, or "Page n" when the number is outside the catalogue.</summary>
        public static string NameOf(string letter, int page)
        {
            var pages = For(letter);
            return page >= 0 && page < pages.Count ? pages[page].Name : ("Page " + page);
        }

        /// <summary>The id of a page of a zone, or null when the number is outside the catalogue. Two
        /// zones showing the same id are showing the same page even when their catalogues differ:
        /// zone A's track page and module 13 are one drawing.</summary>
        public static string IdOf(string letter, int page)
        {
            var pages = For(letter);
            return page >= 0 && page < pages.Count ? pages[page].Id : null;
        }

        /// <summary>The name of a bar field, or "Field n" when the number is outside the catalogue.</summary>
        public static string FieldName(int field)
        {
            return field >= 0 && field < BarFields.Count ? BarFields[field].Name : ("Field " + field);
        }

        /// <summary>"Race time · Lap": what an end of the bar shows, as the panel labels it.</summary>
        public static string EndLabel(string first, string second)
        {
            return second == null ? first : first + " · " + second;
        }
    }
}
