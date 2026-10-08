// PanelCrest.cs: the words of the Crest row under a Porsche face's picture on the Screens page (#714), and
// which faces draw it. Pure, so the tests hold the copy; SettingsControl.Screens.Face.cs draws the row.
//
// The row is the address the crest is downloaded from, prefilled with CarCrestLibrary.DefaultUrl, and a line
// saying whether the crest is on this computer. The address is the rig's: one crest serves every Porsche
// screen, so changing it on one face changes it on all of them, as the card face's Rev bar row is the rig's.
namespace OpenDashPlugin
{
    public static class PanelCrest
    {
        public const string Title = "Crest";

        /// <summary>The address box's hover while it is empty.</summary>
        public const string EmptyTooltip = "Sets where the crest is downloaded from, from an http or https address.";

        /// <summary>The press drawn beside the address after a download that did not work.</summary>
        public const string Retry = "Download";

        public const string RetryTooltip = "Downloads the crest again.";

        /// <summary>Before the start's look at the disk has finished.</summary>
        public const string Loading = "Loading…";

        public const string Downloading = "Downloading…";

        public const string Present = "Downloaded to this computer.";

        /// <summary>The driver cleared the address: the badge draws its empty outline.</summary>
        public const string Cleared = "No address, so the badge is left empty.";

        /// <summary>The reason goes to SimHub's log; the badge draws its empty outline meanwhile.</summary>
        public const string Failed = "Could not download the crest. See SimHub's log.";

        /// <summary>Whether a screen's editor draws the row: a Porsche face at a size that keeps the badge.</summary>
        public static bool Shown(ScreenInstance screen)
        {
            return screen != null && CarCrestLibrary.DrawsBadge(screen.Theme, screen.Width, screen.Height);
        }

        /// <summary>The row's status line. A rig the service has not yet looked at for this face reads Loading,
        /// since the row asks for that look as it is drawn.</summary>
        public static string Line(CarCrestState state)
        {
            switch (state)
            {
                case CarCrestState.Fetching: return Downloading;
                case CarCrestState.Present: return Present;
                case CarCrestState.Cleared: return Cleared;
                case CarCrestState.Failed: return Failed;
                default: return Loading;
            }
        }

        /// <summary>Whether the row offers <see cref="Retry"/>: after a failure only.</summary>
        public static bool OffersRetry(CarCrestState state)
        {
            return state == CarCrestState.Failed;
        }
    }
}
