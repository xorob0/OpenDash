/**
 * The pit wall header: what an engineer needs on every page, in one 64 px strip.
 *
 * The right-hand groups are laid out from the right edge in a fixed order, each measured from its
 * own text, so a longer session name or a three-digit incident count never pushes another group
 * off the screen. They are laid out before the left-hand cluster, because the page name is the one
 * text on the strip whose width is known at build time and is therefore what gives way when the
 * two sides meet: the portrait header once drew "OFFLINE TESTING" over "PIT WALL · PORTRAIT".
 *
 * The track state is one of them, which SimHub publishes as `TrackGripStatus` and which band D and
 * the track module bind as well. The compact portrait header drops it along with the wind and the
 * sim clock, which is room the 1080 px strip does not have.
 *
 * **There is no flag group and there should not be one.** The strip carried a colour block and a
 * word for a while, built from the six normalised `Flag_*` properties, and it was reported from a
 * rig as simply not working: those six are the only flags SimHub normalises, iRacing raises most of
 * its session state outside them, and a 24 px block on a 64 px strip is not where a driver looks for
 * a flag anyway. The flag belongs to the whole page, not to a corner of its header, and the pit wall
 * now draws it the way the companion does -- a band across the top or the full screen, off by
 * default, chosen in the plugin. `flagFormat` on the pit wall page is that setting.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { withMoreBindings, type Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { rect, roundRect } from '../design/geometry.ts';
import { wordmark } from '../components/wordmark.ts';
import { canvasBaseline, canvasYForBaseline, textBox } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { rule } from '../elements/rule.ts';
import { densityOf } from '../second/density.ts';
import { inlineGroup, type InlinePart } from '../second/header.ts';
import {
  CHARS,
  INCIDENTS_WIDEST,
  LAP_TOTAL_WIDEST,
  LAP_WIDEST,
  GRIP_WIDEST,
  currentLap,
  incidentLimit,
  incidents,
  localClock,
  meridiemWidest,
  sessionClock,
  sessionType,
  simClock,
  totalLaps,
  trackGrip,
  twelveHour,
  untimedMark,
  windKmh,
  type TimeOfDay,
} from '../second/values.ts';
import { ds, TRANSPARENT } from '../tokens.ts';

const { concat, str, fmt, iff, gt, num, isnull, isNull, not } = ncalc;

/** Height of the pit wall header and the padding either side of it. */
export const PIT_WALL_HEADER = { height: 64, padX: 32, gap: 16, groupGap: 24 } as const;
/** The three page squares of the landscape dashboard. */
export const PAGE_SQUARE = { size: 8, gap: 6 } as const;
/**
 * The longest session name SimHub reports for iRacing, in the case it reports it. `SessionTypeName` passes
 * iRacing's own `SessionType` through, and "Offline Testing" is the longest of Practice, Lone
 * Qualify, Open Qualify, Warmup, Heat, Consolation and Race.
 */
const WIDEST_SESSION_NAME = 'Offline Testing';

/**
 * iRacing writes `IncidentLimit` as a number or as the word "unlimited", which is what a hosted
 * session with no limit reports and what the header has to have room for.
 */
const WIDEST_INCIDENT_LIMIT = '/ unlimited';

/** A lap total wider than three digits is not a race anyone drives. */
const WIDEST_LAP_TOTAL = 'of 999';

/** The same total after the portrait's "Lap 12 / 30", which writes it as a denominator. */
const WIDEST_LAP_DENOMINATOR = LAP_TOTAL_WIDEST;

/**
 * The strongest wind the readout has room for, unit included. iRacing's weather generator stays
 * far below it, but the box is measured before the binding exists and WPF clips what does not fit.
 */
const WIDEST_WIND = '188 km/h';

/** A word of the data face, under the small label the group is read by. */
interface RunSpec {
  label?: string;
  sample: string;
  widest: string;
  bind: Expr;
}

/**
 * One run of the data face, measured from its own widest string rather than from monospace cells.
 *
 * The wind reads "12 km/h" and the track state reads "MODERATE", and neither can be monospaced: the
 * "m" both carry is one of the characters `font.cell.excluded` names as overrunning the digit cell,
 * so the run is drawn proportionally and the group measures it with the advances.
 */
function dataRun(name: string, spec: RunSpec, fs: number, labelSize: number): { width: number; draw(x: number, top: number): Item[] } {
  const gap = ds.space[2];
  const labelWidth = spec.label === undefined ? 0 : Math.ceil(measureText('BarlowMedium', spec.label, labelSize)) + 2;
  const runWidth = Math.ceil(measureText('BarlowCondensedSemiBold', spec.widest, fs)) + 2;
  const runOffset = spec.label === undefined ? 0 : labelWidth + gap;
  return {
    width: runOffset + runWidth,
    draw(x: number, top: number): Item[] {
      const box = textBox(top, fs);
      const items: Item[] = [];
      if (spec.label !== undefined) {
        items.push(label(`${name}.0`, spec.label, x, canvasYForBaseline(canvasBaseline(top, fs), labelSize), labelWidth, { size: labelSize }));
      }
      items.push(withMoreBindings({
        kind: 'text',
        name: `${name}.${spec.label === undefined ? 0 : 1}`,
        rect: roundRect({ left: x + runOffset, top: box.top, width: runWidth, height: box.height }),
        text: spec.sample,
        widest: spec.widest,
        font: ds.font.data,
        fontWeight: 'SemiBold',
        fontSize: fs,
        textColor: ds.color.text.primary,
        hAlign: 'left',
        vAlign: 'top',
        backgroundColor: TRANSPARENT,
      }, { Text: spec.bind }));
      return items;
    },
  };
}

/**
 * A right-hand group: a run of inline parts, or one proportional run of the data face.
 *
 * `meridiem` is the `AM` or `PM` of a clock, a part appended to the run only while the rig writes its
 * clocks to twelve hours (#324). The header is laid out for it, and every group from the clock
 * leftwards is drawn one word further right while it is not there, so on a twenty-four-hour rig no
 * gap opens where the word would be.
 */
type HeaderGroup = { id: string; parts: InlinePart[]; meridiem?: Expr; run?: undefined } | { id: string; parts?: undefined; meridiem?: undefined; run: RunSpec };

/**
 * `item`, drawn where it is while `when` holds and `dx` further along while it does not: a group of the
 * header standing aside for the word a twelve-hour clock writes after itself, and moving back up to
 * the edge on a twenty-four-hour one. The design-time place is the one without the word, which is the
 * strip every rig draws until the setting is changed.
 */
function movedUnless(when: Expr, item: Item, dx: number): Item {
  if (item.kind === 'layer') throw new Error(`${item.name}: a layer has no place of its own to move`);
  const left = item.rect.left;
  const bind = { Left: iff(when, num(left), num(left + dx)) };
  // One case per kind the header draws, since `withMoreBindings` checks its item against the one
  // type of its kind and a union is not one type.
  if (item.kind === 'text') return withMoreBindings({ ...item, rect: { ...item.rect, left: left + dx } }, bind);
  if (item.kind === 'rect') return withMoreBindings({ ...item, rect: { ...item.rect, left: left + dx } }, bind);
  throw new Error(`${item.name}: the header draws no ${item.kind}`);
}

/**
 * A clock of the day under its label: the digits right aligned in their cells, so that a twelve-hour
 * `9:05` stands against its `PM` and the empty cell falls after the label instead.
 *
 * The cells are the five `HH:mm` draws and not the six the strip used to give it. The sixth held
 * nothing any clock writes, and it was harmless only while the digits were set from the left: it
 * stood between the clock and the next group, where it read as a wider gap than the strip's others.
 */
const clockGroup = (id: string, text: string, sample: string, clock: TimeOfDay): HeaderGroup => ({
  id,
  parts: [
    { kind: 'label', text },
    { kind: 'value', sample, bind: clock.text, chars: CHARS.timeOfDay, hAlign: 'right' },
  ],
  meridiem: clock.meridiem,
});

export interface PitWallHeaderSpec {
  frame: Rect;
  /** "Pit wall · race" and the like. */
  pageName: string;
  /** 1-based page and page count; a portrait dashboard has one page and draws no squares. */
  page: number;
  pages: number;
  /**
   * A narrow header for the portrait page: it drops the wind, the sim clock, the "local" after the
   * wall clock and the incident limit, and writes the lap as "Lap 12 / 30" rather than the session
   * name and "L12 of 30". Every one of those is room the 1080 px sheet does not have.
   */
  compact?: boolean;
}

/**
 * The header. Left: wordmark, page name, page squares. Right, from the right edge inwards: the wall
 * clock, the sim clock, the wind, the track state, the incident count, the time left and the
 * session and lap.
 */
export function pitWallHeader(name: string, spec: PitWallHeaderSpec, density: 'zone' = 'zone'): Item[] {
  const d = densityOf(density);
  const frame = spec.frame;
  const fs = d.small;
  const top = Math.round(frame.top + (frame.height - fs) / 2);
  const labelY = canvasYForBaseline(canvasBaseline(top, fs), d.labelSm);
  const items: Item[] = [];

  const compact = spec.compact ?? false;
  const hasLimit = not(isNull(incidentLimit()));
  const lapTotal = gt(totalLaps(), num(0));
  const groups: HeaderGroup[] = [
    compact
      ? {
          id: 'lap',
          parts: [
            { kind: 'label', text: 'Lap' },
            { kind: 'value', sample: '12', bind: fmt(currentLap(), '0'), chars: CHARS.lap, widest: LAP_WIDEST },
            { kind: 'label', text: '/ 30', widest: WIDEST_LAP_DENOMINATOR, bind: concat(str('/ '), fmt(totalLaps(), '0')), visibleBind: lapTotal },
          ],
        }
      : {
          id: 'session',
          parts: [
            // Sized for "Offline Testing" and drawn from the right, so that the slack a short name
            // leaves falls to the left, into the empty middle of the header, rather than opening a
            // hole between the session name and the lap.
            { kind: 'label', text: 'Race', widest: WIDEST_SESSION_NAME, hAlign: 'right', bind: sessionType() },
            { kind: 'value', sample: 'L12', bind: concat(str('L'), fmt(currentLap(), '0')), chars: { digits: 4, specials: 0 }, widest: `L${LAP_WIDEST}` },
            { kind: 'label', text: 'of 30', widest: WIDEST_LAP_TOTAL, bind: concat(str('of '), fmt(totalLaps(), '0')), visibleBind: lapTotal },
          ],
        },
    {
      id: 'timeLeft',
      parts: [
        { kind: 'label', text: 'Left' },
        { kind: 'value', sample: '0:42:15', bind: sessionClock(), mark: untimedMark(), chars: CHARS.clock },
      ],
    },
    {
      id: 'incidents',
      parts: [
        { kind: 'label', text: 'Inc' },
        { kind: 'value', sample: '3x', bind: concat(fmt(isnull(incidents(), num(0)), '0'), str('x')), chars: CHARS.incidents, widest: INCIDENTS_WIDEST, color: ds.purpose.alert.incident },
        ...(compact ? [] : [{ kind: 'label', text: '/ 17', widest: WIDEST_INCIDENT_LIMIT, bind: concat(str('/ '), incidentLimit()), visibleBind: hasLimit } as InlinePart]),
      ],
    },
    {
      id: 'track',
      run: { label: 'Track', sample: 'Dry', widest: GRIP_WIDEST, bind: trackGrip() },
    },
    {
      id: 'wind',
      run: { sample: '12 km/h', widest: WIDEST_WIND, bind: concat(fmt(windKmh(), '0'), str(' km/h')) },
    },
    // Two groups and not one, each label in front of its own value.
    //
    // They were one run reading "14:32 LOCAL 15:07 SIM" -- the only group on the strip to put its
    // label *after* its value, so the eye pairs 14:32 with LOCAL only if it already knows the rule,
    // and the two pairs sat a word apart while every other group sat a group-gap apart. Reported
    // from a rig as not being able to tell which clock was which and as the spacing looking wrong.
    // Both come from the same thing, so both are fixed by the same thing.
    ...(compact ? [] : [clockGroup('simClock', 'Sim', '15:07', simClock())]),
    clockGroup('localClock', 'Local', '14:32', localClock()),
  ];

  const shown = compact ? groups.filter((g) => g.id !== 'wind' && g.id !== 'track') : groups;
  const readouts: Item[] = [];
  // Two cursors: `right` lays the strip out for a twelve-hour clock, which is the wider of the two
  // and the one everything is measured against, and `without` for a twenty-four-hour one. Where they
  // part, a group is drawn at the second and bound to the first while the words are there -- all but
  // the word itself, which is only ever drawn at the first and so is simply put there.
  let right = frame.left + frame.width - PIT_WALL_HEADER.padX;
  let without = right;
  const twelve = twelveHour();
  for (const group of [...shown].reverse()) {
    const groupName = `${name}.${group.id}`;
    let g: { width: number; draw(x: number, top: number): Item[] };
    let narrow: number;
    let word: string | undefined;
    if (group.run) {
      g = dataRun(groupName, group.run, fs, d.labelSm);
      narrow = g.width;
    } else if (group.meridiem === undefined) {
      g = inlineGroup(groupName, group.parts, fs, density);
      narrow = g.width;
    } else {
      g = inlineGroup(groupName, [...group.parts, { kind: 'label', text: 'PM', widest: meridiemWidest('BarlowMedium'), bind: group.meridiem, visibleBind: twelve }], fs, density);
      narrow = inlineGroup(groupName, group.parts, fs, density).width;
      word = `${groupName}.${group.parts.length}`;
    }
    right -= g.width;
    without -= narrow;
    const shift = without - right;
    const drawn = g.draw(right, top);
    readouts.push(...(shift === 0 ? drawn : drawn.map((item) => (item.name === word ? item : movedUnless(twelve, item, shift)))));
    right -= PIT_WALL_HEADER.groupGap;
    without -= PIT_WALL_HEADER.groupGap;
  }

  const mark = wordmark(`${name}.wordmark`, frame.left + PIT_WALL_HEADER.padX, top - 2, 28);
  items.push(...mark.items);
  let x = frame.left + PIT_WALL_HEADER.padX + mark.width + PIT_WALL_HEADER.gap;
  const squares = spec.pages > 1 ? spec.pages * PAGE_SQUARE.size + (spec.pages - 1) * PAGE_SQUARE.gap : 0;
  const pageWidth = Math.ceil(measureText('BarlowMedium', spec.pageName, d.labelSm)) + 2;
  // The loop leaves `right` a group gap clear of everything it drew, which is the room the cluster
  // has. The squares are counted first because they say which page this is and the name only
  // repeats it, and a name that does not fit goes whole rather than cut to the room: WPF clips
  // mid-word and the strip would read "Pit wall · portr".
  const room = right - x;
  if (pageWidth + (squares > 0 ? PIT_WALL_HEADER.gap + squares : 0) <= room) {
    items.push(label(`${name}.page`, spec.pageName, x, labelY, pageWidth, { size: d.labelSm, color: ds.color.text.secondary }));
    x += pageWidth + PIT_WALL_HEADER.gap;
  }
  if (squares > 0) {
    for (let i = 0; i < spec.pages; i++) {
      items.push(
        band(
          `${name}.square${i + 1}`,
          rect(x + i * (PAGE_SQUARE.size + PAGE_SQUARE.gap), Math.round(top + (fs - PAGE_SQUARE.size) / 2), PAGE_SQUARE.size, PAGE_SQUARE.size),
          i + 1 === spec.page ? ds.color.text.primary : ds.color.text.dim,
        ),
      );
    }
  }

  items.push(...readouts);
  items.push(rule(`${name}.rule`, frame.left, frame.top + frame.height - 1, frame.width, 1));
  return items;
}
