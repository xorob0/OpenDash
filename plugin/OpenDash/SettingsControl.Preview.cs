// SettingsControl.Preview.cs: where the live screen goes in the Rig tab, and when it is taken away.
//
// It sits under the screen's name and above the pane that configures it, which is the order the tab
// already reads in: the cards choose a screen, the header names it, and now the screen itself is
// between that name and the controls that change it. Every kind gets one, because a preview is a
// function of an installed package and nothing about a companion or a pit wall makes it less useful
// than a face.
//
// Nothing here tells the preview what a setting did. The panel writes an OpenDash property, the
// dashboard's own bindings read it, and the preview is the dashboard (ADR 0003, ADR 0020), so a zone
// changing page in the pane below is the same event as a zone changing page on the driver's screen.
// That is the whole reason this costs a hundred lines instead of a thousand.
using System;
using System.Windows;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>The preview the Rig tab is showing, or null. Owned here because it holds SimHub's
        /// renderer and has to be let go when the tab is left.</summary>
        private ScreenPreview screenPreview;

        /// <summary>
        /// The screen, live, or null when there is nothing honest to draw.
        /// </summary>
        /// <remarks>
        /// A missing package draws nothing on purpose. The pane already carries a warning row saying
        /// that this screen's dashboard is gone from SimHub, with a button that writes it again, and a
        /// second notice directly above it would say the same thing twice (docs/design/voice.md).
        /// </remarks>
        private FrameworkElement BuildScreenPreview(ScreenInstance screen)
        {
            DropPreview();
            if (screen == null || screen.Folder == null) return null;

            string file;
            try
            {
                if (!PackageExtractor.IsInstalled(plugin.Installer.SimHubRoot, screen.Folder)) return null;
                file = PackageExtractor.InstalledDashboard(plugin.Installer.SimHubRoot, screen.Folder);
            }
            catch (Exception error)
            {
                Log.Warn("The preview could not find " + screen.Name + ": " + error.Message);
                return null;
            }

            // The screen's own namespace is what makes this preview's remembered screen its own. Two
            // screens of the same package are two instances (ADR 0017), and neither of them is the
            // dashboard the driver has open.
            screenPreview = ScreenPreview.Load(file, PreviewContextId(screen), BodyWidth);
            return screenPreview.Element;
        }

        /// <summary>What SimHub files this preview's screen state under. Prefixed rather than bare, so
        /// that it cannot collide with the id a running dashboard uses.</summary>
        private static string PreviewContextId(ScreenInstance screen)
        {
            return "OpenDashPanelPreview." + screen.Namespace;
        }

        /// <summary>Lets go of the renderer the previous build was holding. Called from every build of
        /// the preview and from ForgetTabControls, so leaving the tab stops the drawing.</summary>
        private void DropPreview()
        {
            if (screenPreview == null) return;
            screenPreview.Dispose();
            screenPreview = null;
        }
    }
}
