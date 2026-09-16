// SettingsControl.Install.Packages.cs: the package rows on the Install tab -- one row per package the
// build carries, drawn in the shape the canvas gives it.
//
// Lifted out of SettingsControl.Install.cs so that the rest of that file stays about the plugin itself,
// its reinstall and its update check. What a row says and what it counts is in PanelPackageRow.cs, which
// the test project compiles; this file is the drawing and nothing else.
using System.Collections.Generic;
using System.Windows;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>
        /// One row per package the build carries: what it is, whether the rig has it, and a button.
        /// </summary>
        /// <remarks>
        /// The pill and the count answer two different questions and the row carries both. The pill is
        /// whether DashTemplates holds the folder; the count, which the tooltip keeps, is how many screens
        /// were made from the package, and under ADR 0017 a package can back more than one. Dropping
        /// either would leave the row saying less than it says today.
        ///
        /// Add here does exactly what Add on the Rig tab does, because they are one list seen from two
        /// sides.
        /// </remarks>
        private FrameworkElement BuildPackageSection()
        {
            var catalogue = PackageCatalogue.From(plugin.Installer.PackageSource, new SimHubInstallLog());
            var caption = Ui.Caption(
                "One package per size, installed into SimHub DashTemplates. Adding one here makes it a screen; "
                + "its settings are its own.",
                BodyWidth);
            if (catalogue.Count == 0)
            {
                return Ui.Section("Screens openDash can install",
                    caption,
                    Ui.Caption("This build of openDash carries no dashboard packages. See plugin/OpenDash/Resources/README.md.", BodyWidth));
            }

            var rows = new List<UIElement>();
            foreach (var entry in catalogue) rows.Add(BuildPackageRow(entry));
            // The rows go in as one child rather than as a child each: a section holds what it is given
            // twenty apart, and an install row is separated by its own rule and by nothing else.
            return Ui.Section("Screens openDash can install", caption, Ui.VStack(0, rows.ToArray()));
        }

        private FrameworkElement BuildPackageRow(PackageEntry entry)
        {
            var captured = entry;
            var uses = PanelPackageRow.Uses(captured, Settings.RigScreens());
            var installed = captured.Folder != null
                && PackageExtractor.IsInstalled(plugin.Installer.SimHubRoot, captured.Folder);
            var state = PanelCopy.ScreenRow(installed);

            var add = Ui.OutlineButton(PanelPackageRow.Verb(uses), PanelMetrics.RowButtonHeight);
            add.MinWidth = ButtonMinWidth;
            add.ToolTip = "Make a screen from this package.";
            add.Click += (sender, args) => AddScreen(captured, null);

            // The dot and the ink part company only where nothing is installed: the canvas draws that dot
            // in status.notInstalled and its label in text.label, which is the one pairing a table
            // carrying a single colour per state cannot say on its own.
            var dot = installed ? state.StateHex : Theme.StatusNotInstalled;

            var row = Ui.InstallRow(
                PanelMetrics.KindIcon(captured.Kind),
                PanelPackageRow.Name(captured),
                PanelPackageRow.Caption(captured),
                Ui.StatusPill(dot, state.State, state.StateHex),
                add);
            row.ToolTip = PanelPackageRow.Tooltip(captured.Folder, uses);
            return row;
        }
    }
}
