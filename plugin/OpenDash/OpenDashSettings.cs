// OpenDashSettings.cs: the persisted settings object. A plain POCO that SimHub serialises with Json.NET
// under PluginsData/Common/OpenDash.GeneralSettings.json. Normalise() repairs whatever comes back from disk.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>
    /// The folder record, over the settings, which is where it is persisted.
    /// </summary>
    /// <remarks>
    /// It records but never saves. Applying an update reaches this from a thread-pool thread, and saving there would
    /// run Normalise and serialise the whole settings object while the settings page mutates the same object on the
    /// UI thread: two writers to one file, an enumeration racing an insertion, and arrays replaced under SimHub's own
    /// data thread. Whoever owns the moment saves instead, on a thread where that is safe. The cost of a SimHub that
    /// is killed before that happens is a forgotten fingerprint, and a forgotten fingerprint asks rather than
    /// destroys, which is the direction to fail in.
    /// </remarks>
    public sealed class SettingsFolderRecord : IFolderRecord
    {
        private readonly Func<OpenDashSettings> settings;

        /// <param name="settings">Read each time, since the panel replaces the object when the user changes one.</param>
        public SettingsFolderRecord(Func<OpenDashSettings> settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Guards the dictionary and the save.
        /// </summary>
        /// <remarks>
        /// Applying an update reaches this from a thread-pool thread while the settings page is live on the UI
        /// thread, and Dictionary is not safe for concurrent writers: the classic symptom on .NET Framework is a
        /// later lookup spinning for ever, which a user sees as SimHub hung with nothing in the log. The lock also
        /// keeps a save from serialising the dictionary while another thread is inserting into it.
        /// </remarks>
        private readonly object gate = new object();

        public string Get(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return null;
            lock (gate)
            {
                var map = settings()?.FolderFingerprints;
                if (map == null) return null;
                return map.TryGetValue(folderName, out var value) ? value : null;
            }
        }

        public void Set(string folderName, string fingerprint)
        {
            lock (gate)
            {
                SetLocked(folderName, fingerprint);
            }
        }

        private void SetLocked(string folderName, string fingerprint)
        {
            var current = settings();
            if (current == null || string.IsNullOrEmpty(folderName)) return;
            if (current.FolderFingerprints == null) current.FolderFingerprints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // A fingerprint that could not be computed is not a reason to forget the one we had. Erasing it turned
            // "cannot vouch for this folder" into "this folder is not ours", which is the opposite bias, and the
            // adoption branch would then have recorded whatever was on disk as OpenDash's own work.
            if (string.IsNullOrWhiteSpace(fingerprint)) return;
            current.FolderFingerprints[folderName] = fingerprint;
        }
    }

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

        // --- The rig ------------------------------------------------------------------------------

        /// <summary>
        /// The screens the rig has, by the prefix each one's properties carry: "Face1920x480",
        /// "Face850x480", "Companion", "PitWall". The plugin attaches the properties of these and of no
        /// other screen, so the property list is proportional to the rig rather than to the catalogue.
        /// </summary>
        /// <remarks>
        /// Null until Normalise() fills it, and deliberately not initialised here: Json.NET's default
        /// ObjectCreationHandling adds to a collection a property already holds rather than replacing
        /// it, so a rig of two screens read into a list that already named ten would come back as
        /// twelve. Null therefore means "this file has never named a rig", which is what a file written
        /// before the rig existed looks like, and what Normalise() reads as every screen OpenDash ships.
        /// </remarks>
        public List<string> Screens { get; set; }

        // --- The dash face ---------------------------------------------------------------------

        /// <summary>
        /// What each face is set to, keyed by the prefix its properties carry ("Face1920x480").
        ///
        /// One entry per face rather than one set shared by all of them, because a rig can have a face
        /// on the wheel and another beside it: cycling zone C on one used to move zone C on the other,
        /// and there was no way to give the second screen a different catalogue.
        /// </summary>
        public Dictionary<string, FaceSettings> Faces { get; set; } = new Dictionary<string, FaceSettings>(StringComparer.Ordinal);

        // --- Read from a settings file written before the faces were separate ---------------------
        //
        // These were the whole of the face state when there could only be one face. They are still read
        // so that a driver who configured the zone face in rc.3 does not find it reset, and they are
        // migrated into the reference face and cleared on the first Normalise. Nothing writes them.

        /// <summary>Pre-face page of each zone. Migrated into the reference face, then null.</summary>
        public int[] FaceZones { get; set; }

        /// <summary>Pre-face mask of each zone. Migrated into the reference face, then null.</summary>
        public int[] FaceZoneMasks { get; set; }

        /// <summary>Pre-face start page of each zone. Migrated into the reference face, then null.</summary>
        public int[] FaceZoneStarts { get; set; }

        /// <summary>Pre-face bar fields. Migrated into the reference face, then null.</summary>
        public int[] BarFields { get; set; }

        /// <summary>Pre-face quick glance. Migrated into the reference face, then zero.</summary>
        public int QuickGlance { get; set; }

        /// <summary>Whether OpenDash may ask GitHub what the newest release is. Plugin-local rather than an
        /// [OpenDash.*] property: a dashboard cannot react to it, so ADR 0003's reason for making a setting a
        /// property does not apply. It is read before a request is constructed, so off means nothing is
        /// fetched at all rather than fetched and discarded. On by default; see ADR 0012.</summary>
        public bool CheckForUpdates { get; set; } = true;

        /// <summary>When the last answer came back, as UTC ticks, so that a check is not repeated within the
        /// interval ADR 0012 sets. Zero means never. Persisted so the interval survives a restart.</summary>
        public long LastUpdateCheckTicks { get; set; }

        /// <summary>Fingerprint of each dashboard folder as OpenDash last wrote it, keyed by folder name. An entry
        /// that no longer matches what is on disk is somebody's Dash Studio work; see FolderFingerprint. Kept here
        /// rather than beside the dashboard so that nothing OpenDash writes into DashTemplates can confuse SimHub's
        /// own scanner.</summary>
        public Dictionary<string, string> FolderFingerprints { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            NormaliseScreens();
            NormaliseFace();
        }

        /// <summary>
        /// The rig, repaired: a prefix no screen carries is dropped, a screen named twice is kept once,
        /// and a file that has never named a rig is read as every screen OpenDash ships.
        /// </summary>
        /// <remarks>
        /// Every screen rather than none, because nothing outside this file knows the rig yet: the
        /// installer installs everything the plugin embeds, and the panel that adds a screen and removes
        /// one is XOR-125. So an old settings file attaches exactly what it attached before the rig
        /// existed, and a driver who updates finds nothing reset; what the rig adds today is that the
        /// list can shrink at all.
        /// </remarks>
        private void NormaliseScreens()
        {
            if (Screens == null)
            {
                Screens = new List<string>(Contract.ScreenPrefixes());
                return;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var kept = new List<string>();
            foreach (var screen in Screens)
            {
                if (screen != null && Contract.IsKnownScreen(screen) && seen.Add(screen)) kept.Add(screen);
            }
            Screens = kept;
        }

        /// <summary>
        /// The rig, readable before Normalise() has filled it: a settings object that has never named one
        /// reads as every screen OpenDash ships, which is the same thing NormaliseScreens() writes.
        /// </summary>
        private IEnumerable<string> Rig()
        {
            return Screens ?? Contract.ScreenPrefixes();
        }

        /// <summary>Whether the rig has a screen, by the prefix its properties carry. Safe to call before Normalise().</summary>
        public bool HasScreen(string screen)
        {
            if (screen == null) return false;
            foreach (var known in Rig())
            {
                if (string.Equals(known, screen, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>The faces the rig has, in the order the settings name them. Safe to call before Normalise().</summary>
        public IEnumerable<Contract.FaceSize> RigFaces()
        {
            foreach (var screen in Rig())
            {
                if (Contract.IsKnownFacePrefix(screen)) yield return Contract.FaceForPrefix(screen);
            }
        }

        /// <summary>Every property the plugin attaches for this rig, in attachment order. OpenDash.cs
        /// attaches exactly these, in this order, and ContractTests holds the two together.</summary>
        public IEnumerable<string> DeclaredProperties()
        {
            return Contract.PropertyNames(Rig());
        }

        /// <summary>
        /// Repairs every face's settings, and adopts anything a pre-face settings file carried.
        /// </summary>
        /// <remarks>
        /// The migration runs once and only when the reference face has no entry of its own, so a
        /// driver who configured the zone face before the faces were separate keeps what they set,
        /// and a later save never overwrites the new state with the stale legacy fields.
        ///
        /// A prefix the settings file names that no face ships at is dropped rather than kept: it is
        /// either a face that was removed or a file from a newer version, and in both cases carrying
        /// it forward would attach properties for a face nobody has.
        /// </remarks>
        private void NormaliseFace()
        {
            if (Faces == null) Faces = new Dictionary<string, FaceSettings>(StringComparer.Ordinal);

            var reference = Contract.FacePrefix(Contract.ReferenceFace);
            var legacy = FaceZones != null || FaceZoneMasks != null || FaceZoneStarts != null || BarFields != null || QuickGlance != 0;
            if (legacy && !Faces.ContainsKey(reference))
            {
                Faces[reference] = new FaceSettings
                {
                    Zones = FaceZones,
                    Masks = FaceZoneMasks,
                    Starts = FaceZoneStarts,
                    BarFields = BarFields,
                    QuickGlance = QuickGlance == 0 ? Contract.DefaultQuickGlance : QuickGlance,
                };
            }
            FaceZones = null;
            FaceZoneMasks = null;
            FaceZoneStarts = null;
            BarFields = null;
            QuickGlance = 0;

            foreach (var prefix in new List<string>(Faces.Keys))
            {
                if (!Contract.IsKnownFacePrefix(prefix) || Faces[prefix] == null) Faces.Remove(prefix);
            }
            // A face the rig has gained since the last save gets its group here, which is what makes a
            // screen added later start from the defaults rather than from nothing. A group belonging to
            // a face the rig no longer has is repaired and kept rather than deleted: removing a screen
            // is something a user asks for, and Normalise() runs on every save.
            foreach (var face in RigFaces()) Face(face);
            foreach (var state in Faces.Values) state.Normalise();
        }

        /// <summary>What one face is set to, created with its defaults the first time it is asked for.</summary>
        public FaceSettings Face(Contract.FaceSize face)
        {
            if (Faces == null) Faces = new Dictionary<string, FaceSettings>(StringComparer.Ordinal);
            var prefix = Contract.FacePrefix(face);
            FaceSettings state;
            if (!Faces.TryGetValue(prefix, out state) || state == null)
            {
                state = new FaceSettings();
                Faces[prefix] = state;
            }
            return state;
        }

        /// <summary>Page a face zone is showing, by its letter. Safe to call before Normalise().</summary>
        public int FaceZone(Contract.FaceSize face, string letter)
        {
            return Face(face).Zone(letter);
        }

        /// <summary>Page a face zone opens on, by its letter. Safe to call before Normalise().</summary>
        public int FaceZoneStart(Contract.FaceSize face, string letter)
        {
            return Face(face).Start(letter);
        }

        public void SetFaceZoneStart(Contract.FaceSize face, string letter, int page)
        {
            Face(face).SetStart(letter, page);
        }

        /// <summary>The enabled-page mask of a face zone, by its letter.</summary>
        public int FaceZoneMask(Contract.FaceSize face, string letter)
        {
            return Face(face).Mask(letter);
        }

        /// <summary>Whether a face zone's list pages show the player's own class, by its letter.</summary>
        public bool FaceZoneIsClassOnly(Contract.FaceSize face, string letter)
        {
            return Face(face).IsClassOnly(letter);
        }

        /// <summary>Sets a face zone's class filter.</summary>
        public void SetFaceZoneClassOnly(Contract.FaceSize face, string letter, bool classOnly)
        {
            Face(face).SetClassOnly(letter, classOnly);
        }

        public bool FaceZonePageEnabled(Contract.FaceSize face, string letter, int page)
        {
            return Face(face).PageEnabled(letter, page);
        }

        public void SetFaceZonePageEnabled(Contract.FaceSize face, string letter, int page, bool enabled)
        {
            Face(face).SetPageEnabled(letter, page, enabled);
        }

        /// <summary>Field an end of the bar shows, by its slot name. Safe to call before Normalise().</summary>
        public int BarField(Contract.FaceSize face, string slot)
        {
            return Face(face).BarField(slot);
        }

        public void SetBarField(Contract.FaceSize face, string slot, int field)
        {
            Face(face).SetBarField(slot, field);
        }

        /// <summary>The zone and page a held button shows on this face.</summary>
        public int QuickGlanceOf(Contract.FaceSize face)
        {
            return Contract.NormaliseQuickGlance(Face(face).QuickGlance);
        }

        public void SetQuickGlance(Contract.FaceSize face, int value)
        {
            Face(face).QuickGlance = Contract.NormaliseQuickGlance(value);
        }

        /// <summary>Puts every zone of every face the rig has on the page it opens on, once, when the plugin starts.</summary>
        public void OpenOnStartPages()
        {
            foreach (var face in RigFaces()) Face(face).OpenOnStartPages();
        }

        /// <summary>Advances one zone of one face to its next enabled page and returns it.</summary>
        public int CycleFaceZone(Contract.FaceSize face, string letter)
        {
            return Face(face).Cycle(letter);
        }

        /// <summary>Whether a glance is being held on any face.</summary>
        public bool GlanceHeld
        {
            get
            {
                foreach (var face in RigFaces())
                {
                    if (Face(face).GlanceHeld) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Begins the glance on every face at once.
        /// </summary>
        /// <remarks>
        /// One button, every face: a driver holding the glance button is looking at one screen, but
        /// which one is not knowable from here, and moving only the reference face would leave the
        /// screen they were actually looking at unchanged. Each face glances to its own configured
        /// zone and page, so a second screen can be set to glance at something else entirely.
        /// </remarks>
        public void BeginQuickGlance()
        {
            foreach (var face in RigFaces()) Face(face).BeginQuickGlance();
        }

        public void EndQuickGlance()
        {
            foreach (var face in RigFaces()) Face(face).EndQuickGlance();
        }

        /// <summary>Zones of one face showing the same page as another, which the panel says and allows.</summary>
        public IReadOnlyList<FacePageClash> FaceClashes(Contract.FaceSize face) => FacePageClash.Find(Face(face));

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
            Screens = other.Screens == null ? null : new List<string>(other.Screens);
            Slots = other.Slots == null ? null : (int[])other.Slots.Clone();
            Modules = other.Modules == null ? null : (bool[])other.Modules.Clone();
            Zones = other.Zones == null ? null : (int[])other.Zones.Clone();
            WideZone = other.WideZone;
            WebViewUrl = other.WebViewUrl;
            // Cloned rather than shared, so that the panel writing into its copy does not reach back
            // into the settings the plugin is reading from.
            Faces = new Dictionary<string, FaceSettings>(StringComparer.Ordinal);
            if (other.Faces != null)
            {
                foreach (var entry in other.Faces)
                {
                    if (entry.Value != null) Faces[entry.Key] = entry.Value.Clone();
                }
            }
            FaceZones = other.FaceZones == null ? null : (int[])other.FaceZones.Clone();
            FaceZoneMasks = other.FaceZoneMasks == null ? null : (int[])other.FaceZoneMasks.Clone();
            FaceZoneStarts = other.FaceZoneStarts == null ? null : (int[])other.FaceZoneStarts.Clone();
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

        public static IReadOnlyList<FacePageClash> Find(FaceSettings face)
        {
            var result = new List<FacePageClash>();
            if (face == null) return result;
            var order = new List<string>();
            var byPage = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var letter in Contract.FaceZoneLetters)
            {
                var id = FacePages.IdOf(letter, face.Start(letter));
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
                result.Add(new FacePageClash(id, FacePages.NameOf(zones[0], face.Start(zones[0])), zones.ToArray()));
            }
            return result;
        }

        /// <summary>All messages, one per line; empty when every zone shows something different.</summary>
        public static string Warning(FaceSettings face)
        {
            return string.Join(Environment.NewLine, Find(face).Select(c => c.Message()));
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
