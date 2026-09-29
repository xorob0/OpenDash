// PanelBindings.cs: how the panel names what a SimHub action is bound to, and where on Shortcuts that
// binding's row is.
//
// Shared rather than the Shortcuts page's, because three pages say it: Shortcuts beside each binder, Screens
// on the zone boxes and the quick-glance chip, and Settings on the night-mode row. The shell reads SimHub's
// mappings (SettingsControl.Bindings.cs) and hands the trigger strings here, so the words are the same on
// every page and a test can hold them.
//
// Pure: no WPF and no SimHub types. PanelBindingsTests holds it.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    public static class PanelBindings
    {
        /// <summary>
        /// The anchor a binding's row carries on Shortcuts, so a chip on another page can land on it:
        /// "binding." and the action's own name. The Shortcuts page tags every binder row with it.
        /// </summary>
        public static string Anchor(string action)
        {
            if (string.IsNullOrEmpty(action)) throw new ArgumentException("an anchor needs an action", "action");
            return AnchorPrefix + action;
        }

        public const string AnchorPrefix = "binding.";

        /// <summary>
        /// A SimHub trigger as a chip says it: the input's own name, without the plugin that reads it and
        /// with its underscores as spaces. "JoystickPlugin.FANATEC_Wheel_Button_3" is "FANATEC Wheel Button 3",
        /// "KeyboardReaderPlugin.F5" is "F5". Null for nothing to name.
        /// </summary>
        public static string TriggerLabel(string trigger)
        {
            if (string.IsNullOrWhiteSpace(trigger)) return null;
            var text = trigger.Trim();
            var dot = text.IndexOf('.');
            if (dot >= 0 && dot < text.Length - 1) text = text.Substring(dot + 1);
            text = text.Replace('_', ' ').Trim();
            while (text.Contains("  ")) text = text.Replace("  ", " ");
            return text.Length == 0 ? null : text;
        }

        /// <summary>
        /// What a chip says for an action's triggers: the first one's name, and "+1" and so on when there
        /// are more. Null when there are none, which a chip draws as Not bound.
        /// </summary>
        public static string ChipText(IEnumerable<string> triggers)
        {
            var named = (triggers ?? Enumerable.Empty<string>()).Select(TriggerLabel).Where(label => label != null).ToList();
            if (named.Count == 0) return null;
            return named.Count == 1 ? named[0] : named[0] + " +" + (named.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
