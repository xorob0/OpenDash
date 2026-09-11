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

        /// <summary>The four configurable zones of a pit wall page.</summary>
        public static readonly string[] ZoneLetters = { "A", "B", "C", "D" };

        /// <summary>Default page of zones A to D: fuel, tyres, relative and opponents, which is what a spotter watches.</summary>
        public static readonly IReadOnlyList<int> DefaultZonePages = new[] { 0, 1, 4, 2 };

        /// <summary>Default page of the wide zone: the car telemetry trace with the settings grid beside it.</summary>
        public const int DefaultWideZonePage = 5;

        /// <summary>The web view page shows nothing until the user sets an address.</summary>
        public const string DefaultWebViewUrl = "";

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
            var index = Array.IndexOf(ZoneLetters, letter);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(letter));
            return "PitWallZone" + letter;
        }

        /// <summary>Default page of a zone, by its letter.</summary>
        public static int DefaultZonePage(string letter)
        {
            var index = Array.IndexOf(ZoneLetters, letter);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(letter));
            return DefaultZonePages[index];
        }

        /// <summary>The default on/off state of the 21 companion modules, index 0 is module 1.</summary>
        public static bool[] DefaultModules()
        {
            return Modules.Defaults();
        }

        /// <summary>The default page of every zone, in letter order.</summary>
        public static int[] DefaultZones()
        {
            var zones = new int[ZoneLetters.Length];
            for (var i = 0; i < zones.Length; i++) zones[i] = DefaultZonePages[i];
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
            for (var module = 1; module <= Modules.Count; module++) yield return ModuleProperty(module);
            foreach (var letter in ZoneLetters) yield return ZoneProperty(letter);
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
