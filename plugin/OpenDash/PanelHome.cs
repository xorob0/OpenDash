// PanelHome.cs: the Home page's words, numbers and decisions -- its title, its sections, what each device's
// row says under its name, which pictures are live, and what search finds on it.
//
// Home says what needs fixing (PanelAttention decides that), what each device is showing, and the two
// controls a driver reaches for between sessions. SettingsControl.Home.cs only draws what this file decides,
// to Main.dc.html. Pure: no WPF, no SimHub. PanelHomeTests holds every string and every rule here.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenDashPlugin
{
    /// <summary>
    /// One device's line under its name on Home: what it says, in which ink, and the state dot beside it.
    /// </summary>
    public sealed class HomeLine
    {
        public HomeLine(string text, string textHex, string dotHex)
        {
            Text = text ?? string.Empty;
            TextHex = textHex ?? Theme.TextSecondary;
            DotHex = dotHex;
        }

        public string Text { get; private set; }

        public string TextHex { get; private set; }

        /// <summary>The dot's ink, or null for no dot: a state nobody could read is not drawn as a good one.</summary>
        public string DotHex { get; private set; }
    }

    /// <summary>What an issue's press does on Home.</summary>
    public enum HomePress
    {
        /// <summary>Opens the issue's page with the thing it names selected.</summary>
        Open,

        /// <summary>Goes to the issue's page, selecting nothing: the issue is about the page as a whole.</summary>
        Go,

        /// <summary>Asks SimHub again and draws the page again.</summary>
        CheckAgain,

        /// <summary>Writes the screen's dashboard back.</summary>
        Reinstall,
    }

    public static class PanelHome
    {
        public const string Title = "Home";

        public const string RightNowTitle = "Right now";
        public const string QuickControlsTitle = "Quick controls";

        /// <summary>The quick controls' third cell: what the Rig page tries, and the press that opens it. A noun
        /// phrase, where the artboard asks a sentence ("Try a flag or the spotter"): the button says what to do.</summary>
        public const string TryTitle = "Flags and spotter";
        public static readonly string OpenRig = PanelAttention.Open(PanelRigMap.Title);

        /// <summary>The link in each Right now card's head, to that card's page.</summary>
        public const string OpenLink = "Open";

        /// <summary>The press under the empty rig's sentence.</summary>
        public static readonly string OpenScreens = PanelAttention.Open(PanelScreens.Title);

        public const string AnchorAttention = "home.attention";
        public const string AnchorRightNow = "home.right-now";
        public const string AnchorQuickControls = "home.quick-controls";

        // --- Geometry, read off Main.dc.html ---------------------------------------------------------------

        /// <summary>The eyebrow "Home" over the headline.</summary>
        public const double HeaderGap = 10;

        /// <summary>An issue row: padded 16 by 18, the 32 px icon well on the caution's tint 16 from the text,
        /// the 18 px icon in it, the title 15/600, the detail 3 under it, the steps 10 under that.</summary>
        public const double IssuePaddingX = 18;
        public const double IssuePaddingY = 16;
        public const double IconWell = 32;
        public const double IconSize = 18;
        public const double IconGap = 16;
        public const double IconTint = 0.12;
        public const double DetailGap = 3;
        public const double StepsGap = 10;
        public const double DetailSize = 13;

        /// <summary>The Right now cards: three to a row where there is the room, none narrower than 280.</summary>
        public const double CardMinWidth = 280;
        public const double CardGap = 16;
        public const int CardMax = 3;

        /// <summary>A card's head: the eyebrow and the Open link, padded 14 and 12 under.</summary>
        public const double CardHeadPaddingX = 14;
        public const double CardHeadPaddingTop = 14;
        public const double CardHeadPaddingBottom = 12;
        public const double OpenLinkSize = 13;

        /// <summary>A device row: padded 12 by 14, 14 between its parts, a rule on top.</summary>
        public const double RowPaddingX = 14;
        public const double RowPaddingY = 12;
        public const double RowGap = 14;
        public const double NameSize = 14;
        public const double MetaSize = 13;
        public const double MetaGap = 8;
        public const double LineSize = 12;
        public const double LineGap = 3;
        public const double DotSize = 8;

        /// <summary>A strip's row stacks its name, its picture and its line, 10 apart.</summary>
        public const double StripRowGap = 10;

        /// <summary>The quick controls' cells: padded 16 by 18, 12 between the eyebrow and the control, and
        /// sharing the card 1.6 : 1 : 1.2 where they sit side by side.</summary>
        public const double QuickPaddingX = 18;
        public const double QuickPaddingY = 16;
        public const double QuickGap = 12;
        public static readonly double[] QuickColumns = { 1.6, 1, 1.2 };
        public const double QuickValueSize = 15;

        /// <summary>"Open Rig" is the small button's 28 px and 13 px type, at the regular button's 14 px padding.</summary>
        public const double QuickRigPaddingX = 14;

        // --- The attention card ----------------------------------------------------------------------------

        /// <summary>The icon in an issue's well, by the issue's rule: the device's own page icon for a strip or a
        /// matrix, the restart arrow for what waits on SimHub, and the warning for a dashboard that is gone.</summary>
        public static string IssueIcon(PanelIssue issue)
        {
            var id = issue == null ? string.Empty : issue.Id ?? string.Empty;
            if (id.StartsWith(PanelAttention.ScreenRestart, StringComparison.Ordinal)) return PanelIcons.Restart;
            if (id.StartsWith(PanelAttention.StripUnselected, StringComparison.Ordinal)) return PanelIcons.Leds;
            if (id.StartsWith(PanelAttention.StripOutdated, StringComparison.Ordinal)) return PanelIcons.Leds;
            if (id.StartsWith(PanelAttention.MatrixDark, StringComparison.Ordinal)) return PanelIcons.Matrix;
            if (id == PanelAttention.FlagBoxOutdated) return PanelIcons.Matrix;
            if (id == PanelAttention.ScreensUnclaimed) return PanelIcons.Screens;
            if (id == PanelAttention.UpdateRestart) return PanelIcons.Restart;
            if (id == PanelAttention.UpdateAvailable) return PanelIcons.Updates;
            return PanelIcons.Warning;
        }

        /// <summary>What an issue's press does. An Open press selects the thing the issue names; one about the
        /// page as a whole goes there and leaves the page's selection alone.</summary>
        public static HomePress Press(PanelIssue issue)
        {
            if (issue == null) return HomePress.Go;
            switch (issue.Action)
            {
                case PanelIssueAction.CheckAgain: return HomePress.CheckAgain;
                case PanelIssueAction.Reinstall: return issue.Subject == null ? HomePress.Go : HomePress.Reinstall;
                default: return issue.Subject == null ? HomePress.Go : HomePress.Open;
            }
        }

        /// <summary>Whether an issue's press sits beside its text. Where two blocks do not fit side by side it
        /// goes under the text, so a narrow panel does not squeeze the sentence into a column of words.</summary>
        public static bool PressBeside(bool twoColumns)
        {
            return twoColumns;
        }

        /// <summary>What Home says after Check again: that the thing is no longer to fix, or, in the caution's
        /// ink, what still is.</summary>
        public static PanelMessage CheckedAgain(PanelIssue before, IEnumerable<PanelIssue> after)
        {
            var still = before == null ? null : PanelAttention.Of(after, before.Id, null);
            if (still == null) return PanelMessage.Info(CheckedFixed);
            return PanelMessage.Caution(CheckedStill + still.Title + ".");
        }

        public const string CheckedFixed = "Checked again. That is fixed.";
        public const string CheckedStill = "Checked again. ";

        // --- Right now: screens ----------------------------------------------------------------------------

        /// <summary>A screen's size beside its name, or nothing for one whose package is gone.</summary>
        public static string ScreenSize(ScreenInstance screen)
        {
            return screen == null || screen.Width <= 0 || screen.Height <= 0 ? string.Empty : screen.SizeLabel;
        }

        /// <summary>
        /// A screen's line: what SimHub is missing when something is, in that state's ink, and otherwise what
        /// the screen shows now.
        /// </summary>
        /// <param name="installed">Whether its dashboard is in SimHub, or null when the disk could not be asked.</param>
        /// <param name="waitsForRestart">Whether SimHub has not loaded it yet (PanelAttention.ScreenRestart).</param>
        public static HomeLine ScreenLine(OpenDashSettings settings, ScreenInstance screen, bool? installed, bool waitsForRestart)
        {
            if (installed == false) return new HomeLine(PanelScreens.Missing, Theme.StatusFailed, Theme.StatusFailed);
            if (waitsForRestart) return new HomeLine(PanelScreens.NotInSimHubYet, Theme.Caution, Theme.Caution);
            return new HomeLine(ScreenShows(settings, screen), Theme.TextSecondary, installed == true ? Theme.StatusUpToDate : null);
        }

        /// <summary>
        /// What a screen shows now, in the names its own controls give it.
        /// </summary>
        /// <remarks>
        /// A face: the page each zone is on, in the panel's zone order ("Lap times · Gear, speed, revs ·
        /// Leaderboard · Fuel"). A pit wall: its page ("Race page"). A companion: how much of the catalogue its
        /// rotation holds ("12 of 21 modules"), since which module it is on is the phone's own and not
        /// something OpenDash is told. A card face: its cards, slot by slot. Nothing is made up for a kind the
        /// panel does not know.
        /// </remarks>
        public static string ScreenShows(OpenDashSettings settings, ScreenInstance screen)
        {
            if (screen == null) return string.Empty;
            if (screen.IsFace)
            {
                var face = screen.Face ?? (settings == null ? null : settings.ScreenFace(screen.Namespace));
                if (face == null) return string.Empty;
                var size = screen.FaceSize ?? Contract.ReferenceFace;
                return string.Join(Separator, PanelFacePlan.ZoneOrder(size).Select(letter => FacePages.NameOf(letter, face.Zone(letter))));
            }
            if (screen.IsPitWall) return PitWallPage(screen.PitWallPage);
            if (screen.IsCompanion) return ModulesLine(screen.Modules);
            if (screen.IsSlots) return settings == null ? string.Empty : CardsLine(Enumerable.Range(1, Contract.SlotCount).Select(settings.Slot));
            return string.Empty;
        }

        public const string Separator = " · ";

        /// <summary>"Race page": the pit wall's page as the Screens page's Page control names it.</summary>
        public static string PitWallPage(int page)
        {
            return Contract.PitWallPageNames[Contract.NormalisePitWallPage(page)] + " page";
        }

        /// <summary>"12 of 21 modules". A rotation with nothing on reads as the whole catalogue, as the
        /// companion itself reads it, and so does one nothing has set up yet.</summary>
        public static string ModulesLine(bool[] modules)
        {
            var on = modules == null ? 0 : modules.Take(Modules.Count).Count(m => m);
            if (on == 0) on = Modules.Count;
            return on.ToString(CultureInfo.InvariantCulture) + " of " + Modules.Count.ToString(CultureInfo.InvariantCulture) + " modules";
        }

        /// <summary>A card face's cards in slot order, by the names the Cards picker gives them.</summary>
        public static string CardsLine(IEnumerable<int> cards)
        {
            return string.Join(Separator, (cards ?? Enumerable.Empty<int>()).Select(Cards.DisplayName));
        }

        // --- Right now: LEDs -------------------------------------------------------------------------------

        public const string CarLightsLine = "Car's own lights";
        public const string StripNotInstalled = "Not installed in SimHub";
        public const string StripNotSelected = "Not selected in SimHub";
        public const string StripHasUpdate = "Profile has an update";
        public const string StripSelected = "Selected in SimHub";
        public const string StripInstalled = "Installed in SimHub";

        /// <summary>The ends and the centre a strip is drawn with. A shape the panel cannot read is drawn as the
        /// Rig page draws it, 3/9/3.</summary>
        public static int[] StripSpan(string shape)
        {
            var parsed = LightShape.Parse(shape);
            return parsed == null ? new[] { 3, 9 } : new[] { parsed.Left, parsed.Centre };
        }

        /// <summary>The shape beside a strip's name, as the LEDs page labels it ("3/9/3").</summary>
        public static string StripShape(LedBar bar)
        {
            return bar == null || string.IsNullOrEmpty(bar.Shape) ? string.Empty : PanelLightRows.ShapeLabel(bar.Shape);
        }

        /// <summary>
        /// Whether a strip's picture is the car's own lights as they are now, or dark.
        /// </summary>
        /// <remarks>
        /// Only what OpenDash computes is drawn: the car's run, while the car tables have a car
        /// (CarLightService.Ready), on a strip whose centre shows the revs in the car's own style, and whose
        /// profile SimHub is not known to be missing or not showing. Flags, the spotter and every other effect
        /// are drawn by the profile inside SimHub, which the plugin does not see, so a strip showing one of
        /// those is drawn dark rather than guessed at.
        /// </remarks>
        public static bool StripLive(bool carReady, string rpmStyle, string centre, FlagBoxInstallState? profile, bool? selected)
        {
            if (!carReady) return false;
            if (!string.Equals(Contract.NormaliseLedRpmStyle(rpmStyle), Contract.LedRpmStyleCar, StringComparison.Ordinal)) return false;
            if (!string.Equals(Contract.NormaliseLedCentre(centre), Contract.LedCentres[0], StringComparison.Ordinal)) return false;
            if (profile == FlagBoxInstallState.NotInstalled || profile == FlagBoxInstallState.Failed) return false;
            return selected != false;
        }

        /// <summary>
        /// A strip's line: what is wrong in SimHub when something is, then the car's own lights while they are
        /// live, then the profile's state. Nothing at all when SimHub could not be asked.
        /// </summary>
        public static HomeLine StripLine(bool live, string carName, FlagBoxInstallState? profile, bool? selected)
        {
            if (profile == FlagBoxInstallState.NotInstalled || profile == FlagBoxInstallState.Failed)
                return new HomeLine(StripNotInstalled, Theme.Caution, Theme.Caution);
            var installed = profile == FlagBoxInstallState.UpToDate || profile == FlagBoxInstallState.Outdated;
            if (installed && selected == false) return new HomeLine(StripNotSelected, Theme.Caution, Theme.Caution);
            if (profile == FlagBoxInstallState.Outdated) return new HomeLine(StripHasUpdate, Theme.Caution, Theme.Caution);
            var dot = installed ? Theme.StatusUpToDate : null;
            if (live) return new HomeLine(string.IsNullOrWhiteSpace(carName) ? CarLightsLine : CarLightsLine + Separator + carName.Trim(), Theme.TextSecondary, dot);
            if (!installed) return new HomeLine(string.Empty, Theme.TextSecondary, null);
            return new HomeLine(selected == true ? StripSelected : StripInstalled, Theme.TextSecondary, dot);
        }

        // --- Right now: matrix -----------------------------------------------------------------------------

        public const string MatrixNotSet = "Not set";

        /// <summary>"Matrix 1 · Gear": the SimHub content number and the idle display, as the Idle display
        /// control names it; "Matrix 2 · Not set" in the caution's ink when no device shows the slot.</summary>
        public static HomeLine MatrixLine(int slot, string rest, bool? shown, bool profileHasUpdate)
        {
            var head = MatrixName(slot) + Separator;
            if (shown == false) return new HomeLine(head + MatrixNotSet, Theme.Caution, Theme.Caution);
            var dot = profileHasUpdate ? Theme.Caution : shown == true ? Theme.StatusUpToDate : null;
            return new HomeLine(head + RestLabel(rest), Theme.TextSecondary, dot);
        }

        /// <summary>"Matrix 2", the name a panel goes by until it is given one, and the head of its line.</summary>
        public static string MatrixName(int slot)
        {
            return "Matrix " + slot.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The idle display's value the way its control labels it: "Dark" or "Gear".</summary>
        public static string RestLabel(string rest)
        {
            var at = Array.IndexOf(Contract.FlagBoxRests, (rest ?? string.Empty).ToLowerInvariant());
            return at >= 0 && at < PanelLights.RestLabels.Length ? PanelLights.RestLabels[at] : PanelLights.RestLabels[0];
        }

        /// <summary>Whether a panel's picture is its idle glyph, or dark: a panel no device shows is dark.</summary>
        public static bool MatrixDrawsGlyph(bool? shown)
        {
            return shown != false;
        }

        // --- Quick controls --------------------------------------------------------------------------------

        /// <summary>The brightness the slider edits: the night one while night mode is on, the day one otherwise,
        /// which is the rule the wheel's brightness buttons follow.</summary>
        public static string BrightnessLabel(bool night)
        {
            return night ? PanelSettings.NightBrightnessTitle : PanelSettings.BrightnessTitle;
        }

        public static int BrightnessInForce(bool night, int day, int nightBrightness)
        {
            return night ? nightBrightness : day;
        }

        /// <summary>"80%", beside the slider's label.</summary>
        public static string Percent(int value)
        {
            return Math.Max(0, Math.Min(100, value)).ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>A card's empty state as a sentence: each page's own words, with the full stop the Screens
        /// page's pill leaves off.</summary>
        public static string EmptyLine(string empty)
        {
            var text = (empty ?? string.Empty).Trim();
            return text.Length == 0 || text.EndsWith(".", StringComparison.Ordinal) ? text : text + ".";
        }

        /// <summary>Whether the rig has nothing at all, in which case Right now is the empty rig's card alone.</summary>
        public static bool RigEmpty(int screens, int strips, int matrices)
        {
            return screens + strips + matrices == 0;
        }

        public static readonly PanelSearch.Entry[] Search =
        {
            // By the page's name, which it draws over its headline: the headline itself is "Nothing to fix", "1
            // thing to fix" or "3 things to fix" (PanelAttention.Headline), so no one label names it.
            new PanelSearch.Entry(Title, PanelPage.Home, null, "things to fix", "nothing to fix", "attention", "problem", "warning"),
            new PanelSearch.Entry(RightNowTitle, PanelPage.Home, AnchorRightNow, "live", "showing"),
            new PanelSearch.Entry(QuickControlsTitle, PanelPage.Home, AnchorQuickControls, "brightness", "night mode"),
            new PanelSearch.Entry(PanelSettings.BrightnessTitle, PanelPage.Home, AnchorQuickControls, "lights", "leds", "dim"),
            new PanelSearch.Entry(PanelSettings.NightBrightnessTitle, PanelPage.Home, AnchorQuickControls, "night", "dim"),
            new PanelSearch.Entry(PanelSettings.NightModeTitle, PanelPage.Home, AnchorQuickControls, "dark", "dim"),
            new PanelSearch.Entry(TryTitle, PanelPage.Home, AnchorQuickControls, "try", "emulate", "rig"),
        };

        /// <summary>The greyed rows this page draws (PanelSoon's named entries), which search lists unless one
        /// is InSheetOnly. PanelSoonTests holds the list to this page's own sources: draw a row, add it here.</summary>
        public static readonly SoonItem[] SoonDrawn = new SoonItem[0];

        /// <summary>Search labels this page draws through something other than the constant: the slider's label,
        /// which is whichever brightness is in force (BrightnessLabel).</summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>
        {
            { PanelSettings.BrightnessTitle, "PanelHome.BrightnessLabel(" },
            { PanelSettings.NightBrightnessTitle, "PanelHome.BrightnessLabel(" },
        };
    }
}
