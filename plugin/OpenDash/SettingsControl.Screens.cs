// SettingsControl.Screens.cs: the Screens page -- a card per screen on the rig, and the selected screen's own
// settings under it: its header, its live preview, what needs fixing and the pane of its kind.
//
// This is the old Rig tab re-hosted by the #503 foundation so that every control keeps working while the
// Screens page agent rebuilds it to Screens.dc.html. The panes are the files beside this one:
// .Screens.Face.cs, .Screens.PitWall.cs, .Screens.Companion.cs and .Screens.Round.cs. The wheel buttons that
// were under a face moved to Shortcuts, where every binding now is; Add, Edit and Remove open in the sheet.
//
// A screen is the unit (ADR 0017). A face is configured on a picture of itself, because "zone C" means nothing
// until you see where zone C is.
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
        /// <summary>The screen whose settings are showing: the one selected, else the first. Null when the
        /// rig is empty.</summary>
        private ScreenInstance SelectedScreen
        {
            get
            {
                var rig = Settings.RigScreens();
                if (rig.Count == 0) return null;
                var chosen = Settings.ScreenByNamespace(Selected(PanelPage.Screens));
                return chosen ?? rig[0];
            }
        }

        /// <summary>The size an icon is drawn at when it stands beside a line of prose rather than inside a
        /// control, which is the second of the two sizes control.icon describes.</summary>
        private const double IconAlone = PanelIcons.SizeAlone;

        private FrameworkElement BuildScreensPage(PanelRoute to)
        {
            var rig = Settings.RigScreens();
            var sections = new List<UIElement> { Ui.Anchor(BuildScreenCards(rig), PanelScreens.AnchorCards) };
            if (rig.Count == 0)
            {
                sections.Add(BuildEmptyRig());
                return PageLayout(PanelScreens.Title, null, sections.ToArray());
            }
            if (PanelScreens.ShowsUnclaimedNote(rig)) sections.Add(BuildUnclaimedNote());

            var screen = SelectedScreen;
            var selected = new List<UIElement> { BuildScreenHeader(screen) };
            var fix = BuildScreenFix(screen);
            if (fix != null) selected.Add(fix);
            // The screen itself, between its name and the controls that change it. Null when its package is
            // not installed, which the fix above says in its own words.
            var preview = BuildScreenPreview(screen, ContentWidth);
            if (preview != null)
            {
                preview.Margin = new Thickness(0, 18, 0, 0);
                selected.Add(preview);
            }
            var pane = BuildScreenPane(screen);
            pane.Margin = new Thickness(0, 18, 0, 0);
            selected.Add(pane);

            var block = new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(0, 20, 0, 0),
                Child = Ui.VStack(0, selected.ToArray()),
            };
            sections.Add(block);
            return PageLayout(PanelScreens.Title, null, sections.ToArray());
        }

        /// <summary>The kind as a word, for the facts under a screen's name.</summary>
        private static string KindLabel(ScreenInstance screen)
        {
            if (screen.IsCompanion) return "Companion";
            if (screen.IsPitWall) return "Pit wall";
            return screen.IsSlots ? "Slots" : "Face";
        }

        /// <summary>
        /// The cards, one per screen, and the dashed tile that adds one: a picture of the screen's shape, its
        /// name, its kind and size, and whether SimHub has it.
        /// </summary>
        private FrameworkElement BuildScreenCards(IReadOnlyList<ScreenInstance> rig)
        {
            var current = SelectedScreen;
            var cards = new List<UIElement>();
            foreach (var screen in rig)
            {
                var captured = screen;
                var installed = Installed(captured);
                var restart = issues.Any(i => i.Id == "screen-restart:" + captured.Namespace);
                var state = !installed ? "Missing" : restart ? "Restart SimHub to load it" : "In SimHub";
                var stateHex = !installed ? Theme.StatusFailed : restart ? Theme.Caution : Theme.StatusUpToDate;
                var thumb = Ui.Thumb(captured.IsSlots ? "round" : captured.Kind, captured.Width, captured.Height);
                cards.Add(Ui.DeviceCard(
                    thumb,
                    captured.Name,
                    // A screen whose package is gone has no size to show, and "0 × 0" is worse than the folder.
                    KindLabel(captured) + " · " + (captured.Width > 0 ? captured.SizeLabel : (captured.Folder ?? string.Empty)),
                    state,
                    stateHex,
                    current != null && ReferenceEquals(current, captured),
                    () =>
                    {
                        Select(PanelPage.Screens, captured.Namespace);
                        Redraw();
                    }));
            }
            cards.Add(Ui.DashedAddCard(PanelAddScreen.SectionTitle, ShowAddScreen));
            return Ui.CardGrid(150, 10, 6, cards.ToArray());
        }

        private bool Installed(ScreenInstance screen)
        {
            try
            {
                return screen.Folder != null && PackageExtractor.IsInstalled(plugin.Installer.SimHubRoot, screen.Folder);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not tell whether " + screen.Folder + " is installed: " + ex.Message);
                return true;
            }
        }

        /// <summary>
        /// The first run, which is the empty state of the thing itself rather than a wizard in front of it.
        /// </summary>
        /// <remarks>
        /// #85's design, and the reason it is one fewer surface to build: nothing has to be dismissed,
        /// because the empty state stops appearing exactly when it stops being true.
        /// </remarks>
        private FrameworkElement BuildEmptyRig()
        {
            var pill = Ui.StatusPill(Theme.TextDim, "No screens yet", Theme.TextLabel);
            var text = Ui.Caption(PanelCopy.EmptyRig);
            return Ui.VStack(4, pill, text);
        }

        /// <summary>
        /// The line an upgrading user meets, and only them: a rig migrated from an older plugin holds cards
        /// for screens nobody owns. Nothing is deleted on their behalf (ADR 0017); the line goes when it stops
        /// being true rather than when somebody dismisses it. PanelScreens decides when that is.
        /// </summary>
        private FrameworkElement BuildUnclaimedNote()
        {
            var icon = Ui.NavIcon(PanelIcons.Warning, Theme.Caution, IconAlone);
            icon.VerticalAlignment = VerticalAlignment.Top;
            var text = Ui.Caption(PanelScreens.UnclaimedNote);
            return Ui.HStack(10, icon, text);
        }

        /// <summary>The name, the facts beside it, and the presses that act on the screen itself.</summary>
        private FrameworkElement BuildScreenHeader(ScreenInstance screen)
        {
            var title = Ui.SubHeading(screen.Name);
            var facts = Ui.Caption(KindLabel(screen) + (screen.Width > 0 ? " · " + screen.SizeLabel : string.Empty));
            facts.VerticalAlignment = VerticalAlignment.Bottom;
            facts.Margin = new Thickness(0, 0, 0, 3);
            var name = Ui.HStack(12, title, facts);

            var edit = Ui.Button("Edit", PanelButtonKind.Outline, PanelButtonSize.Small);
            edit.ToolTip = "Change this screen's name or size, or install its dashboard again.";
            edit.Click += (sender, args) => ShowEdit(screen);
            var duplicate = Ui.Button("Duplicate", PanelButtonKind.Outline, PanelButtonSize.Small);
            duplicate.ToolTip = "Adds a second screen set up like this one.";
            duplicate.Click += (sender, args) => DuplicateScreen(screen);
            var remove = Ui.Button("Remove", PanelButtonKind.GhostDanger, PanelButtonSize.Small);
            remove.ToolTip = "Removes this screen, its settings and its dashboard.";
            remove.Click += (sender, args) => ShowRemove(screen);

            // Where its properties live, quieter than the facts: ADR 0017 freezes the namespace at creation and
            // a rename does not move it, so a screen called "Rim" whose properties say MainDash has to say so.
            var origin = Ui.Caption((screen.Folder ?? "Not installed") + " · properties OpenDash." + screen.Namespace + "*");
            origin.Margin = new Thickness(0, 6, 0, 0);
            return Ui.VStack(0, Ui.Row(name, Ui.HStack(6, edit, duplicate, remove)), origin);
        }

        /// <summary>
        /// What is wrong with this screen, as a fix box: its folder is gone, or it was written after SimHub
        /// started and SimHub has not read it. Null when nothing is.
        /// </summary>
        private FrameworkElement BuildScreenFix(ScreenInstance screen)
        {
            if (!Installed(screen))
            {
                var write = Ui.Button(PanelAttention.InstallAgain, PanelButtonKind.Outline, PanelButtonSize.Small);
                write.ToolTip = "Puts this screen's dashboard back into SimHub.";
                write.Click += (sender, args) => WriteScreenAgain(screen);
                var box = Ui.FixBox("This screen's dashboard is missing from SimHub", "Its settings are kept.", null, write);
                box.Margin = new Thickness(0, 18, 0, 0);
                return box;
            }
            var restart = issues.FirstOrDefault(i => i.Id == "screen-restart:" + screen.Namespace);
            if (restart == null) return null;
            var fix = Ui.FixBox(restart.Title, restart.Detail, null, null, PanelIcons.Restart);
            fix.Margin = new Thickness(0, 18, 0, 0);
            return fix;
        }

        /// <summary>Writes a screen's dashboard again after its folder has gone.</summary>
        private void WriteScreenAgain(ScreenInstance screen)
        {
            var result = plugin.Installer.Write(screen);
            Save(screen);
            plugin.Installer.Refresh();
            Select(PanelPage.Screens, screen.Namespace);
            Redraw();
            if (!result.Ok) Log.Warn("Writing " + screen.Name + " again failed: " + result.Error);
            Say(result.Ok ? PanelAddScreen.Reinstalled(screen.Name) : PanelAddScreen.ReinstallFailed(screen.Name, result.Error), result.Ok);
        }

        private FrameworkElement BuildScreenPane(ScreenInstance screen)
        {
            if (screen.IsCompanion) return BuildCompanionPane(screen);
            if (screen.IsPitWall) return BuildPitWallPane(screen);
            if (screen.IsSlots) return BuildSlotsPane(screen);
            return BuildFacePane(screen);
        }

        // --- Adding, editing, duplicating and removing ---------------------------------------------------

        /// <summary>
        /// The add sheet: what kind of screen, what size, and what it is called.
        /// </summary>
        private void ShowAddScreen()
        {
            var catalogue = PackageCatalogue.From(plugin.Installer.PackageSource, new SimHubInstallLog());
            var types = PanelAddScreen.Types(catalogue);
            if (types.Count == 0)
            {
                ShowSheet(PanelAddScreen.SectionTitle, Ui.Caption("This build ships no dashboards."), null);
                return;
            }

            // The three answers, held here and read by whichever control last wrote one. The name is the only
            // one the driver types, so it is the only one that has to remember whether they have.
            var type = types[0];
            PackageEntry entry = PanelAddScreen.Offered(type)[PanelAddScreen.PreferredIndex(type)];
            var typed = false;

            var name = BuildNameBox(string.Empty);
            name.TextChanged += (sender, args) => typed = name.IsKeyboardFocusWithin;

            var sizeHost = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
            var note = Ui.Caption(string.Empty);

            Action fillName = () =>
            {
                // Only while the driver has not typed one of their own: a default that overwrites what
                // somebody has just written is worse than no default at all.
                if (typed) return;
                name.Text = PackageCatalogue.UniqueName(PanelAddScreen.DefaultName(entry), Settings.RigScreens().Select(s => s.Name));
            };
            Action refreshNote = () =>
            {
                var second = Settings.RigScreens().Any(s => string.Equals(s.Namespace, StockNamespaceOf(entry), StringComparison.Ordinal));
                note.Text = PanelAddScreen.Note(entry, second);
            };
            Action<PackageEntry> choose = chosen =>
            {
                entry = chosen;
                fillName();
                refreshNote();
            };
            Action showSize = () =>
            {
                var offered = PanelAddScreen.Offered(type);
                entry = offered[PanelAddScreen.PreferredIndex(type)];
                var question = PanelAddScreen.Question(type);
                sizeHost.Content = question == SizeQuestion.None ? null : BuildSizeRow(type, offered, question, PanelAddScreen.PreferredIndex(type), choose);
                fillName();
                refreshNote();
            };

            // What the chosen kind is, under the control that chose it: two words on a button cannot say what
            // a companion is, and a driver adding their first screen has nowhere else to find out.
            var typeCaption = Ui.Caption(type.Caption);
            typeCaption.Margin = new Thickness(0, 0, 0, 12);
            var typeRow = Ui.Row(
                PanelAddScreen.TypeTitle,
                PanelAddScreen.TypeCaption,
                BuildSegmented(
                    types.Select(t => t.Kind).ToArray(),
                    types.Select(t => t.Label).ToArray(),
                    type.Kind,
                    kind =>
                    {
                        type = types.First(t => string.Equals(t.Kind, kind, StringComparison.Ordinal));
                        typeCaption.Text = type.Caption;
                        showSize();
                    }));

            showSize();

            var add = Ui.Button(PanelAddScreen.AddButton, PanelButtonKind.Primary, PanelButtonSize.Large);
            add.MinWidth = ButtonMinWidth;
            add.ToolTip = "Creates the screen and installs its dashboard.";
            add.Click += (sender, args) => AddScreen(entry, name.Text);
            var cancel = Ui.Button("Cancel", PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.ToolTip = "Goes back without adding anything.";
            cancel.Click += (sender, args) => CloseSheet();

            var body = Ui.VStack(0,
                typeRow,
                typeCaption,
                sizeHost,
                Ui.Row(PanelAddScreen.NameTitle, PanelAddScreen.NameCaption, name),
                note);
            ShowSheet(PanelAddScreen.SectionTitle, body, SheetFooter(null, cancel, add));
        }

        /// <summary>The size or the orientation control, in the row the question calls for.</summary>
        private FrameworkElement BuildSizeRow(ScreenType type, IReadOnlyList<PackageEntry> offered, SizeQuestion question, int selected, Action<PackageEntry> chose)
        {
            var values = offered.Select((e, i) => i.ToString(CultureInfo.InvariantCulture)).ToArray();
            var labels = offered.Select((e, i) => PanelAddScreen.SizeLabel(type, e, i)).ToArray();
            Action<string> changed = value =>
            {
                int index;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out index)) return;
                if (index < 0 || index >= offered.Count) return;
                chose(offered[index]);
            };
            // Two answers are a pair of buttons; eight are a list. The orientation question is always the pair,
            // which is what makes it read as "which way round" rather than as a resolution.
            var opens = values[selected < 0 || selected >= values.Length ? 0 : selected];
            var control = question == SizeQuestion.Orientation || offered.Count <= 3
                ? (FrameworkElement)BuildSegmented(values, labels, opens, changed)
                : BuildChoice(values, labels, opens, 240, changed);
            return question == SizeQuestion.Orientation
                ? Ui.Row(PanelAddScreen.OrientationTitle, PanelAddScreen.OrientationCaption, control)
                : Ui.Row(PanelAddScreen.SizeTitle, PanelAddScreen.SizeCaption, control);
        }

        /// <summary>
        /// The one sheet for a screen that is already on the rig: its name, its size, and its dashboard.
        /// </summary>
        /// <remarks>
        /// The namespace is frozen at creation (ADR 0017) and none of the three moves it, so the settings and
        /// the bindings survive all of them and only the folder in DashTemplates is written.
        /// </remarks>
        private void ShowEdit(ScreenInstance screen)
        {
            var name = BuildNameBox(screen.Name);

            // The size question is asked only where this build has another size to offer, which is the same
            // rule the add sheet follows; a kind that ships one package draws no row at all.
            var catalogue = PackageCatalogue.From(plugin.Installer.PackageSource, new SimHubInstallLog());
            var type = PanelAddScreen.Types(catalogue).FirstOrDefault(t => string.Equals(t.Kind, screen.Kind, StringComparison.Ordinal));
            var question = type == null ? SizeQuestion.None : PanelAddScreen.Question(type);
            FrameworkElement sizeRow = null;
            PackageEntry chosen = null;
            if (question != SizeQuestion.None)
            {
                var offered = PanelAddScreen.Offered(type);
                var current = offered.FirstOrDefault(e => e.Width == screen.Width && e.Height == screen.Height) ?? offered[0];
                chosen = current;
                var opensOn = 0;
                for (var i = 0; i < offered.Count; i++)
                {
                    if (ReferenceEquals(offered[i], current)) opensOn = i;
                }
                sizeRow = BuildSizeRow(type, offered, question, opensOn, e => chosen = e);
            }

            var edited = Edited(screen);
            var reinstall = Ui.Button(PanelAddScreen.ReinstallButton, PanelButtonKind.Outline, PanelButtonSize.Small);
            reinstall.MinWidth = ButtonMinWidth;
            reinstall.ToolTip = "Writes this screen's dashboard into SimHub again.";
            reinstall.Click += (sender, args) => ReinstallScreen(screen);
            var reinstallRow = Ui.Row(
                PanelAddScreen.ReinstallTitle,
                edited ? PanelAddScreen.ReinstallEditedCaption : PanelAddScreen.ReinstallCaption,
                reinstall);

            var save = Ui.Button(PanelAddScreen.SaveButton, PanelButtonKind.Primary, PanelButtonSize.Large);
            save.MinWidth = ButtonMinWidth;
            save.ToolTip = "Applies the name and the size, and writes the dashboard.";
            save.Click += (sender, args) => SaveEdit(screen, name.Text, chosen);
            var cancel = Ui.Button("Cancel", PanelButtonKind.Ghost, PanelButtonSize.Large);
            cancel.ToolTip = "Goes back without changing anything.";
            cancel.Click += (sender, args) => CloseSheet();

            var rows = new List<UIElement> { Ui.Row(PanelAddScreen.NameTitle, PanelAddScreen.NameCaption, name) };
            if (sizeRow != null) rows.Add(sizeRow);
            rows.Add(reinstallRow);
            ShowSheet(PanelAddScreen.EditTitle + " " + screen.Name, Ui.VStack(0, rows.ToArray()), SheetFooter(PanelAddScreen.EditCaption, cancel, save));
        }

        /// <summary>
        /// Whether this screen's folder no longer matches what OpenDash last wrote into it, which is to say
        /// whether somebody has opened it in Dash Studio and saved. A folder that cannot be fingerprinted
        /// reads as unedited: the alternative is warning everybody whose disk could not be read.
        /// </summary>
        private bool Edited(ScreenInstance screen)
        {
            try
            {
                if (screen.Folder == null || plugin.Installer.Record == null) return false;
                var recorded = plugin.Installer.Record.Get(screen.Folder);
                if (recorded == null) return false;
                var now = FolderFingerprint.Of(PackageExtractor.InstalledFolder(plugin.Installer.SimHubRoot, screen.Folder));
                return now != null && !string.Equals(now, recorded, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not tell whether " + screen.Folder + " has been edited: " + ex.Message);
                return false;
            }
        }

        /// <summary>Applies whichever of the two answers was changed, and writes the dashboard when either
        /// was: SimHub lists a dashboard under its title, so a rename that stopped at the card left the
        /// screen listed under the name the driver had just stopped using.</summary>
        private void SaveEdit(ScreenInstance screen, string wanted, PackageEntry entry)
        {
            // Whatever was changed, even nothing: pressing Save on a screen's own edit sheet is the driver
            // saying that this one is theirs.
            screen.Keep();
            var sizeChanged = entry != null && (entry.Width != screen.Width || entry.Height != screen.Height);
            switch (PanelAddScreen.Edit(screen.Name, wanted, sizeChanged))
            {
                case ScreenEdit.Resize:
                    // The name first, so the folder ResizeScreen writes is titled with it rather than with the
                    // name the screen is about to stop having.
                    RenameScreen(screen, wanted);
                    ResizeScreen(screen, entry);
                    return;
                case ScreenEdit.Rename:
                    RenameScreen(screen, wanted);
                    Save();
                    var result = plugin.Installer.Write(screen);
                    Save();
                    plugin.Installer.Refresh();
                    Select(PanelPage.Screens, screen.Namespace);
                    Redraw();
                    Say(result.Ok ? PanelAddScreen.Renamed(screen.Name) : PanelAddScreen.RenameFailed(screen.Name, result.Error), result.Ok);
                    return;
                default:
                    Save();
                    Redraw();
                    return;
            }
        }

        /// <summary>Takes the name from the box, kept distinct from every other screen's. An empty box keeps
        /// the name it had, which is what PanelAddScreen.Edit has already decided.</summary>
        private void RenameScreen(ScreenInstance screen, string wanted)
        {
            var trimmed = (wanted ?? string.Empty).Trim();
            if (trimmed.Length == 0 || string.Equals(trimmed, screen.Name, StringComparison.Ordinal)) return;
            screen.Name = PackageCatalogue.UniqueName(
                trimmed,
                Settings.RigScreens().Where(s => !ReferenceEquals(s, screen)).Select(s => s.Name));
        }

        /// <summary>Writes this one screen's dashboard again, at the name and size it already has: the repair
        /// for a dashboard that is there but wrong.</summary>
        private void ReinstallScreen(ScreenInstance screen)
        {
            var result = plugin.Installer.Write(screen);
            Save(screen);
            plugin.Installer.Refresh();
            Select(PanelPage.Screens, screen.Namespace);
            Redraw();
            Say(result.Ok ? PanelAddScreen.Reinstalled(screen.Name) : PanelAddScreen.ReinstallFailed(screen.Name, result.Error), result.Ok);
        }

        private void ResizeScreen(ScreenInstance screen, PackageEntry entry)
        {
            if (entry == null || (entry.Width == screen.Width && entry.Height == screen.Height))
            {
                Redraw();
                return;
            }
            var log = new SimHubInstallLog();
            // The old folder first: a screen that was the stock one at its old size owns that package's own
            // folder, and leaving it behind would put a dashboard in SimHub's list that nothing answers for.
            var old = ScreenInstaller.Remove(screen, plugin.Installer.SimHubRoot, log);
            if (!old.Ok) Log.Warn("The old folder of " + screen.Name + " could not be removed: " + old.Error);

            Settings.ResizeScreen(screen, entry);
            Save();
            var result = plugin.Installer.Write(screen);
            Save();
            plugin.Installer.Refresh();
            Select(PanelPage.Screens, screen.Namespace);
            Redraw();
            Say(result.Ok ? PanelAddScreen.Resized(screen.Name, screen.SizeLabel, screen.Name) : PanelAddScreen.ResizeFailed(screen.Name, result.Error), result.Ok);
        }

        private static string StockNamespaceOf(PackageEntry entry)
        {
            var probe = new ScreenInstance { Kind = entry.Kind, Width = entry.Width, Height = entry.Height, Folder = entry.Folder };
            return probe.StockNamespace;
        }

        private void AddScreen(PackageEntry entry, string name)
        {
            var screen = Settings.AddScreen(entry, name);
            Save();
            var result = plugin.Installer.Write(screen);
            Save();
            plugin.Installer.Refresh();
            Select(PanelPage.Screens, screen.Namespace);
            Redraw();

            // Said at the moment it becomes true rather than left to be found: SimHub reads its template list
            // once, at startup, and assigning a dashboard to a display is in another part of SimHub entirely.
            // The dashboard is listed under its title, which is the name the driver just chose.
            Say(result.Ok ? PanelAddScreen.Added(screen.Name, screen.Name) : PanelAddScreen.AddFailed(screen.Name, result.Error), result.Ok);
        }

        /// <summary>
        /// A second screen set up like this one: its settings copied, its bindings not (they are bound to the
        /// source's actions), under a name, a namespace and a folder of its own.
        /// </summary>
        private void DuplicateScreen(ScreenInstance screen)
        {
            var catalogue = PackageCatalogue.From(plugin.Installer.PackageSource, new SimHubInstallLog());
            var copy = Settings.DuplicateScreen(screen.Namespace, catalogue);
            if (copy == null)
            {
                Say(PanelMessage.Caution("Could not duplicate " + screen.Name + "."));
                return;
            }
            Save();
            var result = plugin.Installer.Write(copy);
            Save();
            plugin.Installer.Refresh();
            Select(PanelPage.Screens, copy.Namespace);
            Redraw();
            Say(result.Ok ? PanelAddScreen.Added(copy.Name, copy.Name) : PanelAddScreen.AddFailed(copy.Name, result.Error), result.Ok);
        }

        /// <summary>
        /// The remove sheet, which says the two things that are easy to miss: the folder goes, and a wheel
        /// button bound to this screen's actions stops doing anything, because the action is no longer
        /// registered. ADR 0017 accepts that cost and this is where it is paid.
        /// </summary>
        private void ShowRemove(ScreenInstance screen)
        {
            var bound = screen.IsFace || screen.IsPitWall || screen.IsCompanion
                ? " Any button you bound to it stops working."
                : string.Empty;
            var remove = Ui.Button("Remove it", PanelButtonKind.Danger, PanelButtonSize.Large);
            remove.ToolTip = "Removes the screen, its settings and its dashboard.";
            remove.Click += (sender, args) =>
            {
                var result = ScreenInstaller.Remove(screen, plugin.Installer.SimHubRoot, new SimHubInstallLog());
                Settings.RemoveScreen(screen.Namespace);
                Save();
                plugin.Installer.Refresh();
                Select(PanelPage.Screens, null);
                Redraw();
                Say(result.Ok
                        ? "Removed " + screen.Name + ". SimHub still lists its dashboard until you restart it."
                        : "Removed " + screen.Name + ", but its dashboard could not be deleted: " + result.Error,
                    result.Ok);
            };
            var keep = Ui.Button("Keep it", PanelButtonKind.Ghost, PanelButtonSize.Large);
            keep.ToolTip = "Leaves this screen alone.";
            // The answer to the question the line over the cards asks of a migrated screen, as much as Remove
            // it is, so it keeps the screen as well as going back.
            keep.Click += (sender, args) =>
            {
                Save(screen);
                Redraw();
            };
            ShowSheet("Remove " + screen.Name, Ui.Prose("Removes the screen, its dashboard and its settings." + bound, Theme.SizeBody),
                SheetFooter(null, keep, remove));
        }
    }
}
