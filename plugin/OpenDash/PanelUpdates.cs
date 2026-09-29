// PanelUpdates.cs: the Updates page's words -- its title and what search finds on it.
//
// Updates is what the Install tab was for: the version and the daily check, what OpenDash has written into
// SimHub and at which version, Reinstall, Put mine back, and the links. Its sentences stay in UpdateWording
// and PanelCopy, where their pins are; the Updates page agent owns this file and adds the rest. Pure: no WPF.
namespace OpenDashPlugin
{
    public static class PanelUpdates
    {
        public const string Title = "Updates";

        public const string PluginTitle = "This plugin";
        public const string PackagesTitle = "Screens OpenDash can install";
        public const string LightsTitle = "Lights OpenDash can install";
        public const string LinksTitle = "Support";

        public const string CheckTitle = "Check for updates";

        /// <summary>The presses and links the page draws, which search lists by the same words.</summary>
        public const string PutMineBack = "Put mine back";
        public const string DocumentationLink = "Documentation";
        public const string ReportIssueLink = "Report an issue";

        public const string AnchorPlugin = "updates.plugin";
        public const string AnchorCheck = "updates.check";
        public const string AnchorPackages = "updates.packages";
        public const string AnchorLights = "updates.lights";
        public const string AnchorLinks = "updates.links";

        public static readonly PanelSearch.Entry[] Search =
        {
            new PanelSearch.Entry(PluginTitle, PanelPage.Updates, AnchorPlugin, "version", "reinstall", "update"),
            new PanelSearch.Entry(CheckTitle, PanelPage.Updates, AnchorCheck, "update", "github", "release"),
            new PanelSearch.Entry(PanelConfirmation.ReinstallLabel, PanelPage.Updates, AnchorPlugin, "repair", "dashboards"),
            new PanelSearch.Entry(PutMineBack, PanelPage.Updates, AnchorPlugin, "restore", "edited"),
            new PanelSearch.Entry(PackagesTitle, PanelPage.Updates, AnchorPackages, "dashboards", "packages"),
            new PanelSearch.Entry(LightsTitle, PanelPage.Updates, AnchorLights, "profiles", "strip", "flag box"),
            new PanelSearch.Entry(DocumentationLink, PanelPage.Updates, AnchorLinks, "guide", "help"),
            new PanelSearch.Entry(ReportIssueLink, PanelPage.Updates, AnchorLinks, "bug", "github", "support"),
        };
    }
}
