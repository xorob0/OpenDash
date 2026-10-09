// PanelThemeSettings.cs: the Screens page's Themes group (#715), which themes it lists and what it calls things.
// A theme's settings are declared by the theme (Contract.ThemeSettings) and this names none of them, so a theme
// that declares a setting has a row here without a line of the panel changing.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    public static class PanelThemeSettings
    {
        /// <summary>The group's heading, under the selected screen.</summary>
        public const string Title = "Themes";

        /// <summary>
        /// The themes the group has a disclosure for: every theme a screen of the rig is drawn in that declares
        /// settings, in the catalogue's order, whichever screen is selected and whichever theme the car shows.
        /// </summary>
        /// <remarks>
        /// Every theme on the rig and not the selected screen's alone, because a rig in auto mode lets the
        /// per-car playlist choose the face (#199): the theme on screen is the car's, and the driver who wants
        /// a red backlight for the MX-5 sets it before the MX-5 is ever loaded.
        /// </remarks>
        public static IReadOnlyList<Contract.ThemeEntry> Themes(IEnumerable<ScreenInstance> rig)
        {
            var onRig = new HashSet<string>((rig ?? Enumerable.Empty<ScreenInstance>()).Where(s => s != null && s.Theme != null).Select(s => s.Theme), StringComparer.Ordinal);
            return Contract.Themes.Where(theme => onRig.Contains(theme.Id) && Contract.ThemeSettingsOf(theme.Id).Count > 0).ToList();
        }

        /// <summary>The choice drawn pressed: the one made, which the settings have already normalised to one offered.</summary>
        public static Contract.ThemeSettingChoice Pressed(Contract.ThemeSetting setting, string chosen)
        {
            var id = setting.Normalise(chosen);
            return setting.Choices.First(choice => string.Equals(choice.Id, id, StringComparison.Ordinal));
        }

        /// <summary>What a swatch is called, since a colour is not a word a screen reader can read off it: "Backlight, Red".</summary>
        public static string SwatchName(Contract.ThemeSetting setting, Contract.ThemeSettingChoice choice)
        {
            return setting.Name + ", " + choice.Name;
        }
    }
}
