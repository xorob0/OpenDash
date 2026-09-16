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
        public const string RevBar = "RevBar";
        public const string PositionMode = "PositionMode";
        public const string DeltaReference = "DeltaReference";
        public const string SessionProgress = "SessionProgress";
        public const string PitWallWide = "PitWallWide";
        public const string WebViewUrl = "WebViewUrl";

        /// <summary>The lights. Not a screen, but their settings are properties for the same reason the
        /// screens' are (ADR 0003); ADR 0013 is why openDash lights a box at all.
        ///
        /// Named Lights* rather than FlagBox* on purpose: a driver who owns a flag box probably owns
        /// other lights, and "how bright, and is it night" is one answer for a rig rather than one per
        /// device. A property name is a public interface, so the rename would have to happen later.</summary>
        public const string LightsBrightness = "LightsBrightness";
        public const string LightsNightBrightness = "LightsNightBrightness";
        public const string LightsNightMode = "LightsNightMode";

        /// <summary>Quiet until something matters: the box shows only the flags that mean slow down or
        /// are addressed to this car. Flag-box-specific, because it is about flags rather than lights.</summary>
        public const string FlagBoxCriticalOnly = "FlagBoxCriticalOnly";

        /// <summary>The gear as the box's resting state. Off leaves the panel dark rather than showing
        /// something else.</summary>
        public const string FlagBoxGear = "FlagBoxGear";

        /// <summary>Laps, not litres: a litre threshold means nothing without knowing the car.</summary>
        public const string FlagBoxLowFuelLaps = "FlagBoxLowFuelLaps";

        /// <summary>Degrees in SimHub's own unit. A driver in Fahrenheit who sets 120 and gets a Celsius
        /// threshold has been given a broken feature.</summary>
        public const string FlagBoxOilTemp = "FlagBoxOilTemp";
        public const string FlagBoxWaterTemp = "FlagBoxWaterTemp";

        /// <summary>The RGB strips. Every generated .ledsprofile reads these two and nothing else of its
        /// own, so they are the whole of what a driver can say about a strip: what its middle shows, and
        /// how its rev ladder fills. Named Led* rather than Strip* because the family they configure is
        /// the LED strip driver's, which is what SimHub calls it.
        ///
        /// Not per device, unlike the flag box's matrix groups. openDash generates one profile per strip
        /// shape rather than per box, a driver selects the one that matches the hardware, and every
        /// shape reads the same two names; a per-device group would be a group per LED count, which is
        /// a number rather than a thing somebody owns.</summary>
        public const string LedCentre = "LedCentre";
        public const string LedRpmStyle = "LedRpmStyle";

        /// <summary>Whether a flag on a strip moves at all. Off holds every flag from the frame it would
        /// have settled on and never turns one off, which is what a driver who finds a blinking rim
        /// distracting is asking for. Appended after the other two, which are pinned in order.</summary>
        public const string LedFlagAnimation = "LedFlagAnimation";

        public const bool DefaultShiftLights = true;

        /// <summary>
        /// What the top of a rectangular face carries: the shift lights, a plain RPM bar, or nothing
        /// at all -- in which case the face is drawn in its second arrangement, with the well's room
        /// given back to the zones. A mode rather than a second boolean, because the three are one
        /// decision and two booleans would have a fourth state that means nothing.
        ///
        /// <para>"shift" names the state and not the source. Which ladder lights it is the car's
        /// business rather than a setting: the car's own RPMs where it publishes them, SimHub's bands
        /// where it does not (ADR 0014). There is no fourth value for that and there should not be
        /// one. The other half of this comment is REV_BAR_MODES in packages/dash/src/contract.ts,
        /// and the two are kept saying the same thing.</para>
        /// </summary>
        public static readonly string[] RevBarModes = { "shift", "rpm", "off" };
        public const string RevBarShift = "shift";
        public const string RevBarRpm = "rpm";
        public const string RevBarOff = "off";
        public const string DefaultRevBar = RevBarShift;

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

        /// <summary>Percent. SimHub's own global brightness for the device applies on top of this.</summary>
        public const int DefaultLightsBrightness = 100;

        /// <summary>Percent, at night. Sixty-four LEDs at full output beside a wheel in a dark room is
        /// genuinely too bright, and no amount of good colour choice fixes it.</summary>
        public const int DefaultLightsNightBrightness = 25;

        /// <summary>Off. A switch the driver flips, not a time of day we guess at.</summary>
        public const bool DefaultLightsNightMode = false;

        /// <summary>On: the gear is what the box shows when nothing is happening.</summary>
        public const bool DefaultFlagBoxGear = true;

        public const int DefaultFlagBoxLowFuelLaps = 2;

        /// <summary>120 C and 110 C, and their equivalents, so a default is right in whatever unit is set.
        /// Indexed by SimHub's TemperatureUnit spelling, "Celcius" included.</summary>
        public static readonly IReadOnlyDictionary<string, int> DefaultOilTemp =
            new Dictionary<string, int> { { "Celcius", 120 }, { "Fahrenheit", 248 }, { "Kelvin", 393 } };

        public static readonly IReadOnlyDictionary<string, int> DefaultWaterTemp =
            new Dictionary<string, int> { { "Celcius", 110 }, { "Fahrenheit", 230 }, { "Kelvin", 383 } };

        /// <summary>The four matrix contents SimHub composes. A device is the same shape of thing as a
        /// screen, so it owns its settings as one group (XOR-124), prefixed, exactly as a face does.
        ///
        /// Rotation and serpentine wiring are deliberately absent: they are SimHub device settings decided
        /// by the corner the data cable enters, and a second place to set them is a second place to
        /// disagree. Presets are absent too -- openDash has no store, and a setting *is* a property.</summary>
        public static readonly IReadOnlyList<int> FlagBoxMatrices = new[] { 1, 2, 3, 4 };

        /// <summary>What a matrix shows when nothing has taken it over.</summary>
        public static readonly string[] FlagBoxRests = { "dark", "gear" };

        /// <summary>Which side of the rig a box is on. Getting this wrong is worse than having no box:
        /// one to the left of the wheel lighting for a car on the right is actively dangerous.</summary>
        public static readonly string[] FlagBoxSides = { "both", "left", "right" };

        /// <summary>`FlagBoxMatrix1Rest` and its siblings.</summary>
        public static string FlagBoxMatrixProperty(int matrix, string name)
        {
            if (matrix < 1 || matrix > 4) throw new ArgumentOutOfRangeException(nameof(matrix), matrix, "matrix must be 1..4");
            return "FlagBoxMatrix" + matrix + name;
        }

        /// <summary>The five names of one matrix, in attachment order.</summary>
        public static IEnumerable<string> FlagBoxMatrixProperties(int matrix)
        {
            yield return FlagBoxMatrixProperty(matrix, "Rest");
            yield return FlagBoxMatrixProperty(matrix, "Flags");
            yield return FlagBoxMatrixProperty(matrix, "Pit");
            yield return FlagBoxMatrixProperty(matrix, "Spotter");
            yield return FlagBoxMatrixProperty(matrix, "Warnings");
            yield return FlagBoxMatrixProperty(matrix, "Side");
        }

        /// <summary>Matrix 1 does everything, 2 to 4 are off: one box works out of the box.</summary>
        public static bool DefaultFlagBoxMatrixOn(int matrix) => matrix == 1;

        public static string DefaultFlagBoxMatrixRest(int matrix) => matrix == 1 ? "gear" : "dark";

        public const string DefaultFlagBoxSide = "both";

        public static string[] DefaultFlagBoxRests()
        {
            var rests = new string[FlagBoxMatrices.Count];
            for (var i = 0; i < rests.Length; i++) rests[i] = DefaultFlagBoxMatrixRest(i + 1);
            return rests;
        }

        public static bool[] DefaultFlagBoxOn()
        {
            var on = new bool[FlagBoxMatrices.Count];
            for (var i = 0; i < on.Length; i++) on[i] = DefaultFlagBoxMatrixOn(i + 1);
            return on;
        }

        public static string[] DefaultFlagBoxSides()
        {
            var sides = new string[FlagBoxMatrices.Count];
            for (var i = 0; i < sides.Length; i++) sides[i] = DefaultFlagBoxSide;
            return sides;
        }

        /// <summary>Off. A box that stays dark through a chequered flag is a surprise, and a surprise is
        /// a worse default than a busy one.</summary>
        public const bool DefaultFlagBoxCriticalOnly = false;

        /// <summary>What the middle of a strip shows: the revs, the brake, throttle and brake from the
        /// middle outwards, or the fuel. Mirrors LED_CENTRES in contract.ts.</summary>
        public static readonly string[] LedCentres = { "rpm", "brake", "throttleBrake", "fuel" };

        /// <summary>The fifth centre, retired into "rpm". It lit the same centre and differed only in
        /// leaving the sides dark, which is a decision about the sides rather than about the centre.
        /// Kept as a name so that NormaliseLedCentre can migrate a settings file that carries it.</summary>
        public const string RetiredLedCentre = "rpmOnly";

        /// <summary>The revs, with brake on the sides. What the hardware makers put there.</summary>
        public const string DefaultLedCentre = "rpm";

        /// <summary>How the rev ladder fills the strip. It decides the look and never the when: the
        /// thresholds are the car's own either way (ADR 0014). Mirrors LED_RPM_STYLES in contract.ts.</summary>
        public static readonly string[] LedRpmStyles = { "leftToRight", "meetInMiddle", "f1" };

        public const string DefaultLedRpmStyle = "leftToRight";

        /// <summary>On: movement is what a flag is read by at the edge of vision.</summary>
        public const bool DefaultLedFlagAnimation = true;


        // --- The zone face ---------------------------------------------------------------------
        //
        // Additive: Slot01 to Slot12 stay until the card path is retired. The shape of these is the
        // model -- a slot is arranged once with a mouse, a zone is changed with a thumb mid-lap, so
        // what the contract carries is a page number a button can advance.

        /// <summary>How a face lays its three body zones out.</summary>
        public enum FaceBody
        {
            /// <summary>Zone B, zone A and zone C side by side.</summary>
            Row,
            /// <summary>Zone A over zone B over zone C.</summary>
            Column,
        }

        /// <summary>
        /// One face size that ships, and the little of its shape the panel needs to draw a plan of it.
        /// </summary>
        /// <remarks>
        /// Mirrors FACE_SIZES in packages/dash/src/contract.ts. The shape is carried because the panel
        /// draws a plan of the face and cannot read a layout file: a plan drawn to one face's
        /// proportions for every face is how the nano at 800 x 286 came to be offered bar fields for a
        /// bar it has not got.
        /// </remarks>
        public struct FaceSize
        {
            public FaceSize(int width, int height, FaceBody body, int[] parts, bool hasBar, int barFieldsPerEnd)
            {
                Width = width;
                Height = height;
                Body = body;
                Parts = parts;
                HasBar = hasBar;
                BarFieldsPerEnd = barFieldsPerEnd;
            }

            public int Width { get; }
            public int Height { get; }
            public FaceBody Body { get; }

            /// <summary>Relative sizes of the three body zones, in the order that body draws them.</summary>
            public int[] Parts { get; }

            /// <summary>False on the nano at 800 x 286, where the height for a bar is not there.</summary>
            public bool HasBar { get; }

            /// <summary>Two per end on a wide face, one in portrait.</summary>
            public int BarFieldsPerEnd { get; }

            /// <summary>The zone letters of the body, in the order it draws them.</summary>
            public string[] BodyOrder
            {
                get { return Body == FaceBody.Column ? new[] { "A", "B", "C" } : new[] { "B", "A", "C" }; }
            }

            public override string ToString() { return Width + " x " + Height; }
        }

        /// <summary>
        /// Every face size that ships. Mirrors FACE_SIZES in packages/dash/src/contract.ts, which
        /// ContractTests reads back, so the two cannot drift.
        /// </summary>
        public static readonly IReadOnlyList<FaceSize> FaceSizes = new[]
        {
            new FaceSize(1920, 480, FaceBody.Row, new[] { 769, 380, 769 }, true, 2),
            new FaceSize(1280, 480, FaceBody.Row, new[] { 469, 340, 469 }, true, 2),
            new FaceSize(1280, 400, FaceBody.Row, new[] { 469, 340, 469 }, true, 2),
            new FaceSize(850, 480, FaceBody.Row, new[] { 274, 300, 274 }, true, 2),
            new FaceSize(800, 480, FaceBody.Row, new[] { 249, 300, 249 }, true, 2),
            new FaceSize(1280, 720, FaceBody.Row, new[] { 469, 340, 469 }, true, 2),
            new FaceSize(800, 286, FaceBody.Row, new[] { 269, 260, 269 }, false, 2),
            new FaceSize(600, 686, FaceBody.Column, new[] { 234, 160, 150 }, true, 1),
        };

        /// <summary>The face a rig is most likely to have, and where a pre-face setting is migrated to.</summary>
        public static FaceSize ReferenceFace { get { return FaceSizes[0]; } }

        /// <summary>
        /// The prefix a face's settings carry, for instance "Face1920x480".
        /// </summary>
        /// <remarks>
        /// Concatenated rather than separated by a dot, because SimHub already puts one dot in front of
        /// every property name and whether its parser accepts a second inside the name is unverified.
        /// Every face carries one so that a rig of two faces is configured apart rather than sharing one
        /// set of zones, which is the same idiom the pit wall's PitWall prefix already uses.
        /// </remarks>
        public static string FacePrefix(FaceSize face)
        {
            return "Face" + face.Width + "x" + face.Height;
        }

        /// <summary>The face a prefix names. Throws when nothing ships at that size.</summary>
        public static FaceSize FaceForPrefix(string prefix)
        {
            foreach (var face in FaceSizes)
            {
                if (string.Equals(FacePrefix(face), prefix, StringComparison.Ordinal)) return face;
            }
            throw new ArgumentOutOfRangeException("prefix", prefix, "no face ships at that size");
        }

        /// <summary>Whether a prefix names a face that ships, for reading a settings file written by another version.</summary>
        public static bool IsKnownFacePrefix(string prefix)
        {
            foreach (var face in FaceSizes)
            {
                if (string.Equals(FacePrefix(face), prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// The prefix the pit wall's and the companion's settings carry, as a face's is FacePrefix.
        /// </summary>
        /// <remarks>
        /// Fixed rather than derived from a size, because the landscape and the portrait package of each
        /// are one screen in two orientations rather than two screens: a spotter who turns the monitor
        /// does not expect to configure it again.
        /// </remarks>
        public const string PitWallPrefix = "PitWall";

        public const string CompanionPrefix = "Companion";

        /// <summary>Every screen a rig can have, by the prefix its properties carry.</summary>
        public static IEnumerable<string> ScreenPrefixes()
        {
            foreach (var face in FaceSizes) yield return FacePrefix(face);
            yield return CompanionPrefix;
            yield return PitWallPrefix;
        }

        /// <summary>Whether a prefix names a screen OpenDash knows, for reading a settings file written by another version.</summary>
        public static bool IsKnownScreen(string prefix)
        {
            return IsKnownFacePrefix(prefix)
                || string.Equals(prefix, CompanionPrefix, StringComparison.Ordinal)
                || string.Equals(prefix, PitWallPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// The properties one screen owns, in attachment order.
        /// </summary>
        /// <remarks>
        /// WebViewUrl is the pit wall's although its name carries no prefix: it was named before the
        /// idiom and a published property cannot be renamed under ADR 0003, but no other screen has a
        /// browser page to point anywhere.
        /// </remarks>
        // --- The kinds of screen a rig can hold -------------------------------------------------
        //
        // A screen used to be identified by the prefix its properties carry, which worked only while
        // there could be one of each: "Face1280x480" was both the name and the kind and the size at
        // once. ADR 0017 separates them, so a screen now carries a kind of its own and a namespace
        // that is no longer required to spell out what it is.

        /// <summary>A rectangular zone face: four zones, a bar and a glance.</summary>
        public const string KindFace = "face";

        /// <summary>A companion: one module at a time behind a header.</summary>
        public const string KindCompanion = "companion";

        /// <summary>A pit wall: three pages sharing four data zones and one wide zone.</summary>
        public const string KindPitWall = "pitwall";

        /// <summary>A twelve-slot face, from the card model that the zones replaced. Leaves with XOR-95.</summary>
        public const string KindSlots = "slots";

        public static readonly string[] ScreenKinds = { KindFace, KindCompanion, KindPitWall, KindSlots };

        /// <summary>The kind the stock prefix of a screen names, for migrating a settings file that had only prefixes.</summary>
        public static string KindForPrefix(string prefix)
        {
            if (IsKnownFacePrefix(prefix)) return KindFace;
            if (string.Equals(prefix, CompanionPrefix, StringComparison.Ordinal)) return KindCompanion;
            if (string.Equals(prefix, PitWallPrefix, StringComparison.Ordinal)) return KindPitWall;
            return null;
        }

        /// <summary>
        /// The properties one screen of a kind owns under its own namespace, in attachment order.
        /// </summary>
        /// <remarks>
        /// The same names as ScreenPropertyNames(prefix) produces for a stock screen, which is what
        /// keeps a rig of one screen per size attaching exactly what it attached before ADR 0017. The
        /// companion's and the pit wall's names carried no size even then, so under an instance they
        /// take the namespace where they used to take the fixed prefix.
        /// </remarks>
        public static IEnumerable<string> ScreenPropertyNames(string kind, string ns)
        {
            if (string.Equals(kind, KindFace, StringComparison.Ordinal))
            {
                foreach (var name in FacePropertyNames(ns)) yield return name;
                yield break;
            }
            if (string.Equals(kind, KindCompanion, StringComparison.Ordinal))
            {
                for (var module = 1; module <= Modules.Count; module++) yield return ModuleProperty(ns, module);
                yield break;
            }
            if (string.Equals(kind, KindPitWall, StringComparison.Ordinal))
            {
                foreach (var letter in PitWallZoneLetters) yield return ZoneProperty(ns, letter);
                yield return PitWallWideProperty(ns);
                yield return WebViewUrlProperty(ns);
                yield break;
            }
            // A slots face reads the twelve shared slot properties and nothing of its own, which is why
            // the card model never needed a prefix and why two of them cannot be told apart. That is a
            // property of the model being retired, not something this fixes.
            if (string.Equals(kind, KindSlots, StringComparison.Ordinal)) yield break;
            throw new ArgumentOutOfRangeException("kind", kind, "no screen is of that kind");
        }

        /// <summary>Every action one screen of a kind owns, in registration order.</summary>
        public static IEnumerable<string> ScreenActionNames(string kind, string ns)
        {
            if (string.Equals(kind, KindFace, StringComparison.Ordinal)) return FaceActionNames(ns);
            // Only a face has a button today. The canvas draws "Next module" on the companion pane and
            // no such action is registered, so the panel says it is not bound rather than offering a
            // binder for a name SimHub would never call.
            return new string[0];
        }

        public static IEnumerable<string> ScreenPropertyNames(string prefix)
        {
            if (IsKnownFacePrefix(prefix))
            {
                foreach (var name in FacePropertyNames(FaceForPrefix(prefix))) yield return name;
                yield break;
            }
            if (string.Equals(prefix, CompanionPrefix, StringComparison.Ordinal))
            {
                for (var module = 1; module <= Modules.Count; module++) yield return ModuleProperty(module);
                yield break;
            }
            if (string.Equals(prefix, PitWallPrefix, StringComparison.Ordinal))
            {
                foreach (var letter in PitWallZoneLetters) yield return ZoneProperty(letter);
                yield return PitWallWide;
                yield return WebViewUrl;
                yield break;
            }
            throw new ArgumentOutOfRangeException("prefix", prefix, "no screen carries that prefix");
        }

        /// <summary>
        /// The properties every screen of a rig shares: the four modes, the twelve slots and the rev
        /// bar. A lap time compares against the same lap on the rim as it does on the pit wall, so
        /// these carry no screen's name and are attached whatever the rig is.
        /// </summary>
        /// <remarks>
        /// RevBar is shared and not a face's, although only a rectangular face has the second
        /// arrangement: the segments themselves are drawn by the round faces' rev arc and by the
        /// companion's speedo page as well, and a screen that may not read a property cannot draw
        /// them. It is also the alias of ShiftLights, which has always been shared, and the two
        /// cannot sit on opposite sides of the partition.
        ///
        /// It is yielded after the twelve slots rather than beside the mode it supersedes: the four
        /// fixed names have shipped and the tests assert them by index, so a new setting is appended
        /// to this group and never inserted into it. XOR-119, XOR-138.
        /// </remarks>
        public static IEnumerable<string> SharedPropertyNames()
        {
            yield return ShiftLights;
            yield return PositionMode;
            yield return DeltaReference;
            yield return SessionProgress;
            for (var slot = 1; slot <= SlotCount; slot++) yield return SlotProperty(slot);
            yield return RevBar;
        }

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

        /// <summary>
        /// The characters a namespace may be spelled with, and the rule that produces one from a name.
        /// </summary>
        /// <remarks>
        /// Letters and digits only. The contract's note on FacePrefix records that SimHub already puts
        /// one dot in front of every property name and that whether its parser accepts a second inside
        /// the name is unverified; a namespace is user-typed, so this is not the place to find out.
        /// Everything else in the name is dropped rather than substituted, because a separator that
        /// survives is a separator somebody's name ends with.
        ///
        /// The case the user typed is kept, so "Main dash" is MainDash and not maindash: a namespace is
        /// read in SimHub's property browser and the panel prints it, and both are nicer in the shape
        /// the person chose.
        /// </remarks>
        public static string Slug(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var built = new System.Text.StringBuilder(name.Length);
            var startOfWord = false;
            foreach (var character in name)
            {
                if (character >= '0' && character <= '9')
                {
                    // A namespace that begins with a digit would read as a number wherever a property is
                    // parsed, so the leading run of them is dropped rather than kept.
                    if (built.Length > 0) built.Append(character);
                    continue;
                }
                var upper = character >= 'A' && character <= 'Z';
                var lower = character >= 'a' && character <= 'z';
                if (!upper && !lower)
                {
                    startOfWord = built.Length > 0;
                    continue;
                }
                built.Append(startOfWord && lower ? char.ToUpperInvariant(character) : character);
                startOfWord = false;
            }
            return built.ToString();
        }

        /// <summary>
        /// Whether a namespace is one the plugin allocates for itself and a user's screen may not take.
        /// </summary>
        /// <remarks>
        /// Every stock face prefix, the companion's and the pit wall's. A screen that took one of these
        /// would read the properties of whichever screen holds the stock package, which is the exact
        /// collision ADR 0017 exists to stop.
        /// </remarks>
        public static bool IsReservedNamespace(string ns)
        {
            return ns != null && IsKnownScreen(ns);
        }

        /// <summary>Property name of a face's quick glance: Face1920x480QuickGlance.</summary>
        public static string QuickGlanceProperty(string ns)
        {
            return ns + "QuickGlance";
        }

        public static string QuickGlanceProperty(FaceSize face)
        {
            return QuickGlanceProperty(FacePrefix(face));
        }

        /// <summary>
        /// The actions a driver binds to a wheel button. Named as verbs, because a property is a noun:
        /// `OpenDash.QuickGlance` is what the glance is set to and `OpenDash.HoldQuickGlance` is the
        /// button that shows it, and a log naming one should not read like the other.
        /// </summary>
        public const string HoldQuickGlanceAction = "HoldQuickGlance";

        /// <summary>Action that advances one zone of one face: Face1920x480CycleZoneA.</summary>
        public static string CycleZoneAction(string ns, string letter)
        {
            RequireFaceZone(letter);
            return ns + "CycleZone" + letter;
        }

        public static string CycleZoneAction(FaceSize face, string letter)
        {
            return CycleZoneAction(FacePrefix(face), letter);
        }

        /// <summary>Action that holds the glance on one face: Face1920x480HoldQuickGlance.</summary>
        public static string HoldQuickGlanceActionFor(string ns)
        {
            return ns + HoldQuickGlanceAction;
        }

        public static string HoldQuickGlanceActionFor(FaceSize face)
        {
            return HoldQuickGlanceActionFor(FacePrefix(face));
        }

        /// <summary>
        /// An action's name as SimHub knows it. `PluginManager.GetName` is `pluginType.Name + "." +
        /// name`, and the plugin class is `OpenDash`, so it is the same prefix the properties carry.
        /// </summary>
        public static string FullActionName(string actionName)
        {
            return Prefix + "." + actionName;
        }

        /// <summary>
        /// Every action the plugin registers, in registration order: five per face that ships.
        /// </summary>
        /// <remarks>
        /// Per face and not five in total, because two faces on one rig have to cycle apart, which is
        /// the same reason their properties are prefixed. The cost is that SimHub's binding list holds
        /// five entries for every face rather than five altogether, and a driver with one screen will
        /// see the four they do not have.
        ///
        /// Every face and not the rig's, which is where the actions part company with the properties.
        /// A property a rig does not have is one a binding reads through isnull and falls back on; an
        /// action a rig does not have is a button a driver already assigned, left bound to nothing. The
        /// first costs a default, the second costs somebody their wheel.
        /// </remarks>
        public static IEnumerable<string> ActionNames()
        {
            foreach (var face in FaceSizes)
            {
                foreach (var letter in FaceZoneLetters) yield return CycleZoneAction(face, letter);
                yield return HoldQuickGlanceActionFor(face);
            }
        }

        /// <summary>Property name of a zone's current page: Face1920x480ZoneA .. Face600x686ZoneD.</summary>
        public static string ZonePageProperty(string ns, string letter)
        {
            RequireFaceZone(letter);
            return ns + "Zone" + letter;
        }

        public static string ZonePageProperty(FaceSize face, string letter)
        {
            return ZonePageProperty(FacePrefix(face), letter);
        }

        /// <summary>Property name of a zone's enabled-page mask: Face1920x480ZoneAPages.</summary>
        public static string ZoneMaskProperty(string ns, string letter)
        {
            RequireFaceZone(letter);
            return ns + "Zone" + letter + "Pages";
        }

        public static string ZoneMaskProperty(FaceSize face, string letter)
        {
            return ZoneMaskProperty(FacePrefix(face), letter);
        }

        /// <summary>Property name of a zone's start page: Face1920x480ZoneAStart.</summary>
        public static string ZoneStartProperty(string ns, string letter)
        {
            RequireFaceZone(letter);
            return ns + "Zone" + letter + "Start";
        }

        public static string ZoneStartProperty(FaceSize face, string letter)
        {
            return ZoneStartProperty(FacePrefix(face), letter);
        }

        /// <summary>Property name of a zone's class filter: Face1920x480ZoneAClassOnly.</summary>
        public static string ZoneClassOnlyProperty(string ns, string letter)
        {
            RequireFaceZone(letter);
            return ns + "Zone" + letter + "ClassOnly";
        }

        public static string ZoneClassOnlyProperty(FaceSize face, string letter)
        {
            return ZoneClassOnlyProperty(FacePrefix(face), letter);
        }

        /// <summary>Property name of a bar end field: Face1920x480BarLeft1.</summary>
        public static string BarFieldProperty(string ns, string slot)
        {
            if (Array.IndexOf(BarSlots, slot) < 0) throw new ArgumentOutOfRangeException(nameof(slot));
            return ns + "Bar" + slot;
        }

        public static string BarFieldProperty(FaceSize face, string slot)
        {
            return BarFieldProperty(FacePrefix(face), slot);
        }

        /// <summary>Every property one face owns, in attachment order.</summary>
        public static IEnumerable<string> FacePropertyNames(string ns)
        {
            foreach (var letter in FaceZoneLetters) yield return ZonePageProperty(ns, letter);
            foreach (var letter in FaceZoneLetters) yield return ZoneMaskProperty(ns, letter);
            foreach (var letter in FaceZoneLetters) yield return ZoneStartProperty(ns, letter);
            foreach (var letter in FaceZoneLetters) yield return ZoneClassOnlyProperty(ns, letter);
            foreach (var slot in BarSlots) yield return BarFieldProperty(ns, slot);
            yield return QuickGlanceProperty(ns);
        }

        public static IEnumerable<string> FacePropertyNames(FaceSize face)
        {
            return FacePropertyNames(FacePrefix(face));
        }

        /// <summary>Every action one face's screen owns, in registration order: one per zone and the glance.</summary>
        public static IEnumerable<string> FaceActionNames(string ns)
        {
            foreach (var letter in FaceZoneLetters) yield return CycleZoneAction(ns, letter);
            yield return HoldQuickGlanceActionFor(ns);
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
        public static string ModuleProperty(string ns, int module)
        {
            if (!Modules.IsValidNumber(module)) throw new ArgumentOutOfRangeException(nameof(module));
            return ns + "Module" + module.ToString("00");
        }

        public static string ModuleProperty(int module)
        {
            return ModuleProperty(CompanionPrefix, module);
        }

        /// <summary>Property name of a pit wall zone: PitWallZoneA .. PitWallZoneD.</summary>
        public static string ZoneProperty(string ns, string letter)
        {
            var index = Array.IndexOf(PitWallZoneLetters, letter);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(letter));
            return ns + "Zone" + letter;
        }

        public static string ZoneProperty(string letter)
        {
            return ZoneProperty(PitWallPrefix, letter);
        }

        /// <summary>Property name of a pit wall's wide zone: PitWallWide, or GarageWide on a second one.</summary>
        public static string PitWallWideProperty(string ns)
        {
            return string.Equals(ns, PitWallPrefix, StringComparison.Ordinal) ? PitWallWide : ns + "Wide";
        }

        /// <summary>
        /// Property name of a pit wall's web view address.
        /// </summary>
        /// <remarks>
        /// The stock one is "WebViewUrl" with no prefix at all: it was named before the idiom and a
        /// published property cannot be renamed under ADR 0003. A second pit wall therefore takes a
        /// prefixed spelling, which is the one place where an instance's names are not simply the stock
        /// ones with the namespace swapped in.
        /// </remarks>
        public static string WebViewUrlProperty(string ns)
        {
            return string.Equals(ns, PitWallPrefix, StringComparison.Ordinal) ? WebViewUrl : ns + "WebViewUrl";
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

        /// <summary>
        /// Every property the plugin attaches for a rig, without the prefix, in attachment order: the
        /// shared ones, then each screen the rig has, in the order the settings name them.
        /// </summary>
        /// <remarks>
        /// A rig and not the catalogue, because eight faces of twenty-one properties is a hundred and
        /// sixty-eight names for a rig that has two screens, and a property list proportional to the rig
        /// is both smaller and truthful. OpenDashSettings.DeclaredProperties() is how the plugin asks.
        /// </remarks>
        public static IEnumerable<string> PropertyNames(IEnumerable<string> screens)
        {
            foreach (var name in SharedPropertyNames()) yield return name;
            if (screens != null)
            {
                foreach (var screen in screens)
                {
                    foreach (var name in ScreenPropertyNames(screen)) yield return name;
                }
            }
            foreach (var name in LightsPropertyNames()) yield return name;
        }

        /// <summary>
        /// Every property a rig of instances attaches, in attachment order.
        /// </summary>
        /// <remarks>
        /// The same shape as the overload above and the same order, so that a rig of one screen per size
        /// declares exactly the names it declared before ADR 0017 and the pin in
        /// packages/dash/test/declared-properties.txt keeps checking what it always checked. A screen
        /// with a namespace of its own contributes names that are not in the pin and cannot be: they
        /// exist because a user typed a name, and nothing a build can see produces them.
        /// </remarks>
        public static IEnumerable<string> PropertyNames(IEnumerable<ScreenInstance> screens)
        {
            foreach (var name in SharedPropertyNames()) yield return name;
            if (screens != null)
            {
                foreach (var screen in screens)
                {
                    if (screen == null) continue;
                    foreach (var name in screen.PropertyNames()) yield return name;
                }
            }
            foreach (var name in LightsPropertyNames()) yield return name;
        }

        /// <summary>Every property of every screen OpenDash ships, which is what contract.ts declares
        /// and the validator checks a package against. No rig has all of them.</summary>
        public static IEnumerable<string> PropertyNames()
        {
            return PropertyNames(ScreenPrefixes());
        }

        /// <summary>The lights, which belong to the rig rather than to any screen: brightness and night
        /// mode for every light openDash drives, then the flag box's own settings and one group per
        /// matrix, then the strips'. They come after the screens so that this list and contract.ts
        /// agree end to end.
        ///
        /// The strips are here and not in a group of their own because they are the same kind of thing:
        /// one set of lights with two kinds of hardware behind it, declared for a rig whatever it owns.
        /// A second group would also have to be a fourth part of the partition every property belongs
        /// to -- a screen's, shared, or the lights' -- for no gain; contract.ts keeps them apart only as
        /// two functions whose results it concatenates, which is a spelling and not a category.
        ///
        /// A rig with no matrix and no strip still declares all of them, unlike a screen it does not
        /// have: openDash installs neither profile by itself (ADR 0013), so there is nothing to detect,
        /// and thirty-four names is not the hundred and thirty-six that made the screens worth
        /// narrowing. (Thirteen, this said before the matrices had a group each; it is counted here
        /// rather than guessed at.)</summary>
        public static IEnumerable<string> LightsPropertyNames()
        {
            yield return LightsBrightness;
            yield return LightsNightBrightness;
            yield return LightsNightMode;
            yield return FlagBoxCriticalOnly;
            yield return FlagBoxGear;
            yield return FlagBoxLowFuelLaps;
            yield return FlagBoxOilTemp;
            yield return FlagBoxWaterTemp;
            foreach (var matrix in FlagBoxMatrices)
            {
                foreach (var name in FlagBoxMatrixProperties(matrix)) yield return name;
            }
            foreach (var name in LedPropertyNames()) yield return name;
        }

        /// <summary>The three a generated .ledsprofile reads, last, as ledProperties() is last in
        /// contract.ts. Named apart so that the strips can be pointed at, not so that they are a
        /// category of their own.</summary>
        public static IEnumerable<string> LedPropertyNames()
        {
            yield return LedCentre;
            yield return LedRpmStyle;
            yield return LedFlagAnimation;
        }

        /// <summary>Clamps a brightness to 0..100. A profile reads this with isnull() and its default, so a
        /// value the panel never wrote still has to be one SimHub can use.</summary>
        public static int NormaliseBrightness(int percent)
        {
            if (percent < 0) return 0;
            return percent > 100 ? 100 : percent;
        }

        /// <summary>
        /// The rev bar mode a settings file means.
        ///
        /// `null` is the shape an rc.2 file has -- it was written before the mode existed -- and it
        /// resolves through the deprecated alias, so that a user who had turned the shift lights off
        /// finds the plain RPM bar rather than the shift lights back on. Anything unrecognised
        /// resolves the same way. XOR-119 is the rule this keeps.
        /// </summary>
        public static string NormaliseRevBar(string value, bool shiftLights)
        {
            var alias = shiftLights ? RevBarShift : RevBarRpm;
            return string.IsNullOrWhiteSpace(value) ? alias : NormaliseChoice(value, RevBarModes, alias);
        }

        /// <summary>
        /// The strip centre a settings file means.
        /// </summary>
        /// <remarks>
        /// A stored "rpmOnly" becomes "rpm", which is where the value was retired. Migrated by name
        /// rather than left to the fallback: the fallback lands on "rpm" only for as long as "rpm" is
        /// the default, and a value no conditional group in the profile matches is a strip whose centre
        /// goes dark. XOR-119 is the rule that an rc.2 user's settings survive the release.
        /// </remarks>
        public static string NormaliseLedCentre(string value)
        {
            if (value != null && string.Equals(value.Trim(), RetiredLedCentre, StringComparison.OrdinalIgnoreCase)) return DefaultLedCentre;
            return NormaliseChoice(value, LedCentres, DefaultLedCentre);
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
