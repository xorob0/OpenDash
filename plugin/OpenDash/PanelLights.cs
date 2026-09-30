// PanelLights.cs: the words the LEDs page puts beside a value -- the strips, the Add LEDs sheet and Lovely
// Car Data -- and the matrix words the Matrix page still reads until it lands copies of its own.
//
// Apart from SettingsControl.Lights.cs for the reason PanelCopy.cs is apart from Widgets.cs: the page is
// WPF and the net8.0 test project cannot compile a line of it, so what a test can hold has to live where
// it can reach. PanelLeds holds the page's own decisions; this file holds the strip words it shares with
// the shell's comments and the Matrix page. What it holds here is the pairing. BuildChoice and BuildSegmented are handed the values
// and the labels as two arrays and read them by index, so a value set that gains or loses one leaves the
// labels beside it wrong without saying so: the centre drop-down carried a label for a retired fifth
// value that way, and a segmented bar one label short throws while a page is being drawn. Pure: no WPF
// types.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    public static class PanelLights
    {
        /// <summary>One label per <see cref="Contract.LedCentres"/> value, in its order.</summary>
        public static readonly string[] CentreLabels = { "RPM", "Brake", "Throttle and brake", "Fuel" };


        /// <summary>One label per <see cref="Contract.LedMirrorFits"/> value, in its order.</summary>
        public static readonly string[] MirrorFitLabels = { "Stretch to fit", "Actual size" };

        /// <summary>One label per <see cref="Contract.FlagBoxRests"/> value, in its order.</summary>
        public static readonly string[] RestLabels = { "Dark", "Gear" };

        /// <summary>One label per <see cref="Contract.FlagBoxSides"/> value, in its order.</summary>
        public static readonly string[] SideLabels = { "Both", "Left", "Right" };

        /// <summary>
        /// What the strips a driver has added are, as a group: the cards' accessible name.
        /// </summary>
        /// <remarks>
        /// A strip used to be a shape the Install tab offered and nothing more, so two strips on one rig
        /// could be installed separately and not configured separately -- a wheel and a brow shared one
        /// answer to what their middles show. A bar is an instance now, the way a screen is: it has a
        /// name, a shape and settings of its own, and installing it is what puts a profile of that name
        /// into SimHub.
        /// </remarks>
        public const string BarsTitle = "Your LED strips";

        /// <summary>The tile beside the cards and the sheet it opens: voice.md's own example beside "No strips
        /// yet", since the strips are strips throughout. The artboard's "Add LEDs" is the departure
        /// docs/design/plugin.md records, and search still finds the tile by it.</summary>
        public const string AddBar = "Add an LED strip";

        public const string BarNameTitle = "Name";

        public const string BarNameCaption = "Also shown in SimHub's LED profile list.";

        public const string BarEndsTitle = "LEDs at each end";

        /// <summary>The line under the ends, in a driver's words rather than the generator's ("lamps"): what the
        /// ends carry, and how to have none.</summary>
        public const string BarEndsCaption = "Flags, warnings and cars alongside. Pick None for one continuous run.";

        /// <summary>The device row of the add flow and of every strip: SimHub's word for it, since SimHub's
        /// Devices list is where the driver finds it.</summary>
        public const string BarDeviceTitle = "SimHub device";

        /// <summary>
        /// The line under it, which has to carry a fact about SimHub rather than a preference.
        /// </summary>
        /// <remarks>
        /// There is no shared list of LED profiles in SimHub. Every LED device keeps its own, in its own
        /// file, and a profile in one is invisible in every other. OpenDash used to install into the
        /// Arduino RGB LEDs device whatever the strip was, so a bar added for a wheel was written and
        /// saved correctly into a list the wheel does not read, and the driver went looking in the wheel
        /// and found nothing. Somebody reading this row has to understand that choosing is not optional.
        /// </remarks>
        public const string BarDeviceCaption = null;

        /// <summary>Said in place of the picker when SimHub has exactly one LED device: there is nothing
        /// to choose, and a drop-down of one is a question with one answer.</summary>
        public static string OneDevice(string name)
        {
            return "Goes to " + name + ".";
        }

        /// <summary>Said when SimHub has none, in the row's own noun ("SimHub device"). The bar is still added
        /// and still configurable; what it cannot have is a profile anywhere, which is a thing about the rig
        /// and not about OpenDash.</summary>
        public const string NoDevices = "No SimHub device has LEDs. Add your wheel or Arduino in SimHub first.";

        /// <summary>
        /// Said beside the picker when SimHub has devices with some sign of LEDs that OpenDash did not
        /// offer, naming them. Which devices those are is <see cref="LedDeviceSurvey.Declined"/>'s call.
        /// </summary>
        /// <remarks>
        /// A wheel made in FanaBridge's wizard was plainly in SimHub's Devices view and absent from the
        /// picker, and nothing on the row said that OpenDash had seen it and passed it over (#437). The
        /// reason is in SimHub's log, one line per device, and that is where the sentence points: the
        /// reasons are SimHub's types, which the voice rules keep off the panel. Null when nothing was
        /// passed over, so a rig whose every device is offered reads exactly as before.
        /// </remarks>
        public static string NotOffered(System.Collections.Generic.IList<string> names)
        {
            if (names == null || names.Count == 0) return null;
            return NameList(names) + (names.Count == 1 ? " has" : " have") + " no LEDs OpenDash can reach; see SimHub's log.";
        }

        /// <summary>At most this many names are spelled out before the rest are counted.</summary>
        public const int NotOfferedNames = 3;

        /// <summary>
        /// The caption under the device row, given what it would say on its own and the devices passed over.
        /// </summary>
        /// <remarks>
        /// With no device offered, <see cref="NoDevices"/> would be wrong: it says there is no LED device
        /// in SimHub while the wheel is there in SimHub's list, which is the report this answers. The
        /// sentence naming the device replaces it. With one or more offered, it follows whatever the row
        /// already said.
        /// </remarks>
        public static string DeviceRowCaption(int offered, string caption, System.Collections.Generic.IList<string> declined)
        {
            var passed = NotOffered(declined);
            if (passed == null) return offered == 0 ? NoDevices : caption;
            if (offered == 0 || string.IsNullOrEmpty(caption)) return passed;
            return caption + " " + passed;
        }

        private static string NameList(System.Collections.Generic.IList<string> names)
        {
            var shown = names.Count > NotOfferedNames ? NotOfferedNames : names.Count;
            var rest = names.Count - shown;
            var spelled = new System.Collections.Generic.List<string>();
            for (var i = 0; i < shown; i++) spelled.Add(names[i]);
            if (rest > 0) spelled.Add(rest == 1 ? "1 other" : rest + " others");
            if (spelled.Count == 1) return spelled[0];
            return string.Join(", ", spelled.GetRange(0, spelled.Count - 1)) + " and " + spelled[spelled.Count - 1];
        }

        /// <summary>What a bar pointed at a device SimHub no longer has is shown as, so the row says what
        /// happened rather than silently reading as the first device in the list.</summary>
        public const string DeviceGone = "The device it was on (no longer on this rig)";

        /// <summary>Said beside a device that SimHub is not talking to. A profile installs into it all the
        /// same -- the profile list is SimHub's, not the hardware's -- so this is a note and not a bar.</summary>
        public const string DeviceOffline = " (not connected)";

        /// <summary>The centre's count, in the word the rest of the page uses for it ("Centre display").</summary>
        public const string BarCentreTitle = "LEDs in the centre";

        /// <summary>The line under the centre: how to arrive at the number.</summary>
        public const string BarCentreCaption = "Count your LEDs and subtract the ends.";

        /// <summary>The line under the two numbers: what they add up to, and the shape written as the cards
        /// write it. A driver counts LEDs, and this is where the two counts are checked against the total
        /// they actually have. A bare run is its total alone, since "0 · 15 · 0" is the id talking.</summary>
        public static string BarShapeNote(int side, int centre)
        {
            var total = side * 2 + centre;
            var all = total + (total == 1 ? " LED in all" : " LEDs in all");
            if (side <= 0) return all + ".";
            return all + ", as " + PanelLeds.ShapeDots(BarShapeId(side, centre)) + ".";
        }

        /// <summary>The same line for the form as it stands, the tile included: the Fanatec wheel's shape is
        /// its own whatever the controls held before, and the profile is the Fanatec one.</summary>
        public static string BarShapeNote(int side, int centre, bool fanatec)
        {
            if (!fanatec) return BarShapeNote(side, centre);
            var plain = BarShapeNote(FanatecSide, FanatecCentre);
            return plain.Substring(0, plain.Length - 1) + " Fanatec.";
        }

        /// <summary>The id of a plain A/B/A shape, which the census and the Add sheet ask over.</summary>
        public static string BarShapeId(int side, int centre)
        {
            return side + "-" + centre + "-" + side;
        }

        /// <summary>The hardware tile that decides the shape when it is chosen.</summary>
        public const string BarFanatecTitle = "Fanatec wheel";

        /// <summary>
        /// Why a Fanatec wheel is not simply a 3/9/3. Not drawn: it explains how SimHub presents the LEDs,
        /// which voice.md keeps off the panel, and the tile already says Fanatec wheel and 3 · 9 · 3. Kept, with
        /// PanelLedBarFormTests' pin, as the reason the tile exists.
        /// </summary>
        /// <remarks>
        /// The plain 3/9/3 on a Fanatec wheel lights only some of its LEDs and starts the bar from the
        /// middle of the rim, which is a failure a driver cannot debug, so the line says the one fact that
        /// tells them this switch is theirs. It is the order SimHub's own Fanatec device presents, nine
        /// RevLEDs and then six FlagLEDs (0.3.0-rc.3), and nothing to do with the maker's software.
        /// </remarks>
        public const string BarFanatecCaption = "SimHub hands a Fanatec wheel's LEDs over in an order of their own.";

        /// <summary>The ends and the centre a Fanatec wheel has, which the switch shows and locks.</summary>
        public const int FanatecSide = 3;

        public const int FanatecCentre = 9;

        /// <summary>The id `fanatec(3, 9, 3)` in packages/dash/src/leds/strip.ts spells, which is the shape
        /// the switch adds a bar as.</summary>
        public static readonly string FanatecShapeId = BarShapeId(FanatecSide, FanatecCentre) + "-" + PanelLightRows.FanatecSuffix;

        /// <summary>
        /// The id the add form hands to AddLedBar: the two numbers, or the Fanatec profile when the switch
        /// is on, whatever the two numbers held.
        /// </summary>
        public static string BarShapeId(int side, int centre, bool fanatec)
        {
            return fanatec ? FanatecShapeId : BarShapeId(side, centre);
        }

        /// <summary>
        /// The ends the add form offers, read off every id the build embedded.
        /// </summary>
        /// <remarks>
        /// The census carries every embedded id, the wirings included, because it is also what AddLedBar
        /// and MoveLedBar look a bar's profile up in, and a bar whose shape is `3-9-3-fanatec` has to find
        /// it there: a census that dropped it made a move report "could not be installed" about a profile
        /// the build carries. The question of how many LEDs there are is asked over the plain shapes alone,
        /// which is where the filter belongs: a reversed or Fanatec profile is the same geometry wired
        /// another way and has no place in a question about how many LEDs there are.
        /// </remarks>
        public static int[] BarSides(IEnumerable<string> census)
        {
            return Counted(census).Select(shape => shape.Left).Distinct().OrderBy(n => n).ToArray();
        }

        /// <summary>The centres the add form offers beside a choice of ends, over the plain shapes alone.</summary>
        public static int[] BarCentres(IEnumerable<string> census, int side)
        {
            return Counted(census).Where(shape => shape.Left == side).Select(shape => shape.Centre).Distinct().OrderBy(n => n).ToArray();
        }

        /// <summary>
        /// Whether the add form draws the Fanatec switch: only when the build embedded the profile it
        /// selects, so that it never offers a shape whose profile does not exist.
        /// </summary>
        /// <remarks>
        /// One switch and not a wiring drop-down (#436). Every plain shape has a far-end twin now, and
        /// that is not a wiring to pick here: a bar is reversed by its own switch (LedBar.Reversed), which
        /// installs the twin in place of the plain profile (#503). The Fanatec wiring is not a reversal --
        /// it is the order SimHub's Fanatec LED device presents a wheel's runs in, which reverses nothing
        /// -- so it stays a tile of its own on the Add LEDs sheet. The reversed 4/14/4 keeps a row of its own on the
        /// Updates page only because it is named for a device, as the plain 4/14/4 is.
        /// </remarks>
        public static bool OffersFanatec(IEnumerable<string> census)
        {
            return census != null && census.Contains(FanatecShapeId, StringComparer.Ordinal);
        }

        /// <summary>The ids the two numbers are asked over: an A/B/A geometry in the plain wiring.</summary>
        private static IEnumerable<LightShape> Counted(IEnumerable<string> census)
        {
            if (census == null) return Enumerable.Empty<LightShape>();
            return census
                .Select(LightShape.Parse)
                .Where(shape => shape != null && shape.Wiring == null && shape.Left == shape.Right);
        }

        /// <summary>
        /// What is said once a bar exists: the steps OpenDash does not take, in the order they are done, as
        /// voice.md's own example has them. The restart comes first, since the plugin publishes a strip's own
        /// settings only for the strips it held when SimHub started; then selecting the profile, which
        /// installing adds and does not select. A note the install returned (the device lists only its maker's
        /// profiles) comes before both, since nothing is listed to select until it is done.
        /// </summary>
        /// <remarks>
        /// It names the device, because "your LED device" was the whole confusion: a profile goes into
        /// one device's list and OpenDash used to always pick the Arduino's, so somebody reading this
        /// line went to their wheel and found nothing. Now the line says where to look.
        /// </remarks>
        public static string BarAdded(string name, string device, string note = null)
        {
            var where = string.IsNullOrWhiteSpace(device) ? " in SimHub." : " on " + device + ".";
            var steps = BarAddedRestart + "select \"" + name + "\"" + where;
            return "Added " + name + ". " + (string.IsNullOrWhiteSpace(note) ? string.Empty : note.Trim() + " ") + steps;
        }

        /// <summary>The restart BarAdded asks for, pinned apart so it goes when the plugin attaches a strip's
        /// settings as the strip is added.</summary>
        public const string BarAddedRestart = "Restart SimHub, then ";

        /// <summary>
        /// A strip added whose profile could not be installed. It points at the log (voice.md's failure form);
        /// the LEDs header then offers Install for it.
        /// </summary>
        public static string BarAddFailed(string name)
        {
            return "Added " + name + ", but its profile could not be installed. See SimHub's log.";
        }

        /// <summary>A strip whose profile could not be moved to another device: nothing was added.</summary>
        public static string BarMoveFailed(string name)
        {
            return "Could not move " + name + "'s profile. See SimHub's log.";
        }

        /// <summary>The strip's Rename press. Saving installs the profile again where SimHub holds it, so
        /// SimHub's list carries the new name as well; the tooltip promises only what always happens.</summary>
        public const string RenameBarTooltip = "Renames this strip.";

        /// <summary>The row that offers the car light tables, under Every strip: the source's own name, which
        /// is what a driver who met the tables at Lovely Sim Racing knows them by.</summary>
        public const string CarTablesTitle = "Lovely Car Data";

        /// <summary>
        /// What the button will do, said before it is pressed rather than after.
        /// </summary>
        /// <remarks>
        /// Every clause of this is load-bearing and none of it is decoration.
        ///
        /// <para>The tables are somebody else's work under CC BY-NC-SA 4.0 and OpenDash ships none of
        /// them (ADR 0018). What makes that work is that the copy is the user's own, and a copy a
        /// background thread made during startup is a poor version of that; a copy made when somebody
        /// pressed a button that had named the project, the licence, the size and the host is the whole
        /// of it. #366.</para>
        ///
        /// <para>It says "every car" because that is the privacy decision ADR 0018 part 1 argued for and
        /// the one thing a reader would otherwise get wrong: asking for the car you are in would tell a
        /// CDN which car you are in. Until this row existed that reasoning was written down only in the
        /// source, where no driver reads it.</para>
        /// </remarks>
        public const string CarTablesCaption =
            "Needed for a strip using the car's own rev lights. Every car is downloaded at once, about 400 KB, "
            + "so your car is never disclosed.";

        /// <summary>The button's own tooltip, which is not the row's caption: the caption is three lines
        /// of what the tables are for, and a tooltip on the button says what the button does.</summary>
        public const string CarTablesButtonTooltip = "Downloads Lovely Car Data.";

        /// <summary>Who measured it, and where to go and see. Shown under the row for as long as it exists.</summary>
        public static readonly string CarTablesAttribution = CarLightLibrary.Attribution + " " + CarLightLibrary.ProjectUrl;

        /// <summary>The state a rig is in until somebody presses the button, which is every rig on a fresh install.</summary>
        public const string CarTablesNone = "Not downloaded yet.";

        /// <summary>Said after a download that did not answer, beside whatever is already on disk.</summary>
        public static string CarTablesFailed(string reason)
        {
            return "Download failed: " + (string.IsNullOrWhiteSpace(reason) ? "no reason given" : reason) + ".";
        }

        /// <summary>While the request is out. A press with no answer for ten seconds reads as a dead button.</summary>
        public const string CarTablesDownloading = "Downloading…";

        /// <summary>
        /// The label on the button: the first press is a download and every one after it is a refresh.
        /// </summary>
        /// <remarks>
        /// Pure and pinned so that the pair cannot drift apart: a button that says Download beside a line
        /// saying 85 cars is a button somebody presses expecting to be told they already have them.
        /// </remarks>
        public static string CarTablesButton(int cars)
        {
            return cars > 0 ? "Update" : "Download";
        }

        /// <summary>Said beside the button when the copy is old enough that upstream has probably moved.</summary>
        public const string CarTablesStale = "Over a week old. Press Update for a newer copy.";

        /// <summary>
        /// The row's status line: CarLightService's status, and the stale note after it as a sentence of its
        /// own. The status ends with no stop ("84 cars, updated 9 days ago"), so a bare space ran the two
        /// together.
        /// </summary>
        public static string CarTablesLine(string status, bool stale)
        {
            var line = (status ?? string.Empty).TrimEnd();
            if (!stale) return line;
            if (line.Length == 0) return CarTablesStale;
            return line.TrimEnd('.') + ". " + CarTablesStale;
        }

        /// <summary>The heading over the panels a driver has added.</summary>
        public const string PanelsTitle = "Your matrix panels";

        /// <summary>
        /// The line under it, and what it says when there are none.
        /// </summary>
        /// <remarks>
        /// A rig starts with no panels, the way it starts with no screens. Four numbered groups of eleven
        /// settings, one of them switched on because it was first, is a page for hardware most people own
        /// none of; a panel exists because somebody said they have one, and it carries the name they gave
        /// it rather than its slot number.
        /// </remarks>
        public const string PanelsCaption = "Add one for each panel you have. Four at most.";

        public const string AddPanel = "Add a matrix panel";

        public const string PanelNameTitle = "Name";

        /// <summary>
        /// The line under a panel's name box, which exists to say what the name is NOT.
        /// </summary>
        /// <remarks>
        /// A strip's name goes into SimHub -- <see cref="BarNameCaption"/> says so, because installing a
        /// bar is what puts a profile of that name into the device's list. A panel's does not: all four
        /// are painted by the one flag box profile, so somebody who named a panel and went to SimHub's
        /// Arduino page looking for that name found an OpenDash profile with another name and read it as
        /// a failure. The two name boxes look identical and answered different questions in silence.
        /// </remarks>
        public const string PanelNameCaption = "OpenDash's own label. It is not shown in SimHub's profile list.";

        /// <summary>What the add-a-panel screen says above the name: which content number the panel will
        /// be, and which profile the driver selects on the device to see it.</summary>
        public static string AddPanelCaption(int matrix, string profile)
        {
            return "It will be " + PanelSlot(matrix) + ". Pick that content number on the device, and select \""
                + profile + "\" there: one profile paints all four panels.";
        }

        /// <summary>
        /// What is said once a panel exists, which is the step SimHub does not take for you.
        /// </summary>
        /// <remarks>
        /// The twin of <see cref="BarAdded"/>, and it has to make the one point that differs: a bar is a
        /// profile and a panel is a content number inside one. The sentence therefore names the profile
        /// the driver is to select, and says whose name is on it, rather than leaving them to search
        /// SimHub's list for the name they just typed.
        ///
        /// When SimHub has no copy, the sentence sends the driver where the top of the Matrix page can help
        /// in that state: its Install for a profile SimHub lacks, the copy to import by hand when SimHub's
        /// matrix settings could not be reached (its Install is disabled then), and nowhere when this build
        /// has no profile at all (the page says so, and has nothing to install).
        /// </remarks>
        public static string PanelAdded(string name, int matrix, string profile, FlagBoxInstallState state)
        {
            var known = state == FlagBoxInstallState.UpToDate || state == FlagBoxInstallState.Outdated;
            var where = "Added " + name + ". It is " + PanelSlot(matrix) + ": pick that content number on the device";
            if (known)
            {
                return where + " and select \"" + profile + "\" there. That one profile paints every panel, so"
                    + " SimHub's list carries its name rather than yours.";
            }
            var paints = where + ". \"" + profile + "\" is the profile that paints it, ";
            switch (state)
            {
                case FlagBoxInstallState.NotEmbedded:
                    return paints + "and this build has none to install, so the panel stays dark.";
                case FlagBoxInstallState.Unavailable:
                    return paints + "and SimHub's matrix settings could not be reached: import it by hand from the top of this page.";
                default:
                    return paints + "and SimHub has not got it: install it at the top of this page.";
            }
        }

        /// <summary>Whether <see cref="PanelAdded"/> is asking for something to be done before the panel
        /// will light, which is what decides the colour it is said in.</summary>
        public static bool PanelNeedsInstall(FlagBoxInstallState state)
        {
            return state != FlagBoxInstallState.UpToDate && state != FlagBoxInstallState.Outdated;
        }

        /// <summary>The line under the flag box heading: one profile, named. It names the profile because
        /// the panels below do not carry their own, and says nothing of where to install it: the profile's
        /// own row, with its press, is directly above.</summary>
        /// <summary>The flag box section's caption. The profile row directly above already says "8 × 8
        /// matrix", so the caption does not say it again.</summary>
        public static string BoxCaption(string profile)
        {
            return "\"" + profile + "\" is the one profile that paints every panel below.";
        }

        /// <summary>What a panel's group says under its name: which of SimHub's four contents it is, since
        /// that is the number a driver has to match on the device itself.</summary>
        public static string PanelSlot(int matrix)
        {
            return "SimHub matrix " + matrix;
        }
    }
}
