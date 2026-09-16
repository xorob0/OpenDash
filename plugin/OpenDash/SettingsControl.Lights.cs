// SettingsControl.Lights.cs: the Lights tab -- the flag box, the four matrix contents and the strips.
//
// A tab rather than a card in the rig's row. #282 settles that a light device "is the same shape of
// thing as a screen", which is true of the settings model and not of a user: a screen is a rectangle
// with zones, a box is 64 LEDs with a mounting side, and one row of cards mixing them would have to
// explain itself. docs/design/plugin.md records the divergence from the canvas.
//
// ADR 0013 is why the page exists; docs/design/flag-box.md is what the box draws.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private TextBlock flagBoxLine;
        private Button flagBoxButton;
        private Button flagBoxCopyButton;
        private TextBox flagBoxPath;

        private FrameworkElement BuildLightsTab()
        {
            var matrices = new List<UIElement>();
            foreach (var matrix in Contract.FlagBoxMatrices) matrices.Add(BuildMatrixGroup(matrix));

            var box = Ui.Section("The flag box",
                Ui.Caption(
                    "An 8x8 LED matrix beside the screen. Install the profile below, then select it on your matrix "
                        + "device; everything on this page then reaches it while you drive. openDash adds the profile "
                        + "through SimHub's own settings and never touches a profile you made yourself.",
                    BodyWidth),
                BuildFlagBoxRow(),
                Ui.Row("Brightness", "Percent, for every light openDash drives. SimHub's own device brightness applies on top.", BuildPercentBox(Settings.LightsBrightness, v => { Settings.LightsBrightness = v; Save(); })),
                Ui.Row("Night brightness", "Used while night mode is on. 64 LEDs at full output beside a wheel in a dark room is too bright.", BuildPercentBox(Settings.LightsNightBrightness, v => { Settings.LightsNightBrightness = v; Save(); })),
                Ui.Row("Night mode", "A switch you flip, not a time of day we guess at.", BuildToggle(Settings.LightsNightMode, on => { Settings.LightsNightMode = on; Save(); })),
                Ui.Row("Critical flags only", "Quiet until something matters: drops the chequer, the white, the green and the start gantry.", BuildToggle(Settings.FlagBoxCriticalOnly, on => { Settings.FlagBoxCriticalOnly = on; Save(); })),
                Ui.Row("Show the gear", "What the box shows when nothing else is on it. Off leaves it dark.", BuildToggle(Settings.FlagBoxGear, on => { Settings.FlagBoxGear = on; Save(); })),
                Ui.Row("Low fuel, laps", "Lights when the laps left in the tank fall under this. Laps, not litres: litres mean nothing without the car.", BuildNumberBox(Settings.FlagBoxLowFuelLaps, 0, 99, v => { Settings.FlagBoxLowFuelLaps = v; Save(); })),
                Ui.Row("Oil temperature", "In your own unit; 0 uses the default for it (120 C, 248 F).", BuildNumberBox(Settings.FlagBoxOilTemp, 0, 999, v => { Settings.FlagBoxOilTemp = v; Save(); })),
                Ui.Row("Water temperature", "In your own unit; 0 uses the default for it (110 C, 230 F).", BuildNumberBox(Settings.FlagBoxWaterTemp, 0, 999, v => { Settings.FlagBoxWaterTemp = v; Save(); })));

            var panels = Ui.Section("What each panel does",
                Ui.Caption(
                    "SimHub composes up to four matrix contents. Matrix 1 does everything by default; open a second "
                        + "only if you own a second box.",
                    BodyWidth));
            var panelRows = (StackPanel)panels.Child;
            foreach (var row in matrices) panelRows.Children.Add(row);

            // The attribution row is not decoration. The car tables are somebody else's work under
            // CC BY-NC-SA 4.0 (ADR 0018), openDash ships none of them, and a user is entitled to know
            // whose numbers are lighting their wheel.
            var strips = Ui.Section("The strips",
                Ui.Caption("An RGB LED strip across the wheel or the rim. Install the profile that matches your strip, then these decide what it shows.", BodyWidth),
                Ui.Row("Strip centre", "What the middle of the strip shows. RPM keeps the brake on the sides; RPM only leaves them dark.",
                    BuildChoice(Contract.LedCentres, new[] { "RPM", "RPM only", "Brake", "Throttle and brake", "Fuel" }, Settings.LedCentre, 220,
                        value => { Settings.LedCentre = value; Save(); })),
                // A drop-down rather than a segmented bar, now there are four: the car's own bar, and
                // openDash's three looks.
                Ui.Row("Rev style", "The car's own mirrors the shift lights in the car you are driving: its LEDs, its colours, its order, its flash, in the gear you are in. The other three are openDash's own looks, and are what a car we have no measurements for shows.",
                    BuildChoice(Contract.LedRpmStyles, new[] { "The car's own", "Left to right", "Meet in middle", "F1" }, Settings.LedRpmStyle, 220,
                        value => { Settings.LedRpmStyle = value; Save(); })),
                Ui.Row("Car bar size", "Only for the car's own. Fill the strip spreads the car's lights over every LED; true size draws them at their own length in the middle.",
                    BuildSegmented(Contract.LedMirrorFits, new[] { "Fill the strip", "True size" }, Settings.LedMirrorFit,
                        value => { Settings.LedMirrorFit = value; Save(); })),
                Ui.Caption(
                    CarLightLibrary.Attribution + " openDash ships none of it: the tables are fetched when update checks are on, "
                        + "and every car works offline afterwards. " + CarLightLibrary.ProjectUrl,
                    BodyWidth));

            return Ui.VStack(0, box, panels, strips);
        }

        /// <summary>
        /// One matrix: what it shows at rest, what may take it over, and which side it is on.
        /// </summary>
        /// <remarks>
        /// Matrix 1 is open and 2 to 4 are shut, because four groups of six rows is twenty-four rows of
        /// settings for hardware almost nobody owns. They are kept rather than dropped: somebody does own
        /// two boxes, one in each corner of a monitor stand, and that rig has to be configurable. This is
        /// the "less often used, but kept" rule applied where it costs the most scroll.
        /// </remarks>
        private FrameworkElement BuildMatrixGroup(int matrix)
        {
            var m = matrix;
            var summary = m == 1 ? null : "off by default";
            return Ui.Collapsible("Matrix " + m, summary, m == 1, () =>
            {
                var rest = BuildSegmented(Contract.FlagBoxRests, new[] { "Dark", "Gear" }, Settings.MatrixRest(m), value =>
                {
                    Settings.FlagBoxRest[m - 1] = value;
                    Save();
                });
                var side = BuildSegmented(Contract.FlagBoxSides, new[] { "Both", "Left", "Right" }, Settings.MatrixSide(m), value =>
                {
                    Settings.FlagBoxSide[m - 1] = value;
                    Save();
                });
                return Ui.VStack(4,
                    Ui.Row("At rest", "What this panel shows when nothing has taken it over.", rest),
                    Ui.Row("Flags", "Let the flag catalogue take this panel.", BuildToggle(Settings.MatrixFlags(m), on => { Settings.FlagBoxFlags[m - 1] = on; Save(); })),
                    Ui.Row("Pit", "Let the limiter, the lane and speeding take this panel.", BuildToggle(Settings.MatrixPit(m), on => { Settings.FlagBoxPit[m - 1] = on; Save(); })),
                    Ui.Row("Spotter", "Let a car alongside take this panel.", BuildToggle(Settings.MatrixSpotter(m), on => { Settings.FlagBoxSpotter[m - 1] = on; Save(); })),
                    Ui.Row("Warnings", "Let low fuel, oil and water take this panel.", BuildToggle(Settings.MatrixWarnings(m), on => { Settings.FlagBoxWarnings[m - 1] = on; Save(); })),
                    // Which side the box is physically on. One to the left of the wheel lighting for a car
                    // on the right is worse than no box at all, so it is asked rather than guessed.
                    Ui.Row("Mounted", "Which side of the rig this box is on. A left box must not light for a car on your right.", side));
            });
        }

        /// <summary>
        /// The install row: what SimHub holds now, and one button that changes it.
        ///
        /// openDash hands SimHub a profile object through its own public API and SimHub writes its own
        /// settings file (FlagBoxInstaller.cs); nothing here edits that file. It is a button rather
        /// than something that happens at startup because a profile paints hardware the user owns, and
        /// that is a thing to be asked about once rather than assumed -- ADR 0013.
        /// </summary>
        private FrameworkElement BuildFlagBoxRow()
        {
            flagBoxLine = Ui.Caption("", 460);
            flagBoxButton = BuildSecondaryButton("Install into SimHub", "Adds openDash's profile to SimHub's matrix profiles. It never changes a profile you made yourself.");
            flagBoxButton.Click += (sender, args) => InstallFlagBox();

            var text = Ui.VStack(4, Ui.Body("Flag box profile"), flagBoxLine);
            text.MaxWidth = 460;
            text.HorizontalAlignment = HorizontalAlignment.Left;
            flagBoxCopyButton = BuildSecondaryButton("Copy where SimHub looks", "Puts a copy in Documents\\SimHub, which is the folder SimHub's own profile import opens in.");
            flagBoxCopyButton.Click += (sender, args) => CopyFlagBoxForImport();
            var right = Ui.VStack(4, flagBoxButton, flagBoxCopyButton, FlagBoxPathBox());
            RefreshFlagBox();
            return Ui.Row(text, right);
        }

        /// <summary>Re-reads SimHub's matrix profiles and repaints the row.</summary>
        private void RefreshFlagBox()
        {
            if (flagBoxLine == null) return;
            var plan = SafePlan();
            flagBoxLine.Text = FlagBoxInstallPlan.Summary(plan, plugin.FlagBox?.Path);
            if (flagBoxButton == null) return;
            flagBoxButton.Content = FlagBoxInstallPlan.ButtonLabel(plan);
            // Nothing to press when there is no profile to install or nowhere to put it.
            var usable = plan != null && plan.State != FlagBoxInstallState.NotEmbedded && plan.State != FlagBoxInstallState.Unavailable;
            flagBoxButton.IsEnabled = usable;
            // The by-hand route, offered only when the one-click one is not there. SimHub's import
            // dialog opens in Documents\SimHub, which is not where the profile was written.
            if (flagBoxCopyButton != null)
            {
                flagBoxCopyButton.Visibility = plan != null && plan.State == FlagBoxInstallState.Unavailable
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void CopyFlagBoxForImport()
        {
            var copied = FlagBoxProfile.CopyForImport(plugin.FlagBox, null, new SimHubInstallLog());
            if (flagBoxPath != null && copied?.Path != null) flagBoxPath.Text = copied.Path;
            if (flagBoxLine == null) return;
            flagBoxLine.Text = copied != null && copied.Status == FlagBoxStatus.Failed
                ? "Could not copy the profile: " + copied.Message
                : "Copied to " + copied?.Path + ". In SimHub, open your matrix device's profiles and press Import; "
                    + "the dialog opens in that folder.";
        }

        private FlagBoxPlan SafePlan()
        {
            try
            {
                return FlagBoxInstaller.Plan(plugin.FlagBoxJson);
            }
            catch (Exception ex)
            {
                Log.Warn("Reading SimHub's matrix profiles failed: " + ex.Message);
                return new FlagBoxPlan { State = FlagBoxInstallState.Unavailable };
            }
        }

        private void InstallFlagBox()
        {
            try
            {
                FlagBoxInstaller.Install(plugin.FlagBoxJson);
            }
            catch (Exception ex)
            {
                Log.Error("Installing the flag box profile failed", ex);
            }
            RefreshFlagBox();
        }

        private FrameworkElement FlagBoxPathBox()
        {
            var box = new TextBox
            {
                Width = 320,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsReadOnly = true,
                Text = plugin.FlagBox?.Path ?? string.Empty,
                ToolTip = "Where openDash left the profile.",
            };
            flagBoxPath = box;
            return box;
        }
    }
}
