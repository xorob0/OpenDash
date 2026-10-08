/**
 * The faces the dashboards draw with, as the browser loads them.
 *
 * A `.djson` names two families. `openDash Display` is Barlow Condensed with its family renamed so
 * WPF has no width word to fold (`docs/research/simhub-dash-format.md`, #159), and `Barlow` is
 * Barlow. The woff2 files are the site's own, converted from the same TTFs the packages bundle by
 * `scripts/fonts.ts`, so a run is set in the face it was measured in.
 *
 * They are registered under names of the demo's own rather than the site's `Barlow`, so that the
 * page's stylesheet and the canvas never disagree about which file a weight comes from.
 */

interface Face {
  readonly family: string;
  readonly file: string;
  readonly weight: number;
}

const DISPLAY = 'OpenDash Demo Display';
const LABEL = 'OpenDash Demo Barlow';

export const DEMO_FACES: readonly Face[] = [
  { family: DISPLAY, file: '/fonts/BarlowCondensed-Light.woff2', weight: 300 },
  { family: DISPLAY, file: '/fonts/BarlowCondensed-SemiBold.woff2', weight: 600 },
  { family: DISPLAY, file: '/fonts/BarlowCondensed-Bold.woff2', weight: 700 },
  { family: LABEL, file: '/fonts/Barlow-Regular.woff2', weight: 400 },
  { family: LABEL, file: '/fonts/Barlow-Medium.woff2', weight: 500 },
  { family: LABEL, file: '/fonts/Barlow-SemiBold.woff2', weight: 600 },
  { family: LABEL, file: '/fonts/Barlow-Bold.woff2', weight: 700 },
];

/** WPF's weight names as CSS numbers. */
export const WEIGHTS: Readonly<Record<string, number>> = {
  Thin: 100,
  ExtraLight: 200,
  UltraLight: 200,
  Light: 300,
  Normal: 400,
  Regular: 400,
  Medium: 500,
  SemiBold: 600,
  DemiBold: 600,
  Bold: 700,
  ExtraBold: 800,
  UltraBold: 800,
  Black: 900,
  Heavy: 900,
};

/** The family a canvas asks for, for the family a `.djson` names. */
export function familyFor(font: string): string {
  if (font === 'openDash Display' || font === 'Barlow Condensed') return DISPLAY;
  if (font === 'Barlow') return LABEL;
  return font;
}

/** The canvas `font` shorthand for a text item. */
export const cssFont = (font: string, weight: string, size: number, italic: boolean): string =>
  `${italic ? 'italic ' : ''}${WEIGHTS[weight] ?? 400} ${size}px "${familyFor(font)}", sans-serif`;

let loading: Promise<void> | null = null;

/**
 * Registers and loads every face, once. Resolves when all of them are ready, so the first frame is
 * drawn in the right face rather than in a fallback that is then replaced.
 */
export function loadDemoFonts(): Promise<void> {
  if (loading) return loading;
  if (typeof document === 'undefined' || typeof FontFace === 'undefined') return Promise.resolve();
  loading = Promise.all(
    DEMO_FACES.map(async (f) => {
      const face = new FontFace(f.family, `url(${f.file}) format('woff2')`, { weight: String(f.weight), style: 'normal' });
      document.fonts.add(await face.load());
    }),
  ).then(() => undefined);
  return loading;
}
