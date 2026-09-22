// PanelLights.cs: the words the Lights tab puts beside a value, and the section that answers for the
// whole rig.
//
// Apart from SettingsControl.Lights.cs for the reason PanelCopy.cs is apart from Widgets.cs: the tab is
// WPF and the net8.0 test project cannot compile a line of it, so what a test can hold has to live where
// it can reach. What it holds here is the pairing. BuildChoice and BuildSegmented are handed the values
// and the labels as two arrays and read them by index, so a value set that gains or loses one leaves the
// labels beside it wrong without saying so: the centre drop-down carried a label for a retired fifth
// value that way, and a segmented bar one label short throws while a tab is being drawn. Pure: no WPF
// types.
namespace OpenDashPlugin
{
    public static class PanelLights
    {
        /// <summary>One label per <see cref="Contract.LedCentres"/> value, in its order.</summary>
        public static readonly string[] CentreLabels = { "RPM", "Brake", "Throttle and brake", "Fuel" };

        /// <summary>
        /// One label per <see cref="Contract.LedRpmStyles"/> value, in its order. Car-specific heads the
        /// list because the value does, and it is the one a driver is offered first.
        /// </summary>
        /// <remarks>
        /// It used to read "The car's own", which is a possessive with no head noun: the reader has to
        /// guess what it owns, and the three names beside it are patterns rather than owners, so there is
        /// nothing to guess from. Car-specific is what Lovely Sim Racing calls these tables, so a driver
        /// who met them there recognises the word. docs/design/voice.md keeps the rule.
        /// </remarks>
        public static readonly string[] RpmStyleLabels = { "Car-specific", "Left to right", "Meet in middle", "F1" };

        /// <summary>One label per <see cref="Contract.LedMirrorFits"/> value, in its order.</summary>
        public static readonly string[] MirrorFitLabels = { "Fill the strip", "True size" };

        /// <summary>One label per <see cref="Contract.FlagBoxRests"/> value, in its order.</summary>
        public static readonly string[] RestLabels = { "Dark", "Gear" };

        /// <summary>One label per <see cref="Contract.FlagBoxSides"/> value, in its order.</summary>
        public static readonly string[] SideLabels = { "Both", "Left", "Right" };

        /// <summary>The heading over the strips a driver has added.</summary>
        public const string BarsTitle = "Your LED strips";

        /// <summary>
        /// The line under it.
        /// </summary>
        /// <remarks>
        /// A strip used to be a shape the Install tab offered and nothing more, so two strips on one rig
        /// could be installed separately and not configured separately -- a wheel and a brow shared one
        /// answer to what their middles show. A bar is an instance now, the way a screen is: it has a
        /// name, a shape and settings of its own, and installing it is what puts a profile of that name
        /// into SimHub.
        /// </remarks>
        public const string BarsCaption = "Add one for each RGB strip you have.";

        public const string NoBars = "No strips yet.";

        public const string AddBar = "Add an LED strip";

        public const string BarNameTitle = "Name";

        public const string BarNameCaption = "Also shown in SimHub's LED profile list.";

        public const string BarEndsTitle = "LEDs at each end";

        public const string BarEndsCaption = "Flags, warnings and cars alongside. Pick None for one continuous run.";

        /// <summary>The device row of the add flow and of every bar.</summary>
        public const string BarDeviceTitle = "LED device";

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

        /// <summary>Said when SimHub has none. The bar is still added and still configurable; what it
        /// cannot have is a profile anywhere, which is a thing about the rig and not about OpenDash.</summary>
        public const string NoDevices = "No LED device in SimHub. Add your wheel or Arduino there first.";

        /// <summary>What a bar pointed at a device SimHub no longer has is shown as, so the row says what
        /// happened rather than silently reading as the first device in the list.</summary>
        public const string DeviceGone = "The device it was on (no longer on this rig)";

        /// <summary>Said beside a device that SimHub is not talking to. A profile installs into it all the
        /// same -- the profile list is SimHub's, not the hardware's -- so this is a note and not a bar.</summary>
        public const string DeviceOffline = " (not connected)";

        public const string BarCentreTitle = "LEDs in the middle";

        public const string BarCentreCaption = "Count your LEDs and subtract the ends.";

        /// <summary>The line under the two numbers: what they add up to and what the profile will be
        /// called. A driver counts LEDs, and this is where the two counts are checked against the total
        /// they actually have.</summary>
        public static string BarShapeNote(int side, int centre)
        {
            var total = side * 2 + centre;
            var shape = side + "/" + centre + "/" + side;
            return total + (total == 1 ? " LED in all" : " LEDs in all") + ", as " + shape + ".";
        }

        /// <summary>What a bar of this shape is called before the driver types over it.</summary>
        public static string BarShapeId(int side, int centre)
        {
            return side + "-" + centre + "-" + side;
        }

        /// <summary>
        /// What is said once a bar exists, which is the step SimHub does not take for you: installing
        /// adds a profile, it does not select one on the device.
        /// </summary>
        /// <remarks>
        /// It names the device, because "your LED device" was the whole confusion: a profile goes into
        /// one device's list and OpenDash used to always pick the Arduino's, so somebody reading this
        /// line went to their wheel and found nothing. Now the line says where to look.
        /// </remarks>
        public static string BarAdded(string name, string device)
        {
            var where = string.IsNullOrWhiteSpace(device) ? "your LED device" : device;
            return "Added " + name + ". Select \"" + name + "\" on " + where + " in SimHub to use it.";
        }

        public static string BarAddFailed(string name)
        {
            return "Added " + name + ", but its profile could not be installed. See the Install tab.";
        }

        /// <summary>The row that offers the car light tables, at the foot of the strips section.</summary>
        public const string CarTablesTitle = "Car light tables";

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
            "Needed for the Car-specific rev light style. Every car is downloaded at once, about 400 KB, "
            + "so your car is never disclosed.";

        /// <summary>The button's own tooltip, which is not the row's caption: the caption is three lines
        /// of what the tables are for, and a tooltip on the button says what the button does.</summary>
        public const string CarTablesButtonTooltip = "Downloads the car light tables.";

        /// <summary>Who measured it, and where to go and see. Shown under the row for as long as it exists.</summary>
        public static readonly string CarTablesAttribution = CarLightLibrary.Attribution + " " + CarLightLibrary.ProjectUrl;

        /// <summary>The state a rig is in until somebody presses the button, which is every rig on a fresh install.</summary>
        public const string CarTablesNone = "No car light tables yet.";

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

        public const string NoPanels = "No panels yet.";

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
        /// </remarks>
        public static string PanelAdded(string name, int matrix, string profile, FlagBoxInstallState state)
        {
            var known = state == FlagBoxInstallState.UpToDate || state == FlagBoxInstallState.Outdated;
            var where = "Added " + name + ". It is " + PanelSlot(matrix) + ": pick that content number on the device";
            return known
                ? where + " and select \"" + profile + "\" there. That one profile paints every panel, so"
                    + " SimHub's list carries its name rather than yours."
                : where + ". \"" + profile + "\" is the profile that paints it, and SimHub has not got it:"
                    + " install it from the Install tab.";
        }

        /// <summary>Whether <see cref="PanelAdded"/> is asking for something to be done before the panel
        /// will light, which is what decides the colour it is said in.</summary>
        public static bool PanelNeedsInstall(FlagBoxInstallState state)
        {
            return state != FlagBoxInstallState.UpToDate && state != FlagBoxInstallState.Outdated;
        }

        /// <summary>The line under the flag box heading: one profile, named, and where it comes from.
        /// It names the profile because the panels below do not carry their own.</summary>
        public static string BoxCaption(string profile)
        {
            return "An 8x8 LED matrix. \"" + profile + "\" is the one profile that paints every panel below;"
                + " install it from the Install tab.";
        }

        /// <summary>What a panel's group says under its name: which of SimHub's four contents it is, since
        /// that is the number a driver has to match on the device itself.</summary>
        public static string PanelSlot(int matrix)
        {
            return "SimHub matrix " + matrix;
        }

        /// <summary>The section the three rig-wide settings sit in, at the foot of the tab: they are not
        /// the flag box's, and a driver who owns a strip as well as a box sets them once.</summary>
        public const string RigWideTitle = "All lights";
    }
}
