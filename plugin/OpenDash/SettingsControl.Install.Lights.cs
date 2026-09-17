// SettingsControl.Install.Lights.cs: the "Lights openDash can install" section of the Install tab -- one
// row per light profile the build carries, in the shape the canvas gives it.
//
// Lifted out of SettingsControl.Install.cs beside the package rows and the plugin's own section, so the
// three sections of the tab are a file each. What decides a row -- which shapes share one, what it is
// called, what its caption says and what its tooltip says -- is in PanelLightRows.cs, which the test
// project compiles; this file is the drawing and the two presses.
//
// The flag box row used to be on the Lights tab (SettingsControl.Lights.cs), which is where a driver goes
// to configure rather than to install. It is here now beside the strips, because the twenty profiles are
// one list and a driver who owns a wheel and a box should not have to find them in two places.
//
// NOTHING IS WRITTEN TO SIMHUB EXCEPT ON A PRESS, which is the consent half of ADR 0013 and what the
// section's own caption says. Drawing the section is free of side effects: FlagBoxInstaller.Plan and
// StripInstaller.Plan only ask SimHub what it already holds, and the profiles are read out of the
// assembly rather than off disk.
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
        private TextBlock flagBoxLine;
        private Button flagBoxButton;
        private Button flagBoxCopyButton;
        private TextBox flagBoxPath;

        private const string LightsSectionLabel = "Lights openDash can install";

        private FrameworkElement BuildLightsSection()
        {
            var flagBoxPlan = SafePlan();
            var rows = new List<UIElement>();
            var flagBox = BuildFlagBoxRow(flagBoxPlan);
            if (flagBox != null) rows.Add(flagBox);
            rows.AddRange(BuildStripRows(typeof(OpenDash).Assembly));

            var children = new List<UIElement> { Ui.Caption(PanelLightRows.SectionCaption, BodyWidth) };
            if (rows.Count == 0)
            {
                children.Add(Ui.Caption(PanelLightRows.NoProfiles, BodyWidth));
                return Ui.Section(LightsSectionLabel, children.ToArray());
            }

            // The rows go in as one child rather than as a child each: a section holds what it is given
            // twenty apart, and an install row is separated by its own rule and by nothing else.
            children.Add(Ui.VStack(0, rows.ToArray()));
            if (flagBox != null && flagBoxPlan.State == FlagBoxInstallState.Unavailable)
            {
                children.Add(BuildFlagBoxFallback(flagBoxPlan));
            }
            return Ui.Section(LightsSectionLabel, children.ToArray());
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
                plugin.FlagBox.ProfileName ?? FlagBoxProfile.ProfileName,
                PanelLightRows.FlagBoxCaption,
                plan,
                InstallFlagBox,
                current => FlagBoxInstallPlan.Summary(current, plugin.FlagBox?.Path),
                button =>
                {
                    flagBoxButton = button;
                    flagBoxButton.ToolTip =
                        "Adds openDash's profile to SimHub's matrix profiles. It never changes a profile you made yourself.";
                });
        }

        /// <summary>
        /// One row per strip or brow shape the build embedded, grouped the way the canvas groups them.
        /// </summary>
        /// <remarks>
        /// One read of SimHub's profile list for all nineteen rather than one per row: StripInstaller.Plan
        /// takes the whole list, and a grouped row reduces its members with FlagBoxInstallPlan.Combine, so
        /// the row can never read better than the profile it is worst about.
        ///
        /// A shape this build did not embed has no row at all, which is the honest answer: a row for a
        /// profile that is not there could only offer a press that does nothing.
        /// </remarks>
        private IList<UIElement> BuildStripRows(Assembly assembly)
        {
            var log = new SimHubInstallLog();
            var json = new Dictionary<string, string>(StringComparer.Ordinal);
            var profiles = new List<LightProfile>();
            foreach (var resource in FlagBoxProfile.StripResourceNames(assembly))
            {
                var id = FlagBoxProfile.ShapeIdOf(resource);
                if (id == null || json.ContainsKey(id)) continue;
                var text = FlagBoxProfile.ResourceText(assembly, resource, log);
                json[id] = text;
                profiles.Add(new LightProfile(id, FlagBoxProfile.ProfileNameOf(text)));
            }

            var rows = PanelLightRows.Rows(profiles);
            var order = rows.SelectMany(row => row.ShapeIds).ToList();
            var plans = Plan(order.Select(id => json[id]).ToList());
            var byId = new Dictionary<string, FlagBoxPlan>(StringComparer.Ordinal);
            for (var i = 0; i < order.Count && i < plans.Count; i++) byId[order[i]] = plans[i];

            var drawn = new List<UIElement>();
            foreach (var row in rows)
            {
                var members = row.ShapeIds;
                drawn.Add(BuildLightRow(
                    row.Name,
                    row.Caption,
                    FlagBoxInstallPlan.Combine(members.Select(id => byId[id])),
                    () => FlagBoxInstallPlan.Combine(Install(members.Select(id => json[id]).ToList())),
                    current => PanelLightRows.Tooltip(members.Count, current.State, current.InstalledVersion)));
            }
            return drawn;
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
                button.Click += (sender, args) => draw(press());
                actionHost.Child = button;
                row.ToolTip = tooltip(current);
                if (adopt != null) adopt(button);
            };
            draw(plan);
            return row;
        }

        /// <summary>What SimHub holds for each of the shapes given. An unreachable driver is a state a
        /// driver can do nothing about rather than an error, so it is reported as one.</summary>
        private static IList<FlagBoxPlan> Plan(IList<string> embedded)
        {
            try
            {
                return StripInstaller.Plan(embedded);
            }
            catch (Exception ex)
            {
                Log.Warn("Reading SimHub's LED profiles failed: " + ex.Message);
                return embedded.Select(x => new FlagBoxPlan { State = FlagBoxInstallState.Unavailable }).ToList();
            }
        }

        private static IList<FlagBoxPlan> Install(IList<string> embedded)
        {
            try
            {
                return StripInstaller.Install(embedded);
            }
            catch (Exception ex)
            {
                Log.Error("Installing the strip profiles into SimHub failed", ex);
                return embedded.Select(x => new FlagBoxPlan { State = FlagBoxInstallState.Failed }).ToList();
            }
        }

        /// <summary>Asks SimHub what it holds for the flag box, without touching it.</summary>
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

        private FlagBoxPlan InstallFlagBox()
        {
            try
            {
                return FlagBoxInstaller.Install(plugin.FlagBoxJson);
            }
            catch (Exception ex)
            {
                Log.Error("Installing the flag box profile failed", ex);
                return new FlagBoxPlan { State = FlagBoxInstallState.Failed };
            }
        }

        /// <summary>
        /// The by-hand route for the flag box, beneath the rows and only when the one-click one is not
        /// there at all.
        /// </summary>
        /// <remarks>
        /// ADR 0013 (docs/decisions/0013-lighting-hardware.md) keeps the extracted file precisely so that
        /// there is something to import when the matrix driver cannot be reached, so it could not go with
        /// the sentence it used to sit beside. It is under the rows rather than in one because the canvas
        /// draws a row as a name, a state and one button, and because it answers for the flag box alone:
        /// no strip profile is ever written to the openDash folder.
        ///
        /// Built only in the state that needs it, rather than built and hidden, so that a section with
        /// nothing to fall back to does not carry the gap of a block nobody can see.
        /// </remarks>
        private FrameworkElement BuildFlagBoxFallback(FlagBoxPlan plan)
        {
            flagBoxLine = Ui.Caption(FlagBoxInstallPlan.Summary(plan, plugin.FlagBox?.Path), BodyWidth);
            flagBoxCopyButton = BuildSecondaryButton(
                "Copy where SimHub looks",
                "Puts a copy in Documents\\SimHub, which is the folder SimHub's own profile import opens in.");
            flagBoxCopyButton.Click += (sender, args) => CopyFlagBoxForImport();
            var block = Ui.VStack(8, flagBoxLine, Ui.HStack(12, flagBoxCopyButton, FlagBoxPathBox()));
            block.HorizontalAlignment = HorizontalAlignment.Left;
            return block;
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

        private FrameworkElement FlagBoxPathBox()
        {
            var box = new TextBox
            {
                Width = 320,
                IsReadOnly = true,
                Text = plugin.FlagBox?.Path ?? string.Empty,
                ToolTip = "Where openDash left the profile.",
            };
            // The kit's field chrome, so a path that is read rather than typed still reads as the same
            // shape as the number boxes on the other tabs, and carries the ring a keyboard needs.
            Ui.Field(box, Theme.ControlHeightSm);
            flagBoxPath = box;
            return box;
        }
    }
}
