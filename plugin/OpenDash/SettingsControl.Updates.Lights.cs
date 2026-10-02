// SettingsControl.Updates.Lights.cs: the light rows of the Updates page's "In SimHub" table -- one per strip on
// the rig, with the version of its profile in SimHub, and the flag box profile's when the rig has a matrix --
// and the half of Reinstall everything that brings older and missing light profiles forward.
//
// What a row says is PanelUpdates.StripRow and FlagBoxRow, over PanelCopy.LightRow's words. Asking SimHub and
// installing are in SettingsControl.Profiles.cs, shared with LEDs, Matrix and Home.
//
// NOTHING IS WRITTEN TO SIMHUB EXCEPT ON A PRESS (ADR 0013). A profile installed by an older build is reported
// with an Update press, as the strip's header on LEDs is (where PanelLeds.StripUpdateRoute sends a driver), and
// is not rewritten until that or Reinstall everything is pressed; a missing one is installed by Reinstall
// everything alone (ruling 70), and one that is current is never rewritten.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>
        /// What SimHub holds for each of the rig's strips, off one read of every LED device, each strip with its
        /// plan as PanelUpdates.StripPlans reads the census: Unavailable for every strip when SimHub's LED
        /// settings cannot be read, and NotEmbedded for a strip whose shape this build carries no profile for.
        /// </summary>
        /// <remarks>
        /// Only the rig's own strips' profiles are opened for the census, as the attention check does: the
        /// build runs on every resize and every return to the panel.
        /// </remarks>
        private IList<KeyValuePair<LedBar, FlagBoxPlan>> UpdatesStripPlans(out bool stripsReachable)
        {
            stripsReachable = true;
            var bars = Settings.LedBarList().Where(bar => bar != null && bar.ProfileShapeId != null).ToList();
            if (bars.Count == 0) return new List<KeyValuePair<LedBar, FlagBoxPlan>>();
            var embedded = EmbeddedJsonFor(bars.Select(bar => bar.ProfileShapeId));
            bool reachable;
            var census = BarCensus(embedded, out reachable);
            stripsReachable = reachable;
            return PanelUpdates.StripPlans(census, reachable, embedded.Keys);
        }

        /// <summary>SimHub's LED devices, their names by id, off one read (LedTargets.All, which never throws):
        /// whether a strip's device is listed, and the name the select step gives it.</summary>
        private static IDictionary<string, string> UpdatesDevices()
        {
            var devices = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var target in LedTargets.All())
            {
                if (target != null && target.Id != null) devices[target.Id] = target.Name;
            }
            return devices;
        }

        /// <summary>Whether the table draws the flag box profile's row (PanelUpdates.DrawsFlagBoxRow).</summary>
        private bool UpdatesDrawsFlagBox()
        {
            return PanelUpdates.DrawsFlagBoxRow(Settings.MatrixPanels().Any());
        }

        /// <summary>
        /// The strips' rows, then the flag box's when the table draws it (<paramref name="flagBoxPlan"/> not null).
        /// </summary>
        private IList<FrameworkElement> UpdatesLightRows(IList<KeyValuePair<LedBar, FlagBoxPlan>> strips, IList<UpdatesRow> stripRows, FlagBoxPlan flagBoxPlan, UpdatesRow flagBoxRow, double versionWidth)
        {
            var drawn = new List<FrameworkElement>();
            // Every strip's row is repainted by a press on any of them, each from its own strip's plan: the
            // press reads SimHub again, and a row only ever says what its own strip holds.
            var painters = new List<Action<IDictionary<string, FlagBoxPlan>, IDictionary<string, string>>>();
            for (var i = 0; i < strips.Count; i++) drawn.Add(UpdatesStripRow(strips[i].Key, stripRows[i], versionWidth, painters));
            if (flagBoxPlan != null) drawn.Add(UpdatesFlagBoxRow(flagBoxPlan, flagBoxRow, versionWidth));
            return drawn;
        }

        /// <summary>The row's one press, Update, while its profile is older than this build's.</summary>
        private static Button UpdatesRowPress()
        {
            var button = Ui.Button(PanelUpdates.RowUpdate, PanelButtonKind.Outline, PanelButtonSize.Small);
            button.ToolTip = PanelUpdates.RowUpdateTooltip;
            button.Margin = new Thickness(PanelUpdates.TableGap, 0, 0, 0);
            return button;
        }

        /// <summary>Takes a press a repaint replaced out of the presses a run holds off (ShowRun).</summary>
        private void UpdatesUnhold(Button press)
        {
            if (press != null) updatesRunPresses.Remove(press);
        }

        /// <summary>
        /// A strip's row, and its Update press while its profile is older than this build's and SimHub lists
        /// its device.
        /// </summary>
        /// <remarks>
        /// The press writes this strip alone, repaints every strip's row from its own plan -- what the write
        /// REPORTED for a strip it could not write, so the row says so, and what SimHub now holds for the rest
        /// -- and says what it did, as the LEDs page's Update does. A row was once repainted with one plan for
        /// every strip of its shape, and a healthy strip read "Install failed" when another strip of that
        /// shape could not be written.
        /// </remarks>
        private FrameworkElement UpdatesStripRow(LedBar bar, UpdatesRow first, double versionWidth, IList<Action<IDictionary<string, FlagBoxPlan>, IDictionary<string, string>>> painters)
        {
            var key = PanelUpdates.StripKey(bar);
            var actionHost = new Border();
            Action<UpdatesRow> paint;
            var element = UpdatesTableRow(first, versionWidth, actionHost, out paint);

            UpdatesLightsTally said = null;
            Func<IDictionary<string, FlagBoxPlan>> update = () => UpdatesWriteStrips(PanelUpdates.RowUpdateWrites(key), out said);
            Action<IDictionary<string, FlagBoxPlan>> draw = plans =>
            {
                var devices = UpdatesDevices();
                foreach (var painter in painters.ToList()) painter(plans, devices);
            };
            Action<UpdatesRow> own = null;
            own = row =>
            {
                paint(row);
                var hadFocus = actionHost.IsKeyboardFocusWithin;
                // The press this repaint replaces leaves the run's list with the tree, so the list holds only
                // the presses this build draws (UpdatesUnhold).
                UpdatesUnhold(actionHost.Child as Button);
                actionHost.Child = null;
                if (row.OffersUpdate)
                {
                    var button = UpdatesRowPress();
                    button.IsEnabled = !applying;
                    updatesRunPresses.Add(button);
                    button.Click += (sender, args) =>
                    {
                        if (applying) return;
                        draw(update());
                        // What needs fixing moved with the press: Home's list, the Matrix and Updates dots.
                        RefreshAttention();
                        RefreshSidebar();
                        UpdatesSay(said);
                    };
                    actionHost.Child = button;
                }
                if (hadFocus) UpdatesRefocus(actionHost.Child, updatesReinstall);
            };
            painters.Add((plans, devices) =>
            {
                FlagBoxPlan current;
                if (plans != null && plans.TryGetValue(key, out current)) own(PanelUpdates.StripRow(bar.Name, current, PanelUpdates.DeviceListed(devices, bar)));
            });
            own(first);
            return element;
        }

        /// <summary>What a light row's Update did, said as the LEDs and Matrix pages say theirs; nothing when
        /// the press found nothing left to write.</summary>
        private void UpdatesSay(UpdatesLightsTally said)
        {
            var line = PanelUpdates.LightsSaid(said, FlagBoxName(), Settings.MatrixPanels().ToList());
            if (line != null) Say(line, said.Ok);
        }

        /// <summary>
        /// The flag box profile's row, named as SimHub lists it, and its Update press while the profile in
        /// SimHub is older than this build's.
        /// </summary>
        private FrameworkElement UpdatesFlagBoxRow(FlagBoxPlan plan, UpdatesRow first, double versionWidth)
        {
            var name = FlagBoxName();
            var path = plugin.FlagBox == null ? null : plugin.FlagBox.Path;
            var actionHost = new Border();
            Action<UpdatesRow> paint;
            var element = UpdatesTableRow(first, versionWidth, actionHost, out paint);
            var drawn = plan;
            UpdatesLightsTally said = null;
            Func<FlagBoxPlan> press = () =>
            {
                var result = InstallFlagBox();
                said = new UpdatesLightsTally();
                said.FlagBox(drawn.State, result);
                return result;
            };
            Action<FlagBoxPlan> draw = null;
            draw = current =>
            {
                drawn = current;
                var row = PanelUpdates.FlagBoxRow(name, current, path);
                paint(row);
                var hadFocus = actionHost.IsKeyboardFocusWithin;
                // The press this repaint replaces leaves the run's list with the tree, so the list holds only
                // the presses this build draws (UpdatesUnhold).
                UpdatesUnhold(actionHost.Child as Button);
                actionHost.Child = null;
                if (row.OffersUpdate)
                {
                    var button = UpdatesRowPress();
                    button.IsEnabled = !applying;
                    updatesRunPresses.Add(button);
                    button.Click += (sender, args) =>
                    {
                        if (applying) return;
                        draw(press());
                        // What needs fixing moved with the press: Home's list, the Matrix and Updates dots.
                        RefreshAttention();
                        RefreshSidebar();
                        UpdatesSay(said);
                    };
                    actionHost.Child = button;
                }
                if (hadFocus) UpdatesRefocus(actionHost.Child, updatesReinstall);
            };
            draw(plan);
            return element;
        }

        /// <summary>
        /// Writes the profiles of the rig's strips that <paramref name="wanted"/> picks from what SimHub holds
        /// for each, each into the device it names, and returns every strip's plan as it then stands.
        /// </summary>
        /// <remarks>
        /// The census is read at the press rather than carried from the draw, so what is written is what
        /// SimHub holds when the button is pressed, and it is read through PanelUpdates.StripPlans, so a strip
        /// this build has no profile for is NotEmbedded and never picked. Which strips are written, which are
        /// left out because SimHub does not list their device, and what each row is repainted with
        /// (PanelUpdates.AfterWrite) are PanelUpdates', where they are pinned; this reads SimHub and writes.
        /// </remarks>
        /// <param name="tally">What the writes did, strip by strip.</param>
        private IDictionary<string, FlagBoxPlan> UpdatesWriteStrips(Func<LedBar, FlagBoxInstallState, bool> wanted, out UpdatesLightsTally tally)
        {
            tally = new UpdatesLightsTally();
            var plans = new Dictionary<string, FlagBoxPlan>(StringComparer.Ordinal);
            var bars = Settings.LedBarList().Where(bar => bar != null && bar.ProfileShapeId != null).ToList();
            if (bars.Count == 0) return plans;
            var embedded = EmbeddedJsonFor(bars.Select(bar => bar.ProfileShapeId).Distinct(StringComparer.Ordinal));
            var devices = UpdatesDevices();
            Func<LedBar, bool> listed = bar => PanelUpdates.DeviceListed(devices, bar);
            bool reachable;
            var census = PanelUpdates.StripPlans(BarCensus(embedded, out reachable), reachable, embedded.Keys);
            var written = new Dictionary<string, FlagBoxPlan>(StringComparer.Ordinal);
            foreach (var entry in PanelUpdates.StripsToWrite(census, reachable, wanted, listed))
            {
                var bar = entry.Key;
                var result = InstallBar(bar, embedded[bar.ProfileShapeId]);
                tally.Strip(bar.Name, PanelUpdates.DeviceName(devices, bar), entry.Value.State, result);
                written[PanelUpdates.StripKey(bar)] = result;
            }
            foreach (var bar in PanelUpdates.StripsWithoutDevice(census, reachable, wanted, listed))
            {
                Log.Warn("The profile for " + bar.Name + " was not written: SimHub does not list the LED device it names.");
                tally.DeviceNotListed(bar.Name);
            }
            var after = PanelUpdates.StripPlans(BarCensus(embedded, out reachable), reachable, embedded.Keys);
            return PanelUpdates.AfterWrite(after, reachable, written);
        }

        /// <summary>
        /// Reinstall everything's lights (ruling 70): every strip whose profile in SimHub is older than this
        /// build's, missing or failed, and the flag box profile likewise on a rig with a matrix, and nothing
        /// that is current.
        /// </summary>
        private UpdatesLightsTally UpdatesBringLightsForward()
        {
            UpdatesLightsTally tally;
            UpdatesWriteStrips(PanelUpdates.ReinstallWrites, out tally);
            if (plugin.FlagBoxJson != null)
            {
                var before = SafePlan().State;
                if (PanelUpdates.BringsFlagBoxForward(Settings.MatrixPanels().Any(), before)) tally.FlagBox(before, InstallFlagBox());
            }
            return tally;
        }
    }
}
