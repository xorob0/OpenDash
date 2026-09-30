// SettingsControl.Messages.cs: the line the panel says after something happened, at the top of the main
// column, until the next page is drawn.
//
// It replaces the old Announce and AnnounceLights, which each dug into a tab's first section to insert a
// line and silently did nothing when the tab was shaped differently. A message has one place now, above
// whatever page is showing, and a page says one by calling Say after it has redrawn.
using System.Windows;

namespace OpenDashPlugin
{
    public partial class SettingsControl
    {
        /// <summary>Says something at the top of the page. Call it after Redraw or Go, which clear the lines.</summary>
        private void Say(PanelMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.Text)) return;
            var line = Ui.Notice(message);
            line.MaxWidth = BodyWidth;
            line.HorizontalAlignment = HorizontalAlignment.Left;
            messageHost.Children.Add(line);
            messageHost.Margin = new Thickness(0, 0, 0, 12);
            mainScroll.ScrollToTop();
        }

        /// <summary>The same, as an ordinary line or a caution: the shape almost every press answers in.</summary>
        private void Say(string text, bool ok = true)
        {
            Say(PanelMessage.Of(ok, text));
        }

        /// <summary>Takes every line away.</summary>
        private void ClearMessages()
        {
            messageHost.Children.Clear();
            messageHost.Margin = new Thickness(0);
        }
    }
}
