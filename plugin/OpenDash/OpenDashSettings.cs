// OpenDashSettings.cs: the persisted settings object. A plain POCO that SimHub serialises with Json.NET
// under PluginsData/Common/OpenDash.GeneralSettings.json. Normalise() repairs whatever comes back from disk.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    public class OpenDashSettings
    {
        public bool ShiftLights { get; set; } = Contract.DefaultShiftLights;

        public string PositionMode { get; set; } = Contract.DefaultPositionMode;

        public string DeltaReference { get; set; } = Contract.DefaultDeltaReference;

        public string SessionProgress { get; set; } = Contract.DefaultSessionProgress;

        /// <summary>Card number per slot, index 0 is slot 1. Always Contract.SlotCount long after Normalise().</summary>
        public int[] Slots { get; set; } = Contract.DefaultSlots();

        /// <summary>Whether each companion module is enabled, index 0 is module 1. Always Modules.Count long after Normalise().</summary>
        public bool[] Modules { get; set; } = Contract.DefaultModules();

        /// <summary>Standard zone page per pit wall zone, index 0 is zone A. Always four long after Normalise().</summary>
        public int[] Zones { get; set; } = Contract.PitWallDefaultZones();

        /// <summary>Wide zone page of the pit wall tower page.</summary>
        public int WideZone { get; set; } = Contract.DefaultWideZonePage;

        /// <summary>Address of the web view zone page; empty until the user sets one.</summary>
        public string WebViewUrl { get; set; } = Contract.DefaultWebViewUrl;

        /// <summary>Clamps every value into its contract: unknown modes and card numbers fall back to the defaults,
        /// a short or missing slot array is padded with the default assignment, a long one is truncated.</summary>
        public void Normalise()
        {
            PositionMode = Contract.NormaliseChoice(PositionMode, Contract.PositionModes, Contract.DefaultPositionMode);
            DeltaReference = Contract.NormaliseChoice(DeltaReference, Contract.DeltaReferences, Contract.DefaultDeltaReference);
            SessionProgress = Contract.NormaliseChoice(SessionProgress, Contract.SessionProgressModes, Contract.DefaultSessionProgress);

            var normalised = Contract.DefaultSlots();
            if (Slots != null)
            {
                for (var i = 0; i < normalised.Length && i < Slots.Length; i++)
                {
                    normalised[i] = Contract.NormaliseCard(Slots[i], Contract.DefaultCard(i + 1));
                }
            }
            Slots = normalised;

            var modules = Contract.DefaultModules();
            if (Modules != null)
            {
                for (var i = 0; i < modules.Length && i < Modules.Length; i++) modules[i] = Modules[i];
            }
            Modules = modules;

            var zones = Contract.PitWallDefaultZones();
            if (Zones != null)
            {
                for (var i = 0; i < zones.Length && i < Zones.Length; i++)
                {
                    zones[i] = Contract.NormaliseZonePage(Zones[i], Contract.PitWallDefaultZonePages[i]);
                }
            }
            Zones = zones;

            WideZone = Contract.NormaliseWideZonePage(WideZone);
            WebViewUrl = Contract.NormaliseUrl(WebViewUrl);
        }

        /// <summary>Whether a companion module is enabled, 1-based. Safe to call before Normalise().</summary>
        public bool Module(int module)
        {
            var meta = OpenDashPlugin.Modules.ByNumber(module);
            if (meta == null) return false;
            var index = module - 1;
            if (Modules == null || index >= Modules.Length) return meta.Enabled;
            return Modules[index];
        }

        public void SetModule(int module, bool enabled)
        {
            if (!OpenDashPlugin.Modules.IsValidNumber(module)) throw new ArgumentOutOfRangeException(nameof(module));
            if (Modules == null || Modules.Length != OpenDashPlugin.Modules.Count) Normalise();
            Modules[module - 1] = enabled;
        }

        /// <summary>Page shown in a pit wall zone, by its letter. Safe to call before Normalise().</summary>
        public int Zone(string letter)
        {
            var index = Array.IndexOf(Contract.PitWallZoneLetters, letter);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(letter));
            if (Zones == null || index >= Zones.Length) return Contract.PitWallDefaultZonePages[index];
            return Contract.NormaliseZonePage(Zones[index], Contract.PitWallDefaultZonePages[index]);
        }

        public void SetZone(string letter, int page)
        {
            var index = Array.IndexOf(Contract.PitWallZoneLetters, letter);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(letter));
            if (Zones == null || Zones.Length != Contract.PitWallZoneLetters.Length) Normalise();
            Zones[index] = Contract.NormaliseZonePage(page, Contract.PitWallDefaultZonePages[index]);
        }

        /// <summary>Card number shown in a slot, 1-based. Safe to call before Normalise().</summary>
        public int Slot(int slot)
        {
            var index = slot - 1;
            if (Slots == null || index < 0 || index >= Slots.Length) return Contract.DefaultCard(slot);
            return Contract.NormaliseCard(Slots[index], Contract.DefaultCard(slot));
        }

        public void SetSlot(int slot, int card)
        {
            if (slot < 1 || slot > Contract.SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            if (Slots == null || Slots.Length != Contract.SlotCount) Normalise();
            Slots[slot - 1] = Contract.NormaliseCard(card, Contract.DefaultCard(slot));
        }

        /// <summary>Cards assigned to more than one slot, in card order.</summary>
        public IReadOnlyList<DuplicateAssignment> Duplicates() => DuplicateAssignment.Find(Slots);

        /// <summary>Copies the values of another settings object; used by the panel to keep one instance alive.</summary>
        public void CopyFrom(OpenDashSettings other)
        {
            if (other == null) return;
            ShiftLights = other.ShiftLights;
            PositionMode = other.PositionMode;
            DeltaReference = other.DeltaReference;
            SessionProgress = other.SessionProgress;
            Slots = other.Slots == null ? null : (int[])other.Slots.Clone();
            Modules = other.Modules == null ? null : (bool[])other.Modules.Clone();
            Zones = other.Zones == null ? null : (int[])other.Zones.Clone();
            WideZone = other.WideZone;
            WebViewUrl = other.WebViewUrl;
            Normalise();
        }
    }

    /// <summary>One card that sits in several slots. The panel shows it as a warning and does not prevent it.</summary>
    public sealed class DuplicateAssignment
    {
        public DuplicateAssignment(int card, int[] slots)
        {
            Card = card;
            Slots = slots;
        }

        public int Card { get; }

        /// <summary>1-based slot numbers, ascending.</summary>
        public int[] Slots { get; }

        /// <summary>"Fuel laps is assigned to slots 8 and 9."</summary>
        public string Message()
        {
            var name = Cards.DisplayName(Card);
            var list = Slots.Length == 2
                ? Slots[0] + " and " + Slots[1]
                : string.Join(", ", Slots.Take(Slots.Length - 1)) + " and " + Slots[Slots.Length - 1];
            return name + " is assigned to slots " + list + ".";
        }

        public static IReadOnlyList<DuplicateAssignment> Find(int[] slots)
        {
            var result = new List<DuplicateAssignment>();
            if (slots == null) return result;
            var bySlot = new Dictionary<int, List<int>>();
            for (var i = 0; i < slots.Length; i++)
            {
                List<int> list;
                if (!bySlot.TryGetValue(slots[i], out list))
                {
                    list = new List<int>();
                    bySlot[slots[i]] = list;
                }
                list.Add(i + 1);
            }
            foreach (var pair in bySlot.OrderBy(p => p.Key))
            {
                if (pair.Value.Count > 1) result.Add(new DuplicateAssignment(pair.Key, pair.Value.ToArray()));
            }
            return result;
        }

        /// <summary>All messages, one per line; empty when there is no duplicate.</summary>
        public static string Warning(int[] slots)
        {
            return string.Join(Environment.NewLine, Find(slots).Select(d => d.Message()));
        }
    }
}
