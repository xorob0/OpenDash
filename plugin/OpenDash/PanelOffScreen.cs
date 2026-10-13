// PanelOffScreen.cs: what a redraw asked for while SimHub is showing another page does (#802). Pure, so the
// decision is tested; the panel reads IsLoaded and calls this from Redraw, RebuildPage, Loaded and Unloaded.
namespace OpenDashPlugin
{
    /// <summary>
    /// Whether the panel draws its page now, or owes the drawing to the moment SimHub shows it again.
    /// </summary>
    /// <remarks>
    /// A press that writes a dashboard ends a second or more after the press, on the interface thread, and the
    /// driver may have clicked another of SimHub's pages by then. Its ending redrew the page all the same: the
    /// Screens page was rebuilt with no window holding it and started a live preview, SimHub's renderer and its
    /// timer, that nothing let go of until the panel came back, since Unloaded had already dropped the last one;
    /// on Updates it walked SimHub's LED devices and read the disk for a build nobody saw. A redraw asked for off
    /// screen is now only owed, and the return pays it: the attention, the page and the sidebar are drawn then,
    /// from the rig as it is by then. The line the ending says is the shell's and stays for the return.
    ///
    /// Owed from the moment the panel leaves the screen as well, because SimHub keeps the control and shows it
    /// again later, and whatever moved meanwhile (an answer, a binding made on SimHub's own page) was not heard.
    /// A rebuild asked for before the panel is first shown (a resize that changes the layout, an update answer
    /// landing on Home) is owed in the same way, and paid by the first Loaded.
    /// </remarks>
    public sealed class PanelOffScreen
    {
        private bool owed;

        /// <summary>Whether a drawing is waiting for the panel to come back on screen.</summary>
        public bool Owed
        {
            get { return owed; }
        }

        /// <summary>
        /// Whether a redraw or a rebuild asked for now draws now: only while the panel is on screen. Off screen
        /// the drawing is owed instead, and <see cref="Returned"/> answers for it.
        /// </summary>
        /// <param name="loaded">The control's IsLoaded.</param>
        public bool Draws(bool loaded)
        {
            if (loaded) return true;
            owed = true;
            return false;
        }

        /// <summary>The panel has left the screen (Unloaded): its return catches up.</summary>
        public void Left()
        {
            owed = true;
        }

        /// <summary>
        /// The panel is on screen again (Loaded): whether it catches up, which it does once for however many
        /// drawings it was asked for while it was away.
        /// </summary>
        public bool Returned()
        {
            var catchUp = owed;
            owed = false;
            return catchUp;
        }
    }
}
