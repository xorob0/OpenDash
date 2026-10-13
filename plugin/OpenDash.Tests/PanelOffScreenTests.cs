// PanelOffScreenTests.cs: a press's ending that lands after SimHub has shown another page draws nothing (#802).
// A write ends a second or more after its press, and the Screens page rebuilt with no window holding it started a
// live preview, SimHub's renderer and its timer, that nothing let go of until the panel came back.
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenDashPlugin.Tests
{
    public class PanelOffScreenTests
    {
        [Fact]
        public void On_screen_a_redraw_draws_now_and_owes_nothing()
        {
            var gate = new PanelOffScreen();
            Assert.True(gate.Draws(loaded: true));
            Assert.False(gate.Owed);
            Assert.False(gate.Returned());
        }

        [Fact]
        public void Off_screen_a_redraw_draws_nothing_and_is_owed_to_the_return()
        {
            var gate = new PanelOffScreen();
            Assert.False(gate.Draws(loaded: false));
            Assert.True(gate.Owed);
            Assert.True(gate.Returned());
            Assert.False(gate.Owed);
        }

        /// <summary>The return catches up once, for however many endings landed while the panel was away, and a
        /// second Loaded with nothing asked since does not build the page again.</summary>
        [Fact]
        public void The_return_pays_every_owed_drawing_once()
        {
            var gate = new PanelOffScreen();
            gate.Left();
            Assert.False(gate.Draws(loaded: false));
            Assert.False(gate.Draws(loaded: false));
            Assert.True(gate.Returned());
            Assert.False(gate.Returned());
        }

        /// <summary>Leaving the screen is owed by itself: an answer or a binding made on SimHub's own page meanwhile
        /// was not heard, and the return reads them again, as it always did.</summary>
        [Fact]
        public void Leaving_the_screen_is_caught_up_on_return_with_nothing_asked_meanwhile()
        {
            var gate = new PanelOffScreen();
            gate.Left();
            Assert.True(gate.Owed);
            Assert.True(gate.Returned());
        }

        /// <summary>The first Loaded with nothing owed builds nothing more: the first page was built at the first
        /// real width, and a second build there loaded Screens' preview twice on every opening.</summary>
        [Fact]
        public void The_first_showing_builds_nothing_more_when_nothing_was_asked()
        {
            Assert.False(new PanelOffScreen().Returned());
        }

        /// <summary>
        /// The panel asks the gate where it draws: Redraw after clearing the lines and before asking SimHub anything,
        /// RebuildPage before it lets go of the build that is showing, Unloaded owes the return, and Loaded pays it
        /// through CatchUp. The panel is WPF and is read rather than run.
        /// </summary>
        [Fact]
        public void The_panel_draws_nothing_off_screen_and_catches_up_on_return()
        {
            var code = Regex.Replace(RepoPaths.Code(RepoPaths.SettingsControlSources().Single(p => Path.GetFileName(p) == "SettingsControl.cs")), @"\s+", " ");

            var redraw = Between(code, "private void Redraw()", "private void RebuildPage()");
            Assert.Contains("CloseSheet(); ClearMessages(); if (!offScreen.Draws(IsLoaded)) return; RefreshAttention(); RebuildPage(); RefreshSidebar();", redraw);

            var rebuild = Between(code, "private void RebuildPage()", "pageHost.Content = BuildPage(route);");
            var gated = rebuild.IndexOf("if (!offScreen.Draws(IsLoaded)) return;", StringComparison.Ordinal);
            Assert.True(gated >= 0, "RebuildPage builds off screen");
            Assert.True(gated < rebuild.IndexOf("DropPreview();", StringComparison.Ordinal), "RebuildPage lets go of the showing build before it asks");

            var ctor = code.Substring(code.IndexOf("public SettingsControl(OpenDash plugin)", StringComparison.Ordinal));
            var loaded = Between(ctor, "Loaded += (sender, args) =>", "Unloaded += (sender, args) =>");
            Assert.Contains("if (!pageBuilt) BuildFirstPage(); if (offScreen.Returned()) CatchUp();", loaded);
            var unloaded = Between(ctor, "Unloaded += (sender, args) =>", "};");
            Assert.Contains("DropPreview(); offScreen.Left();", unloaded);

            // CatchUp is what draws the owed page: it asks again and rebuilds in place, keeping the ending's line.
            var catchUp = Between(code, "private void CatchUp()", "private void Select(");
            Assert.Contains("RefreshAttention(); RebuildPage(); RefreshSidebar();", catchUp);
            Assert.DoesNotContain("ClearMessages()", catchUp);
        }

        private static string Between(string text, string start, string end)
        {
            var from = text.IndexOf(start, StringComparison.Ordinal);
            Assert.True(from >= 0, start + " is not in the shell");
            var to = text.IndexOf(end, from + start.Length, StringComparison.Ordinal);
            Assert.True(to >= 0, end + " does not follow " + start);
            return text.Substring(from, to - from);
        }
    }
}
