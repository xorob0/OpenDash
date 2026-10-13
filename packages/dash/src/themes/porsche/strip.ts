/**
 * The car's top strip, in the `bar` region: the page name in red at the left, the speed in its
 * grey box over the gear, `Lap` on a grey cell, and the track state in the teal outline at the
 * right. The speed box reads the engine instead while the limiter is on, as the car's does.
 *
 * The car's page name is the name of its own dash page, which openDash does not have, so the strip
 * writes the session the car is in, `RACE`, `QUAL` or `PRACTICE`, in the same place and colour; the
 * pull request for #205 names it as the approximation it is. The speed box carries the number alone,
 * as the car's does, with no unit beside it.
 *
 * Where each part sits and how large it is drawn is `geometry.ts`'s: the boxes scale with the face's
 * strip, and their text follows the box.
 */
import type { Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { rect } from '../../design/geometry.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import { currentLap, GRIP_WIDEST, pitLimiterOn, rpm, sessionType, speed, trackGrip } from '../../second/values.ts';
import { ds } from '../../tokens.ts';
import { regionRect } from '../anatomy.ts';
import type { FaceContext } from '../drawing.ts';
import { porscheFace, type StripBox } from './geometry.ts';
import { pictogram } from './pictograms.ts';
import { BORDER, carColour, centredY, namedCell, RADIUS, runWidth } from './register.ts';

const { and, changed, eq, fmt, iff, isIn, isNull, not, num, raw, str, ucase } = ncalc;

/** The house's widest track state, `GRIP_WIDEST`, in the capitals the car writes. */
const TRACK_WIDEST = GRIP_WIDEST.toUpperCase();


/** Every qualifying session iRacing names, which the car's `QUAL` page stands for. */
const QUALIFYING = ['Qualify', 'Lone Qualify', 'Open Qualify'];

const pageName = (): string => iff(eq(sessionType(), str('Race')), str('RACE'), iff(isIn(sessionType(), ...QUALIFYING.map((name) => str(name))), str('QUAL'), str('PRACTICE')));

/** A strip box's rect: its x and size, centred on the strip's height and dropped as the canvas drops it. */
const placed = (strip: Rect, box: StripBox): Rect => rect(box.left, strip.top + Math.round((strip.height - box.height) / 2 + box.drop), box.width, box.height);

export function porscheStrip(ctx: FaceContext): Item[] {
  const strip = regionRect(ctx.regions, 'bar');
  const { parts } = porscheFace(ctx.layout);
  const items: Item[] = [];

  items.push(
    label('strip.page', 'RACE', parts.page.left, centredY(strip, parts.page.size), runWidth('PRACTICE', parts.page.size), {
      size: parts.page.size,
      color: ds.color.danger.primary,
      bind: pageName(),
      widest: 'PRACTICE',
    }),
  );

  // Over the gear and as wide as its tile, the way the car stacks the two, and the number alone in it.
  const speedBox = placed(strip, parts.speed);
  items.push(
    band('strip.speed.box', speedBox, carColour('tile'), { border: { color: carColour('edge'), width: BORDER }, radius: RADIUS }),
    label('strip.speed.value', '148', speedBox.left + BORDER, centredY(speedBox, parts.speed.size), speedBox.width - 2 * BORDER, {
      size: parts.speed.size,
      color: ds.color.text.primary,
      hAlign: 'center',
      bind: iff(pitLimiterOn(), fmt(rpm(), '0'), fmt(speed(), '0')),
      widest: '8888',
    }),
  );

  // The headlight, lit for as long as the band's own alert holds a flash of the lights: iRacing
  // publishes the flash control and no headlight state, so a flash is all that can light it.
  const flash = raw('dcHeadlightFlash');
  items.push(...pictogram('strip.headlight', 'headlight', placed(strip, parts.headlight), { on: and(not(isNull(flash)), changed(num(ds.indicator.alert.durationMs), flash)), state: 'lit' }));

  // `Lap` on a grey cell over zone C, its number on a dark one.
  const { lap } = parts;
  const lapCell = placed(strip, lap);
  const lapValue: Rect = rect(lapCell.left + lap.pad + lap.title, lapCell.top + 4, lapCell.width - lap.pad - lap.title - 4, lapCell.height - 8);
  items.push(...namedCell('strip.lap', lapCell, 'Lap', lapValue, lap.value, { sample: '16', bind: fmt(currentLap(), '0'), widest: '888' }, lap.label));

  // The track state in the car's teal outline, written in capitals as the car writes DRY.
  const track = placed(strip, parts.track);
  // DRY and WET fit the car's 27 px with room to spare; the widest state iRacing reports does not,
  // so the box is set at the largest size that holds it rather than clipping it.
  const trackRoom = track.width - 2 * BORDER;
  let trackSize = parts.track.size;
  while (runWidth(TRACK_WIDEST, trackSize) > trackRoom) trackSize--;
  items.push(
    band('strip.track.box', track, ds.color.surface.base, { border: { color: carColour('compound'), width: BORDER }, radius: RADIUS }),
    label('strip.track.value', 'OPTIMUM', track.left + BORDER, centredY(track, trackSize), trackRoom, {
      size: trackSize,
      color: ds.color.text.primary,
      hAlign: 'center',
      bind: ucase(trackGrip()),
      widest: TRACK_WIDEST,
    }),
  );
  return items;
}
