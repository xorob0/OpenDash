/**
 * The pictures a package ships, and the box each one is cut from.
 *
 * A few things on a face are pictures rather than text: a nationality flag, a licence badge, a
 * telltale pictogram, the top-down car. SimHub draws them with an `ImageItem`, which references a
 * file by name out of the dashboard's own `Images` list, and the file travels in the
 * `<dashboard>.djson.ressources` sidecar beside the `_SHFonts/` folder. Nothing in this package
 * drew one until now, which is why every drawn object was absent at once; this is the registry the
 * drawings reference, and `build.ts` is what packs the files it names.
 *
 * Three facts of the format decide how a drawing is built, all of them verified against
 * SimHub 9.12.6 and recorded in docs/research/simhub-dash-format.md:
 *
 *   - **An image carries no colour.** `ImageItem` declares `Image`, `AutoSize`, `AutoSizeScale`
 *     and nothing that tints what is drawn, so a lamp that is amber when it lights and grey when
 *     it does not is two files and not one file recoloured. The colour is in the pixels, which is
 *     the one place on the face where a colour is not read from `design/tokens.json`; an entry
 *     below therefore records the token its file was rendered from, and a token that moves means
 *     the file is rendered again.
 *   - **`Visible` binds as it does anywhere else**, so the two files of a two-state lamp are two
 *     items at the same rect, each hidden by its own expression.
 *   - **The picture is stretched to the rect**, never letterboxed inside it. A drawing is
 *     therefore only as faithful as the box it is given, which is what {@link assetBox} is for.
 *
 * What ships is what is drawn: `build.ts` packs the assets a dashboard's items reference and no
 * others, and an asset is rendered at the smallest size that stays crisp in the largest box it is
 * drawn in, because a package is a zip SimHub extracts and reads at startup.
 *
 * Every asset names its source, and `notices.ts` records what each source owes; an image no entry
 * here claims fails the build rather than shipping unlicensed.
 */
import path from 'node:path';
import { describeImage, type ImageAsset, type Rect } from '../generator.ts';
import { rect, type Size } from './geometry.ts';

/** Where the artwork lives, which is the one place an asset is read from. */
export const VENDORED_IMAGES_DIR = path.resolve(import.meta.dir, '..', '..', 'images');

/** Where a picture came from, and what it may be redistributed under. */
export interface AssetSource {
  /** Who made it, as a notice has to name them. */
  who: string;
  /** SPDX identifier of the licence the artwork is under. */
  licence: string;
}

/**
 * The sources artwork is taken from. A source is added here and its notice in `notices.ts`, which
 * maps this set exhaustively, so a source cannot arrive without an answer to what it owes.
 *
 * Material Design Icons by Pictogrammers are the twelve telltale pictograms the band D car page
 * waits on. They are Apache 2.0, which asks for the licence text and the NOTICE to travel with
 * them, so the source arrives with its files and its licence in one commit and not before.
 */
export const ASSET_SOURCES = {
  openDash: { who: 'the openDash authors', licence: 'MIT' },
} as const satisfies Record<string, AssetSource>;

export type AssetSourceId = keyof typeof ASSET_SOURCES;

/** One file a package may carry. */
export interface DashAsset {
  /** What an image item references, and the base name of the `.ressources` entry. */
  name: string;
  /** The file under {@link VENDORED_IMAGES_DIR}. */
  file: string;
  source: AssetSourceId;
}

/**
 * The tick that marks a wheel down for a change at the next stop, drawn on the badge at the top
 * outer corner of a tyre.
 *
 * The badge itself is a rect in `text.secondary` with a 1.5 radius, so that colour stays a token;
 * the tick is the part no rect can draw and no text can measure, `advances.ts` having no glyph for
 * one. It is the canvas's own geometry rather than anybody's icon set: within the badge square,
 * (0.24, 0.52) to (0.44, 0.74) to (0.78, 0.32), stroked at 0.15 of the side with round caps and
 * joins, which is `M59.06 18.08 l5.71 6.28 l9.71 -12.00` at the 28.56 badge of CompanionModules
 * and the same shape at the 12.58 badge of PitWall1920x1080. The ink is `color.surface.base`, baked
 * in because an image item cannot be tinted, and the file covers the whole badge square so that
 * the item takes the badge's rect exactly.
 */
export const WHEEL_CHANGE_TICK: DashAsset = { name: 'wheel-change-tick', file: 'wheel-change-tick.png', source: 'openDash' };

/**
 * The two marks of a table's rank column: a car that has gained places, and one that has lost them.
 *
 * The canvas draws a 10 by 10 triangle, `M1 8h8L5 2z` pointing up and `M1 2h8L5 8z` pointing down,
 * in a box of the same ten pixels. It is two files rather than one and a binding because an image
 * carries no colour and no rotation: the direction and the ink are both in the pixels. They are
 * rendered from `purpose.delta.faster` and `purpose.delta.slower`, so a move of either token means
 * these files are rendered again; the count beside the mark reads its colour from the token as
 * every other value does.
 *
 * Rendered at 40 rather than at 10. The mark is drawn at 10 px on all four artboards that head a
 * rank column, and an image is stretched to its rect, so four times the drawn size is what keeps
 * the diagonals clean on a panel a pit wall scales.
 */
export const RANK_UP: DashAsset = { name: 'rank-up', file: 'rank-up.png', source: 'openDash' };
export const RANK_DOWN: DashAsset = { name: 'rank-down', file: 'rank-down.png', source: 'openDash' };

/** Every asset a package may carry. */
export const ASSETS: readonly DashAsset[] = [WHEEL_CHANGE_TICK, RANK_UP, RANK_DOWN];

/** The asset an image item's `image` names, or undefined when nothing here claims it. */
export const assetNamed = (name: string): DashAsset | undefined => ASSETS.find((asset) => asset.name === name);

/** Where an asset's file is read from. */
export const assetPath = (asset: DashAsset): string => path.join(VENDORED_IMAGES_DIR, asset.file);

/** What has already been measured in this process, so that the second package to draw it does not reread the file. */
const described = new Map<string, ImageAsset>();

/**
 * The asset as a dashboard declares it: its own pixels, its byte count and the MD5 of those bytes,
 * all read off the file, because a descriptor that disagrees with what is packed beside it draws
 * nothing at all.
 */
export function imageOf(asset: DashAsset): ImageAsset {
  const file = assetPath(asset);
  const done = described.get(file);
  if (done !== undefined) return done;
  const image = describeImage(file, asset.name);
  described.set(file, image);
  return image;
}

/**
 * The box a picture is drawn in: the largest rect of the source's own proportions that fits
 * `frame`, centred in it, after whatever cap the caller puts on it.
 *
 * A drawing is cut from the box it is given rather than placed at a size, which is rule 18, and
 * the caps are how the rule's exceptions are stated: a car is at most a third of the zone's width
 * however tall its box is, `assetBox(frame, car, { maxWidth: frame.width / 3 })`. The proportions
 * have to be kept here because SimHub keeps none of its own: it stretches the picture to the rect,
 * so a square glyph in a wide box is a wide glyph unless the box is made square first.
 */
export function assetBox(frame: Rect, source: Size, limits: { maxWidth?: number; maxHeight?: number } = {}): Rect {
  const maxWidth = Math.max(0, Math.min(frame.width, limits.maxWidth ?? frame.width));
  const maxHeight = Math.max(0, Math.min(frame.height, limits.maxHeight ?? frame.height));
  const scale = Math.min(maxWidth / source.width, maxHeight / source.height);
  const width = source.width * scale;
  const height = source.height * scale;
  return rect(frame.left + (frame.width - width) / 2, frame.top + (frame.height - height) / 2, width, height);
}
