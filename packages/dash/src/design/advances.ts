/**
 * Advance widths in em, measured from the bundled TTFs, for the characters OpenDash draws. They
 * are how a proportional string's width is known before SimHub renders it: SimHub hands the box
 * to WPF as `MaxTextWidth` and clips whatever does not fit, so a label that does not fit must be
 * shortened by the card rather than cut by the renderer.
 *
 * Generated from packages/dash/fonts by tools/measure-advances (see the design notes); regenerate
 * when a font file changes. Characters absent from the table fall back to `FALLBACK_ADVANCE`.
 */

export type MeasuredFace = 'BarlowMedium' | 'BarlowBold' | 'BarlowCondensedSemiBold' | 'BarlowCondensedBold' | 'BarlowCondensedLight';

/** Widest advance of the measured set, used for a character the table does not carry. */
export const FALLBACK_ADVANCE = 0.75;

const BarlowMedium: Readonly<Record<string, number>> = {
  ' ': 0.2, '!': 0.328, '"': 0.297, '#': 0.669, '$': 0.563, '%': 0.839, '&': 0.675, '\'': 0.145, '(': 0.291,
  ')': 0.291, '*': 0.398, '+': 0.495, ',': 0.264, '-': 0.4, '.': 0.275, '/': 0.426, '0': 0.566, '1': 0.352,
  '2': 0.544, '3': 0.524, '4': 0.57, '5': 0.523, '6': 0.523, '7': 0.481, '8': 0.527, '9': 0.525, ':': 0.348,
  ';': 0.312, '<': 0.495, '=': 0.495, '>': 0.495, '?': 0.482, '@': 0.823, 'A': 0.63, 'B': 0.624, 'C': 0.607,
  'D': 0.618, 'E': 0.598, 'F': 0.565, 'G': 0.613, 'H': 0.645, 'I': 0.26, 'J': 0.579, 'K': 0.616, 'L': 0.57,
  'M': 0.715, 'N': 0.673, 'O': 0.622, 'P': 0.593, 'Q': 0.582, 'R': 0.613, 'S': 0.588, 'T': 0.574, 'U': 0.641,
  'V': 0.601, 'W': 0.878, 'X': 0.604, 'Y': 0.585, 'Z': 0.551, '[': 0.393, '\\': 0.426, ']': 0.393, '^': 0.458,
  '_': 0.498, '`': 0.209, 'a': 0.517, 'b': 0.553, 'c': 0.526, 'd': 0.553, 'e': 0.536, 'f': 0.367, 'g': 0.543,
  'h': 0.542, 'i': 0.255, 'j': 0.248, 'k': 0.512, 'l': 0.235, 'm': 0.826, 'n': 0.542, 'o': 0.555, 'p': 0.558,
  'q': 0.558, 'r': 0.371, 's': 0.487, 't': 0.369, 'u': 0.538, 'v': 0.491, 'w': 0.743, 'x': 0.499, 'y': 0.476,
  'z': 0.451, '{': 0.329, '|': 0.179, '}': 0.329, '~': 0.513, '°': 0.379, '·': 0.219, '−': 0.495, '–': 0.46,
  'Δ': 0.646, '…': 0.815,
};

/** The flag band's name, which the artboards set in 700 where every other label is 500. */
const BarlowBold: Readonly<Record<string, number>> = {
  ' ': 0.2, '!': 0.344, '"': 0.359, '#': 0.687, '$': 0.572, '%': 0.825, '&': 0.705, '\'': 0.168, '(': 0.347,
  ')': 0.347, '*': 0.399, '+': 0.486, ',': 0.265, '-': 0.409, '.': 0.27, '/': 0.473, '0': 0.57, '1': 0.354, '2': 0.56,
  '3': 0.54, '4': 0.605, '5': 0.539, '6': 0.537, '7': 0.496, '8': 0.541, '9': 0.531, ':': 0.364, ';': 0.316,
  '<': 0.486, '=': 0.486, '>': 0.486, '?': 0.521, '@': 0.819, 'A': 0.671, 'B': 0.62, 'C': 0.608, 'D': 0.618,
  'E': 0.584, 'F': 0.56, 'G': 0.612, 'H': 0.628, 'I': 0.263, 'J': 0.583, 'K': 0.626, 'L': 0.572, 'M': 0.715,
  'N': 0.665, 'O': 0.621, 'P': 0.597, 'Q': 0.593, 'R': 0.614, 'S': 0.595, 'T': 0.585, 'U': 0.629, 'V': 0.622,
  'W': 0.877, 'X': 0.631, 'Y': 0.617, 'Z': 0.557, '[': 0.416, '\\': 0.473, ']': 0.416, '^': 0.473, '_': 0.523,
  '`': 0.233, 'a': 0.528, 'b': 0.566, 'c': 0.537, 'd': 0.566, 'e': 0.546, 'f': 0.387, 'g': 0.556, 'h': 0.547,
  'i': 0.255, 'j': 0.254, 'k': 0.533, 'l': 0.244, 'm': 0.835, 'n': 0.547, 'o': 0.557, 'p': 0.568, 'q': 0.568,
  'r': 0.383, 's': 0.502, 't': 0.38, 'u': 0.545, 'v': 0.52, 'w': 0.783, 'x': 0.538, 'y': 0.504, 'z': 0.46, '{': 0.376,
  '|': 0.203, '}': 0.376, '~': 0.534, '°': 0.376, '·': 0.235, '−': 0.486, '–': 0.476, 'Δ': 0.661, '…': 0.851,
};

const BarlowCondensedSemiBold: Readonly<Record<string, number>> = {
  ' ': 0.2, '!': 0.269, '"': 0.306, '#': 0.606, '$': 0.434, '%': 0.772, '&': 0.581, '\'': 0.152, '(': 0.279,
  ')': 0.279, '*': 0.361, '+': 0.439, ',': 0.199, '-': 0.325, '.': 0.211, '/': 0.386, '0': 0.45, '1': 0.274,
  '2': 0.425, '3': 0.426, '4': 0.459, '5': 0.428, '6': 0.429, '7': 0.392, '8': 0.434, '9': 0.423, ':': 0.252,
  ';': 0.218, '<': 0.439, '=': 0.439, '>': 0.439, '?': 0.413, '@': 0.759, 'A': 0.456, 'B': 0.462, 'C': 0.457,
  'D': 0.472, 'E': 0.435, 'F': 0.415, 'G': 0.462, 'H': 0.477, 'I': 0.224, 'J': 0.443, 'K': 0.477, 'L': 0.415,
  'M': 0.538, 'N': 0.507, 'O': 0.467, 'P': 0.457, 'Q': 0.455, 'R': 0.461, 'S': 0.434, 'T': 0.452, 'U': 0.478,
  'V': 0.471, 'W': 0.665, 'X': 0.46, 'Y': 0.456, 'Z': 0.406, '[': 0.33, '\\': 0.386, ']': 0.33, '^': 0.413,
  '_': 0.405, '`': 0.206, 'a': 0.434, 'b': 0.437, 'c': 0.422, 'd': 0.437, 'e': 0.426, 'f': 0.291, 'g': 0.431,
  'h': 0.439, 'i': 0.215, 'j': 0.211, 'k': 0.433, 'l': 0.199, 'm': 0.665, 'n': 0.439, 'o': 0.432, 'p': 0.441,
  'q': 0.441, 'r': 0.313, 's': 0.398, 't': 0.282, 'u': 0.438, 'v': 0.415, 'w': 0.592, 'x': 0.417, 'y': 0.403,
  'z': 0.368, '{': 0.319, '|': 0.171, '}': 0.319, '~': 0.48, '°': 0.359, '·': 0.215, '−': 0.439, '–': 0.377,
  'Δ': 0.525, '…': 0.679,
};

const BarlowCondensedBold: Readonly<Record<string, number>> = {
  ' ': 0.2, '!': 0.279, '"': 0.346, '#': 0.616, '$': 0.444, '%': 0.766, '&': 0.603, '\'': 0.17, '(': 0.311, ')': 0.311,
  '*': 0.361, '+': 0.438, ',': 0.211, '-': 0.331, '.': 0.223, '/': 0.415, '0': 0.453, '1': 0.284, '2': 0.438,
  '3': 0.436, '4': 0.484, '5': 0.439, '6': 0.44, '7': 0.407, '8': 0.44, '9': 0.435, ':': 0.275, ';': 0.232, '<': 0.438,
  '=': 0.438, '>': 0.438, '?': 0.437, '@': 0.759, 'A': 0.482, 'B': 0.47, 'C': 0.464, 'D': 0.476, 'E': 0.438,
  'F': 0.421, 'G': 0.467, 'H': 0.48, 'I': 0.23, 'J': 0.452, 'K': 0.491, 'L': 0.426, 'M': 0.548, 'N': 0.514, 'O': 0.473,
  'P': 0.465, 'Q': 0.461, 'R': 0.471, 'S': 0.444, 'T': 0.468, 'U': 0.479, 'V': 0.488, 'W': 0.689, 'X': 0.475,
  'Y': 0.474, 'Z': 0.412, '[': 0.345, '\\': 0.415, ']': 0.345, '^': 0.422, '_': 0.421, '`': 0.22, 'a': 0.446,
  'b': 0.445, 'c': 0.434, 'd': 0.445, 'e': 0.436, 'f': 0.298, 'g': 0.441, 'h': 0.447, 'i': 0.219, 'j': 0.217,
  'k': 0.449, 'l': 0.209, 'm': 0.675, 'n': 0.447, 'o': 0.442, 'p': 0.448, 'q': 0.448, 'r': 0.324, 's': 0.412,
  't': 0.291, 'u': 0.447, 'v': 0.436, 'w': 0.613, 'x': 0.439, 'y': 0.423, 'z': 0.373, '{': 0.344, '|': 0.186,
  '}': 0.344, '~': 0.49, '°': 0.359, '·': 0.225, '−': 0.438, '–': 0.38, 'Δ': 0.54, '…': 0.709,
};

const BarlowCondensedLight: Readonly<Record<string, number>> = {
  ' ': 0.2, '!': 0.244, '"': 0.206, '#': 0.582, '$': 0.409, '%': 0.785, '&': 0.527, '\'': 0.106, '(': 0.199,
  ')': 0.199, '*': 0.36, '+': 0.443, ',': 0.169, '-': 0.309, '.': 0.181, '/': 0.313, '0': 0.442, '1': 0.249,
  '2': 0.392, '3': 0.401, '4': 0.397, '5': 0.401, '6': 0.402, '7': 0.355, '8': 0.418, '9': 0.395, ':': 0.194,
  ';': 0.183, '<': 0.443, '=': 0.443, '>': 0.443, '?': 0.352, '@': 0.763, 'A': 0.391, 'B': 0.443, 'C': 0.441,
  'D': 0.463, 'E': 0.427, 'F': 0.399, 'G': 0.447, 'H': 0.47, 'I': 0.211, 'J': 0.419, 'K': 0.442, 'L': 0.388,
  'M': 0.514, 'N': 0.488, 'O': 0.451, 'P': 0.436, 'Q': 0.439, 'R': 0.436, 'S': 0.409, 'T': 0.411, 'U': 0.475,
  'V': 0.427, 'W': 0.606, 'X': 0.423, 'Y': 0.411, 'Z': 0.391, '[': 0.292, '\\': 0.313, ']': 0.292, '^': 0.39,
  '_': 0.365, '`': 0.17, 'a': 0.404, 'b': 0.417, 'c': 0.394, 'd': 0.417, 'e': 0.4, 'f': 0.272, 'g': 0.406, 'h': 0.418,
  'i': 0.205, 'j': 0.195, 'k': 0.394, 'l': 0.174, 'm': 0.641, 'n': 0.418, 'o': 0.407, 'p': 0.423, 'q': 0.423,
  'r': 0.284, 's': 0.364, 't': 0.261, 'u': 0.418, 'v': 0.364, 'w': 0.539, 'x': 0.359, 'y': 0.353, 'z': 0.357,
  '{': 0.255, '|': 0.133, '}': 0.255, '~': 0.454, '°': 0.358, '·': 0.189, '−': 0.443, '–': 0.37, 'Δ': 0.488,
  '…': 0.603,
};

const FACES: Record<MeasuredFace, Readonly<Record<string, number>>> = { BarlowMedium, BarlowBold, BarlowCondensedSemiBold, BarlowCondensedBold, BarlowCondensedLight };

/** Width in pixels of `text` set in `face` at `fs`, from the measured advances. */
export function measureText(face: MeasuredFace, text: string, fs: number): number {
  const table = FACES[face];
  let em = 0;
  for (const ch of text) em += table[ch] ?? FALLBACK_ADVANCE;
  return em * fs;
}

/** The character a cut-short text ends in, which is one glyph and not three full stops. */
export const ELLIPSIS = '…';

/**
 * The widest glyph a face draws, and its advance in em.
 *
 * Derived rather than written down, and it is what a **character** budget has to assume: a budget is
 * per character where an advance is per glyph, and an expression can count characters but cannot
 * measure them, so the only count that cannot clip is the one taken from the widest glyph in the
 * face. It cuts an ordinary name a little early, which is the trade #385 records.
 *
 * `FALLBACK_ADVANCE` is narrower than this in every measured face — `widestGlyph` covers the whole
 * of Latin-1's letters and the fallback is for the accented characters outside the table, every one
 * of which is a narrower letter than W — and `metrics.test.ts` holds that, so a character the table
 * does not carry is inside the budget too.
 */
export function widestGlyph(face: MeasuredFace): { glyph: string; advance: number } {
  let glyph = '';
  let advance = 0;
  for (const [ch, em] of Object.entries(FACES[face])) if (em > advance) [glyph, advance] = [ch, em];
  return { glyph, advance };
}

/**
 * How many characters of any text are certain to fit `width` in `face` at `fs`.
 *
 * Strictly inside the box, because WPF clips at the edge and `textFit.test.ts` asks for `width <
 * box`: a count whose widest rendering exactly met the box would lose the last glyph's final column.
 */
export function charsThatFit(face: MeasuredFace, fs: number, width: number): number {
  const advance = widestGlyph(face).advance * fs;
  if (advance <= 0) return 0;
  const n = Math.floor(width / advance);
  return Math.max(0, n * advance < width ? n : n - 1);
}

/**
 * The widest string `charsThatFit` promises to hold: that many of the face's widest glyph.
 *
 * What a bound text declares as its `widest`, so that the fit tests measure the budget itself rather
 * than whatever sample the item happens to draw at design time. A text cut to `chars - 1` characters
 * and closed with {@link ELLIPSIS} is narrower than this, the ellipsis being narrower than the
 * widest glyph in every face measured here.
 */
export const widestOf = (face: MeasuredFace, chars: number): string => widestGlyph(face).glyph.repeat(Math.max(0, chars));

/**
 * The gap between an `i`'s tittle and its stem, in em, per face, measured from the same TTFs.
 *
 * The one thing an advance table cannot say: where a glyph's ink **stops and starts again**. Barlow's
 * `i` and `j` are the only *unaccented* letters in any bundled face drawn in two pieces —
 * `advances.test.ts` holds that, by reading every character of this table back out of the outlines —
 * and the piece that can be lost is the dot. Lose it and the letter is an `l`, since the two are the
 * same height to within 0.017 em: on the 850 x 480 face's relative, `Liam Byrne` came back from the VM
 * as `Llam B…` and `Nina Hartmann` as `NIna H…`.
 *
 * The accented letters are built the same way and are not in this number, an entry per face being the
 * `i`'s: in the name face `É`, `Å`, `Í` and `Ö` break at 0.064 to 0.076 em, tighter than the `i`'s
 * 0.080, and `advances.test.ts` measures them too. They are not what the case rule in `second/table.ts`
 * is for — a welded acute leaves `É` an `É`, where a welded tittle leaves `Liam` a legal `Llam` — so
 * this entry stays the tittle's and the rule stays about the letter that can become another letter.
 *
 * The number falls as the weight rises, which is the opposite of the instinct: a heavier face draws a
 * fatter stem and a fatter dot into the same vertical, so Bold's break is 0.057 em where Light's is
 * 0.101. Reaching for a heavier weight to separate the two closes the gap.
 */
export const TITTLE_BREAK: Readonly<Record<MeasuredFace, number>> = {
  BarlowMedium: 0.08,
  BarlowBold: 0.057,
  BarlowCondensedSemiBold: 0.07,
  BarlowCondensedBold: 0.058,
  BarlowCondensedLight: 0.101,
};

/**
 * The device pixels of break a dot needs to be certain of surviving the raster, which is two.
 *
 * A break of `g` px lands at whatever sub-pixel phase the glyph's baseline puts it at. Under two
 * pixels there is no pixel row the break is guaranteed to fall wholly inside, so the renderer may
 * shade the row above and the row below at partial coverage and draw a grey bridge where the design
 * has background. At two the break covers one whole row at any phase and the dot is separate.
 *
 * It is a bound rather than a threshold measured on a rim, because WPF's raster is not something this
 * build can run: what it can do is say at which size the question stops being one.
 */
const SAFE_BREAK_PX = 2;

/**
 * The smallest size at which this face's `i` is certain to keep its dot.
 *
 * 25 px in Barlow Medium, which is the name face: every size any list draws a name at — 15 px from
 * the 34 px row up, 13 in the narrow zone's 28 px row, 12 on a companion — is under it, and the
 * relative's 13 is where the VM saw it fail first. That is what makes the name column's case rule a
 * rule and not a special case for one face.
 */
export const dottedLetterSize = (face: MeasuredFace): number => Math.ceil(SAFE_BREAK_PX / TITTLE_BREAK[face]);
