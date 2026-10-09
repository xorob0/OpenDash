// SettingsControl.Themes.cs: the Screens page's Themes group (#715) -- one disclosure for each theme on the
// rig that declares settings of its own, and inside it a row per setting, drawn as swatches of its choices.
//
// Every decision and every word is PanelThemeSettings'; the settings themselves are Contract.ThemeSettings, so
// nothing here names a theme.
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>Which themes' disclosures are open, for the session.</summary>
        private readonly HashSet<string> screensThemesOpen = new HashSet<string>();

        /// <summary>The Themes group under the selected screen, or null when no theme on the rig has settings.</summary>
        private FrameworkElement BuildThemesGroup(IReadOnlyList<ScreenInstance> rig)
        {
            var themes = PanelThemeSettings.Themes(rig);
            if (themes.Count == 0) return null;
            var disclosures = themes.Select(theme =>
            {
                var id = theme.Id;
                var host = new ContentControl();
                return (UIElement)Ui.Collapsible(theme.Name, null, screensThemesOpen.Contains(id), () =>
                {
                    host.Content = BuildThemeSettingRows(id, host);
                    return host;
                }, open =>
                {
                    if (open) screensThemesOpen.Add(id);
                    else screensThemesOpen.Remove(id);
                });
            }).ToArray();
            return new Border
            {
                BorderBrush = Ui.Brush(Theme.Rule),
                BorderThickness = new Thickness(0, PanelMetrics.BorderWeight, 0, 0),
                Padding = new Thickness(0, PanelScreens.SelectedTop, 0, 0),
                Child = Ui.VStack(0, Ui.Heading(PanelThemeSettings.Title), Ui.VStack(0, disclosures)),
            };
        }

        /// <summary>A theme's settings, a row each, its choices as swatches; a press saves and redraws the rows in place.</summary>
        private FrameworkElement BuildThemeSettingRows(string theme, ContentControl host)
        {
            var rows = new List<UIElement>();
            foreach (var setting in Contract.ThemeSettingsOf(theme))
            {
                var declared = setting;
                var pressed = PanelThemeSettings.Pressed(declared, Settings.ThemeSetting(declared));
                var swatches = new List<UIElement>();
                foreach (var choice in declared.Choices)
                {
                    var picked = choice;
                    var swatch = Ui.ColourSwatch(PanelThemeSettings.SwatchName(declared, picked), picked.Colours[0], ReferenceEquals(picked, pressed), () =>
                    {
                        Settings.SetThemeSetting(declared, picked.Id);
                        Save();
                        ScreensRedraw(host, () => BuildThemeSettingRows(theme, host));
                    }, picked.Colours.Count > 1 ? picked.Colours[1] : null);
                    swatch.Uid = "screens.theme." + theme + "." + declared.Id + "." + picked.Id;
                    swatch.Margin = new Thickness(0, 0, PanelKit.ColourSwatchGap, 0);
                    swatches.Add(swatch);
                }
                var row = Ui.SettingRow(declared.Name, Ui.HStack(0, swatches.ToArray()));
                if (rows.Count == 0) row.BorderThickness = new Thickness(0);
                rows.Add(row);
            }
            return Ui.Rows(rows.ToArray());
        }
    }
}
