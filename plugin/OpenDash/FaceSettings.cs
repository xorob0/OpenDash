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

        /// <summary>
        /// The order each zone cycles in, index 0 is zone A: every page of the zone's catalogue once.
        /// </summary>
        /// <remarks>
        /// The driver's since #791, where the Screens page lets a zone's pages be dragged into the order
        /// the button should step through them. The mask still says which pages are in the cycle; this
        /// says in what order. A file written before it has none and reads the catalogue's own order,
        /// which is what every zone cycled in before, so nothing moves until somebody reorders a zone.
        /// </remarks>
        public int[][] Orders { get; set; } = Contract.DefaultFaceZoneOrders();

        private int glanceZone = -1;
        private int glanceRestore = -1;

        /// <summary>
        /// The id of the theme the face is drawn in, or null for the default look: what decides band D's
        /// catalogue, nine pages on a Porsche face and eight on every other (#718).
        /// </summary>
        /// <remarks>
        /// A field and two methods rather than a property, so that Json.NET leaves it out of the file: the
        /// theme is the screen's (<see cref="ScreenInstance.Theme"/>), which hands it here, and a settings file
        /// written before #718 is the same file after it.
        /// </remarks>
        private string theme;

        /// <summary>The theme the face is drawn in, or null for the default look.</summary>
        public string ThemeId() { return theme; }

        /// <summary>Says which theme the face is drawn in. Changes nothing stored: <see cref="Normalise"/> is
        /// what fits the zones to the theme's catalogues, and every reader clamps in the meantime.</summary>
        public void UseTheme(string themeId)
        {
            theme = string.IsNullOrEmpty(themeId) ? null : themeId;
        }

        /// <summary>
        /// A face as it starts on a screen of a theme: every zone on its default page with every page
        /// enabled, band D on the theme's first band page where it has one.
        /// </summary>
        public static FaceSettings For(string themeId)
        {
            var face = new FaceSettings();
            face.UseTheme(themeId);
            face.Zones = Contract.DefaultFaceZones(face.theme);
            face.Masks = Contract.DefaultFaceZoneMasks(face.theme);
            face.Starts = Contract.DefaultFaceZones(face.theme);
            face.Orders = Contract.DefaultFaceZoneOrders(face.theme);
            return face;
        }

        /// <summary>A zone's catalogue on this face, by its letter: band D's carries the theme's band pages.</summary>
        public IReadOnlyList<ZonePage> Pages(string letter)
        {
            return FacePages.For(letter, theme);
        }

        /// <summary>The name of a page of a zone of this face, or "Page n" outside its catalogue.</summary>
        public string PageName(string letter, int page)
        {
            return FacePages.NameOf(letter, page, theme);
        }

        /// <summary>The id of a page of a zone of this face, or null outside its catalogue.</summary>
        public string PageId(string letter, int page)
        {
            return FacePages.IdOf(letter, page, theme);
        }

        /// <summary>How many pages a zone of this face can show, by its index.</summary>
        private int PageCount(int index)
        {
            return Contract.FaceZonePageCount(index, theme);
        }

        /// <summary>The page a zone of this face opens on by default, by its index.</summary>
        private int DefaultPage(int index)
        {
            return Contract.DefaultFaceZonePage(index, theme);
        }

        /// <summary>The glance, or the default where it names a page this face does not carry.</summary>
        public int NormalisedQuickGlance()
        {
            return Contract.NormaliseQuickGlance(QuickGlance, theme);
        }

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
            var zones = Contract.DefaultFaceZones(theme);
            var masks = Contract.DefaultFaceZoneMasks(theme);
            var starts = Contract.DefaultFaceZones(theme);
            var orders = new int[zones.Length][];
            for (var i = 0; i < zones.Length; i++)
            {
                var pages = PageCount(i);
                var all = Contract.DefaultZoneMask(i, theme);

                orders[i] = Contract.NormaliseOrder(Orders != null && i < Orders.Length ? Orders[i] : null, pages);

                if (Masks != null && i < Masks.Length) masks[i] = Masks[i] & all;
                if (masks[i] == 0) masks[i] = all;

                // Forward in the zone's own order, which is where its button would carry on to.
                if (Starts != null && i < Starts.Length) starts[i] = Contract.NormalisePage(Starts[i], pages, DefaultPage(i));
                starts[i] = Contract.FirstEnabledInOrder(starts[i], masks[i], orders[i]);

                if (Zones != null && i < Zones.Length) zones[i] = Contract.NormalisePage(Zones[i], pages, starts[i]);
                zones[i] = Contract.FirstEnabledInOrder(zones[i], masks[i], orders[i]);
            }
            Zones = zones;
            Masks = masks;
            Starts = starts;
            Orders = orders;

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

            QuickGlance = NormalisedQuickGlance();
        }

        /// <summary>Page a zone is showing, by its letter. Safe to call before Normalise().</summary>
        public int Zone(string letter)
        {
            var index = ZoneIndex(letter);
            if (Zones == null || index >= Zones.Length) return DefaultPage(index);
            return Contract.NormalisePage(Zones[index], PageCount(index), DefaultPage(index));
        }

        /// <summary>Page a zone opens on, by its letter. Safe to call before Normalise().</summary>
        public int Start(string letter)
        {
            var index = ZoneIndex(letter);
            if (Starts == null || index >= Starts.Length) return DefaultPage(index);
            return Contract.NormalisePage(Starts[index], PageCount(index), DefaultPage(index));
        }

        /// <summary>Sets the page a zone opens on, and the page it is showing with it: the panel is in
        /// front of a running dash, and a start page that only takes effect next time reads as broken.</summary>
        public void SetStart(string letter, int page)
        {
            var index = ZoneIndex(letter);
            EnsureArrays();
            var clamped = Contract.NormalisePage(page, PageCount(index), DefaultPage(index));
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
            var all = Contract.DefaultZoneMask(index, theme);
            if (Masks == null || index >= Masks.Length) return all;
            var mask = Masks[index] & all;
            return mask == 0 ? all : mask;
        }

        /// <summary>The order a zone cycles in, by its letter: every page of its catalogue once. Safe to
        /// call before Normalise(), and a copy, so the caller cannot edit the zone through it.</summary>
        public int[] Order(string letter)
        {
            var index = ZoneIndex(letter);
            var stored = Orders != null && index < Orders.Length ? Orders[index] : null;
            return Contract.NormaliseOrder(stored, PageCount(index));
        }

        /// <summary>
        /// Sets the order a zone cycles in. What the order leaves out is appended, and a repeated or
        /// unknown page is dropped, so any list the panel hands over is a whole order.
        /// </summary>
        /// <remarks>
        /// The page the zone is showing and the page it opens on stay where they are: reordering
        /// changes where the button goes next, not what is on the screen.
        /// </remarks>
        public void SetOrder(string letter, int[] order)
        {
            var index = ZoneIndex(letter);
            EnsureArrays();
            Orders[index] = Contract.NormaliseOrder(order, PageCount(index));
        }

        /// <summary>
        /// Where the page a zone is showing sits in its cycle, counting from one: the enabled pages
        /// before it in the zone's order, plus itself. What <see cref="Contract.ZonePositionProperty(string, string)"/> publishes.
        /// </summary>
        /// <remarks>
        /// The same count zoneCyclePosition makes in contract.ts, taken in the zone's own order rather
        /// than the catalogue's. A page the mask has turned off -- which a held glance can show -- still
        /// counts itself, so it reads as the one after the enabled pages before it.
        ///
        /// A dashboard reads this for every zone of every face each frame, so it walks the stored order
        /// in place when that is already whole, which Normalise and SetOrder see to, and builds the
        /// repaired copy <see cref="Order"/> hands out only for a file nothing has normalised yet.
        /// </remarks>
        public int Position(string letter)
        {
            var index = ZoneIndex(letter);
            var page = Zone(letter);
            var mask = Mask(letter);
            var stored = Orders != null && index < Orders.Length ? Orders[index] : null;
            var order = IsWholeOrder(stored, PageCount(index)) ? stored : Order(letter);
            var position = 1;
            foreach (var candidate in order)
            {
                if (candidate == page) break;
                if ((mask & (1 << candidate)) != 0) position++;
            }
            return position;
        }

        /// <summary>Whether an order holds every page of a catalogue of that many exactly once, which is
        /// what <see cref="Contract.NormaliseOrder"/> would leave as it is. Allocates nothing.</summary>
        private static bool IsWholeOrder(int[] order, int count)
        {
            if (order == null || order.Length != count || count > 64) return false;
            var seen = 0UL;
            foreach (var page in order)
            {
                if (page < 0 || page >= count) return false;
                var bit = 1UL << page;
                if ((seen & bit) != 0) return false;
                seen |= bit;
            }
            return true;
        }

        public bool PageEnabled(string letter, int page)
        {
            var index = ZoneIndex(letter);
            if (page < 0 || page >= PageCount(index)) return false;
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
            if (page < 0 || page >= PageCount(index)) throw new ArgumentOutOfRangeException("page");
            EnsureArrays();
            var mask = Mask(letter);
            var next = enabled ? mask | (1 << page) : mask & ~(1 << page);
            if (next == 0) return;
            Masks[index] = next;
            Starts[index] = Contract.FirstEnabledInOrder(Starts[index], next, Orders[index]);
            Zones[index] = Contract.FirstEnabledInOrder(Zones[index], next, Orders[index]);
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
        /// Advances a zone to its next enabled page in its own order and returns it. The mask is what
        /// sets the length of the cycle, so a zone with one page enabled stays where it is rather than
        /// flickering.
        /// </summary>
        public int Cycle(string letter)
        {
            var index = ZoneIndex(letter);
            EnsureArrays();
            var next = Contract.FirstEnabledAfter(Zone(letter), Mask(letter), Orders[index]);
            Zones[index] = next;
            return next;
        }

        /// <summary>
        /// Moves a zone back to its previous enabled page and returns it: the partner of <see
        /// cref="Cycle"/>, for a driver who pressed once too often. A zone with one page enabled stays
        /// where it is, as it does going forward.
        /// </summary>
        public int CycleBack(string letter)
        {
            var index = ZoneIndex(letter);
            EnsureArrays();
            var previous = Contract.LastEnabledBefore(Zone(letter), Mask(letter), Orders[index]);
            Zones[index] = previous;
            return previous;
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
            var glance = NormalisedQuickGlance();
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
                Orders = CloneOrders(Orders),
                theme = theme,
            };
        }

        private static int[][] CloneOrders(int[][] orders)
        {
            if (orders == null) return null;
            var copy = new int[orders.Length][];
            for (var i = 0; i < orders.Length; i++) copy[i] = (int[])orders[i]?.Clone();
            return copy;
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
                || ClassOnly == null || ClassOnly.Length != zones
                || !OrdersAreWhole())
            {
                Normalise();
            }
        }

        /// <summary>Whether every zone holds an order of its whole catalogue, every page once, which is
        /// what the cycle steps through; a hand-edited or partly read file, or an order assigned with a
        /// page twice, is repaired before anything steps. Allocates nothing.</summary>
        private bool OrdersAreWhole()
        {
            if (Orders == null || Orders.Length != Contract.FaceZoneLetters.Length) return false;
            for (var i = 0; i < Orders.Length; i++)
            {
                if (!IsWholeOrder(Orders[i], PageCount(i))) return false;
            }
            return true;
        }

        /// <summary>Zones of this face showing the same page as another zone, which the panel says and allows.</summary>
        public IReadOnlyList<FacePageClash> Clashes()
        {
            return FacePageClash.Find(this);
        }
    }
}
