// SettingsControl.Matrix.cs: the Matrix page -- the flag box profile in the page's header, one group per
// matrix panel, and adding, renaming and removing one.
//
// Re-hosted by the #503 foundation from the old Lights tab's matrix groups and the Install tab's flag box row,
// so every control keeps working while the Matrix page agent rebuilds it to Matrix.dc.html. The profile's
// state is asked of SimHub on each draw and only a press writes it (ADR 0013). ADR 0013 is why the page
// exists; docs/design/flag-box.md is what the box draws.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private FrameworkElement BuildMatrixPage(PanelRoute to)
        {
            var panels = Settings.MatrixPanels().ToList();
            var groups = new List<UIElement>();
            var caption = Ui.Caption(PanelLights.PanelsCaption);
            caption.Margin = new Thickness(0, 0, 0, 12);
            groups.Add(caption);
            if (panels.Count == 0) groups.Add(Ui.Caption(PanelLights.NoPanels));
            var selected = Selected(PanelPage.Matrix);
            foreach (var matrix in panels)
            {
                var open = selected == null ? matrix == panels[0] : selected == matrix.ToString(System.Globalization.CultureInfo.InvariantCulture);
                groups.Add(BuildMatrixGroup(matrix, open));
            }
            groups.Add(BuildAddMatrixRow());

            var box = PageSection("The flag box",
                // It names the profile. A panel below carries the name its driver gave it and SimHub's matrix
                // list carries this one, so a page that mentioned neither left somebody looking for their own
                // name on SimHub's Arduino page and finding an OpenDash profile instead.
                Ui.Caption(PanelLights.BoxCaption(FlagBoxName())),
                // The rig's answer to "is a car alongside" slides in or snaps, on every panel alike.
                Ui.Anchor(Ui.Row("Spotter bar animation", "The bar slides in from the edge.", BuildToggle(Settings.FlagBoxSpotterAnimation, on => { Settings.FlagBoxSpotterAnimation = on; Save(); })), PanelMatrix.AnchorSpotterAnimation));

            return PageLayout(PanelMatrix.Title, null,
                Ui.Anchor(BuildFlagBoxProfile(), PanelMatrix.AnchorProfile),
                box,
                Ui.Anchor(PageSection(PanelLights.PanelsTitle, groups.ToArray()), PanelMatrix.AnchorPanels));
        }

        /// <summary>
        /// The flag box profile as SimHub holds it: its name, its state, and the one press that changes it,
        /// with the by-hand route under it when SimHub's matrix settings cannot be reached at all.
        /// </summary>
        private FrameworkElement BuildFlagBoxProfile()
        {
            if (plugin.FlagBoxJson == null) return Ui.Caption("This build ships no flag box profile.");
            var plan = SafePlan();
            var pillHost = new Border { VerticalAlignment = VerticalAlignment.Center };
            var actionHost = new Border { VerticalAlignment = VerticalAlignment.Center };
            var row = Ui.InstallRow(null, FlagBoxName(), PanelLightRows.FlagBoxCaption, pillHost, actionHost);
            Action<FlagBoxPlan> draw = null;
            draw = current =>
            {
                var state = PanelCopy.LightRow(current.State, current.InstalledVersion);
                pillHost.Child = Ui.StatusPill(PanelLightRows.DotHex(current.State), state.State, state.StateHex);
                var button = state.Style == PanelButton.Primary
                    ? Ui.Button(state.Button, PanelButtonKind.Primary, PanelButtonSize.Small)
                    : Ui.Button(state.Button, PanelButtonKind.Outline, PanelButtonSize.Small);
                button.MinWidth = ButtonMinWidth;
                // Nothing to press when there is no profile to install or nowhere to put it.
                button.IsEnabled = current.State != FlagBoxInstallState.NotEmbedded && current.State != FlagBoxInstallState.Unavailable;
                button.ToolTip = "Adds OpenDash's profile. Your own profiles are never changed.";
                button.Click += (sender, args) =>
                {
                    // Redrawn from what the press reported rather than from a fresh read of SimHub.
                    draw(InstallFlagBox());
                    RefreshAttention();
                    RefreshSidebar();
                };
                actionHost.Child = button;
                row.ToolTip = FlagBoxInstallPlan.Summary(current, plugin.FlagBox?.Path);
            };
            draw(plan);
            if (plan.State != FlagBoxInstallState.Unavailable) return row;
            return Ui.VStack(12, row, BuildFlagBoxImportFallback(plan));
        }

        /// <summary>
        /// The press that adds a panel. It disappears when all four slots are taken rather than failing on the
        /// press, because SimHub composes four contents and not five.
        /// </summary>
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
            add.Margin = new Thickness(0, 12, 0, 0);
            add.ToolTip = "Adds a matrix panel.";
            add.Click += (sender, args) => ShowAddMatrixPanel();
            return add;
        }

        /// <summary>Naming the panel, in the sheet, the way a screen is named when it is added.</summary>
        private void ShowAddMatrixPanel()
        {
            var slot = Settings.FreeMatrixSlot();
            if (slot == 0) return;
            var name = BuildNameBox("Matrix " + slot);
            var add = Ui.Button(PanelLights.AddPanel, PanelButtonKind.Primary, PanelButtonSize.Large);
            add.Click += (sender, args) =>
            {
                var added = Settings.AddMatrixPanel(name.Text);
                Save();
                if (added != 0) Select(PanelPage.Matrix, added.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Redraw();
                if (added == 0) return;
                // Adding is not the last step, and the step that is left is on SimHub's page rather than this
                // one. Nothing was installed here -- the flag box profile paints all four panels and is
                // installed once -- so the line names that profile and says whether SimHub has it.
                var state = SafePlan().State;
                Say(new PanelMessage(
                    PanelLights.PanelAdded(Settings.MatrixName(added), added, FlagBoxName(), state),
                    PanelLights.PanelNeedsInstall(state) ? PanelTone.Caution : PanelTone.Info));
            };
            var cancel = Ui.Button("Cancel", PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.Click += (sender, args) => CloseSheet();
            var body = Ui.VStack(12,
                Ui.Caption(PanelLights.AddPanelCaption(slot, FlagBoxName())),
                Ui.Row(PanelLights.PanelNameTitle, PanelLights.PanelNameCaption, name));
            ShowSheet(PanelLights.AddPanel, body, SheetFooter(null, cancel, add));
        }

        /// <summary>
        /// One matrix: what it shows at rest, what may take it over, and which side it is on, with a picture
        /// of what it shows at rest drawn from the flag box's own glyphs.
        /// </summary>
        private FrameworkElement BuildMatrixGroup(int matrix, bool open)
        {
            var m = matrix;
            var summary = PanelLights.PanelSlot(m);
            return Ui.Collapsible(Settings.MatrixName(m) ?? ("Matrix " + m), summary, open, () =>
            {
                var rest = BuildSegmented(Contract.FlagBoxRests, PanelLights.RestLabels, Settings.MatrixRest(m), value =>
                {
                    // Through the setter rather than into the array, because the deprecated Gear switch has to
                    // move with it or the collapse in Normalise() undoes this on the next load.
                    Settings.SetMatrixRest(m, value);
                    Save();
                    Redraw();
                });
                var side = BuildSegmented(Contract.FlagBoxSides, PanelLights.SideLabels, Settings.MatrixSide(m), value =>
                {
                    Settings.FlagBoxSide[m - 1] = value;
                    Save();
                });
                var options = new MatrixOptions
                {
                    Rest = Settings.MatrixRest(m),
                    Side = Settings.MatrixSide(m),
                    Bands = Settings.MatrixGearBands(m),
                    CarLadder = Settings.MatrixGearCarLadder(m),
                };
                var preview = Ui.Matrix(PanelEmulation.MatrixFrame(GlyphSheet, PanelEmulation.Idle, options), MatrixStyle.Home,
                    PanelEmulation.Dim(Settings.LightsNightMode, Settings.LightsNightBrightness));
                preview.Margin = new Thickness(0, 0, 0, 12);
                return Ui.VStack(0,
                    preview,
                    Ui.Row("Idle display", null, rest),
                    Ui.Row("Race flags", null, BuildToggle(Settings.MatrixFlags(m), on => { Settings.FlagBoxFlags[m - 1] = on; Save(); })),
                    Ui.Row("Pit status", "Limiter, pit lane and speeding.", BuildToggle(Settings.MatrixPit(m), on => { Settings.FlagBoxPit[m - 1] = on; Save(); })),
                    Ui.Row("Spotter", "Warns about cars alongside.", BuildToggle(Settings.MatrixSpotter(m), on => { Settings.FlagBoxSpotter[m - 1] = on; Save(); })),
                    Ui.Row("Car warnings", "Low fuel, oil and water.", BuildToggle(Settings.MatrixWarnings(m), on => { Settings.FlagBoxWarnings[m - 1] = on; Save(); })),
                    // Which side the box is physically on. One to the left of the wheel lighting for a car on
                    // the right is worse than no box at all, so it is asked rather than guessed.
                    Ui.Row("Mounting side", "Only lights for cars on this side.", side),
                    Ui.Row("Critical flags only", "Stays dark for the chequer, white, green and start gantry.", BuildToggle(Settings.MatrixCriticalOnly(m), on => { Settings.FlagBoxMatrixCriticalOnly[m - 1] = on; Save(); })),
                    Ui.Row("Shift colours", "Off keeps the gear one colour as the revs rise.", BuildToggle(Settings.MatrixGearBands(m), on => { Settings.FlagBoxMatrixGearBands[m - 1] = on; Save(); })),
                    Ui.Row("Redline flash", "Off keeps the gear steady and red.", BuildToggle(Settings.MatrixGearBlink(m), on => { Settings.FlagBoxMatrixGearBlink[m - 1] = on; Save(); })),
                    // The same answer the strips give, offered here because the digit is the one other thing on
                    // the rig those tables can colour.
                    Ui.Row("Car-specific thresholds", "Colours change where this car's own lights do. Falls back when it has no table.", BuildToggle(Settings.MatrixGearCarLadder(m), on => { Settings.FlagBoxMatrixGearCarLadder[m - 1] = on; Save(); })),
                    BuildMatrixPanelActions(m));
            }, opened => { if (opened) Select(PanelPage.Matrix, m.ToString(System.Globalization.CultureInfo.InvariantCulture)); });
        }

        /// <summary>Renaming a panel and taking it away, at the foot of its own group.</summary>
        private FrameworkElement BuildMatrixPanelActions(int matrix)
        {
            var m = matrix;
            var rename = Ui.Button("Rename", PanelButtonKind.Outline, PanelButtonSize.Small);
            rename.ToolTip = "Renames this panel.";
            rename.Click += (sender, args) => ShowRenameMatrixPanel(m);
            var remove = Ui.Button("Remove", PanelButtonKind.GhostDanger, PanelButtonSize.Small);
            remove.ToolTip = "Removes this panel and frees its slot.";
            remove.Click += (sender, args) =>
            {
                Settings.RemoveMatrixPanel(m);
                Save();
                Select(PanelPage.Matrix, null);
                Redraw();
            };
            var row = Ui.HStack(6, rename, remove);
            row.HorizontalAlignment = HorizontalAlignment.Right;
            row.Margin = new Thickness(0, 12, 0, 8);
            return row;
        }

        private void ShowRenameMatrixPanel(int matrix)
        {
            var name = BuildNameBox(Settings.MatrixName(matrix) ?? string.Empty);
            var save = Ui.Button("Rename", PanelButtonKind.Primary, PanelButtonSize.Large);
            save.Click += (sender, args) =>
            {
                Settings.RenameMatrixPanel(matrix, name.Text);
                Save();
                Redraw();
            };
            var cancel = Ui.Button("Cancel", PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.Click += (sender, args) => CloseSheet();
            ShowSheet("Rename " + (Settings.MatrixName(matrix) ?? ("Matrix " + matrix)),
                Ui.Row(PanelLights.PanelNameTitle, PanelLights.PanelNameCaption, name),
                SheetFooter(null, cancel, save));
        }
    }
}
