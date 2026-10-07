/**
 * The car's top strip, in the `bar` region: the page name in red at the left, the speed in its
 * grey box over the gear, `Lap` on a grey cell, and the track state in the teal outline at the
 * right. The speed box reads the engine instead while the limiter is on, as the car's does.
 *
 * The car's page name is the name of its own dash page, which openDash does not have, so the strip
 * writes the session the car is in, `RACE`, `QUAL` or `PRACTICE`, in the same place and colour; the
 * pull request for #205 names it as the approximation it is. The speed box carries the number alone,
 * as the car's does, with no unit beside it.
 */
import type { Item, Rect } from '../../generator.ts';
import { ncalc } from '../../generator.ts';
import { limiterOn } from '../../components/pitAlerts.ts';
import { rect } from '../../design/geometry.ts';
import { band } from '../../elements/band.ts';
import { label } from '../../elements/label.ts';
import { currentLap, GRIP_WIDEST, rpm, sessionType, speed, trackGrip } from '../../second/values.ts';
import { ds } from '../../tokens.ts';
import { regionRect, zoneRect } from '../anatomy.ts';
import type { FaceContext } from '../drawing.ts';
import { headlight } from './pictograms.ts';
import { BORDER, BOX_PAD, carColour, centredY, namedCell, RADIUS, runWidth } from './register.ts';

const { and, changed, eq, fmt, iff, isIn, isNull, not, num, raw, str, ucase } = ncalc;

/** What the strip's own text is set at, from the ticket's register: the page name and the two boxes. */
const PAGE_NAME_SIZE = 24;
const SPEED_SIZE = 40;
const LAP_SIZE = 26;
const TRACK_SIZE = 27;
/** The house's widest track state, `GRIP_WIDEST`, in the capitals the car writes. */
const TRACK_WIDEST = GRIP_WIDEST.toUpperCase();


/** Every qualifying session iRacing names, which the car's `QUAL` page stands for. */
const QUALIFYING = ['Qualify', 'Lone Qualify', 'Open Qualify'];

const pageName = (): string => iff(eq(sessionType(), str('Race')), str('RACE'), iff(isIn(sessionType(), ...QUALIFYING.map((name) => str(name))), str('QUAL'), str('PRACTICE')));


export function porscheStrip(ctx: FaceContext): Item[] {
  const strip = regionRect(ctx.regions, 'bar');
  const gear = zoneRect(ctx.regions, 'A');
  const right = zoneRect(ctx.regions, 'C');
  const items: Item[] = [];

  items.push(
    label('strip.page', 'RACE', 41, centredY(strip, PAGE_NAME_SIZE), runWidth('PRACTICE', PAGE_NAME_SIZE), {
      size: PAGE_NAME_SIZE,
      color: ds.color.danger.primary,
      bind: pageName(),
      widest: 'PRACTICE',
    }),
  );

  // Over the gear and as wide as its tile, the way the car stacks the two, and the number alone in it.
  const speedBox = rect(gear.left - BORDER, strip.top + 2, gear.width + 2 * BORDER, 58);
  items.push(
    band('strip.speed.box', speedBox, carColour('tile'), { border: { color: carColour('edge'), width: BORDER }, radius: RADIUS }),
    label('strip.speed.value', '148', speedBox.left + BORDER, centredY(speedBox, SPEED_SIZE), speedBox.width - 2 * BORDER, {
      size: SPEED_SIZE,
      color: ds.color.text.primary,
      hAlign: 'center',
      bind: iff(limiterOn(), fmt(rpm(), '0'), fmt(speed(), '0')),
      widest: '8888',
    }),
  );

  // The headlight, lit for as long as the band's own alert holds a flash of the lights: iRacing
  // publishes the flash control and no headlight state, so a flash is all that can light it.
  const flash = raw('dcHeadlightFlash');
  items.push(...headlight('strip.headlight', rect(174, strip.top + 14, 50, 32), { on: and(not(isNull(flash)), changed(num(ds.indicator.alert.durationMs), flash)), colour: ds.color.text.primary }));

  // `Lap` on a grey cell over zone C, its number on a dark one.
  const lapCell = rect(right.left - 4 + 34, strip.top + 8, 178, 46);
  const lapValue: Rect = rect(lapCell.left + BOX_PAD + 64, lapCell.top + 4, lapCell.width - BOX_PAD - 64 - 4, lapCell.height - 8);
  items.push(...namedCell('strip.lap', lapCell, 'Lap', lapValue, LAP_SIZE, { sample: '16', bind: fmt(currentLap(), '0'), widest: '888' }));

  // The track state in the car's teal outline, written in capitals as the car writes DRY.
  const track = rect(strip.width - 39 - 119, strip.top + 4, 119, 56);
  // DRY and WET fit the car's 27 px with room to spare; the widest state iRacing reports does not,
  // so the box is set at the largest size that holds it rather than clipping it.
  const trackRoom = track.width - 2 * BORDER;
  let trackSize = TRACK_SIZE;
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
