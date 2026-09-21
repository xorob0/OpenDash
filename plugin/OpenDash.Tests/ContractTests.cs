// ContractTests.cs: the card catalogue and the property names, and their agreement with contract.ts when present.
using System;
using System.Collections.Generic;
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
            // Four settings, twelve slots, the rev bar mode, the blue flag detail, the zone face of
            // every face that ships
            // (four pages, four masks, four starts, four class filters, four bar fields, the glance,
            // the flag format, the lap review and its own rev bar), twenty-one companion modules,
            // every zone of every pit wall page, the page it opens on and the page it is showing,
            // the URL, the pit wall's class filter, its flag format, and the flag box.
            const int perFace = 4 + 4 + 4 + 4 + 4 + 1 + 1 + 1 + 1;
            // Six global flag box settings and eleven per matrix, the way every face carries its own
            // group, and then the three the strips read. It was nine and six until critical flags
            // only, the gear and the two temperature thresholds moved under the matrix that owns them,
            // and the switch on the spotter bar's movement joined the rig's own names; the eleventh is
            // the switch on the digit's redline flash, which is a panel's own for the same reason.
            Assert.Equal(
                4 + 12 + 2 + Contract.FaceSizes.Count * perFace + 21 + 3 + Contract.PitWallZoneSlots.Count + 4 + 6 + Contract.FlagBoxMatrices.Count * 11 + Contract.LedPropertyNames().Count(),
                names.Count);
            // And what that sum comes to, said out loud: contract.test.ts asserts the same number of
            // the TypeScript's own list, and the two were 244 and 246 for as long as LedCentre and
            // LedRpmStyle were declared by one side only. 256 before the four settings a box owns
            // became four per matrix, which is twelve names more, 269 before the pit wall gained the
            // class filter its board and its list zones read, 270 before band D was allowed to name the
            // car a blue flag is being waved for, 271 before each face was given its own answer to when
            // the lap review is shown, 279 before the companion's page became the plugin's, 280
            // before the mirror brought its fit, its gate and one packed run per length a centre can
            // be, 292 before each matrix was given its own answer to whether the digit flashes
            // through the redline, 296 before each face was given its own answer to what it carries at
            // the top, 304 before the strip shapes became a grid and the mirror had to publish a
            // run for every centre the grid reaches, and 317 before a pit wall zone belonged to a page:
            // four zones and a wide one became twelve, and the pit wall gained the page it opens on and
            // the page it shows, 325 before a companion was given its own answer to how it draws a
            // flag, and 327 before the pit wall was given the same one and the flag readout came off
            // its header.
            // 329 with #369's LedCarRevBar, added beside the style it deprecates rather than instead of it.
            Assert.Equal(329, names.Count);
            Assert.Equal(names.Count, names.Distinct().Count());
            Assert.Equal(new[] { "ShiftLights", "PositionMode", "DeltaReference", "SessionProgress" }, names.Take(4));
            Assert.Equal("Slot01", Contract.SlotProperty(1));
            Assert.Equal("Slot12", Contract.SlotProperty(12));
            Assert.Equal(Enumerable.Range(1, 12).Select(Contract.SlotProperty), names.Skip(4).Take(12));

            // The zone face, declared beside the slots rather than instead of them: ten faces still
            // read Slot01 to Slot12, and README publishes them as properties an LED profile may read.
            // The zone face's groups follow, one per face that ships, each naming its own screen so
            // that two faces on a rig are configured apart.
            // Appended to the shared group, not inserted beside ShiftLights: the four names above and
            // the twelve slots have shipped and this test asserts them by index. #170, #189.
            Assert.Equal("RevBar", names[16]);
            Assert.Contains("RevBar", Contract.SharedPropertyNames());
            // And the blue flag detail after it, shared for a different reason: the flag format is
            // the screen's because it decides how much of one screen a flag takes, whereas what a
            // band may say is the same answer wherever it is written.
            Assert.Equal("BlueFlagDetail", names[17]);
            Assert.Contains("BlueFlagDetail", Contract.SharedPropertyNames());

            var p = Contract.FacePrefix(Contract.ReferenceFace);
            Assert.Equal(new[] { p + "ZoneA", p + "ZoneB", p + "ZoneC", p + "ZoneD" }, names.Skip(18).Take(4));
            Assert.Equal(new[] { p + "ZoneAPages", p + "ZoneBPages", p + "ZoneCPages", p + "ZoneDPages" }, names.Skip(22).Take(4));
            Assert.Equal(new[] { p + "ZoneAStart", p + "ZoneBStart", p + "ZoneCStart", p + "ZoneDStart" }, names.Skip(26).Take(4));
            Assert.Equal(new[] { p + "ZoneAClassOnly", p + "ZoneBClassOnly", p + "ZoneCClassOnly", p + "ZoneDClassOnly" }, names.Skip(30).Take(4));
            Assert.Equal(new[] { p + "BarLeft1", p + "BarLeft2", p + "BarRight1", p + "BarRight2" }, names.Skip(34).Take(4));
            Assert.Equal(p + "QuickGlance", names[38]);
            Assert.Equal(p + "FlagFormat", names[39]);
            Assert.Equal(p + "LapReview", names[40]);
            // And no name without a face, which is the promise: a bare ZoneA would be one screen's
            // settings silently shared with every other.
            Assert.DoesNotContain(names, n => n.StartsWith("Zone", StringComparison.Ordinal) && !n.StartsWith("Face", StringComparison.Ordinal));

            var afterFaces = 18 + Contract.FaceSizes.Count * perFace;
            Assert.Equal("CompanionModule01", Contract.ModuleProperty(1));
            Assert.Equal("CompanionModule21", Contract.ModuleProperty(21));
            Assert.Equal(Enumerable.Range(1, 21).Select(Contract.ModuleProperty), names.Skip(afterFaces).Take(21));
            // The page after the switches: live state the screens' enabled expressions follow, and the
            // one companion name that is not a switch. The start and the glance are not properties.
            Assert.Equal("CompanionPage", names[afterFaces + 21]);
            Assert.Equal("CompanionFlagFormat", names[afterFaces + 22]);
            Assert.Equal("CompanionOpenOn", names[afterFaces + 23]);
            Assert.Equal(new[] { "PitWallRaceA", "PitWallRaceB", "PitWallTowerWide", "PitWallTowerA", "PitWallTowerB", "PitWallTelemetryA", "PitWallTelemetryB", "PitWallTelemetryC", "PitWallPortraitA", "PitWallPortraitB", "PitWallPortraitC", "PitWallPortraitD", "PitWallPage", "WebViewUrl", "PitWallClassOnly", "PitWallFlagFormat", "LightsBrightness", "LightsNightBrightness",
                "LightsNightMode", "FlagBoxLowFuelLaps", "LightsLowFuelLaps", "FlagBoxSpotterAnimation" }, names.Skip(afterFaces + 24).Take(22));
            // One filter for the screen, not one per zone: a pit wall zone is a widget pointed at one
            // dashboard file per rectangle, so zones A and B of the race page are the same file. The
            // page they belong to is what tells them apart now, and that is a different question.
            Assert.False(Contract.DefaultPitWallClassOnly);
            Assert.Equal("PitWallClassOnly", Contract.PitWallClassOnlyProperty(Contract.PitWallPrefix));
            Assert.Equal("GarageClassOnly", Contract.PitWallClassOnlyProperty("Garage"));
            Assert.Single(names.Where(n => n.EndsWith("PitWallClassOnly", StringComparison.Ordinal)));
            // The companion's three answers, asked of the wall, and defaulting to the band rather than
            // the companion's full screen: a wall is watched because of the flag, so covering it is the
            // one thing the flag must not do.
            Assert.Equal("band", Contract.DefaultPitWallFlagFormat);
            Assert.Equal(new[] { "off", "band", "full" }, Contract.CompanionFlagFormats);
            Assert.Equal("PitWallFlagFormat", Contract.PitWallFlagFormatProperty(Contract.PitWallPrefix));
            Assert.Equal("GarageFlagFormat", Contract.PitWallFlagFormatProperty("Garage"));
            Assert.Equal("band", Contract.NormalisePitWallFlagFormat(null));
            Assert.Equal("band", Contract.NormalisePitWallFlagFormat("sideways"));
            Assert.Equal("full", Contract.NormalisePitWallFlagFormat("full"));
            Assert.Equal("OpenDash", Contract.Prefix);
        }

        [Fact]
        public void Every_property_belongs_to_one_screen_or_to_every_screen()
        {
            // The rule the build enforces over a package is a partition of the contract: a name is one
            // screen's or it is shared by all of them, and never both. Without that, attaching a rig's
            // properties would either drop a name no screen claims or attach one twice.
            //
            // The lights are a third part of that partition rather than an exception to it. They belong
            // to no screen -- a matrix is not one -- and they are not the screens' shared settings
            // either, so folding them into SharedPropertyNames would make "shared" mean two things.
            var all = Contract.PropertyNames().ToList();
            var owned = Contract.ScreenPrefixes().SelectMany(Contract.ScreenPropertyNames).ToList();
            var lights = Contract.LightsPropertyNames().ToList();
            Assert.Equal(owned.Count, owned.Distinct().Count());
            Assert.Empty(owned.Except(all));
            Assert.Empty(lights.Except(all));
            Assert.Empty(lights.Intersect(owned));
            Assert.Empty(lights.Intersect(Contract.SharedPropertyNames()));
            Assert.Equal(Contract.SharedPropertyNames(), all.Except(owned).Except(lights));

            // The web view address is the pit wall's although its name carries no prefix: it was named
            // before the idiom, and no other screen has a browser page to point anywhere.
            Assert.Contains(Contract.WebViewUrl, Contract.ScreenPropertyNames(Contract.PitWallPrefix));
            // The twenty-one switches, the page and the flag format. The start module and the glance
            // module are the plugin's own state and not properties, because nothing on the screen reads
            // either of them.
            Assert.Equal(Modules.Count + 3, Contract.ScreenPropertyNames(Contract.CompanionPrefix).Count());
            Assert.Equal("CompanionOpenOn", Contract.ScreenPropertyNames(Contract.CompanionPrefix).Last());
            Assert.Equal(Contract.FacePropertyNames(Contract.ReferenceFace), Contract.ScreenPropertyNames(Contract.FacePrefix(Contract.ReferenceFace)));

            Assert.True(Contract.IsKnownScreen(Contract.FacePrefix(Contract.ReferenceFace)));
            Assert.False(Contract.IsKnownScreen("Face1x1"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Contract.ScreenPropertyNames("Face1x1").ToList());
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
            Assert.Equal(new[] { "A", "B", "C", "D" }, Contract.PitWallZoneLetters);
            Assert.Equal(new[] { 0, 1, 4, 2 }, Contract.PitWallDefaultZones());
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
            Assert.Equal(Contract.RevBarModes, ListOf(source, "REV_BAR_MODES"));
            Assert.Equal(Contract.BlueFlagDetails, ListOf(source, "BLUE_FLAG_DETAILS"));
            Assert.Equal(Contract.LapReviewModes, ListOf(source, "LAP_REVIEW_MODES"));
            Assert.Contains("BlueFlagDetail: '" + Contract.DefaultBlueFlagDetail + "'", source);
            Assert.Equal(Contract.LedCentres, ListOf(source, "LED_CENTRES"));
            Assert.Equal(Contract.LedRpmStyles, ListOf(source, "LED_RPM_STYLES"));
            Assert.Contains("LedCentre: '" + Contract.DefaultLedCentre + "'", source);
            Assert.Contains("LedRpmStyle: '" + Contract.DefaultLedRpmStyle + "'", source);
            Assert.Contains("RevBar: '" + Contract.DefaultRevBar + "'", source);
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

            Assert.Equal(ZonePages.Standard.Select(p => p.Id), PageIdsOf(source, "export const PIT_WALL_ZONE_PAGES"));
            Assert.Equal(ZonePages.Wide.Select(p => p.Id), PageIdsOf(source, "export const PIT_WALL_WIDE_ZONE_PAGES"));
            foreach (var slot in Contract.PitWallZoneSlots)
            {
                Assert.Contains("{ slot: '" + slot.Slot + "', kind: '" + (slot.Wide ? "wide" : "standard") + "', fallback: " + slot.Fallback + " }", source);
            }
        }

        /// <summary>The zone face's half of the contract, checked against contract.ts the same way the
        /// cards are: the two files are one contract and neither is allowed to move alone.</summary>
        [Fact]
        public void Zone_face_agrees_with_contract_ts_when_present()
        {
            var path = RepoPaths.ContractTs();
            if (!File.Exists(path)) return; // the dash package is built separately; nothing to compare yet
            var source = File.ReadAllText(path);

            Assert.Equal(Contract.FaceZoneLetters, ListOf(source, "export const FACE_ZONE_LETTERS"));
            Assert.Equal(Contract.BarSlots, ListOf(source, "export const BAR_SLOTS"));

            // The page counts decide the mask width, so a catalogue that grows on one side and not
            // the other would silently shorten a driver's cycle.
            Assert.Equal(4, PageIdsOf(source, "export const ZONE_A_PAGES").Length);
            Assert.Equal(8, PageIdsOf(source, "export const BAND_D_PAGES").Length);
            Assert.Equal(Contract.BarFieldCount, PageIdsOf(source, "export const BAR_FIELDS").Length);
            Assert.Equal(Contract.FaceZonePageCounts[0], PageIdsOf(source, "export const ZONE_A_PAGES").Length);
            Assert.Equal(Contract.FaceZonePageCounts[3], PageIdsOf(source, "export const BAND_D_PAGES").Length);
            Assert.Equal(Modules.Count, Contract.FaceZonePageCounts[1]);
            Assert.Equal(Modules.Count, Contract.FaceZonePageCounts[2]);

            Assert.Contains("DEFAULT_ZONE_PAGE: Record<FaceZone, number> = { A: 0, B: 0, C: 14, D: 0 }", source);
            Assert.Contains("DEFAULT_BAR_FIELDS: Record<BarSlot, number> = { Left1: 0, Left2: 1, Right1: 5, Right2: 6 }", source);
            Assert.Contains("DEFAULT_QUICK_GLANCE = " + (Contract.DefaultQuickGlance / 100) + " * 100 + " + (Contract.DefaultQuickGlance % 100), source);
        }

        /// <summary>The page names the panel lists, against the names the face draws. Ids alone are not
        /// enough here: the panel is the only place a driver ever reads these, so a name that drifts is a
        /// dropdown that does not say what the zone will show.</summary>
        [Fact]
        public void Face_page_catalogues_agree_with_contract_ts_when_present()
        {
            var path = RepoPaths.ContractTs();
            if (!File.Exists(path)) return;
            var source = File.ReadAllText(path);

            Assert.Equal(PageIdsOf(source, "export const ZONE_A_PAGES"), FacePages.ZoneA.Select(p => p.Id));
            Assert.Equal(PageNamesOf(source, "export const ZONE_A_PAGES"), FacePages.ZoneA.Select(p => p.Name));
            Assert.Equal(PageIdsOf(source, "export const BAND_D_PAGES"), FacePages.BandD.Select(p => p.Id));
            Assert.Equal(PageNamesOf(source, "export const BAND_D_PAGES"), FacePages.BandD.Select(p => p.Name));
            Assert.Equal(PageIdsOf(source, "export const BAR_FIELDS"), FacePages.BarFields.Select(p => p.Id));
            Assert.Equal(PageNamesOf(source, "export const BAR_FIELDS"), FacePages.BarFields.Select(p => p.Name));

            // Each catalogue numbers itself from zero, because a zone setting is an index into it.
            for (var i = 0; i < Contract.FaceZoneLetters.Length; i++)
            {
                var pages = FacePages.For(Contract.FaceZoneLetters[i]);
                Assert.Equal(Contract.FaceZonePageCounts[i], pages.Count);
                for (var page = 0; page < pages.Count; page++) Assert.Equal(page, pages[page].Number);
            }
            // Zones B and C are the module catalogue less one, which is what makes module 15 page 14.
            Assert.Equal("relative", FacePages.IdOf("C", Contract.DefaultFaceZonePages[2]));
            Assert.Equal(Contract.BarFieldCount, FacePages.BarFields.Count);
        }

        [Fact]
        public void Zone_defaults_are_every_page_enabled()
        {
            // A driver turns off what they do not want rather than turning on what they do, so every
            // bit of the mask is set and the cycle starts at its longest.
            for (var i = 0; i < Contract.FaceZoneLetters.Length; i++)
            {
                var mask = Contract.DefaultZoneMask(i);
                var pages = Contract.FaceZonePageCounts[i];
                Assert.Equal((1 << pages) - 1, mask);
                for (var page = 0; page < pages; page++) Assert.True((mask & (1 << page)) != 0);
                Assert.Equal(0, mask >> pages);
            }
            Assert.Equal(new[] { 0, 0, 14, 0 }, Contract.DefaultFaceZones());
            Assert.Equal(new[] { 0, 1, 5, 6 }, Contract.DefaultBarSlots());
        }

        [Fact]
        public void Zone_property_names_carry_their_face_and_refuse_a_letter_that_is_not_a_zone()
        {
            var face = Contract.ReferenceFace;
            Assert.Equal("Face1920x480ZoneB", Contract.ZonePageProperty(face, "B"));
            Assert.Equal("Face1920x480ZoneBPages", Contract.ZoneMaskProperty(face, "B"));
            Assert.Equal("Face1920x480ZoneBStart", Contract.ZoneStartProperty(face, "B"));
            Assert.Equal("Face1920x480ZoneBClassOnly", Contract.ZoneClassOnlyProperty(face, "B"));
            Assert.Equal("Face1920x480BarRight2", Contract.BarFieldProperty(face, "Right2"));
            Assert.Equal("Face1920x480QuickGlance", Contract.QuickGlanceProperty(face));
            // Per screen like the zones, so a rig can take a flag over the whole of one face and leave
            // the other's band alone.
            Assert.Equal("Face1920x480FlagFormat", Contract.FlagFormatProperty(face));
            Assert.Equal(new[] { "band", "full" }, Contract.FlagFormats);
            Assert.Equal("band", Contract.DefaultFlagFormat);
            Assert.Contains(Contract.DefaultFlagFormat, Contract.FlagFormats);
            Assert.Throws<ArgumentOutOfRangeException>(() => Contract.ZonePageProperty(face, "E"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Contract.BarFieldProperty(face, "Middle"));
        }

        [Fact]
        public void The_flag_box_is_declared_last_and_clamps_its_brightness()
        {
            // After the screens, because the lights are the artefacts the plugin does not install
            // (ADR 0013); declared at all because a profile reads them, and an undeclared read fails
            // the dash build. The strips follow the matrices, so the flag box's last name is the last
            // before them rather than the last of all.
            Assert.Equal("FlagBoxMatrix4GearBlink", Contract.PropertyNames().Except(Contract.LedPropertyNames()).Last());
            // One threshold for the strip, the rev bar and the box, under the Lights* name; the box's
            // own name stays attached as its deprecated alias, which is what a profile of the rc.2
            // vintage reads and what the contract's second isnull() falls back to.
            Assert.Contains(Contract.LightsLowFuelLaps, Contract.LightsPropertyNames());
            // The spotter bar's movement is the rig's rather than a box's, and off by default: on this
            // box movement means act, and a car alongside informs.
            Assert.Contains(Contract.FlagBoxSpotterAnimation, Contract.LightsPropertyNames());
            Assert.False(Contract.DefaultFlagBoxSpotterAnimation);
            Assert.Contains(Contract.FlagBoxLowFuelLaps, Contract.LightsPropertyNames());
            // The head of the strips' own group is pinned in order, because both halves of the
            // contract assert it by index; the mirror's runs are declared last of all, after the two
            // settings a strip profile reads, the switch on a flag's movement, and the fit and the gate.
            Assert.Equal(
                new[] { "LedCentre", "LedRpmStyle", "LedCarRevBar", "LedFlagAnimation", "LedMirrorFit" },
                Contract.LedPropertyNames().Take(5));
            Assert.Equal(Contract.LedSpotterWhole, Contract.PropertyNames().Last());
            Assert.True(Contract.DefaultFlagBoxGear);
            // Matrix 1 does everything, 2 to 4 are off: one box works out of the box.
            Assert.True(Contract.DefaultFlagBoxMatrixOn(1));
            Assert.False(Contract.DefaultFlagBoxMatrixOn(2));
            Assert.Equal("gear", Contract.DefaultFlagBoxMatrixRest(1));
            Assert.Equal("dark", Contract.DefaultFlagBoxMatrixRest(4));
            Assert.Equal("both", Contract.DefaultFlagBoxSide);
            Assert.Equal("FlagBoxMatrix2Spotter", Contract.FlagBoxMatrixProperty(2, "Spotter"));
            // The four settings a box owns are that box's, so a rig with one in each corner can show
            // the whole catalogue on one and the gear alone on the other.
            Assert.Equal("FlagBoxMatrix3CriticalOnly", Contract.FlagBoxMatrixProperty(3, "CriticalOnly"));
            foreach (var matrix in Contract.FlagBoxMatrices)
            {
                var own = Contract.FlagBoxMatrixProperties(matrix).ToList();
                Assert.Contains(Contract.FlagBoxMatrixProperty(matrix, "CriticalOnly"), own);
                Assert.Contains(Contract.FlagBoxMatrixProperty(matrix, "Gear"), own);
                Assert.Contains(Contract.FlagBoxMatrixProperty(matrix, "OilTemp"), own);
                Assert.Contains(Contract.FlagBoxMatrixProperty(matrix, "WaterTemp"), own);
            }
            // ...and the names they replaced are gone from the attached list, which is what a
            // migration rather than an alias means.
            foreach (var gone in new[] { "FlagBoxCriticalOnly", "FlagBoxGear", "FlagBoxOilTemp", "FlagBoxWaterTemp" })
            {
                Assert.DoesNotContain(gone, Contract.PropertyNames());
            }
            // Pit is its own switch, not the flags'. A driver who silences flags on a panel has not
            // asked to lose the pit limiter warning with them.
            Assert.Contains("FlagBoxMatrix1Pit", Contract.PropertyNames());
            Assert.Throws<ArgumentOutOfRangeException>(() => Contract.FlagBoxMatrixProperty(5, "Rest"));
            // Rotation and serpentine are SimHub device settings, not ours.
            foreach (var name in Contract.PropertyNames())
            {
                Assert.DoesNotContain("Rotation", name, StringComparison.Ordinal);
                Assert.DoesNotContain("Serpentine", name, StringComparison.Ordinal);
            }
            // The defaults are right in every unit SimHub reports, not only in Celsius.
            Assert.Equal(120, Contract.DefaultOilTemp["Celcius"]);
            Assert.Equal(248, Contract.DefaultOilTemp["Fahrenheit"]);
            Assert.Equal(110, Contract.DefaultWaterTemp["Celcius"]);
            Assert.Equal(2, Contract.DefaultFlagBoxLowFuelLaps);
            Assert.Equal(100, Contract.DefaultLightsBrightness);
            // Dimmer at night: 64 LEDs at full output beside a wheel in a dark room is too bright.
            Assert.True(Contract.DefaultLightsNightBrightness < Contract.DefaultLightsBrightness);
            Assert.False(Contract.DefaultLightsNightMode);
            // Off: the box shows the whole catalogue until the driver asks for quiet.
            Assert.False(Contract.DefaultFlagBoxCriticalOnly);
            Assert.Equal(0, Contract.NormaliseBrightness(-5));
            Assert.Equal(100, Contract.NormaliseBrightness(101));
            Assert.Equal(60, Contract.NormaliseBrightness(60));
        }

        [Fact]
        public void The_strips_declare_the_names_every_generated_profile_reads()
        {
            // LedCentre and LedRpmStyle were the gap: contract.ts declared them, nineteen generated
            // .ledsprofile files read them through isnull(), and the plugin had neither constant,
            // delegate nor control, so every strip could only ever draw its defaults. Both suites were
            // green throughout, which is why The_two_sides_declare_the_same_properties() exists below.
            var expected = new List<string> { "LedCentre", "LedRpmStyle", "LedCarRevBar", "LedFlagAnimation", "LedMirrorFit", "LedMirrorReady" };
            expected.AddRange(Contract.MirrorRunLengths.Select(Contract.LedMirrorRun));
            // Appended after the runs rather than beside the three it belongs with, for the reason every
            // other addition is appended: both halves of the contract pin this list in order.
            expected.Add(Contract.LedSpotterWhole);
            Assert.Equal(expected, Contract.LedPropertyNames());
            foreach (var name in expected) Assert.Contains(name, Contract.LightsPropertyNames());
            // On: movement is what a flag is read by at the edge of vision, and off is the driver
            // asking for a rim that holds rather than blinks.
            Assert.True(Contract.DefaultLedFlagAnimation);
            // Rig settings rather than per-device groups, unlike a matrix: openDash generates one
            // profile per strip shape. The mirror runs are the exception and are not a device either --
            // LedMirror14 is a run length, which several shapes share -- so they are held out of this
            // check rather than allowed to retire it.
            var mirrorRuns = Contract.MirrorRunLengths.Select(Contract.LedMirrorRun);
            Assert.DoesNotContain(Contract.LedPropertyNames().Except(mirrorRuns), n => n.Contains("1") || n.Contains("2"));
            // Four to twenty-five without a gap: the grid generates every centre in that range, so a
            // missing one is a shape whose mirror reads a property nobody attaches.
            Assert.Equal(Enumerable.Range(4, 22), Contract.MirrorRunLengths);

            Assert.Equal(new[] { "rpm", "brake", "throttleBrake", "fuel" }, Contract.LedCentres);
            Assert.Equal(new[] { "car", "leftToRight", "meetInMiddle", "f1" }, Contract.LedRpmStyles);
            Assert.Equal(new[] { "stretch", "exact" }, Contract.LedMirrorFits);
            // The fifth centre is retired into the first, and migrated by name: the profile carries a
            // conditional group per value, and a stored "rpmOnly" would match none of them.
            Assert.DoesNotContain(Contract.RetiredLedCentre, Contract.LedCentres);
            Assert.Equal("rpm", Contract.NormaliseLedCentre("rpmOnly"));
            Assert.Equal("rpm", Contract.NormaliseLedCentre(" RPMONLY "));
            Assert.Equal("fuel", Contract.NormaliseLedCentre("fuel"));
            Assert.Equal("rpm", Contract.NormaliseLedCentre("sparkles"));
            // The defaults are members of their own sets, which is what makes an unrecognised value
            // safe to fall back from.
            Assert.Contains(Contract.DefaultLedCentre, Contract.LedCentres);
            Assert.Contains(Contract.DefaultLedRpmStyle, Contract.LedRpmStyles);
            Assert.Contains(Contract.DefaultLedMirrorFit, Contract.LedMirrorFits);
            Assert.Equal("rpm", Contract.DefaultLedCentre);
            // The car's own, by default: ADR 0018's opinion is that the car is right, and a car with no
            // table falls back on its own without the driver choosing anything.
            Assert.Equal("car", Contract.DefaultLedRpmStyle);
            Assert.Equal("stretch", Contract.DefaultLedMirrorFit);
            Assert.Equal("rpm", Contract.NormaliseChoice("sparkles", Contract.LedCentres, Contract.DefaultLedCentre));
            Assert.Equal("f1", Contract.NormaliseChoice(" F1 ", Contract.LedRpmStyles, Contract.DefaultLedRpmStyle));
            // A run length missing here is a strip shape with no mirror and nothing that would say so,
            // so the list is checked against the shapes themselves in packages/dash/test/leds.test.ts.
            Assert.Equal(Enumerable.Range(4, 22), Contract.MirrorRunLengths);
            Assert.Equal("LedMirror14", Contract.LedMirrorRun(14));
        }

        [Fact]
        public void The_two_sides_declare_the_same_properties()
        {
            // The gap this closes: contract.ts and Contract.cs each build the list for themselves, and
            // nothing compared the two, so a name could be added on one side alone and both suites stay
            // green -- which is exactly what happened to LedCentre and LedRpmStyle. The pinned file is
            // the third party they are both measured against; contract.test.ts checks the TypeScript
            // against the same file, in order.
            //
            // By set and not in order. The two sides genuinely emit a face's twenty-one names in
            // different orders -- contract.ts groups them zone by zone, Contract.cs groups them by
            // property across the zones -- and that predates this test and is not settled by it. The
            // orders that do matter are asserted by index elsewhere in this file.
            var path = RepoPaths.DeclaredProperties();
            // Not skipped when it is missing, unlike the tests that read the dash package's own source:
            // a pin nobody can find is the state this test was written to end.
            Assert.True(File.Exists(path), "declared-properties.txt not found at " + path);
            var pinned = File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
                .ToList();
            var declared = Contract.PropertyNames().Select(name => Contract.Prefix + "." + name).ToList();

            Assert.Equal(pinned.Count, pinned.Distinct().Count());
            Assert.Equal(declared.Count, declared.Distinct().Count());
            // Named rather than only counted, so the failure says which name moved.
            Assert.Empty(declared.Except(pinned));
            Assert.Empty(pinned.Except(declared));
            Assert.Equal(pinned.Count, declared.Count);
            Assert.Contains("OpenDash.LedCentre", declared);
            Assert.Contains("OpenDash.LedRpmStyle", declared);
        }

        [Fact]
        public void Every_face_has_its_own_group_and_no_two_faces_share_a_property()
        {
            var all = new List<string>(Contract.PropertyNames());
            Assert.Equal(all.Count, new HashSet<string>(all, StringComparer.Ordinal).Count);
            foreach (var face in Contract.FaceSizes)
            {
                foreach (var name in Contract.FacePropertyNames(face))
                {
                    Assert.Contains(name, all);
                    Assert.StartsWith(Contract.FacePrefix(face), name, StringComparison.Ordinal);
                }
            }
            // Twenty-four each: four zones times page, mask, start and class filter, four bar fields,
            // the glance, the flag format, the lap review and what this face carries at the top.
            Assert.Equal(24, new List<string>(Contract.FacePropertyNames(Contract.ReferenceFace)).Count);
        }

        [Fact]
        public void A_face_prefix_round_trips_and_an_unknown_one_is_refused()
        {
            foreach (var face in Contract.FaceSizes)
            {
                Assert.Equal(face.Width, Contract.FaceForPrefix(Contract.FacePrefix(face)).Width);
                Assert.Equal(face.Height, Contract.FaceForPrefix(Contract.FacePrefix(face)).Height);
                Assert.True(Contract.IsKnownFacePrefix(Contract.FacePrefix(face)));
            }
            Assert.False(Contract.IsKnownFacePrefix("Face1x1"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Contract.FaceForPrefix("Face1x1"));
        }

        [Fact]
        public void The_face_sizes_are_the_ones_the_generator_ships()
        {
            // FACE_SIZES in contract.ts is the list the build walks; a face there and not here would
            // ship with no properties at all, and the reverse would attach properties nothing reads.
            var path = RepoPaths.ContractTs();
            if (!File.Exists(path)) return; // the dash package is built separately; nothing to compare yet
            var source = File.ReadAllText(path);
            // Captured to the closing "];" on its own line rather than to the first "]", because each
            // entry now carries a parts array of its own.
            var match = Regex.Match(source, @"FACE_SIZES[^=]*=\s*\[(?<items>.*?)\r?\n\];", RegexOptions.Singleline);
            Assert.True(match.Success, "FACE_SIZES not found in contract.ts");
            var sizes = Regex.Matches(
                match.Groups["items"].Value,
                @"width:\s*(?<w>\d+),\s*height:\s*(?<h>\d+),\s*body:\s*'(?<body>row|column)',\s*parts:\s*\[(?<parts>[^\]]*)\],\s*hasBar:\s*(?<bar>true|false),\s*barFieldsPerEnd:\s*(?<per>\d+),"
                    + @"\s*rows:\s*\{\s*revBar:\s*(?<revBar>\d+),\s*bar:\s*(?<barRow>\d+),\s*body:\s*(?<bodyRow>\d+),\s*band:\s*(?<band>\d+)\s*\}");
            Assert.Equal(Contract.FaceSizes.Count, sizes.Count);
            for (var i = 0; i < sizes.Count; i++)
            {
                var face = Contract.FaceSizes[i];
                Assert.Equal(face.Width, int.Parse(sizes[i].Groups["w"].Value, CultureInfo.InvariantCulture));
                Assert.Equal(face.Height, int.Parse(sizes[i].Groups["h"].Value, CultureInfo.InvariantCulture));
                Assert.Equal(face.Body == Contract.FaceBody.Column ? "column" : "row", sizes[i].Groups["body"].Value);
                Assert.Equal(face.HasBar, sizes[i].Groups["bar"].Value == "true");
                Assert.Equal(face.BarFieldsPerEnd, int.Parse(sizes[i].Groups["per"].Value, CultureInfo.InvariantCulture));
                var parts = sizes[i].Groups["parts"].Value.Split(',').Select(v => int.Parse(v.Trim(), CultureInfo.InvariantCulture)).ToArray();
                Assert.Equal(face.Parts, parts);
                // The four rows a plan of the face scales from. zoneFace.test.ts holds these against
                // the rectangles in zones/faces/*.ts, so checking them here against contract.ts reaches
                // the drawings without this file having to parse eight layouts, one of which is derived
                // from another and carries no numbers of its own.
                Assert.Equal(face.RevBarHeight, int.Parse(sizes[i].Groups["revBar"].Value, CultureInfo.InvariantCulture));
                Assert.Equal(face.BarHeight, int.Parse(sizes[i].Groups["barRow"].Value, CultureInfo.InvariantCulture));
                Assert.Equal(face.BodyHeight, int.Parse(sizes[i].Groups["bodyRow"].Value, CultureInfo.InvariantCulture));
                Assert.Equal(face.BandHeight, int.Parse(sizes[i].Groups["band"].Value, CultureInfo.InvariantCulture));
                // A face with no bar has no bar row, and every other row is there to be drawn.
                Assert.Equal(face.HasBar, face.BarHeight > 0);
                Assert.True(face.RevBarHeight > 0 && face.BodyHeight > 0 && face.BandHeight > 0);
                // And the rows add up to the face, one seam between each pair that is drawn.
                var seams = face.HasBar ? 2 : 1;
                Assert.Equal(face.Height - seams, face.RevBarHeight + face.BarHeight + face.BodyHeight + face.BandHeight);
            }
            // The nano is the one face with no bar, and the portrait the one with a stacked body and a
            // single field per end. Stated here because both are what the panel has to draw differently.
            Assert.Single(Contract.FaceSizes.Where(f => !f.HasBar));
            Assert.Single(Contract.FaceSizes.Where(f => f.Body == Contract.FaceBody.Column));
            Assert.Single(Contract.FaceSizes.Where(f => f.BarFieldsPerEnd == 1));
        }

        [Fact]
        public void Every_action_names_the_face_it_moves()
        {
            var actions = new List<string>(Contract.ActionNames());
            Assert.Equal(Contract.FaceSizes.Count * 5, actions.Count);
            Assert.Equal(actions.Count, new HashSet<string>(actions, StringComparer.Ordinal).Count);
            Assert.Contains("Face1920x480CycleZoneA", actions);
            Assert.Contains("Face600x686HoldQuickGlance", actions);
            // Nothing unprefixed: one action moving every face is what the prefix exists to prevent.
            Assert.DoesNotContain(actions, a => a.StartsWith("CycleZone", StringComparison.Ordinal));
        }

        [Fact]
        public void A_companion_registers_no_action_because_SimHub_pages_it()
        {
            // None, and that is the change. Both of the companion's actions moved `CompanionPage`, the
            // screens were gated on it, and that gate is why a tap on the phone did nothing: SimHub's
            // only touch gesture maps a tap to the previous or next screen and its navigation walks
            // the screens whose expression is true, so one of twenty-one enabled had nowhere to go.
            // SimHub owns the paging now, and an action that moves nothing would be a dead row in its
            // Controls and events.
            Assert.Empty(Contract.ScreenActionNames(Contract.KindCompanion, Contract.CompanionPrefix));
            Assert.Empty(Contract.CompanionActionNames("Rim"));
            // The name is kept, because a face still uses the same spelling for its own zones.
            Assert.Equal("RimNextModule", Contract.NextModuleActionFor("Rim"));
            // A pit wall has the glance alone: it cycles nothing, every panel being on screen at once,
            // but the canvas asks for a page called up on demand over a zone's assigned one.
            Assert.Equal(new[] { "PitWallHoldQuickGlance" }, Contract.ScreenActionNames(Contract.KindPitWall, Contract.PitWallPrefix).ToArray());
            Assert.Equal(new[] { "GarageHoldQuickGlance" }, Contract.PitWallActionNames("Garage").ToArray());
            Assert.Empty(Contract.ScreenActionNames(Contract.KindSlots, "Slots480"));
            // Lap times and the track map, counted from zero, so the track map is module 13 at page 12.
            Assert.Equal("lapTimes", Modules.ByNumber(Contract.DefaultCompanionStart + 1).Id);
            Assert.Equal("track", Modules.ByNumber(Contract.DefaultCompanionQuickGlance + 1).Id);
        }

        [Fact]
        public void A_pit_wall_glance_packs_a_zone_and_a_standard_page()
        {
            // Zone D and the leaderboard: the zone a glance can borrow without hiding what it is for.
            Assert.Equal(305, Contract.DefaultPitWallQuickGlance);
            Assert.Equal(3, Contract.QuickGlanceZone(Contract.DefaultPitWallQuickGlance));
            Assert.Equal("Leaderboard", ZonePages.StandardName(Contract.QuickGlancePage(Contract.DefaultPitWallQuickGlance)));
            Assert.Equal(210, Contract.PitWallQuickGlanceValue(2, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => Contract.PitWallQuickGlanceValue(Contract.GlanceZoneSlots().Count, 0));
            // Half a glance is not a glance: a page outside the standard catalogue takes the zone with it.
            Assert.Equal(Contract.DefaultPitWallQuickGlance, Contract.NormalisePitWallQuickGlance(211));
            Assert.Equal(Contract.DefaultPitWallQuickGlance, Contract.NormalisePitWallQuickGlance(Contract.GlanceZoneSlots().Count * 100));
            Assert.Equal(Contract.DefaultPitWallQuickGlance, Contract.NormalisePitWallQuickGlance(-1));
            Assert.Equal(210, Contract.NormalisePitWallQuickGlance(210));
            // Every option the pane offers survives its own normaliser, and reads as a zone and a page.
            foreach (var option in PanelPitWallPlan.GlanceOptions()) Assert.Equal(option, Contract.NormalisePitWallQuickGlance(option));
            Assert.Equal(Contract.GlanceZoneSlots().Count * ZonePages.Standard.Count, PanelPitWallPlan.GlanceOptions().Length);
            Assert.Equal("Tower B \u00b7 Leaderboard", PanelPitWallPlan.GlanceLabel(Contract.DefaultPitWallQuickGlance));
        }

        /// <summary>The `id` fields of the page list that follows the given declaration.</summary>
        private static string[] PageIdsOf(string source, string declaration)
        {
            var match = Regex.Match(source, Regex.Escape(declaration) + @"[^=]*=\s*\[(?<items>[^\]]*)\]");
            Assert.True(match.Success, declaration + " not found in contract.ts");
            return Regex.Matches(match.Groups["items"].Value, @"id:\s*'([^']*)'").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        }

        /// <summary>The `name` fields of the page list that follows the given declaration.</summary>
        private static string[] PageNamesOf(string source, string declaration)
        {
            var match = Regex.Match(source, Regex.Escape(declaration) + @"[^=]*=\s*\[(?<items>[^\]]*)\]");
            Assert.True(match.Success, declaration + " not found in contract.ts");
            return Regex.Matches(match.Groups["items"].Value, @"name:\s*'([^']*)'").Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
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
