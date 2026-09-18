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

        /// <summary>What a blue flag band says beyond its colour: "none", "class" or "positionClass".
        /// Shared, because what a band may say is the same answer on every screen; the flag *format*,
        /// which decides how much of a screen a flag takes, is the screen's own.</summary>
        public string BlueFlagDetail { get; set; } = Contract.DefaultBlueFlagDetail;

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

        public int FlagBoxLowFuelLaps { get; set; } = Contract.DefaultFlagBoxLowFuelLaps;

        /// <summary>Whether the spotter bar grows inwards. The rig's rather than a box's, and off by
        /// default: a car alongside informs, and only a flag that interrupts the race moves.</summary>
        public bool FlagBoxSpotterAnimation { get; set; } = Contract.DefaultFlagBoxSpotterAnimation;

        // The four settings a box owns, as they were written before they belonged to a box: one value
        // for the whole tab. Nullable and with no initialiser, so that "absent" is distinguishable from
        // "the driver chose the default"; MigrateFlagBoxToMatrices() copies each into all four panels
        // once and then clears it, the way Modules and Zones are emptied into the rig. A property name
        // is a public interface (ADR 0003), so the rename ships with the migration rather than after it.
        public bool? FlagBoxCriticalOnly { get; set; }

        public bool? FlagBoxGear { get; set; }

        public int? FlagBoxOilTemp { get; set; }

        public int? FlagBoxWaterTemp { get; set; }

        /// <summary>
        /// What each matrix panel is called, or null in the slots nobody has added.
        /// </summary>
        /// <remarks>
        /// A panel is an instance now, the way a screen is: a rig has none until somebody adds one, and
        /// what they add has a name they chose -- "top left", "by the wheel" -- because four numbered
        /// groups of eleven settings for hardware most people own none of is a page nobody can read.
        /// SimHub composes at most four matrix contents, so the four slots stay and this says which of
        /// them exist.
        ///
        /// Null until Normalise() fills it, and deliberately not initialised here, for the reason Rig
        /// gives: null means "this file has never named panels", which is the signal the migration reads.
        /// </remarks>
        public string[] FlagBoxMatrixName { get; set; }

        /// <summary>
        /// The RGB strips on the rig, each with the settings its own profile reads.
        /// </summary>
        /// <remarks>
        /// Null until Normalise() fills it, the way Rig is: a strip used to be a shape the panel offered
        /// to install and nothing more, so two strips on one rig could not be configured apart. Null
        /// means "this file has never named bars", which is every file written before they existed and
        /// every new install; both start with none, because a profile paints hardware somebody owns and
        /// openDash does not guess at what that is (ADR 0013).
        /// </remarks>
        public List<LedBar> LedBars { get; set; }

        /// <summary>Per matrix, index 0 is matrix 1. Always four long after Normalise().</summary>
        public string[] FlagBoxRest { get; set; } = Contract.DefaultFlagBoxRests();

        public bool[] FlagBoxFlags { get; set; } = Contract.DefaultFlagBoxOn();

        /// <summary>The limiter, the lane and speeding. Its own switch, not the flags'.</summary>
        public bool[] FlagBoxPit { get; set; } = Contract.DefaultFlagBoxOn();

        public bool[] FlagBoxSpotter { get; set; } = Contract.DefaultFlagBoxOn();

        public bool[] FlagBoxWarnings { get; set; } = Contract.DefaultFlagBoxOn();

        public string[] FlagBoxSide { get; set; } = Contract.DefaultFlagBoxSides();

        // The four that moved under the matrix. They carry the Matrix infix although their six siblings
        // above do not, because the unprefixed name is still occupied by the legacy scalar each one
        // migrates from and a saved file cannot hold one name as both a bool and a bool[].

        public bool[] FlagBoxMatrixCriticalOnly { get; set; } = Contract.DefaultFlagBoxCriticalOnlys();

        public bool[] FlagBoxMatrixGear { get; set; } = Contract.DefaultFlagBoxGears();

        /// <summary>Whether this panel's digit flashes while the car is over-revving. Per panel, because
        /// a box in the corner of a monitor stand strobing at the edge of vision is what a driver with a
        /// rev bar in front of them turns off, and the box on the wheel is not.</summary>
        public bool[] FlagBoxMatrixGearBlink { get; set; } = Contract.DefaultFlagBoxGearBlinks();

        /// <summary>Zero means "not set", so that the profile's own per-unit default applies. A driver in
        /// Fahrenheit who has never opened this page must not get a Celsius number.</summary>
        public int[] FlagBoxMatrixOilTemp { get; set; } = Contract.DefaultFlagBoxTemps();

        public int[] FlagBoxMatrixWaterTemp { get; set; } = Contract.DefaultFlagBoxTemps();

        /// <summary>What the middle of an RGB strip shows: "rpm", "brake", "throttleBrake" or "fuel".
        /// One value for the rig and not an array, because openDash generates one profile per strip
        /// shape rather than per device and every shape reads this one name.</summary>
        public string LedCentre { get; set; } = Contract.DefaultLedCentre;

        /// <summary>How the rev ladder fills a strip: "car", "leftToRight", "meetInMiddle" or "f1".
        /// The three openDash styles are the look only; the thresholds are the car's own whichever is
        /// set (ADR 0014). "car" is the car's whole bar, from the fetched table (ADR 0018).</summary>
        public string LedRpmStyle { get; set; } = Contract.DefaultLedRpmStyle;

        /// <summary>Whether a flag on a strip moves. Off holds every flag from the frame it would have
        /// settled on and never turns one off.</summary>
        public bool LedFlagAnimation { get; set; } = Contract.DefaultLedFlagAnimation;

        /// <summary>What a mirrored bar does on a strip that is not the car's length: "stretch" fills
        /// the run, "exact" draws the bar at its own length in the middle of it.</summary>
        public string LedMirrorFit { get; set; } = Contract.DefaultLedMirrorFit;

        /// <summary>One matrix's settings, 1-based, repaired if the array came back short.</summary>
        // These say what a slot is *set to*, which is what the panel's own controls draw. What it
        // *shows* is that and whether a panel was added at all, and that is asked at the one place it
        // matters: the delegates OpenDash.cs attaches, which are what the profile reads.
        public string MatrixRest(int matrix) => Pick(FlagBoxRest, matrix, Contract.DefaultFlagBoxMatrixRest(matrix));

        public bool MatrixFlags(int matrix) => Pick(FlagBoxFlags, matrix, Contract.DefaultFlagBoxMatrixOn(matrix));

        public bool MatrixPit(int matrix) => Pick(FlagBoxPit, matrix, Contract.DefaultFlagBoxMatrixOn(matrix));

        public bool MatrixSpotter(int matrix) => Pick(FlagBoxSpotter, matrix, Contract.DefaultFlagBoxMatrixOn(matrix));

        public bool MatrixWarnings(int matrix) => Pick(FlagBoxWarnings, matrix, Contract.DefaultFlagBoxMatrixOn(matrix));

        public string MatrixSide(int matrix) => Pick(FlagBoxSide, matrix, Contract.DefaultFlagBoxSide);

        public bool MatrixCriticalOnly(int matrix) => Pick(FlagBoxMatrixCriticalOnly, matrix, Contract.DefaultFlagBoxCriticalOnly);

        public bool MatrixGear(int matrix) => Pick(FlagBoxMatrixGear, matrix, Contract.DefaultFlagBoxGear);

        public bool MatrixGearBlink(int matrix) => Pick(FlagBoxMatrixGearBlink, matrix, Contract.DefaultFlagBoxGearBlink);

        // --- The LED bars, which are the strips as instances ------------------------------------

        /// <summary>The bars on the rig, never null after Normalise().</summary>
        public IReadOnlyList<LedBar> LedBarList() { return LedBars ?? new List<LedBar>(); }

        public LedBar LedBarByNamespace(string ns)
        {
            if (ns == null || LedBars == null) return null;
            foreach (var bar in LedBars)
            {
                if (bar != null && string.Equals(bar.Namespace, ns, StringComparison.Ordinal)) return bar;
            }
            return null;
        }

        /// <summary>What one bar's middle shows, or the rig's own answer when the bar has gone. An
        /// attached delegate outlives the bar it was attached for until SimHub restarts.</summary>
        public string BarCentre(string ns)
        {
            var bar = LedBarByNamespace(ns);
            return bar == null ? Contract.NormaliseLedCentre(LedCentre) : Contract.NormaliseLedCentre(bar.Centre);
        }

        public string BarRpmStyle(string ns)
        {
            var bar = LedBarByNamespace(ns);
            var value = bar == null ? LedRpmStyle : bar.RpmStyle;
            return Contract.NormaliseChoice(value, Contract.LedRpmStyles, Contract.DefaultLedRpmStyle);
        }

        /// <summary>Whether anything on the rig is asking for the car's own shift pattern, which is what
        /// decides whether the mirror is computed at all.</summary>
        public bool AnyCarLadderWanted()
        {
            var bars = LedBarList();
            if (bars.Count == 0) return LedRpmStyle == Contract.LedRpmStyleCar;
            foreach (var bar in bars)
            {
                if (BarRpmStyle(bar.Namespace) == Contract.LedRpmStyleCar) return true;
            }
            // A rig with bars may still have a face or a box reading the rig-wide answer.
            return LedRpmStyle == Contract.LedRpmStyleCar;
        }

        public bool BarFlagAnimation(string ns)
        {
            var bar = LedBarByNamespace(ns);
            return bar == null ? LedFlagAnimation : bar.FlagAnimation;
        }

        public bool BarSpotterWhole(string ns)
        {
            var bar = LedBarByNamespace(ns);
            return bar == null ? Contract.DefaultLedSpotterWhole : bar.SpotterWhole;
        }

        /// <summary>
        /// Adds a bar of a shape, with the rig's own settings as its starting point.
        /// </summary>
        /// <remarks>
        /// The rig-wide values rather than the class defaults, because somebody adding their second bar
        /// has already said what they like on the first and on the tab before bars existed. The namespace
        /// is frozen here and nowhere else moves it.
        /// </remarks>
        public LedBar AddLedBar(string shape, string name)
        {
            if (LedBars == null) LedBars = new List<LedBar>();
            var names = new List<string>();
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var bar in LedBars)
            {
                if (bar == null) continue;
                names.Add(bar.Name);
                taken.Add(bar.Namespace);
            }
            var wanted = PackageCatalogue.UniqueName(string.IsNullOrWhiteSpace(name) ? shape : name.Trim(), names);
            var added = new LedBar
            {
                Name = wanted,
                Shape = shape,
                Namespace = FreeBarNamespace(wanted, taken),
                Centre = LedCentre,
                RpmStyle = LedRpmStyle,
                FlagAnimation = LedFlagAnimation,
            };
            added.Normalise();
            LedBars.Add(added);
            return added;
        }

        public bool RemoveLedBar(string ns)
        {
            if (LedBars == null) return false;
            for (var i = 0; i < LedBars.Count; i++)
            {
                if (LedBars[i] != null && string.Equals(LedBars[i].Namespace, ns, StringComparison.Ordinal))
                {
                    LedBars.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public void RenameLedBar(string ns, string name)
        {
            var bar = LedBarByNamespace(ns);
            if (bar == null || string.IsNullOrWhiteSpace(name)) return;
            var others = new List<string>();
            foreach (var other in LedBarList())
            {
                if (!ReferenceEquals(other, bar)) others.Add(other.Name);
            }
            bar.Name = PackageCatalogue.UniqueName(name.Trim(), others);
        }

        /// <summary>Whether this slot holds a panel somebody added, and what they called it.</summary>
        public bool MatrixAdded(int matrix) => MatrixName(matrix) != null;

        /// <summary>What a slot actually shows, which is what it is set to and whether it exists at all.
        /// The delegates read these; the panel's controls read the plain setters above them.</summary>
        public string MatrixShownRest(int matrix) => MatrixAdded(matrix) ? MatrixRest(matrix) : "dark";

        public bool MatrixShowsFlags(int matrix) => MatrixAdded(matrix) && MatrixFlags(matrix);

        public bool MatrixShowsPit(int matrix) => MatrixAdded(matrix) && MatrixPit(matrix);

        public bool MatrixShowsSpotter(int matrix) => MatrixAdded(matrix) && MatrixSpotter(matrix);

        public bool MatrixShowsWarnings(int matrix) => MatrixAdded(matrix) && MatrixWarnings(matrix);

        public bool MatrixShowsGear(int matrix) => MatrixAdded(matrix) && MatrixGear(matrix);

        public string MatrixName(int matrix)
        {
            if (FlagBoxMatrixName == null || matrix < 1 || matrix > FlagBoxMatrixName.Length) return null;
            var name = FlagBoxMatrixName[matrix - 1];
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        /// <summary>Every slot with a panel in it, in slot order.</summary>
        public IEnumerable<int> MatrixPanels()
        {
            foreach (var matrix in Contract.FlagBoxMatrices)
            {
                if (MatrixAdded(matrix)) yield return matrix;
            }
        }

        /// <summary>The first slot with no panel in it, or 0 when all four are taken.</summary>
        public int FreeMatrixSlot()
        {
            foreach (var matrix in Contract.FlagBoxMatrices)
            {
                if (!MatrixAdded(matrix)) return matrix;
            }
            return 0;
        }

        /// <summary>
        /// Adds a panel in the first free slot, with the settings a first box wants.
        /// </summary>
        /// <remarks>
        /// Not the class defaults: those made matrix 1 do everything and the other three nothing, which
        /// was the whole of the old model. A panel somebody has just added is one they mean to use, so it
        /// arrives showing the catalogue, the pit family, the spotter, the warnings and the gear --
        /// which is what matrix 1 used to be, now applied to whichever slot they added rather than to
        /// the first one whether they wanted it or not.
        /// </remarks>
        public int AddMatrixPanel(string name)
        {
            var slot = FreeMatrixSlot();
            if (slot == 0) return 0;
            var i = slot - 1;
            FlagBoxMatrixName[i] = string.IsNullOrWhiteSpace(name) ? "Matrix " + slot : name.Trim();
            FlagBoxRest[i] = "gear";
            FlagBoxFlags[i] = true;
            FlagBoxPit[i] = true;
            FlagBoxSpotter[i] = true;
            FlagBoxWarnings[i] = true;
            FlagBoxSide[i] = Contract.DefaultFlagBoxSide;
            FlagBoxMatrixCriticalOnly[i] = Contract.DefaultFlagBoxCriticalOnly;
            FlagBoxMatrixGear[i] = Contract.DefaultFlagBoxGear;
            FlagBoxMatrixGearBlink[i] = Contract.DefaultFlagBoxGearBlink;
            FlagBoxMatrixOilTemp[i] = 0;
            FlagBoxMatrixWaterTemp[i] = 0;
            return slot;
        }

        /// <summary>Takes a panel out of its slot and puts the slot back to dark, so the profile draws
        /// nothing there rather than whatever the removed panel had been showing.</summary>
        public void RemoveMatrixPanel(int matrix)
        {
            if (matrix < 1 || matrix > Contract.FlagBoxMatrices.Count) return;
            var i = matrix - 1;
            FlagBoxMatrixName[i] = null;
            FlagBoxRest[i] = "dark";
            FlagBoxFlags[i] = false;
            FlagBoxPit[i] = false;
            FlagBoxSpotter[i] = false;
            FlagBoxWarnings[i] = false;
        }

        public void RenameMatrixPanel(int matrix, string name)
        {
            if (matrix < 1 || matrix > Contract.FlagBoxMatrices.Count) return;
            if (string.IsNullOrWhiteSpace(name)) return;
            FlagBoxMatrixName[matrix - 1] = name.Trim();
        }

        /// <summary>
        /// Fills the panel names, and decides what a file written before panels were instances keeps.
        /// </summary>
        /// <remarks>
        /// A new install starts with none, which is what was asked for and what the rig of screens
        /// already does: nothing is configured for hardware nobody has said they own. A file that
        /// predates this keeps every slot that was doing something, which on the old defaults is matrix 1
        /// alone and on a rig with two boxes is both -- so nobody's box goes dark because the model
        /// underneath it changed.
        /// </remarks>
        private void NormaliseMatrixPanels()
        {
            if (FlagBoxMatrixName == null)
            {
                FlagBoxMatrixName = new string[Contract.FlagBoxMatrices.Count];
                // A settings object nobody has saved is a new install, and a new install owns nothing.
                if (!WasSaved()) return;
                foreach (var matrix in Contract.FlagBoxMatrices)
                {
                    var i = matrix - 1;
                    var doing = !string.Equals(FlagBoxRest[i], "dark", StringComparison.Ordinal)
                        || FlagBoxFlags[i] || FlagBoxPit[i] || FlagBoxSpotter[i] || FlagBoxWarnings[i];
                    if (doing) FlagBoxMatrixName[i] = "Matrix " + matrix;
                }
                return;
            }
            FlagBoxMatrixName = Resize(FlagBoxMatrixName, new string[Contract.FlagBoxMatrices.Count], v => true);
            for (var i = 0; i < FlagBoxMatrixName.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(FlagBoxMatrixName[i])) FlagBoxMatrixName[i] = null;
            }
        }

        public int MatrixOilTemp(int matrix) => Pick(FlagBoxMatrixOilTemp, matrix, 0);

        public int MatrixWaterTemp(int matrix) => Pick(FlagBoxMatrixWaterTemp, matrix, 0);

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
            FlagBoxRest = Resize(FlagBoxRest, Contract.DefaultFlagBoxRests(), v => Array.IndexOf(Contract.FlagBoxRests, v) >= 0);
            FlagBoxSide = Resize(FlagBoxSide, Contract.DefaultFlagBoxSides(), v => Array.IndexOf(Contract.FlagBoxSides, v) >= 0);
            FlagBoxFlags = Resize(FlagBoxFlags, Contract.DefaultFlagBoxOn(), v => true);
            FlagBoxPit = Resize(FlagBoxPit, Contract.DefaultFlagBoxOn(), v => true);
            FlagBoxSpotter = Resize(FlagBoxSpotter, Contract.DefaultFlagBoxOn(), v => true);
            FlagBoxWarnings = Resize(FlagBoxWarnings, Contract.DefaultFlagBoxOn(), v => true);
            FlagBoxMatrixCriticalOnly = Resize(FlagBoxMatrixCriticalOnly, Contract.DefaultFlagBoxCriticalOnlys(), v => true);
            FlagBoxMatrixGear = Resize(FlagBoxMatrixGear, Contract.DefaultFlagBoxGears(), v => true);
            FlagBoxMatrixGearBlink = Resize(FlagBoxMatrixGearBlink, Contract.DefaultFlagBoxGearBlinks(), v => true);
            FlagBoxMatrixOilTemp = Resize(FlagBoxMatrixOilTemp, Contract.DefaultFlagBoxTemps(), v => v >= 0);
            FlagBoxMatrixWaterTemp = Resize(FlagBoxMatrixWaterTemp, Contract.DefaultFlagBoxTemps(), v => v >= 0);
            // After the arrays are four long, so the migration has four slots to fill.
            MigrateFlagBoxToMatrices();
            // After the arrays are four long and the old scalars have been emptied into them, because
            // which panels a migrating rig keeps is read off what those panels were doing.
            NormaliseMatrixPanels();
            NormaliseLedBars();
            // No array to repair: the strips carry one value each for the whole rig. A profile reads
            // both through isnull() with its own default, so an unrecognised spelling has to become a
            // legal one here rather than reaching the strip as itself.
            LedCentre = Contract.NormaliseLedCentre(LedCentre);
            LedRpmStyle = Contract.NormaliseChoice(LedRpmStyle, Contract.LedRpmStyles, Contract.DefaultLedRpmStyle);
            LedMirrorFit = Contract.NormaliseChoice(LedMirrorFit, Contract.LedMirrorFits, Contract.DefaultLedMirrorFit);
        }

        /// <summary>
        /// Carries a settings file written before the four settings a box owns belonged to a box.
        /// </summary>
        /// <remarks>
        /// Each legacy scalar goes into all four panels and is then cleared, exactly as Modules and
        /// Zones are emptied into the rig: a driver who had set "critical flags only" keeps it on every
        /// box they own rather than being silently reset to the default. Cleared afterwards so that it
        /// happens once; a file written by this version carries nulls and is left alone, and a value a
        /// driver sets on one panel afterwards is not overwritten on the next load.
        ///
        /// It runs after the arrays are four long, so there are always four slots to fill.
        /// </remarks>
        private void MigrateFlagBoxToMatrices()
        {
            for (var i = 0; i < Contract.FlagBoxMatrices.Count; i++)
            {
                if (FlagBoxCriticalOnly.HasValue) FlagBoxMatrixCriticalOnly[i] = FlagBoxCriticalOnly.Value;
                if (FlagBoxGear.HasValue) FlagBoxMatrixGear[i] = FlagBoxGear.Value;
                if (FlagBoxOilTemp.HasValue) FlagBoxMatrixOilTemp[i] = FlagBoxOilTemp.Value < 0 ? 0 : FlagBoxOilTemp.Value;
                if (FlagBoxWaterTemp.HasValue) FlagBoxMatrixWaterTemp[i] = FlagBoxWaterTemp.Value < 0 ? 0 : FlagBoxWaterTemp.Value;
            }

            FlagBoxCriticalOnly = null;
            FlagBoxGear = null;
            FlagBoxOilTemp = null;
            FlagBoxWaterTemp = null;
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
            BlueFlagDetail = Contract.NormaliseChoice(BlueFlagDetail, Contract.BlueFlagDetails, Contract.DefaultBlueFlagDetail);
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
        /// one is #176. So an old settings file attaches exactly what it attached before the rig
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
            return Contract.PropertyNames(RigScreens(), LedBarList());
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
        /// first-run state the panel teaches from, and #85's point that an empty rig is one fewer
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

        /// <summary>When one face shows the lap review, or the default when the rig no longer has that screen.</summary>
        public string ScreenLapReview(string ns)
        {
            var screen = ScreenByNamespace(ns);
            if (screen == null) return Contract.DefaultLapReview;
            return Contract.NormaliseChoice(screen.LapReview, Contract.LapReviewModes, Contract.DefaultLapReview);
        }

        /// <summary>
        /// What one face carries at the top, falling back to the rig's own answer.
        /// </summary>
        /// <remarks>
        /// The fallback is the whole of the migration: a settings file written before this was per
        /// screen holds null on every face and every one of them goes on reading the rig-wide value,
        /// so nothing changes until a driver answers one of them individually. The package's own
        /// expression falls back the same way, so a screen with no plugin behaves identically.
        /// </remarks>
        public string ScreenRevBar(string ns)
        {
            var screen = ScreenByNamespace(ns);
            if (screen == null || screen.RevBar == null) return RevBarMode();
            return Contract.NormaliseChoice(screen.RevBar, Contract.RevBarModes, Contract.DefaultRevBar);
        }

        /// <summary>Sets one face's own answer, or clears it back to the rig's.</summary>
        public void SetScreenRevBar(string ns, string mode)
        {
            var screen = ScreenByNamespace(ns);
            if (screen == null) return;
            screen.RevBar = mode == null ? null : Contract.NormaliseChoice(mode, Contract.RevBarModes, Contract.DefaultRevBar);
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
            return screen == null ? Contract.DefaultCompanionPage : Contract.NormalisePage(screen.CompanionPage, OpenDashPlugin.Modules.Count, Contract.DefaultCompanionPage);
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

        /// <summary>Whether one pit wall lists the player's own class rather than the whole field.</summary>
        public bool ScreenPitWallClassOnly(string ns)
        {
            var screen = ScreenByNamespace(ns);
            return screen == null ? Contract.DefaultPitWallClassOnly : screen.PitWallClassOnly;
        }

        /// <summary>The zone and page a held button shows on one pit wall.</summary>
        public int ScreenPitWallQuickGlance(string ns)
        {
            var screen = ScreenByNamespace(ns);
            return screen == null ? Contract.DefaultPitWallQuickGlance : Contract.NormalisePitWallQuickGlance(screen.PitWallQuickGlance);
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
            // The folders too, and not only the namespaces: two screens sharing a DashTemplates folder
            // means removing one deletes the other's dashboard, which is what "openDash rim" twice on one
            // rig did.
            var folders = new List<string>();
            foreach (var screen in Rig)
            {
                if (screen != null && screen.Folder != null) folders.Add(screen.Folder);
            }
            var added = PackageCatalogue.NewScreen(entry, PackageCatalogue.UniqueName(wanted, names), taken, folders);
            Rig.Add(added);
            return added;
        }

        /// <summary>
        /// Moves a screen to another size, keeping everything a driver has set on it.
        /// </summary>
        /// <remarks>
        /// The namespace is frozen at creation and this does not move it either (ADR 0017), so the zone
        /// settings, the bar and every wheel button bound to the screen survive a resize. What that costs
        /// is the folder: a screen that held the stock folder of its old size no longer holds the stock
        /// namespace of its new one, so it needs a folder of its own, and the caller deletes the old one
        /// before calling this.
        /// </remarks>
        public void ResizeScreen(ScreenInstance screen, PackageEntry entry)
        {
            if (screen == null || entry == null) return;
            screen.Width = entry.Width;
            screen.Height = entry.Height;
            screen.Package = entry.Package;
            screen.Folder = screen.IsStock
                ? entry.Folder
                : PackageCatalogue.UniqueFolder(screen.Name, Taken(screen), entry.Folder);
            screen.Normalise();
        }

        /// <summary>
        /// Whether this object was read from a file somebody's SimHub has written, rather than being the
        /// blank one a new install starts from.
        /// </summary>
        /// <remarks>
        /// The per-slot arrays cannot answer it: they have class defaults, so a blank object and a saved
        /// one carrying the defaults look the same from there. What does answer it is the state only a
        /// save produces -- a rig, or the screen list that preceded it, or one of the legacy scalars a
        /// migration has not yet emptied. It decides one thing: whether a matrix panel is kept for
        /// somebody who had a box working before panels were instances, or whether the rig starts empty
        /// as a new one should.
        /// </remarks>
        /// <summary>
        /// A bar namespace nothing else on the rig holds.
        /// </summary>
        /// <remarks>
        /// Prefixed with "Led" before it is deduplicated and not after, which is the difference between
        /// checking the name that will be attached and checking a fragment of it: two bars both called
        /// "Rim" would otherwise both be asked about "Rim", both be told it was free, and both be
        /// attached as "LedRim" -- one profile silently reading the other's settings.
        /// </remarks>
        private static string FreeBarNamespace(string name, ICollection<string> taken)
        {
            var slug = Contract.Slug(name);
            if (slug.Length == 0) slug = "Strip";
            var candidate = "Led" + slug;
            var n = 2;
            while (taken.Contains(candidate) || Contract.IsReservedNamespace(candidate))
            {
                candidate = "Led" + slug + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                n++;
            }
            return candidate;
        }

        /// <summary>Repairs the bars: every one spellable, legal and holding a namespace nothing else
        /// on the rig holds, so two bars cannot install one profile over each other.</summary>
        private void NormaliseLedBars()
        {
            if (LedBars == null)
            {
                LedBars = new List<LedBar>();
                return;
            }
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = LedBars.Count - 1; i >= 0; i--)
            {
                if (LedBars[i] == null) LedBars.RemoveAt(i);
            }
            foreach (var bar in LedBars)
            {
                bar.Normalise();
                if (taken.Contains(bar.Namespace)) bar.Namespace = FreeBarNamespace(bar.Name, taken);
                taken.Add(bar.Namespace);
            }
        }

        private bool WasSaved()
        {
            return Rig != null
                || Screens != null
                || (Faces != null && Faces.Count > 0)
                || FlagBoxCriticalOnly.HasValue
                || FlagBoxGear.HasValue
                || FlagBoxOilTemp.HasValue
                || FlagBoxWaterTemp.HasValue;
        }

        /// <summary>The folders every other screen on the rig owns, so a folder given out here is not one
        /// of theirs.</summary>
        private IEnumerable<string> Taken(ScreenInstance except)
        {
            foreach (var screen in RigScreens())
            {
                if (screen == null || ReferenceEquals(screen, except) || screen.Folder == null) continue;
                yield return screen.Folder;
            }
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
            BlueFlagDetail = other.BlueFlagDetail;
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
            FlagBoxSpotterAnimation = other.FlagBoxSpotterAnimation;
            FlagBoxOilTemp = other.FlagBoxOilTemp;
            FlagBoxWaterTemp = other.FlagBoxWaterTemp;
            FlagBoxMatrixCriticalOnly = other.FlagBoxMatrixCriticalOnly == null ? null : (bool[])other.FlagBoxMatrixCriticalOnly.Clone();
            FlagBoxMatrixGear = other.FlagBoxMatrixGear == null ? null : (bool[])other.FlagBoxMatrixGear.Clone();
            FlagBoxMatrixGearBlink = other.FlagBoxMatrixGearBlink == null ? null : (bool[])other.FlagBoxMatrixGearBlink.Clone();
            FlagBoxMatrixName = other.FlagBoxMatrixName == null ? null : (string[])other.FlagBoxMatrixName.Clone();
            LedBars = other.LedBars == null ? null : other.LedBars.Select(bar => bar == null ? null : bar.Copy()).ToList();
            FlagBoxMatrixOilTemp = other.FlagBoxMatrixOilTemp == null ? null : (int[])other.FlagBoxMatrixOilTemp.Clone();
            FlagBoxMatrixWaterTemp = other.FlagBoxMatrixWaterTemp == null ? null : (int[])other.FlagBoxMatrixWaterTemp.Clone();
            FlagBoxRest = other.FlagBoxRest == null ? null : (string[])other.FlagBoxRest.Clone();
            FlagBoxFlags = other.FlagBoxFlags == null ? null : (bool[])other.FlagBoxFlags.Clone();
            FlagBoxPit = other.FlagBoxPit == null ? null : (bool[])other.FlagBoxPit.Clone();
            FlagBoxSpotter = other.FlagBoxSpotter == null ? null : (bool[])other.FlagBoxSpotter.Clone();
            FlagBoxWarnings = other.FlagBoxWarnings == null ? null : (bool[])other.FlagBoxWarnings.Clone();
            FlagBoxSide = other.FlagBoxSide == null ? null : (string[])other.FlagBoxSide.Clone();
            LedCentre = other.LedCentre;
            LedRpmStyle = other.LedRpmStyle;
            LedFlagAnimation = other.LedFlagAnimation;
            LedMirrorFit = other.LedMirrorFit;
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
        public FacePageClash(string pageId, string pageName, string[] zones, bool glance = false)
        {
            PageId = pageId;
            PageName = pageName;
            Zones = zones;
            Glance = glance;
        }

        public string PageId { get; }

        public string PageName { get; }

        /// <summary>The zone letters showing it, in letter order.</summary>
        public string[] Zones { get; }

        /// <summary>Whether the quick glance shows it too, which makes it a participant like a zone.</summary>
        public bool Glance { get; }

        /// <summary>
        /// The page as the sentence spells it: "the relative".
        /// </summary>
        /// <remarks>
        /// An article and a lower-case noun, because that is how the sentence reads aloud and how the
        /// canvas writes it. A name that lists what a page draws rather than naming one thing keeps the
        /// spelling the panel's own drop-down uses instead: "Gear, speed, revs" does not read after an
        /// article, and no short noun for it exists to invent.
        /// </remarks>
        public static string DisplayName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (name.IndexOf(',') >= 0) return name;
            return "the " + char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>"Zone B and zone C both show the relative."</summary>
        public string Message()
        {
            var parts = Zones.Select(z => "zone " + z).ToList();
            if (Glance) parts.Add("the quick glance");
            var list = parts.Count == 2
                ? parts[0] + " and " + parts[1]
                : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[parts.Count - 1];
            var verb = parts.Count == 2 ? " both show " : " all show ";
            return char.ToUpperInvariant(list[0]) + list.Substring(1) + verb + DisplayName(PageName) + ".";
        }

        /// <summary>
        /// Every page two or more of the five participants show: the four zones' start pages, and the
        /// quick glance.
        /// </summary>
        /// <remarks>
        /// The glance is compared by page id and against each zone's *start* page, exactly as the zones
        /// are compared with each other. By id because the four catalogues overlap, so zone A's track
        /// page and module 13 are one drawing under two numbers; against the start page rather than the
        /// whole cycle because a glance set to a page a zone can cycle to is a thing somebody may well
        /// want, and warning about it would be a false alarm on every second rig.
        /// </remarks>
        public static IReadOnlyList<FacePageClash> Find(FaceSettings face)
        {
            var result = new List<FacePageClash>();
            if (face == null) return result;
            var order = new List<string>();
            var byPage = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            var glanced = new HashSet<string>(StringComparer.Ordinal);
            Action<string, string, string> add = (id, name, letter) =>
            {
                if (id == null) return;
                List<string> zones;
                if (!byPage.TryGetValue(id, out zones))
                {
                    zones = new List<string>();
                    byPage[id] = zones;
                    names[id] = name;
                    order.Add(id);
                }
                if (letter == null) glanced.Add(id);
                else zones.Add(letter);
            };
            foreach (var letter in Contract.FaceZoneLetters) add(FacePages.IdOf(letter, face.Start(letter)), FacePages.NameOf(letter, face.Start(letter)), letter);

            var glance = Contract.NormaliseQuickGlance(face.QuickGlance);
            var glanceLetter = Contract.FaceZoneLetters[Contract.QuickGlanceZone(glance)];
            var glancePage = Contract.QuickGlancePage(glance);
            add(FacePages.IdOf(glanceLetter, glancePage), FacePages.NameOf(glanceLetter, glancePage), null);

            foreach (var id in order)
            {
                var zones = byPage[id];
                var showsGlance = glanced.Contains(id);
                if (zones.Count + (showsGlance ? 1 : 0) < 2) continue;
                result.Add(new FacePageClash(id, names[id], zones.ToArray(), showsGlance));
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
