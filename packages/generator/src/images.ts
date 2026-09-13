/**
 * Reading what a `.djson` has to say about an image.
 *
 * SimHub's `Images` descriptor carries the image's own pixel dimensions, its uncompressed byte
 * count and an MD5 of those bytes, none of which can be known without opening the file. The
 * serialiser is pure and stays that way, so the measuring happens here and the result travels in
 * the model as an {@link ImageAsset}, exactly as a font travels as a path.
 *
 * PNG only. Material Design Icons and the nationality flags are both rendered to PNG, SimHub's
 * own dashboards use PNG, and a second decoder is a second way to be wrong about a dimension; a
 * file of any other kind is refused loudly rather than described incorrectly.
 */

import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { basename, extname } from 'node:path';
import type { ImageAsset } from './model.ts';

/** The eight bytes every PNG begins with. */
const PNG_SIGNATURE = Uint8Array.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

/** IHDR is required to be the first chunk, so width and height sit at fixed offsets. */
const IHDR_WIDTH_OFFSET = 16;
const IHDR_HEIGHT_OFFSET = 20;
const IHDR_MINIMUM_LENGTH = 24;

const startsWithSignature = (bytes: Uint8Array): boolean =>
  bytes.length >= PNG_SIGNATURE.length && PNG_SIGNATURE.every((byte, i) => bytes[i] === byte);

const readUint32BE = (bytes: Uint8Array, offset: number): number =>
  ((bytes[offset]! << 24) | (bytes[offset + 1]! << 16) | (bytes[offset + 2]! << 8) | bytes[offset + 3]!) >>> 0;

/** The pixel dimensions in a PNG's IHDR chunk. Throws when the bytes are not a PNG. */
export const pngSize = (bytes: Uint8Array, label = 'the image'): { width: number; height: number } => {
  if (!startsWithSignature(bytes)) throw new Error(`${label} is not a PNG; only PNG images are packed`);
  if (bytes.length < IHDR_MINIMUM_LENGTH) throw new Error(`${label} is too short to hold a PNG header`);
  const width = readUint32BE(bytes, IHDR_WIDTH_OFFSET);
  const height = readUint32BE(bytes, IHDR_HEIGHT_OFFSET);
  if (width === 0 || height === 0) throw new Error(`${label} declares a zero dimension (${width} by ${height})`);
  return { width, height };
};

/** Lowercase hex MD5, which is the form SimHub writes into a descriptor. */
export const md5Hex = (bytes: Uint8Array): string => createHash('md5').update(bytes).digest('hex');

/**
 * Describes an image on disk so that a dashboard can reference it.
 *
 * The name defaults to the file's base name, which is what an item then refers to and what the
 * `.ressources` entry is called. It has to be unique within a dashboard, which
 * {@link validatePackage} checks rather than this.
 */
export const describeImage = (path: string, name = basename(path, extname(path))): ImageAsset => {
  const bytes = new Uint8Array(readFileSync(path));
  const extension = extname(path).toLowerCase();
  if (extension !== '.png') throw new Error(`${basename(path)} is a ${extension || 'nameless'} file; only PNG images are packed`);
  const { width, height } = pngSize(bytes, basename(path));
  return { name, extension, path, width, height, length: bytes.length, md5: md5Hex(bytes) };
};
