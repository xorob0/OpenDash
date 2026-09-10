/**
 * Colour normalisation. The model accepts `#RRGGBB` or `#AARRGGBB`; SimHub always reads and
 * writes `#AARRGGBB` (alpha first, upper case). `#00FFFFFF` is SimHub's transparent.
 */

import type { Hex } from './model.ts';

const HEX6 = /^#[0-9a-fA-F]{6}$/;
const HEX8 = /^#[0-9a-fA-F]{8}$/;

/** SimHub's transparent colour, the default background of text items, widgets and layers. */
export const TRANSPARENT: Hex = '#00FFFFFF';

/** True for `#RRGGBB` and `#AARRGGBB`, any case. */
export const isHex = (value: unknown): value is Hex => typeof value === 'string' && (HEX6.test(value) || HEX8.test(value));

/** True only for the normalised `#AARRGGBB` upper-case form the serialiser writes. */
export const isNormalisedHex = (value: unknown): value is Hex => typeof value === 'string' && /^#[0-9A-F]{8}$/.test(value);

/**
 * `#RRGGBB` becomes `#FFRRGGBB`; `#AARRGGBB` is kept; hex digits are upper-cased.
 * Throws a TypeError for anything else, so a bad colour fails the build rather than the dash.
 */
export const normaliseHex = (value: string): Hex => {
  if (!isHex(value)) {
    throw new TypeError(`Invalid colour ${JSON.stringify(value)}: expected #RRGGBB or #AARRGGBB`);
  }
  const body = value.slice(1).toUpperCase();
  return `#${body.length === 6 ? `FF${body}` : body}`;
};

/** American spelling alias of {@link normaliseHex}. */
export const normalizeHex = normaliseHex;

/** Replaces the alpha channel. `alpha` is 0..1 (0 transparent, 1 opaque). */
export const withAlpha = (value: string, alpha: number): Hex => {
  if (!Number.isFinite(alpha) || alpha < 0 || alpha > 1) {
    throw new RangeError(`Alpha ${alpha} is outside 0..1`);
  }
  const rgb = normaliseHex(value).slice(3);
  const aa = Math.round(alpha * 255).toString(16).toUpperCase().padStart(2, '0');
  return `#${aa}${rgb}`;
};

/** Channels of a colour as integers 0..255. */
export const hexToArgb = (value: string): { a: number; r: number; g: number; b: number } => {
  const n = normaliseHex(value);
  const at = (i: number): number => parseInt(n.slice(i, i + 2), 16);
  return { a: at(1), r: at(3), g: at(5), b: at(7) };
};
