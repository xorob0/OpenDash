// SettingsControl.Rig.cs: the Rig tab -- the row of screen cards, and the pane of whichever one is
// selected.
//
// A screen is the unit (ADR 0017). A face is configured on a picture of itself, because "zone C" means
// nothing until you see where zone C is; a pit wall gets a picture of its three pages for the same
// reason; a companion gets its rotation. The wheel buttons are on the screen they cycle, which is what
// lets a second face sit still while the one in front of the driver cycles.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>The namespace of the screen whose pane is showing. Null means the first one.</summary>
        private string selected;

        private readonly Dictionary<string, ComboBox> zoneSelects = new Dictionary<string, ComboBox>();
        private readonly Dictionary<string, ToggleButton> zoneMaskButtons = new Dictionary<string, ToggleButton>();
        private readonly Dictionary<string, List<CheckBox>> zoneMaskBoxes = new Dictionary<string, List<CheckBox>>();
        private readonly Dictionary<string, ToggleButton> barEndButtons = new Dictionary<string, ToggleButton>();
        private TextBlock faceWarningText;
        private FrameworkElement faceWarningRow;
        private TextBlock stripCaption;

        /// <summary>The screen the pane belongs to, or null when the rig is empty.</summary>
        private ScreenInstance Selected
        {
            get
            {
                var rig = Settings.RigScreens();
                if (rig.Count == 0) return null;
                var chosen = Settings.ScreenByNamespace(selected);
                return chosen ?? rig[0];
            }
        }

        /// <summary>The size an icon is drawn at when it stands beside a line of prose rather than inside a
        /// control, which is the second of the two sizes control.icon describes.</summary>
        // TODO: read this from Theme once design/tokens.json carries the standing-alone size as a token of
        // its own; control.icon mirrors the 16 and says the 20 in its description only.
        private const double IconAlone = 20;

        private FrameworkElement BuildRigTab()
        {
            var rig = Settings.RigScreens();
            var rows = new List<UIElement> { BuildCardRow(rig) };
            if (rig.Count == 0)
            {
                rows.Add(BuildEmptyRig());
                return Ui.VStack(0, Ui.Section("Your rig", rows.ToArray()));
            }
            if (rig.Count > 4) rows.Add(BuildCrowdedRigNote());

            var screen = Selected;
            rows.Add(BuildScreenHeader(screen));
            // The pane takes a section of its own rather than a place inside "Your rig". Ui.Section nests
            // perfectly well -- it is a rule and a label with no indent -- but the pane already carries a
            // section of its own in the wheel buttons, and a heading that sits one level deeper than the
            // heading beneath it reads as a mistake.
            // A pit wall brings its own two headings, "Where the zones are" over the picture and "What
            // each zone shows" over the rows, because one wrapper here could only ever carry one of
            // them and the canvas draws both. Every other kind takes a single heading from here.
            var pane = BuildScreenPane(screen);
            return Ui.VStack(0,
                Ui.Section("Your rig", rows.ToArray()),
                screen.IsPitWall ? pane : Ui.Section(PaneTitle(screen), pane));
        }

        /// <summary>
        /// The heading over the selected screen's pane, which names what the pane is a list of.
        /// </summary>
        /// <remarks>
        /// A pit wall is not here: it carries its own two headings, so this is never asked for one. Slots
        /// is the kind the canvas never drew, and it leaves with #146, so it borrows the shape of the
        /// face's heading rather than being given a design of its own.
        /// </remarks>
        private static string PaneTitle(ScreenInstance screen)
        {
            if (screen.IsCompanion) return "Modules in the rotation";
            if (string.Equals(screen.Kind, Contract.KindSlots, StringComparison.Ordinal)) return "What each slot shows";
            return "What each zone shows";
        }

        /// <summary>The cards, wrapped, and the add card after them.</summary>
        private FrameworkElement BuildCardRow(IReadOnlyList<ScreenInstance> rig)
        {
            var wrap = new WrapPanel { Width = BodyWidth, HorizontalAlignment = HorizontalAlignment.Left };
            var current = Selected;
            foreach (var screen in rig)
            {
                var captured = screen;
                var missing = !Installed(captured);
                var card = Ui.Card(
                    captured.Name,
                    // A screen whose package is gone has no size to show, and "0 × 0" is worse than
                    // the folder it lives in.
                    captured.Width > 0 ? captured.SizeLabel : (captured.Folder ?? string.Empty),
                    // The kind itself rather than a word for it: the card draws the icon the canvas gives
                    // it, and writes the word only for the kind that has none.
                    captured.Kind,
                    current != null && ReferenceEquals(current, captured),
                    missing ? Theme.StatusFailed : null,
                    () =>
                    {
                        selected = captured.Namespace;
                        Redraw();
                    });
                // The card holds a minimum width rather than a fixed one, so a long name widens it rather
                // than being cut short. Rename accepts a name of any length, though, and a card wider than
                // the row it wraps inside is arranged past the panel's edge instead of wrapping; the row is
                // therefore the ceiling, and a name that reaches it ellipsises, which is what the card's
                // trimming is there for.
                card.MaxWidth = BodyWidth;
                wrap.Children.Add(card);
            }
            wrap.Children.Add(Ui.AddCard(ShowAddScreen));
            return wrap;
        }

        /// <summary>The kind as a word, for the fact line under the screen's name. The card draws an icon
        /// instead, so this is the one place the kind is still spelled out.</summary>
        private static string KindLabel(ScreenInstance screen)
        {
            if (screen.IsCompanion) return "companion";
            if (screen.IsPitWall) return "pit wall";
            return string.Equals(screen.Kind, Contract.KindSlots, StringComparison.Ordinal) ? "slots" : "face";
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
            // The pill states the rig is empty, so the sentence under it no longer says so as well: it
            // is the explanation of what adding a screen does, not a second announcement.
            var text = Ui.Caption(PanelCopy.EmptyRig);
            var stack = Ui.VStack(4, pill, text);
            stack.Margin = new Thickness(0, 4, 0, 0);
            return stack;
        }

        /// <summary>
        /// The line an upgrading user meets, and only them.
        /// </summary>
        /// <remarks>
        /// Older versions installed every package the plugin embeds, so a rig migrated from one of them
        /// holds a dozen cards for screens nobody owns. Nothing is deleted on their behalf (ADR 0017),
        /// so the panel says what to do instead, and the line goes when it stops being true rather than
        /// when somebody dismisses it.
        /// </remarks>
        private FrameworkElement BuildCrowdedRigNote()
        {
            var icon = Ui.Icon(Ui.WarningIcon, Theme.Caution, IconAlone);
            icon.VerticalAlignment = VerticalAlignment.Top;
            var text = Ui.Caption(
                "openDash used to install every dashboard it ships, which is why there are so many here. "
                + "Remove the ones you have no screen for: it tidies SimHub's dashboard list too, and nothing "
                + "else is touched.");
            var row = Ui.HStack(10, icon, text);
            row.Margin = new Thickness(0, 4, 0, 4);
            return row;
        }

        /// <summary>The name, the facts under it, and the two buttons that act on the screen itself.</summary>
        private FrameworkElement BuildScreenHeader(ScreenInstance screen)
        {
            var title = Ui.Text(screen.Name, Theme.SizeTitle, FontWeights.SemiBold, Theme.TextPrimary);
            var facts = Ui.Label(ScreenFacts(screen));
            // The folder and the namespace, quieter than the two facts above them because the canvas draws
            // neither. The namespace is here because ADR 0017 freezes it at creation and a rename does not
            // move it, so a screen called "Rim" whose properties say MainDash has to be able to say so
            // rather than leave it to be discovered.
            var origin = Ui.Caption((screen.Folder ?? "not installed") + " · properties OpenDash." + screen.Namespace + "*");
            var text = Ui.VStack(4, title, facts, origin);
            text.HorizontalAlignment = HorizontalAlignment.Left;

            var rename = Ui.LinkButton("Rename");
            rename.ToolTip = "Change what this screen is called here and in SimHub's dashboard list.";
            rename.Click += (sender, args) => ShowRename(screen);
            // Beside Rename, because it is the same kind of correction: a driver who picked the wrong
            // size once had to remove the screen and start again, which threw away the zones they had set
            // and every wheel button bound to it.
            var resize = Ui.LinkButton("Change the size");
            resize.ToolTip = "Move this screen to another of the sizes openDash draws for, keeping its settings.";
            resize.Click += (sender, args) => ShowResize(screen);
            // Text and not a button face, which is what the canvas draws. What keeps a quiet destructive
            // action from being an accident is the confirmation behind it rather than its own weight.
            var remove = Ui.LinkButton("Remove this screen", Theme.Danger);
            remove.ToolTip = "Remove this screen, its settings and its dashboard folder.";
            remove.Click += (sender, args) => ShowRemove(screen);

            var actions = Resizable(screen) ? Ui.HStack(12, rename, resize, remove) : Ui.HStack(12, rename, remove);
            var rows = new List<UIElement> { Ui.Row(text, actions) };
            if (!Installed(screen)) rows.Add(BuildMissingFolder(screen));
            var stack = Ui.VStack(10, rows.ToArray());
            stack.Margin = new Thickness(0, 8, 0, PanelMetrics.SectionGap);
            // The rule closes the header rather than opening the pane: the pane's own section draws its
            // own, and the two say different things about what they separate.
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, 0, 0, PanelMetrics.BorderWeight),
                Child = stack,
            };
        }

        /// <summary>Whether this build carries another size for this kind of screen, which is the only
        /// case where offering to change it would lead anywhere.</summary>
        private bool Resizable(ScreenInstance screen)
        {
            try
            {
                var catalogue = PackageCatalogue.From(plugin.Installer.PackageSource, new SimHubInstallLog());
                var type = PanelAddScreen.Types(catalogue).FirstOrDefault(t => string.Equals(t.Kind, screen.Kind, StringComparison.Ordinal));
                return type != null && PanelAddScreen.Question(type) != SizeQuestion.None;
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read the packages to see whether " + screen.Name + " has another size: " + ex.Message);
                return false;
            }
        }

        /// <summary>The two facts the canvas puts under a screen's name, in the order it puts them. Drawn
        /// through Ui.Label, which upper-cases them.</summary>
        private static string ScreenFacts(ScreenInstance screen)
        {
            return KindLabel(screen) + (screen.Width > 0 ? " · " + screen.SizeLabel : string.Empty);
        }

        /// <summary>
        /// A screen whose folder has gone: marked, and offered back rather than dropped.
        /// </summary>
        /// <remarks>
        /// Removing the card would destroy the zone setup behind it and hide the thing that needs
        /// fixing, which is the reasoning #176 already applies to a failed install.
        /// </remarks>
        private FrameworkElement BuildMissingFolder(ScreenInstance screen)
        {
            var icon = Ui.Icon(Ui.WarningIcon, Theme.Caution, IconAlone);
            icon.VerticalAlignment = VerticalAlignment.Top;
            var text = Ui.Caption(
                "This screen's dashboard is not in SimHub: " + (screen.Folder ?? "it has no folder")
                    + " is missing. Your settings for it are kept.");
            var write = BuildSecondaryButton("Install it again", "Write this screen's dashboard back into SimHub DashTemplates.");
            write.Click += (sender, args) =>
            {
                var result = ScreenInstaller.Write(screen, plugin.Installer.PackageSource, plugin.Installer.SimHubRoot, plugin.Installer.Record, new SimHubInstallLog(), force: true);
                Save();
                plugin.Installer.Refresh();
                Redraw();
                if (!result.Ok) Log.Warn("Writing " + screen.Name + " again failed: " + result.Error);
            };
            return Ui.Row(Ui.HStack(10, icon, text), write);
        }

        private FrameworkElement BuildScreenPane(ScreenInstance screen)
        {
            if (screen.IsCompanion) return BuildCompanionPane(screen);
            if (screen.IsPitWall) return BuildPitWallPane(screen);
            if (string.Equals(screen.Kind, Contract.KindSlots, StringComparison.Ordinal)) return BuildSlotsPane();
            return BuildFacePane(screen);
        }

        // --- Adding, renaming and removing ------------------------------------------------------

        /// <summary>
        /// The add panel, in place rather than in a dialog.
        /// </summary>
        /// <remarks>
        /// A modal in a settings page is worse than a panel that appears where the card was, which is
        /// the same reasoning the update button's two-click confirmation already follows.
        /// </remarks>
        private void ShowAddScreen()
        {
            var catalogue = PackageCatalogue.From(plugin.Installer.PackageSource, new SimHubInstallLog());
            var types = PanelAddScreen.Types(catalogue);
            if (types.Count == 0)
            {
                bodyHost.Content = Ui.VStack(0, Ui.Section(PanelAddScreen.SectionTitle,
                    Ui.Caption("This build of openDash carries no dashboard packages, so there is nothing to add. "
                        + "See plugin/OpenDash/Resources/README.md."),
                    BackRow()));
                return;
            }

            // The three answers, held here and read by whichever control last wrote one. The name is the
            // only one the driver types, so it is the only one that has to remember whether they have.
            var type = types[0];
            PackageEntry entry = PanelAddScreen.Offered(type)[0];
            var typed = false;

            var name = new TextBox
            {
                Width = 280,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            name.TextChanged += (sender, args) => typed = name.IsKeyboardFocusWithin;

            var sizeHost = new ContentControl { HorizontalAlignment = HorizontalAlignment.Left };
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
                entry = offered[0];
                var question = PanelAddScreen.Question(type);
                sizeHost.Content = question == SizeQuestion.None ? null : BuildSizeRow(type, offered, question, 0, choose);
                fillName();
                refreshNote();
            };

            // What the chosen kind is, under the control that chose it: two words on a button cannot say
            // what a companion is, and a driver adding their first screen has nowhere else to find out.
            var typeCaption = Ui.Caption(type.Caption);

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
            typeRow.Width = BodyWidth;

            showSize();

            var add = Ui.OutlineButton(PanelAddScreen.AddButton, PanelMetrics.RowButtonHeight);
            add.MinWidth = ButtonMinWidth;
            add.ToolTip = "Create the screen and install its dashboard.";
            add.Click += (sender, args) => AddScreen(entry, name.Text);
            var cancel = Ui.LinkButton("Cancel");
            cancel.ToolTip = "Go back to the rig without adding anything.";
            cancel.Click += (sender, args) => Redraw();

            var nameRow = Ui.Row(PanelAddScreen.NameTitle, PanelAddScreen.NameCaption, name);
            nameRow.Width = BodyWidth;

            bodyHost.Content = Ui.VStack(0, Ui.Section(PanelAddScreen.SectionTitle,
                typeRow,
                typeCaption,
                sizeHost,
                nameRow,
                note,
                Ui.Row(new Border(), Ui.HStack(8, cancel, add))));
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
            // Two answers are a pair of buttons; eight are a list. The orientation question is always the
            // pair, which is what makes it read as "which way round" rather than as a resolution.
            var opens = values[selected < 0 || selected >= values.Length ? 0 : selected];
            var control = question == SizeQuestion.Orientation || offered.Count <= 3
                ? (FrameworkElement)BuildSegmented(values, labels, opens, changed)
                : BuildChoice(values, labels, opens, 280, changed);
            var row = question == SizeQuestion.Orientation
                ? Ui.Row(PanelAddScreen.OrientationTitle, PanelAddScreen.OrientationCaption, control)
                : Ui.Row(PanelAddScreen.SizeTitle, PanelAddScreen.SizeCaption, control);
            row.Width = BodyWidth;
            return row;
        }

        /// <summary>
        /// Changing the size of a screen that is already on the rig.
        /// </summary>
        /// <remarks>
        /// A driver who picked the wrong size had to remove the screen and add another, which threw away
        /// their zones and left every wheel button bound to it pointing at nothing. The namespace is
        /// frozen at creation (ADR 0017) and this does not move it either, so the settings and the
        /// bindings survive and only the folder in DashTemplates is rewritten.
        /// </remarks>
        private void ShowResize(ScreenInstance screen)
        {
            var catalogue = PackageCatalogue.From(plugin.Installer.PackageSource, new SimHubInstallLog());
            var type = PanelAddScreen.Types(catalogue).FirstOrDefault(t => string.Equals(t.Kind, screen.Kind, StringComparison.Ordinal));
            if (type == null || PanelAddScreen.Question(type) == SizeQuestion.None)
            {
                bodyHost.Content = Ui.VStack(0, Ui.Section(PanelAddScreen.ResizeTitle,
                    Ui.Caption("openDash carries only one " + KindLabel(screen) + ", so there is no other size to move this screen to."),
                    BackRow()));
                return;
            }

            var offered = PanelAddScreen.Offered(type);
            var current = offered.FirstOrDefault(e => e.Width == screen.Width && e.Height == screen.Height) ?? offered[0];
            var chosen = current;
            var question = PanelAddScreen.Question(type);
            // Opened on the size the screen already is, so the control says what it is before it is used
            // to say what it should be.
            var opensOn = 0;
            for (var i = 0; i < offered.Count; i++)
            {
                if (ReferenceEquals(offered[i], current)) opensOn = i;
            }
            var row = BuildSizeRow(type, offered, question, opensOn, e => chosen = e);

            var apply = Ui.OutlineButton("Change it", PanelMetrics.RowButtonHeight);
            apply.MinWidth = ButtonMinWidth;
            apply.ToolTip = "Write this screen's dashboard again at the new size.";
            apply.Click += (sender, args) => ResizeScreen(screen, chosen);
            var cancel = Ui.LinkButton("Cancel");
            cancel.ToolTip = "Leave this screen the size it is.";
            cancel.Click += (sender, args) => Redraw();

            bodyHost.Content = Ui.VStack(0, Ui.Section(PanelAddScreen.ResizeTitle + " of " + screen.Name,
                row,
                Ui.Caption(PanelAddScreen.ResizeCaption),
                Ui.Row(new Border(), Ui.HStack(8, cancel, apply))));
        }

        private void ResizeScreen(ScreenInstance screen, PackageEntry entry)
        {
            if (entry == null || (entry.Width == screen.Width && entry.Height == screen.Height))
            {
                Redraw();
                return;
            }
            var log = new SimHubInstallLog();
            // The old folder first: a screen that was the stock one at its old size owns that package's
            // own folder, and leaving it behind would put a dashboard in SimHub's list that nothing on
            // the rig answers for.
            var old = ScreenInstaller.Remove(screen, plugin.Installer.SimHubRoot, log);
            if (!old.Ok) Log.Warn("The old folder of " + screen.Name + " could not be removed: " + old.Error);

            Settings.ResizeScreen(screen, entry);
            Save();
            var result = ScreenInstaller.Write(screen, plugin.Installer.PackageSource, plugin.Installer.SimHubRoot, plugin.Installer.Record, log, force: true);
            Save();
            plugin.Installer.Wanted = Settings.RigScreens().Select(s => s.Folder).Where(folder => folder != null).ToList();
            plugin.Installer.Refresh();
            selected = screen.Namespace;
            Redraw();
            Announce(
                result.Ok ? PanelAddScreen.Resized(screen.Name, screen.SizeLabel, screen.Name) : PanelAddScreen.ResizeFailed(screen.Name, result.Error),
                result.Ok ? Theme.TextSecondary : Theme.Caution);
        }

        private static string StockNamespaceOf(PackageEntry entry)
        {
            var probe = new ScreenInstance { Kind = entry.Kind, Width = entry.Width, Height = entry.Height, Folder = entry.Folder };
            return probe.StockNamespace;
        }

        private static string Describe(PackageEntry entry)
        {
            var kind = string.Equals(entry.Kind, Contract.KindCompanion, StringComparison.Ordinal) ? "companion"
                : string.Equals(entry.Kind, Contract.KindPitWall, StringComparison.Ordinal) ? "pit wall"
                : string.Equals(entry.Kind, Contract.KindSlots, StringComparison.Ordinal) ? "slots"
                : "face";
            return entry.Width > 0 ? entry.SizeLabel + "  ·  " + kind : entry.Folder + "  ·  " + kind;
        }

        private void AddScreen(PackageEntry entry, string name)
        {
            var screen = Settings.AddScreen(entry, name);
            Save();
            var result = ScreenInstaller.Write(screen, plugin.Installer.PackageSource, plugin.Installer.SimHubRoot, plugin.Installer.Record, new SimHubInstallLog(), force: true);
            Save();
            plugin.Installer.Wanted = Settings.RigScreens().Select(s => s.Folder).Where(folder => folder != null).ToList();
            plugin.Installer.Refresh();
            selected = screen.Namespace;
            Redraw();

            // Said at the moment it becomes true rather than left to be found: SimHub reads its
            // template list once, at startup, and assigning a dashboard to a display is in another part
            // of SimHub entirely. Both are the steps a new user gives up on.
            // The dashboard is listed in SimHub under its *title*, which the installer sets to the name
            // the driver just chose -- not under the folder. Naming the folder here sent them looking
            // through Dash Studio for a row that does not exist under that word.
            var line = result.Ok
                ? PanelAddScreen.Added(screen.Name, screen.Name)
                : PanelAddScreen.AddFailed(screen.Name, result.Error);
            Announce(line, result.Ok ? Theme.TextSecondary : Theme.Caution);
        }

        private void ShowRename(ScreenInstance screen)
        {
            var name = new TextBox
            {
                Width = 280,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Text = screen.Name,
            };
            var save = BuildSecondaryButton("Rename", "Use this name for the screen.");
            save.Click += (sender, args) =>
            {
                var wanted = (name.Text ?? string.Empty).Trim();
                if (wanted.Length > 0)
                {
                    screen.Name = PackageCatalogue.UniqueName(wanted, Settings.RigScreens().Where(s => !ReferenceEquals(s, screen)).Select(s => s.Name));
                    Save();
                }
                Redraw();
            };
            var cancel = BuildSecondaryButton("Cancel", "Keep the name it has.");
            cancel.Click += (sender, args) => Redraw();

            bodyHost.Content = Ui.VStack(0, Ui.Section("Rename " + screen.Name,
                Ui.Row("Name", "What this screen is called here, and what SimHub's dashboard list shows.", name),
                Ui.Caption(
                    "Only the name changes. Its settings stay as they are, and its properties keep the names they "
                    + "have (OpenDash." + screen.Namespace + "*) so that anything you have bound to them keeps working."),
                Ui.Row(new Border(), Ui.HStack(8, cancel, save))));
        }

        /// <summary>
        /// The remove confirmation, which says the two things that are easy to miss.
        /// </summary>
        /// <remarks>
        /// The folder goes, and a wheel button bound to this screen's actions stops doing anything,
        /// because the action is no longer registered. ADR 0017 accepts that cost and this is where it
        /// is paid: a driver who removes a screen and finds a dead button three laps into a race is a
        /// bug report one sentence prevents.
        /// </remarks>
        private void ShowRemove(ScreenInstance screen)
        {
            var bound = screen.IsFace
                ? " Any wheel button you bound to this screen — the zone buttons and the glance — will stop doing anything."
                : string.Empty;
            var remove = Ui.DestructiveButton("Remove it");
            remove.ToolTip = "Remove the screen, its settings and its folder.";
            remove.Click += (sender, args) =>
            {
                var result = ScreenInstaller.Remove(screen, plugin.Installer.SimHubRoot, new SimHubInstallLog());
                Settings.RemoveScreen(screen.Namespace);
                Save();
                plugin.Installer.Wanted = Settings.RigScreens().Select(s => s.Folder).Where(folder => folder != null).ToList();
                plugin.Installer.Refresh();
                selected = null;
                Redraw();
                Announce(
                    result.Ok
                        ? "Removed " + screen.Name + ". SimHub still lists its dashboard until you restart it."
                        : "Removed " + screen.Name + " from the rig, but its folder could not be deleted: " + result.Error,
                    result.Ok ? Theme.TextSecondary : Theme.Caution);
            };
            var cancel = BuildSecondaryButton("Keep it", "Leave this screen alone.");
            cancel.Click += (sender, args) => Redraw();

            bodyHost.Content = Ui.VStack(0, Ui.Section("Remove " + screen.Name,
                Ui.Caption(
                    "This removes the screen from your rig, deletes " + (screen.Folder ?? "its folder")
                    + " from SimHub's DashTemplates, and forgets what you had set on it." + bound),
                Ui.Row(new Border(), Ui.HStack(8, cancel, remove))));
        }

        /// <summary>A line under the cards saying what just happened, until the next thing happens.</summary>
        private void Announce(string line, string colour)
        {
            var stack = bodyHost.Content as StackPanel;
            var section = stack?.Children.Count > 0 ? stack.Children[0] as Border : null;
            var rows = section?.Child as StackPanel;
            if (rows == null) return;
            var text = Ui.Text(line, Theme.SizeSmall, FontWeights.Normal, colour);
            text.TextWrapping = TextWrapping.Wrap;
            text.MaxWidth = BodyWidth;
            text.Margin = new Thickness(0, 8, 0, 0);
            rows.Children.Insert(Math.Min(2, rows.Children.Count), text);
        }

        private FrameworkElement BackRow()
        {
            var back = BuildSecondaryButton("Back", "Go back to the rig.");
            back.Click += (sender, args) => Redraw();
            return Ui.Row(new Border(), back);
        }
    }
}
