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

        /// <summary>One label per <see cref="Contract.LedRpmStyles"/> value, in its order. The car's own
        /// heads the list because the value does, and it is the one a driver is offered first.</summary>
        public static readonly string[] RpmStyleLabels = { "The car's own", "Left to right", "Meet in middle", "F1" };

        /// <summary>One label per <see cref="Contract.LedMirrorFits"/> value, in its order.</summary>
        public static readonly string[] MirrorFitLabels = { "Fill the strip", "True size" };

        /// <summary>One label per <see cref="Contract.FlagBoxRests"/> value, in its order.</summary>
        public static readonly string[] RestLabels = { "Dark", "Gear" };

        /// <summary>One label per <see cref="Contract.FlagBoxSides"/> value, in its order.</summary>
        public static readonly string[] SideLabels = { "Both", "Left", "Right" };

        /// <summary>The heading over the strips a driver has added.</summary>
        public const string BarsTitle = "Your LED bars";

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
        public const string BarsCaption =
            "An RGB strip on the wheel, the rim or above the monitor. Add one for each strip you have and give it a name; "
            + "openDash installs a profile under that name, and the settings under it are that bar's alone.";

        public const string NoBars = "No bars yet. Add one and openDash installs its profile into SimHub, ready to pick on the device.";

        public const string AddBar = "Add an LED bar";

        public const string BarNameTitle = "Call it";

        public const string BarNameCaption = "Yours. It names the group here and the profile in SimHub's own LED profile list.";

        public const string BarEndsTitle = "LEDs at each end";

        public const string BarEndsCaption = "The group at each end of the strip, which carries the lamps: a car alongside, the flags, your car's own warnings and the aids. None means the whole strip is one run, which is what a brow above a monitor is.";

        /// <summary>The device row of the add flow and of every bar.</summary>
        public const string BarDeviceTitle = "Which device";

        /// <summary>
        /// The line under it, which has to carry a fact about SimHub rather than a preference.
        /// </summary>
        /// <remarks>
        /// There is no shared list of LED profiles in SimHub. Every LED device keeps its own, in its own
        /// file, and a profile in one is invisible in every other. openDash used to install into the
        /// Arduino RGB LEDs device whatever the strip was, so a bar added for a wheel was written and
        /// saved correctly into a list the wheel does not read, and the driver went looking in the wheel
        /// and found nothing. Somebody reading this row has to understand that choosing is not optional.
        /// </remarks>
        public const string BarDeviceCaption =
            "Where the profile goes. Each device in SimHub keeps its own LED profiles, so pick the one these LEDs are on -- "
            + "a wheel, a button plate, or the Arduino if you wired the strip yourself.";

        /// <summary>Said in place of the picker when SimHub has exactly one LED device: there is nothing
        /// to choose, and a drop-down of one is a question with one answer.</summary>
        public static string OneDevice(string name)
        {
            return "Its profile goes to " + name + ", which is the only device SimHub has LEDs for.";
        }

        /// <summary>Said when SimHub has none. The bar is still added and still configurable; what it
        /// cannot have is a profile anywhere, which is a thing about the rig and not about openDash.</summary>
        public const string NoDevices =
            "SimHub has no device with RGB LEDs. Add your wheel or your Arduino under SimHub's own Devices or Arduino page first, "
            + "then add the bar and its profile has somewhere to go.";

        /// <summary>What a bar pointed at a device SimHub no longer has is shown as, so the row says what
        /// happened rather than silently reading as the first device in the list.</summary>
        public const string DeviceGone = "The device it was on (not on this rig now)";

        /// <summary>Said beside a device that SimHub is not talking to. A profile installs into it all the
        /// same -- the profile list is SimHub's, not the hardware's -- so this is a note and not a bar.</summary>
        public const string DeviceOffline = " (not connected)";

        public const string BarCentreTitle = "LEDs in the middle";

        public const string BarCentreCaption = "The run between them, which carries the rev ladder. Count the LEDs on your strip and take the ends off; the line below says what that adds up to.";

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
        /// one device's list and openDash used to always pick the Arduino's, so somebody reading this
        /// line went to their wheel and found nothing. Now the line says where to look.
        /// </remarks>
        public static string BarAdded(string name, string device)
        {
            var where = string.IsNullOrWhiteSpace(device) ? "your LED device" : device;
            return "Added " + name + " and installed its profile into " + where + ". Open it in SimHub and select \"" + name + "\" on it.";
        }

        public static string BarAddFailed(string name)
        {
            return "Added " + name + ", but its profile could not be installed into SimHub. The Install tab says why.";
        }

        /// <summary>The row that offers the car light tables, at the foot of the strips section.</summary>
        public const string CarTablesTitle = "The car's own lights";

        /// <summary>
        /// What the button will do, said before it is pressed rather than after.
        /// </summary>
        /// <remarks>
        /// Every clause of this is load-bearing and none of it is decoration.
        ///
        /// <para>The tables are somebody else's work under CC BY-NC-SA 4.0 and openDash ships none of
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
            "Measured tables for the cars other people have measured: your car's own LED colours, thresholds and gears, "
            + "instead of openDash's three bands. Downloading asks GitHub once for every car at once — about 400 KB — "
            + "so nothing about which car you drive leaves your machine. Afterwards every car works offline.";

        /// <summary>Who measured it, and where to go and see. Shown under the row for as long as it exists.</summary>
        public static readonly string CarTablesAttribution = CarLightLibrary.Attribution + " " + CarLightLibrary.ProjectUrl;

        /// <summary>The state a rig is in until somebody presses the button, which is every rig on a fresh install.</summary>
        public const string CarTablesNone = "No car light tables yet, so every car is on the lights openDash works out for itself.";

        /// <summary>Said after a download that did not answer, beside whatever is already on disk.</summary>
        public static string CarTablesFailed(string reason)
        {
            return "The download did not answer: " + (string.IsNullOrWhiteSpace(reason) ? "no reason given" : reason) + ".";
        }

        /// <summary>While the request is out. A press with no answer for ten seconds reads as a dead button.</summary>
        public const string CarTablesDownloading = "Downloading the car light tables…";

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
        public const string CarTablesStale = "This copy is over a week old. Nothing refetches on its own; press Update when you want a newer one.";

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
        public const string PanelsCaption =
            "SimHub composes up to four contents onto one matrix device. Add one for each panel you actually have, and "
            + "give it a name you will recognise — \"top left\", \"by the wheel\".";

        public const string NoPanels = "No panels yet. The flag box profile is installed and will draw nothing until you add one.";

        public const string AddPanel = "Add a matrix panel";

        public const string PanelNameTitle = "Call it";

        public const string PanelNameCaption = "Yours. It names the group here; the panel itself is whichever of SimHub's four contents this is.";

        /// <summary>What a panel's group says under its name: which of SimHub's four contents it is, since
        /// that is the number a driver has to match on the device itself.</summary>
        public static string PanelSlot(int matrix)
        {
            return "SimHub matrix " + matrix;
        }

        /// <summary>The section the three rig-wide settings sit in, at the foot of the tab: they are not
        /// the flag box's, and a driver who owns a strip as well as a box sets them once.</summary>
        public const string RigWideTitle = "For every light";

        public const string RigWideCaption = "Brightness is one answer for the rig.";
    }
}
