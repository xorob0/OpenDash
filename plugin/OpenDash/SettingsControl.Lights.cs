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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>The car tables' button and the line under it. Dropped with the tab, like every other
        /// control here, so a download that finishes after the tab has gone writes nowhere.</summary>
        private Button carTablesButton;

        private TextBlock carTablesLine;

        private FrameworkElement BuildLightsTab()
        {
            var matrices = new List<UIElement>();
            foreach (var matrix in Settings.MatrixPanels()) matrices.Add(BuildMatrixGroup(matrix));

            var box = Ui.Section("The flag box",
                // It names the profile. A panel below carries the name its driver gave it and SimHub's
                // matrix list carries this one, so a page that mentioned neither left somebody looking
                // for their own name on SimHub's Arduino page and finding an OpenDash profile instead.
                Ui.Caption(PanelLights.BoxCaption(FlagBoxName())),
                // Critical flags only, the gear and the two temperature thresholds used to sit here, one
                // value for every panel. They belong to a panel: a rig with a box in each corner wants the
                // catalogue on one and the gear alone on the other, which is what the group below is for.
                // One number under two names. The contract reads LightsLowFuelLaps first and falls back to
                // FlagBoxLowFuelLaps, which is the name that shipped; the field below therefore keeps the
                // old name, because ADR 0003 makes a published property a public interface and a rig set up
                // against rc.2 has to keep the number its driver chose. It stays on the tab rather than
                // moving with the other four: it is the rig's one answer to "am I low", read by the strip
                // and the faces as well, and a per-box copy would be four more places to disagree.
                Ui.Row("Low fuel warning", "Warns below this many laps of fuel.", BuildNumberBox(Settings.FlagBoxLowFuelLaps, 0, 99, v => { Settings.FlagBoxLowFuelLaps = v; Save(); })),
                // Off by default, unlike the flags' own switch on the strips: movement on this box
                // means act, and a car alongside is something you live with for half a straight.
                Ui.Row("Spotter bar animation", "The bar slides in from the edge.", BuildToggle(Settings.FlagBoxSpotterAnimation, on => { Settings.FlagBoxSpotterAnimation = on; Save(); })));

            var panels = Ui.Section(PanelLights.PanelsTitle, Ui.Caption(PanelLights.PanelsCaption));
            var panelRows = (StackPanel)panels.Child;
            if (matrices.Count == 0) panelRows.Children.Add(Ui.Caption(PanelLights.NoPanels));
            foreach (var row in matrices) panelRows.Children.Add(row);
            panelRows.Children.Add(BuildAddMatrixRow());

            var strips = Ui.Section(PanelLights.BarsTitle, Ui.Caption(PanelLights.BarsCaption));
            var stripRows = (StackPanel)strips.Child;
            var bars = Settings.LedBarList();
            if (bars.Count == 0) stripRows.Children.Add(Ui.Caption(PanelLights.NoBars));
            foreach (var bar in bars) stripRows.Children.Add(BuildLedBarGroup(bar, bars[0]));
            stripRows.Children.Add(BuildAddLedBarRow());
            stripRows.Children.Add(Ui.Row("Car shift light width", "Only for the Car-specific rev light style.",
                BuildSegmented(Contract.LedMirrorFits, PanelLights.MirrorFitLabels, Settings.LedMirrorFit,
                    value => { Settings.LedMirrorFit = value; Save(); })));
            stripRows.Children.Add(BuildCarTablesRow());

            // Brightness and night mode are the rig's rather than the box's -- Contract.cs says so in their
            // names -- so they sit under everything a device owns rather than inside the first device that
            // happened to want them.
            var everyLight = Ui.Section(PanelLights.RigWideTitle,
                Ui.Row("Brightness", "SimHub's device brightness applies on top.", BuildPercentBox(Settings.LightsBrightness, v => { Settings.LightsBrightness = v; Save(); })),
                Ui.Row("Night brightness", "Used while night mode is on.", BuildPercentBox(Settings.LightsNightBrightness, v => { Settings.LightsNightBrightness = v; Save(); })),
                Ui.Row("Night mode", null, BuildToggle(Settings.LightsNightMode, on => { Settings.LightsNightMode = on; Save(); })));

            return Ui.VStack(0, box, panels, strips, everyLight);
        }

        /// <summary>
        /// The car light tables: what they are, who measured them, and the button that fetches them.
        /// </summary>
        /// <remarks>
        /// A button rather than a setting, and that is the whole of #366. The tables used to arrive on
        /// their own during startup, weekly, gated by the update-check switch -- so a driver never chose
        /// to fetch them and was never told it had happened, and the reasoning about what is disclosed
        /// lived in a source comment. Two things are wrong with that. The visible one is that "the car's
        /// own" is the default bar style and its fallback is deliberately silent (ADR 0018 part 3), so a
        /// driver whose lights look generic had nothing to read and nothing to press. The other is the
        /// licence: OpenDash carries none of this data, and the copy being the user's own is what makes
        /// that work, which is a great deal truer of a press than of a background thread.
        ///
        /// The attribution stays underneath either way. CC BY-NC-SA 4.0 asks for it, and a driver is
        /// entitled to know whose numbers are lighting their wheel.
        /// </remarks>
        private FrameworkElement BuildCarTablesRow()
        {
            carTablesButton = BuildSecondaryButton(PanelLights.CarTablesButton(plugin.CarLights.CarCount), PanelLights.CarTablesButtonTooltip);
            carTablesButton.Click += (sender, args) => DownloadCarTables();

            carTablesLine = Ui.Caption(string.Empty, BodyWidth);
            var row = Ui.Row(PanelLights.CarTablesTitle, PanelLights.CarTablesCaption, carTablesButton);
            var attribution = Ui.Caption(PanelLights.CarTablesAttribution, BodyWidth);
            RefreshCarTables();
            return Ui.VStack(4, row, carTablesLine, attribution);
        }

        /// <summary>The status line and the button's label, which are one answer and so are written together.</summary>
        private void RefreshCarTables()
        {
            if (carTablesLine == null) return;
            var service = plugin.CarLights;
            var line = service.Status;
            // A copy old enough that upstream has probably moved is mentioned and not acted on: nothing
            // refetches on its own any more, so the invitation is the whole of what staleness now does.
            if (service.Stale) line += " " + PanelLights.CarTablesStale;
            carTablesLine.Text = line;
            if (carTablesButton != null) carTablesButton.Content = PanelLights.CarTablesButton(service.CarCount);
        }

        /// <summary>
        /// The press: fetches, off the interface thread, and says what came back.
        /// </summary>
        /// <remarks>
        /// The answer may land after the tab it was asked from has gone, which is why every control it
        /// writes to is checked first -- the same precaution the update check takes, for the same reason.
        /// </remarks>
        private void DownloadCarTables()
        {
            if (carTablesButton == null) return;
            carTablesButton.IsEnabled = false;
            if (carTablesLine != null) carTablesLine.Text = PanelLights.CarTablesDownloading;

            UpdateService.InBackground(() =>
            {
                plugin.CarLights.Download(DateTime.UtcNow);
                Dispatcher.Invoke(() =>
                {
                    if (carTablesButton != null) carTablesButton.IsEnabled = true;
                    RefreshCarTables();
                });
            }, new SimHubInstallLog());
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
        /// <summary>
        /// The button that adds a panel, and nothing else: the name is asked for on the panel it opens.
        /// </summary>
        /// <remarks>
        /// It disappears when all four slots are taken rather than failing on the press, because SimHub
        /// composes four contents and not five, and a button that cannot work is worse than none.
        /// </remarks>
        private FrameworkElement BuildAddMatrixRow()
        {
            if (Settings.FreeMatrixSlot() == 0)
            {
                var full = Ui.Caption("All four matrix panels are in use.");
                full.Margin = new Thickness(0, 8, 0, 0);
                return full;
            }
            var add = Ui.AddButton(PanelLights.AddPanel, PanelMetrics.RowButtonHeight);
            add.HorizontalAlignment = HorizontalAlignment.Left;
            add.Margin = new Thickness(0, 8, 0, 0);
            add.ToolTip = "Adds a matrix panel.";
            add.Click += (sender, args) => ShowAddMatrixPanel();
            return add;
        }

        /// <summary>Naming the panel, in place, the way a screen is named when it is added.</summary>
        private void ShowAddMatrixPanel()
        {
            var slot = Settings.FreeMatrixSlot();
            if (slot == 0) return;
            var name = new TextBox
            {
                Width = 280,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Text = "Matrix " + slot,
            };
            var add = Ui.OutlineButton(PanelLights.AddPanel, PanelMetrics.RowButtonHeight);
            add.MinWidth = ButtonMinWidth;
            add.Click += (sender, args) =>
            {
                var added = Settings.AddMatrixPanel(name.Text);
                Save();
                Redraw();
                if (added == 0) return;
                // Said for the reason the bar flow says its own line: adding is not the last step, and
                // the step that is left is on SimHub's page rather than this one. Unlike a bar, nothing
                // was installed here -- the flag box profile paints all four panels and is installed
                // once -- so the line names that profile and says whether SimHub has it.
                var state = SafePlan().State;
                AnnounceLights(
                    PanelLights.PanelAdded(Settings.MatrixName(added), added, FlagBoxName(), state),
                    PanelLights.PanelNeedsInstall(state) ? Theme.Caution : Theme.TextSecondary);
            };
            var cancel = Ui.LinkButton("Cancel");
            cancel.Click += (sender, args) => Redraw();
            var nameRow = Ui.Row(PanelLights.PanelNameTitle, PanelLights.PanelNameCaption, name);
            nameRow.HorizontalAlignment = HorizontalAlignment.Stretch;
            bodyHost.Content = Ui.VStack(0, Ui.Section(PanelLights.AddPanel,
                Ui.Caption(PanelLights.AddPanelCaption(slot, FlagBoxName())),
                nameRow,
                Ui.Row(new Border(), Ui.HStack(8, cancel, add))));
        }

        private FrameworkElement BuildMatrixGroup(int matrix)
        {
            var m = matrix;
            var summary = PanelLights.PanelSlot(m);
            return Ui.Collapsible(Settings.MatrixName(m) ?? ("Matrix " + m), summary, Settings.MatrixPanels().First() == m, () =>
            {
                var rest = BuildSegmented(Contract.FlagBoxRests, PanelLights.RestLabels, Settings.MatrixRest(m), value =>
                {
                    // Through the setter rather than into the array, because the deprecated Gear switch
                    // has to move with it or the collapse in Normalise() undoes this on the next load.
                    Settings.SetMatrixRest(m, value);
                    Save();
                });
                var side = BuildSegmented(Contract.FlagBoxSides, PanelLights.SideLabels, Settings.MatrixSide(m), value =>
                {
                    Settings.FlagBoxSide[m - 1] = value;
                    Save();
                });
                return Ui.VStack(4,
                    Ui.Row("Idle display", null, rest),
                    Ui.Row("Race flags", null, BuildToggle(Settings.MatrixFlags(m), on => { Settings.FlagBoxFlags[m - 1] = on; Save(); })),
                    Ui.Row("Pit status", "Limiter, pit lane and speeding.", BuildToggle(Settings.MatrixPit(m), on => { Settings.FlagBoxPit[m - 1] = on; Save(); })),
                    Ui.Row("Spotter", "Warns about cars alongside.", BuildToggle(Settings.MatrixSpotter(m), on => { Settings.FlagBoxSpotter[m - 1] = on; Save(); })),
                    Ui.Row("Car warnings", "Low fuel, oil and water.", BuildToggle(Settings.MatrixWarnings(m), on => { Settings.FlagBoxWarnings[m - 1] = on; Save(); })),
                    // Which side the box is physically on. One to the left of the wheel lighting for a car
                    // on the right is worse than no box at all, so it is asked rather than guessed.
                    Ui.Row("Mounting side", "Only lights for cars on this side.", side),
                    BuildMatrixPanelActions(m),
                    // The four that moved off the tab header, less the gear switch: it asked the same
                    // question as "Idle display" above, whose Dark is the answer that switch called off,
                    // and two controls over one decision is a pair nobody can tell apart.
                    Ui.Row("Critical flags only", "Stays dark for the chequer, white, green and start gantry.", BuildToggle(Settings.MatrixCriticalOnly(m), on => { Settings.FlagBoxMatrixCriticalOnly[m - 1] = on; Save(); })),
                    // Per panel, because a box on the wheel and a box on a monitor stand do not want the
                    // same answer: the one at the edge of vision strobing through the redline is what a
                    // driver who already has a rev bar turns off. Off leaves the digit in the redline
                    // colour, which is still the whole of the message.
                    Ui.Row("Redline flash", "Off keeps the gear steady and red.", BuildToggle(Settings.MatrixGearBlink(m), on => { Settings.FlagBoxMatrixGearBlink[m - 1] = on; Save(); })),
                    Ui.Row("Oil temperature warning", "Warns above this. 0 uses the default (120 C, 248 F).", BuildNumberBox(Settings.MatrixOilTemp(m), 0, 999, v => { Settings.FlagBoxMatrixOilTemp[m - 1] = v; Save(); })),
                    Ui.Row("Water temperature warning", "Warns above this. 0 uses the default (110 C, 230 F).", BuildNumberBox(Settings.MatrixWaterTemp(m), 0, 999, v => { Settings.FlagBoxMatrixWaterTemp[m - 1] = v; Save(); })));
            });
        }
        /// <summary>Renaming a panel and taking it away, at the foot of its own group.</summary>
        private FrameworkElement BuildMatrixPanelActions(int matrix)
        {
            var m = matrix;
            var rename = Ui.LinkButton("Rename");
            rename.ToolTip = "Renames this panel.";
            rename.Click += (sender, args) => ShowRenameMatrixPanel(m);
            var remove = Ui.LinkButton("Remove", Theme.Danger);
            remove.ToolTip = "Removes this panel and frees its slot.";
            remove.Click += (sender, args) =>
            {
                Settings.RemoveMatrixPanel(m);
                Save();
                Redraw();
            };
            var row = Ui.Row(new Border(), Ui.HStack(12, rename, remove));
            row.HorizontalAlignment = HorizontalAlignment.Stretch;
            return row;
        }

        private void ShowRenameMatrixPanel(int matrix)
        {
            var name = new TextBox
            {
                Width = 280,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Text = Settings.MatrixName(matrix) ?? string.Empty,
            };
            var save = Ui.OutlineButton("Rename", PanelMetrics.RowButtonHeight);
            save.MinWidth = ButtonMinWidth;
            save.Click += (sender, args) =>
            {
                Settings.RenameMatrixPanel(matrix, name.Text);
                Save();
                Redraw();
            };
            var cancel = Ui.LinkButton("Cancel");
            cancel.Click += (sender, args) => Redraw();
            var nameRow = Ui.Row(PanelLights.PanelNameTitle, PanelLights.PanelNameCaption, name);
            nameRow.HorizontalAlignment = HorizontalAlignment.Stretch;
            bodyHost.Content = Ui.VStack(0, Ui.Section("Rename " + (Settings.MatrixName(matrix) ?? ("Matrix " + matrix)),
                nameRow,
                Ui.Row(new Border(), Ui.HStack(8, cancel, save))));
        }
        // --- The LED bars ------------------------------------------------------------------------

        /// <summary>One bar: what its middle shows, how its ladder fills, whether its flags move, and the
        /// two actions on the bar itself.</summary>
        private FrameworkElement BuildLedBarGroup(LedBar bar, LedBar first)
        {
            var ns = bar.Namespace;
            return Ui.Collapsible(bar.Name, PanelLightRows.ShapeLabel(bar.Shape), ReferenceEquals(bar, first), () =>
            {
                var centre = BuildChoice(Contract.LedCentres, PanelLights.CentreLabels, Settings.BarCentre(ns), 220,
                    value =>
                    {
                        var live = Settings.LedBarByNamespace(ns);
                        if (live != null) live.Centre = value;
                        Save();
                    });
                var style = BuildChoice(Contract.LedRpmStyles, PanelLights.RpmStyleLabels, Settings.BarRpmStyle(ns), 220,
                    value =>
                    {
                        var live = Settings.LedBarByNamespace(ns);
                        if (live != null) live.RpmStyle = value;
                        Save();
                    });
                return Ui.VStack(4,
                    BuildLedDeviceRow(LedTargets.All(), Settings.BarDevice(ns), value => MoveLedBar(ns, value)),
                    Ui.Row("Centre display", "The LEDs at each end are not affected.", centre),
                    Ui.Row("Rev light style", "Car-specific copies the car you are driving.", style),
                    Ui.Row("Flag animation", "Off shows each flag as a steady colour.",
                        BuildToggle(Settings.BarFlagAnimation(ns), on =>
                        {
                            var live = Settings.LedBarByNamespace(ns);
                            if (live != null) live.FlagAnimation = on;
                            Save();
                        })),
                    // Per bar, because a brow above a monitor has no ends to speak of and a rim does.
                    Ui.Row("Full-strip spotter", "Off lights only the end nearest the car alongside.",
                        BuildToggle(Settings.BarSpotterWhole(ns), on =>
                        {
                            var live = Settings.LedBarByNamespace(ns);
                            if (live != null) live.SpotterWhole = on;
                            Save();
                        })),
                    BuildLedBarActions(ns));
            });
        }

        /// <summary>
        /// The device picker: which of SimHub's LED devices a bar's profile goes to.
        /// </summary>
        /// <remarks>
        /// Three shapes rather than always a drop-down. A rig with one LED device has nothing to choose
        /// and is told where the profile went; a rig with none is told why there is nowhere for it to go,
        /// which is a thing about the rig rather than a failure; and only a rig with two or more is asked.
        ///
        /// A bar pointed at a device SimHub no longer has keeps its own entry at the top of the list,
        /// labelled as gone. Dropping it would silently re-point the bar at whatever sorted first, which
        /// is the class of bug this whole picker exists to close.
        /// </remarks>
        private static FrameworkElement BuildLedDeviceRow(IList<LedTarget> targets, string current, Action<string> chosen)
        {
            if (targets.Count == 0)
            {
                return Ui.Row(PanelLights.BarDeviceTitle, PanelLights.NoDevices, new Border());
            }

            var ids = targets.Select(t => t.Id).ToList();
            var labels = targets.Select(t => t.Connected ? t.Name : t.Name + PanelLights.DeviceOffline).ToList();
            var known = ids.Contains(current, StringComparer.Ordinal);
            if (!known)
            {
                ids.Insert(0, current);
                labels.Insert(0, PanelLights.DeviceGone);
            }

            if (targets.Count == 1 && known)
            {
                return Ui.Row(PanelLights.BarDeviceTitle, PanelLights.OneDevice(labels[0]), new Border());
            }

            var row = Ui.Row(PanelLights.BarDeviceTitle, PanelLights.BarDeviceCaption,
                BuildChoice(ids.ToArray(), labels.ToArray(), current, 260, chosen));
            row.HorizontalAlignment = HorizontalAlignment.Stretch;
            return row;
        }

        /// <summary>
        /// Moves one bar's profile to another of SimHub's LED devices.
        /// </summary>
        /// <remarks>
        /// Installing rather than recording: the profile is the whole point of the bar, and a setting
        /// that said "the wheel" while the profile sat in the Arduino's list would be the reported bug
        /// with a drop-down in front of it. The install takes the copy out of every device first.
        /// </remarks>
        private void MoveLedBar(string ns, string device)
        {
            var bar = Settings.LedBarByNamespace(ns);
            if (bar == null) return;
            bar.Device = LedBar.NormaliseDevice(device);
            Save();
            var found = EmbeddedShapes().FirstOrDefault(entry => string.Equals(entry.Id, bar.Shape, StringComparison.Ordinal));
            if (found == null)
            {
                AnnounceLights(PanelLights.BarAddFailed(bar.Name), Theme.Caution);
                return;
            }
            var plan = InstallBar(bar, found.Json);
            var ok = plan.State == FlagBoxInstallState.UpToDate;
            var target = LedTargets.Find(bar.Device);
            var where = target == null ? "that device" : target.Name;
            var line = ok ? "Moved " + bar.Name + "'s profile to " + where + "." : PanelLights.BarAddFailed(bar.Name);
            if (ok && plan.Note != null) line += " " + plan.Note;
            AnnounceLights(line, ok && plan.Note == null ? Theme.TextSecondary : Theme.Caution);
        }

        private FrameworkElement BuildLedBarActions(string ns)
        {
            var rename = Ui.LinkButton("Rename");
            rename.ToolTip = "Renames this strip. Install it again to rename it in SimHub.";
            rename.Click += (sender, args) => ShowRenameLedBar(ns);
            // "Remove" and not "Remove this bar": a group is indented inside its section and the longer
            // words were cut off at the panel's edge.
            var remove = Ui.LinkButton("Remove", Theme.Danger);
            remove.ToolTip = "Removes this strip and its profile from SimHub.";
            remove.Click += (sender, args) => RemoveLedBar(ns);
            var row = Ui.Row(new Border(), Ui.HStack(12, rename, remove));
            row.HorizontalAlignment = HorizontalAlignment.Stretch;
            return row;
        }

        /// <summary>
        /// Adding a bar: which shape, then what to call it.
        /// </summary>
        /// <remarks>
        /// The shapes are read out of the assembly rather than listed here, exactly as the Install tab's
        /// rows are: the census is what this build embedded, so a shape it does not carry is not offered
        /// and cannot be added as a bar whose profile does not exist.
        /// </remarks>
        private void ShowAddLedBar()
        {
            var shapes = EmbeddedShapes();
            if (shapes.Count == 0)
            {
                bodyHost.Content = Ui.VStack(0, Ui.Section(PanelLights.AddBar,
                    Ui.Caption("This build ships no strip profiles."),
                    BackRow()));
                return;
            }

            // Two numbers rather than a list of sixty-three. A driver knows how many LEDs their strip
            // has and how they are grouped, which is exactly A and B; a drop-down asked them to find
            // "3/9/3" among every other geometry and to know that is what their wheel is called.
            var sides = shapes.Select(entry => entry.Side).Distinct().OrderBy(n => n).ToArray();
            var side = sides.Contains(3) ? 3 : sides[0];
            var centres = shapes.Where(e => e.Side == side).Select(e => e.Centre).OrderBy(n => n).ToArray();
            var centre = centres.Contains(9) ? 9 : centres[0];

            var note = Ui.Caption(string.Empty);
            var name = new TextBox
            {
                Width = 280,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            var typed = false;
            name.TextChanged += (sender, args) => typed = name.IsKeyboardFocusWithin;
            var centreHost = new ContentControl { HorizontalAlignment = HorizontalAlignment.Right };

            Action refresh = () =>
            {
                note.Text = PanelLights.BarShapeNote(side, centre);
                if (!typed) name.Text = DefaultBarName(PanelLights.BarShapeId(side, centre));
            };
            Action showCentres = () =>
            {
                centres = shapes.Where(e => e.Side == side).Select(e => e.Centre).OrderBy(n => n).ToArray();
                // A side of none reaches twenty-five and a side of four stops at twelve, so the choice
                // of centre follows the choice of ends rather than offering lengths nothing is built for.
                if (!centres.Contains(centre)) centre = centres.Contains(9) ? 9 : centres[0];
                centreHost.Content = BuildChoice(
                    centres.Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray(),
                    centres.Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray(),
                    centre.ToString(CultureInfo.InvariantCulture),
                    120,
                    value =>
                    {
                        centre = int.Parse(value, CultureInfo.InvariantCulture);
                        refresh();
                    });
                refresh();
            };

            // Which device gets the profile. SimHub keeps one profile list per LED device, so this is
            // not a detail: a bar installed into the wrong one is written, saved and verified correctly
            // into a list the hardware does not read, which is exactly what a rig reported.
            var targets = LedTargets.All();
            var preferred = LedTargets.Preferred();
            var device = preferred == null ? LedBar.ArduinoDevice : preferred.Id;
            var deviceRow = BuildLedDeviceRow(targets, device, value => device = value);

            var endsRow = Ui.Row(PanelLights.BarEndsTitle, PanelLights.BarEndsCaption,
                BuildSegmented(
                    sides.Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray(),
                    sides.Select(n => n == 0 ? "None" : n.ToString(CultureInfo.InvariantCulture)).ToArray(),
                    side.ToString(CultureInfo.InvariantCulture),
                    value =>
                    {
                        side = int.Parse(value, CultureInfo.InvariantCulture);
                        showCentres();
                    }));
            endsRow.HorizontalAlignment = HorizontalAlignment.Stretch;
            var centreRow = Ui.Row(PanelLights.BarCentreTitle, PanelLights.BarCentreCaption, centreHost);
            centreRow.HorizontalAlignment = HorizontalAlignment.Stretch;
            var nameRow = Ui.Row(PanelLights.BarNameTitle, PanelLights.BarNameCaption, name);
            nameRow.HorizontalAlignment = HorizontalAlignment.Stretch;
            showCentres();

            var add = Ui.OutlineButton(PanelLights.AddBar, PanelMetrics.RowButtonHeight);
            add.MinWidth = ButtonMinWidth;
            add.Click += (sender, args) => AddLedBar(PanelLights.BarShapeId(side, centre), name.Text, device, shapes);
            var cancel = Ui.LinkButton("Cancel");
            cancel.Click += (sender, args) => Redraw();

            bodyHost.Content = Ui.VStack(0, Ui.Section(PanelLights.AddBar, endsRow, centreRow, note, nameRow, deviceRow,
                Ui.Row(new Border(), Ui.HStack(8, cancel, add))));
        }

        private FrameworkElement BuildAddLedBarRow()
        {
            var add = Ui.AddButton(PanelLights.AddBar, PanelMetrics.RowButtonHeight);
            add.HorizontalAlignment = HorizontalAlignment.Left;
            add.Margin = new Thickness(0, 8, 0, 0);
            add.ToolTip = "Adds a strip and installs its profile.";
            add.Click += (sender, args) => ShowAddLedBar();
            return add;
        }

        /// <summary>What the name box opens on: the name the build gave the profile for that shape, so
        /// the row in SimHub's own LED profile list says whose it is rather than reading as a bare
        /// geometry among everybody else's profiles.</summary>
        private static string DefaultBarName(string shape)
        {
            return FlagBoxProfile.FilePrefix + PanelLightRows.ShapeLabel(shape);
        }

        /// <summary>One shape this build embedded: its geometry, and the profile written for it.</summary>
        private sealed class EmbeddedShape
        {
            public EmbeddedShape(string id, int side, int centre, string json)
            {
                Id = id;
                Side = side;
                Centre = centre;
                Json = json;
            }

            public string Id { get; private set; }
            public int Side { get; private set; }
            public int Centre { get; private set; }
            public string Json { get; private set; }
        }

        /// <summary>
        /// Every A/B/A shape this build embedded, read back as the two numbers it was generated from.
        /// </summary>
        /// <remarks>
        /// The census is what is embedded, which is the rule the Install tab's rows already follow: a
        /// shape this build does not carry is not offered and cannot be added as a bar whose profile does
        /// not exist. The wirings are left out -- a reversed or Fanatec profile is the same geometry
        /// wired another way and has no place in a question about how many LEDs there are.
        /// </remarks>
        private static IList<EmbeddedShape> EmbeddedShapes()
        {
            var log = new SimHubInstallLog();
            var assembly = typeof(OpenDash).Assembly;
            var shapes = new List<EmbeddedShape>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var resource in FlagBoxProfile.StripResourceNames(assembly))
            {
                var id = FlagBoxProfile.ShapeIdOf(resource);
                if (id == null || !seen.Add(id)) continue;
                var geometry = LightShape.Parse(id);
                if (geometry == null || geometry.Wiring != null || geometry.Left != geometry.Right) continue;
                var text = FlagBoxProfile.ResourceText(assembly, resource, log);
                if (text == null) continue;
                shapes.Add(new EmbeddedShape(id, geometry.Left, geometry.Centre, text));
            }
            return shapes;
        }

        private void AddLedBar(string shape, string name, string device, IList<EmbeddedShape> shapes)
        {
            var bar = Settings.AddLedBar(shape, name, device);
            Save();
            var found = shapes.FirstOrDefault(entry => string.Equals(entry.Id, shape, StringComparison.Ordinal));
            var embedded = found == null ? null : found.Json;
            var ok = embedded != null;
            string note = null;
            if (ok)
            {
                var plan = InstallBar(bar, embedded);
                ok = plan.State == FlagBoxInstallState.UpToDate;
                note = plan.Note;
            }
            Redraw();
            // A note is not a failure, so the line stays the ordinary one and gains a sentence. The one
            // note there is says the device is listing its maker's built-in profiles, which is the only
            // way an install can be correct and still leave nothing for the driver to select.
            var target = LedTargets.Find(bar.Device);
            var line = ok ? PanelLights.BarAdded(bar.Name, target == null ? null : target.Name) : PanelLights.BarAddFailed(bar.Name);
            if (ok && note != null) line += " " + note;
            AnnounceLights(line, ok && note == null ? Theme.TextSecondary : Theme.Caution);
        }

        private static FlagBoxPlan InstallBar(LedBar bar, string embedded)
        {
            try
            {
                // Out of wherever it was first. A bar that has moved from the Arduino to a wheel must not
                // leave a copy behind reading properties that now drive the wheel's, and the install only
                // knows about the device it is going to.
                StripInstaller.UninstallEverywhere(LedBarProfile.IdFor(bar.Namespace));
                return StripInstaller.Install(LedBarProfile.For(bar, embedded), bar.Device);
            }
            catch (Exception ex)
            {
                Log.Error("Installing the profile for " + bar.Name + " failed", ex);
                return new FlagBoxPlan { State = FlagBoxInstallState.Failed };
            }
        }

        private void RemoveLedBar(string ns)
        {
            var bar = Settings.LedBarByNamespace(ns);
            if (bar == null) return;
            var name = bar.Name;
            try
            {
                // Everywhere, not just the device the bar names: the device it was installed into may
                // have been removed from SimHub since, and a profile nothing attaches settings to any
                // more is a row in somebody's list that lights nothing.
                StripInstaller.UninstallEverywhere(LedBarProfile.IdFor(ns));
            }
            catch (Exception ex)
            {
                Log.Warn("The profile for " + name + " could not be taken out of SimHub: " + ex.Message);
            }
            Settings.RemoveLedBar(ns);
            Save();
            Redraw();
            AnnounceLights("Removed " + name + " and its profile.", Theme.TextSecondary);
        }

        private void ShowRenameLedBar(string ns)
        {
            var bar = Settings.LedBarByNamespace(ns);
            if (bar == null) return;
            var name = new TextBox
            {
                Width = 280,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Text = bar.Name,
            };
            var save = Ui.OutlineButton("Rename", PanelMetrics.RowButtonHeight);
            save.MinWidth = ButtonMinWidth;
            save.Click += (sender, args) =>
            {
                Settings.RenameLedBar(ns, name.Text);
                Save();
                Redraw();
            };
            var cancel = Ui.LinkButton("Cancel");
            cancel.Click += (sender, args) => Redraw();
            var nameRow = Ui.Row(PanelLights.BarNameTitle, PanelLights.BarNameCaption, name);
            nameRow.HorizontalAlignment = HorizontalAlignment.Stretch;
            bodyHost.Content = Ui.VStack(0, Ui.Section("Rename " + bar.Name,
                nameRow,
                Ui.Caption("Install the strip again to rename it in SimHub."),
                Ui.Row(new Border(), Ui.HStack(8, cancel, save))));
        }

        /// <summary>What SimHub's own list calls OpenDash's matrix profile: the name the build stamped
        /// into it, and the constant only when this build carries no profile to read it from. One
        /// answer, because two places spelling it differently is the confusion this fixes.</summary>
        private string FlagBoxName()
        {
            return plugin.FlagBox?.ProfileName ?? FlagBoxProfile.ProfileName;
        }

        /// <summary>A line at the top of the tab saying what just happened, until the next thing happens.</summary>
        private void AnnounceLights(string line, string colour)
        {
            var stack = bodyHost.Content as StackPanel;
            var section = stack?.Children.Count > 0 ? stack.Children[0] as Border : null;
            var rows = section?.Child as StackPanel;
            if (rows == null) return;
            var text = Ui.Text(line, Theme.SizeSmall, System.Windows.FontWeights.Normal, colour);
            text.TextWrapping = TextWrapping.Wrap;
            text.MaxWidth = BodyWidth;
            text.Margin = new Thickness(0, 8, 0, 0);
            rows.Children.Insert(Math.Min(1, rows.Children.Count), text);
        }
    }
}
