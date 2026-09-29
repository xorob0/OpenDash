// PanelConfirmationTests.cs: the question Update and Reinstall ask before replacing a dashboard somebody has edited.
//
// Each test drives the confirmation the way the Install tab does: a press is given the folders edited at that
// moment, the question it would ask, and what the update line shows, and when it asks the line is then made to show
// that question. The line is the whole of what binds a yes to its question, so every test keeps it in step.
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelConfirmationTests
    {
        private const string Offer = "Version 0.3.1 is available. You have 0.3.0.";
        private const string AskUpdate = "You have edited 1 dashboard: OpenDash. Updating replaces your version.";
        private const string AskReinstall = "You have edited 1 dashboard: OpenDash. Reinstalling replaces your version.";
        private const string Reinstalled = "Reinstalled 1 dashboard. Close and reopen the dashboard to see it.";

        private static readonly string[] OneEdited = { "OpenDash" };

        /// <summary>The Install tab's side of a press: the line shows the question whenever the press asks.</summary>
        private sealed class Tab
        {
            public readonly PanelConfirmation Confirmation = new PanelConfirmation();
            public string Line = Offer;

            public PressOutcome Press(ReplacingAction action, string[] edited, string question)
            {
                var outcome = Confirmation.Press(action, edited, question, Line);
                if (outcome == PressOutcome.Ask) Line = question;
                return outcome;
            }

            public string Update => Confirmation.Label(ReplacingAction.Update, Line);
            public string Reinstall => Confirmation.Label(ReplacingAction.Reinstall, Line);
        }

        /// <summary>One press to be told, a second to mean it, and a third is a new question rather than a second yes.</summary>
        [Fact]
        public void Each_button_asks_once_and_replaces_on_the_second_press()
        {
            var tab = new Tab();
            Assert.Equal("Update", tab.Update);
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
            Assert.Equal(PanelConfirmation.ReplaceAnyway, tab.Update);
            Assert.Equal(PressOutcome.RunReplacingEdited, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
            tab.Line = "Downloading 0.3.1…";
            Assert.Equal("Update", tab.Update);

            tab.Line = Offer;
            Assert.Equal("Reinstall", tab.Reinstall);
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));
            Assert.Equal(PanelConfirmation.ReplaceAnyway, tab.Reinstall);
            Assert.Equal(PressOutcome.RunReplacingEdited, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));

            // The run wrote a sentence of its own; the same sentence written back by some later writer is no yes.
            tab.Line = AskReinstall;
            Assert.Equal("Reinstall", tab.Reinstall);
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));
        }

        /// <summary>
        /// The report (#480): Update asks, Reinstall is pressed instead and runs through its own question, and Update
        /// read "Replace anyway" under the Reinstall sentence and would have replaced edited work on its next press.
        /// </summary>
        [Fact]
        public void A_reinstall_after_the_update_question_leaves_update_to_ask_again()
        {
            var tab = new Tab();
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));

            // Reinstall does not take Update's yes for its own: it asks its question, which takes the line.
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));
            Assert.Equal("Update", tab.Update);
            Assert.Equal(PanelConfirmation.ReplaceAnyway, tab.Reinstall);

            Assert.Equal(PressOutcome.RunReplacingEdited, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));
            tab.Line = Reinstalled;
            Assert.Equal("Update", tab.Update);
            Assert.Equal("Reinstall", tab.Reinstall);

            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
            Assert.Equal(PanelConfirmation.ReplaceAnyway, tab.Update);
        }

        /// <summary>The same the other way round: Update asks its own question over Reinstall's, and only its own yes counts.</summary>
        [Fact]
        public void An_update_after_the_reinstall_question_asks_its_own()
        {
            var tab = new Tab();
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));

            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
            Assert.Equal(PanelConfirmation.ReplaceAnyway, tab.Update);
            Assert.Equal("Reinstall", tab.Reinstall);

            // Reinstall's question has gone from the line, so its button asks it again rather than running.
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));
            Assert.Equal("Update", tab.Update);
        }

        /// <summary>A second press replaces exactly the folders the question named, and a list that has moved is asked
        /// about again, whether a dashboard was edited in between or one was put back.</summary>
        [Fact]
        public void A_changed_list_of_edited_dashboards_is_asked_about_again()
        {
            var tab = new Tab();
            var two = new[] { "OpenDash", "OpenDash Rim" };
            const string askTwo = "You have edited 2 dashboards: OpenDash, OpenDash Rim. Updating replaces your version.";

            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, two, askTwo));
            Assert.Equal(askTwo, tab.Line);
            Assert.Equal(PanelConfirmation.ReplaceAnyway, tab.Update);

            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
            Assert.Equal(AskUpdate, tab.Line);

            // The same folders are the same question, in whatever order the installer lists them.
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, two, askTwo));
            Assert.Equal(PressOutcome.RunReplacingEdited, tab.Press(ReplacingAction.Update, new[] { "OpenDash Rim", "OpenDash" }, askTwo));
        }

        /// <summary>A check, its answer, the switch or "Put mine back" writes the line, and that alone withdraws the
        /// question, without any of them having to know that one was open.</summary>
        [Fact]
        public void Whatever_takes_the_line_withdraws_the_question()
        {
            var tab = new Tab();
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));

            tab.Line = "Checking for updates…";
            Assert.Equal("Update", tab.Update);
            tab.Line = Offer;
            Assert.Equal("Update", tab.Update);
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));

            // The tab being left takes the line away altogether.
            Assert.Equal("Update", tab.Confirmation.Label(ReplacingAction.Update, null));
            Assert.Equal(PressOutcome.Ask, tab.Confirmation.Press(ReplacingAction.Update, OneEdited, AskUpdate, null));
        }

        /// <summary>With nothing edited there is nothing to ask, and the run takes the line from whatever question was open.</summary>
        [Fact]
        public void With_nothing_edited_a_press_runs_and_withdraws_what_was_asked()
        {
            var tab = new Tab();
            Assert.Equal(PressOutcome.Run, tab.Press(ReplacingAction.Update, new string[0], AskUpdate));
            Assert.Equal(Offer, tab.Line);

            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
            Assert.Equal(PressOutcome.Run, tab.Press(ReplacingAction.Reinstall, new string[0], AskReinstall));
            // Nothing was written over the question yet, and the label already lets it go.
            Assert.Equal("Update", tab.Update);
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
        }
    }
}
