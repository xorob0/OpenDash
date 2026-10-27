/**
 * Module 13, Track: the whole circuit with every car on it, under a header naming the track with
 * its length on the left and its surface state on the right.
 *
 * The map comes from SimHub's recorded outline, so it appears once a lap has been recorded for the
 * track and its layout: one lap driven from the line to the line outside the pit lane, which SimHub
 * keeps per track and layout and never ships. Until then SimHub's map item draws an empty box and
 * says nothing, which on a sim where nobody has driven the track reads as a broken page, so the
 * module says what the box is waiting for in its place (#555). The plugin is what knows: SimHub keeps
 * no property for it, and `trackMapRecorded` falls back on the map as it always was drawn.
 * `docs/research/simhub-dash-format.md` records what SimHub needs before it draws a track.
 *
 * Class colours are off: identity on these screens is a labelled chip, and a map full of
 * iRacing's class colours would fight the state colours this design reserves.
 *
 * The cars are round because SimHub's map item carries one player style and one opponent style,
 * each a dot with a radius: the design's square markers, and the two coloured cars it draws beside
 * the grey ones, are not expressible and are left out rather than approximated.
 */
import { ncalc } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { charsThatFit, measureText, widestFigure, widestOf } from '../design/advances.ts';
import { boxSlack } from '../design/metrics.ts';
import { rect, type Rect } from '../design/geometry.ts';
import { densityOf, type Density } from '../second/density.ts';
import { label } from '../elements/label.ts';
import type { StaticMapItem } from '../generator.ts';
import { ds } from '../tokens.ts';
import { placeholder } from '../second/placeholder.ts';
import { ellipsised, GRIP_WIDEST, hasTrackState, TRACK_MAP_NOTICE, trackGrip, trackLengthKm, trackMapRecorded, trackName } from '../second/values.ts';
import { defineModule } from './module.ts';

const { concat, eq, fmt, iff, left, min, not, num, str } = ncalc;

/** What the map's box says until SimHub has an outline to draw in it, which the radar says too (#1176). */
export { TRACK_MAP_NOTICE };

/**
 * The line the circuit is drawn with, cut from the map the way the gear's cell is cut from its box.
 *
 * The catalogue's own drawing strokes 2.5 into a map 220 px down its shorter side, which is a
 * pixel of line for every 88 of map. A literal drew that same weight into a 135 px zone map, where
 * readability-pass.md §17 calls it a fat worm, and into a 516 px one, where it is a hairline.
 */
const trackWidthFor = (map: Rect): number => Math.round(10 * Math.max(1.5, Math.min(6, Math.min(map.width, map.height) / 88))) / 10;

/** The catalogue's own viewBox: every map on every artboard is this box scaled into its slot. */
const VIEW = { width: 231, height: 150 } as const;

/** The drawing's ratio, which the box cut for it keeps. */
export const TRACK_ASPECT = VIEW.width / VIEW.height;

/** What a titled frame spends above its map: the header row and the gap under it. */
export const trackHeaderHeight = (density: Density): number => {
  const d = densityOf(density);
  return d.labelSm + d.fieldGap * 2;
};

/**
 * The width a titled track frame takes at `height` so that the map under its header is the
 * catalogue's own ratio, capped by the room `within` leaves. **Rule 18**: the map is cut from its
 * box rather than placed in it at a size.
 *
 * The circuit is drawn into its rectangle at its recorded proportions, so a box wider than the
 * ratio asks for buys no ink: it either letterboxes the drawing or stretches it, and neither is
 * what a panel meant to spend the width on. Cutting the box to the ratio is therefore the widest
 * the drawing can be read at that height, and the field column beside it keeps the rest.
 */
export const trackFrameWidth = (height: number, density: Density, within: number): number =>
  Math.max(0, Math.min(Math.round(TRACK_ASPECT * Math.max(0, height - trackHeaderHeight(density))), within));

/** The face the header is drawn in, which is `label`'s. */
const HEADER_FACE = 'BarlowMedium';

/**
 * The longest track a length is drawn for, in km. The Nordschleife with the GP circuit, the longest
 * lap anyone races, is 25.4, so a reading past this is not a circuit; it is drawn as this rather than
 * let a fourth figure push the line past its box.
 */
const LENGTH_CAP = 99.9;

/** The widest the length after the name draws: the separator, the widest figures of the cap, and the unit. */
export const TRACK_LENGTH_WIDEST = ` · ${widestFigure(HEADER_FACE, LENGTH_CAP.toFixed(1))} km`;

/**
 * The header's line: the track's name and its length, as the canvas writes it, where the name leaves
 * room for the length; the name alone where it does not; and the name cut with an ellipsis where it
 * does not fit the line even alone (#1150).
 *
 * The name is an open string, the sim's or a community track's own `ui_track.json`, and nothing an
 * expression can measure: SimHub's NCalc counts characters and has no width. So the room is counted in
 * the face's widest glyph, as `ellipsised` counts a driver's name, and a name with that many characters
 * or fewer is certain to fit whatever they are. It is cautious for an ordinary name, which draws at
 * about half that glyph, and it is the only count that holds for every name.
 *
 * The length gives way before the name does. A driver glancing at the page wants to know which
 * track the map is, and a name cut to make room for its own length answers that worse than the
 * name whole; the length is the secondary reading of the two. The `widest` is the wider of the two
 * forms, so the fit tests measure the line by what either can draw.
 */
export function trackHeader(width: number, fs: number): { bind: Expr; widest: string } {
  // Counted in the box less its slack, so the line the count promises ends a slack short of the edge.
  const room = width - boxSlack(fs);
  const withLength = charsThatFit(HEADER_FACE, fs, room - measureText(HEADER_FACE, TRACK_LENGTH_WIDEST, fs));
  const alone = charsThatFit(HEADER_FACE, fs, room);
  const name = trackName();
  const bind = iff(
    // `left(name, 1, n)` is the character just past `n`, and empty exactly when the name has no more.
    eq(left(name, 1, withLength), str('')),
    concat(name, str(' · '), fmt(min(trackLengthKm(), num(LENGTH_CAP)), '0.0'), str(' km')),
    ellipsised(name, alone),
  );
  const forms = [widestOf(HEADER_FACE, withLength) + TRACK_LENGTH_WIDEST, widestOf(HEADER_FACE, alone)];
  const widest = forms.reduce((a, b) => (measureText(HEADER_FACE, b, fs) > measureText(HEADER_FACE, a, fs) ? b : a));
  return { bind, widest };
}

export const track = defineModule('track', (ctx) => {
  const d = densityOf(ctx.density);
  const titled = ctx.title !== false;
  const mapTop = titled ? ctx.frame.top + trackHeaderHeight(ctx.density) : ctx.frame.top;
  const mapRect = rect(ctx.frame.left, mapTop, ctx.frame.width, Math.max(0, ctx.frame.top + ctx.frame.height - mapTop));
  const map: StaticMapItem = {
    kind: 'staticMap',
    name: `${ctx.prefix}map`,
    rect: mapRect,
    trackColor: ds.color.text.dim,
    trackWidth: trackWidthFor(mapRect),
    trackBorderColor: ds.color.text.dim,
    trackBorderWidth: 0,
    mapShadow: false,
    overrideColorsWithCarClassColors: false,
    displayPerClassPosition: false,
    playerStyle: { dotColor: ds.color.text.primary, dotRadius: 4, dotBorderThickness: 1, dotBorderColor: ds.color.surface.base, labelFontSize: 1, labelColor: ds.color.surface.base },
    opponentStyle: { dotColor: ds.color.text.secondary, dotRadius: 3, dotBorderThickness: 0, dotBorderColor: ds.color.surface.base, labelFontSize: 1, labelColor: ds.color.surface.base },
    startLine: { color: ds.color.text.primary, enabled: true, width: 3, height: 16 },
  };
  // The map and what its box says without one, one of them at a time, the way the session gate puts
  // a module and its notice in the same rectangle. The header stays: the name and the grip are the
  // sim's whether or not the outline is recorded.
  const recorded = trackMapRecorded();
  const drawing = [
    withMoreBindings<'staticMap'>(map, { Visible: recorded }),
    ...placeholder(`${ctx.prefix}map.`, TRACK_MAP_NOTICE, mapRect, ctx.density).map((item) => withMoreBindings(item, { Visible: not(recorded) })),
  ];
  if (!titled) return drawing;
  // The state is the right half of the canvas's header row. Its box is measured for the longest
  // word the binding can produce rather than for "Dry", and the name gives up exactly that much
  // plus the canvas's 12 px: a name and a state competing for one 245 px line is where WPF clips
  // whichever it draws second.
  const stateWidth = Math.ceil(measureText(HEADER_FACE, GRIP_WIDEST, d.labelSm)) + boxSlack(d.labelSm);
  const titleWidth = Math.max(0, ctx.frame.width - stateWidth - ds.space[3]);
  const header = trackHeader(titleWidth, d.labelSm);
  return [
    label(`${ctx.prefix}title`, 'Valmont Park · 4.1 km', ctx.frame.left, ctx.frame.top, titleWidth, {
      size: d.labelSm,
      bind: header.bind,
      widest: header.widest,
    }),
    label(`${ctx.prefix}state`, 'Dry', ctx.frame.left + ctx.frame.width - stateWidth, ctx.frame.top, stateWidth, {
      size: d.labelSm,
      hAlign: 'right',
      bind: trackGrip(),
      widest: GRIP_WIDEST,
      // Hidden where the sim reports no track state, rather than `--` beside the track's name (#1128).
      visibleBind: hasTrackState(),
    }),
    ...drawing,
  ];
});
