// SettingsControl.Bindings.cs: what SimHub has bound to OpenDash's actions, read once per page and shared by
// every page that says it.
//
// The Shortcuts count in the sidebar, the zone boxes and quick-glance chips on Screens and the night-mode chip
// on Settings all ask the same question, and before this nothing in the panel could answer it without drawing
// SimHub's ControlsEditor. ControlsEditorModel is public and draws nothing: its constructor fills Triggers
// from SimHub's own mapping list. The shell owns this file; PanelBindings has the words.
//
// The cache is forgotten on every Go, on every mapping change SimHub reports, and when the panel comes back
// on screen (CatchUp): the mapping event is only listened to while the panel is showing, so anything bound
// while it was away -- on SimHub's own Controls and events page, most often -- is read afresh on return.
using System;
using System.Collections.Generic;
using System.Linq;
using SimHub.Plugins;
using SimHub.Plugins.UI;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>The triggers read for each action since the last Go or mapping change. Null when unreadable.</summary>
        private readonly Dictionary<string, IList<string>> bindingCache = new Dictionary<string, IList<string>>(StringComparer.Ordinal);

        /// <summary>Forgets what was read: on Go, on return to the panel, and whenever SimHub says a mapping changed.</summary>
        private void ForgetBindings()
        {
            bindingCache.Clear();
        }

        /// <summary>
        /// The triggers SimHub has bound to one of OpenDash's actions, by its short name
        /// (<see cref="Contract.FullActionName"/> is added here), or null when SimHub's mappings cannot be read.
        /// </summary>
        private IList<string> TriggersOf(string action)
        {
            if (string.IsNullOrEmpty(action)) return null;
            IList<string> triggers;
            if (bindingCache.TryGetValue(action, out triggers)) return triggers;
            try
            {
                if (PluginManager.GetInstance() == null)
                {
                    triggers = null;
                }
                else
                {
                    var model = new ControlsEditorModel(Contract.FullActionName(action), null);
                    triggers = model.Triggers == null
                        ? null
                        : model.Triggers.Where(mapping => mapping != null && !string.IsNullOrWhiteSpace(mapping.Trigger)).Select(mapping => mapping.Trigger).ToList();
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read SimHub's bindings for " + action + ": " + ex.Message);
                triggers = null;
            }
            bindingCache[action] = triggers;
            return triggers;
        }

        /// <summary>
        /// How many of OpenDash's actions somebody has bound -- every screen's and the rig's own -- or null
        /// when any of them could not be read: a count that is sometimes wrong would be worse than none.
        /// </summary>
        private int? BoundCount()
        {
            var actions = Settings.RigScreens().Where(screen => screen != null).SelectMany(screen => screen.ActionNames()).Concat(Contract.RigActionNames());
            var bound = 0;
            foreach (var action in actions)
            {
                var triggers = TriggersOf(action);
                if (triggers == null) return null;
                if (triggers.Count > 0) bound++;
            }
            return bound;
        }

        /// <summary>
        /// The chip another page draws for a binding: what it is bound to, Not bound, or -- when SimHub's
        /// mappings could not be read -- "Shortcuts", which makes no claim. A press lands on the binding's
        /// own row on Shortcuts (<see cref="PanelBindings.Anchor"/>).
        /// </summary>
        private System.Windows.FrameworkElement BindingChipFor(string action)
        {
            var triggers = TriggersOf(action);
            Action open = () => Go(PanelPage.Shortcuts, PanelBindings.Anchor(action));
            System.Windows.FrameworkElement chip;
            if (triggers == null) chip = Ui.BindingChip(PanelShortcuts.Title, null, open);
            else
            {
                var text = PanelBindings.ChipText(triggers);
                chip = Ui.BindingChip(text ?? Ui.NotBound, text != null, open);
            }
            chip.ToolTip = PanelBindings.ChipTooltip;
            return chip;
        }

        /// <summary>SimHub's mappings changed, perhaps on its own Controls and events page: read them again.</summary>
        private void MappingsChanged(object sender, EventArgs args)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ForgetBindings();
                RefreshSidebar();
            }));
        }

        /// <summary>
        /// Watches what one of SimHub's ControlsEditors has bound: its model, the model's Triggers and every
        /// mapping in them, calling <paramref name="eachMapping"/> on each mapping as it is watched and whenever
        /// its trigger or press type changes, and <paramref name="changed"/> whenever any of them moves. Returns
        /// the detach, which the caller registers with OnDrop. The shell's HoldWhilePressed and the Shortcuts
        /// page's ShortcutsWatch both stand on it, so the two follow SimHub's model the same way and let go of
        /// it the same way.
        /// </summary>
        /// <remarks>
        /// SimHub's own Loaded handler replaces Model with a new ControlsEditorModel every time the editor is
        /// shown (ControlsEditor_Loaded in 9.12.6), and the Add dialog writes into that new model's Triggers, so
        /// the watch follows Model wherever it goes, and Triggers when the model replaces it. SimHub's Change
        /// command edits a mapping already in the list in place (btnChange_Click sets trigger.PressType and
        /// leaves the collection alone), so each mapping's own PropertyChanged is watched too.
        ///
        /// The mappings are SimHub's for the whole session (ControlsEditorModel's Triggers is a new collection
        /// over PluginManager's own InputActionMapping), and a mapping's PropertyChanged holds its handlers
        /// strongly: a handler left on one kept the editor, and the whole page it sat on, alive until SimHub
        /// closed. So the watch is let go of whenever the editor leaves the tree, and Loaded (or the next Model)
        /// takes it up again; and the caller lets go of it when the build is dropped, which covers an editor
        /// built and never loaded, which raises no Unloaded.
        /// </remarks>
        private static Action WatchBindings(ControlsEditor editor, Action<InputMapping> eachMapping, Action changed)
        {
            ControlsEditorModel watched = null;
            System.Collections.ObjectModel.ObservableCollection<InputMapping> watchedTriggers = null;
            var watchedMappings = new List<InputMapping>();
            System.ComponentModel.PropertyChangedEventHandler mappingChanged = null;
            System.ComponentModel.PropertyChangedEventHandler modelChanged = null;
            System.Collections.Specialized.NotifyCollectionChangedEventHandler collectionChanged = null;
            Action told = () =>
            {
                if (changed != null) changed();
            };
            Action<InputMapping> each = mapping =>
            {
                if (eachMapping != null && mapping != null) eachMapping(mapping);
            };

            Action rewatchMappings = () =>
            {
                foreach (var mapping in watchedMappings) mapping.PropertyChanged -= mappingChanged;
                watchedMappings.Clear();
                if (watchedTriggers == null) return;
                foreach (var mapping in watchedTriggers)
                {
                    if (mapping == null) continue;
                    mapping.PropertyChanged += mappingChanged;
                    watchedMappings.Add(mapping);
                    each(mapping);
                }
            };
            Action rewatchTriggers = () =>
            {
                var triggers = watched == null ? null : watched.Triggers;
                if (!ReferenceEquals(triggers, watchedTriggers))
                {
                    if (watchedTriggers != null) watchedTriggers.CollectionChanged -= collectionChanged;
                    watchedTriggers = triggers;
                    if (watchedTriggers != null) watchedTriggers.CollectionChanged += collectionChanged;
                }
                rewatchMappings();
            };
            mappingChanged = (sender, args) =>
            {
                if (args.PropertyName != null && args.PropertyName != "Trigger" && args.PropertyName != "PressType") return;
                each(sender as InputMapping);
                told();
            };
            collectionChanged = (sender, args) =>
            {
                rewatchMappings();
                told();
            };
            modelChanged = (sender, args) =>
            {
                if (args.PropertyName != null && args.PropertyName != "Triggers") return;
                rewatchTriggers();
                told();
            };
            Action attach = () =>
            {
                var model = editor.Model;
                if (ReferenceEquals(model, watched)) return;
                if (watched != null) watched.PropertyChanged -= modelChanged;
                watched = model;
                if (watched != null) watched.PropertyChanged += modelChanged;
                rewatchTriggers();
                told();
            };
            Action detach = () =>
            {
                foreach (var mapping in watchedMappings) mapping.PropertyChanged -= mappingChanged;
                watchedMappings.Clear();
                if (watchedTriggers != null) watchedTriggers.CollectionChanged -= collectionChanged;
                watchedTriggers = null;
                if (watched != null) watched.PropertyChanged -= modelChanged;
                watched = null;
            };
            editor.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == null || args.PropertyName == "Model") attach();
            };
            editor.Loaded += (sender, args) => attach();
            editor.Unloaded += (sender, args) => detach();
            attach();
            return detach;
        }
    }
}
