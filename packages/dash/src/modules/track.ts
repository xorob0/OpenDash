/**
 * Module 13, Track: the whole circuit with every car on it, under a header naming the track with
 * its length on the left and its surface state on the right.
 *
 * The map comes from SimHub's recorded outline, so it appears once a lap has been recorded for the
 * track. Class colours are off: identity on these screens is a labelled chip, and a map full of
 * iRacing's class colours would fight the state colours this design reserves.
 *
 * The cars are round because SimHub's map item carries one player style and one opponent style,
 * each a dot with a radius: the design's square markers, and the two coloured cars it draws beside
 * the grey ones, are not expressible and are left out rather than approximated.
 */
import { ncalc } from '../generator.ts';
import { measureText } from '../design/advances.ts';
import { rect, type Rect } from '../design/geometry.ts';
import { densityOf } from '../second/density.ts';
import { label } from '../elements/label.ts';
import type { StaticMapItem } from '../generator.ts';
import { ds } from '../tokens.ts';
import { GRIP_WIDEST, trackGrip, trackLengthKm, trackName } from '../second/values.ts';
import { defineModule } from './module.ts';

const { concat, fmt, str, ucase } = ncalc;

/**
 * The line the circuit is drawn with, cut from the map the way the gear's cell is cut from its box.
 *
 * The catalogue's own drawing strokes 2.5 into a map 220 px down its shorter side, which is a
 * pixel of line for every 88 of map. A literal drew that same weight into a 135 px zone map, where
 * readability-pass.md §17 calls it a fat worm, and into a 516 px one, where it is a hairline.
 */
const trackWidthFor = (map: Rect): number => Math.round(10 * Math.max(1.5, Math.min(6, Math.min(map.width, map.height) / 88))) / 10;

export const track = defineModule('track', (ctx) => {
  const d = densityOf(ctx.density);
  const titled = ctx.title !== false;
  const mapTop = titled ? ctx.frame.top + d.labelSm + d.fieldGap * 2 : ctx.frame.top;
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
  if (!titled) return [map];
  // The state is the right half of the canvas's header row. Its box is measured for the longest
  // word the binding can produce rather than for "DRY", and the name gives up exactly that much
  // plus the canvas's 12 px: a name and a state competing for one 245 px line is where WPF clips
  // whichever it draws second.
  const stateWidth = Math.ceil(measureText('BarlowMedium', GRIP_WIDEST, d.labelSm));
  return [
    label(`${ctx.prefix}title`, 'VALMONT PARK · 4.1 KM', ctx.frame.left, ctx.frame.top, Math.max(0, ctx.frame.width - stateWidth - ds.space[3]), {
      size: d.labelSm,
      bind: concat(ucase(trackName()), str(' · '), fmt(trackLengthKm(), '0.0'), str(' KM')),
    }),
    label(`${ctx.prefix}state`, 'DRY', ctx.frame.left + ctx.frame.width - stateWidth, ctx.frame.top, stateWidth, {
      size: d.labelSm,
      hAlign: 'right',
      bind: trackGrip(),
      widest: GRIP_WIDEST,
    }),
    map,
  ];
});
