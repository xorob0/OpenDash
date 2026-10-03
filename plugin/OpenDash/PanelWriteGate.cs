// PanelWriteGate.cs: whether a press that writes SimHub's dashboards may start, and what the panel says when it
// may not (#606). Pure, so the decision is tested; the panel reads the two facts from the plugin's UpdateService.
namespace OpenDashPlugin
{
    public static class PanelWriteGate
    {
        /// <summary>Said to a press made while an update is downloading or installing.</summary>
        public const string WhileUpdating = "An update is installing. Try again when it has finished.";

        /// <summary>Said to a press made while another press is still writing a dashboard.</summary>
        public const string WhileWriting = "A dashboard is still being written. Try again in a moment.";

        /// <summary>
        /// Why a press that writes is refused, or null when it may write.
        /// </summary>
        /// <remarks>
        /// Every press that writes, puts back or removes a dashboard folder asks this before it changes anything:
        /// the Screens page's add, duplicate, rename, resize, reinstall and remove, Home's install again, and on
        /// Updates, Download, Reinstall everything and Put mine back. A press that waited behind the run instead
        /// would write over what the update had just installed, after the driver had moved on, and the run would
        /// not know: the update is what the driver asked for most recently, so the press is the one turned away.
        ///
        /// Refused rather than drawn disabled, as the Updates page draws its own presses while its run goes: a
        /// press elsewhere is one of a dozen controls across three pages and their sheets, a run's end reaches only
        /// the panel that started it, and a disabled button nothing redraws would stay disabled after the run had
        /// finished. Asked at the press, the answer is always the current one, and a sheet that is refused stays
        /// open with what was typed in it.
        /// </remarks>
        /// <param name="updating">An update is running, its download included (UpdateService.Applying).</param>
        /// <param name="writing">Something is writing DashTemplates: an update's install or another press
        /// (UpdateService.Busy).</param>
        public static string Refusal(bool updating, bool writing)
        {
            if (updating) return WhileUpdating;
            return writing ? WhileWriting : null;
        }
    }
}
