// SettingsControl.Lights.cs: the Lights tab -- the flag box, the four matrix contents, the strips, and
// the three settings that answer for every light on the rig rather than for one device.
//
// A tab rather than a card in the rig's row. #282 settles that a light device "is the same shape of
// thing as a screen", which is true of the settings model and not of a user: a screen is a rectangle
// with zones, a box is 64 LEDs with a mounting side, and one row of cards mixing them would have to
// explain itself. docs/design/plugin.md records the divergence from the canvas.
//
// The install row is NOT here. The twenty light profiles are one list and they are installed from the
// Install tab (SettingsControl.Install.Lights.cs), beside the packages: this tab is where a driver sets
// what a light already installed shows, and the two questions were never the same one.
//
// ADR 0013 is why the page exists; docs/design/flag-box.md is what the box draws.
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private FrameworkElement BuildLightsTab()
        {
            var matrices = new List<UIElement>();
            foreach (var matrix in Contract.FlagBoxMatrices) matrices.Add(BuildMatrixGroup(matrix));

            var box = Ui.Section("The flag box",
                Ui.Caption(
                    "An 8x8 LED matrix beside the screen. Install its profile from the Install tab, then select it on "
                        + "your matrix device; everything on this page then reaches it while you drive. openDash adds "
                        + "the profile through SimHub's own settings and never touches a profile you made yourself."),
                // Critical flags only, the gear and the two temperature thresholds used to sit here, one
                // value for every panel. They belong to a panel: a rig with a box in each corner wants the
                // catalogue on one and the gear alone on the other, which is what the group below is for.
                // One number under two names. The contract reads LightsLowFuelLaps first and falls back to
                // FlagBoxLowFuelLaps, which is the name that shipped; the field below therefore keeps the
                // old name, because ADR 0003 makes a published property a public interface and a rig set up
                // against rc.2 has to keep the number its driver chose. It stays on the tab rather than
                // moving with the other four: it is the rig's one answer to "am I low", read by the strip
                // and the faces as well, and a per-box copy would be four more places to disagree.
                Ui.Row("Low fuel, laps", "One answer for every light: the box, the screens' fuel telltale and the pop-up all light when the laps left in the tank fall under this. Laps, not litres: litres mean nothing without the car.", BuildNumberBox(Settings.FlagBoxLowFuelLaps, 0, 99, v => { Settings.FlagBoxLowFuelLaps = v; Save(); })),
                // Off by default, unlike the flags' own switch on the strips: movement on this box
                // means act, and a car alongside is something you live with for half a straight.
                Ui.Row("Spotter bar grows", "On, the bar grows inwards from the edge; off it is simply there. The bar is painted over whatever else is on the panel either way, so a flag stays readable under it.", BuildToggle(Settings.FlagBoxSpotterAnimation, on => { Settings.FlagBoxSpotterAnimation = on; Save(); })));

            var panels = Ui.Section("What each panel does",
                Ui.Caption(
                    "SimHub composes up to four matrix contents. Matrix 1 does everything by default; open a second "
                        + "only if you own a second box."));
            var panelRows = (StackPanel)panels.Child;
            foreach (var row in matrices) panelRows.Children.Add(row);

            // The attribution row is not decoration. The car tables are somebody else's work under
            // CC BY-NC-SA 4.0 (ADR 0018), openDash ships none of them, and a user is entitled to know
            // whose numbers are lighting their wheel.
            var strips = Ui.Section("The strips",
                Ui.Caption("An RGB LED strip across the wheel or the rim. Install the profile that matches your strip from the Install tab, then these decide what it shows."),
                Ui.Row("Strip centre", "What the middle of the strip shows. The LEDs at the ends are lamps and are not affected by it.",
                    BuildChoice(Contract.LedCentres, PanelLights.CentreLabels, Settings.LedCentre, 220,
                        value => { Settings.LedCentre = value; Save(); })),
                // A drop-down rather than a segmented bar, now there are four: the car's own bar, and
                // openDash's three looks.
                Ui.Row("Rev style", "The car's own mirrors the shift lights in the car you are driving: its LEDs, its colours, its order, its flash, in the gear you are in. The other three are openDash's own looks, and are what a car we have no measurements for shows.",
                    BuildChoice(Contract.LedRpmStyles, PanelLights.RpmStyleLabels, Settings.LedRpmStyle, 220,
                        value => { Settings.LedRpmStyle = value; Save(); })),
                Ui.Row("Car bar size", "Only for the car's own. Fill the strip spreads the car's lights over every LED; true size draws them at their own length in the middle.",
                    BuildSegmented(Contract.LedMirrorFits, PanelLights.MirrorFitLabels, Settings.LedMirrorFit,
                        value => { Settings.LedMirrorFit = value; Save(); })),
                // A switch rather than a rate: how fast a flag blinks is the standard's decision, and the
                // driver who asks for this is asking for a rim that stops moving rather than a slower one.
                Ui.Row("Flag animation", "On, a flag moves, which is what the corner of your eye reads it by. Off holds every flag from the frame it would have settled on and never turns one off.",
                    BuildToggle(Settings.LedFlagAnimation, on => { Settings.LedFlagAnimation = on; Save(); })),
                Ui.Caption(
                    CarLightLibrary.Attribution + " openDash ships none of it: the tables are fetched when update checks are on, "
                        + "and every car works offline afterwards. " + CarLightLibrary.ProjectUrl,
                    BodyWidth));

            // Brightness and night mode are the rig's rather than the box's -- Contract.cs says so in their
            // names -- so they sit under everything a device owns rather than inside the first device that
            // happened to want them.
            var everyLight = Ui.Section(PanelLights.RigWideTitle,
                Ui.Caption(PanelLights.RigWideCaption),
                Ui.Row("Brightness", "Percent, for every light openDash drives. SimHub's own device brightness applies on top.", BuildPercentBox(Settings.LightsBrightness, v => { Settings.LightsBrightness = v; Save(); })),
                Ui.Row("Night brightness", "Used while night mode is on. 64 LEDs at full output beside a wheel in a dark room is too bright.", BuildPercentBox(Settings.LightsNightBrightness, v => { Settings.LightsNightBrightness = v; Save(); })),
                Ui.Row("Night mode", "A switch you flip, not a time of day we guess at.", BuildToggle(Settings.LightsNightMode, on => { Settings.LightsNightMode = on; Save(); })));

            return Ui.VStack(0, box, panels, strips, everyLight);
        }

        /// <summary>
        /// One matrix: what it shows at rest, what may take it over, and which side it is on.
        /// </summary>
        /// <remarks>
        /// Matrix 1 is open and 2 to 4 are shut, because four groups of ten rows is forty rows of
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
                var rest = BuildSegmented(Contract.FlagBoxRests, PanelLights.RestLabels, Settings.MatrixRest(m), value =>
                {
                    Settings.FlagBoxRest[m - 1] = value;
                    Save();
                });
                var side = BuildSegmented(Contract.FlagBoxSides, PanelLights.SideLabels, Settings.MatrixSide(m), value =>
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
                    Ui.Row("Mounted", "Which side of the rig this box is on. A left box must not light for a car on your right.", side),
                    // The four that moved off the tab header. They read as this panel's own rather than as
                    // the rig's, which is what they had become by sitting above every panel at once.
                    Ui.Row("Critical flags only", "Quiet until something matters: drops the chequer, the white, the green and the start gantry.", BuildToggle(Settings.MatrixCriticalOnly(m), on => { Settings.FlagBoxMatrixCriticalOnly[m - 1] = on; Save(); })),
                    Ui.Row("Show the gear", "What this panel shows when nothing else is on it. Off leaves it dark.", BuildToggle(Settings.MatrixGear(m), on => { Settings.FlagBoxMatrixGear[m - 1] = on; Save(); })),
                    // Per panel, because a box on the wheel and a box on a monitor stand do not want the
                    // same answer: the one at the edge of vision strobing through the redline is what a
                    // driver who already has a rev bar turns off. Off leaves the digit in the redline
                    // colour, which is still the whole of the message.
                    Ui.Row("Flash the gear at the redline", "The digit blinks while the engine is over-revving. Off keeps it steady and red.", BuildToggle(Settings.MatrixGearBlink(m), on => { Settings.FlagBoxMatrixGearBlink[m - 1] = on; Save(); })),
                    Ui.Row("Oil temperature", "In your own unit; 0 uses the default for it (120 C, 248 F).", BuildNumberBox(Settings.MatrixOilTemp(m), 0, 999, v => { Settings.FlagBoxMatrixOilTemp[m - 1] = v; Save(); })),
                    Ui.Row("Water temperature", "In your own unit; 0 uses the default for it (110 C, 230 F).", BuildNumberBox(Settings.MatrixWaterTemp(m), 0, 999, v => { Settings.FlagBoxMatrixWaterTemp[m - 1] = v; Save(); })));
            });
        }
    }
}
