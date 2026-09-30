// PanelConfirmationTests.cs: the question Download and Reinstall everything ask before replacing a dashboard
// somebody has edited.
//
// Each test drives the confirmation the way the Updates page does: each press has a line of its own, Download's on
// the update card and Reinstall everything's beside it, and a press is given the folders edited at that moment, the
// question it would ask, and what its own line shows. When it asks, its line shows the question and the other line
// is cleared (SettingsControl.UpdatesAsk). The line is the whole of what binds a yes to its question, so every test
// keeps both lines in step with what the page writes to them.
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelConfirmationTests
    {
        private const string AskUpdate = "You have edited 1 dashboard: OpenDash. Updating replaces your version.";
        private const string AskReinstall = "You have edited 1 dashboard: OpenDash. Reinstalling replaces your version.";
        private const string Checking = "Checking for updates…";

        private static readonly string[] OneEdited = { "OpenDash" };

        /// <summary>The Updates page's side of a press: two lines, each empty until something writes it.</summary>
        private sealed class Tab
        {
            public readonly PanelConfirmation Confirmation = new PanelConfirmation();

            /// <summary>Download's line on the update card.</summary>
            public string CardLine = string.Empty;

            /// <summary>Reinstall everything's line beside it.</summary>
            public string ReinstallLine = string.Empty;

            public PressOutcome Press(ReplacingAction action, string[] edited, string question)
            {
                var outcome = Confirmation.Press(action, edited, question, action == ReplacingAction.Update ? CardLine : ReinstallLine);
                if (outcome == PressOutcome.Ask) Ask(action, question);
                return outcome;
            }

            /// <summary>UpdatesAsk: the question on the press's own line, and the other line cleared.</summary>
            private void Ask(ReplacingAction action, string question)
            {
                if (action == ReplacingAction.Update)
                {
                    CardLine = question;
                    ReinstallLine = string.Empty;
                }
                else
                {
                    ReinstallLine = question;
                    CardLine = string.Empty;
                }
            }

            /// <summary>A redraw of the card (an answer, the switch): a new, empty line for Download.</summary>
            public void RedrawCard()
            {
                CardLine = string.Empty;
            }

            /// <summary>A redraw of the page (a run, Put mine back, a return): both lines new.</summary>
            public void Redraw()
            {
                CardLine = string.Empty;
                ReinstallLine = string.Empty;
            }

            public string Update => Confirmation.Label(ReplacingAction.Update, CardLine);
            public string Reinstall => Confirmation.Label(ReplacingAction.Reinstall, ReinstallLine);
        }

        /// <summary>One press to be told, a second to mean it, and a third is a new question rather than a second yes.</summary>
        [Fact]
        public void Each_button_asks_once_and_replaces_on_the_second_press()
        {
            var tab = new Tab();
            Assert.Equal("Download", tab.Update);
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
            Assert.Equal(PanelConfirmation.ReplaceAnyway, tab.Update);
            Assert.Equal(PressOutcome.RunReplacingEdited, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
            // The run redraws the card, and its line with it.
            tab.RedrawCard();
            Assert.Equal("Download", tab.Update);

            Assert.Equal("Reinstall everything", tab.Reinstall);
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));
            Assert.Equal(PanelConfirmation.ReplaceAnyway, tab.Reinstall);
            Assert.Equal(PressOutcome.RunReplacingEdited, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));

            // The run spent the yes; the same sentence written back by some later writer is no yes.
            tab.ReinstallLine = AskReinstall;
            Assert.Equal("Reinstall everything", tab.Reinstall);
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

            // Reinstall does not take Update's yes for its own: it asks its question on its own line, and the
            // card's line gives Download's question up.
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));
            Assert.Equal(string.Empty, tab.CardLine);
            Assert.Equal("Download", tab.Update);
            Assert.Equal(PanelConfirmation.ReplaceAnyway, tab.Reinstall);

            Assert.Equal(PressOutcome.RunReplacingEdited, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));
            tab.Redraw();
            Assert.Equal("Download", tab.Update);
            Assert.Equal("Reinstall everything", tab.Reinstall);

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
            Assert.Equal(string.Empty, tab.ReinstallLine);
            Assert.Equal(PanelConfirmation.ReplaceAnyway, tab.Update);
            Assert.Equal("Reinstall everything", tab.Reinstall);

            // Reinstall's question has gone from its line, so its button asks it again rather than running.
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));
            Assert.Equal("Download", tab.Update);
        }

        /// <summary>
        /// Even were the other line still to show its question, one question is open at a time: the confirmation
        /// holds only the last one asked, so a press on the first button asks again rather than running.
        /// </summary>
        [Fact]
        public void One_question_is_open_whatever_the_other_line_still_shows()
        {
            var confirmation = new PanelConfirmation();
            Assert.Equal(PressOutcome.Ask, confirmation.Press(ReplacingAction.Update, OneEdited, AskUpdate, string.Empty));
            Assert.Equal(PressOutcome.Ask, confirmation.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall, string.Empty));
            Assert.Equal("Download", confirmation.Label(ReplacingAction.Update, AskUpdate));
            Assert.Equal(PressOutcome.Ask, confirmation.Press(ReplacingAction.Update, OneEdited, AskUpdate, AskUpdate));
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
            Assert.Equal(askTwo, tab.CardLine);
            Assert.Equal(PanelConfirmation.ReplaceAnyway, tab.Update);

            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
            Assert.Equal(AskUpdate, tab.CardLine);

            // The same folders are the same question, in whatever order the installer lists them.
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, two, askTwo));
            Assert.Equal(PressOutcome.RunReplacingEdited, tab.Press(ReplacingAction.Update, new[] { "OpenDash Rim", "OpenDash" }, askTwo));
        }

        /// <summary>A check in flight writes the card's line, and its answer and the switch redraw the card: each
        /// withdraws Download's question without having to know that one was open.</summary>
        [Fact]
        public void A_check_its_answer_or_the_switch_withdraws_Download_s_question()
        {
            var tab = new Tab();
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
            tab.CardLine = Checking;
            Assert.Equal("Download", tab.Update);
            tab.RedrawCard();
            Assert.Equal("Download", tab.Update);
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));

            // The switch redraws the card too.
            tab.RedrawCard();
            Assert.Equal("Download", tab.Update);
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
        }

        /// <summary>Neither a check nor the switch writes Reinstall everything's line, so its question stands there,
        /// still on screen, its button still reads "Replace anyway", and the next press on it is the yes.</summary>
        [Fact]
        public void A_check_or_the_switch_leaves_Reinstall_everything_s_question_standing()
        {
            var tab = new Tab();
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));
            tab.CardLine = Checking;
            Assert.Equal(PanelConfirmation.ReplaceAnyway, tab.Reinstall);
            Assert.Equal("Download", tab.Update);
            tab.RedrawCard();
            Assert.Equal(PanelConfirmation.ReplaceAnyway, tab.Reinstall);
            Assert.Equal(PressOutcome.RunReplacingEdited, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));
        }

        /// <summary>"Put mine back" and the page being drawn again take both lines, and the page being left takes
        /// them away altogether.</summary>
        [Fact]
        public void A_redraw_or_leaving_the_page_withdraws_either_question()
        {
            var tab = new Tab();
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));
            tab.Redraw();
            Assert.Equal("Reinstall everything", tab.Reinstall);
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Reinstall, OneEdited, AskReinstall));

            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
            tab.Redraw();
            Assert.Equal("Download", tab.Update);

            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
            Assert.Equal("Download", tab.Confirmation.Label(ReplacingAction.Update, null));
            Assert.Equal(PressOutcome.Ask, tab.Confirmation.Press(ReplacingAction.Update, OneEdited, AskUpdate, null));
        }

        /// <summary>With nothing edited there is nothing to ask, and the run takes the line from whatever question was open.</summary>
        [Fact]
        public void With_nothing_edited_a_press_runs_and_withdraws_what_was_asked()
        {
            var tab = new Tab();
            Assert.Equal(PressOutcome.Run, tab.Press(ReplacingAction.Update, new string[0], AskUpdate));
            Assert.Equal(string.Empty, tab.CardLine);

            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
            Assert.Equal(PressOutcome.Run, tab.Press(ReplacingAction.Reinstall, new string[0], AskReinstall));
            // Nothing was written over the question on the card's line yet, and the label already lets it go.
            Assert.Equal(AskUpdate, tab.CardLine);
            Assert.Equal("Download", tab.Update);
            Assert.Equal(PressOutcome.Ask, tab.Press(ReplacingAction.Update, OneEdited, AskUpdate));
        }
    }
}
