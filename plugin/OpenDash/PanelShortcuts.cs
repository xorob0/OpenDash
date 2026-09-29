// PanelShortcuts.cs: the Shortcuts page's words -- its title and what search finds on it.
//
// Every wheel button and key in one list, per screen and for the rig: a zone's next and previous page, the
// quick glance, night mode, brightness up and down. The binding control is SimHub's own ControlsEditor, so
// this page draws rows around it rather than a binder of its own. The Shortcuts page agent owns this file.
// Pure: no WPF.
namespace OpenDashPlugin
{
    public static class PanelShortcuts
    {
        public const string Title = "Shortcuts";

        public const string AnchorScreens = "shortcuts.screens";
        public const string AnchorRig = "shortcuts.rig";

        /// <summary>What the rig's own actions are listed under.</summary>
        public const string RigGroupTitle = "Lights";

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry("Wheel buttons", PanelPage.Shortcuts, AnchorScreens, "bind", "button", "key", "next page", "zone"),
            new PanelSearch.Entry("Quick glance", PanelPage.Shortcuts, AnchorScreens, "hold", "bind", "button"),
            new PanelSearch.Entry("Previous page", PanelPage.Shortcuts, AnchorScreens, "back", "zone", "bind"),
            new PanelSearch.Entry("Night mode button", PanelPage.Shortcuts, AnchorRig, "bind", "toggle"),
            new PanelSearch.Entry("Brightness buttons", PanelPage.Shortcuts, AnchorRig, "brighter", "dimmer", "bind"),
        };

        /// <summary>The friendly name a binder gives one of the rig's own actions (Contract.RigActionNames).</summary>
        public static string RigActionLabel(string action)
        {
            switch (action)
            {
                case Contract.ToggleNightModeAction: return "Night mode";
                case Contract.BrightnessUpAction: return "Brightness up";
                case Contract.BrightnessDownAction: return "Brightness down";
                default: return action;
            }
        }
    }
}
