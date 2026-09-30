// SettingsControl.Updates.Lights.cs: the light rows of the Updates page's "In SimHub" table -- one per strip on
// the rig, with the version of its profile in SimHub, and the flag box profile's when the rig has a matrix --
// and the half of Reinstall everything that brings older light profiles forward.
//
// What a row says is PanelUpdates.StripRow and FlagBoxRow, over PanelCopy.LightRow's words. Asking SimHub and
// installing are in SettingsControl.Profiles.cs, shared with LEDs, Matrix and Home.
//
// NOTHING IS WRITTEN TO SIMHUB EXCEPT ON A PRESS (ADR 0013). A profile installed by an older build is reported
// with an Update press, which is where the strip-outdated fix sends a driver (PanelLeds.StripUpdateRoute), and
// is not rewritten until that or Reinstall everything is pressed.
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
                // Every row of one profile is redrawn by a press on any of them, since an update rewrites every
                // older strip of that profile at once.
                var painters = new Dictionary<string, List<Action<FlagBoxPlan>>>(StringComparer.Ordinal);
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
            var button = Ui.Button(PanelCopy.LightRow(FlagBoxInstallState.Outdated, null).Button, PanelButtonKind.Outline, PanelButtonSize.Small);
            button.ToolTip = "Replaces the copy in SimHub with this version.";
            button.Margin = new Thickness(PanelUpdates.TableGap, 0, 0, 0);
            return button;
        }

        /// <summary>
        /// A strip's row, and its Update press while its profile is older than this build's.
        /// </summary>
        /// <remarks>
        /// The press redraws the rows from what it REPORTED, rather than from a fresh read of SimHub, so a strip
        /// that could not be rewritten leaves its row saying so.
        /// </remarks>
        private FrameworkElement UpdatesStripRow(LedBar bar, FlagBoxPlan plan, double versionWidth, IDictionary<string, List<Action<FlagBoxPlan>>> painters)
        {
            var shape = bar.ProfileShapeId;
            var actionHost = new Border();
            Action<UpdatesRow> paint;
            var element = UpdatesTableRow(PanelUpdates.StripRow(bar.Name, plan), versionWidth, actionHost, out paint);

            List<Action<FlagBoxPlan>> group;
            if (!painters.TryGetValue(shape, out group))
            {
                group = new List<Action<FlagBoxPlan>>();
                painters[shape] = group;
            }
            Func<FlagBoxPlan> update = () => UpdateBars(new[] { shape }, EmbeddedJsonFor(new[] { shape }));
            Action<FlagBoxPlan> draw = current =>
            {
                foreach (var painter in group.ToList()) painter(current);
            };
            Action<FlagBoxPlan> own = null;
            own = current =>
            {
                var row = PanelUpdates.StripRow(bar.Name, current);
                paint(row);
                actionHost.Child = null;
                if (!row.OffersUpdate) return;
                var button = UpdatesRowPress();
                button.IsEnabled = !applying;
                button.Click += (sender, args) =>
                {
                    draw(update());
                    // What needs fixing moved with the press: Home's list, the Matrix and Updates dots.
                    RefreshAttention();
                    RefreshSidebar();
                };
                actionHost.Child = button;
            };
            group.Add(own);
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
                actionHost.Child = null;
                if (!row.OffersUpdate) return;
                var button = UpdatesRowPress();
                button.IsEnabled = !applying;
                button.Click += (sender, args) =>
                {
                    draw(press());
                    // What needs fixing moved with the press: Home's list, the Matrix and Updates dots.
                    RefreshAttention();
                    RefreshSidebar();
                };
                actionHost.Child = button;
            };
            draw(plan);
            return element;
        }

        /// <summary>
        /// Reinstall everything's lights: every strip whose profile in SimHub is older than this build's, and
        /// the flag box profile when it is older, and nothing that is current.
        /// </summary>
        /// <param name="updated">Strips brought to this build's version.</param>
        /// <param name="failed">Older strips that could not be rewritten; the reason is in SimHub's log.</param>
        /// <param name="flagBox">What updating the flag box profile left, or null when it was not older.</param>
        private void UpdatesBringLightsForward(out int updated, out int failed, out FlagBoxInstallState? flagBox)
        {
            updated = 0;
            failed = 0;
            flagBox = null;
            var bars = Settings.LedBarList().Where(bar => bar != null && bar.ProfileShapeId != null).ToList();
            if (bars.Count > 0)
            {
                var shapes = bars.Select(bar => bar.ProfileShapeId).Distinct(StringComparer.Ordinal).ToList();
                var embedded = EmbeddedJsonFor(shapes);
                bool reachable;
                var outdated = PanelLightRows.OutdatedBars(shapes, BarCensus(embedded, out reachable));
                foreach (var group in outdated.GroupBy(bar => bar.ProfileShapeId, StringComparer.Ordinal))
                {
                    var plan = UpdateBars(new[] { group.Key }, embedded);
                    if (plan.State == FlagBoxInstallState.UpToDate) updated += group.Count();
                    else failed += group.Count();
                }
            }
            if (plugin.FlagBoxJson != null && SafePlan().State == FlagBoxInstallState.Outdated) flagBox = InstallFlagBox().State;
        }
    }
}
