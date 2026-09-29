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

        /// <summary>The line under the title, as ruled: what can be bound, and nothing the rows already show.</summary>
        public const string IntroCaption = "A wheel button, a button box or a key.";

        /// <summary>What the rig's own actions are listed under.</summary>
        public const string RigGroupTitle = "Lights";

        // The rows' own words: a face's rows read "Band D · next page", and every kind's glance row reads
        // "Quick glance".
        public const string NextPageTitle = "Next page";
        public const string PreviousPageTitle = "Previous page";
        public const string QuickGlanceTitle = "Quick glance";

        /// <summary>A zone's row, and the name its binder gives the action in SimHub's own list, in the
        /// same words: "Band D · next page", "Rim · Band D · previous page".</summary>
        public static string ZoneRow(string zoneLabel, bool next)
        {
            return zoneLabel + " · " + (next ? NextPageTitle : PreviousPageTitle).ToLowerInvariant();
        }

        public static string ZoneBinderName(string screenName, string zoneLabel, bool next)
        {
            return screenName + " · " + ZoneRow(zoneLabel, next);
        }

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(NextPageTitle, PanelPage.Shortcuts, AnchorScreens, "wheel buttons", "bind", "button", "key", "zone"),
            new PanelSearch.Entry(QuickGlanceTitle, PanelPage.Shortcuts, AnchorScreens, "hold", "bind", "button"),
            new PanelSearch.Entry(PreviousPageTitle, PanelPage.Shortcuts, AnchorScreens, "back", "zone", "bind"),
            new PanelSearch.Entry(RigActionLabel(Contract.ToggleNightModeAction), PanelPage.Shortcuts, AnchorRig, "night mode button", "bind", "toggle"),
            new PanelSearch.Entry(RigActionLabel(Contract.BrightnessUpAction), PanelPage.Shortcuts, AnchorRig, "brightness buttons", "brighter", "bind"),
            new PanelSearch.Entry(RigActionLabel(Contract.BrightnessDownAction), PanelPage.Shortcuts, AnchorRig, "brightness buttons", "dimmer", "bind"),
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
