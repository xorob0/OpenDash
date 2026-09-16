// SettingsControl.Install.Lights.cs: the "Lights openDash can install" section of the Install tab.
//
// The section stands with its label and nothing under it until the strip profiles are installable at all.
// A caption over an empty column is honest about a tab that has three things to say and can say two of
// them, whereas a section that is simply absent tells a driver looking for their LEDs that the plugin has
// no opinion about them, which is the one thing that is not true.
using System.Windows;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        private FrameworkElement BuildLightsSection()
        {
            return Ui.Section("Lights openDash can install");
        }
    }
}
