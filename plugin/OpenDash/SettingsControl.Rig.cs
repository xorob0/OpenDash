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
        private readonly Dictionary<string, CheckBox> zoneClassBoxes = new Dictionary<string, CheckBox>();
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
            return Ui.VStack(0,
                Ui.Section("Your rig", rows.ToArray()),
                Ui.Section(PaneTitle(screen), BuildScreenPane(screen)));
        }

        /// <summary>
        /// The heading over the selected screen's pane, which names what the pane is a list of.
        /// </summary>
        /// <remarks>
        /// A pit wall takes the first of the two headings the canvas gives it, because its pane opens with
        /// the picture; the second, "What each zone shows", waits on the pane itself being split in two.
        /// Slots is the kind the canvas never drew, and it leaves with XOR-95, so it borrows the shape of
        /// the face's heading rather than being given a design of its own.
        /// </remarks>
        private static string PaneTitle(ScreenInstance screen)
        {
            if (screen.IsCompanion) return "Modules in the rotation";
            if (screen.IsPitWall) return "Where the zones are";
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
        /// XOR-34's design, and the reason it is one fewer surface to build: nothing has to be dismissed,
        /// because the empty state stops appearing exactly when it stops being true.
        /// </remarks>
        private FrameworkElement BuildEmptyRig()
        {
            var pill = Ui.StatusPill(Theme.TextDim, "No screens yet", Theme.TextLabel);
            // The pill states the rig is empty, so the sentence under it no longer says so as well: it
            // is the explanation of what adding a screen does, not a second announcement.
            var text = Ui.Caption(
                "Add the one your rig actually has and openDash installs its dashboard into SimHub; "
                + "everything else on this page is about the screens you have added.");
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
            // Text and not a button face, which is what the canvas draws. What keeps a quiet destructive
            // action from being an accident is the confirmation behind it rather than its own weight.
            var remove = Ui.LinkButton("Remove this screen", Theme.Danger);
            remove.ToolTip = "Remove this screen, its settings and its dashboard folder.";
            remove.Click += (sender, args) => ShowRemove(screen);

            var rows = new List<UIElement> { Ui.Row(text, Ui.HStack(8, rename, remove)) };
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
        /// fixing, which is the reasoning XOR-125 already applies to a failed install.
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
            if (catalogue.Count == 0)
            {
                bodyHost.Content = Ui.VStack(0, Ui.Section("Add a screen",
                    Ui.Caption("This build of openDash carries no dashboard packages, so there is nothing to add. "
                        + "See plugin/OpenDash/Resources/README.md."),
                    BackRow()));
                return;
            }

            var sizes = new ComboBox
            {
                Width = 280,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            foreach (var entry in catalogue) sizes.Items.Add(Describe(entry));
            sizes.SelectedIndex = 0;

            var name = new TextBox
            {
                Width = 280,
                Height = Theme.ControlHeightSm,
                FontSize = Theme.SizeLabel,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            var note = Ui.Caption("");

            Action refresh = () =>
            {
                var entry = catalogue[Math.Max(0, sizes.SelectedIndex)];
                name.Text = PackageCatalogue.UniqueName(entry.SizeLabel, Settings.RigScreens().Select(s => s.Name));
                var second = Settings.RigScreens().Any(s => string.Equals(s.Namespace, StockNamespaceOf(entry), StringComparison.Ordinal));
                note.Text = second
                    ? "This one is your second " + entry.SizeLabel + ", so it gets its own copy of the dashboard and its own "
                        + "settings. The first keeps the one openDash ships."
                    : "openDash installs " + entry.Folder + " into SimHub for this screen.";
            };
            sizes.SelectionChanged += (sender, args) => refresh();
            refresh();

            var add = BuildSecondaryButton("Add screen", "Create the screen and install its dashboard.");
            add.Click += (sender, args) =>
            {
                var entry = catalogue[Math.Max(0, sizes.SelectedIndex)];
                AddScreen(entry, name.Text);
            };
            var cancel = BuildSecondaryButton("Cancel", "Go back to the rig without adding anything.");
            cancel.Click += (sender, args) => Redraw();

            bodyHost.Content = Ui.VStack(0, Ui.Section("Add a screen",
                Ui.Row("Size", "The sizes openDash ships. Pick the one your screen actually is; SimHub scales nothing.", sizes),
                Ui.Row("Name", "Yours. It names the card here and the dashboard in SimHub's own list.", name),
                note,
                Ui.Row(new Border(), Ui.HStack(8, cancel, add))));
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
            var line = result.Ok
                ? "Added " + screen.Name + ". Restart SimHub to see " + screen.Folder
                    + " in its dashboard list, then assign it to this display in Dash Studio."
                : "Added " + screen.Name + ", but its dashboard could not be installed: " + result.Error;
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
