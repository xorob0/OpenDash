// PanelConfirmation.cs: the one question the Updates page can have open before a press replaces a dashboard
// somebody has edited, and what the next press on Download or Reinstall everything does with it.
//
// Apart from the WPF file for the reason PanelPackageRow.cs is apart from Widgets.cs: the panel is net48 and the
// net8.0 test project cannot compile a line of it, and whether a press may destroy somebody's work is the last rule
// on the page that ought to go untested. Pure: no WPF types.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>The two presses on the Updates page that can replace a dashboard somebody has edited: Download
    /// (Update) and Reinstall everything (Reinstall).</summary>
    public enum ReplacingAction
    {
        Update,
        Reinstall,
    }

    /// <summary>What a press on Download or Reinstall everything is to do, once the confirmation has been consulted.</summary>
    public enum PressOutcome
    {
        /// <summary>Put the question on the press's line, and do nothing else.</summary>
        Ask,

        /// <summary>Run. Nothing on the rig is edited, so there is nothing to ask about and nothing to replace.</summary>
        Run,

        /// <summary>Run and replace the edited dashboards, which the question on the line named.</summary>
        RunReplacingEdited,
    }

    /// <summary>
    /// At most one question, for one of the two buttons, bound to the folders it named and to the line that shows it.
    /// Each button has a line of its own on the Updates page, Download's on the update card and Reinstall
    /// everything's beside it, and a question counts only while its own line shows it.
    /// </summary>
    /// <remarks>
    /// It used to be two booleans on the panel, one per button, each armed by its own first press and disarmed only by
    /// its own run or by a refresh of the line. Nothing tied either to the question it was given for, so a Reinstall
    /// that ran after Update had asked wrote its own sentence over the question and left Update reading "Replace
    /// anyway", and the next press on it replaced edited work with no question on screen, whatever had been edited
    /// since (#480). EditedConsent binds the yes that crosses a restart to one version; this binds the yes given on
    /// the page to one question.
    ///
    /// A question is therefore held with the button that asked it, the folders it named and the sentence it put on
    /// its line, and a second press is a yes only when all three still stand: the same button, the same edited
    /// folders, and that sentence still on that button's line. Whatever else takes the line withdraws the question
    /// without having to know that one was open, which is the property the two booleans lacked. There is one
    /// question for both buttons, so the other button asking withdraws it (and the page clears the first line as it
    /// asks), as does the other button running. Download's line is on the update card, so a check in flight, its
    /// answer and the switch, which write or redraw the card, withdraw Download's question; they do not touch
    /// Reinstall everything's line, and its question stands there, still the one on screen. "Put mine back" and the
    /// page being left or drawn again take both lines, as will any writer added later to either.
    /// </remarks>
    public sealed class PanelConfirmation
    {
        /// <summary>What a button reads while its question is on the line.</summary>
        public const string ReplaceAnyway = "Replace anyway";

        private ReplacingAction asking;
        private string[] named;
        private string question;

        /// <summary>
        /// Decides a press, asking when the press would replace edited work nobody has yet agreed to lose.
        /// </summary>
        /// <param name="action">The button pressed.</param>
        /// <param name="edited">The folders the run would replace although somebody has edited them, read at the press.</param>
        /// <param name="ask">The question this press puts on the line when it has to ask.</param>
        /// <param name="showing">What the update line shows at the press, or null when there is no line.</param>
        /// <remarks>
        /// A question that is answered is spent, so a third press asks again rather than replacing a second time on
        /// the one yes. A run with nothing edited withdraws whatever was open, since the run takes the line.
        /// </remarks>
        public PressOutcome Press(ReplacingAction action, IReadOnlyCollection<string> edited, string ask, string showing)
        {
            if (edited == null || edited.Count == 0)
            {
                Withdraw();
                return PressOutcome.Run;
            }
            if (IsAsking(action, showing) && SameFolders(named, edited))
            {
                Withdraw();
                return PressOutcome.RunReplacingEdited;
            }
            asking = action;
            named = edited.ToArray();
            question = ask;
            return PressOutcome.Ask;
        }

        /// <summary>
        /// What a button reads: "Replace anyway" while its own question is on the line, its verb otherwise.
        /// </summary>
        /// <remarks>
        /// Read from the line as it stands rather than from whether a question was ever asked, so that a label can
        /// never promise a replacement once its question has gone from the screen.
        /// </remarks>
        public string Label(ReplacingAction action, string showing)
        {
            if (IsAsking(action, showing)) return ReplaceAnyway;
            return action == ReplacingAction.Update ? UpdateLabel : ReinstallLabel;
        }

        /// <summary>The two buttons' own words: the update card's primary, and the press under the "In SimHub"
        /// table, which search lists by its label.</summary>
        public const string UpdateLabel = "Download";
        public const string ReinstallLabel = "Reinstall everything";

        private bool IsAsking(ReplacingAction action, string showing)
        {
            return question != null && asking == action && string.Equals(showing, question, StringComparison.Ordinal);
        }

        private void Withdraw()
        {
            named = null;
            question = null;
        }

        /// <summary>
        /// The same folders, in whatever order. Ordinal, because a question is re-asked rather than stretched: a name
        /// that reads differently is a list the driver was not shown.
        /// </summary>
        private static bool SameFolders(IReadOnlyCollection<string> asked, IReadOnlyCollection<string> now)
        {
            if (asked == null || asked.Count != now.Count) return false;
            return new HashSet<string>(asked, StringComparer.Ordinal).SetEquals(now);
        }
    }
}
