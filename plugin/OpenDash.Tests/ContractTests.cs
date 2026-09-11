// ContractTests.cs: the card catalogue and the property names, and their agreement with contract.ts when present.
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class ContractTests
    {
        [Fact]
        public void Catalogue_has_thirteen_cards_numbered_in_order()
        {
            Assert.Equal(13, Cards.Count);
            Assert.Equal(13, Cards.All.Count);
            Assert.Equal(Enumerable.Range(0, 13), Cards.All.Select(card => card.Number));
            Assert.Equal(13, Cards.All.Select(card => card.Id).Distinct().Count());
            Assert.Equal(13, Cards.All.Select(card => card.DisplayName).Distinct().Count());
            Assert.All(Cards.All, card => Assert.False(string.IsNullOrWhiteSpace(card.Label)));
        }

        [Fact]
        public void Catalogue_ids_match_the_spec()
        {
            var expected = new[]
            {
                "currentLap", "lastLap", "bestLap", "delta", "position", "session",
                "fuel", "fuelLaps", "tc", "abs", "tyreTemps", "tyrePressures", "speed",
            };
            Assert.Equal(expected, Cards.All.Select(card => card.Id));
        }

        [Fact]
        public void Card_lookup_is_bounded()
        {
            Assert.True(Cards.IsValidNumber(0));
            Assert.True(Cards.IsValidNumber(12));
            Assert.False(Cards.IsValidNumber(13));
            Assert.False(Cards.IsValidNumber(-1));
            Assert.Null(Cards.ByNumber(13));
            Assert.Equal("Fuel laps", Cards.DisplayName(7));
            Assert.Equal("Card 40", Cards.DisplayName(40));
        }

        [Fact]
        public void Default_slots_lead_with_speed_then_the_timing_block()
        {
            Assert.Equal(12, Contract.SlotCount);
            Assert.Equal(new[] { 12, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, Contract.DefaultSlots());
            // Speed first, so a two-slot round face still shows it beside the gear.
            Assert.Equal(12, Contract.DefaultCard(1));
            Assert.Equal(10, Contract.DefaultCard(12));
            // Tyre pressures is the one card no slot shows by default.
            Assert.DoesNotContain(11, Contract.DefaultSlots());
        }

        [Fact]
        public void Property_names_cover_the_dash_the_companion_and_the_pit_wall()
        {
            var names = Contract.PropertyNames().ToList();
            // Four settings, twelve slots, twenty-one companion modules, four zones, the wide zone and the URL.
            Assert.Equal(4 + 12 + 21 + 4 + 2, names.Count);
            Assert.Equal(names.Count, names.Distinct().Count());
            Assert.Equal(new[] { "ShiftLights", "PositionMode", "DeltaReference", "SessionProgress" }, names.Take(4));
            Assert.Equal("Slot01", Contract.SlotProperty(1));
            Assert.Equal("Slot12", Contract.SlotProperty(12));
            Assert.Equal(Enumerable.Range(1, 12).Select(Contract.SlotProperty), names.Skip(4).Take(12));
            Assert.Equal("CompanionModule01", Contract.ModuleProperty(1));
            Assert.Equal("CompanionModule21", Contract.ModuleProperty(21));
            Assert.Equal(Enumerable.Range(1, 21).Select(Contract.ModuleProperty), names.Skip(16).Take(21));
            Assert.Equal(new[] { "PitWallZoneA", "PitWallZoneB", "PitWallZoneC", "PitWallZoneD", "PitWallWide", "WebViewUrl" }, names.Skip(37));
            Assert.Equal("OpenDash", Contract.Prefix);
        }

        [Fact]
        public void Module_catalogue_has_twenty_one_pages_three_of_them_off()
        {
            Assert.Equal(21, Modules.Count);
            Assert.Equal(21, Modules.All.Count);
            Assert.Equal(Enumerable.Range(1, 21), Modules.All.Select(m => m.Number));
            Assert.Equal(21, Modules.All.Select(m => m.Id).Distinct().Count());
            Assert.All(Modules.All, m => Assert.False(string.IsNullOrWhiteSpace(m.Description)));
            // The three iRacing cannot fill: virtual energy, damage and segment rivals.
            Assert.Equal(new[] { "energy", "damage", "trackRivals" }, Modules.All.Where(m => !m.Enabled).Select(m => m.Id));
            Assert.Equal("Gear", Modules.DisplayName(17));
            Assert.Equal("Module 22", Modules.DisplayName(22));
            Assert.Null(Modules.ByNumber(0));
        }

        [Fact]
        public void Zone_pages_and_their_defaults_match_the_contract()
        {
            Assert.Equal(11, ZonePages.Standard.Count);
            Assert.Equal(6, ZonePages.Wide.Count);
            Assert.Equal(Enumerable.Range(0, 11), ZonePages.Standard.Select(p => p.Number));
            Assert.Equal(Enumerable.Range(0, 6), ZonePages.Wide.Select(p => p.Number));
            Assert.Equal(new[] { "A", "B", "C", "D" }, Contract.ZoneLetters);
            Assert.Equal(new[] { 0, 1, 4, 2 }, Contract.DefaultZones());
            Assert.Equal("Fuel", ZonePages.StandardName(Contract.DefaultZonePage("A")));
            Assert.Equal("Relative", ZonePages.StandardName(Contract.DefaultZonePage("C")));
            Assert.Equal("Car telemetry", ZonePages.WideName(Contract.DefaultWideZonePage));
            Assert.Equal(3, Contract.NormaliseZonePage(3, 0));
            Assert.Equal(0, Contract.NormaliseZonePage(11, 0));
            Assert.Equal(5, Contract.NormaliseWideZonePage(6));
        }

        [Fact]
        public void A_web_view_address_is_kept_only_when_it_is_an_http_url()
        {
            Assert.Equal("https://garage61.net", Contract.NormaliseUrl("  https://garage61.net  "));
            Assert.Equal("http://localhost:8080/timing", Contract.NormaliseUrl("http://localhost:8080/timing"));
            Assert.Equal("", Contract.NormaliseUrl("file:///C:/secrets.txt"));
            Assert.Equal("", Contract.NormaliseUrl("javascript:alert(1)"));
            Assert.Equal("", Contract.NormaliseUrl("not a url"));
            Assert.Equal("", Contract.NormaliseUrl(null));
        }

        [Fact]
        public void Value_sets_match_the_contract()
        {
            Assert.Equal(new[] { "overall", "class" }, Contract.PositionModes);
            Assert.Equal(new[] { "session", "alltime" }, Contract.DeltaReferences);
            Assert.Equal(new[] { "auto", "laps", "time" }, Contract.SessionProgressModes);
            Assert.Equal("alltime", Contract.NormaliseChoice("AllTime", Contract.DeltaReferences, "session"));
            Assert.Equal("session", Contract.NormaliseChoice("never", Contract.DeltaReferences, "session"));
        }

        /// <summary>The dash package's contract.ts is the other half of the contract. When it exists, its
        /// CARD_CATALOGUE rows, SLOT_MAX, fixed property names, value sets and defaults must equal the C# side.</summary>
        [Fact]
        public void Catalogue_agrees_with_contract_ts_when_present()
        {
            var path = RepoPaths.ContractTs();
            if (!File.Exists(path)) return; // the dash package is built separately; nothing to compare yet
            var source = File.ReadAllText(path);

            var rows = Regex.Matches(source,
                @"\{\s*number:\s*(?<number>\d+),\s*id:\s*'(?<id>[^']*)',\s*label:\s*'(?<label>[^']*)',\s*displayName:\s*'(?<name>[^']*)'\s*\}");
            Assert.Equal(Cards.All.Count, rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                var card = Cards.All[i];
                Assert.Equal(card.Number, int.Parse(rows[i].Groups["number"].Value, CultureInfo.InvariantCulture));
                Assert.Equal(card.Id, rows[i].Groups["id"].Value);
                Assert.Equal(card.Label, rows[i].Groups["label"].Value);
                Assert.Equal(card.DisplayName, rows[i].Groups["name"].Value);
            }

            Assert.Equal(Contract.SlotCount, int.Parse(Regex.Match(source, @"SLOT_MAX\s*=\s*(\d+)").Groups[1].Value, CultureInfo.InvariantCulture));
            Assert.Equal("'" + Contract.Prefix + "'", Regex.Match(source, @"PROPERTY_PREFIX\s*=\s*('[^']*')").Groups[1].Value);
            Assert.Equal(Contract.PropertyNames().Take(4), ListOf(source, "const fixed"));
            Assert.Equal(Contract.PositionModes, ListOf(source, "POSITION_MODES"));
            Assert.Equal(Contract.DeltaReferences, ListOf(source, "DELTA_REFERENCES"));
            Assert.Equal(Contract.SessionProgressModes, ListOf(source, "SESSION_PROGRESS_MODES"));
            Assert.Contains("ShiftLights: " + Contract.DefaultShiftLights.ToString().ToLowerInvariant(), source);
            Assert.Contains("PositionMode: '" + Contract.DefaultPositionMode + "'", source);
            Assert.Contains("DeltaReference: '" + Contract.DefaultDeltaReference + "'", source);
            Assert.Contains("SessionProgress: '" + Contract.DefaultSessionProgress + "'", source);
        }

        /// <summary>The module catalogue and the zone pages are the other half of the second-screen contract.</summary>
        [Fact]
        public void Modules_and_zone_pages_agree_with_contract_ts_when_present()
        {
            var path = RepoPaths.ContractTs();
            if (!File.Exists(path)) return;
            var source = File.ReadAllText(path);

            var rows = Regex.Matches(source,
                @"\{\s*number:\s*(?<number>\d+),\s*id:\s*'(?<id>[^']*)',\s*name:\s*'(?<name>[^']*)',\s*description:\s*'(?<description>[^']*)',\s*enabled:\s*(?<enabled>true|false)\s*\}");
            Assert.Equal(Modules.All.Count, rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                var module = Modules.All[i];
                Assert.Equal(module.Number, int.Parse(rows[i].Groups["number"].Value, CultureInfo.InvariantCulture));
                Assert.Equal(module.Id, rows[i].Groups["id"].Value);
                Assert.Equal(module.Name, rows[i].Groups["name"].Value);
                Assert.Equal(module.Description, rows[i].Groups["description"].Value);
                Assert.Equal(module.Enabled, bool.Parse(rows[i].Groups["enabled"].Value));
            }

            Assert.Equal(ZonePages.Standard.Select(p => p.Id), PageIdsOf(source, "export const ZONE_PAGES"));
            Assert.Equal(ZonePages.Wide.Select(p => p.Id), PageIdsOf(source, "export const WIDE_ZONE_PAGES"));
            Assert.Contains("DEFAULT_WIDE_ZONE_PAGE = " + Contract.DefaultWideZonePage, source);
            Assert.Contains("{ A: 0, B: 1, C: 4, D: 2 }", source);
        }

        /// <summary>The `id` fields of the page list that follows the given declaration.</summary>
        private static string[] PageIdsOf(string source, string declaration)
        {
            var match = Regex.Match(source, Regex.Escape(declaration) + @"[^=]*=\s*\[(?<items>[^\]]*)\]");
            Assert.True(match.Success, declaration + " not found in contract.ts");
            return Regex.Matches(match.Groups["items"].Value, @"id:\s*'([^']*)'").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        }

        /// <summary>The single-quoted strings of the array literal that follows the given declaration.</summary>
        private static string[] ListOf(string source, string declaration)
        {
            var match = Regex.Match(source, Regex.Escape(declaration) + @"[^=]*=\s*\[(?<items>[^\]]*)\]");
            Assert.True(match.Success, declaration + " not found in contract.ts");
            return Regex.Matches(match.Groups["items"].Value, "'([^']*)'").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        }
    }
}
