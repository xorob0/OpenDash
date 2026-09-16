/**
 * Module 13, Track: the whole circuit with every car on it, and the track's name and length.
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
import { rect } from '../design/geometry.ts';
import { densityOf } from '../second/density.ts';
import { label } from '../elements/label.ts';
import type { StaticMapItem } from '../generator.ts';
import { ds } from '../tokens.ts';
import { trackLengthKm, trackName } from '../second/values.ts';
import { defineModule } from './module.ts';

const { concat, fmt, str, ucase } = ncalc;

export const track = defineModule('track', (ctx) => {
  const d = densityOf(ctx.density);
  const titled = ctx.title !== false;
  const mapTop = titled ? ctx.frame.top + d.labelSm + d.fieldGap * 2 : ctx.frame.top;
  const map: StaticMapItem = {
    kind: 'staticMap',
    name: `${ctx.prefix}map`,
    rect: rect(ctx.frame.left, mapTop, ctx.frame.width, Math.max(0, ctx.frame.top + ctx.frame.height - mapTop)),
    trackColor: ds.color.text.dim,
    trackWidth: 2.5,
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
  return [
    label(`${ctx.prefix}title`, 'VALMONT PARK · 4.1 KM', ctx.frame.left, ctx.frame.top, ctx.frame.width, {
      size: d.labelSm,
      bind: concat(ucase(trackName()), str(' · '), fmt(trackLengthKm(), '0.0'), str(' KM')),
    }),
    map,
  ];
});
