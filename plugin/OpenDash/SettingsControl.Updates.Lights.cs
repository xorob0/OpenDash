// SettingsControl.Updates.Lights.cs: the "Lights OpenDash can install" section of the Updates page -- the flag
// box's row and one row per strip shape the build carries, with what the rig's strips of it hold in SimHub.
//
// What decides a row -- which shapes share one, what it is called, what its caption and tooltip say -- is in
// PanelLightRows.cs, which the test project compiles. Asking SimHub and installing are in
// SettingsControl.Profiles.cs, shared with LEDs, Matrix and Home; the by-hand route for the flag box is on the
// Matrix page, beside the profile it is the route for.
//
// NOTHING IS WRITTEN TO SIMHUB EXCEPT ON A PRESS (ADR 0013). A strip installed by an older build is reported
// and offered an Update, and is not rewritten until that is pressed.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private const string LightsSectionLabel = PanelUpdates.LightsTitle;

        private FrameworkElement BuildLightsSection()
        {
            var flagBoxPlan = SafePlan();
            var rows = new List<UIElement>();
            var flagBox = BuildFlagBoxRow(flagBoxPlan);
            if (flagBox != null) rows.Add(flagBox);
            bool stripsUnavailable;
            rows.AddRange(BuildStripRows(typeof(OpenDash).Assembly, out stripsUnavailable));

            var children = new List<UIElement> { Ui.Caption(PanelLightRows.SectionCaption, BodyWidth) };
            if (rows.Count == 0)
            {
                children.Add(Ui.Caption(PanelLightRows.NoProfiles, BodyWidth));
                return PageSection(LightsSectionLabel, children.ToArray());
            }

            // The rows go in as one child rather than as a child each: a section holds what it is given
            // twenty apart, and an install row is separated by its own rule and by nothing else.
            children.Add(Ui.VStack(0, rows.ToArray()));
            // A row whose driver cannot be reached reads "Not installed", because that is the only pill
            // PanelCopy has for a state nothing is known about. The sentence under the rows is what stops
            // that pill being read as a fact: it says the settings could not be asked, which is what the
            // press would run into.
            if (stripsUnavailable) children.Add(Ui.Caption(PanelLightRows.Unavailable, BodyWidth));
            return PageSection(LightsSectionLabel, children.ToArray());
        }

        /// <summary>
        /// The flag box's row, or nothing when this build carries no flag box profile.
        /// </summary>
        /// <remarks>
        /// The name is the profile's own, which the build wrote and FlagBoxProfile read back, rather than a
        /// string here that a rename on the other side would leave stale. The sentence that used to be the
        /// row's second line is its tooltip now: the canvas puts the shape there instead, and the sentence
        /// -- which is the one that warns that a press replaces the copy in SimHub -- is what the row says
        /// when it is asked.
        /// </remarks>
        private FrameworkElement BuildFlagBoxRow(FlagBoxPlan plan)
        {
            if (plugin.FlagBoxJson == null) return null;
            return BuildLightRow(
                FlagBoxName(),
                PanelLightRows.FlagBoxCaption,
                plan,
                InstallFlagBox,
                current => FlagBoxInstallPlan.Summary(current, plugin.FlagBox?.Path),
                button => button.ToolTip = "Adds OpenDash's profile. Your own profiles are never changed.");
        }

        /// <summary>
        /// One row per strip or brow shape the build embedded, grouped the way the canvas groups them.
        /// </summary>
        /// <remarks>
        /// One read of every LED device's profile list for all the rows rather than one per row or per
        /// bar, and each of the rig's bars is looked for in them by the id it derives
        /// (LedBarProfile.Plan). A row then reports what its shapes' bars hold (PanelLightRows.RowPlan).
        ///
        /// A shape this build did not embed has no row at all, which is the honest answer: a row for a
        /// profile that is not there could only offer a press that does nothing.
        ///
        /// The rows' names come from <see cref="EmbeddedProfileNames"/>, read once for the plugin's lifetime,
        /// and only the rig's own strips' profiles are opened for the census: this unpacked all 122 on every
        /// build, and a build is now also a resize or a return to the panel.
        /// </remarks>
        private IList<UIElement> BuildStripRows(Assembly assembly, out bool unavailable)
        {
            var profiles = EmbeddedProfileNames(assembly).Select(pair => new LightProfile(pair.Key, pair.Value)).ToList();
            Func<IDictionary<string, string>> rigJson = () => EmbeddedJsonFor(Settings.LedBarList().Where(bar => bar != null).Select(bar => bar.ProfileShapeId));

            bool reachable;
            var bars = BarCensus(rigJson(), out reachable);
            unavailable = !reachable;

            var drawn = new List<UIElement>();
            foreach (var row in PanelLightRows.Rows(profiles))
            {
                // No Install press. A strip profile reaches SimHub as an LED bar's, under the name its
                // owner gave it, because two strips that share one profile share one set of settings --
                // which is the thing the bars exist to stop -- and a bar is added on the Lights tab. This
                // row is the census: which shapes this build draws for, and what the rig's own strips of
                // those shapes hold in SimHub.
                var shapeIds = row.ShapeIds;
                drawn.Add(BuildCensusRow(
                    row.Name,
                    row.Caption,
                    shapeIds.Count,
                    PanelLightRows.RowPlan(shapeIds, bars, reachable),
                    () => UpdateBars(shapeIds, rigJson())));
            }
            return drawn;
        }

        /// <summary>
        /// A strip row: what the shapes are and what the rig's strips of them hold in SimHub, with an
        /// Update press while one of those strips is older than this build.
        /// </summary>
        /// <remarks>
        /// Update and nothing else, because it is the one thing this row can do that nothing else on the
        /// panel does: a strip is added, moved and removed on the Lights tab, and an older copy in SimHub
        /// is not rewritten on its own (ADR 0013, ADR 0017). The press redraws the row from what it
        /// REPORTED, the way the flag box's does, so a strip that could not be rewritten leaves the row
        /// saying so rather than a fresh read finding the others fine.
        /// </remarks>
        private FrameworkElement BuildCensusRow(string name, string caption, int members, FlagBoxPlan plan, Func<FlagBoxPlan> update)
        {
            var pillHost = new Border { VerticalAlignment = VerticalAlignment.Center };
            var actionHost = new Border { VerticalAlignment = VerticalAlignment.Center };
            var row = Ui.InstallRow(null, name, caption, pillHost, actionHost);
            Action<FlagBoxPlan> draw = null;
            draw = current =>
            {
                var state = PanelCopy.LightRow(current.State, current.InstalledVersion);
                pillHost.Child = Ui.StatusPill(PanelLightRows.DotHex(current.State), state.State, state.StateHex);
                row.ToolTip = PanelLightRows.Tooltip(members, current);
                if (current.State != FlagBoxInstallState.Outdated)
                {
                    actionHost.Child = null;
                    return;
                }
                var button = Ui.PrimaryButton(state.Button, PanelMetrics.RowButtonHeight);
                button.MinWidth = ButtonMinWidth;
                button.Click += (sender, args) =>
                {
                    draw(update());
                    // What needs fixing moved with the press: Home's list, the Matrix and Updates dots.
                    RefreshAttention();
                    RefreshSidebar();
                };
                actionHost.Child = button;
            };
            draw(plan);
            return row;
        }

        /// <summary>
        /// An install row: what the profile is, the state it is in, and one button that changes it.
        /// </summary>
        /// <remarks>
        /// The pill and the button are held in hosts and swapped rather than edited, because the verb and
        /// the button's face change together -- an update is the accented press and a reinstall is an
        /// outline -- and a Button cannot be restyled in place.
        ///
        /// The row redraws from what the press REPORTED rather than from a fresh read of SimHub, which is
        /// what keeps a partial failure inside a group visible: Install returns a plan per member, Combine
        /// reduces them worst-first, and a member that could not be written leaves the row saying so rather
        /// than the row asking again and finding the other eighteen fine. The controls are captured rather
        /// than held in fields because a press runs on the UI thread between one draw and the next, so the
        /// tab it belongs to cannot have been left underneath it.
        /// </remarks>
        private FrameworkElement BuildLightRow(
            string name,
            string caption,
            FlagBoxPlan plan,
            Func<FlagBoxPlan> press,
            Func<FlagBoxPlan, string> tooltip,
            Action<Button> adopt = null)
        {
            var pillHost = new Border { VerticalAlignment = VerticalAlignment.Center };
            var actionHost = new Border { VerticalAlignment = VerticalAlignment.Center };
            var row = Ui.InstallRow(null, name, caption, pillHost, actionHost);
            Action<FlagBoxPlan> draw = null;
            draw = current =>
            {
                var state = PanelCopy.LightRow(current.State, current.InstalledVersion);
                pillHost.Child = Ui.StatusPill(PanelLightRows.DotHex(current.State), state.State, state.StateHex);
                var button = state.Style == PanelButton.Primary
                    ? Ui.PrimaryButton(state.Button, PanelMetrics.RowButtonHeight)
                    : Ui.OutlineButton(state.Button, PanelMetrics.RowButtonHeight);
                button.MinWidth = ButtonMinWidth;
                // Nothing to press when there is no profile to install or nowhere to put it. The kit draws
                // the disabled state at the canvas's 40 per cent, so nothing here has to dim it.
                button.IsEnabled = current.State != FlagBoxInstallState.NotEmbedded
                    && current.State != FlagBoxInstallState.Unavailable;
                button.Click += (sender, args) =>
                {
                    draw(press());
                    // What needs fixing moved with the press: Home's list, the Matrix and Updates dots.
                    RefreshAttention();
                    RefreshSidebar();
                };
                actionHost.Child = button;
                row.ToolTip = tooltip(current);
                if (adopt != null) adopt(button);
            };
            draw(plan);
            return row;
        }

    }
}
