/**
 * Advance widths in em, measured from the bundled TTFs, for the characters openDash draws. They
 * are how a proportional string's width is known before SimHub renders it: SimHub hands the box
 * to WPF as `MaxTextWidth` and clips whatever does not fit, so a label that does not fit must be
 * shortened by the card rather than cut by the renderer.
 *
 * Generated from packages/dash/fonts by tools/measure-advances (see the design notes); regenerate
 * when a font file changes. Characters absent from the table fall back to `FALLBACK_ADVANCE`.
 */

export type MeasuredFace = 'BarlowMedium' | 'BarlowCondensedSemiBold';

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
  'Δ': 0.646,
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
  'Δ': 0.525,
};

const FACES: Record<MeasuredFace, Readonly<Record<string, number>>> = { BarlowMedium, BarlowCondensedSemiBold };

/** Width in pixels of `text` set in `face` at `fs`, from the measured advances. */
export function measureText(face: MeasuredFace, text: string, fs: number): number {
  const table = FACES[face];
  let em = 0;
  for (const ch of text) em += table[ch] ?? FALLBACK_ADVANCE;
  return em * fs;
}
