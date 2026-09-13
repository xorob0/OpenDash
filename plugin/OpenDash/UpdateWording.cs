// UpdateWording.cs: every sentence the panel says about updates, in one place.
//
// Separate from UpdateCheck so that the copy is reviewable as a diff of its own rather than buried in control flow,
// and so that it is tested as a table. The wording is the deliverable of XOR-31 as much as the fetching is: a line
// that reads as an error when nothing is wrong, or that tells a driver to restart SimHub when reopening the
// dashboard is enough, costs more than a failed request does.
using System;

namespace OpenDashPlugin
{
    public static class UpdateWording
    {
        /// <summary>
        /// What replacing a package does and does not require, which was measured on the VM rather than assumed.
        /// </summary>
        /// <remarks>
        /// An open dash window keeps what it parsed when it opened and does not reload. Closing the dashboard and
        /// starting it again picks the new one up with SimHub still running. Telling a driver to restart SimHub
        /// would be wrong and would cost them a session; telling them nothing would leave them looking at a face
        /// they think did not update.
        /// </remarks>
        public const string Reopen = "Close the dashboard and start it again to see it. SimHub does not need restarting.";

        /// <summary>
        /// What the update-check setting says about itself, which is the user-facing form of ADR 0012's promise.
        /// </summary>
        /// <remarks>
        /// Taken verbatim from design/canvas/Plugin.dc.html, where the author wrote it. The canvas is the design
        /// source and is not edited from code, so when the two disagree this is what moves. It is here rather than
        /// in the panel because it is copy, and because the whole of what openDash discloses ought to be readable
        /// in one sentence beside the switch that turns it off.
        /// </remarks>
        public const string CheckCaption = "Asks GitHub for the newest release once a day. Nothing else leaves your machine.";

        /// <summary>
        /// What a press of Update says when there is no release behind it.
        /// </summary>
        /// <remarks>
        /// Such a press ought not to be possible, since the button is drawn only while a release is on offer, and it
        /// is said out loud precisely because it ought not to be possible: a button that answers a click with no
        /// line, no log and no request reads as a panel that has broken, whereas a sentence naming the button that
        /// would fetch an answer leaves the user with something to do.
        /// </remarks>
        public const string NothingToApply = "There is no release to install. Press \"Check now\" to ask GitHub again.";

        /// <summary>The one line the Dashboard section shows, or null when it shows nothing.</summary>
        public static string Line(UpdateStatus status)
        {
            if (status == null) return null;
            switch (status.State)
            {
                case UpdateState.Checking:
                    return "Checking for updates…";
                case UpdateState.UpdateAvailable:
                    return "Version " + Show(status.LatestVersion) + " is available. You have " + Show(status.InstalledVersion) + ".";
                case UpdateState.UpToDate:
                    return "You have the newest release, " + Show(status.InstalledVersion) + ".";
                case UpdateState.Unreachable:
                    // Not "failed" and not "error": a rig with no network is a normal rig, and the user is not
                    // being asked to do anything about it.
                    return "Could not reach GitHub, so there is nothing to report. You have " + Show(status.InstalledVersion) + ".";
                case UpdateState.Disabled:
                    return "Update checks are off, so nothing is fetched.";
                default:
                    return null;
            }
        }

        /// <summary>The release notes, cut to something that fits beside a button.</summary>
        /// <remarks>
        /// The body is the release's section of CHANGELOG.md (XOR-25), which opens with a sentence of prose and
        /// then groups its changes under headings. A leading heading is dropped and the first real line is shown,
        /// which is that opening sentence; the whole of it is a click away on the release page, which is what the
        /// link is for. A release cut before that change carries GitHub's generated summary instead, and the same
        /// rule turns it into the first pull request title, which is why the heading skip stays.
        /// </remarks>
        public static string Summarise(string notes, int maxLength = 140)
        {
            if (string.IsNullOrWhiteSpace(notes)) return null;
            var lines = notes.Replace("\r\n", "\n").Split('\n');
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#", StringComparison.Ordinal)) continue;
                line = line.TrimStart('*', '-', ' ');
                if (line.Length == 0) continue;
                return line.Length <= maxLength ? line : line.Substring(0, maxLength - 1).TrimEnd() + "…";
            }
            return null;
        }

        private static string Show(string version) => string.IsNullOrWhiteSpace(version) ? "an unknown version" : version;
    }
}
