// PanelMatrix.cs: the Matrix page's words, numbers and decisions -- everything SettingsControl.Matrix.cs
// draws, so a test can hold it.
//
// The page is Matrix.dc.html: the flag box profile on the title's line, a card for each matrix the rig has,
// and for the one selected its preview beside what may take it over, in priority order, and what it shows
// at rest. The words follow docs/design/voice.md where the artboard differs (the critic's rulings in the
// #503 inventory): one noun, "matrix", for the thing a driver owns; the row names the panel has always used
// ("Idle display", "Mounting side", "Redline flash"); and a profile named the way SimHub lists it.
//
// The matrix copy PanelLights still holds for the old tab is copied here rather than moved, because
// PanelLights is the LEDs page's; this page reads none of it. Pure: no WPF.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenDashPlugin
{
    public static class PanelMatrix
    {
        public const string Title = "Matrix";

        /// <summary>
        /// The empty state, here and under Home's Matrix eyebrow, which reads this constant. One noun for the
        /// thing a driver owns (ruling 59): a matrix, never a panel, which in this panel is the settings
        /// window itself.
        /// </summary>
        public const string NoPanels = "No matrices yet.";

        /// <summary>How many a rig can have: SimHub composes four matrix contents and not five.</summary>
        public const int MaxPanels = 4;

        // --- The profile, on the title's line ---------------------------------------------------------

        /// <summary>
        /// What the flag box profile's line says after the profile's name, and the press beside it. The
        /// Matrix page's own table: the Updates page's rows read PanelCopy.LightRow, so neither page rewords
        /// a state for the other. The state is the version SimHub holds, since the name already says which
        /// profile it is: "OpenDash Flag box · 0.5.0".
        /// </summary>
        public static RowAction ProfileRow(FlagBoxInstallState state, string installedVersion)
        {
            var installed = string.IsNullOrEmpty(installedVersion) ? Installed : installedVersion;
            switch (state)
            {
                case FlagBoxInstallState.Outdated:
                    return new RowAction(installed, Theme.StatusUpToDate, "Update", PanelButton.Primary);
                case FlagBoxInstallState.UpToDate:
                    return new RowAction(installed, Theme.StatusUpToDate, "Reinstall", PanelButton.Outline);
                case FlagBoxInstallState.Failed:
                    return new RowAction(PanelCopy.InstallFailed, Theme.StatusFailed, "Install", PanelButton.Outline);
                default:
                    return new RowAction(PanelCopy.NotInstalled, Theme.TextLabel, "Install", PanelButton.Outline);
            }
        }

        /// <summary>The state of a profile SimHub holds whose version it does not say.</summary>
        public const string Installed = "Installed";

        /// <summary>What the title's line says when this build carries no profile to install.</summary>
        public const string NoProfile = "This build ships no flag box profile.";

        /// <summary>
        /// The line beside the status dot: the profile as SimHub lists it and its state. A build with no
        /// profile says so and nothing further; a SimHub whose matrix settings could not be reached names the
        /// profile only, since the by-hand import under the title says the rest.
        /// </summary>
        public static string ProfileLine(string profile, FlagBoxInstallState state, string installedVersion)
        {
            if (state == FlagBoxInstallState.NotEmbedded) return NoProfile;
            if (state == FlagBoxInstallState.Unavailable) return profile;
            return profile + " · " + ProfileRow(state, installedVersion).State;
        }

        /// <summary>Whether the line offers its press: never when there is no profile to install or nowhere
        /// to put it. The press is left out then rather than drawn disabled.</summary>
        public static bool ProfileHasButton(FlagBoxInstallState state)
        {
            return state != FlagBoxInstallState.NotEmbedded && state != FlagBoxInstallState.Unavailable;
        }

        public const string ProfileTooltip = "Adds OpenDash's profile. Your own profiles are never changed.";

        /// <summary>What is said once the press has run, or null when the line above already says it all.
        /// Installing adds the profile and does not select it, so the sentence names that step.</summary>
        public static PanelMessage InstallSaid(FlagBoxInstallState state, string profile)
        {
            switch (state)
            {
                case FlagBoxInstallState.UpToDate:
                case FlagBoxInstallState.Outdated:
                    return PanelMessage.Info("Installed \"" + profile + "\". Select it on each matrix device in SimHub.");
                case FlagBoxInstallState.Unavailable:
                    return PanelMessage.Caution("SimHub's matrix settings could not be reached. Import the profile by hand.");
                case FlagBoxInstallState.NotEmbedded:
                    return null;
                default:
                    return PanelMessage.Danger("Install failed. See SimHub's log.");
            }
        }

        public const double ProfileDotSize = 7;
        public const double ProfileGap = 10;

        /// <summary>The ghost press on the title's line is padded 6, not the kit's 10 (.btn.sm.ghost, padding 0 6).</summary>
        public const double ProfileButtonPaddingX = 6;

        // --- The cards ---------------------------------------------------------------------------------

        /// <summary>What the cards are, for a screen reader and for search: the artboard's tablist.</summary>
        public const string PanelsTitle = "Your matrices";

        public const string AddPanel = "Add a matrix";
        public const string AddTooltip = "Adds a matrix.";
        public const string AllInUse = "All four matrices are in use.";

        /// <summary>The count after the add tile's words: "2 / 4".</summary>
        public static string AddCount(int count)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " / " + MaxPanels.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Whether another can be added, which is what enables the tile.</summary>
        public static bool CanAdd(int count)
        {
            return count < MaxPanels;
        }

        /// <summary>The artboard's three columns 12 apart, and the narrowest a card reads at: the 8x8, 12,
        /// and "Not shown in SimHub" at 12 px inside 14 either side.</summary>
        public const double CardMinWidth = 200;
        public const double CardGap = 12;
        public const int CardColumns = 3;

        /// <summary>Which of SimHub's four contents a matrix is: the number a driver matches on the device.</summary>
        public static string Slot(int matrix)
        {
            return "Matrix " + matrix.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The name a matrix is added with, and the one it is called by when it has none.</summary>
        public static string DefaultName(int matrix)
        {
            return Slot(matrix);
        }

        public static string NameOf(string name, int matrix)
        {
            return string.IsNullOrWhiteSpace(name) ? DefaultName(matrix) : name;
        }

        /// <summary>The selection a card keeps for the session (SettingsControl.Select), as text.</summary>
        public static string SlotId(int matrix)
        {
            return matrix.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The matrix the page opens on: the one selected when the rig still has it, else the
        /// first, else none (0).</summary>
        public static int SelectedSlot(IList<int> panels, string selected)
        {
            if (panels == null || panels.Count == 0) return 0;
            int slot;
            if (selected != null && int.TryParse(selected, NumberStyles.Integer, CultureInfo.InvariantCulture, out slot) && panels.Contains(slot)) return slot;
            return panels[0];
        }

        /// <summary>One label per <see cref="Contract.FlagBoxSides"/> value, in its order: the artboard's
        /// "Both sides" for the one that is not a side.</summary>
        public static readonly string[] SideLabels = { "Both sides", "Left", "Right" };

        /// <summary>One label per <see cref="Contract.FlagBoxRests"/> value, in its order.</summary>
        public static readonly string[] RestLabels = { "Dark", "Gear" };

        public static string SideLabel(string side)
        {
            var i = Array.IndexOf(Contract.FlagBoxSides, side);
            return SideLabels[i < 0 ? 0 : i];
        }

        public const string Showing = "Showing";
        public const string NotShown = "Not shown in SimHub";

        /// <summary>
        /// The line under a card's name. Whether a device shows the matrix is not on SimHub's public
        /// surface today (MatrixFacts' Shown is null), and a card never claims what nothing read: it then
        /// says which content it is and which side it is mounted on, written as its control names it.
        /// </summary>
        public static string CardLine(int matrix, string side, bool? shown)
        {
            if (shown == true) return Showing;
            if (shown == false) return NotShown;
            return Slot(matrix) + " · " + SideLabel(side);
        }

        public static string CardLineHex(bool? shown)
        {
            return shown == false ? Theme.Caution : Theme.TextSecondary;
        }

        // --- The selected matrix -------------------------------------------------------------------------

        /// <summary>The line beside the selected matrix's name: its content number, unless the name is it.</summary>
        public static string SlotCaption(string name, int matrix)
        {
            var slot = Slot(matrix);
            return string.Equals(name, slot, StringComparison.Ordinal) ? null : slot;
        }

        public const string Rename = "Rename";
        public const string RenameTooltip = "Renames this matrix.";
        public const string Remove = "Remove";
        public const string RemoveTooltip = "Removes this matrix.";

        public static string RenameTitle(string name)
        {
            return "Rename " + name;
        }

        public static string RemoveTitle(string name)
        {
            return "Remove " + name;
        }

        /// <summary>What the one question before a removal says: the consequence, not the reason.</summary>
        public const string RemoveCaption = "Its settings go with it. The flag box profile stays in SimHub.";

        public static string Renamed(string name)
        {
            return "Renamed to " + name + ".";
        }

        /// <summary>What is said after a removal: the step it leaves, which is a device that now shows nothing.</summary>
        public static string Removed(string name, int matrix)
        {
            return "Removed " + name + ". A device set to matrix content " + matrix.ToString(CultureInfo.InvariantCulture) + " stays dark.";
        }

        public const string NameTitle = "Name";

        /// <summary>
        /// The line under the name box, which says what the name is not: one profile paints every matrix, so
        /// SimHub's list carries the profile's name and never this one.
        /// </summary>
        public const string NameCaption = "OpenDash's own label. It is not shown in SimHub's profile list.";

        public const string Cancel = "Cancel";

        /// <summary>The gap between a sheet's caption and its name row, and under the empty state.</summary>
        public const double SheetGap = 12;
        public const double EmptyGap = 12;

        /// <summary>The name box's width in the sheets.</summary>
        public const double NameWidth = 280;

        /// <summary>What the add sheet says above the name: the content number the matrix will be and the
        /// step on the device that shows it.</summary>
        public static string AddPanelCaption(int matrix, string profile)
        {
            var n = matrix.ToString(CultureInfo.InvariantCulture);
            return "It will be matrix " + n + ". On the device, select \"" + profile + "\" and set Matrix content to " + n + ".";
        }

        /// <summary>
        /// What is said once a matrix exists: the step SimHub does not take, in the order it is taken. When
        /// SimHub has no copy of the profile, the sentence sends the driver to the title's line, which can
        /// install it, or to the by-hand import under it; and when this build has none, it says so.
        /// </summary>
        public static string PanelAdded(string name, int matrix, string profile, FlagBoxInstallState state)
        {
            var n = matrix.ToString(CultureInfo.InvariantCulture);
            var added = "Added " + name + ". ";
            switch (state)
            {
                case FlagBoxInstallState.UpToDate:
                case FlagBoxInstallState.Outdated:
                    return added + "On the device, select \"" + profile + "\" and set Matrix content to " + n + ".";
                case FlagBoxInstallState.NotEmbedded:
                    return added + "This build has no flag box profile to install, so it stays dark.";
                case FlagBoxInstallState.Unavailable:
                    return added + "Import \"" + profile + "\" by hand from the top of this page, then set Matrix content to " + n + " on the device.";
                default:
                    return added + "Install \"" + profile + "\" at the top of this page, then select it on the device and set Matrix content to " + n + ".";
            }
        }

        /// <summary>Whether <see cref="PanelAdded"/> asks for something before the matrix will light, which
        /// is what decides the ink it is said in.</summary>
        public static bool NeedsInstall(FlagBoxInstallState state)
        {
            return state != FlagBoxInstallState.UpToDate && state != FlagBoxInstallState.Outdated;
        }

        /// <summary>The fix box's title, drawn only when something has read that no device shows the matrix.</summary>
        public static string FixTitle(int matrix)
        {
            return "No device in SimHub shows matrix " + matrix.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>SimHub names its devices itself, so the crumb names the one a driver is to open.</summary>
        public const string YourMatrixCrumb = "your matrix";

        /// <summary>The fix box's steps: the device in SimHub, the profile to select on it, the content to set.</summary>
        public static IList<string[]> FixSteps(int matrix, string profile)
        {
            return new List<string[]>
            {
                new[] { PanelAttention.DevicesCrumb, YourMatrixCrumb },
                new[] { "Profile: " + profile },
                new[] { "Matrix content: " + matrix.ToString(CultureInfo.InvariantCulture) },
            };
        }

        // The selected matrix's section: a rule over it, 20 under the rule and 18 between its parts; the name
        // at 22 with its content number 12 after it, and Rename and Remove 6 apart.
        public const double SectionPaddingTop = 20;
        public const double SectionGap = 18;
        public const double HeaderGap = 12;
        public const double ActionGap = 6;

        // --- The preview -----------------------------------------------------------------------------

        /// <summary>
        /// The scenario the idle display is drawn in, on the cards and under the preview's first chip: the revs
        /// at idle, which is the box at rest -- the gear in its one white, whatever Shift colours says, as Home
        /// draws the matrix and as the Rig page's "Idle" chip does, so one picture has one name.
        /// </summary>
        public const string IdleScenario = PanelEmulation.Idle;

        /// <summary>The preview's chips, in the artboard's order.</summary>
        public static readonly string[] PreviewScenarios =
        {
            IdleScenario, PanelEmulation.Yellow, PanelEmulation.Blue, PanelEmulation.Limiter,
            PanelEmulation.CarLeft, PanelEmulation.LowFuel, PanelEmulation.Chequer,
        };

        /// <summary>A chip's label: the Rig page's word for the scenario, and the idle display by its row's name.</summary>
        public static string PreviewLabel(string scenarioId)
        {
            if (scenarioId == IdleScenario) return IdleDisplayTitle;
            var scenario = PanelEmulation.Find(scenarioId);
            return scenario == null ? IdleDisplayTitle : scenario.Label;
        }

        /// <summary>The chip the preview draws: the one held for the session when it is one of the chips.</summary>
        public static string PreviewScenario(string held)
        {
            return held != null && Array.IndexOf(PreviewScenarios, held) >= 0 ? held : IdleScenario;
        }

        /// <summary>
        /// A matrix's settings as its picture reads them, the families included: a family switched off
        /// leaves the matrix at its idle display under that chip, which is what the matrix would do.
        /// </summary>
        public static MatrixOptions OptionsFor(OpenDashSettings settings, int matrix)
        {
            return new MatrixOptions
            {
                Side = settings.MatrixSide(matrix),
                Rest = settings.MatrixRest(matrix),
                Bands = settings.MatrixGearBands(matrix),
                CarLadder = settings.MatrixGearCarLadder(matrix),
                Flags = settings.MatrixFlags(matrix),
                Pit = settings.MatrixPit(matrix),
                Spotter = settings.MatrixSpotter(matrix),
                Warnings = settings.MatrixWarnings(matrix),
            };
        }

        /// <summary>
        /// The scenario a chip is drawn in, which is the chip's own unless the matrix would not show it: a
        /// family switched off, a car on the side the matrix is not mounted on, or, with Critical flags only on,
        /// a flag that is news rather than a warning (the chequer, the white and the green). Each of those
        /// leaves the matrix at its idle display, so it is drawn as the first chip draws it. The flag box's
        /// catalogue is packages/dash/src/flags.ts ("critical"); docs/design/flag-box.md says what it drops.
        /// </summary>
        public static string DrawnScenario(string scenario, MatrixOptions options, bool criticalOnly)
        {
            if (scenario == null || scenario == IdleScenario) return IdleScenario;
            if (criticalOnly && IsNewsFlag(scenario)) return IdleScenario;
            // Whether anything but the idle display shows: the chip on a matrix that rests dark.
            var dark = new MatrixOptions
            {
                Side = options == null ? Contract.FlagBoxSides[0] : options.Side,
                Rest = "dark",
                Flags = options == null || options.Flags,
                Pit = options == null || options.Pit,
                Spotter = options == null || options.Spotter,
                Warnings = options == null || options.Warnings,
            };
            return PanelEmulation.GlyphFor(scenario, dark) == null ? IdleScenario : scenario;
        }

        /// <summary>The flags Critical flags only drops, among those the emulation draws.</summary>
        public static bool IsNewsFlag(string scenario)
        {
            return scenario == PanelEmulation.Chequer || scenario == PanelEmulation.White || scenario == PanelEmulation.Green;
        }

        /// <summary>What the chips are, for a screen reader: the artboard's group.</summary>
        public const string PreviewChipsName = "Preview";

        /// <summary>The link under the preview, to the Rig page on the same scenario.</summary>
        public const string AllDevices = "All devices at once";

        // The preview column: 300 wide and 36 from the priority list, its 8x8 in a frame padded 20 on the
        // inset ground, 14 between the frame, the link and the chips, and the chips 6 apart.
        public const double PreviewColumnWidth = 300;
        public const double BodyGap = 36;
        public const double PreviewFramePadding = 20;
        public const double PreviewGap = 14;
        public const double ChipGap = 6;

        /// <summary>The gap between the preview and the priority list when they are stacked: the page's own.</summary>
        public const double StackedGap = 22;

        /// <summary>How far into the preview's frame the New tag sits from its corner.</summary>
        public const double NewTagInset = 8;

        // --- What may take the matrix over -------------------------------------------------------------

        public const string PriorityTitle = "Priority";

        /// <summary>The greyed caption over the list, until the order can be changed (#505).</summary>
        public const string DragToReorder = "Drag to reorder";
        public const double ReorderGap = 8;

        public const string FlagsTitle = "Flags";
        public const string PitLaneTitle = "Pit lane";
        public const string SpotterTitle = "Spotter";
        public const string WarningsTitle = "Warnings";

        /// <summary>What may take the matrix over, first first. The flag box's own order, which is fixed
        /// until #505 lets a driver change it.</summary>
        public static readonly string[] Layers = { FlagsTitle, PitLaneTitle, SpotterTitle, WarningsTitle };

        /// <summary>A layer's number in the list, 1 for the first.</summary>
        public static string Rank(string layer)
        {
            var i = Array.IndexOf(Layers, layer);
            return i < 0 ? string.Empty : (i + 1).ToString(CultureInfo.InvariantCulture);
        }

        public const string PitLaneCaption = "Pit limiter and speeding.";
        public const string WarningsCaption = "Low fuel, oil temperature and water temperature.";

        public const string CriticalFlagsOnlyTitle = "Critical flags only";
        public const string CriticalFlagsOnlyCaption = "Stays dark for the chequer, white, green and start gantry.";
        public const string MountingSideTitle = "Mounting side";

        /// <summary>The spotter's slide, which every matrix shares: the caption says so, since the row sits
        /// among one matrix's own.</summary>
        public const string SpotterAnimationTitle = "Spotter bar animation";
        public const string SpotterAnimationCaption = "Every matrix.";

        /// <summary>The link on the Warnings row: the thresholds are the rig's, on Settings' Alerts.</summary>
        public const string ThresholdsLink = "Thresholds";
        public static readonly PanelRoute ThresholdsRoute = new PanelRoute(PanelPage.Settings, PanelSettings.AnchorAlerts);

        public const string IdleDisplayTitle = "Idle display";
        public const string ShiftColoursTitle = "Shift colours";
        public const string RedlineFlashTitle = "Redline flash";

        /// <summary>The digit's colours following the car's own shift lights: "shift points", as voice.md's
        /// rulings name them, since "thresholds" is the settings model's word and not the driver's; and
        /// "Car-specific", the name voice.md gives the car's own tables, which is also what Lovely Sim Racing
        /// calls the tables behind them, so a driver who met them there recognises the word.</summary>
        public const string CarShiftPointsTitle = "Car-specific shift points";

        /// <summary>The rows under Idle display appear only when the gear is what it shows.</summary>
        public static bool ShowsGearRows(string rest)
        {
            return string.Equals(rest, "gear", StringComparison.Ordinal);
        }

        /// <summary>The car's own shift points colour the gear, so the row is there only with Shift colours on.</summary>
        public static bool ShowsCarShiftPoints(string rest, bool bands)
        {
            return ShowsGearRows(rest) && bands;
        }

        /// <summary>The flash is the last shift colour, the car's own included, so it shows whenever the
        /// gear changes colour at all.</summary>
        public static bool ShowsRedlineFlash(string rest, bool bands)
        {
            return ShowsGearRows(rest) && bands;
        }

        /// <summary>
        /// The line under Car-specific shift points, the same as on LEDs (PanelLeds.CarLine): while the switch
        /// is on and a car is loaded, whether the tables have measured it, which says whether the gear takes
        /// the car's own points or falls back to openDash's. No line while the switch is off or no car is loaded.
        /// </summary>
        public static string CarLine(bool on, string car, bool hasTable)
        {
            if (!on || string.IsNullOrWhiteSpace(car)) return null;
            return car.Trim() + (hasTable ? " is in Lovely Car Data." : " is not in Lovely Car Data.");
        }

        /// <summary>The car line's ink: the in-use green while the tables have the car, caution while not.</summary>
        public static string CarLineHex(bool hasTable)
        {
            return hasTable ? Theme.StatusUpToDate : Theme.Caution;
        }

        /// <summary>The greyed press on the SimHub device row (#363).</summary>
        public const string SimHubDeviceButton = "Choose a device";

        // The list, as Matrix.dc.html draws it: the heading 8 over the first rule; each layer a rule over a
        // 12-padded line of its number (16 in the display family, 14 wide), 12, its name at 15 and its switch;
        // each option under it indented 46 and padded 7, at 14, its control 16 from its words; a line under an
        // option at 12, 2 below; the Thresholds link 8 before the switch.
        public const double PriorityHeadGap = 8;
        public const double LayerPaddingY = 12;
        public const double LayerGap = 12;
        public const double RankWidth = 14;
        public const double RankSize = 16;
        public const double OptionIndent = 46;
        public const double OptionPaddingY = 7;
        public const double OptionTextSize = 14;
        public const double OptionGap = 16;
        public const double OptionLineSize = 12;
        public const double OptionLineGap = 2;
        public const double ThresholdsGap = 8;

        // --- Search and anchors ------------------------------------------------------------------------

        public const string AnchorProfile = "matrix.profile";
        public const string AnchorPanels = "matrix.panels";
        public const string AnchorPreview = "matrix.preview";
        public const string AnchorPriority = "matrix.priority";
        public const string AnchorFlags = "matrix.flags";
        public const string AnchorPitLane = "matrix.pit-lane";
        public const string AnchorSpotter = "matrix.spotter";
        public const string AnchorSpotterAnimation = "matrix.spotter-animation";
        public const string AnchorWarnings = "matrix.warnings";
        public const string AnchorIdleDisplay = "matrix.idle-display";

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(FlagBoxProfile.ProfileName, PanelPage.Matrix, AnchorProfile, "flag box profile", "install", "reinstall", "matrix profile", "8x8"),
            new PanelSearch.Entry(PanelsTitle, PanelPage.Matrix, AnchorPanels, "8x8", "flag box", "pillar", "panels"),
            new PanelSearch.Entry(AddPanel, PanelPage.Matrix, AnchorPanels, "new", "8x8", "panel"),
            new PanelSearch.Entry(PriorityTitle, PanelPage.Matrix, AnchorPriority, "order", "layers", "takes over"),
            new PanelSearch.Entry(FlagsTitle, PanelPage.Matrix, AnchorFlags, "race flags", "yellow", "blue"),
            new PanelSearch.Entry(CriticalFlagsOnlyTitle, PanelPage.Matrix, AnchorFlags, "chequer", "white", "green"),
            new PanelSearch.Entry(PitLaneTitle, PanelPage.Matrix, AnchorPitLane, "limiter", "speeding", "pit status"),
            new PanelSearch.Entry(SpotterTitle, PanelPage.Matrix, AnchorSpotter, "cars alongside"),
            new PanelSearch.Entry(MountingSideTitle, PanelPage.Matrix, AnchorSpotter, "left", "right", "both"),
            new PanelSearch.Entry(SpotterAnimationTitle, PanelPage.Matrix, AnchorSpotterAnimation, "slide in"),
            new PanelSearch.Entry(WarningsTitle, PanelPage.Matrix, AnchorWarnings, "fuel", "oil", "water", "car warnings"),
            new PanelSearch.Entry(IdleDisplayTitle, PanelPage.Matrix, AnchorIdleDisplay, "at rest", "gear", "dark"),
            new PanelSearch.Entry(ShiftColoursTitle, PanelPage.Matrix, AnchorIdleDisplay, "gear", "revs"),
            new PanelSearch.Entry(CarShiftPointsTitle, PanelPage.Matrix, AnchorIdleDisplay, "lovely", "car data", "car-specific", "thresholds"),
            new PanelSearch.Entry(RedlineFlashTitle, PanelPage.Matrix, AnchorIdleDisplay, "gear", "shift"),
        };

        /// <summary>The greyed rows this page draws (PanelSoon's named entries), which search lists unless one
        /// is InSheetOnly. PanelSoonTests holds the list to this page's own sources: draw a row, add it here.</summary>
        public static readonly SoonItem[] SoonDrawn = { PanelSoon.PriorityOrder, PanelSoon.RpmColourForEverything, PanelSoon.SimHubDevice };

        /// <summary>
        /// Search labels this page draws through something other than the constant, each with the text its
        /// sources draw it by, so the list is a record rather than a way round PanelSearchTests.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            // The profile's line names it as SimHub lists it, FlagBoxName(), whose fallback is this.
            { FlagBoxProfile.ProfileName, "FlagBoxName()" },
        };
    }
}
