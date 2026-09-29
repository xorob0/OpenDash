// PanelKit.cs: the numbers the kit's shared controls are drawn with (Widgets.Kit.cs), each read off the CSS class
// the #503 artboards draw it with, so that PanelKitTests can hold a card, a chip or a sheet's step to its
// artboard rather than leave it to the VM.
//
// The frame's numbers are PanelShell's; these are the controls every page shares inside it. A page agent that
// needs one of these at another size takes the constant rather than typing a number beside the control.
//
// Pure: no WPF and no SimHub types. Compiled into OpenDash.Tests by the Panel*.cs wildcard.
namespace OpenDashPlugin
{
    public static class PanelKit
    {
        // --- The device card: Screens' .scard, which LEDs and Matrix draw their cards as ---------------

        /// <summary>.scard's padding and the gap its column puts between the picture, the name block and
        /// the state line.</summary>
        public const double CardPadding = 10;
        public const double CardGap = 8;

        /// <summary>The name at 14/600 and the line of facts under it at 12, 2 apart.</summary>
        public const double CardNameSize = 14;
        public const double CardMetaSize = 12;
        public const double CardMetaGap = 2;

        /// <summary>The state line: an 11 px word led by a 6 px dot, 6 apart.</summary>
        public const double CardStateSize = 11;
        public const double CardStateDot = 6;
        public const double CardStateGap = 6;

        /// <summary>The accent bar along a selected card's foot.</summary>
        public const double CardFootBar = 2;

        /// <summary>The gap between cards in the grid, and the narrowest a card is let become before the grid
        /// takes a column fewer: the artboard fixes six columns at its 1200 px and gives no minimum.</summary>
        public const double CardGridGap = 10;
        public const double CardMinWidth = 150;

        // --- The dashed tile beside the cards: "Add a screen" ----------------------------------------

        /// <summary>The plus over its words, 6 apart, the words at 13/500.</summary>
        public const double AddTileGap = 6;
        public const double AddTileTextSize = 13;

        /// <summary>What the tile holds when the grid row is shorter; the artboard sizes it from the row.</summary>
        public const double AddTileMinHeight = 96;

        // --- Chips ---------------------------------------------------------------------------------------

        /// <summary>Rig's .chip: 30 high and 11 in, the word at 13/500, a 10 px swatch 6 before it.</summary>
        public const double ChipHeight = 30;
        public const double ChipPaddingX = 11;
        public const double ChipTextSize = 13;
        public const double ChipSwatch = 10;
        public const double ChipSwatchGap = 6;

        /// <summary>Screens' .chip, the binding chip on a zone or a glance: 26 high and 9 in.</summary>
        public const double BindingChipHeight = 26;
        public const double BindingChipPaddingX = 9;

        /// <summary>Shortcuts' and Settings' .key, the same chip where a binding is the row's subject: 28 high
        /// and 10 in.</summary>
        public const double KeyHeight = 28;
        public const double KeyPaddingX = 10;

        // --- The fix box: .fix ---------------------------------------------------------------------------

        /// <summary>.fix: 14 above and below and 18 in (Screens.dc.html; LEDs and Matrix draw 16 above and
        /// below), the icon 16 before the words, the detail 3 under the title and the steps 10 under that.</summary>
        public const double FixPaddingX = 18;
        public const double FixPaddingY = 14;
        public const double FixIconGap = 16;
        public const double FixDetailGap = 3;
        public const double FixStepsGap = 10;

        // --- Choices in a sheet --------------------------------------------------------------------------

        /// <summary>AddLeds' .dev, a radio row: 10 above and below, 12 in, a 14 px ring 12 before the name,
        /// the name at 14 and the caption at 12.</summary>
        public const double RadioRowPaddingX = 12;
        public const double RadioRowPaddingY = 10;
        public const double RadioRowGap = 12;
        public const double RadioRing = 14;
        public const double RadioNameSize = 14;
        public const double RadioMetaSize = 12;

        /// <summary>AddScreen's .kind and AddLeds' .hw: 12 all round.</summary>
        public const double ChoiceTilePadding = 12;

        /// <summary>AddScreen's .tile, the size tile: 12 above, 8 in and 10 below.</summary>
        public const double SizeTilePaddingTop = 12;
        public const double SizeTilePaddingX = 8;
        public const double SizeTilePaddingBottom = 10;

        /// <summary>A chosen tile's accent edge; at rest it is the one pixel border.</summary>
        public const double ChoiceTileChosenEdge = 2;

        // --- A sheet's step: .step and .stepn ------------------------------------------------------------

        /// <summary>.step: 20 above and below under a rule, the body 12 under the head; the first step of a
        /// sheet has neither the rule nor the 20 above.</summary>
        public const double StepPaddingY = 20;
        public const double StepBodyGap = 12;

        /// <summary>The step's title at 16/600, as AddScreen.dc.html and AddLeds.dc.html draw all seven.</summary>
        public const double StepTitleSize = 16;

        /// <summary>.stepn: a 22 px ring, its figure at 12, 10 before the title.</summary>
        public const double StepNumberSize = 22;
        public const double StepNumberTextSize = 12;
        public const double StepHeadGap = 10;

        /// <summary>AddLeds' ends buttons: its .seg button's min-width.</summary>
        public const double SegmentMinWidth = 40;

        // --- The slider ------------------------------------------------------------------------------------

        /// <summary>
        /// The kit's 0 to 100 slider: a 4 px track, a 14 px thumb, a 22 px surface to press, and the value 12
        /// beside it at 15. The artboards draw the browser's range input in the accent and give no numbers
        /// of their own, so these are the kit's.
        /// </summary>
        public const double SliderTrack = 4;
        public const double SliderThumb = 14;
        public const double SliderHeight = 22;
        public const double SliderMinWidth = 120;
        public const double SliderValueGap = 12;
        public const double SliderValueSize = 15;
        public const double SliderValueMinWidth = 40;
    }
}
