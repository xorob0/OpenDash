// PanelMetrics.cs: the settings panel's geometry -- the sizes, the gaps and the icon paths Widgets.cs
// draws it from.
//
// Kept apart from Widgets.cs for the reason Mark.cs is: the panel is WPF and the net8.0 test project
// cannot compile a line of it, so the numbers live where PanelMetricsTests can reach them and Widgets
// draws from here. Every value is design/canvas/PluginComponents.dc.html's own; where a token already
// says it, the constant is the Theme mirror rather than the literal, so that ThemeTests keeps holding it
// against design/tokens.json. Pure: no WPF types.
using System;
using System.Collections.Generic;

namespace OpenDashPlugin
{
    public static class PanelMetrics
    {
        // The icons the cards draw. The paths themselves are in PanelIcons.cs, which is where the whole
        // set lives and where the test that holds them against the sheet can reach them; these four are
        // the names the card and the add card ask for, and they stay here so that the kind table below
        // reads as one thing.

        public const string DisplayIcon = PanelIcons.Display;
        public const string GridIcon = PanelIcons.Grid;
        public const string PhoneIcon = PanelIcons.Phone;
        public const string PlusIcon = PanelIcons.Plus;

        // The padding inside a field, which the select, the drop button and the text box share because
        // the canvas draws them as one shape. Both sheets write it `padding: 0 8px 0 10px`, twenty times
        // between them, so the left is the wider of the two: the chevron on the right sits inside its own
        // box and needs less room beside it than a value does at the other end. Widgets.cs carried 9 and
        // 6 and said they were the canvas's, which nothing could check from there.

        public const double FieldPaddingLeft = 10;
        public const double FieldPaddingRight = 8;

        /// <summary>
        /// The icon a screen of each kind is drawn with, and null for a kind the canvas gives none.
        /// </summary>
        /// <remarks>
        /// Every kind in Contract.ScreenKinds is a key here, including the one with no icon, so a fifth
        /// kind fails PanelMetricsTests rather than quietly losing its mark on the card. A kind without an
        /// icon keeps a textual home instead: PanelCopy.KindWord is the other half of the same rule.
        /// </remarks>
        private static readonly Dictionary<string, string> KindIcons = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { Contract.KindFace, DisplayIcon },
            { Contract.KindPitWall, GridIcon },
            { Contract.KindCompanion, PhoneIcon },
            { Contract.KindSlots, null },
        };

        /// <summary>Whether the card knows how to say a kind at all, by an icon or by a word.</summary>
        public static bool KnowsKind(string kind)
        {
            return kind != null && KindIcons.ContainsKey(kind);
        }

        /// <summary>The kind's icon, or null when it has none and the word carries it.</summary>
        public static string KindIcon(string kind)
        {
            string path;
            if (kind != null && KindIcons.TryGetValue(kind, out path)) return path;
            return null;
        }

        // Geometry the token file does not carry. docs/design/plugin.md records which of these are owed
        // as tokens; nothing here invents a colour, and every colour below is a Theme constant.

        /// <summary>The one pixel every outline on the panel is drawn at: the card in both states, and
        /// the outline button.</summary>
        public const double BorderWeight = 1;

        public const double Radius = Theme.Radius;
        public const double IconSize = Theme.IconSize;

        // The screen card
        public const double CardMinWidth = 152;
        public const double CardHeight = 58;
        public const double CardPaddingX = 12;
        public const double CardIconGap = 10;
        public const double CardLineGap = 5;

        // The add card
        public const double AddCardMinWidth = 132;
        public const double AddCardGap = 8;

        /// <summary>The dash pattern, in multiples of the stroke. The canvas says `1px dashed` and leaves
        /// the pattern to the browser; this is what the browser draws it as.</summary>
        public const double DashOn = 3;
        public const double DashOff = 3;

        // The status pill
        public const double PillHeight = Theme.ControlHeightSm;
        public const double PillGap = 8;
        public const double DotSize = 6;

        // Buttons
        public const double ButtonHeight = Theme.ControlHeight;

        /// <summary>A button sitting in an install row, which is shorter than the 40 the row is.</summary>
        public const double RowButtonHeight = 28;

        public const double ButtonPaddingX = 16;
        public const double ButtonIconGap = 8;

        /// <summary>
        /// The focus ring: two pixels of accent, two pixels clear of the control's own edge.
        /// </summary>
        /// <remarks>
        /// Mirrors of Theme's, which is what Widgets.cs draws from, rather than a second pair. Two
        /// packages implemented the ring at once and both sets of constants survived the merge, so the
        /// numbers a test could reach were not the numbers a control was drawn with.
        /// </remarks>
        public const double FocusRingWeight = Theme.FocusRing;
        public const double FocusRingOffset = Theme.FocusRingOffset;

        /// <summary>What a disabled button keeps, which is the canvas's 40 %.</summary>
        public const double DisabledOpacity = 0.4;

        // The install row
        public const double RowHeight = 40;
        public const double RowIconGap = 12;
        public const double RowRightGap = 20;
        public const string RowRule = Theme.SurfaceZone;

        // Progress
        public const double ProgressWidth = 240;
        public const double ProgressBarHeight = 4;
        public const double ProgressGap = 8;

        // The section
        public const double SectionPadding = 28;
        public const double SectionGap = 20;
        public const string SectionRule = Theme.Rule;

        /// <summary>The four colours a screen card carries. Selected decides the ink as well as the
        /// brushes, which is why the card is built where the flag is in hand.</summary>
        public sealed class CardColours
        {
            public CardColours(string fill, string border, string icon, string sizeLabel)
            {
                Fill = fill;
                Border = border;
                Icon = icon;
                SizeLabel = sizeLabel;
            }

            /// <summary>The ground, or null for the rest card, which is transparent.</summary>
            public string Fill { get; private set; }

            public string Border { get; private set; }
            public string Icon { get; private set; }
            public string SizeLabel { get; private set; }
        }

        /// <summary>
        /// A card's colours in the state it is in.
        /// </summary>
        /// <remarks>
        /// The selected card used to be a raised fill under an accent underline. It is now the quieter
        /// pair the canvas draws -- ui.field under a uniform one pixel of accent -- so the accent icon and
        /// the accent outline are the whole of the cue, and the rest card is transparent with its icon and
        /// its size in text.label.
        /// </remarks>
        public static CardColours Card(bool selected)
        {
            return selected
                ? new CardColours(Theme.Field, Theme.Accent, Theme.Accent, Theme.TextSecondary)
                : new CardColours(null, Theme.Border, Theme.TextLabel, Theme.TextLabel);
        }

        /// <summary>The percentage a fraction reads as, clamped: a run that reports more than it has done
        /// must not put a number over a hundred on the panel.</summary>
        public static int PercentOf(double fraction)
        {
            return (int)Math.Round(Clamp(fraction) * 100);
        }

        /// <summary>How wide the filled part of a progress bar is, clamped to its track for the same
        /// reason.</summary>
        public static double ProgressFill(double fraction, double width)
        {
            return Clamp(fraction) * width;
        }

        private static double Clamp(double fraction)
        {
            if (double.IsNaN(fraction) || fraction < 0) return 0;
            return fraction > 1 ? 1 : fraction;
        }
    }
}
