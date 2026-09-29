// PanelBindings.cs: how the panel names what a SimHub action is bound to, and where on Shortcuts that
// binding's row is.
//
// Shared rather than the Shortcuts page's, because three pages say it: Shortcuts beside each binder and in its
// clash banner, Screens on the zone boxes and the quick-glance chip, and Settings on the night-mode row. A page
// names a binding through TriggerLabel and never writes a label of its own. The shell reads SimHub's
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

        /// <summary>What a chip says for an action nothing is bound to, on every page that draws one.</summary>
        public const string NotBound = "Not bound";

        /// <summary>
        /// A SimHub trigger as every page names it, in the artboards' one form, "{source} · {input}": what reads
        /// the input, then the input. "KeyboardReaderPlugin.F9" is "Keyboard · F9"; a joystick is its device
        /// and its button, so "JoystickPlugin.CSL_Elite_B09" is "CSL Elite · 9" and
        /// "JoystickPlugin.FANATEC_Wheel_Button_3" is "FANATEC Wheel · 3"; any other plugin is its name less
        /// "Plugin". Underscores are spaces. A trigger with no plugin is named as it is. Null for nothing to name.
        /// </summary>
        /// <remarks>
        /// Shortcuts.dc.html and Screens.dc.html draw one binding one way on both pages ("Keyboard · F9",
        /// "CSL Elite · 9"), so Shortcuts' rows and clash banner read this too rather than a label of their own.
        /// </remarks>
        public static string TriggerLabel(string trigger)
        {
            if (string.IsNullOrWhiteSpace(trigger)) return null;
            var text = trigger.Trim();
            var dot = text.IndexOf('.');
            if (dot <= 0 || dot >= text.Length - 1) return Spaced(text);
            var plugin = text.Substring(0, dot);
            var input = text.Substring(dot + 1);
            if (string.Equals(plugin, "JoystickPlugin", StringComparison.Ordinal)) return JoystickLabel(input);
            string source;
            if (string.Equals(plugin, "KeyboardReaderPlugin", StringComparison.Ordinal)) source = "Keyboard";
            else source = plugin.EndsWith("Plugin", StringComparison.Ordinal) && plugin.Length > "Plugin".Length ? plugin.Substring(0, plugin.Length - "Plugin".Length) : plugin;
            return Join(Spaced(source), Spaced(input));
        }

        /// <summary>A joystick's input as its device and its button: "…_B09" and "…_Button_3" are button 9 and
        /// button 3 of the device named before them; anything else is split at its last underscore.</summary>
        private static string JoystickLabel(string input)
        {
            var match = System.Text.RegularExpressions.Regex.Match(input, @"^(?<device>.+?)_(?:B|Button_?)(?<button>\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var number = match.Groups["button"].Value.TrimStart('0');
                return Join(Spaced(match.Groups["device"].Value), number.Length == 0 ? "0" : number);
            }
            var last = input.TrimEnd('_').LastIndexOf('_');
            if (last > 0) return Join(Spaced(input.Substring(0, last)), Spaced(input.Substring(last + 1)));
            return Spaced(input);
        }

        private static string Join(string source, string input)
        {
            if (source == null) return input;
            if (input == null) return source;
            return source + " · " + input;
        }

        private static string Spaced(string text)
        {
            if (text == null) return null;
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
