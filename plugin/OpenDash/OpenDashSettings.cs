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
            // Kept under the spelling it is given and under no other. A dictionary that ignores case keeps the spelling
            // a folder was first recorded under, which on a rig older than #374 is "openDash", and ADR 0017's migration
            // read the rig's folders from those keys; a record that follows what was written stops handing the old
            // spelling on (#467). The comparison is made here rather than left to the dictionary, whose comparer is
            // whatever the settings were deserialised with.
            var stale = current.FolderFingerprints.Keys
                .Where(key => string.Equals(key, folderName, StringComparison.OrdinalIgnoreCase) && !string.Equals(key, folderName, StringComparison.Ordinal))
                .ToList();
            foreach (var key in stale) current.FolderFingerprints.Remove(key);
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

        /// <summary>How many places the live delta is drawn to: "hundredths" or "thousandths". Shared,
        /// like the reference it qualifies.</summary>
        public string DeltaPrecision { get; set; } = Contract.DefaultDeltaPrecision;

        public string SessionProgress { get; set; } = Contract.DefaultSessionProgress;

        /// <summary>What a blue flag band says beyond its colour: "none", "class" or "positionClass".
        /// Shared, because what a band may say is the same answer on every screen; the flag *format*,
        /// which decides how much of a screen a flag takes, is the screen's own.</summary>
        public string BlueFlagDetail { get; set; } = Contract.DefaultBlueFlagDetail;

        /// <summary>How a driver is named wherever a list names one: one of Contract.DriverNameFormats.
        /// Shared, because which of the four reads best is a fact about the reader and not about the
        /// screen they are reading.</summary>
        public string DriverNameFormat { get; set; } = Contract.DefaultDriverNameFormat;

        /// <summary>Whether a list names the team rather than the driver, which is what an endurance
        /// entry is known by. The dashboard falls back to the driver per row where the sim publishes no
        /// team, so a mixed grid draws teams for the entries that have one.</summary>
        public bool DriverNameTeam { get; set; } = Contract.DefaultDriverNameTeam;

        /// <summary>How a clock of the day is written, "24h" or "12h": one of Contract.ClockFormats.
        /// Shared, because a driver reads a clock one way wherever it is drawn, and both the wall clock
        /// and the sim's time of day follow it. #324.</summary>
        public string ClockFormat { get; set; } = Contract.DefaultClockFormat;

        /// <summary>Whether a flag shows while the car is in the pit lane. Shared, because a driver who
        /// wants quiet on the way down the lane wants it of every surface. #503.</summary>
        public bool FlagsInPitLane { get; set; } = Contract.DefaultFlagsInPitLane;

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
        /// OpenDash does not guess at what that is (ADR 0013).
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

        /// <summary>Whether the gear is this panel's resting state. Deprecated by <see cref="FlagBoxRest"/>,
        /// kept as its alias and kept attached, because it has shipped and ADR 0003 makes a published
        /// property a public interface. <see cref="SetMatrixRest"/> moves the two together and
        /// Normalise() keeps them agreeing.</summary>
        public bool[] FlagBoxMatrixGear { get; set; } = Contract.DefaultFlagBoxGears();

        /// <summary>Whether this panel's digit flashes while the car is over-revving. Per panel, because
        /// a box in the corner of a monitor stand strobing at the edge of vision is what a driver with a
        /// rev bar in front of them turns off, and the box on the wheel is not.</summary>
        public bool[] FlagBoxMatrixGearBlink { get; set; } = Contract.DefaultFlagBoxGearBlinks();

        /// <summary>Whether this panel's digit is coloured by the shift model at all. Off leaves it in
        /// the resting colour at any engine speed, which is the whole of what a driver asking for the
        /// digit to stop lighting up is asking for.</summary>
        public bool[] FlagBoxMatrixGearBands { get; set; } = Contract.EveryFlagBoxMatrix(Contract.DefaultFlagBoxGearBands);

        /// <summary>Whether those colours change on the car's own measured bar rather than on the
        /// ladder the sim publishes. The digit's half of what the strips have had since ADR 0018.</summary>
        public bool[] FlagBoxMatrixGearCarLadder { get; set; } = Contract.EveryFlagBoxMatrix(Contract.DefaultFlagBoxGearCarLadder);

        /// <summary>Zero means "not set", so that the profile's own per-unit default applies. A driver in
        /// Fahrenheit who has never opened this page must not get a Celsius number.</summary>
        public int[] FlagBoxMatrixOilTemp { get; set; } = Contract.DefaultFlagBoxTemps();

        public int[] FlagBoxMatrixWaterTemp { get; set; } = Contract.DefaultFlagBoxTemps();

        /// <summary>
        /// Where the Rig page's canvas draws each matrix slot's tile, as ScreenInstance.LayoutX and LayoutY
        /// are for a screen: four slots, the panel's to read and write and never attached as properties.
        /// Null, or a negative position, is a tile the page lays out itself.
        /// </summary>
        public int?[] MatrixLayoutX { get; set; } = new int?[4];

        public int?[] MatrixLayoutY { get; set; } = new int?[4];

        /// <summary>
        /// The oil temperature the rig warns at, for every panel at once; zero is the profile's own
        /// default for the driver's unit. Null only in a file written before it was the rig's.
        /// </summary>
        /// <remarks>
        /// A threshold is a fact about the car, not about which corner of the rig a box is in, so the
        /// Settings page asks it once (#503). The four per-panel arrays stay, because they are what the
        /// published FlagBoxMatrix&lt;N&gt;OilTemp names read and an LED profile of any vintage reads
        /// them; NormaliseLights and <see cref="SetLightsOilTemp"/> keep all four equal to this. A file
        /// written before this existed takes the first named panel's value that is not zero, or zero
        /// when every named panel left its box at the default; a file that names no panel takes matrix
        /// 1's, which is the one a single box had. See NormaliseLightsTemps.
        /// </remarks>
        public int? LightsOilTemp { get; set; }

        /// <summary>The water temperature the rig warns at; the same shape as <see cref="LightsOilTemp"/>.</summary>
        public int? LightsWaterTemp { get; set; }

        /// <summary>What the middle of an RGB strip shows: "rpm", "brake", "throttleBrake" or "fuel".
        /// One value for the rig and not an array, because OpenDash generates one profile per strip
        /// shape rather than per device and every shape reads this one name.</summary>
        public string LedCentre { get; set; } = Contract.DefaultLedCentre;

        /// <summary>How the rev ladder fills a strip: "car", the car's whole bar from the fetched table
        /// (ADR 0018), or "leftToRight", which takes the car's thresholds and only the look is OpenDash's
        /// (ADR 0014). A file naming "meetInMiddle" or "f1", the looks #503 retired, is moved onto
        /// "leftToRight" by Normalise.</summary>
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

        /// <summary>The deprecated alias, answered from the resting state rather than from its own
        /// array, so that what is attached cannot drift from the setting it stands for.</summary>
        public bool MatrixGear(int matrix) => string.Equals(MatrixRest(matrix), "gear", StringComparison.Ordinal);

        public bool MatrixGearBlink(int matrix) => Pick(FlagBoxMatrixGearBlink, matrix, Contract.DefaultFlagBoxGearBlink);

        public bool MatrixGearBands(int matrix) => Pick(FlagBoxMatrixGearBands, matrix, Contract.DefaultFlagBoxGearBands);

        public bool MatrixGearCarLadder(int matrix) => Pick(FlagBoxMatrixGearCarLadder, matrix, Contract.DefaultFlagBoxGearCarLadder);

        /// <summary>Sets what a panel shows at rest, and the deprecated switch with it, the way
        /// SetRevBar() sets ShiftLights: a profile still reading FlagBoxMatrix&lt;N&gt;Gear sees the
        /// choice the driver just made, and the collapse in Normalise() reads a pair that agrees.</summary>
        public void SetMatrixRest(int matrix, string value)
        {
            if (matrix < 1 || matrix > Contract.FlagBoxMatrices.Count) return;
            if (Array.IndexOf(Contract.FlagBoxRests, value) < 0) return;
            FlagBoxRest[matrix - 1] = value;
            FlagBoxMatrixGear[matrix - 1] = string.Equals(value, "gear", StringComparison.Ordinal);
        }

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

        /// <summary>Which LED device one bar's profile is installed into. The Arduino's when the bar is
        /// unknown, which is what a bar written before OpenDash knew there was more than one is read as.</summary>
        public string BarDevice(string ns)
        {
            var bar = LedBarByNamespace(ns);
            return LedBar.NormaliseDevice(bar == null ? null : bar.Device);
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
            return Contract.NormaliseLedRpmStyle(value);
        }

        /// <summary>
        /// Whether anything on the rig is asking for the car's own shift pattern: what decides whether
        /// the mirror is computed at all, and since #353 what a screen reads as
        /// <see cref="Contract.CarLadderChosen"/> to decide whether to draw it.
        /// </summary>
        /// <remarks>
        /// <para>One reduction for both, so that a surface cannot be gated on an answer that leaves the
        /// numbers behind it unfilled. Any bar asking for the car's own is enough: the mirror is one
        /// computation feeding every strip, and a rig with one strip on the car's own bar and another on
        /// F1 has asked for the car's instants somewhere, which is the divergence #353 exists to close.</para>
        /// <para>The rig-wide <see cref="LedRpmStyle"/> answers only for a rig with no bars, which is
        /// where a bar with no opinion of its own would have taken it from. It is deliberately not
        /// consulted for a rig that has bars: the panel writes the style onto the bar and has written
        /// nothing rig-wide since the styles went per bar, so a rig upgraded from before that carries a
        /// stale value nobody can see or change.</para>
        /// </remarks>
        public bool AnyCarLadderWanted()
        {
            var bars = LedBarList();
            if (bars.Count == 0) return LedRpmStyle == Contract.LedRpmStyleCar;
            foreach (var bar in bars)
            {
                if (BarRpmStyle(bar.Namespace) == Contract.LedRpmStyleCar) return true;
            }
            return false;
        }

        /// <summary>
        /// Whether any flag box panel wants its digit banded on the car's own measured bar.
        ///
        /// <para>Asked beside <see cref="AnyCarLadderWanted"/> and not folded into it, because the two
        /// are different questions about the same tables: a rig may have no strip at all and a box on
        /// the wheel, and the bands are published for that rig as readily as the runs are.</para>
        /// </summary>
        public bool AnyMatrixCarLadderWanted()
        {
            foreach (var matrix in MatrixPanels())
            {
                if (MatrixGear(matrix) && MatrixGearBands(matrix) && MatrixGearCarLadder(matrix)) return true;
            }
            return false;
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

        /// <summary>Whether one bar's aid lamps read the slip estimate; the default for a bar that has gone.</summary>
        public bool BarInferSlip(string ns)
        {
            var bar = LedBarByNamespace(ns);
            return bar == null ? Contract.DefaultLedInferSlip : bar.InferSlip;
        }

        /// <summary>
        /// One bar's own brightness, or null when it follows the rig's -- which is also what a bar that
        /// has gone reads. What <c>&lt;ns&gt;LedBrightness</c> publishes: the profile falls back to
        /// LightsBrightness on a null, so a bar with no answer of its own is exactly as bright as the rig.
        /// </summary>
        public int? BarBrightness(string ns)
        {
            var bar = LedBarByNamespace(ns);
            if (bar == null || !bar.Brightness.HasValue) return null;
            return Contract.NormaliseBrightness(bar.Brightness.Value);
        }

        /// <summary>Sets one bar's own brightness, or clears it back to the rig's with null.</summary>
        public void SetBarBrightness(string ns, int? percent)
        {
            var bar = LedBarByNamespace(ns);
            if (bar == null) return;
            bar.Brightness = percent.HasValue ? Contract.NormaliseBrightness(percent.Value) : (int?)null;
        }

        /// <summary>Whether one bar draws an effect. On for a bar that has gone, which is what a strip
        /// nobody configured draws.</summary>
        public bool BarEffectEnabled(string ns, string effectId)
        {
            var bar = LedBarByNamespace(ns);
            return bar == null || bar.EffectEnabled(effectId);
        }

        /// <summary>Turns one effect on or off on one bar; any flag row switches every flag row.</summary>
        public void SetBarEffect(string ns, string effectId, bool enabled)
        {
            var bar = LedBarByNamespace(ns);
            if (bar == null) return;
            bar.SetEffect(effectId, enabled);
        }

        /// <summary>The colour one bar draws a colour setting in: its own or the default. Null for a bar that has
        /// gone or a key no colour setting has.</summary>
        public string BarColour(string ns, string key)
        {
            var bar = LedBarByNamespace(ns);
            return bar == null ? null : bar.ColourOf(key);
        }

        /// <summary>Gives one bar a colour of its own for a colour setting, or the default back where
        /// <paramref name="hex"/> is null; returns whether the bar's profile wants installing again (#694).</summary>
        public bool SetBarColour(string ns, string key, string hex)
        {
            var bar = LedBarByNamespace(ns);
            return bar != null && bar.SetColour(key, hex);
        }

        /// <summary>Whether one bar is wired from the far end. False for a bar that has gone.</summary>
        public bool BarReversed(string ns)
        {
            var bar = LedBarByNamespace(ns);
            return bar != null && bar.Reversed && bar.SupportsReversal;
        }

        /// <summary>
        /// Sets which end a bar is wired from, and says whether that changed the profile it installs.
        /// </summary>
        /// <remarks>
        /// True means the bar's profile is now the other twin and has to be installed again for SimHub
        /// to draw the change, which the caller does. A shape with a wiring of its own has no twin, so
        /// the switch is refused and nothing changes.
        /// </remarks>
        public bool SetBarReversed(string ns, bool reversed)
        {
            var bar = LedBarByNamespace(ns);
            if (bar == null || !bar.SupportsReversal) return false;
            var before = bar.ProfileShapeId;
            bar.Reversed = reversed;
            return !string.Equals(before, bar.ProfileShapeId, StringComparison.Ordinal);
        }

        /// <summary>Whether one bar is in the Fanatec wiring. False for a bar that has gone.</summary>
        public bool BarFanatec(string ns)
        {
            var bar = LedBarByNamespace(ns);
            return bar != null && bar.Fanatec && bar.SupportsFanatec;
        }

        /// <summary>
        /// Turns a bar's Fanatec compatibility mode on or off, and says whether that changed the profile it
        /// installs.
        /// </summary>
        /// <remarks>
        /// True means the bar's profile is now the other wiring and has to be installed again for SimHub to
        /// draw the change, which the caller does. The Fanatec order has no far end, so turning it on turns
        /// Reverse direction off; turning it off leaves the strip in the plain order. A shape with no Fanatec
        /// wiring, a bare run, is refused and nothing changes.
        /// </remarks>
        public bool SetBarFanatec(string ns, bool fanatec)
        {
            var bar = LedBarByNamespace(ns);
            if (bar == null || !bar.SupportsFanatec) return false;
            var before = bar.ProfileShapeId;
            bar.Fanatec = fanatec;
            if (fanatec) bar.Reversed = false;
            return !string.Equals(before, bar.ProfileShapeId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Adds a bar of a shape, with the rig's own settings as its starting point.
        /// </summary>
        /// <remarks>
        /// The rig-wide values rather than the class defaults, because somebody adding their second bar
        /// has already said what they like on the first and on the tab before bars existed. The namespace
        /// is frozen here and nowhere else moves it.
        /// </remarks>
        public LedBar AddLedBar(string shape, string name, string device)
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
                Device = device,
                // As bright as the rig, drawing everything, wired the plain way: the three a driver
                // changes on the LEDs page once they have seen the strip lit. A reversed or Fanatec shape id
                // still arrives in that wiring, which Normalise reads off the id: the Add LEDs sheet hands its
                // Fanatec compatibility mode over as `<shape>-fanatec`.
                Reversed = false,
                Fanatec = false,
                Brightness = null,
                EffectsOff = new List<string>(),
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
            SetMatrixRest(slot, "gear");
            FlagBoxFlags[i] = true;
            FlagBoxPit[i] = true;
            FlagBoxSpotter[i] = true;
            FlagBoxWarnings[i] = true;
            FlagBoxSide[i] = Contract.DefaultFlagBoxSide;
            FlagBoxMatrixCriticalOnly[i] = Contract.DefaultFlagBoxCriticalOnly;
            FlagBoxMatrixGearBlink[i] = Contract.DefaultFlagBoxGearBlink;
            FlagBoxMatrixGearBands[i] = Contract.DefaultFlagBoxGearBands;
            FlagBoxMatrixGearCarLadder[i] = Contract.DefaultFlagBoxGearCarLadder;
            // The rig's thresholds, which every panel shares: a panel added after they were set warns at
            // the same temperature as the ones already there.
            FlagBoxMatrixOilTemp[i] = LightsOilTemp ?? 0;
            FlagBoxMatrixWaterTemp[i] = LightsWaterTemp ?? 0;
            return slot;
        }

        /// <summary>Takes a panel out of its slot and puts the slot back to dark, so the profile draws
        /// nothing there rather than whatever the removed panel had been showing.</summary>
        public void RemoveMatrixPanel(int matrix)
        {
            if (matrix < 1 || matrix > Contract.FlagBoxMatrices.Count) return;
            var i = matrix - 1;
            FlagBoxMatrixName[i] = null;
            SetMatrixRest(matrix, "dark");
            FlagBoxFlags[i] = false;
            FlagBoxPit[i] = false;
            FlagBoxSpotter[i] = false;
            FlagBoxWarnings[i] = false;
            // The next panel in this slot is laid out afresh, not where the removed one was dragged.
            if (MatrixLayoutX != null && i < MatrixLayoutX.Length) MatrixLayoutX[i] = null;
            if (MatrixLayoutY != null && i < MatrixLayoutY.Length) MatrixLayoutY[i] = null;
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
            FlagBoxMatrixGearBands = Resize(FlagBoxMatrixGearBands, Contract.EveryFlagBoxMatrix(Contract.DefaultFlagBoxGearBands), v => true);
            FlagBoxMatrixGearCarLadder = Resize(FlagBoxMatrixGearCarLadder, Contract.EveryFlagBoxMatrix(Contract.DefaultFlagBoxGearCarLadder), v => true);
            FlagBoxMatrixOilTemp = Resize(FlagBoxMatrixOilTemp, Contract.DefaultFlagBoxTemps(), v => v >= 0);
            FlagBoxMatrixWaterTemp = Resize(FlagBoxMatrixWaterTemp, Contract.DefaultFlagBoxTemps(), v => v >= 0);
            MatrixLayoutX = Resize(MatrixLayoutX, new int?[Contract.FlagBoxMatrices.Count], v => v >= 0);
            MatrixLayoutY = Resize(MatrixLayoutY, new int?[Contract.FlagBoxMatrices.Count], v => v >= 0);
            // After the arrays are four long, so the migration has four slots to fill.
            MigrateFlagBoxToMatrices();
            // After that, because a file may carry the gear switch under either spelling and the
            // collapse has to read whichever one it ended up in.
            CollapseFlagBoxGearIntoRest();
            // After the arrays are four long and the old scalars have been emptied into them, because
            // which panels a migrating rig keeps is read off what those panels were doing.
            NormaliseMatrixPanels();
            // After both of those: a file that predates the rig-wide thresholds takes a named panel's,
            // which may be where a legacy scalar just went, and which panels are named is only known
            // once the names are.
            NormaliseLightsTemps();
            NormaliseLedBars();
            // No array to repair: the strips carry one value each for the whole rig. A profile reads
            // both through isnull() with its own default, so an unrecognised spelling has to become a
            // legal one here rather than reaching the strip as itself.
            LedCentre = Contract.NormaliseLedCentre(LedCentre);
            LedRpmStyle = Contract.NormaliseLedRpmStyle(LedRpmStyle);
            LedMirrorFit = Contract.NormaliseChoice(LedMirrorFit, Contract.LedMirrorFits, Contract.DefaultLedMirrorFit);
        }

        /// <summary>
        /// The rig's two thresholds, taken from the first panel in use when a file predates them, and
        /// written into every panel's entry so that what is published per panel is the rig's answer.
        /// </summary>
        /// <remarks>
        /// The named slots rather than slot 1, because removing a panel leaves its thresholds behind in
        /// the slot: a rig that added Left and Right and then removed Left still has Left's numbers in
        /// slot 1, and they are not what the box on the rig warns at. A file with no names at all is from
        /// before panels were instances, and there slot 1 was the one box.
        ///
        /// Among the named panels, the first that set a number rather than the first in order: zero is
        /// "the profile's default", which a panel holds because nobody touched its box, so a Left at 0
        /// beside a Right at 130 is a rig that asked for 130 once. Oil and water are chosen apart, for
        /// the same reason. Zero only when no named panel set one.
        /// </remarks>
        private void NormaliseLightsTemps()
        {
            if (!LightsOilTemp.HasValue) LightsOilTemp = LegacyTemp(FlagBoxMatrixOilTemp);
            if (!LightsWaterTemp.HasValue) LightsWaterTemp = LegacyTemp(FlagBoxMatrixWaterTemp);
            if (LightsOilTemp.Value < 0) LightsOilTemp = 0;
            if (LightsWaterTemp.Value < 0) LightsWaterTemp = 0;
            FillTemps();
        }

        /// <summary>A threshold for a file that predates the rig's: the first named panel's that is
        /// not zero, or matrix 1's when no panel is named. See <see cref="NormaliseLightsTemps"/>.</summary>
        private int LegacyTemp(int[] temps)
        {
            var named = false;
            if (FlagBoxMatrixName != null)
            {
                for (var i = 0; i < FlagBoxMatrixName.Length; i++)
                {
                    if (FlagBoxMatrixName[i] == null) continue;
                    named = true;
                    var value = Pick(temps, i + 1, 0);
                    if (value > 0) return value;
                }
            }
            return named ? 0 : Pick(temps, 1, 0);
        }

        private void FillTemps()
        {
            if (FlagBoxMatrixOilTemp == null || FlagBoxMatrixOilTemp.Length != Contract.FlagBoxMatrices.Count) FlagBoxMatrixOilTemp = Contract.DefaultFlagBoxTemps();
            if (FlagBoxMatrixWaterTemp == null || FlagBoxMatrixWaterTemp.Length != Contract.FlagBoxMatrices.Count) FlagBoxMatrixWaterTemp = Contract.DefaultFlagBoxTemps();
            for (var i = 0; i < Contract.FlagBoxMatrices.Count; i++)
            {
                FlagBoxMatrixOilTemp[i] = LightsOilTemp ?? 0;
                FlagBoxMatrixWaterTemp[i] = LightsWaterTemp ?? 0;
            }
        }

        /// <summary>Sets the oil temperature the rig warns at, on every panel. Zero, or anything below
        /// it, is the profile's own default for the driver's unit.</summary>
        public void SetLightsOilTemp(int value)
        {
            LightsOilTemp = value < 0 ? 0 : value;
            if (!LightsWaterTemp.HasValue) LightsWaterTemp = LegacyTemp(FlagBoxMatrixWaterTemp);
            FillTemps();
        }

        /// <summary>Sets the water temperature the rig warns at, on every panel.</summary>
        public void SetLightsWaterTemp(int value)
        {
            LightsWaterTemp = value < 0 ? 0 : value;
            if (!LightsOilTemp.HasValue) LightsOilTemp = LegacyTemp(FlagBoxMatrixOilTemp);
            FillTemps();
        }

        /// <summary>Night mode on or off, from a wheel button or the sidebar. Returns the new state.</summary>
        public bool ToggleNightMode()
        {
            LightsNightMode = !LightsNightMode;
            return LightsNightMode;
        }

        /// <summary>
        /// Moves the brightness in force by a number of steps, and returns where it landed.
        /// </summary>
        /// <remarks>
        /// The night brightness while night mode is on and the day one otherwise, because a driver
        /// pressing "dimmer" in a dark room means the lights they are looking at. Clamped to
        /// <see cref="Contract.BrightnessStepFloor"/> and 100: a button never takes the lights out, and
        /// a brightness already set below the floor on the panel is left where it is rather than raised
        /// by a press meant to lower it. #503.
        /// </remarks>
        public int StepBrightness(int steps)
        {
            var current = LightsNightMode ? LightsNightBrightness : LightsBrightness;
            var floor = Math.Min(current, Contract.BrightnessStepFloor);
            var next = current + steps * Contract.BrightnessStep;
            if (next < floor) next = floor;
            if (next > 100) next = 100;
            if (LightsNightMode) LightsNightBrightness = next;
            else LightsBrightness = next;
            return next;
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

        /// <summary>
        /// Collapses the gear switch into the resting state, which is the one thing the two of them
        /// were between them saying.
        /// </summary>
        /// <remarks>
        /// The profile drew the gear where the resting state was "gear" *and* the switch was on, so a
        /// panel resting dark ignored the switch and a panel resting on the gear was decided by the
        /// switch alone. What goes into the resting state here is that conjunction, which is what the
        /// driver was looking at either way; the switch is then kept agreeing with it rather than
        /// cleared as the legacy scalars above are, because it is published and an LED profile of an
        /// earlier vintage still reads it (ADR 0003).
        ///
        /// A switch nobody has touched is absent from the file and comes back on, so it vetoes
        /// nothing: a driver who set a panel's resting state and never met the switch keeps it. And
        /// the pass is idempotent, since every write since keeps the pair agreeing, so a file written
        /// by this version goes through it unchanged.
        /// </remarks>
        private void CollapseFlagBoxGearIntoRest()
        {
            for (var i = 0; i < FlagBoxRest.Length; i++)
            {
                if (!FlagBoxMatrixGear[i]) FlagBoxRest[i] = "dark";
                FlagBoxMatrixGear[i] = string.Equals(FlagBoxRest[i], "gear", StringComparison.Ordinal);
            }
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

        /// <summary>The release the last answered check offered, or null when it found this rig current. Persisted
        /// because the check runs once a day and SimHub starts more often than that, and the idle screen's mark is
        /// read from it on every start between; see UpdateMark.Remember. #83.</summary>
        public string OfferedRelease { get; set; }

        /// <summary>The release a person said yes to replacing their edited dashboards for, or null. Written when an
        /// update stages the plugin, read and cleared by that plugin's first start, which is what writes the
        /// dashboards; see EditedConsent.</summary>
        public string ReplaceEditedFor { get; set; }

        /// <summary>Fingerprint of each dashboard folder as OpenDash last wrote it, keyed by folder name. An entry
        /// that no longer matches what is on disk is somebody's Dash Studio work; see FolderFingerprint. Kept here
        /// rather than beside the dashboard so that nothing OpenDash writes into DashTemplates can confuse SimHub's
        /// own scanner.</summary>
        public Dictionary<string, string> FolderFingerprints { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Fingerprint of SimHub/OpenDash/OpenDash Flag box.ledsprofile as OpenDash last wrote it, or null
        /// before it has written one. The file's counterpart of <see cref="FolderFingerprints"/>: a file that no
        /// longer matches it is the driver's, and FlagBoxProfile.Extract leaves it alone (#618).</summary>
        public string FlagBoxFingerprint { get; set; }

        /// <summary>Clamps every value into its contract: unknown modes and card numbers fall back to the defaults,
        /// a short or missing slot array is padded with the default assignment, a long one is truncated.</summary>
        public void Normalise()
        {
            RevBar = RevBarMode();
            ShiftLights = RevBar == Contract.RevBarShift;
            PositionMode = Contract.NormaliseChoice(PositionMode, Contract.PositionModes, Contract.DefaultPositionMode);
            DeltaReference = Contract.NormaliseChoice(DeltaReference, Contract.DeltaReferences, Contract.DefaultDeltaReference);
            DeltaPrecision = Contract.NormaliseChoice(DeltaPrecision, Contract.DeltaPrecisions, Contract.DefaultDeltaPrecision);
            SessionProgress = Contract.NormaliseChoice(SessionProgress, Contract.SessionProgressModes, Contract.DefaultSessionProgress);
            BlueFlagDetail = Contract.NormaliseChoice(BlueFlagDetail, Contract.BlueFlagDetails, Contract.DefaultBlueFlagDetail);
            DriverNameFormat = Contract.NormaliseChoice(DriverNameFormat, Contract.DriverNameFormats, Contract.DefaultDriverNameFormat);
            ClockFormat = Contract.NormaliseChoice(ClockFormat, Contract.ClockFormats, Contract.DefaultClockFormat);
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
            SpellFoldersAsWritten(kept);
            AnswerUnclaimed(kept);
            // Spent: MigratedRig() has emptied it into the rig, and a second copy left behind would be
            // serialised, read by something one day, and disagree.
            if (Faces != null && Faces.Count > 0) Faces = new Dictionary<string, FaceSettings>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Spells a folder an older plugin gave a trailing dot or space as Windows wrote it.
        /// </summary>
        /// <remarks>
        /// A screen named "Rim." was given the folder "OpenDash Rim." (#620). Windows wrote it as "OpenDash Rim", with
        /// a main dashboard SimHub never looks for inside, and on a rig that already had a Rim it wrote it over that
        /// screen's folder. Spelled as it is on disk, the folder holds no dashboard under its own name, so the next
        /// start reads it as not installed and writes one SimHub lists. Where another screen holds that folder,
        /// the two have been writing over each other, and this one is given a folder of its own instead. The folder
        /// is spelled anew here and nowhere else for the reason a stock folder's case is (#467): every caller then
        /// reads the one spelling.
        /// </remarks>
        private static void SpellFoldersAsWritten(List<ScreenInstance> rig)
        {
            foreach (var screen in rig)
            {
                if (screen.Folder == null) continue;
                var written = PackageCatalogue.FolderOnDisk(screen.Folder);
                if (string.Equals(written, screen.Folder, StringComparison.Ordinal)) continue;
                var others = rig.Where(other => !ReferenceEquals(other, screen) && other.Folder != null).Select(other => other.Folder).ToList();
                var shared = written.Length == 0
                    || others.Any(folder => string.Equals(PackageCatalogue.FolderOnDisk(folder), written, StringComparison.OrdinalIgnoreCase));
                screen.Folder = shared ? PackageCatalogue.UniqueFolder(screen.Name, others, null) : written;
            }
        }

        /// <summary>
        /// The rig a settings file written before ADR 0017 describes.
        /// </summary>
        /// <remarks>
        /// A file that has never installed anything is a new install, and its rig is empty: that is the
        /// first-run state the panel teaches from, and #85's point that an empty rig is one fewer
        /// surface than a wizard.
        ///
        /// Any other file gets one screen per folder OpenDash has written, because those are the
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
                // A card face whose folder spells no size, which is both round ones, would be Slots0x0 here, and the
                // second would then be dropped as a repeat of the first. Its folder is what tells them apart at this
                // moment, and it is asked once: the namespace names nothing and is frozen from here, so neither the
                // size the start repairs nor a folder spelled anew moves it (#474).
                screen.Namespace = screen.IsSlots && (width <= 0 || height <= 0)
                    ? "Slots" + Contract.Slug(folder)
                    : screen.StockNamespace;
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
            // Every one of them is the migration's guess at the rig rather than the driver's choice, and
            // stays so until the driver keeps it or removes it: the fact the Rig tab's line reads.
            foreach (var screen in rig) screen.Unclaimed = true;
            return rig;
        }

        /// <summary>
        /// Answers, for a rig written before a screen said so, whether each screen is one the migration
        /// made and the driver has not yet kept (#478).
        /// </summary>
        /// <remarks>
        /// Every release from 0.3.0-rc.1 migrated a rig and wrote it back without recording which screens
        /// the migration had made, and the panel guessed from how many there were. What such a rig still
        /// holds answers part of the question, and the rest is answered conservatively.
        ///
        /// Which screens the migration made is answered for most of them. A screen added from the Rig tab
        /// has always recorded the package it was made from, and the migration recorded none; the start
        /// has since filled one in for the migrated screens whose folder spells no size, which are the
        /// companion, the pit wall and the round faces, and those can no longer be told from an added one.
        ///
        /// Whether the driver has kept one is answered only where keeping it left a trace: a rename left
        /// the name, and a resize left a package. A screen configured since left none, its settings having
        /// been carried over from the old file. Whether the driver has removed any is answered by the
        /// folder record, which forgets nothing, so a folder OpenDash wrote that no screen holds any more
        /// is a screen somebody removed or moved.
        ///
        /// A migrated screen therefore counts as unclaimed only while the rig is still as the migration
        /// left it and the screen still carries the name the migration gave it. A rig whose driver has
        /// begun to remove screens is taken as dealt with, because telling somebody to remove dashboards
        /// is the worse mistake to make about a rig whose remaining screens are all theirs.
        /// </remarks>
        private void AnswerUnclaimed(List<ScreenInstance> rig)
        {
            if (!rig.Any(screen => screen.Unclaimed == null)) return;
            var asMigrated = FolderFingerprints == null || FolderFingerprints.Keys.All(folder =>
                rig.Any(screen => string.Equals(screen.Folder, folder, StringComparison.OrdinalIgnoreCase)));
            foreach (var screen in rig)
            {
                if (screen.Unclaimed != null) continue;
                screen.Unclaimed = asMigrated
                    && screen.Package == null
                    && string.Equals(screen.Name, screen.SizeLabel, StringComparison.Ordinal);
            }
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

        /// <summary>Holds, and releases, one screen's glance, whatever its kind. A namespace the rig no
        /// longer holds does nothing, since its action stays bound until SimHub restarts.</summary>
        public void BeginScreenGlance(string ns)
        {
            var screen = ScreenByNamespace(ns);
            if (screen != null) screen.BeginQuickGlance();
        }

        public void EndScreenGlance(string ns)
        {
            EndScreenGlance(ns, DateTime.UtcNow);
        }

        /// <summary>The same, released at a given moment, from which a companion's way back is timed.</summary>
        public void EndScreenGlance(string ns, DateTime now)
        {
            var screen = ScreenByNamespace(ns);
            if (screen != null) screen.EndQuickGlance(now);
        }

        /// <summary>The page one zone of one pit wall shows, by the key naming its page and slot.</summary>
        public int ScreenZone(string ns, string key)
        {
            var slot = Contract.PitWallZoneSlotByKey(key);
            if (slot == null) throw new ArgumentOutOfRangeException("key");
            var screen = ScreenByNamespace(ns);
            // A delegate outlives the screen it was attached for until SimHub restarts, so the reader has
            // to answer something rather than throw on SimHub's data thread.
            return screen == null ? slot.Fallback : screen.ZonePage(key);
        }

        /// <summary>
        /// The landscape page one pit wall shows.
        /// </summary>
        /// <remarks>
        /// The package gates each of its three screens on this, so it is what decides which one is up.
        /// One name and not a saved one beside a live one, because a pit wall's page is configuration:
        /// it is set once with a mouse and then left, so there is nothing to move it at speed and
        /// nothing to put back afterwards.
        /// </remarks>
        public int ScreenPitWallPage(string ns)
        {
            var screen = ScreenByNamespace(ns);
            // A delegate outlives the screen it was attached for until SimHub restarts, so the reader
            // has to answer something rather than throw on SimHub's data thread.
            return screen == null ? Contract.DefaultPitWallPage : Contract.NormalisePitWallPage(screen.PitWallPage);
        }

        /// <summary>The module one companion is being forced onto, or -1 once the window has passed.</summary>
        public int ScreenCompanionOpenOn(string ns)
        {
            return ScreenCompanionOpenOn(ns, DateTime.UtcNow);
        }

        /// <summary>The same, as it reads at a given moment.</summary>
        public int ScreenCompanionOpenOn(string ns, DateTime now)
        {
            var screen = ScreenByNamespace(ns);
            return screen == null ? Contract.DefaultCompanionOpenOn : screen.CompanionOpenOnAt(now);
        }

        /// <summary>How one companion draws a flag: off, the strip at the foot, or over the module.</summary>
        public string ScreenCompanionFlagFormat(string ns)
        {
            var screen = ScreenByNamespace(ns);
            return screen == null ? Contract.DefaultCompanionFlagFormat : Contract.NormaliseCompanionFlagFormat(screen.CompanionFlagFormat);
        }

        /// <summary>Whether one pit wall lists the player's own class rather than the whole field.</summary>
        public bool ScreenPitWallClassOnly(string ns)
        {
            var screen = ScreenByNamespace(ns);
            return screen == null ? Contract.DefaultPitWallClassOnly : screen.PitWallClassOnly;
        }

        /// <summary>How one pit wall draws a flag: off, a band under the header, or over the body.</summary>
        public string ScreenPitWallFlagFormat(string ns)
        {
            var screen = ScreenByNamespace(ns);
            return screen == null ? Contract.DefaultPitWallFlagFormat : Contract.NormalisePitWallFlagFormat(screen.PitWallFlagFormat);
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
        /// <remarks>
        /// A screen the rig no longer holds is left alone and reads its default, rather than cycling the
        /// shared orphan ScreenFace hands the property readers: its button stays bound until SimHub
        /// restarts, and a press on it would otherwise move a default every other removed face reads.
        /// </remarks>
        public int CycleScreenZone(string ns, string letter)
        {
            var screen = ScreenByNamespace(ns);
            if (screen == null || screen.Face == null) return ScreenFace(ns).Zone(letter);
            return screen.Face.Cycle(letter);
        }

        /// <summary>Moves one zone of one screen back a page, and returns the page it landed on. A screen
        /// the rig no longer holds is left alone, for the reason <see cref="CycleScreenZone"/> gives.</summary>
        public int CycleScreenZoneBack(string ns, string letter)
        {
            var screen = ScreenByNamespace(ns);
            if (screen == null || screen.Face == null) return ScreenFace(ns).Zone(letter);
            return screen.Face.CycleBack(letter);
        }

        /// <summary>Every zone of every face back on the page it opens on, which is what Init does.</summary>
        public void OpenRigOnStartPages()
        {
            foreach (var screen in FaceScreens()) screen.Face.OpenOnStartPages();
        }

        /// <summary>
        /// Adds a copy of a screen and returns it, under a name, a namespace and a folder nothing else
        /// on the rig holds. The caller installs it.
        /// </summary>
        /// <remarks>
        /// Everything the driver set on the source comes with it -- the zones, their orders, the bar,
        /// the glance, the flag format, a pit wall's pages, a companion's rotation -- because that is
        /// what "duplicate" is for: a second screen set up like the first, to be changed from there.
        /// What does not come is what makes it a different screen: its namespace, which its properties
        /// carry, and its folder, which its dashboard lives in; and its place on the Rig page, which is
        /// the driver's to choose. A face opens on its start pages, as every face does on a start.
        /// Null when the rig has no screen of that namespace. #503.
        ///
        /// The panel calls the overload that takes the catalogue, which finds the source's package as the
        /// installer does. Most screens on an upgraded rig remember no package, so a lookup by
        /// <see cref="ScreenInstance.Package"/> alone finds nothing for them.
        /// </remarks>
        public ScreenInstance DuplicateScreen(string ns, IEnumerable<PackageEntry> catalogue, string name = null)
        {
            var source = ScreenByNamespace(ns);
            if (source == null) return null;
            // No package makes it, so there is nothing to install a copy from: the Duplicate that adds a
            // card the installer then cannot write is the worse answer.
            var entry = PackageCatalogue.EntryFor(catalogue, source);
            return entry == null ? null : DuplicateScreen(ns, entry, name);
        }

        /// <summary>
        /// Adds a copy of a screen made from a package already found; see the overload that finds it.
        /// </summary>
        /// <remarks>
        /// <paramref name="entry"/> is the package the source is written from, found by
        /// <see cref="PackageCatalogue.EntryFor"/>, and its folder is reserved as
        /// <see cref="PackageCatalogue.NewScreen"/> reserves it: the copy never takes its package's stock
        /// folder, even with the stock screen removed, since the stock folder goes with the stock
        /// namespace and only the first screen at a size holds those. The copy remembers the package,
        /// which a source migrated from folders did not.
        /// </remarks>
        public ScreenInstance DuplicateScreen(string ns, PackageEntry entry, string name = null)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            var source = ScreenByNamespace(ns);
            if (source == null) return null;
            var taken = new List<string>();
            var names = new List<string>();
            var folders = new List<string>();
            foreach (var screen in Rig)
            {
                if (screen == null) continue;
                taken.Add(screen.Namespace);
                names.Add(screen.Name);
                if (screen.Folder != null) folders.Add(screen.Folder);
            }
            var copy = source.Copy();
            var wanted = string.IsNullOrWhiteSpace(name) ? source.Name : name.Trim();
            copy.Name = PackageCatalogue.UniqueName(wanted, names);
            copy.Namespace = PackageCatalogue.UniqueNamespace(copy.Name, new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase));
            copy.Folder = PackageCatalogue.UniqueFolder(copy.Name, folders, entry.Folder);
            copy.Package = entry.Package;
            copy.Theme = entry.Theme;
            copy.Unclaimed = false;
            // Laid out by the Rig page until the driver drags it: on top of the source is nowhere.
            copy.LayoutX = null;
            copy.LayoutY = null;
            copy.Normalise();
            if (copy.Face != null) copy.Face.OpenOnStartPages();
            Rig.Add(copy);
            return copy;
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
            // means removing one deletes the other's dashboard, which is what "OpenDash rim" twice on one
            // rig did.
            var folders = new List<string>();
            foreach (var screen in Rig)
            {
                if (screen != null && screen.Folder != null) folders.Add(screen.Folder);
            }
            var added = PackageCatalogue.NewScreen(entry, PackageCatalogue.UniqueName(wanted, names), taken, folders);
            // Chosen, which is the one thing a screen the migration made is not.
            added.Unclaimed = false;
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
            screen.Theme = entry.Theme;
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
            // Without the product's own name in front of it. The name box opens on "OpenDash 0/9/0",
            // which is right for SimHub's profile list and reads as `LedOpenDash090` in a property
            // name; what a driver wants to find in the property list is what they called the bar.
            var wanted = name ?? string.Empty;
            if (wanted.StartsWith(FlagBoxProfile.FilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                wanted = wanted.Substring(FlagBoxProfile.FilePrefix.Length);
            }
            // Slugged with the prefix already on it, not bolted on after. Slug drops a *leading* run of
            // digits, because a namespace beginning with one reads as a number wherever a property is
            // parsed -- so slugging "0/9/0" on its own gives nothing at all, and slugging "Led0/9/0"
            // gives Led090, which is the name a driver would look for.
            var slug = Contract.Slug("Led" + wanted);
            if (slug.Length <= 3) slug = "LedStrip";
            var candidate = slug;
            var n = 2;
            while (taken.Contains(candidate) || Contract.IsReservedNamespace(candidate))
            {
                candidate = slug + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
                    Unclaimed = false,
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
            RevBar = Contract.MigrateRevBar(mode);
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
            OpenOnStartPages(DateTime.UtcNow);
        }

        /// <summary>The same, with the companions' window measured from a given moment.</summary>
        public void OpenOnStartPages(DateTime now)
        {
            foreach (var screen in FaceScreens()) screen.Face.OpenOnStartPages();
            foreach (var screen in RigScreens())
            {
                if (screen != null && screen.IsCompanion) screen.OpenOnStartModule(now);
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

        /// <summary>
        /// A normalised copy of these settings, for saving without repairing the live object.
        /// </summary>
        /// <remarks>
        /// What a rig button saves after its press. Normalise moves a zone off a page its cycle has
        /// turned off, which is right on a load and after a click in the panel, and wrong while a quick
        /// glance is held on such a page: the brightness step would move the glanced zone under the
        /// driver's thumb. The copy is repaired and written, and the zone on screen stays where the
        /// glance put it until the release puts it back. #503.
        ///
        /// SimHub writes the whole object it is handed, so the copy has to carry every persisted
        /// property, the plugin's own included: a copy without the update opt-out or the folder
        /// fingerprints would put a file on disk that turns the checks back on and forgets which
        /// dashboards the driver edited, for as long as nothing saves the live settings over it.
        /// </remarks>
        public OpenDashSettings NormalisedCopy()
        {
            var copy = new OpenDashSettings();
            copy.CopyFrom(this);
            copy.Normalise();
            return copy;
        }

        /// <summary>Copies every persisted value of another settings object, then normalises;
        /// <see cref="NormalisedCopy"/> is built on it.</summary>
        public void CopyFrom(OpenDashSettings other)
        {
            if (other == null) return;
            ShiftLights = other.ShiftLights;
            RevBar = other.RevBar;
            PositionMode = other.PositionMode;
            DeltaReference = other.DeltaReference;
            DeltaPrecision = other.DeltaPrecision;
            SessionProgress = other.SessionProgress;
            BlueFlagDetail = other.BlueFlagDetail;
            DriverNameFormat = other.DriverNameFormat;
            DriverNameTeam = other.DriverNameTeam;
            ClockFormat = other.ClockFormat;
            FlagsInPitLane = other.FlagsInPitLane;
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
            LightsOilTemp = other.LightsOilTemp;
            LightsWaterTemp = other.LightsWaterTemp;
            FlagBoxMatrixCriticalOnly = other.FlagBoxMatrixCriticalOnly == null ? null : (bool[])other.FlagBoxMatrixCriticalOnly.Clone();
            FlagBoxMatrixGear = other.FlagBoxMatrixGear == null ? null : (bool[])other.FlagBoxMatrixGear.Clone();
            FlagBoxMatrixGearBlink = other.FlagBoxMatrixGearBlink == null ? null : (bool[])other.FlagBoxMatrixGearBlink.Clone();
            FlagBoxMatrixGearBands = other.FlagBoxMatrixGearBands == null ? null : (bool[])other.FlagBoxMatrixGearBands.Clone();
            FlagBoxMatrixGearCarLadder = other.FlagBoxMatrixGearCarLadder == null ? null : (bool[])other.FlagBoxMatrixGearCarLadder.Clone();
            FlagBoxMatrixName = other.FlagBoxMatrixName == null ? null : (string[])other.FlagBoxMatrixName.Clone();
            LedBars = other.LedBars == null ? null : other.LedBars.Select(bar => bar == null ? null : bar.Copy()).ToList();
            FlagBoxMatrixOilTemp = other.FlagBoxMatrixOilTemp == null ? null : (int[])other.FlagBoxMatrixOilTemp.Clone();
            FlagBoxMatrixWaterTemp = other.FlagBoxMatrixWaterTemp == null ? null : (int[])other.FlagBoxMatrixWaterTemp.Clone();
            MatrixLayoutX = other.MatrixLayoutX == null ? null : (int?[])other.MatrixLayoutX.Clone();
            MatrixLayoutY = other.MatrixLayoutY == null ? null : (int?[])other.MatrixLayoutY.Clone();
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
            // The plugin's own fields, which no panel row writes and which were never carried until a
            // rig press began saving a copy: the update opt-out and what it last heard (ADR 0012), the
            // edited-dashboard consent, and the fingerprints that tell a driver's Dash Studio work and
            // their edited flag box file from OpenDash's. The dictionary is cloned under its own comparer, so that two spellings a
            // case-sensitive one kept apart cannot collide in the copy and throw.
            CheckForUpdates = other.CheckForUpdates;
            LastUpdateCheckTicks = other.LastUpdateCheckTicks;
            OfferedRelease = other.OfferedRelease;
            ReplaceEditedFor = other.ReplaceEditedFor;
            FolderFingerprints = other.FolderFingerprints == null
                ? null
                : new Dictionary<string, string>(other.FolderFingerprints, other.FolderFingerprints.Comparer);
            FlagBoxFingerprint = other.FlagBoxFingerprint;
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
