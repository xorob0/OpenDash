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
    }
}
