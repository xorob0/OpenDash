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
        /// yet", since the strips are strips throughout. It departs from the artboard's "Add LEDs", a row of
        /// docs/design/plugin.md's departures table; search still finds the tile by it.</summary>
        public const string AddBar = "Add an LED strip";

        public const string BarNameTitle = "Name";

        public const string BarNameCaption = "Also shown in SimHub's LED profile list.";

        public const string BarEndsTitle = "LEDs at each end";

        /// <summary>What the ends carry, and how to have none, in a driver's words rather than the generator's
        /// ("lamps"). Not drawn: the artboard has no caption there and voice.md's default is none. Kept, with its
        /// pin, as BarFanatecCaption is, as the reason the choice includes None.</summary>
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
        public static string OneDevice(string name, bool connected = true)
        {
            return connected ? "Goes to " + name + "." : "Goes to " + name + ", which is not connected.";
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
            return NameList(names) + Unreachable(names);
        }

        /// <summary>The devices passed over and what they have, with no full stop and no pointer at the log, for a
        /// line that goes on to say the steps after it: "Rim has no LEDs OpenDash can reach". Null with none.</summary>
        public static string Unreached(System.Collections.Generic.IList<string> names)
        {
            if (names == null || names.Count == 0) return null;
            return NameList(names) + (names.Count == 1 ? " has" : " have") + UnreachableFact;
        }

        private const string UnreachableFact = " no LEDs OpenDash can reach";

        /// <summary>What the devices passed over have, and where the reason is.</summary>
        private static string Unreachable(System.Collections.Generic.IList<string> names)
        {
            return (names.Count == 1 ? " has" : " have") + UnreachableFact + ". See SimHub's log.";
        }

        /// <summary>At most this many names are spelled out before the rest are counted.</summary>
        public const int NotOfferedNames = 3;

        /// <summary>
        /// The caption under the device row, given what it would say on its own and the devices passed over.
        /// </summary>
        /// <remarks>
        /// With no device offered, <see cref="NoDevices"/> would be wrong: it says there is no LED device
        /// in SimHub while the wheel is there in SimHub's list, which is the report this answers. The
        /// sentence naming the device replaces it. With one offered, the device it goes to and the ones
        /// passed over are one sentence, so the caption stays at voice.md's two: "Goes to Arduino RGB LEDs,
        /// not Rim, which has no LEDs OpenDash can reach. See SimHub's log."
        /// </remarks>
        public static string DeviceRowCaption(int offered, string caption, System.Collections.Generic.IList<string> declined)
        {
            var passed = NotOffered(declined);
            if (passed == null) return offered == 0 ? NoDevices : caption;
            if (offered == 0 || string.IsNullOrEmpty(caption)) return passed;
            return caption.Trim().TrimEnd('.') + ", not " + NameList(declined) + ", which" + Unreachable(declined);
        }

        /// <summary>
        /// The caption under the device row where SimHub has the one device and the strip is on it: where it goes,
        /// whether SimHub is talking to it, and the devices passed over.
        /// </summary>
        /// <remarks>
        /// Built from the device's name and state rather than by trimming <see cref="OneDevice"/>'s sentence: a
        /// device that is not connected is already a "which" clause, and a second ", not Rim, which ..." stacked on
        /// it read as part of "not connected". There the two facts are joined, each beside its own device, and the
        /// caption stays at voice.md's two sentences.
        /// </remarks>
        public static string OneDeviceCaption(string name, bool connected, System.Collections.Generic.IList<string> declined)
        {
            var unreached = Unreached(declined);
            if (unreached == null) return OneDevice(name, connected);
            if (connected) return DeviceRowCaption(1, OneDevice(name), declined);
            return "Goes to " + name + ", which is not connected, and " + unreached + ". See SimHub's log.";
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

        /// <summary>What a bar pointed at a device SimHub does not list is shown as, so the row says what the
        /// code knows rather than silently reading as the first device in the list. A chooser's value is read
        /// without its label, so it names the device and not an "it".</summary>
        public const string DeviceGone = "Device not in SimHub";

        /// <summary>Said beside a device that SimHub is not talking to, in the picker's value, joined as the Add
        /// sheet's device rows join their meta. A profile installs into it all the same -- the profile list is
        /// SimHub's, not the hardware's -- so this is a note and not a bar.</summary>
        public const string DeviceOffline = PanelLeds.Dot + PanelLeds.NotConnected;

        /// <summary>The centre's count, in the word the rest of the page uses for it ("Centre display").</summary>
        public const string BarCentreTitle = "LEDs in the centre";

        /// <summary>How to arrive at the centre's number. Not drawn, as <see cref="BarEndsCaption"/> is not: the
        /// note under the picture ("15 LEDs in all, as 3 · 9 · 3.") is where the count is checked.</summary>
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
        /// voice.md's own example has them: selecting the profile, which installing adds and does not select.
        /// A note the install returned (the device lists only its maker's profiles) comes first, since nothing
        /// is listed to select until it is done.
        /// </summary>
        /// <remarks>
        /// It names the device, because "your LED device" was the whole confusion: a profile goes into
        /// one device's list and OpenDash used to always pick the Arduino's, so somebody reading this
        /// line went to their wheel and found nothing. Now the line says where to look.
        ///
        /// No restart: the plugin attaches a strip's own settings when the strip is saved, so a strip added
        /// in a running SimHub takes them at once (#565). Until then the line asked for one, because the
        /// plugin published them only for the strips it held when SimHub started.
        /// </remarks>
        public static string BarAdded(string name, string device, string note = null)
        {
            return "Added " + name + ". " + (string.IsNullOrWhiteSpace(note) ? string.Empty : note.Trim() + " ") + PanelLeds.SelectIt(name, device);
        }

        /// <summary>
        /// A strip added whose profile could not be installed. It points at the log (voice.md's failure form),
        /// then names the step left: the LEDs header's Install. No restart, as <see cref="BarAdded"/> says.
        /// </summary>
        public static string BarAddFailed(string name)
        {
            return "Added " + name + ", but its profile could not be installed. See SimHub's log, then install it here.";
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
            "Needed for the car's own rev lights and car-specific shift points. Every car is downloaded at once, "
            + "about 400 KB, so your car is never disclosed.";

        /// <summary>The button's own tooltip, which is not the row's caption: the caption is three lines
        /// of what the tables are for, and a tooltip on the button says what the button does, in the verb its
        /// label carries (<see cref="CarTablesButton"/>), so a first download that turns Download into Update
        /// turns the hover with it.</summary>
        public static string CarTablesButtonTooltip(int cars)
        {
            return cars > 0 ? "Updates Lovely Car Data." : "Downloads Lovely Car Data.";
        }

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

        /// <summary>
        /// Said in place of the status's ", updated ..." once the copy is over a week old
        /// (<see cref="CarTablesStale"/>), which is worked out as the row is drawn: the status is written when the
        /// tables are read, at start and after a download, so its age can be days behind a SimHub left running. In
        /// place of it rather than after it, so the line never gives the age twice or says "updated just now"
        /// beside "over a week ago". The copy is what was updated, so the age is the update's, not the cars'.
        /// </summary>
        public const string CarTablesStaleAge = ", updated over a week ago";

        /// <summary>The step a copy over a week old is offered, after the count.</summary>
        public const string CarTablesUpdateStep = "Press Update for a newer copy.";

        /// <summary>A download that did not answer, with no copy on disk: voice.md's failure form, with the reason
        /// in SimHub's log, where the page writes it, rather than the fetch's own message on the panel.</summary>
        public const string CarTablesDownloadFailed = "Could not download Lovely Car Data. See SimHub's log.";

        /// <summary>A download that did not answer beside a copy that works, said as voice.md's "Could not reach
        /// GitHub. You have 0.3.0-rc.4." is: what failed, then what the driver still has, with the copy the subject
        /// of the count (<see cref="CarTablesCopyHas"/>).</summary>
        public const string CarTablesNewerFailed = "Could not download a newer copy.";

        /// <summary>What leads the count after a failed download: the cars are the copy's, not the driver's.</summary>
        public const string CarTablesCopyHas = "Your copy has ";

        /// <summary>Said while the tables are still being read at start, which a page opened at once can see.</summary>
        public const string CarTablesLoading = "Loading…";

        /// <summary>Said where the tables on disk could not be read: CarLightService's own status, which is in
        /// the panel's words since #523. The reason is in SimHub's log, where the page writes it, since an
        /// exception's message is not the panel's to show.</summary>
        public const string CarTablesUnreadable = CarLightService.Unreadable;

        /// <summary>CarLightService's status before the start's read has finished, as it writes it.</summary>
        public const string ServiceNotLoaded = "not loaded";

        /// <summary>The age clause of CarLightService's status ("84 cars, updated 9 days ago"), as Describe
        /// writes it.</summary>
        public const string ServiceAge = ", updated ";

        /// <summary>The tail CarLightService's status carries after a download that did not answer beside a
        /// copy that works, as Describe writes it, with the fetch's own message after it.</summary>
        public const string ServiceFailedTail = " (last download failed: ";

        /// <summary>Whether CarLightService's status says the tables could not be read, whose reason the page
        /// writes to the log.</summary>
        public static bool CarTablesUnread(string status)
        {
            return (status ?? string.Empty).Trim() == CarLightService.Unreadable;
        }

        /// <summary>
        /// The row's status line: CarLightService's status, with "updated over a week ago" in place of its age where
        /// the copy is, and a full stop in every state. The service's lowercase states, which name the tables by the noun the row retired and carry an
        /// exception's message, and its two failed downloads, which carry the fetch's message, are said in the
        /// row's own words; the page writes the reasons to SimHub's log.
        /// </summary>
        public static string CarTablesLine(string status, bool stale)
        {
            var said = (status ?? string.Empty).Trim();
            if (said == ServiceNotLoaded) return CarTablesLoading;
            if (CarTablesUnread(said)) return CarTablesUnreadable;
            if (said.StartsWith(CarTablesNone, StringComparison.Ordinal))
            {
                // "Not downloaded yet. Download failed: <reason>." where a download did not answer.
                return said.Length > CarTablesNone.Length ? CarTablesDownloadFailed : CarTablesNone;
            }
            var failedAt = said.IndexOf(ServiceFailedTail, StringComparison.Ordinal);
            var failed = failedAt >= 0;
            if (failed) said = said.Substring(0, failedAt).TrimEnd();
            if (said.Length == 0) return string.Empty;
            var agedAt = said.IndexOf(ServiceAge, StringComparison.Ordinal);
            // Over a week ago replaces the age; a copy with no fetch stamp has no age to replace, and is stale
            // only because nothing says when it came, so it is not called over a week old.
            var copy = !stale ? said : agedAt >= 0 ? said.Substring(0, agedAt) + CarTablesStaleAge : said;
            if (failed) return CarTablesNewerFailed + " " + CarTablesCopyHas + copy + ".";
            return stale ? copy + ". " + CarTablesUpdateStep : copy + ".";
        }

        /// <summary>
        /// Whether the copy is over a week old, from what CarLightService holds in memory: its count of cars and
        /// its fetch stamp, both written by the read that wrote its status. CarLightLibrary.IsStale's rule (no
        /// stamp, a stamp <see cref="CarLightLibrary.MaxAge"/> old, or a stamp in the future) over the same stamp
        /// without reading the folder, so the row can be drawn from a tick: nothing is read from disk on one.
        /// </summary>
        public static bool CarTablesStale(int cars, DateTime? fetchedAt, DateTime nowUtc)
        {
            if (cars <= 0) return false;
            if (fetchedAt == null) return true;
            return nowUtc - fetchedAt.Value >= CarLightLibrary.MaxAge || fetchedAt.Value > nowUtc;
        }

        /// <summary>Whether CarLightService's status says a download did not answer, with a copy on disk or
        /// without: the page writes its reason to SimHub's log, since the row says only that it failed.</summary>
        public static bool CarTablesDownloadDidNotAnswer(string status)
        {
            var said = (status ?? string.Empty).Trim();
            return said.IndexOf(ServiceFailedTail, StringComparison.Ordinal) >= 0
                || (said.StartsWith(CarTablesNone, StringComparison.Ordinal) && said.Length > CarTablesNone.Length);
        }
    }
}
