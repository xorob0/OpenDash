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
        /// <summary>Whether the segments are SimHub's shift lights. Deprecated by <see cref="RevBar"/>,
        /// kept as its alias and kept attached, because it has shipped and README publishes it as a
        /// property an LED profile may read. Normalise() keeps the two agreeing.</summary>
        public bool ShiftLights { get; set; } = Contract.DefaultShiftLights;

        /// <summary>
        /// What the top of a rectangular face carries: "shift", "rpm" or "off".
        ///
        /// Null rather than defaulted, so that a settings file written before the mode existed is
        /// recognisable as one: an initialiser here would make an rc.2 file that says
        /// <c>ShiftLights: false</c> indistinguishable from one that asked for the shift lights, and
        /// that user would find them switched back on. Normalise() fills it in.
        /// </summary>
        public string RevBar { get; set; }

        public string PositionMode { get; set; } = Contract.DefaultPositionMode;

        public string DeltaReference { get; set; } = Contract.DefaultDeltaReference;

        public string SessionProgress { get; set; } = Contract.DefaultSessionProgress;

        /// <summary>Card number per slot, index 0 is slot 1. Always Contract.SlotCount long after Normalise().</summary>
        public int[] Slots { get; set; } = Contract.DefaultSlots();

        // --- Read from a settings file written before a screen was an instance ------------------
        //
        // These were the whole of the companion's and the pit wall's state when there could be one of
        // each. Since ADR 0017 they live on the screen that has them (ScreenInstance), and these are
        // read once by MigratedRig() and never written again. They keep their initialisers so that a
        // migration finds the defaults rather than nulls.

        /// <summary>Pre-rig companion rotation. Migrated onto the companion screen.</summary>
        public bool[] Modules { get; set; } = Contract.DefaultModules();

        /// <summary>Pre-rig pit wall zones. Migrated onto the pit wall screen.</summary>
        public int[] Zones { get; set; } = Contract.PitWallDefaultZones();

        /// <summary>Pre-rig wide zone. Migrated onto the pit wall screen.</summary>
        public int WideZone { get; set; } = Contract.DefaultWideZonePage;

        /// <summary>Pre-rig web view address. Migrated onto the pit wall screen.</summary>
        public string WebViewUrl { get; set; } = Contract.DefaultWebViewUrl;

        // --- The lights ------------------------------------------------------------------------
        //
        // Brightness and night mode are the rig's, not this box's: a driver who owns a flag box
        // probably owns other lights. Then the flag box's own, then the two an RGB strip reads.
        // ADR 0013.

        public int LightsBrightness { get; set; } = Contract.DefaultLightsBrightness;

        public int LightsNightBrightness { get; set; } = Contract.DefaultLightsNightBrightness;

        public bool LightsNightMode { get; set; } = Contract.DefaultLightsNightMode;

        public bool FlagBoxCriticalOnly { get; set; } = Contract.DefaultFlagBoxCriticalOnly;

        public bool FlagBoxGear { get; set; } = Contract.DefaultFlagBoxGear;

        public int FlagBoxLowFuelLaps { get; set; } = Contract.DefaultFlagBoxLowFuelLaps;

        /// <summary>Zero means "not set", so that the profile's own per-unit default applies. A driver in
        /// Fahrenheit who has never opened this page must not get a Celsius number.</summary>
        public int FlagBoxOilTemp { get; set; }

        public int FlagBoxWaterTemp { get; set; }

        /// <summary>Per matrix, index 0 is matrix 1. Always four long after Normalise().</summary>
        public string[] FlagBoxRest { get; set; } = Contract.DefaultFlagBoxRests();

        public bool[] FlagBoxFlags { get; set; } = Contract.DefaultFlagBoxOn();

        /// <summary>The limiter, the lane and speeding. Its own switch, not the flags'.</summary>
        public bool[] FlagBoxPit { get; set; } = Contract.DefaultFlagBoxOn();

        public bool[] FlagBoxSpotter { get; set; } = Contract.DefaultFlagBoxOn();

        public bool[] FlagBoxWarnings { get; set; } = Contract.DefaultFlagBoxOn();

        public string[] FlagBoxSide { get; set; } = Contract.DefaultFlagBoxSides();

        /// <summary>What the middle of an RGB strip shows: "rpm", "brake", "throttleBrake" or "fuel".
        /// One value for the rig and not an array, because openDash generates one profile per strip
        /// shape rather than per device and every shape reads this one name.</summary>
        public string LedCentre { get; set; } = Contract.DefaultLedCentre;

        /// <summary>How the rev ladder fills a strip: "leftToRight", "meetInMiddle" or "f1". The look
        /// only; the thresholds are the car's own whichever is set (ADR 0014).</summary>
        public string LedRpmStyle { get; set; } = Contract.DefaultLedRpmStyle;

        /// <summary>Whether a flag on a strip moves. Off holds every flag from the frame it would have
        /// settled on and never turns one off.</summary>
        public bool LedFlagAnimation { get; set; } = Contract.DefaultLedFlagAnimation;

        /// <summary>One matrix's settings, 1-based, repaired if the array came back short.</summary>
        public string MatrixRest(int matrix) => Pick(FlagBoxRest, matrix, Contract.DefaultFlagBoxMatrixRest(matrix));

        public bool MatrixFlags(int matrix) => Pick(FlagBoxFlags, matrix, Contract.DefaultFlagBoxMatrixOn(matrix));

        public bool MatrixPit(int matrix) => Pick(FlagBoxPit, matrix, Contract.DefaultFlagBoxMatrixOn(matrix));

        public bool MatrixSpotter(int matrix) => Pick(FlagBoxSpotter, matrix, Contract.DefaultFlagBoxMatrixOn(matrix));

        public bool MatrixWarnings(int matrix) => Pick(FlagBoxWarnings, matrix, Contract.DefaultFlagBoxMatrixOn(matrix));

        public string MatrixSide(int matrix) => Pick(FlagBoxSide, matrix, Contract.DefaultFlagBoxSide);

        private static T Pick<T>(T[] values, int matrix, T fallback)
        {
            if (values == null || matrix < 1 || matrix > values.Length) return fallback;
            var value = values[matrix - 1];
            return value == null ? fallback : value;
        }

        /// <summary>Repairs the per-matrix arrays, whatever came back from disk.</summary>
        private void NormaliseLights()
        {
            LightsBrightness = Contract.NormaliseBrightness(LightsBrightness);
            LightsNightBrightness = Contract.NormaliseBrightness(LightsNightBrightness);
            if (FlagBoxLowFuelLaps < 0) FlagBoxLowFuelLaps = Contract.DefaultFlagBoxLowFuelLaps;
            if (FlagBoxOilTemp < 0) FlagBoxOilTemp = 0;
            if (FlagBoxWaterTemp < 0) FlagBoxWaterTemp = 0;
            FlagBoxRest = Resize(FlagBoxRest, Contract.DefaultFlagBoxRests(), v => Array.IndexOf(Contract.FlagBoxRests, v) >= 0);
            FlagBoxSide = Resize(FlagBoxSide, Contract.DefaultFlagBoxSides(), v => Array.IndexOf(Contract.FlagBoxSides, v) >= 0);
            FlagBoxFlags = Resize(FlagBoxFlags, Contract.DefaultFlagBoxOn(), v => true);
            FlagBoxPit = Resize(FlagBoxPit, Contract.DefaultFlagBoxOn(), v => true);
            FlagBoxSpotter = Resize(FlagBoxSpotter, Contract.DefaultFlagBoxOn(), v => true);
            FlagBoxWarnings = Resize(FlagBoxWarnings, Contract.DefaultFlagBoxOn(), v => true);
            // No array to repair: the strips carry one value each for the whole rig. A profile reads
            // both through isnull() with its own default, so an unrecognised spelling has to become a
            // legal one here rather than reaching the strip as itself.
            LedCentre = Contract.NormaliseLedCentre(LedCentre);
            LedRpmStyle = Contract.NormaliseChoice(LedRpmStyle, Contract.LedRpmStyles, Contract.DefaultLedRpmStyle);
        }

        private static T[] Resize<T>(T[] values, T[] defaults, Func<T, bool> valid)
        {
            var result = (T[])defaults.Clone();
            if (values == null) return result;
            for (var i = 0; i < result.Length && i < values.Length; i++)
            {
                if (values[i] != null && valid(values[i])) result[i] = values[i];
            }
            return result;
        }
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

        /// <summary>
        /// The rig: every screen the user has, in the order they were added.
        /// </summary>
        /// <remarks>
        /// This replaces Screens and Faces together, and ADR 0017 is why. A screen is no longer a
        /// resolution but an instance with a name, a kind, a size and a namespace of its own, so a rig
        /// may hold two screens of one size and configure them apart.
        ///
        /// Null until Normalise() fills it, and deliberately not initialised here, for the reason
        /// Screens gives: Json.NET's default ObjectCreationHandling adds to a collection a property
        /// already holds rather than replacing it. Null therefore means "this file has never named a
        /// rig", which is what a file written before ADR 0017 looks like, and is the signal MigrateRig()
        /// reads.
        /// </remarks>
        public List<ScreenInstance> Rig { get; set; }

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
            RevBar = RevBarMode();
            ShiftLights = RevBar == Contract.RevBarShift;
            PositionMode = Contract.NormaliseChoice(PositionMode, Contract.PositionModes, Contract.DefaultPositionMode);
            DeltaReference = Contract.NormaliseChoice(DeltaReference, Contract.DeltaReferences, Contract.DefaultDeltaReference);
            SessionProgress = Contract.NormaliseChoice(SessionProgress, Contract.SessionProgressModes, Contract.DefaultSessionProgress);
            NormaliseLights();

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
            NormaliseRig();
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
        private IEnumerable<string> LegacyScreenPrefixes()
        {
            return Screens ?? Contract.ScreenPrefixes();
        }

        /// <summary>Whether the rig has a screen, by the prefix its properties carry. Safe to call before Normalise().</summary>
        public bool HasScreen(string screen)
        {
            if (screen == null) return false;
            foreach (var known in LegacyScreenPrefixes())
            {
                if (string.Equals(known, screen, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>The faces the rig has, in the order the settings name them. Safe to call before Normalise().</summary>
        public IEnumerable<Contract.FaceSize> RigFaces()
        {
            foreach (var screen in LegacyScreenPrefixes())
            {
                if (Contract.IsKnownFacePrefix(screen)) yield return Contract.FaceForPrefix(screen);
            }
        }

        /// <summary>Every property the plugin attaches for this rig, in attachment order. OpenDash.cs
        /// attaches exactly these, in this order, and ContractTests holds the two together.</summary>
        public IEnumerable<string> DeclaredProperties()
        {
            return Contract.PropertyNames(RigScreens());
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
            foreach (var state in Faces.Values) state.Normalise();
            // Nothing else repairs or reads this dictionary. Since ADR 0017 a face's settings live on
            // the screen that has them, and Faces is what a settings file written before that looks
            // like: MigratedRig() empties it into the rig on the next line, and NormaliseRig() clears
            // it once it has. Keeping a second copy in step was how the panel and the plugin came to
            // disagree about what zone B was showing.
        }

        // --- The rig, as ADR 0017 shapes it ------------------------------------------------------

        /// <summary>
        /// Repairs the rig, and builds one from a settings file written before the rig existed.
        /// </summary>
        /// <remarks>
        /// Runs after NormaliseFace(), because the migration reads the per-face groups it has just
        /// repaired. Two screens may not share a namespace: that is the collision ADR 0017 exists to
        /// stop, and a file that somehow carried one has the second dropped rather than allowed to
        /// shadow the first.
        /// </remarks>
        private void NormaliseRig()
        {
            if (Rig == null) Rig = MigratedRig();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var kept = new List<ScreenInstance>();
            foreach (var screen in Rig)
            {
                if (screen == null) continue;
                screen.Normalise();
                if (!seen.Add(screen.Namespace)) continue;
                // Two cards both reading "1280 × 480" is a panel nobody can use, so a repeated name is
                // made unique here rather than refused at the point somebody typed it.
                screen.Name = PackageCatalogue.UniqueName(screen.Name, names);
                names.Add(screen.Name);
                kept.Add(screen);
            }
            Rig = kept;
            // Spent: MigratedRig() has emptied it into the rig, and a second copy left behind would be
            // serialised, read by something one day, and disagree.
            if (Faces != null && Faces.Count > 0) Faces = new Dictionary<string, FaceSettings>(StringComparer.Ordinal);
        }

        /// <summary>
        /// The rig a settings file written before ADR 0017 describes.
        /// </summary>
        /// <remarks>
        /// A file that has never installed anything is a new install, and its rig is empty: that is the
        /// first-run state the panel teaches from, and XOR-34's point that an empty rig is one fewer
        /// surface than a wizard.
        ///
        /// Any other file gets one screen per folder openDash has written, because those are the
        /// dashboards the user actually has. Every one takes the stock namespace for its kind and size,
        /// which is what carries the settings across: a face finds the group Faces already held under
        /// that key, and the companion and the pit wall find the flat fields that were theirs when there
        /// could be only one of each.
        ///
        /// Nothing is deleted. Older versions installed every package the plugin embeds, so an upgrading
        /// user meets a rig of a dozen cards; the panel says why and offers to remove the ones they have
        /// no screen for. Dropping them here instead would be this code deciding which dashboards
        /// somebody wanted.
        /// </remarks>
        private List<ScreenInstance> MigratedRig()
        {
            var rig = new List<ScreenInstance>();
            var folders = new List<string>();
            if (FolderFingerprints != null) folders.AddRange(FolderFingerprints.Keys);
            folders.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in folders)
            {
                if (string.IsNullOrEmpty(folder)) continue;
                int width, height;
                PackageCatalogue.SizeFromFolder(folder, out width, out height);
                var screen = new ScreenInstance
                {
                    Kind = PackageCatalogue.Classify(folder, width, height),
                    Width = width,
                    Height = height,
                    Folder = folder,
                };
                screen.Namespace = screen.StockNamespace;
                screen.Name = screen.IsCompanion ? "Companion"
                    : screen.IsPitWall ? "Pit wall"
                    : width > 0 ? screen.SizeLabel
                    : folder;
                if (screen.IsFace && Faces != null)
                {
                    FaceSettings carried;
                    if (Faces.TryGetValue(screen.Namespace, out carried) && carried != null) screen.Face = carried;
                }
                if (screen.IsCompanion) screen.Modules = Modules == null ? null : (bool[])Modules.Clone();
                if (screen.IsPitWall)
                {
                    screen.Zones = Zones == null ? null : (int[])Zones.Clone();
                    screen.WideZone = WideZone;
                    screen.WebViewUrl = WebViewUrl;
                }
                rig.Add(screen);
            }

            // A face somebody configured but whose folder is not on record -- an install that failed,
            // or a settings file carried between machines -- still becomes a screen. The zones are the
            // part a user spent time on, and losing them because a fingerprint was missing would be the
            // migration destroying exactly what it exists to carry.
            if (Faces != null)
            {
                foreach (var entry in Faces)
                {
                    if (entry.Value == null || !Contract.IsKnownFacePrefix(entry.Key)) continue;
                    if (rig.Any(screen => string.Equals(screen.Namespace, entry.Key, StringComparison.Ordinal))) continue;
                    var face = Contract.FaceForPrefix(entry.Key);
                    rig.Add(new ScreenInstance
                    {
                        Kind = Contract.KindFace,
                        Width = face.Width,
                        Height = face.Height,
                        Namespace = entry.Key,
                        Name = face.Width + " × " + face.Height,
                        Face = entry.Value,
                    });
                }
            }
            return rig;
        }

        /// <summary>The rig, readable before Normalise() has filled it.</summary>
        public IReadOnlyList<ScreenInstance> RigScreens() { return Rig ?? new List<ScreenInstance>(); }

        /// <summary>The screen a namespace names, or null when the rig has none.</summary>
        public ScreenInstance ScreenByNamespace(string ns)
        {
            if (ns == null || Rig == null) return null;
            foreach (var screen in Rig)
            {
                if (screen != null && string.Equals(screen.Namespace, ns, StringComparison.Ordinal)) return screen;
            }
            return null;
        }

        /// <summary>The first screen of a kind, which is what a setting that was global before ADR 0017 reads.</summary>
        public ScreenInstance FirstOfKind(string kind)
        {
            if (Rig == null) return null;
            foreach (var screen in Rig)
            {
                if (screen != null && string.Equals(screen.Kind, kind, StringComparison.Ordinal)) return screen;
            }
            return null;
        }

        /// <summary>Every face on the rig, in rig order.</summary>
        public IEnumerable<ScreenInstance> FaceScreens()
        {
            foreach (var screen in RigScreens())
            {
                if (screen.IsFace) yield return screen;
            }
        }

        // The readers the attached properties go through. Each looks the screen up by namespace rather
        // than closing over it, because the panel replaces the whole settings object when the user
        // changes something and a delegate holding the old instance would report the old value for
        // ever. Every one of them survives a namespace the rig no longer has, since an attached
        // delegate outlives the screen it was attached for until SimHub restarts.

        /// <summary>The screen a namespace names, or null. The name ScreenByNamespace reads badly inline.</summary>
        public ScreenInstance ScreenOf(string ns) { return ScreenByNamespace(ns); }

        /// <summary>One face's zones, or a default set when the rig no longer has that screen.</summary>
        public FaceSettings ScreenFace(string ns)
        {
            var screen = ScreenByNamespace(ns);
            if (screen != null && screen.Face != null) return screen.Face;
            // A removed screen's properties are still attached until SimHub restarts, so they must read
            // something rather than throw on SimHub's data thread.
            return Orphaned;
        }

        private static readonly FaceSettings Orphaned = NewOrphan();

        private static FaceSettings NewOrphan()
        {
            var face = new FaceSettings();
            face.Normalise();
            return face;
        }

        /// <summary>How one face draws a flag, or the default when the rig no longer has that screen.</summary>
        public string ScreenFlagFormat(string ns)
        {
            var screen = ScreenByNamespace(ns);
            if (screen == null) return Contract.DefaultFlagFormat;
            return Contract.NormaliseChoice(screen.FlagFormat, Contract.FlagFormats, Contract.DefaultFlagFormat);
        }

        public bool ScreenModule(string ns, int module)
        {
            var screen = ScreenByNamespace(ns);
            var meta = OpenDashPlugin.Modules.ByNumber(module);
            if (meta == null) return false;
            if (screen == null || screen.Modules == null || module - 1 >= screen.Modules.Length) return meta.Enabled;
            return screen.Modules[module - 1];
        }

        /// <summary>The module one companion is showing, or the default when the rig no longer has it.</summary>
        public int ScreenCompanionPage(string ns)
        {
            var screen = ScreenByNamespace(ns);
            return screen == null ? Contract.DefaultCompanionStart : Contract.NormalisePage(screen.CompanionPage, OpenDashPlugin.Modules.Count, Contract.DefaultCompanionStart);
        }

        /// <summary>The module one companion opens on.</summary>
        public int ScreenCompanionStart(string ns)
        {
            var screen = ScreenByNamespace(ns);
            return screen == null ? Contract.DefaultCompanionStart : Contract.NormalisePage(screen.CompanionStart, OpenDashPlugin.Modules.Count, Contract.DefaultCompanionStart);
        }

        /// <summary>The module a held button shows on one companion.</summary>
        public int ScreenCompanionQuickGlance(string ns)
        {
            var screen = ScreenByNamespace(ns);
            return screen == null
                ? Contract.DefaultCompanionQuickGlance
                : Contract.NormalisePage(screen.CompanionQuickGlance, OpenDashPlugin.Modules.Count, Contract.DefaultCompanionQuickGlance);
        }

        /// <summary>Advances one companion to the next module its rotation leaves on.</summary>
        public int CycleScreenModule(string ns)
        {
            var screen = ScreenByNamespace(ns);
            // A removed screen's actions are still bound until SimHub restarts, so a press has to do
            // nothing rather than throw on SimHub's own thread.
            return screen == null ? Contract.DefaultCompanionStart : screen.CycleModule();
        }

        /// <summary>Holds, and releases, one companion's glance.</summary>
        public void BeginScreenGlance(string ns)
        {
            var screen = ScreenByNamespace(ns);
            if (screen != null) screen.BeginQuickGlance();
        }

        public void EndScreenGlance(string ns)
        {
            var screen = ScreenByNamespace(ns);
            if (screen != null) screen.EndQuickGlance();
        }

        public int ScreenZone(string ns, string letter)
        {
            var index = Array.IndexOf(Contract.PitWallZoneLetters, letter);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(letter));
            var screen = ScreenByNamespace(ns);
            if (screen == null || screen.Zones == null || index >= screen.Zones.Length) return Contract.PitWallDefaultZonePages[index];
            return Contract.NormaliseZonePage(screen.Zones[index], Contract.PitWallDefaultZonePages[index]);
        }

        public int ScreenWideZone(string ns)
        {
            var screen = ScreenByNamespace(ns);
            return screen == null ? Contract.DefaultWideZonePage : Contract.NormaliseWideZonePage(screen.WideZone);
        }

        public string ScreenWebViewUrl(string ns)
        {
            var screen = ScreenByNamespace(ns);
            return screen == null ? Contract.DefaultWebViewUrl : (screen.WebViewUrl ?? string.Empty);
        }

        /// <summary>Advances one zone of one screen, and returns the page it landed on.</summary>
        public int CycleScreenZone(string ns, string letter)
        {
            return ScreenFace(ns).Cycle(letter);
        }

        /// <summary>Every zone of every face back on the page it opens on, which is what Init does.</summary>
        public void OpenRigOnStartPages()
        {
            foreach (var screen in FaceScreens()) screen.Face.OpenOnStartPages();
        }

        /// <summary>Adds a screen and returns it, giving it a namespace and a folder nothing else holds.</summary>
        public ScreenInstance AddScreen(PackageEntry entry, string name)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (Rig == null) Rig = new List<ScreenInstance>();
            var taken = new List<string>();
            var names = new List<string>();
            foreach (var screen in Rig)
            {
                if (screen == null) continue;
                taken.Add(screen.Namespace);
                names.Add(screen.Name);
            }
            var wanted = string.IsNullOrWhiteSpace(name) ? entry.SizeLabel : name.Trim();
            var added = PackageCatalogue.NewScreen(entry, PackageCatalogue.UniqueName(wanted, names), taken);
            Rig.Add(added);
            return added;
        }

        /// <summary>Removes a screen and its settings. The folder it owned is the installer's to delete.</summary>
        public bool RemoveScreen(string ns)
        {
            if (Rig == null) return false;
            for (var i = 0; i < Rig.Count; i++)
            {
                if (Rig[i] != null && string.Equals(Rig[i].Namespace, ns, StringComparison.Ordinal))
                {
                    Rig.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// What the stock screen at a size is set to, created with its defaults if the rig has not got one.
        /// </summary>
        /// <remarks>
        /// The compatibility view of the rig, for the callers that still name a face by its size: the
        /// card path, and the tests that predate ADR 0017. It reaches only the screen holding the stock
        /// namespace for that size, which is the one and only sense in which "the 1280x480 face" is
        /// still a thing a rig has -- a second screen of that size has a namespace of its own and is
        /// reachable only through the rig.
        ///
        /// Creating on demand is the pre-0017 behaviour kept deliberately, and it is safe because no
        /// production path calls this any more: the plugin attaches, registers and cycles through the
        /// rig. If one ever does, adding a card to somebody's rig from a property read is the bug to
        /// look for.
        /// </remarks>
        public FaceSettings Face(Contract.FaceSize face)
        {
            var ns = Contract.FacePrefix(face);
            var screen = ScreenByNamespace(ns);
            if (screen == null)
            {
                if (Rig == null) Rig = new List<ScreenInstance>();
                screen = new ScreenInstance
                {
                    Kind = Contract.KindFace,
                    Width = face.Width,
                    Height = face.Height,
                    Namespace = ns,
                    Name = face.Width + " × " + face.Height,
                };
                screen.Normalise();
                Rig.Add(screen);
            }
            if (screen.Face == null)
            {
                screen.Face = new FaceSettings();
                screen.Face.Normalise();
            }
            return screen.Face;
        }

        /// <summary>What the top of the face carries, resolving a file written before the mode existed
        /// through the deprecated ShiftLights alias. Safe to call before Normalise().</summary>
        public string RevBarMode()
        {
            return Contract.NormaliseRevBar(RevBar, ShiftLights);
        }

        /// <summary>Sets the mode, and the alias with it: a dashboard or an LED profile still reading
        /// ShiftLights should see the switch the driver just moved.</summary>
        public void SetRevBar(string mode)
        {
            RevBar = Contract.NormaliseChoice(mode, Contract.RevBarModes, Contract.DefaultRevBar);
            ShiftLights = RevBar == Contract.RevBarShift;
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

        /// <summary>Puts every zone of every face, and every companion, on the page it opens on, once,
        /// when the plugin starts.</summary>
        public void OpenOnStartPages()
        {
            foreach (var screen in FaceScreens()) screen.Face.OpenOnStartPages();
            foreach (var screen in RigScreens())
            {
                if (screen != null && screen.IsCompanion) screen.OpenOnStartModule();
            }
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
                foreach (var screen in FaceScreens())
                {
                    if (screen.Face.GlanceHeld) return true;
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
            foreach (var screen in FaceScreens()) screen.Face.BeginQuickGlance();
        }

        public void EndQuickGlance()
        {
            foreach (var screen in FaceScreens()) screen.Face.EndQuickGlance();
        }

        /// <summary>Zones of one face showing the same page as another, which the panel says and allows.</summary>
        public IReadOnlyList<FacePageClash> FaceClashes(Contract.FaceSize face) => FacePageClash.Find(Face(face));

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
            RevBar = other.RevBar;
            PositionMode = other.PositionMode;
            DeltaReference = other.DeltaReference;
            SessionProgress = other.SessionProgress;
            Screens = other.Screens == null ? null : new List<string>(other.Screens);
            Slots = other.Slots == null ? null : (int[])other.Slots.Clone();
            Modules = other.Modules == null ? null : (bool[])other.Modules.Clone();
            Zones = other.Zones == null ? null : (int[])other.Zones.Clone();
            WideZone = other.WideZone;
            WebViewUrl = other.WebViewUrl;
            // The lights, which were not carried at all before the strips were added: a copy that drops
            // them hands the panel a rig with the brightness back at 100 and matrix 1 back on flags.
            // The per-matrix arrays are cloned for the same reason the slots above are.
            LightsBrightness = other.LightsBrightness;
            LightsNightBrightness = other.LightsNightBrightness;
            LightsNightMode = other.LightsNightMode;
            FlagBoxCriticalOnly = other.FlagBoxCriticalOnly;
            FlagBoxGear = other.FlagBoxGear;
            FlagBoxLowFuelLaps = other.FlagBoxLowFuelLaps;
            FlagBoxOilTemp = other.FlagBoxOilTemp;
            FlagBoxWaterTemp = other.FlagBoxWaterTemp;
            FlagBoxRest = other.FlagBoxRest == null ? null : (string[])other.FlagBoxRest.Clone();
            FlagBoxFlags = other.FlagBoxFlags == null ? null : (bool[])other.FlagBoxFlags.Clone();
            FlagBoxPit = other.FlagBoxPit == null ? null : (bool[])other.FlagBoxPit.Clone();
            FlagBoxSpotter = other.FlagBoxSpotter == null ? null : (bool[])other.FlagBoxSpotter.Clone();
            FlagBoxWarnings = other.FlagBoxWarnings == null ? null : (bool[])other.FlagBoxWarnings.Clone();
            FlagBoxSide = other.FlagBoxSide == null ? null : (string[])other.FlagBoxSide.Clone();
            LedCentre = other.LedCentre;
            LedRpmStyle = other.LedRpmStyle;
            LedFlagAnimation = other.LedFlagAnimation;
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
            // The rig is cloned screen by screen for the reason the faces above are: the panel writes
            // into its copy, and a shared instance would reach back into the settings the plugin reads.
            Rig = other.Rig == null ? null : other.Rig.Where(s => s != null).Select(s => s.Copy()).ToList();
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
