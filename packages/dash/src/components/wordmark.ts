/**
 * The wordmark: `open` in Light and `Dash` in Bold, which `docs/design/brand.md` says is the whole
 * of it -- two weights of the display family, no second typeface and no letter spacing.
 *
 * It lived in `screens/pitwallHeader.ts` while that strip was its only caller. The idle screen
 * draws the same two words on every face, and a DDU has no business importing a pit wall's header,
 * so the drawing moved here unchanged. It is the one component both a face and a second screen
 * draw: it is the identity rather than a readout, which is why it is not in `second/`.
 */
import type { TextItem } from '../generator.ts';
import { measureText } from '../design/advances.ts';
import { rect } from '../design/geometry.ts';
import { ds } from '../tokens.ts';

/**
 * Ink width of the mark at a font size: the two halves measured in the two weights they are drawn
 * in, which is what a caller lays out from.
 *
 * Each half is measured in its own face because "Dash" set in Bold is wider than the same letters
 * in any other, and a box measured from the wrong one loses its last letter.
 */
export const wordmarkWidth = (fs: number): number =>
  Math.ceil(measureText('BarlowCondensedLight', 'open', fs)) + 2 + Math.ceil(measureText('BarlowCondensedBold', 'Dash', fs)) + 2;

export interface WordmarkOptions {
  /**
   * Widest the two boxes may reach from `x`, typically the room the frame leaves. The slack below is
   * capped by it and never falls below the measured letters, exactly as `numeral`'s own `maxWidth`
   * caps its slack at the card's inner width.
   *
   * Without the cap the slack decides a layout. On the 480 x 850 portrait companion the centred mark
   * at 116 px has its letters 39 px clear of both edges, but the inflated "Dash" box ends 19 px past
   * the canvas, and a screen that rejects a rung for a box drawing nothing loses nearly half the mark
   * for pixels that are not there.
   */
  maxWidth?: number;
}

/** The wordmark, in the two weights the brand uses. Barlow Condensed Light and Bold are bundled. */
export function wordmark(name: string, x: number, top: number, fs: number, opts: WordmarkOptions = {}): { items: TextItem[]; width: number } {
  // Each half is measured in its own weight: "Dash" set in Bold is wider than the same letters in
  // any other face, and a box measured from the wrong one loses its last letter.
  //
  // The two halves are the only text OpenDash draws in a weight WPF may have to synthesise, since
  // a SimHub install can be missing a face the package ships. The boxes are therefore a quarter
  // wider than the measurement, which costs nothing (they are transparent, left aligned, and the
  // layout uses the measured width) and leaves no way for the wordmark to lose a letter.
  const openWidth = Math.ceil(measureText('BarlowCondensedLight', 'open', fs)) + 2;
  const dashWidth = Math.ceil(measureText('BarlowCondensedBold', 'Dash', fs)) + 2;
  // Floored, so a box whose left rounds up still ends inside the room it was given, and never below
  // the letters it holds: the cap trades slack a reader cannot see for room, and clipping is the one
  // thing it may not buy.
  const limit = opts.maxWidth === undefined ? undefined : x + Math.floor(opts.maxWidth);
  const boxOf = (left: number, width: number): number => {
    const wanted = Math.ceil(width * 1.25) + 4;
    return limit === undefined ? wanted : Math.max(width, Math.min(wanted, limit - left));
  };
  const common = { font: ds.font.data, fontSize: fs, textColor: ds.color.text.primary, hAlign: 'left', vAlign: 'top', backgroundColor: '#00FFFFFF' } as const;
  const boxTop = Math.round(top - 0.1 * fs);
  const height = Math.ceil(1.2 * fs) + 1;
  return {
    width: openWidth + dashWidth,
    items: [
      { kind: 'text', name: `${name}.open`, rect: rect(x, boxTop, boxOf(x, openWidth), height), text: 'open', fontWeight: 'Light', ...common },
      { kind: 'text', name: `${name}.dash`, rect: rect(x + openWidth, boxTop, boxOf(x + openWidth, dashWidth), height), text: 'Dash', fontWeight: 'Bold', ...common },
    ],
  };
}
