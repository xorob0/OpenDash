// FaceSettings.cs: what one face is set to, separately from every other face on the rig.
//
// The zone settings used to be four flat arrays on OpenDashSettings, which was correct while there
// could only be one face. A rig with a 1920 face on the wheel and an 850 face beside it configured
// them together: cycling zone C on one moved zone C on the other, and there was no way to give the
// second screen a different catalogue. So the state moves here and OpenDashSettings holds one of
// these per face, keyed by the prefix its properties carry.
//
// Everything in this file is the logic that was already in OpenDashSettings, unchanged in
// behaviour. What changed is who owns it.
using System;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    /// <summary>The zones, the bar and the glance of one face.</summary>
    public sealed class FaceSettings
    {
        /// <summary>Page each zone is showing, index 0 is zone A. Live state, written by a wheel button.</summary>
        public int[] Zones { get; set; } = Contract.DefaultFaceZones();

        /// <summary>Which pages of each zone are enabled, as a bit mask. This is what sets the length of a cycle.</summary>
        public int[] Masks { get; set; } = Contract.DefaultFaceZoneMasks();

        /// <summary>Page each zone opens on.</summary>
        public int[] Starts { get; set; } = Contract.DefaultFaceZones();

        /// <summary>Whether each zone's list pages show the player's own class rather than the whole
        /// field, index 0 is zone A. Per zone rather than per face, because the point of it is zone B
        /// listing the race and zone C listing the class a driver is actually in.</summary>
        public bool[] ClassOnly { get; set; } = Contract.DefaultFaceZoneClassOnly();

        /// <summary>Field each end of the bar shows, in Contract.BarSlots order.</summary>
        public int[] BarFields { get; set; } = Contract.DefaultBarSlots();

        /// <summary>The zone and page a held button shows, encoded as zoneIndex * 100 + page.</summary>
        public int QuickGlance { get; set; } = Contract.DefaultQuickGlance;

        private int glanceZone = -1;
        private int glanceRestore = -1;

        /// <summary>Whether a glance is being held on this face; a second press while one is does nothing.</summary>
        public bool GlanceHeld { get { return glanceZone >= 0; } }

        /// <summary>
        /// Every value made consistent: a mask trimmed to the pages that exist and refilled when empty,
        /// a start page that exists and is enabled, and a current page that exists and is enabled.
        ///
        /// The last rule is the one that matters in use: turning off the page a zone is sitting on would
        /// otherwise leave the zone showing something its own cycle can never return to.
        /// </summary>
        public void Normalise()
        {
            var zones = Contract.DefaultFaceZones();
            var masks = Contract.DefaultFaceZoneMasks();
            var starts = Contract.DefaultFaceZones();
            for (var i = 0; i < zones.Length; i++)
            {
                var pages = Contract.FaceZonePageCounts[i];
                var all = Contract.DefaultZoneMask(i);

                if (Masks != null && i < Masks.Length) masks[i] = Masks[i] & all;
                if (masks[i] == 0) masks[i] = all;

                if (Starts != null && i < Starts.Length) starts[i] = Contract.NormalisePage(Starts[i], pages, Contract.DefaultFaceZonePages[i]);
                starts[i] = Contract.FirstEnabledFrom(starts[i], masks[i], pages);

                if (Zones != null && i < Zones.Length) zones[i] = Contract.NormalisePage(Zones[i], pages, starts[i]);
                zones[i] = Contract.FirstEnabledFrom(zones[i], masks[i], pages);
            }
            Zones = zones;
            Masks = masks;
            Starts = starts;

            var classOnly = Contract.DefaultFaceZoneClassOnly();
            if (ClassOnly != null)
            {
                for (var i = 0; i < classOnly.Length && i < ClassOnly.Length; i++) classOnly[i] = ClassOnly[i];
            }
            ClassOnly = classOnly;

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

        /// <summary>Page a zone is showing, by its letter. Safe to call before Normalise().</summary>
        public int Zone(string letter)
        {
            var index = ZoneIndex(letter);
            if (Zones == null || index >= Zones.Length) return Contract.DefaultFaceZonePages[index];
            return Contract.NormalisePage(Zones[index], Contract.FaceZonePageCounts[index], Contract.DefaultFaceZonePages[index]);
        }

        /// <summary>Page a zone opens on, by its letter. Safe to call before Normalise().</summary>
        public int Start(string letter)
        {
            var index = ZoneIndex(letter);
            if (Starts == null || index >= Starts.Length) return Contract.DefaultFaceZonePages[index];
            return Contract.NormalisePage(Starts[index], Contract.FaceZonePageCounts[index], Contract.DefaultFaceZonePages[index]);
        }

        /// <summary>Sets the page a zone opens on, and the page it is showing with it: the panel is in
        /// front of a running dash, and a start page that only takes effect next time reads as broken.</summary>
        public void SetStart(string letter, int page)
        {
            var index = ZoneIndex(letter);
            EnsureArrays();
            var clamped = Contract.NormalisePage(page, Contract.FaceZonePageCounts[index], Contract.DefaultFaceZonePages[index]);
            Starts[index] = clamped;
            Zones[index] = clamped;
            SetPageEnabled(letter, clamped, true);
        }

        /// <summary>Whether a zone's list pages show the player's own class. Safe to call before Normalise().</summary>
        public bool IsClassOnly(string letter)
        {
            var index = ZoneIndex(letter);
            if (ClassOnly == null || index >= ClassOnly.Length) return Contract.DefaultZoneClassOnly;
            return ClassOnly[index];
        }

        /// <summary>Sets a zone's class filter.</summary>
        public void SetClassOnly(string letter, bool classOnly)
        {
            var index = ZoneIndex(letter);
            EnsureArrays();
            ClassOnly[index] = classOnly;
        }

        /// <summary>The enabled-page mask of a zone, by its letter.</summary>
        public int Mask(string letter)
        {
            var index = ZoneIndex(letter);
            if (Masks == null || index >= Masks.Length) return Contract.DefaultZoneMask(index);
            var mask = Masks[index] & Contract.DefaultZoneMask(index);
            return mask == 0 ? Contract.DefaultZoneMask(index) : mask;
        }

        public bool PageEnabled(string letter, int page)
        {
            var index = ZoneIndex(letter);
            if (page < 0 || page >= Contract.FaceZonePageCounts[index]) return false;
            return (Mask(letter) & (1 << page)) != 0;
        }

        /// <summary>
        /// Turns one page of a zone on or off. Turning off the last enabled page is refused rather than
        /// obeyed: a zone with an empty cycle has nothing to draw, and the panel would have to invent a
        /// page to show anyway.
        /// </summary>
        public void SetPageEnabled(string letter, int page, bool enabled)
        {
            var index = ZoneIndex(letter);
            if (page < 0 || page >= Contract.FaceZonePageCounts[index]) throw new ArgumentOutOfRangeException("page");
            EnsureArrays();
            var mask = Mask(letter);
            var next = enabled ? mask | (1 << page) : mask & ~(1 << page);
            if (next == 0) return;
            Masks[index] = next;
            Starts[index] = Contract.FirstEnabledFrom(Starts[index], next, Contract.FaceZonePageCounts[index]);
            Zones[index] = Contract.FirstEnabledFrom(Zones[index], next, Contract.FaceZonePageCounts[index]);
        }

        /// <summary>Field an end of the bar shows, by its slot name. Safe to call before Normalise().</summary>
        public int BarField(string slot)
        {
            var index = BarIndex(slot);
            if (BarFields == null || index >= BarFields.Length) return Contract.DefaultBarFields[index];
            return Contract.NormalisePage(BarFields[index], Contract.BarFieldCount, Contract.DefaultBarFields[index]);
        }

        public void SetBarField(string slot, int field)
        {
            var index = BarIndex(slot);
            if (BarFields == null || BarFields.Length != Contract.BarSlots.Length) Normalise();
            BarFields[index] = Contract.NormalisePage(field, Contract.BarFieldCount, Contract.DefaultBarFields[index]);
        }

        /// <summary>
        /// Puts every zone on the page it opens on. Called once when the plugin starts, because that is
        /// what "the page the zone opens on" means: a session begins where the driver set it to, not
        /// where they happened to leave it three races ago.
        ///
        /// Cycling therefore does not save. The page a zone is showing is live state, and the only
        /// restart it has to survive is the dashboard window's, which it does, because the page lives
        /// in the plugin and not in the dash.
        /// </summary>
        public void OpenOnStartPages()
        {
            EnsureArrays();
            for (var i = 0; i < Zones.Length; i++) Zones[i] = Starts[i];
        }

        /// <summary>
        /// Advances a zone to its next enabled page and returns it. The mask is what sets the length of
        /// the cycle, so a zone with one page enabled stays where it is rather than flickering.
        /// </summary>
        public int Cycle(string letter)
        {
            var index = ZoneIndex(letter);
            EnsureArrays();
            var count = Contract.FaceZonePageCounts[index];
            var next = Contract.FirstEnabledFrom((Zone(letter) + 1) % count, Mask(letter), count);
            Zones[index] = next;
            return next;
        }

        /// <summary>
        /// Shows the glance page in its zone, remembering what was there.
        ///
        /// The page does not have to be one the mask enables. A glance is an explicit thing a driver
        /// asked for by holding a button, and the mask is about what the cycle steps through; the two
        /// are different questions, and the track map is exactly the page somebody would want held and
        /// not cycled to.
        /// </summary>
        public void BeginQuickGlance()
        {
            if (GlanceHeld) return;
            EnsureArrays();
            var glance = Contract.NormaliseQuickGlance(QuickGlance);
            var zone = Contract.QuickGlanceZone(glance);
            glanceZone = zone;
            glanceRestore = Zones[zone];
            Zones[zone] = Contract.QuickGlancePage(glance);
        }

        /// <summary>Puts the zone back where it was. A release with no press does nothing.</summary>
        public void EndQuickGlance()
        {
            if (!GlanceHeld) return;
            EnsureArrays();
            Zones[glanceZone] = glanceRestore;
            glanceZone = -1;
            glanceRestore = -1;
        }

        /// <summary>A copy, for the panel, which keeps one instance alive and writes into it.</summary>
        public FaceSettings Clone()
        {
            return new FaceSettings
            {
                Zones = (int[])Zones?.Clone(),
                Masks = (int[])Masks?.Clone(),
                Starts = (int[])Starts?.Clone(),
                ClassOnly = (bool[])ClassOnly?.Clone(),
                BarFields = (int[])BarFields?.Clone(),
                QuickGlance = QuickGlance,
            };
        }

        private static int ZoneIndex(string letter)
        {
            var index = Array.IndexOf(Contract.FaceZoneLetters, letter);
            if (index < 0) throw new ArgumentOutOfRangeException("letter");
            return index;
        }

        private static int BarIndex(string slot)
        {
            var index = Array.IndexOf(Contract.BarSlots, slot);
            if (index < 0) throw new ArgumentOutOfRangeException("slot");
            return index;
        }

        private void EnsureArrays()
        {
            var zones = Contract.FaceZoneLetters.Length;
            if (Zones == null || Zones.Length != zones
                || Masks == null || Masks.Length != zones
                || Starts == null || Starts.Length != zones
                || ClassOnly == null || ClassOnly.Length != zones)
            {
                Normalise();
            }
        }

        /// <summary>Zones of this face showing the same page as another zone, which the panel says and allows.</summary>
        public IReadOnlyList<FacePageClash> Clashes()
        {
            return FacePageClash.Find(this);
        }
    }
}
