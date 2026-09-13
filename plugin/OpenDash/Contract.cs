// Contract.cs: the settings contract shared with packages/dash/src/contract.ts (spec section 4).
// Property names, value sets and defaults. Pure C#: no SimHub or WPF types, compiled into the tests.
using System;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    public static class Contract
    {
        /// <summary>SimHub prefixes every property with the plugin class name: [OpenDash.ShiftLights].</summary>
        public const string Prefix = "OpenDash";

        /// <summary>SLOT_MAX in contract.ts: the largest slot count any layout declares. The plugin exposes exactly this many.</summary>
        public const int SlotCount = 12;

        public const string ShiftLights = "ShiftLights";
        public const string PositionMode = "PositionMode";
        public const string DeltaReference = "DeltaReference";
        public const string SessionProgress = "SessionProgress";
        public const string PitWallWide = "PitWallWide";
        public const string WebViewUrl = "WebViewUrl";

        public const bool DefaultShiftLights = true;

        public static readonly string[] PositionModes = { "overall", "class" };
        public const string DefaultPositionMode = "overall";

        public static readonly string[] DeltaReferences = { "session", "alltime" };
        public const string DefaultDeltaReference = "session";

        public static readonly string[] SessionProgressModes = { "auto", "laps", "time" };
        public const string DefaultSessionProgress = "auto";

        /// <summary>The four configurable zones of a pit wall page. Prefixed because the dash face has
        /// zones of its own now, and the two are deliberately different catalogues.</summary>
        public static readonly string[] PitWallZoneLetters = { "A", "B", "C", "D" };

        /// <summary>Default page of zones A to D: fuel, tyres, relative and opponents, which is what a spotter watches.</summary>
        public static readonly IReadOnlyList<int> PitWallDefaultZonePages = new[] { 0, 1, 4, 2 };

        /// <summary>Default page of the wide zone: the car telemetry trace with the settings grid beside it.</summary>
        public const int DefaultWideZonePage = 5;

        /// <summary>The web view page shows nothing until the user sets an address.</summary>
        public const string DefaultWebViewUrl = "";


        // --- The zone face ---------------------------------------------------------------------
        //
        // Additive: Slot01 to Slot12 stay until the card path is retired. The shape of these is the
        // model -- a slot is arranged once with a mouse, a zone is changed with a thumb mid-lap, so
        // what the contract carries is a page number a button can advance.

        /// <summary>The four zones of a rectangular face. Band D is a zone: it cycles a catalogue.</summary>
        public static readonly string[] FaceZoneLetters = { "A", "B", "C", "D" };

        /// <summary>How many pages each zone can show, in letter order: A four, B and C the twenty-one
        /// modules, D eight.</summary>
        public static readonly IReadOnlyList<int> FaceZonePageCounts = new[] { 4, Modules.Count, Modules.Count, 8 };

        /// <summary>Which page each zone opens on: the gear, lap times, the relative, and fuel.</summary>
        public static readonly IReadOnlyList<int> DefaultFaceZonePages = new[] { 0, 0, 14, 0 };

        /// <summary>The bar's four end fields, left to right.</summary>
        public static readonly string[] BarSlots = { "Left1", "Left2", "Right1", "Right2" };

        /// <summary>Race and Lap on the left, Position and Class on the right, as the artboards draw them.</summary>
        public static readonly IReadOnlyList<int> DefaultBarFields = new[] { 0, 1, 5, 6 };

        /// <summary>How many fields an end of the bar can choose from. Ten, not the canvas's eleven:
        /// strength of field is not published by SimHub in any form, and ADR 0009 decided a field that
        /// can never have a value is not offered.</summary>
        public const int BarFieldCount = 10;

        /// <summary>Zone C on the track page, which is what a glance is usually for. Encoded as
        /// zoneIndex * 100 + page, so a glance is one property rather than a pair per zone.</summary>
        public const int DefaultQuickGlance = 2 * 100 + 12;

        public const string QuickGlance = "QuickGlance";

        /// <summary>
        /// The actions a driver binds to a wheel button. Named as verbs, because a property is a noun:
        /// `OpenDash.QuickGlance` is what the glance is set to and `OpenDash.HoldQuickGlance` is the
        /// button that shows it, and a log naming one should not read like the other.
        /// </summary>
        public const string HoldQuickGlanceAction = "HoldQuickGlance";

        /// <summary>Action that advances one zone to its next enabled page: CycleZoneA .. CycleZoneD.</summary>
        public static string CycleZoneAction(string letter)
        {
            RequireFaceZone(letter);
            return "CycleZone" + letter;
        }

        /// <summary>
        /// An action's name as SimHub knows it. `PluginManager.GetName` is `pluginType.Name + "." +
        /// name`, and the plugin class is `OpenDash`, so it is the same prefix the properties carry.
        /// </summary>
        public static string FullActionName(string actionName)
        {
            return Prefix + "." + actionName;
        }

        /// <summary>Every action the plugin registers, in registration order.</summary>
        public static IEnumerable<string> ActionNames()
        {
            foreach (var letter in FaceZoneLetters) yield return CycleZoneAction(letter);
            yield return HoldQuickGlanceAction;
        }

        /// <summary>Property name of a zone's current page: ZoneA .. ZoneD.</summary>
        public static string ZonePageProperty(string letter)
        {
            RequireFaceZone(letter);
            return "Zone" + letter;
        }

        /// <summary>Property name of a zone's enabled-page mask: ZoneAPages .. ZoneDPages.</summary>
        public static string ZoneMaskProperty(string letter)
        {
            RequireFaceZone(letter);
            return "Zone" + letter + "Pages";
        }

        /// <summary>Property name of a zone's start page: ZoneAStart .. ZoneDStart.</summary>
        public static string ZoneStartProperty(string letter)
        {
            RequireFaceZone(letter);
            return "Zone" + letter + "Start";
        }

        /// <summary>Property name of a zone's class filter: ZoneAClassOnly .. ZoneDClassOnly.</summary>
        public static string ZoneClassOnlyProperty(string letter)
        {
            RequireFaceZone(letter);
            return "Zone" + letter + "ClassOnly";
        }

        /// <summary>Property name of a bar end field: BarLeft1 .. BarRight2.</summary>
        public static string BarFieldProperty(string slot)
        {
            if (Array.IndexOf(BarSlots, slot) < 0) throw new ArgumentOutOfRangeException(nameof(slot));
            return "Bar" + slot;
        }

        private static void RequireFaceZone(string letter)
        {
            if (Array.IndexOf(FaceZoneLetters, letter) < 0) throw new ArgumentOutOfRangeException(nameof(letter));
        }

        /// <summary>Every page enabled, which is the mask a zone starts with: a driver turns off what
        /// they do not want rather than turning on what they do.</summary>
        public static int DefaultZoneMask(int zoneIndex)
        {
            if (zoneIndex < 0 || zoneIndex >= FaceZoneLetters.Length) throw new ArgumentOutOfRangeException(nameof(zoneIndex));
            return (1 << FaceZonePageCounts[zoneIndex]) - 1;
        }

        /// <summary>The default page of every face zone, in letter order.</summary>
        public static int[] DefaultFaceZones()
        {
            var zones = new int[FaceZoneLetters.Length];
            for (var i = 0; i < zones.Length; i++) zones[i] = DefaultFaceZonePages[i];
            return zones;
        }

        /// <summary>Whether a zone's list pages show the player's own class rather than the whole field.
        /// Off: most racing is single-class, and a driver in one would not thank us for a leaderboard
        /// that hides nobody but says it does.</summary>
        public const bool DefaultZoneClassOnly = false;

        /// <summary>The class filter of every face zone, in letter order.</summary>
        public static bool[] DefaultFaceZoneClassOnly()
        {
            var flags = new bool[FaceZoneLetters.Length];
            for (var i = 0; i < flags.Length; i++) flags[i] = DefaultZoneClassOnly;
            return flags;
        }

        /// <summary>The default mask of every face zone, in letter order.</summary>
        public static int[] DefaultFaceZoneMasks()
        {
            var masks = new int[FaceZoneLetters.Length];
            for (var i = 0; i < masks.Length; i++) masks[i] = DefaultZoneMask(i);
            return masks;
        }

        /// <summary>The default field of every bar slot, left to right.</summary>
        public static int[] DefaultBarSlots()
        {
            var fields = new int[BarSlots.Length];
            for (var i = 0; i < fields.Length; i++) fields[i] = DefaultBarFields[i];
            return fields;
        }

        /// <summary>Property name of a slot, 1-based: Slot01 .. Slot12.</summary>
        public static string SlotProperty(int slot)
        {
            if (slot < 1 || slot > SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return "Slot" + slot.ToString("00");
        }

        /// <summary>
        /// Default card of each slot, slot 1 first. Speed leads, so that even a two-slot face shows it
        /// now that the hero holds the gear alone; the timing block follows, then the car values. Tyre
        /// pressures is the one card no slot shows by default: in iRacing it only changes in the pit
        /// stall, and tyre temperatures already carry that reading. Mirrors DEFAULT_SLOT_CARDS in
        /// packages/dash/src/contract.ts.
        /// </summary>
        public static readonly IReadOnlyList<int> DefaultSlotCards = new[] { 12, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        /// <summary>Default card of a slot, 1-based.</summary>
        public static int DefaultCard(int slot)
        {
            if (slot < 1 || slot > SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return DefaultSlotCards[slot - 1];
        }

        public static int[] DefaultSlots()
        {
            var slots = new int[SlotCount];
            for (var i = 0; i < SlotCount; i++) slots[i] = DefaultCard(i + 1);
            return slots;
        }

        /// <summary>Property name of a companion module, 1-based: CompanionModule01 .. CompanionModule21.</summary>
        public static string ModuleProperty(int module)
        {
            if (!Modules.IsValidNumber(module)) throw new ArgumentOutOfRangeException(nameof(module));
            return "CompanionModule" + module.ToString("00");
        }

        /// <summary>Property name of a pit wall zone: PitWallZoneA .. PitWallZoneD.</summary>
        public static string ZoneProperty(string letter)
        {
            var index = Array.IndexOf(PitWallZoneLetters, letter);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(letter));
            return "PitWallZone" + letter;
        }

        /// <summary>Default page of a zone, by its letter.</summary>
        public static int DefaultZonePage(string letter)
        {
            var index = Array.IndexOf(PitWallZoneLetters, letter);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(letter));
            return PitWallDefaultZonePages[index];
        }

        /// <summary>The default on/off state of the 21 companion modules, index 0 is module 1.</summary>
        public static bool[] DefaultModules()
        {
            return Modules.Defaults();
        }

        /// <summary>The default page of every zone, in letter order.</summary>
        public static int[] PitWallDefaultZones()
        {
            var zones = new int[PitWallZoneLetters.Length];
            for (var i = 0; i < zones.Length; i++) zones[i] = PitWallDefaultZonePages[i];
            return zones;
        }

        /// <summary>Every property the plugin attaches, without the prefix, in attachment order.</summary>
        public static IEnumerable<string> PropertyNames()
        {
            yield return ShiftLights;
            yield return PositionMode;
            yield return DeltaReference;
            yield return SessionProgress;
            for (var slot = 1; slot <= SlotCount; slot++) yield return SlotProperty(slot);
            foreach (var letter in FaceZoneLetters) yield return ZonePageProperty(letter);
            foreach (var letter in FaceZoneLetters) yield return ZoneMaskProperty(letter);
            foreach (var letter in FaceZoneLetters) yield return ZoneStartProperty(letter);
            foreach (var letter in FaceZoneLetters) yield return ZoneClassOnlyProperty(letter);
            foreach (var slot in BarSlots) yield return BarFieldProperty(slot);
            yield return QuickGlance;
            for (var module = 1; module <= Modules.Count; module++) yield return ModuleProperty(module);
            foreach (var letter in PitWallZoneLetters) yield return ZoneProperty(letter);
            yield return PitWallWide;
            yield return WebViewUrl;
        }

        /// <summary>Returns value when it is one of allowed (ordinal, case-insensitive, canonical casing), else fallback.</summary>
        public static string NormaliseChoice(string value, string[] allowed, string fallback)
        {
            if (value == null) return fallback;
            var trimmed = value.Trim();
            foreach (var option in allowed)
            {
                if (string.Equals(option, trimmed, StringComparison.OrdinalIgnoreCase)) return option;
            }
            return fallback;
        }

        /// <summary>Clamps a page number into a catalogue of the given size, or returns fallback.</summary>
        public static int NormalisePage(int page, int count, int fallback)
        {
            return page >= 0 && page < count ? page : fallback;
        }

        /// <summary>
        /// The first enabled page at or after the given one, wrapping once. This is where a zone lands
        /// when the page it was sitting on is turned off: forward rather than back, because a cycle
        /// runs forward and a driver pressing the button again should carry on rather than repeat.
        /// A mask with nothing set returns the page unchanged; Normalise() never produces one.
        /// </summary>
        public static int FirstEnabledFrom(int page, int mask, int count)
        {
            if (count <= 0 || (mask & ((1 << count) - 1)) == 0) return page;
            var from = page >= 0 && page < count ? page : 0;
            for (var step = 0; step < count; step++)
            {
                var candidate = (from + step) % count;
                if ((mask & (1 << candidate)) != 0) return candidate;
            }
            return from;
        }

        /// <summary>The zone a quick glance shows, as an index into FaceZoneLetters.</summary>
        public static int QuickGlanceZone(int value) => value / 100;

        /// <summary>The page a quick glance shows, within that zone's catalogue.</summary>
        public static int QuickGlancePage(int value) => value % 100;

        /// <summary>A zone and a page packed into the one property a glance is configured with.</summary>
        public static int QuickGlanceValue(int zoneIndex, int page)
        {
            if (zoneIndex < 0 || zoneIndex >= FaceZoneLetters.Length) throw new ArgumentOutOfRangeException(nameof(zoneIndex));
            return zoneIndex * 100 + page;
        }

        /// <summary>Returns the glance when both halves are in range, else the default. A page outside
        /// its zone's catalogue takes the zone with it: half a glance is not a glance.</summary>
        public static int NormaliseQuickGlance(int value)
        {
            if (value < 0) return DefaultQuickGlance;
            var zoneIndex = QuickGlanceZone(value);
            if (zoneIndex >= FaceZoneLetters.Length) return DefaultQuickGlance;
            var page = QuickGlancePage(value);
            return page < FaceZonePageCounts[zoneIndex] ? value : DefaultQuickGlance;
        }

        /// <summary>Clamps a card number into the catalogue, or returns fallback when it is outside.</summary>
        public static int NormaliseCard(int card, int fallback)
        {
            return Cards.IsValidNumber(card) ? card : fallback;
        }

        /// <summary>Clamps a standard zone page number, or returns fallback when it is outside.</summary>
        public static int NormaliseZonePage(int page, int fallback)
        {
            return ZonePages.IsValidStandard(page) ? page : fallback;
        }

        /// <summary>Clamps a wide zone page number, or returns the default when it is outside.</summary>
        public static int NormaliseWideZonePage(int page)
        {
            return ZonePages.IsValidWide(page) ? page : DefaultWideZonePage;
        }

        /// <summary>
        /// Trims a web view address and keeps it only when it is an absolute http or https URL.
        /// Anything else becomes the empty default, so the page shows its "no address" state rather
        /// than handing SimHub's browser a file path or a script URL.
        /// </summary>
        public static string NormaliseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return DefaultWebViewUrl;
            var trimmed = url.Trim();
            Uri parsed;
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out parsed)) return DefaultWebViewUrl;
            if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) return DefaultWebViewUrl;
            return trimmed;
        }
    }
}
