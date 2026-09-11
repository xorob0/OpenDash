/**
 * Module 12, Radar: SimHub's proximity radar with the spotter either side of it.
 *
 * The radar draws the player at the centre and the cars around from the recorded track map, so it
 * needs a lap on the track before it shows anything. The two blocks beside it come from SimHub's
 * own spotter and turn red when a car is alongside.
 */
import { ncalc } from '../generator.ts';
import { withBindings } from '../bind.ts';
import { rect } from '../design/geometry.ts';
import { band } from '../elements/band.ts';
import type { Item, RadarItem } from '../generator.ts';
import { ds } from '../tokens.ts';
import { defineModule } from './module.ts';

const { game, gt, num, isnull, iff, str } = ncalc;

/** Pixels per metre are ten times the scale; the companion sees further than a zone does. */
export const RADAR_SCALE = { companion: 1.25, zone: 0.8 } as const;

/** Width of a spotter block and the gap between it and the radar. */
export const SPOTTER = { width: 18, gap: 16 } as const;

const spotterOn = (side: 'Left' | 'Right') => gt(isnull(game(`SpotterCar${side}`), num(0)), num(0));

export const radar = defineModule('radar', (ctx) => {
  const scale = ctx.density === 'companion' ? RADAR_SCALE.companion : RADAR_SCALE.zone;
  const height = ctx.frame.height;
  const plot = rect(ctx.frame.left + SPOTTER.width + SPOTTER.gap, ctx.frame.top, Math.max(0, ctx.frame.width - 2 * (SPOTTER.width + SPOTTER.gap)), height);
  const blockHeight = Math.round(height * 0.5);
  const blockTop = ctx.frame.top + Math.round((height - blockHeight) / 2);
  const item: RadarItem = {
    kind: 'radar',
    name: `${ctx.prefix}radar`,
    rect: plot,
    scale,
    useSmoothedPlayerAngle: true,
    playerStyle: { dotColor: ds.color.text.primary, dotRadius: 14, labelFontSize: 1, labelColor: ds.color.surface.base },
    opponentStyle: { dotColor: ds.color.text.secondary, dotRadius: 12, labelFontSize: 1, labelColor: ds.color.surface.base },
  };
  const side = (id: 'left' | 'right', x: number): Item => ({
    ...band(`${ctx.prefix}${id}`, rect(x, blockTop, SPOTTER.width, blockHeight), ds.color.text.dim),
    ...withBindings({ BackgroundColor: iff(spotterOn(id === 'left' ? 'Left' : 'Right'), str(ds.purpose.delta.slower), str(ds.color.text.dim)) }),
  });
  return [side('left', ctx.frame.left), item, side('right', ctx.frame.left + ctx.frame.width - SPOTTER.width)];
});
