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
        public void Catalogue_has_twelve_cards_numbered_in_order()
        {
            Assert.Equal(12, Cards.Count);
            Assert.Equal(12, Cards.All.Count);
            Assert.Equal(Enumerable.Range(0, 12), Cards.All.Select(card => card.Number));
            Assert.Equal(12, Cards.All.Select(card => card.Id).Distinct().Count());
            Assert.Equal(12, Cards.All.Select(card => card.DisplayName).Distinct().Count());
            Assert.All(Cards.All, card => Assert.False(string.IsNullOrWhiteSpace(card.Label)));
        }

        [Fact]
        public void Catalogue_ids_match_the_spec()
        {
            var expected = new[]
            {
                "currentLap", "lastLap", "bestLap", "delta", "position", "session",
                "fuel", "fuelLaps", "tc", "abs", "tyreTemps", "tyrePressures",
            };
            Assert.Equal(expected, Cards.All.Select(card => card.Id));
        }

        [Fact]
        public void Card_lookup_is_bounded()
        {
            Assert.True(Cards.IsValidNumber(0));
            Assert.True(Cards.IsValidNumber(11));
            Assert.False(Cards.IsValidNumber(12));
            Assert.False(Cards.IsValidNumber(-1));
            Assert.Null(Cards.ByNumber(12));
            Assert.Equal("Fuel laps", Cards.DisplayName(7));
            Assert.Equal("Card 40", Cards.DisplayName(40));
        }

        [Fact]
        public void Default_slots_show_cards_in_order()
        {
            Assert.Equal(12, Contract.SlotCount);
            Assert.Equal(Enumerable.Range(0, 12).ToArray(), Contract.DefaultSlots());
            Assert.Equal(0, Contract.DefaultCard(1));
            Assert.Equal(11, Contract.DefaultCard(12));
        }

        [Fact]
        public void Property_names_are_the_sixteen_of_the_contract()
        {
            var names = Contract.PropertyNames().ToList();
            Assert.Equal(16, names.Count);
            Assert.Equal(new[] { "ShiftLights", "PositionMode", "DeltaReference", "SessionProgress" }, names.Take(4));
            Assert.Equal("Slot01", Contract.SlotProperty(1));
            Assert.Equal("Slot12", Contract.SlotProperty(12));
            Assert.Equal(Enumerable.Range(1, 12).Select(Contract.SlotProperty), names.Skip(4));
            Assert.Equal("OpenDash", Contract.Prefix);
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

        /// <summary>The single-quoted strings of the array literal that follows the given declaration.</summary>
        private static string[] ListOf(string source, string declaration)
        {
            var match = Regex.Match(source, Regex.Escape(declaration) + @"[^=]*=\s*\[(?<items>[^\]]*)\]");
            Assert.True(match.Success, declaration + " not found in contract.ts");
            return Regex.Matches(match.Groups["items"].Value, "'([^']*)'").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        }
    }
}
