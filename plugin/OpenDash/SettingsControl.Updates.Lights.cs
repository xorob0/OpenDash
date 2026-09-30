// SettingsControl.Updates.Lights.cs: the light rows of the Updates page's "In SimHub" table -- one per strip on
// the rig, with the version of its profile in SimHub, and the flag box profile's when the rig has a matrix --
// and the half of Reinstall everything that brings older and missing light profiles forward.
//
// What a row says is PanelUpdates.StripRow and FlagBoxRow, over PanelCopy.LightRow's words. Asking SimHub and
// installing are in SettingsControl.Profiles.cs, shared with LEDs, Matrix and Home.
//
// NOTHING IS WRITTEN TO SIMHUB EXCEPT ON A PRESS (ADR 0013). A profile installed by an older build is reported
// with an Update press, which is where the strip-outdated fix sends a driver (PanelLeds.StripUpdateRoute), and
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
        /// The strips' rows off one read of every LED device, then the flag box's when the rig has a matrix.
        /// </summary>
        /// <remarks>
        /// Only the rig's own strips' profiles are opened for the census, as the attention check does: the
        /// build runs on every resize and every return to the panel.
        /// </remarks>
        private IList<FrameworkElement> BuildLightRows(double versionWidth, out bool stripsReachable, out FlagBoxPlan flagBoxPlan)
        {
            var drawn = new List<FrameworkElement>();
            stripsReachable = true;
            var bars = Settings.LedBarList().Where(bar => bar != null && bar.ProfileShapeId != null).ToList();
            if (bars.Count > 0)
            {
                bool reachable;
                var census = BarCensus(EmbeddedJsonFor(bars.Select(bar => bar.ProfileShapeId)), out reachable);
                stripsReachable = reachable;
                // Every strip's row is repainted by a press on any of them, each from its own strip's plan:
                // the press reads SimHub again, and a row only ever says what its own strip holds.
                var painters = new List<Action<IDictionary<string, FlagBoxPlan>>>();
                foreach (var entry in census)
                {
                    var plan = reachable ? entry.Value : new FlagBoxPlan { State = FlagBoxInstallState.Unavailable };
                    drawn.Add(UpdatesStripRow(entry.Key, plan, versionWidth, painters));
                }
            }

            flagBoxPlan = null;
            if (Settings.MatrixPanels().Any() && plugin.FlagBoxJson != null)
            {
                flagBoxPlan = SafePlan();
                drawn.Add(UpdatesFlagBoxRow(flagBoxPlan, versionWidth));
            }
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

        /// <summary>The key a strip's plan is held under: its namespace, which its profile's id is made from.</summary>
        private static string UpdatesBarKey(LedBar bar)
        {
            return bar.Namespace ?? bar.Name ?? string.Empty;
        }

        /// <summary>
        /// A strip's row, and its Update press while its profile is older than this build's.
        /// </summary>
        /// <remarks>
        /// The press writes this strip alone and repaints every strip's row from its own plan: what the write
        /// REPORTED for a strip it could not write, so the row says so, and what SimHub now holds for the rest.
        /// A row was once repainted with one plan for every strip of its shape, and a healthy strip read
        /// "Install failed" when another strip of that shape could not be written.
        /// </remarks>
        private FrameworkElement UpdatesStripRow(LedBar bar, FlagBoxPlan plan, double versionWidth, IList<Action<IDictionary<string, FlagBoxPlan>>> painters)
        {
            var key = UpdatesBarKey(bar);
            var actionHost = new Border();
            Action<UpdatesRow> paint;
            var element = UpdatesTableRow(PanelUpdates.StripRow(bar.Name, plan), versionWidth, actionHost, out paint);

            Func<IDictionary<string, FlagBoxPlan>> update = () =>
            {
                UpdatesLightsTally ignored;
                return UpdatesWriteStrips((other, state) => state == FlagBoxInstallState.Outdated && UpdatesBarKey(other) == key, out ignored);
            };
            Action<IDictionary<string, FlagBoxPlan>> draw = plans =>
            {
                foreach (var painter in painters.ToList()) painter(plans);
            };
            Action<FlagBoxPlan> own = null;
            own = current =>
            {
                var row = PanelUpdates.StripRow(bar.Name, current);
                paint(row);
                var hadFocus = actionHost.IsKeyboardFocusWithin;
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
                    };
                    actionHost.Child = button;
                }
                if (hadFocus) UpdatesRefocus(actionHost.Child, updatesReinstall);
            };
            painters.Add(plans =>
            {
                FlagBoxPlan current;
                if (plans != null && plans.TryGetValue(key, out current)) own(current);
            });
            own(plan);
            return element;
        }

        /// <summary>
        /// The flag box profile's row, named as SimHub lists it, and its Update press while the profile in
        /// SimHub is older than this build's.
        /// </summary>
        private FrameworkElement UpdatesFlagBoxRow(FlagBoxPlan plan, double versionWidth)
        {
            var name = FlagBoxName();
            var path = plugin.FlagBox == null ? null : plugin.FlagBox.Path;
            var actionHost = new Border();
            Action<UpdatesRow> paint;
            var element = UpdatesTableRow(PanelUpdates.FlagBoxRow(name, plan, path), versionWidth, actionHost, out paint);
            Func<FlagBoxPlan> press = InstallFlagBox;
            Action<FlagBoxPlan> draw = null;
            draw = current =>
            {
                var row = PanelUpdates.FlagBoxRow(name, current, path);
                paint(row);
                var hadFocus = actionHost.IsKeyboardFocusWithin;
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
        /// SimHub holds when the button is pressed. A strip whose device SimHub no longer has is left alone
        /// and reported as failed with the reason in SimHub's log, as UpdateBars does: InstallBar takes the
        /// strip's copy out of every device first, so running it there would remove a strip that still lights.
        /// </remarks>
        /// <param name="tally">What the writes did, counted per strip.</param>
        private IDictionary<string, FlagBoxPlan> UpdatesWriteStrips(Func<LedBar, FlagBoxInstallState, bool> wanted, out UpdatesLightsTally tally)
        {
            tally = new UpdatesLightsTally();
            var plans = new Dictionary<string, FlagBoxPlan>(StringComparer.Ordinal);
            var bars = Settings.LedBarList().Where(bar => bar != null && bar.ProfileShapeId != null).ToList();
            if (bars.Count == 0) return plans;
            var embedded = EmbeddedJsonFor(bars.Select(bar => bar.ProfileShapeId).Distinct(StringComparer.Ordinal));
            bool reachable;
            var written = new Dictionary<string, FlagBoxPlan>(StringComparer.Ordinal);
            foreach (var entry in BarCensus(embedded, out reachable))
            {
                if (!reachable || !wanted(entry.Key, entry.Value.State)) continue;
                var bar = entry.Key;
                string json;
                FlagBoxPlan result;
                if (!embedded.TryGetValue(bar.ProfileShapeId, out json))
                {
                    result = new FlagBoxPlan { State = FlagBoxInstallState.NotEmbedded };
                }
                else if (LedTargets.Find(bar.Device) == null)
                {
                    Log.Warn("The profile for " + bar.Name + " was not written: the LED device it names is no longer in SimHub.");
                    result = new FlagBoxPlan { State = FlagBoxInstallState.Failed };
                }
                else
                {
                    result = InstallBar(bar, json);
                }
                tally.Strip(entry.Value.State, result.State);
                written[UpdatesBarKey(bar)] = result;
            }
            foreach (var entry in BarCensus(embedded, out reachable))
            {
                var key = UpdatesBarKey(entry.Key);
                FlagBoxPlan result;
                if (!reachable) plans[key] = new FlagBoxPlan { State = FlagBoxInstallState.Unavailable };
                else if (written.TryGetValue(key, out result) && result.State != FlagBoxInstallState.UpToDate) plans[key] = result;
                else plans[key] = entry.Value;
            }
            return plans;
        }

        /// <summary>
        /// Reinstall everything's lights (ruling 70): every strip whose profile in SimHub is older than this
        /// build's, missing or failed, and the flag box profile likewise on a rig with a matrix, and nothing
        /// that is current.
        /// </summary>
        private UpdatesLightsTally UpdatesBringLightsForward()
        {
            UpdatesLightsTally tally;
            UpdatesWriteStrips((bar, state) => PanelUpdates.BringsForward(state), out tally);
            if (plugin.FlagBoxJson != null)
            {
                var before = SafePlan().State;
                if (PanelUpdates.BringsFlagBoxForward(Settings.MatrixPanels().Any(), before)) tally.FlagBox(before, InstallFlagBox().State);
            }
            return tally;
        }
    }
}
