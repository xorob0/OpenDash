// SettingsControl.Lights.cs: the LEDs page -- one group per strip, adding one, the car shift light width and
// the car light tables. The file keeps its name because PanelLedBarFormTests reads the Add LEDs form here.
//
// Re-hosted by the #503 foundation from the old Lights tab's strip section, so every control keeps working
// while the LEDs page agent rebuilds it to Leds.dc.html and AddLeds.dc.html. Installing, moving and the census
// are in SettingsControl.Profiles.cs, shared with Matrix, Updates and Home; every one of them finds a strip's
// embedded profile by LedBar.ProfileShapeId, which is the reversed twin for a strip wired from the far end.
//
// #369 turns the four-value rev light style into one switch, the car's own rev lights on or off: on writes
// Contract.LedRpmStyleCar and off Contract.LedRpmStyleLeftToRight. Until the LEDs page lands the chooser
// below still offers every style Contract.LedRpmStyles declares.
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
        /// <summary>The car tables' button and the line under it. Dropped with the page, so a download that
        /// finishes after the page has gone writes nowhere.</summary>
        private Button carTablesButton;

        private TextBlock carTablesLine;

        private FrameworkElement BuildLedsPage(PanelRoute to)
        {
            OnLeave(() =>
            {
                carTablesButton = null;
                carTablesLine = null;
            });
            var bars = Settings.LedBarList();
            var selected = Selected(PanelPage.Leds);
            var groups = new List<UIElement>();
            var caption = Ui.Caption(PanelLights.BarsCaption);
            caption.Margin = new Thickness(0, 0, 0, 12);
            groups.Add(caption);
            if (bars.Count == 0) groups.Add(Ui.Caption(PanelLights.NoBars));
            foreach (var bar in bars)
            {
                var open = selected == null ? ReferenceEquals(bar, bars[0]) : string.Equals(selected, bar.Namespace, StringComparison.Ordinal);
                groups.Add(BuildLedBarGroup(bar, open));
            }
            groups.Add(BuildAddLedBarRow());

            // Rig-wide: what a mirrored bar does on a strip that is not the car's length.
            var fit = Ui.Anchor(Ui.Row("Car shift light width", "Only for the Car-specific rev light style.",
                BuildSegmented(Contract.LedMirrorFits, PanelLights.MirrorFitLabels, Settings.LedMirrorFit,
                    value => { Settings.LedMirrorFit = value; Save(); })), PanelLeds.AnchorMirrorFit);

            return PageLayout(PanelLeds.Title, null,
                Ui.Anchor(PageSection(PanelLights.BarsTitle, groups.ToArray()), PanelLeds.AnchorStrips),
                PageSection("Every strip", fit, Ui.Anchor(BuildCarTablesRow(), PanelLeds.AnchorCarTables)));
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
            if (service.Stale(DateTime.UtcNow)) line += " " + PanelLights.CarTablesStale;
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

        /// <summary>One bar: what its middle shows, how its ladder fills, whether its flags move, and the
        /// two actions on the bar itself.</summary>
        private FrameworkElement BuildLedBarGroup(LedBar bar, bool open)
        {
            var ns = bar.Namespace;
            return Ui.Collapsible(bar.Name, PanelLightRows.ShapeLabel(bar.Shape), open, () =>
            {
                var centre = BuildChoice(Contract.LedCentres, PanelLights.CentreLabels, Settings.BarCentre(ns), 220,
                    value =>
                    {
                        var live = Settings.LedBarByNamespace(ns);
                        if (live != null) live.Centre = value;
                        Save();
                    });
                // Contract.LedRpmStyleCar is the value the rebuilt LEDs page's single switch writes (#369);
                // until then the chooser offers every style the contract declares.
                var style = BuildChoice(Contract.LedRpmStyles, PanelLights.RpmStyleLabels, Settings.BarRpmStyle(ns) ?? Contract.LedRpmStyleCar, 220,
                    value =>
                    {
                        var live = Settings.LedBarByNamespace(ns);
                        if (live != null) live.RpmStyle = value;
                        Save();
                    });
                IList<string> notOffered;
                var targets = LedTargets.All(out notOffered);
                // What the strip shows with the revs half way, from the rules the Rig page paints with.
                var shape = LightShape.Parse(bar.Shape);
                var preview = Ui.Strip(PanelEmulation.StripFrame(shape == null ? 0 : shape.Left, shape == null ? 0 : shape.Centre, PanelEmulation.Mid), StripStyle.Home,
                    PanelEmulation.Dim(Settings.LightsNightMode, Settings.LightsNightBrightness));
                preview.Margin = new Thickness(0, 0, 0, 12);
                return Ui.VStack(0,
                    preview,
                    Ui.Anchor(BuildLedDeviceRow(targets, notOffered, Settings.BarDevice(ns), value => MoveLedBar(ns, value)), PanelLeds.AnchorDevice),
                    Ui.Anchor(Ui.Row("Centre display", "The LEDs at each end are not affected.", centre), PanelLeds.AnchorCentre),
                    Ui.Anchor(Ui.Row("Rev light style", "Car-specific copies the car you are driving.", style), PanelLeds.AnchorRevStyle),
                    Ui.Anchor(Ui.Row("Flag animation", "Off shows each flag as a steady colour.",
                        BuildToggle(Settings.BarFlagAnimation(ns), on =>
                        {
                            var live = Settings.LedBarByNamespace(ns);
                            if (live != null) live.FlagAnimation = on;
                            Save();
                        })), PanelLeds.AnchorFlagAnimation),
                    // Per bar, because a brow above a monitor has no ends to speak of and a rim does.
                    Ui.Anchor(Ui.Row("Full-strip spotter", "Off lights only the end nearest the car alongside.",
                        BuildToggle(Settings.BarSpotterWhole(ns), on =>
                        {
                            var live = Settings.LedBarByNamespace(ns);
                            if (live != null) live.SpotterWhole = on;
                            Save();
                        })), PanelLeds.AnchorSpotter),
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
        ///
        /// A device SimHub has and OpenDash did not offer is named under the row in every shape, with
        /// the reason in SimHub's log: a wheel missing from the picker with nothing said about it is how
        /// #437 was reported, and the line would have answered it.
        /// </remarks>
        private static FrameworkElement BuildLedDeviceRow(IList<LedTarget> targets, IList<string> declined, string current, Action<string> chosen)
        {
            if (targets.Count == 0)
            {
                return Ui.Row(PanelLights.BarDeviceTitle, PanelLights.DeviceRowCaption(0, null, declined), new Border());
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
                return Ui.Row(PanelLights.BarDeviceTitle,
                    PanelLights.DeviceRowCaption(targets.Count, PanelLights.OneDevice(labels[0]), declined), new Border());
            }

            return Ui.Row(PanelLights.BarDeviceTitle, PanelLights.DeviceRowCaption(targets.Count, PanelLights.BarDeviceCaption, declined),
                BuildChoice(ids.ToArray(), labels.ToArray(), current, 260, chosen));
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
            // By the profile the bar installs, which is the reversed twin for a strip wired from the far end.
            var found = EmbeddedProfileOf(bar);
            if (found == null)
            {
                Say(PanelMessage.Caution(PanelLights.BarAddFailed(bar.Name)));
                return;
            }
            var plan = InstallBar(bar, found.Json);
            var ok = plan.State == FlagBoxInstallState.UpToDate;
            var target = LedTargets.Find(bar.Device);
            var where = target == null ? "that device" : target.Name;
            var line = ok ? "Moved " + bar.Name + "'s profile to " + where + "." : PanelLights.BarAddFailed(bar.Name);
            if (ok && plan.Note != null) line += " " + plan.Note;
            Say(line, ok && plan.Note == null);
        }

        private FrameworkElement BuildLedBarActions(string ns)
        {
            var rename = Ui.Button("Rename", PanelButtonKind.Outline, PanelButtonSize.Small);
            rename.ToolTip = "Renames this strip. Install it again to rename it in SimHub.";
            rename.Click += (sender, args) => ShowRenameLedBar(ns);
            var remove = Ui.Button("Remove", PanelButtonKind.GhostDanger, PanelButtonSize.Small);
            remove.ToolTip = "Removes this strip and its profile from SimHub.";
            remove.Click += (sender, args) => RemoveLedBar(ns);
            var row = Ui.HStack(6, rename, remove);
            row.HorizontalAlignment = HorizontalAlignment.Right;
            row.Margin = new Thickness(0, 12, 0, 8);
            return row;
        }

        /// <summary>
        /// Adding a bar: which shape, then what to call it.
        /// </summary>
        /// <remarks>
        /// The shapes are read out of the assembly rather than listed here, exactly as the Install tab's
        /// rows are: the census is what this build embedded, so a shape it does not carry is not offered
        /// and cannot be added as a bar whose profile does not exist.
        ///
        /// <para>The two numbers cannot say how a wheel is wired, and no second question used to follow
        /// them, so a Fanatec owner who added a strip here got the plain 3/9/3, which lights only some of
        /// the wheel's LEDs and starts the bar from the middle of the rim. The Fanatec switch is that
        /// second question, asked first because it decides the other two (#436).</para>
        /// </remarks>
        private void ShowAddLedBar()
        {
            var shapes = EmbeddedShapes();
            var census = shapes.Select(entry => entry.Id).ToList();
            // Two numbers rather than a list of sixty-three. A driver knows how many LEDs their strip
            // has and how they are grouped, which is exactly A and B; a drop-down asked them to find
            // "3/9/3" among every other geometry and to know that is what their wheel is called.
            var sides = PanelLights.BarSides(census);
            if (sides.Length == 0)
            {
                ShowSheet(PanelLights.AddBar, Ui.Caption("This build ships no strip profiles."), null);
                return;
            }

            var side = sides.Contains(3) ? 3 : sides[0];
            var centres = PanelLights.BarCentres(census, side);
            var centre = centres.Contains(9) ? 9 : centres[0];
            // The one question the two numbers cannot answer: how the wheel is wired. On, it decides them,
            // and side and centre keep what the driver chose so that turning it off gives that back.
            var fanatec = false;

            var note = Ui.Caption(string.Empty);
            note.Margin = new Thickness(0, 4, 0, 8);
            var picture = new ContentControl { Margin = new Thickness(0, 0, 0, 12) };
            var name = BuildNameBox(string.Empty);
            var typed = false;
            name.TextChanged += (sender, args) => typed = name.IsKeyboardFocusWithin;
            var endsHost = new ContentControl { HorizontalAlignment = HorizontalAlignment.Right };
            var centreHost = new ContentControl { HorizontalAlignment = HorizontalAlignment.Right };

            Action refresh = () =>
            {
                note.Text = PanelLights.BarShapeNote(side, centre, fanatec);
                // The shape, drawn: the ends and the centre the two numbers make, at rest.
                var drawnSide = fanatec ? PanelLights.FanatecSide : side;
                var drawnCentre = fanatec ? PanelLights.FanatecCentre : centre;
                picture.Content = Ui.Strip(PanelEmulation.StripFrame(drawnSide, drawnCentre, PanelEmulation.Idle), StripStyle.AddLeds);
                if (!typed) name.Text = DefaultBarName(PanelLights.BarShapeId(side, centre, fanatec));
            };
            Action showCentres = () =>
            {
                centres = PanelLights.BarCentres(census, side);
                // A side of none reaches twenty-five and a side of four stops at twelve, so the choice
                // of centre follows the choice of ends rather than offering lengths nothing is built for.
                if (!centres.Contains(centre)) centre = centres.Contains(9) ? 9 : centres[0];
                // With the switch on the choice shows the wheel's nine and cannot be changed. Shown and
                // not hidden, so that a driver sees what a Fanatec wheel is rather than being told the
                // number is none of their business.
                var offered = fanatec ? Including(PanelLights.BarCentres(census, PanelLights.FanatecSide), PanelLights.FanatecCentre) : centres;
                var choice = BuildChoice(
                    offered.Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray(),
                    offered.Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray(),
                    (fanatec ? PanelLights.FanatecCentre : centre).ToString(CultureInfo.InvariantCulture),
                    120,
                    value =>
                    {
                        centre = int.Parse(value, CultureInfo.InvariantCulture);
                        refresh();
                    });
                choice.IsEnabled = !fanatec;
                centreHost.Content = choice;
                refresh();
            };
            Action showEnds = () =>
            {
                var offered = fanatec ? Including(sides, PanelLights.FanatecSide) : sides;
                var ends = BuildSegmented(
                    offered.Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray(),
                    offered.Select(n => n == 0 ? "None" : n.ToString(CultureInfo.InvariantCulture)).ToArray(),
                    (fanatec ? PanelLights.FanatecSide : side).ToString(CultureInfo.InvariantCulture),
                    value =>
                    {
                        side = int.Parse(value, CultureInfo.InvariantCulture);
                        showCentres();
                    });
                ends.IsEnabled = !fanatec;
                endsHost.Content = ends;
            };

            // Which device gets the profile. SimHub keeps one profile list per LED device, so this is
            // not a detail: a bar installed into the wrong one is written, saved and verified correctly
            // into a list the hardware does not read, which is exactly what a rig reported.
            IList<string> notOffered;
            var targets = LedTargets.All(out notOffered);
            var preferred = LedTargets.Preferred(targets);
            var device = preferred == null ? LedBar.ArduinoDevice : preferred.Id;
            var deviceRow = BuildLedDeviceRow(targets, notOffered, device, value => device = value);

            var rows = new List<UIElement>();
            // Above the ends and the centre because it decides them, and only in a build that embedded
            // the profile it selects: a switch for a shape whose profile does not exist would add a bar
            // that installs nothing.
            if (PanelLights.OffersFanatec(census))
            {
                rows.Add(Ui.Row(PanelLights.BarFanatecTitle, PanelLights.BarFanatecCaption,
                    BuildToggle(false, on =>
                    {
                        fanatec = on;
                        showEnds();
                        showCentres();
                    })));
            }
            var endsRow = Ui.Row(PanelLights.BarEndsTitle, PanelLights.BarEndsCaption, endsHost);
            var centreRow = Ui.Row(PanelLights.BarCentreTitle, PanelLights.BarCentreCaption, centreHost);
            var nameRow = Ui.Row(PanelLights.BarNameTitle, PanelLights.BarNameCaption, name);
            showEnds();
            showCentres();

            var add = Ui.Button(PanelLights.AddBar, PanelButtonKind.Primary, PanelButtonSize.Large);
            add.MinWidth = ButtonMinWidth;
            add.Click += (sender, args) => AddLedBar(PanelLights.BarShapeId(side, centre, fanatec), name.Text, device, shapes);
            var cancel = Ui.Button("Cancel", PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.Click += (sender, args) => CloseSheet();

            rows.Insert(0, picture);
            rows.Add(endsRow);
            rows.Add(centreRow);
            rows.Add(note);
            rows.Add(nameRow);
            rows.Add(deviceRow);
            ShowSheet(PanelLights.AddBar, Ui.VStack(0, rows.ToArray()), SheetFooter(null, cancel, add));
        }

        /// <summary>A list of lengths with one more in it, in order: what a locked control shows, so that
        /// the value it is locked on is always one of its options even in a build that carries the wiring
        /// and not the plain geometry beside it.</summary>
        private static int[] Including(int[] values, int value)
        {
            return values.Contains(value) ? values : values.Concat(new[] { value }).OrderBy(n => n).ToArray();
        }

        private FrameworkElement BuildAddLedBarRow()
        {
            var add = Ui.AddButton(PanelLights.AddBar, PanelMetrics.RowButtonHeight);
            add.HorizontalAlignment = HorizontalAlignment.Left;
            add.Margin = new Thickness(0, 12, 0, 0);
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

        private void AddLedBar(string shape, string name, string device, IList<EmbeddedShape> shapes)
        {
            var bar = Settings.AddLedBar(shape, name, device);
            Save();
            Select(PanelPage.Leds, bar.Namespace);
            var found = shapes.FirstOrDefault(entry => string.Equals(entry.Id, bar.ProfileShapeId, StringComparison.Ordinal));
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
            Say(line, ok && note == null);
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
            Select(PanelPage.Leds, null);
            Redraw();
            Say(PanelMessage.Info("Removed " + name + " and its profile."));
        }

        private void ShowRenameLedBar(string ns)
        {
            var bar = Settings.LedBarByNamespace(ns);
            if (bar == null) return;
            var name = BuildNameBox(bar.Name);
            var save = Ui.Button("Rename", PanelButtonKind.Primary, PanelButtonSize.Large);
            save.MinWidth = ButtonMinWidth;
            save.Click += (sender, args) =>
            {
                Settings.RenameLedBar(ns, name.Text);
                Save();
                Redraw();
            };
            var cancel = Ui.Button("Cancel", PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.Click += (sender, args) => CloseSheet();
            ShowSheet("Rename " + bar.Name,
                Ui.Row(PanelLights.BarNameTitle, PanelLights.BarNameCaption, name),
                SheetFooter("Install the strip again to rename it in SimHub.", cancel, save));
        }

    }
}
