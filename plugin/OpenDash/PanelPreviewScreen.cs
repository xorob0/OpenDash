// PanelPreviewScreen.cs: which of a dashboard's screens the live preview shows, chosen the way SimHub's
// own dash window chooses it. #1085.
//
// Apart from ScreenPreview.cs for the reason PanelPreview.cs is: that file holds SimHub's renderer and
// the net8.0 test project cannot compile it, so the decision lives here over the booleans a screen
// carries, and PanelPreviewScreenTests holds it. Pure: no WPF, no SimHub.
//
// The rule is SimHub's, read out of `EditorModel.CheckGameModeScreen` in 9.12.6 and written down in
// docs/research/simhub-dash-format.md ("How the three roles actually pick a screen"): the candidates are
// the enabled screens that are not layers; the mode is Game while a game runs and some candidate is an
// in-game screen, Idle otherwise when some candidate is an idle screen, and Indeterminate when neither,
// which takes any candidate. A role nobody carries falls through rather than blanking the display.
//
// Before #1085 the preview took the first screen whatever its role. Every package OpenDash generates
// puts its idle screen last, so with no game running the preview drew a racing screen with nothing to
// draw, an empty dark frame, while the same dashboard in its own window showed the wordmark and the
// clock.
using System;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    /// <summary>What the preview needs to know about one of a dashboard's screens: the three roles the
    /// .djson declares, whether its <c>ScreenEnabledExpression</c> currently holds, and whether it is a
    /// layer, which no role ever picks.</summary>
    public struct PreviewScreenRoles
    {
        public bool InGame;
        public bool Idle;
        public bool Pit;
        public bool Enabled;
        public bool Layer;
    }

    /// <summary>The modes SimHub's dash window can be in, less Pit, which the preview does not decide
    /// (see <see cref="PanelPreviewScreen.Choose"/>).</summary>
    public enum PreviewMode
    {
        /// <summary>The dashboard has no screen to show.</summary>
        None,
        Game,
        Idle,
        Indeterminate,
    }

    public static class PanelPreviewScreen
    {
        /// <summary>The mode SimHub would be in for these screens with the game in this state.</summary>
        public static PreviewMode Mode(IList<PreviewScreenRoles> screens, bool gameRunning)
        {
            var candidates = Candidates(screens);
            if (candidates.Count == 0) return PreviewMode.None;
            if (gameRunning && Any(screens, candidates, s => s.InGame)) return PreviewMode.Game;
            if (Any(screens, candidates, s => s.Idle)) return PreviewMode.Idle;
            return PreviewMode.Indeterminate;
        }

        /// <summary>
        /// The index of the screen the preview should show, or -1 when the dashboard has none.
        /// </summary>
        /// <param name="screens">The dashboard's screens, in the order the .djson lists them.</param>
        /// <param name="gameRunning">What SimHub's last frame said, which is what its own window reads.</param>
        /// <param name="current">The screen showing now, or -1. It is kept while it carries the role the
        /// mode wants, because SimHub remembers a screen per mode and the companion pages between module
        /// screens that all carry the same role: replacing it with the first of them every second would
        /// undo a page turn.</param>
        /// <remarks>
        /// <para>Pit mode is not decided here. Nothing the panel holds says whether the car is in the pit
        /// lane, so a pit screen SimHub has already chosen while a game runs is kept rather than replaced,
        /// and otherwise the in-game screen is the answer. Deciding it here would only fight SimHub's own
        /// choice once a second.</para>
        /// <para>Where no screen is enabled the role rule is applied to every screen that is not a layer.
        /// SimHub would leave whatever it last showed, but <c>ScreenEnabled</c> starts false and only the
        /// frame loop writes it, so a preview looked at before its first frame would otherwise draw the
        /// first screen, which is the racing one.</para>
        /// </remarks>
        public static int Choose(IList<PreviewScreenRoles> screens, bool gameRunning, int current)
        {
            var candidates = Candidates(screens);
            if (candidates.Count == 0) return -1;

            Func<PreviewScreenRoles, bool> role;
            var mode = Mode(screens, gameRunning);
            switch (mode)
            {
                case PreviewMode.Game: role = s => s.InGame; break;
                case PreviewMode.Idle: role = s => s.Idle; break;
                default: role = s => true; break;
            }

            if (candidates.Contains(current))
            {
                var shown = screens[current];
                if (role(shown)) return current;
                if (mode == PreviewMode.Game && shown.Pit) return current;
            }

            foreach (var index in candidates)
            {
                if (role(screens[index])) return index;
            }
            return candidates[0];
        }

        /// <summary>The screens SimHub would choose among: enabled and not a layer; or, while none has
        /// been found enabled, every screen that is not a layer; or, failing that, every screen.</summary>
        private static List<int> Candidates(IList<PreviewScreenRoles> screens)
        {
            var enabled = new List<int>();
            if (screens == null) return enabled;
            var unlayered = new List<int>();
            for (var i = 0; i < screens.Count; i++)
            {
                if (screens[i].Layer) continue;
                unlayered.Add(i);
                if (screens[i].Enabled) enabled.Add(i);
            }
            if (enabled.Count > 0) return enabled;
            if (unlayered.Count > 0) return unlayered;
            for (var i = 0; i < screens.Count; i++) enabled.Add(i);
            return enabled;
        }

        private static bool Any(IList<PreviewScreenRoles> screens, List<int> candidates, Func<PreviewScreenRoles, bool> role)
        {
            foreach (var index in candidates)
            {
                if (role(screens[index])) return true;
            }
            return false;
        }
    }
}
