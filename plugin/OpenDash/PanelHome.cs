// PanelHome.cs: the Home page's words, numbers and decisions -- its title, its sections, what each device's
// row says under its name, which pictures are live, and what search finds on it.
//
// Home says what needs fixing (PanelAttention decides that), what each device is showing, and the two
// controls a driver reaches for between sessions. SettingsControl.Home.cs only draws what this file decides,
// to Main.dc.html. Pure: no WPF, no SimHub. PanelHomeTests holds its strings and its rules.
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

        /// <summary>The chip the cell's press opens Rig on: the one the driver picked there this session, else the
        /// yellow flag, so a press under "Flags and spotter" never lands on Rig's default revs chip.</summary>
        public static string TryScenario(string selected)
        {
            return selected != null && PanelEmulation.Find(selected) != null ? selected : PanelEmulation.Yellow;
        }

        /// <summary>The link in each Right now card's head, to that card's page.</summary>
        public const string OpenLink = "Open";

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

        /// <summary>The Right now cards: three to a row where there is the room, as the artboard's
        /// repeat(3, minmax(0, 1fr)) lays them, and none narrower than the brief's 280.</summary>
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

        /// <summary>The empty rig, which the artboard does not draw: the Screens page's add tile in a column as
        /// wide as a Right now card, and the sentence 12 under it.</summary>
        public const double EmptyRigGap = 12;

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

        /// <summary>
        /// What the headline and the fix rows were drawn from, as one key: each issue's id and every word its
        /// row draws.
        /// </summary>
        public static string DrawnFrom(IEnumerable<PanelIssue> issues)
        {
            return string.Join("\n", (issues ?? Enumerable.Empty<PanelIssue>()).Where(issue => issue != null).Select(issue =>
                string.Join("|", new[]
                {
                    issue.Id, issue.Title, issue.Detail, issue.ActionLabel,
                    string.Join(">", issue.Steps.Select(step => string.Join("/", step ?? new string[0]))),
                })));
        }

        /// <summary>
        /// What the headline, the fix rows and the Right now lines were drawn from, as one key: the issues'
        /// key, and each device's facts as the rows copied them at build (<see cref="FactKey"/>). Home draws
        /// itself again on an update check's answer only when this moved, since the answer lands whenever it
        /// lands, a drag or a press included; the answer re-reads the facts too, and a row keeps the ones it
        /// copied, so a fact that moved without moving an issue would otherwise leave a row stale.
        /// </summary>
        public static string DrawnFrom(IEnumerable<PanelIssue> issues, IEnumerable<string> facts)
        {
            return DrawnFrom(issues) + "\n#\n" + string.Join("\n", facts ?? Enumerable.Empty<string>());
        }

        /// <summary>One device's facts as a row copies them, for <see cref="DrawnFrom(IEnumerable{PanelIssue}, IEnumerable{string})"/>:
        /// its kind, its id, and each fact, a fact nobody could read written apart from every value.</summary>
        public static string FactKey(string kind, string id, params object[] facts)
        {
            var parts = new List<string> { kind ?? string.Empty, id ?? string.Empty };
            parts.AddRange((facts ?? new object[0]).Select(fact => fact == null ? "?" : Convert.ToString(fact, CultureInfo.InvariantCulture)));
            return string.Join("|", parts);
        }

        /// <summary>The widest an issue's press is drawn beside its text: "Open " and a name the driver typed,
        /// which nothing caps, trims there rather than taking the title's room. Every fixed label, "Install it
        /// again" the longest, fits whole.</summary>
        public const double PressMaxWidth = 200;

        /// <summary>The narrowest text column a press beside it may leave: under that the sentence becomes a
        /// column of words, and the press goes under the text instead.</summary>
        public const double PressTextMinWidth = 300;

        /// <summary>The content width from which an issue's press sits beside its text: the attention card's
        /// border on each side (Ui.CardBox), the row's padding, the icon well and its gap, the press's gap and
        /// its widest, and the text column's narrowest.</summary>
        public const double PressBesideFrom = PanelMetrics.BorderWeight * 2 + IssuePaddingX * 2 + IconWell + IconGap + IconGap + PressMaxWidth + PressTextMinWidth;

        /// <summary>Whether an issue's press sits beside its text, as a row's trailing action does at any width
        /// the text column keeps its room, the rail's included; where it would not, it goes under the text.</summary>
        public static bool PressBeside(double contentWidth)
        {
            return contentWidth >= PressBesideFrom;
        }

        /// <summary>
        /// What Home says after Check again, from the issues and the strip's facts as SimHub gave them the second
        /// time: what is still to fix, in the caution's ink; that the profile is selected, only when SimHub said
        /// so; and otherwise what could not be read or is left, with the step that is left, where it is taken.
        /// </summary>
        /// <remarks>
        /// An issue also goes from the list when a fact could not be read: SimHub's LED settings were out of
        /// reach, the strip's device is not in SimHub's device list, or the selection could not be read.
        /// Saying "fixed" then would tell a driver who changed nothing that the problem is gone, so only a
        /// read of Selected == true on an installed profile says it. A build that carries no profile for the
        /// strip is not a read that failed: SimHub answered, and there is nothing to compare its copy with,
        /// so that is said, as the LEDs page says it (PanelLeds.NoProfileForStrip on the LEDs branch). A
        /// device SimHub no longer lists comes before the profile: removing a device takes its profiles with
        /// it, so the profile then reads as not installed, and the LEDs page offers no Install for a strip
        /// whose device is not listed. A profile that failed to install reads as one that is not installed,
        /// as the LEDs card reads it: Check again installs nothing, so "failed" would report an install
        /// nobody tried. A result with its step is two sentences and drops "Checked again.", which two is the
        /// ceiling for (voice.md).
        /// </remarks>
        /// <param name="strip">What was read about the issue's strip the second time, or null.</param>
        public static PanelMessage CheckedAgain(PanelIssue before, IEnumerable<PanelIssue> after, AttentionStrip strip)
        {
            var still = before == null ? null : PanelAttention.Of(after, before.Id, null);
            if (still != null) return PanelMessage.Caution(Checked + still.Title + ".");
            var profile = strip == null ? null : strip.Profile;
            if (profile == FlagBoxInstallState.NotEmbedded) return PanelMessage.Caution(Checked + CheckedNoProfile + StripName(strip, false) + ".");
            var installed = profile == FlagBoxInstallState.UpToDate || profile == FlagBoxInstallState.Outdated;
            var missing = profile == FlagBoxInstallState.NotInstalled || profile == FlagBoxInstallState.Failed;
            // SimHub's LED settings could not be read, or say nothing about this strip's profile.
            if (!installed && !missing) return PanelMessage.Caution(Checked + PanelLightRows.Unavailable);
            var name = StripName(strip, true);
            if (string.IsNullOrWhiteSpace(strip.DeviceName)) return PanelMessage.Caution(name + CheckedNoDevice);
            if (missing) return PanelMessage.Info(name + CheckedNotInstalled);
            if (strip.Selected == true) return PanelMessage.Info(Checked + name + CheckedSelected);
            if (strip.Selected == false) return PanelMessage.Caution(Checked + name + CheckedNotSelected);
            return PanelMessage.Caution(CheckedUnread + StripName(strip, false) + CheckedUnreadTail);
        }

        /// <summary>A strip's name for a sentence, or <see cref="ThisStrip"/> where it has none, capitalised
        /// only where it starts the sentence.</summary>
        private static string StripName(AttentionStrip strip, bool starts)
        {
            if (!string.IsNullOrWhiteSpace(strip.Name)) return strip.Name.Trim();
            return starts ? ThisStrip : ThisStrip.ToLowerInvariant();
        }

        public const string Checked = "Checked again. ";
        public const string CheckedSelected = "'s profile is selected.";

        /// <summary>The step that is left, named where it is taken (voice.md, Messages).</summary>
        public const string CheckedNotInstalled = "'s profile is not installed. Install it on the LEDs page.";

        public const string CheckedNotSelected = "'s profile is not selected.";

        /// <summary>The strip's device is not in SimHub's device list, in the LEDs page's one phrase for it
        /// (PanelLeds.DeviceNotListed on the LEDs branch: "This strip's device is not in SimHub."), and the
        /// step, which is taken on that page under SimHub device.</summary>
        public const string CheckedNoDevice = "'s device is not in SimHub. Choose one on the LEDs page.";

        /// <summary>The build carries no profile for the strip, as the LEDs page says it
        /// (PanelLeds.NoProfileForStrip on the LEDs branch: "This build ships no profile for this strip.").</summary>
        public const string CheckedNoProfile = "This build ships no profile for ";

        /// <summary>The device is there and whether the profile is selected could not be read, which SimHub's
        /// log has the reason for (SettingsControl.Status.cs writes it): voice.md's failure form, "Could not
        /// ...", in the words of the row just pressed (profile, selected), and the log.</summary>
        public const string CheckedUnread = "Could not read whether ";
        public const string CheckedUnreadTail = "'s profile is selected. See SimHub's log.";

        /// <summary>A strip with no name, which a hand-edited settings file can leave.</summary>
        public const string ThisStrip = "This strip";

        // --- Right now: screens ----------------------------------------------------------------------------

        /// <summary>A screen's size beside its name, or nothing: for a size nobody knows (a zero Width or
        /// Height, as a migrated face whose folder named no size has), and for a screen whose name is already
        /// its size ("1280 × 480", the name an unnamed package's screen is given), which would say it twice.</summary>
        public static string ScreenSize(ScreenInstance screen)
        {
            if (screen == null || screen.Width <= 0 || screen.Height <= 0) return string.Empty;
            var size = screen.SizeLabel;
            return string.Equals((screen.Name ?? string.Empty).Trim(), size, StringComparison.Ordinal) ? string.Empty : size;
        }

        /// <summary>
        /// A screen's line: what SimHub is missing when something is, in the Screens card's words and ink, and
        /// otherwise what the screen shows now.
        /// </summary>
        /// <param name="installed">Whether its dashboard is in SimHub, or null when the disk could not be asked.</param>
        /// <param name="waitsForRestart">Whether SimHub has not loaded it yet (PanelAttention.ScreenRestart).</param>
        public static HomeLine ScreenLine(OpenDashSettings settings, ScreenInstance screen, bool? installed, bool waitsForRestart)
        {
            if (installed == false) return new HomeLine(PanelScreens.Missing, Theme.StatusFailed, Theme.StatusFailed);
            if (waitsForRestart) return new HomeLine(PanelCopy.RestartToLoad, Theme.Caution, Theme.Caution);
            return new HomeLine(ScreenShows(settings, screen), Theme.TextSecondary, installed == true ? Theme.StatusUpToDate : null);
        }

        /// <summary>
        /// What a screen shows now, in the names its own controls give it.
        /// </summary>
        /// <remarks>
        /// A face: the page each zone is on, in the panel's zone order ("Lap times · Gear, speed, revs ·
        /// Leaderboard · Fuel"). A landscape pit wall: its page ("Race page"). A portrait pit wall, which is its
        /// own package with one page and never reads PitWallPage: its four zones' pages, as a face's line is. A
        /// companion: how much of the catalogue its
        /// rotation holds ("12 of 21 modules"), since which module it is on is the phone's own and not
        /// something OpenDash is told. A card face: the cards its package reads, slot by slot (SlotsRead).
        /// Nothing is made up for a kind the panel does not know.
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
            if (screen.IsPitWall) return PitWallPortrait(screen) ? PortraitZones(screen) : PitWallPage(screen.PitWallPage);
            if (screen.IsCompanion) return ModulesLine(screen.Modules);
            if (screen.IsSlots)
            {
                var read = SlotsRead(screen.Width, screen.Height);
                return settings == null || read == 0 ? string.Empty : CardsLine(Enumerable.Range(1, read).Select(settings.Slot));
            }
            return string.Empty;
        }

        /// <summary>
        /// How many slots a card face's package reads, by its size: the first that many of the rig's twelve,
        /// since a face with fewer slots uses the first ones. Nought for a size no package is drawn at, whose
        /// cards nobody knows.
        /// </summary>
        /// <remarks>
        /// build/manifest.json's slots, which packages/dash/test/e2e.test.ts pins and PanelHomeTests reads
        /// back from there, so a package that gains a slot fails a test here rather than Home naming a card
        /// the screen does not draw.
        /// </remarks>
        public static int SlotsRead(int width, int height)
        {
            foreach (var entry in SlotFaces)
            {
                if (entry[0] == width && entry[1] == height) return entry[2];
            }
            return 0;
        }

        /// <summary>Width, height and slots of each card face the build ships.</summary>
        public static readonly int[][] SlotFaces =
        {
            new[] { 1920, 480, 12 },
            new[] { 1280, 480, 8 },
            new[] { 1280, 400, 8 },
            new[] { 850, 480, 6 },
            new[] { 800, 480, 6 },
            new[] { 1280, 720, 12 },
            new[] { 800, 286, 4 },
            new[] { 600, 686, 6 },
            new[] { 480, 480, 2 },
            new[] { 800, 800, 6 },
        };

        public const string Separator = " · ";

        /// <summary>"Race page": the pit wall's page as the Screens page's Page control names it.</summary>
        public static string PitWallPage(int page)
        {
            return Contract.PitWallPageNames[Contract.NormalisePitWallPage(page)] + " page";
        }

        /// <summary>Whether a pit wall is the portrait package ("OpenDash Pit wall portrait", 1080 × 1920), told
        /// by its shape as the Screens and Rig pages tell it: that package draws one page of four zones, and
        /// has no Race, Tower or Telemetry page for PitWallPage to name.</summary>
        public static bool PitWallPortrait(ScreenInstance screen)
        {
            return screen != null && screen.IsPitWall && screen.Height > screen.Width;
        }

        /// <summary>"Fuel · Tyres · Relative · Opponents": the portrait pit wall's zones A to D, by the names
        /// the Screens page's zone controls give their pages.</summary>
        public static string PortraitZones(ScreenInstance screen)
        {
            if (screen == null) return string.Empty;
            return string.Join(Separator, Contract.PitWallZoneSlots
                .Where(slot => !slot.Landscape)
                .Select(slot => slot.Wide ? ZonePages.WideName(screen.ZonePage(slot.Key)) : ZonePages.StandardName(screen.ZonePage(slot.Key))));
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

        /// <summary>The #369 switch's noun, read from the switch itself (PanelLeds.CarRevLightsTitle), so one
        /// thing has one name (voice.md, "One word per thing"): the car's own rev lights, never just "lights",
        /// and renamed with the switch if it is. Main.dc.html's "Car-specific · Porsche table" names the
        /// tables behind it, which is the panel talking to itself.</summary>
        public const string CarLightsLine = PanelLeds.CarRevLightsTitle;

        // A strip's state, in the LEDs cards' own words, read from PanelLeds rather than copied (#524): Installed
        // and Not installed are PanelCopy's, which the Updates page's rows say too. A failed install, and a
        // strip whose profile this build does not ship (ruling 2), read as Not installed, as the LEDs card reads
        // them.
        public const string StripShowing = PanelLeds.Showing;
        public const string StripNotSelected = PanelLeds.NotSelected;
        public const string StripUpdateAvailable = PanelLeds.UpdateAvailable;
        public const string StripInstalled = PanelLeds.Installed;
        public const string StripNotInstalled = PanelLeds.NotInstalled;

        /// <summary>The ends and the centre a strip is drawn with. A shape the panel cannot read is drawn as the
        /// Rig page draws it, 3/9/3.</summary>
        public static int[] StripSpan(string shape)
        {
            var parsed = LightShape.Parse(shape);
            return parsed == null ? new[] { 3, 9 } : new[] { parsed.Left, parsed.Centre };
        }

        /// <summary>The shape beside a strip's name, as Main.dc.html and the LEDs cards write it: the three counts
        /// with a spaced dot between them ("3 · 9 · 3"), or the one count of a bare run ("15"). An id this
        /// cannot read is written as the Updates page writes it.</summary>
        public static string StripShape(LedBar bar)
        {
            if (bar == null || string.IsNullOrEmpty(bar.Shape)) return string.Empty;
            var shape = LightShape.Parse(bar.Shape);
            if (shape == null) return PanelLightRows.ShapeLabel(bar.Shape);
            if (shape.Bare) return Digits(shape.Centre);
            return Digits(shape.Left) + Separator + Digits(shape.Centre) + Separator + Digits(shape.Right);
        }

        private static string Digits(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Whether a strip's picture is the car's own lights as they are now, or dark.
        /// </summary>
        /// <remarks>
        /// Only what OpenDash computes is drawn: the car's run, while the car tables have a car
        /// (CarLightService.Ready), on a strip whose centre shows the revs in the car's own style, and whose
        /// profile SimHub is not known to be missing or not showing. Flags, the spotter and every other effect
        /// are drawn by the profile inside SimHub, which the plugin does not see, so a strip showing one of
        /// those is drawn dark rather than guessed at. A strip whose line says Not installed is drawn dark
        /// too, a build that carries no profile for it included (<see cref="StripNotInstalledFor"/>): the
        /// picture never shows lights the line says nothing is there to draw.
        /// </remarks>
        public static bool StripLive(bool carReady, string rpmStyle, string centre, FlagBoxInstallState? profile, bool? selected)
        {
            if (!carReady) return false;
            if (!string.Equals(Contract.NormaliseLedRpmStyle(rpmStyle), Contract.LedRpmStyleCar, StringComparison.Ordinal)) return false;
            if (!string.Equals(Contract.NormaliseLedCentre(centre), Contract.LedCentres[0], StringComparison.Ordinal)) return false;
            if (StripNotInstalledFor(profile)) return false;
            return selected != false;
        }

        /// <summary>
        /// Whether a strip's line says Not installed: SimHub has no profile for it, the last install failed,
        /// or this build carries none, as the LEDs card says each of them (PanelLeds.StateText on the LEDs
        /// branch, whose default case is Not installed).
        /// </summary>
        /// <remarks>
        /// One strip state has one word on both pages (voice.md), and the LEDs card says Not installed for a
        /// profile this build does not carry, with its header saying the rest ("This build ships no profile
        /// for this strip." in place of an Install press). Saying nothing for it, as Home once did, is the
        /// other reading, and needs a ruling: one for silence moves this and the LEDs card's StateText
        /// together, each with its pin.
        /// </remarks>
        public static bool StripNotInstalledFor(FlagBoxInstallState? profile)
        {
            return profile == FlagBoxInstallState.NotInstalled || profile == FlagBoxInstallState.Failed || profile == FlagBoxInstallState.NotEmbedded;
        }

        /// <summary>
        /// Whether a strip's line keeps its room while it has nothing to say: a strip that goes live when a
        /// car's tables are ready, whose line then says the car's own rev lights. The room is one line, and
        /// the line is drawn on one, trimmed however long the car's name, so its card keeps one height as a
        /// session starts and ends, and the cards and the quick controls under it never move under the
        /// pointer, as the sidebar's live card does not (PanelShell.LiveCardHeight). A strip that can never
        /// be live gives its line's room back.
        /// </summary>
        public static bool StripLineKeepsRoom(string rpmStyle, string centre, FlagBoxInstallState? profile, bool? selected)
        {
            return StripLive(true, rpmStyle, centre, profile, selected);
        }

        /// <summary>
        /// A strip's line: the profile that is not in SimHub, then one that is and is not selected, then one
        /// with an update, then the car's own rev lights while they are live, then the profile's state.
        /// Nothing at all when SimHub could not be asked.
        /// </summary>
        /// <remarks>
        /// Amber only where the attention card has a row for it (StripUnselected, StripOutdated), so an amber
        /// line never sits under "Nothing to fix". A profile that is not installed is the LEDs card's Install
        /// press, in the secondary ink and with no dot; a failed install is one that is not installed, since
        /// nothing Home reads ever says Failed and the LEDs card says Not installed for it; and so is a
        /// profile this build does not carry, as the LEDs card says it (<see cref="StripNotInstalledFor"/>).
        /// The green dot is for a selection SimHub reported, never for one nobody could read.
        /// </remarks>
        public static HomeLine StripLine(bool live, string carName, FlagBoxInstallState? profile, bool? selected)
        {
            if (StripNotInstalledFor(profile)) return new HomeLine(StripNotInstalled, Theme.TextSecondary, null);
            var installed = profile == FlagBoxInstallState.UpToDate || profile == FlagBoxInstallState.Outdated;
            if (installed && selected == false) return new HomeLine(StripNotSelected, Theme.Caution, Theme.Caution);
            if (profile == FlagBoxInstallState.Outdated) return new HomeLine(StripUpdateAvailable, Theme.StatusUpdateAvailable, Theme.StatusUpdateAvailable);
            var dot = installed && selected == true ? Theme.StatusUpToDate : null;
            if (live) return new HomeLine(string.IsNullOrWhiteSpace(carName) ? CarLightsLine : CarLightsLine + Separator + carName.Trim(), Theme.TextSecondary, dot);
            if (!installed) return new HomeLine(string.Empty, Theme.TextSecondary, null);
            return new HomeLine(selected == true ? StripShowing : StripInstalled, Theme.TextSecondary, dot);
        }

        // --- Right now: matrix -----------------------------------------------------------------------------

        /// <summary>The Matrix card's word for a slot no device shows, read from the card (#524, ruling 8).</summary>
        public const string MatrixNotShown = PanelMatrix.NotShown;

        /// <summary>
        /// "Matrix 1 · Gear": the SimHub content number and the idle display, as the Idle display control
        /// names it; "Matrix 2 · Not shown in SimHub" in the caution's ink when no device shows the slot. A
        /// matrix still called by its number drops the number, which its name over the line already says.
        /// </summary>
        /// <remarks>
        /// No dot for the flag box profile's update: the line would not say what the amber meant, and the
        /// attention card already has the row for it. Which content number a device shows is not read yet
        /// (#521), so every rig's panels are Shown null today: the amber line, its dark picture and the
        /// green dot are drawn once that ticket fills Shown, and until then a panel nobody checked is not
        /// called dark.
        /// </remarks>
        public static HomeLine MatrixLine(int slot, string name, string rest, bool? shown)
        {
            var head = string.Equals((name ?? string.Empty).Trim(), MatrixName(slot), StringComparison.Ordinal) ? string.Empty : MatrixName(slot) + Separator;
            if (shown == false) return new HomeLine(head + MatrixNotShown, Theme.Caution, Theme.Caution);
            return new HomeLine(head + RestLabel(rest), Theme.TextSecondary, shown == true ? Theme.StatusUpToDate : null);
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

        /// <summary>Whether the rig has no screen, no strip and no matrix, in which case the empty rig stands in
        /// place of Right now and the quick controls, and only what needs fixing stays above it.</summary>
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
            // Every heading and every label Home draws is an entry (search covers every row label and section
            // heading on every page), its quick controls' included, as Settings and Rig list their own switches
            // too. The brightness's label is whichever brightness is in force, so the section's name carries
            // the word (SearchDrawnOtherwise). An empty rig draws neither Right now nor the quick controls: one
            // rule for all four of these entries, which then land on the add tile that stands in Right now's
            // place, or at the top of a page short enough to show it all (The_empty_rig_stands_in_place_of_right_now).
            new PanelSearch.Entry(QuickControlsTitle, PanelPage.Home, AnchorQuickControls, "brightness"),
            new PanelSearch.Entry(PanelSettings.NightModeTitle, PanelPage.Home, AnchorQuickControls, "dark", "dim"),
            new PanelSearch.Entry(TryTitle, PanelPage.Home, AnchorQuickControls, "try", "emulate", "rig"),
        };

        /// <summary>The greyed rows this page draws (PanelSoon's named entries), which search lists unless one
        /// is InSheetOnly. PanelSoonTests holds the list to this page's own sources: draw a row, add it here.</summary>
        public static readonly SoonItem[] SoonDrawn = new SoonItem[0];

        /// <summary>Search labels this page draws through something other than the constant: none. The slider's
        /// label is whichever brightness is in force (BrightnessLabel), so neither brightness is a Home entry: a
        /// result is labelled by what it lands on, and a fixed "Night brightness · Home" would land on the day
        /// slider by day. Settings lists both, on the page that draws both; Quick controls carries the word.</summary>
        public static readonly IReadOnlyDictionary<string, string> SearchDrawnOtherwise = new Dictionary<string, string>();
    }
}
