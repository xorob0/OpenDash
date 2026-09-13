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

        // --- The dash face ---------------------------------------------------------------------

        /// <summary>Page each face zone is showing, index 0 is zone A. The wheel button that cycles a
        /// zone writes this; so does the panel, because a driver who picks a start page expects the
        /// face in front of them to follow rather than to wait for the next session.</summary>
        public int[] FaceZones { get; set; } = Contract.DefaultFaceZones();

        /// <summary>Which pages of each face zone are enabled, as a bit mask, index 0 is zone A. This is
        /// what decides how long a driver's cycle is, which makes it the most consequential setting here.</summary>
        public int[] FaceZoneMasks { get; set; } = Contract.DefaultFaceZoneMasks();

        /// <summary>Page each face zone opens on, index 0 is zone A.</summary>
        public int[] FaceZoneStarts { get; set; } = Contract.DefaultFaceZones();

        /// <summary>Whether each face zone's list pages show the player's own class rather than the whole
        /// field, index 0 is zone A. Per zone rather than per face, because the point of it is zone B
        /// listing the race and zone C listing the class a driver is actually in.</summary>
        public bool[] FaceZoneClassOnly { get; set; } = Contract.DefaultFaceZoneClassOnly();

        /// <summary>Field each end of the bar shows, in Contract.BarSlots order.</summary>
        public int[] BarFields { get; set; } = Contract.DefaultBarSlots();

        /// <summary>The zone and page a held button shows, encoded as zoneIndex * 100 + page.</summary>
        public int QuickGlance { get; set; } = Contract.DefaultQuickGlance;

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
            NormaliseFace();
        }

        /// <summary>
        /// Repairs the face settings, which are four parallel arrays and a packed pair. Three rules,
        /// in this order: a page number outside its zone's catalogue becomes the default, a mask is
        /// trimmed to the pages that exist and an empty one is refilled, and a page that the mask has
        /// turned off is snapped forward to the next one that is on.
        ///
        /// The last rule is the one that matters in use: turning off the page a zone is sitting on
        /// would otherwise leave the zone showing something its own cycle can never return to.
        /// </summary>
        private void NormaliseFace()
        {
            var zones = Contract.DefaultFaceZones();
            var masks = Contract.DefaultFaceZoneMasks();
            var starts = Contract.DefaultFaceZones();
            for (var i = 0; i < zones.Length; i++)
            {
                var pages = Contract.FaceZonePageCounts[i];
                var all = Contract.DefaultZoneMask(i);

                if (FaceZoneMasks != null && i < FaceZoneMasks.Length) masks[i] = FaceZoneMasks[i] & all;
                if (masks[i] == 0) masks[i] = all;

                if (FaceZoneStarts != null && i < FaceZoneStarts.Length) starts[i] = Contract.NormalisePage(FaceZoneStarts[i], pages, Contract.DefaultFaceZonePages[i]);
                starts[i] = Contract.FirstEnabledFrom(starts[i], masks[i], pages);

                if (FaceZones != null && i < FaceZones.Length) zones[i] = Contract.NormalisePage(FaceZones[i], pages, starts[i]);
                zones[i] = Contract.FirstEnabledFrom(zones[i], masks[i], pages);
            }
            FaceZones = zones;
            FaceZoneMasks = masks;
            FaceZoneStarts = starts;

            var classOnly = Contract.DefaultFaceZoneClassOnly();
            if (FaceZoneClassOnly != null)
            {
                for (var i = 0; i < classOnly.Length && i < FaceZoneClassOnly.Length; i++) classOnly[i] = FaceZoneClassOnly[i];
            }
            FaceZoneClassOnly = classOnly;

            var bar = Contract.DefaultBarSlots();
            if (BarFields != null)
            {
                for (var i = 0; i < bar.Length && i < BarFields.Length; i++)
                {
                    bar[i] = Contract.NormalisePage(BarFields[i], Contract.BarFieldCount, Contract.DefaultBarFields[i]);
                }
            }
            BarFields = bar;

            QuickGlance = Contract.NormaliseQuickGlance(QuickGlance);
        }

        /// <summary>Page a face zone is showing, by its letter. Safe to call before Normalise().</summary>
        public int FaceZone(string letter)
        {
            var index = FaceZoneIndex(letter);
            if (FaceZones == null || index >= FaceZones.Length) return Contract.DefaultFaceZonePages[index];
            return Contract.NormalisePage(FaceZones[index], Contract.FaceZonePageCounts[index], Contract.DefaultFaceZonePages[index]);
        }

        /// <summary>Page a face zone opens on, by its letter. Safe to call before Normalise().</summary>
        public int FaceZoneStart(string letter)
        {
            var index = FaceZoneIndex(letter);
            if (FaceZoneStarts == null || index >= FaceZoneStarts.Length) return Contract.DefaultFaceZonePages[index];
            return Contract.NormalisePage(FaceZoneStarts[index], Contract.FaceZonePageCounts[index], Contract.DefaultFaceZonePages[index]);
        }

        /// <summary>Sets the page a zone opens on, and the page it is showing with it: the panel is in
        /// front of a running dash, and a start page that only takes effect next time reads as broken.</summary>
        public void SetFaceZoneStart(string letter, int page)
        {
            var index = FaceZoneIndex(letter);
            EnsureFaceArrays();
            var clamped = Contract.NormalisePage(page, Contract.FaceZonePageCounts[index], Contract.DefaultFaceZonePages[index]);
            FaceZoneStarts[index] = clamped;
            FaceZones[index] = clamped;
            SetFaceZonePageEnabled(letter, clamped, true);
        }

        /// <summary>The enabled-page mask of a face zone, by its letter.</summary>
        public int FaceZoneMask(string letter)
        {
            var index = FaceZoneIndex(letter);
            if (FaceZoneMasks == null || index >= FaceZoneMasks.Length) return Contract.DefaultZoneMask(index);
            var mask = FaceZoneMasks[index] & Contract.DefaultZoneMask(index);
            return mask == 0 ? Contract.DefaultZoneMask(index) : mask;
        }

        /// <summary>Whether a zone's list pages show the player's own class, by its letter.</summary>
        public bool FaceZoneIsClassOnly(string letter)
        {
            var index = FaceZoneIndex(letter);
            if (FaceZoneClassOnly == null || index >= FaceZoneClassOnly.Length) return Contract.DefaultZoneClassOnly;
            return FaceZoneClassOnly[index];
        }

        /// <summary>Sets a zone's class filter.</summary>
        public void SetFaceZoneClassOnly(string letter, bool classOnly)
        {
            var index = FaceZoneIndex(letter);
            EnsureFaceArrays();
            FaceZoneClassOnly[index] = classOnly;
        }

        public bool FaceZonePageEnabled(string letter, int page)
        {
            var index = FaceZoneIndex(letter);
            if (page < 0 || page >= Contract.FaceZonePageCounts[index]) return false;
            return (FaceZoneMask(letter) & (1 << page)) != 0;
        }

        /// <summary>
        /// Turns one page of a zone on or off. Turning off the last enabled page is refused rather than
        /// obeyed: a zone with an empty cycle has nothing to draw, and the panel would have to invent a
        /// page to show anyway.
        /// </summary>
        public void SetFaceZonePageEnabled(string letter, int page, bool enabled)
        {
            var index = FaceZoneIndex(letter);
            if (page < 0 || page >= Contract.FaceZonePageCounts[index]) throw new ArgumentOutOfRangeException(nameof(page));
            EnsureFaceArrays();
            var mask = FaceZoneMask(letter);
            var next = enabled ? mask | (1 << page) : mask & ~(1 << page);
            if (next == 0) return;
            FaceZoneMasks[index] = next;
            FaceZoneStarts[index] = Contract.FirstEnabledFrom(FaceZoneStarts[index], next, Contract.FaceZonePageCounts[index]);
            FaceZones[index] = Contract.FirstEnabledFrom(FaceZones[index], next, Contract.FaceZonePageCounts[index]);
        }

        /// <summary>Field an end of the bar shows, by its slot name. Safe to call before Normalise().</summary>
        public int BarField(string slot)
        {
            var index = Array.IndexOf(Contract.BarSlots, slot);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(slot));
            if (BarFields == null || index >= BarFields.Length) return Contract.DefaultBarFields[index];
            return Contract.NormalisePage(BarFields[index], Contract.BarFieldCount, Contract.DefaultBarFields[index]);
        }

        public void SetBarField(string slot, int field)
        {
            var index = Array.IndexOf(Contract.BarSlots, slot);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(slot));
            if (BarFields == null || BarFields.Length != Contract.BarSlots.Length) Normalise();
            BarFields[index] = Contract.NormalisePage(field, Contract.BarFieldCount, Contract.DefaultBarFields[index]);
        }

        private static int FaceZoneIndex(string letter)
        {
            var index = Array.IndexOf(Contract.FaceZoneLetters, letter);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(letter));
            return index;
        }

        private void EnsureFaceArrays()
        {
            var zones = Contract.FaceZoneLetters.Length;
            if (FaceZones == null || FaceZones.Length != zones
                || FaceZoneMasks == null || FaceZoneMasks.Length != zones
                || FaceZoneStarts == null || FaceZoneStarts.Length != zones
                || FaceZoneClassOnly == null || FaceZoneClassOnly.Length != zones)
            {
                Normalise();
            }
        }

        /// <summary>Zones showing the same page as another zone, which the panel says and allows.</summary>
        public IReadOnlyList<FacePageClash> FaceClashes() => FacePageClash.Find(this);

        // --- What a wheel button does ----------------------------------------------------------

        /// <summary>
        /// Puts every zone on the page it opens on. Called once when the plugin starts, because that
        /// is what "the page the zone opens on" means: a session begins where the driver set it to,
        /// not where they happened to leave it three races ago.
        ///
        /// Cycling therefore does not save. The page a zone is showing is live state, and the only
        /// restart it has to survive is the dashboard window's -- which it does, because the page
        /// lives in the plugin and not in the dash.
        /// </summary>
        public void OpenOnStartPages()
        {
            EnsureFaceArrays();
            for (var i = 0; i < FaceZones.Length; i++) FaceZones[i] = FaceZoneStarts[i];
        }

        /// <summary>
        /// Advances a zone to its next enabled page and returns it. The mask is what sets the length
        /// of the cycle, so a zone with one page enabled stays where it is rather than flickering.
        /// </summary>
        public int CycleFaceZone(string letter)
        {
            var index = FaceZoneIndex(letter);
            EnsureFaceArrays();
            var count = Contract.FaceZonePageCounts[index];
            var next = Contract.FirstEnabledFrom((FaceZone(letter) + 1) % count, FaceZoneMask(letter), count);
            FaceZones[index] = next;
            return next;
        }

        /// <summary>Whether a glance is being held; a second press while one is does nothing.</summary>
        public bool GlanceHeld => glanceZone >= 0;

        private int glanceZone = -1;
        private int glanceRestore = -1;

        /// <summary>
        /// Shows the glance page in its zone, remembering what was there.
        ///
        /// The page does not have to be one the mask enables. A glance is an explicit thing a driver
        /// asked for by holding a button, and the mask is about what the cycle steps through; the
        /// two are different questions, and the track map is exactly the page somebody would want
        /// held and not cycled to.
        /// </summary>
        public void BeginQuickGlance()
        {
            if (GlanceHeld) return;
            EnsureFaceArrays();
            var glance = Contract.NormaliseQuickGlance(QuickGlance);
            var zone = Contract.QuickGlanceZone(glance);
            glanceZone = zone;
            glanceRestore = FaceZones[zone];
            FaceZones[zone] = Contract.QuickGlancePage(glance);
        }

        /// <summary>Puts the zone back where it was. A release with no press does nothing.</summary>
        public void EndQuickGlance()
        {
            if (!GlanceHeld) return;
            EnsureFaceArrays();
            FaceZones[glanceZone] = glanceRestore;
            glanceZone = -1;
            glanceRestore = -1;
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
            FaceZones = other.FaceZones == null ? null : (int[])other.FaceZones.Clone();
            FaceZoneMasks = other.FaceZoneMasks == null ? null : (int[])other.FaceZoneMasks.Clone();
            FaceZoneStarts = other.FaceZoneStarts == null ? null : (int[])other.FaceZoneStarts.Clone();
            FaceZoneClassOnly = other.FaceZoneClassOnly == null ? null : (bool[])other.FaceZoneClassOnly.Clone();
            BarFields = other.BarFields == null ? null : (int[])other.BarFields.Clone();
            QuickGlance = other.QuickGlance;
            Normalise();
        }
    }

    /// <summary>
    /// One page that two or more zones are showing at once. The panel says so and does not prevent it,
    /// because a driver watching the relative in two places is a choice and not a mistake.
    ///
    /// Zones are compared by page **id** rather than page number: the four catalogues overlap, so zone
    /// A's track page and module 13 are one drawing under two numbers, and a comparison by number
    /// would miss exactly the duplicate a driver would notice.
    /// </summary>
    public sealed class FacePageClash
    {
        public FacePageClash(string pageId, string pageName, string[] zones)
        {
            PageId = pageId;
            PageName = pageName;
            Zones = zones;
        }

        public string PageId { get; }

        public string PageName { get; }

        /// <summary>The zone letters showing it, in letter order.</summary>
        public string[] Zones { get; }

        /// <summary>"Zone B and zone C both show Relative."</summary>
        public string Message()
        {
            var letters = Zones.Select(z => "zone " + z).ToArray();
            var list = letters.Length == 2
                ? letters[0] + " and " + letters[1]
                : string.Join(", ", letters.Take(letters.Length - 1)) + " and " + letters[letters.Length - 1];
            var verb = letters.Length == 2 ? " both show " : " all show ";
            return char.ToUpperInvariant(list[0]) + list.Substring(1) + verb + PageName + ".";
        }

        public static IReadOnlyList<FacePageClash> Find(OpenDashSettings settings)
        {
            var result = new List<FacePageClash>();
            if (settings == null) return result;
            var order = new List<string>();
            var byPage = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var letter in Contract.FaceZoneLetters)
            {
                var id = FacePages.IdOf(letter, settings.FaceZoneStart(letter));
                if (id == null) continue;
                List<string> zones;
                if (!byPage.TryGetValue(id, out zones))
                {
                    zones = new List<string>();
                    byPage[id] = zones;
                    order.Add(id);
                }
                zones.Add(letter);
            }
            foreach (var id in order)
            {
                var zones = byPage[id];
                if (zones.Count < 2) continue;
                result.Add(new FacePageClash(id, FacePages.NameOf(zones[0], settings.FaceZoneStart(zones[0])), zones.ToArray()));
            }
            return result;
        }

        /// <summary>All messages, one per line; empty when every zone shows something different.</summary>
        public static string Warning(OpenDashSettings settings)
        {
            return string.Join(Environment.NewLine, Find(settings).Select(c => c.Message()));
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
