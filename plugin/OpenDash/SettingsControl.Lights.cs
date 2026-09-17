// SettingsControl.Lights.cs: the Lights tab -- the flag box, the four matrix contents, the strips, and
// the three settings that answer for every light on the rig rather than for one device.
//
// A tab rather than a card in the rig's row. XOR-231 settles that a light device "is the same shape of
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
                Ui.Row("Critical flags only", "Quiet until something matters: drops the chequer, the white, the green and the start gantry.", BuildToggle(Settings.FlagBoxCriticalOnly, on => { Settings.FlagBoxCriticalOnly = on; Save(); })),
                Ui.Row("Show the gear", "What the box shows when nothing else is on it. Off leaves it dark.", BuildToggle(Settings.FlagBoxGear, on => { Settings.FlagBoxGear = on; Save(); })),
                // One number under two names. The contract reads LightsLowFuelLaps first and falls back to
                // FlagBoxLowFuelLaps, which is the name that shipped; the field below therefore keeps the
                // old name, because ADR 0003 makes a published property a public interface and a rig set up
                // against rc.2 has to keep the number its driver chose.
                Ui.Row("Low fuel, laps", "One answer for every light: the box, the screens' fuel telltale and the pop-up all light when the laps left in the tank fall under this. Laps, not litres: litres mean nothing without the car.", BuildNumberBox(Settings.FlagBoxLowFuelLaps, 0, 99, v => { Settings.FlagBoxLowFuelLaps = v; Save(); })),
                Ui.Row("Oil temperature", "In your own unit; 0 uses the default for it (120 C, 248 F).", BuildNumberBox(Settings.FlagBoxOilTemp, 0, 999, v => { Settings.FlagBoxOilTemp = v; Save(); })),
                Ui.Row("Water temperature", "In your own unit; 0 uses the default for it (110 C, 230 F).", BuildNumberBox(Settings.FlagBoxWaterTemp, 0, 999, v => { Settings.FlagBoxWaterTemp = v; Save(); })));

            var panels = Ui.Section("What each panel does",
                Ui.Caption(
                    "SimHub composes up to four matrix contents. Matrix 1 does everything by default; open a second "
                        + "only if you own a second box."));
            var panelRows = (StackPanel)panels.Child;
            foreach (var row in matrices) panelRows.Children.Add(row);

            var strips = Ui.Section("The strips",
                Ui.Caption("An RGB LED strip across the wheel or the rim. Install the profile that matches your strip from the Install tab, then these three decide what it shows."),
                Ui.Row("Strip centre", "What the middle of the strip shows. RPM is the one that keeps the brake on the sides.",
                    BuildChoice(Contract.LedCentres, PanelLights.CentreLabels, Settings.LedCentre, 220,
                        value => { Settings.LedCentre = value; Save(); })),
                Ui.Row("Rev style", "How the ladder fills. Meet in middle works inwards from both ends; F1 is a formula wheel's colours, and flashes whole.",
                    BuildSegmented(Contract.LedRpmStyles, PanelLights.RpmStyleLabels, Settings.LedRpmStyle,
                        value => { Settings.LedRpmStyle = value; Save(); })),
                // A switch rather than a rate: how fast a flag blinks is the standard's decision, and the
                // driver who asks for this is asking for a rim that stops moving rather than a slower one.
                Ui.Row("Flag animation", "On, a flag moves, which is what the corner of your eye reads it by. Off holds every flag from the frame it would have settled on and never turns one off.",
                    BuildToggle(Settings.LedFlagAnimation, on => { Settings.LedFlagAnimation = on; Save(); })));

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
                    Ui.Row("Mounted", "Which side of the rig this box is on. A left box must not light for a car on your right.", side));
            });
        }
    }
}
