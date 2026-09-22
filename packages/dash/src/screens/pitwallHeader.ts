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
import { withBindings, type Expr } from '../bind.ts';
import { measureText } from '../design/advances.ts';
import { rect, roundRect } from '../design/geometry.ts';
import { canvasBaseline, canvasYForBaseline, textBox } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { rule } from '../elements/rule.ts';
import { densityOf } from '../second/density.ts';
import { inlineGroup, type InlinePart } from '../second/header.ts';
import { CHARS, GRIP_WIDEST, clock, currentLap, incidentLimit, incidents, isTimedSession, localClock, sessionTimeLeft, sessionType, simClock, totalLaps, trackGrip, windKmh } from '../second/values.ts';
import { ds, TRANSPARENT } from '../tokens.ts';

const { concat, str, fmt, iff, gt, num, isnull, isNull, not, ucase } = ncalc;

/** Height of the pit wall header and the padding either side of it. */
export const PIT_WALL_HEADER = { height: 64, padX: 32, gap: 16, groupGap: 24 } as const;
/** The three page squares of the landscape dashboard. */
export const PAGE_SQUARE = { size: 8, gap: 6 } as const;
/**
 * The longest session name SimHub reports for iRacing, uppercased. `SessionTypeName` passes
 * iRacing's own `SessionType` through, and "Offline Testing" is the longest of Practice, Lone
 * Qualify, Open Qualify, Warmup, Heat, Consolation and Race.
 */
const WIDEST_SESSION_NAME = 'OFFLINE TESTING';

/**
 * iRacing writes `IncidentLimit` as a number or as the word "unlimited", which is what a hosted
 * session with no limit reports and what the header has to have room for.
 */
const WIDEST_INCIDENT_LIMIT = '/ unlimited';

/** A lap total wider than three digits is not a race anyone drives. */
const WIDEST_LAP_TOTAL = 'OF 999';

/** The same total after the portrait's "Lap 12 / 30", which writes it as a denominator. */
const WIDEST_LAP_DENOMINATOR = '/ 999';

/**
 * The strongest wind the readout has room for, unit included. iRacing's weather generator stays
 * far below it, but the box is measured before the binding exists and WPF clips what does not fit.
 */
const WIDEST_WIND = '188 km/h';

/** The wordmark, in the two weights the brand uses. Barlow Condensed Light and Bold are bundled. */
export function wordmark(name: string, x: number, top: number, fs: number): { items: Item[]; width: number } {
  // Each half is measured in its own weight: "Dash" set in Bold is wider than the same letters in
  // any other face, and a box measured from the wrong one loses its last letter.
  //
  // The two halves are the only text OpenDash draws in a weight WPF may have to synthesise, since
  // a SimHub install can be missing a face the package ships. The boxes are therefore a quarter
  // wider than the measurement, which costs nothing (they are transparent, left aligned, and the
  // layout uses the measured width) and leaves no way for the wordmark to lose a letter.
  const openWidth = Math.ceil(measureText('BarlowCondensedLight', 'open', fs)) + 2;
  const dashWidth = Math.ceil(measureText('BarlowCondensedBold', 'Dash', fs)) + 2;
  const boxOf = (width: number): number => Math.ceil(width * 1.25) + 4;
  const common = { font: ds.font.data, fontSize: fs, textColor: ds.color.text.primary, hAlign: 'left', vAlign: 'top', backgroundColor: '#00FFFFFF' } as const;
  const boxTop = Math.round(top - 0.1 * fs);
  const height = Math.ceil(1.2 * fs) + 1;
  return {
    width: openWidth + dashWidth,
    items: [
      { kind: 'text', name: `${name}.open`, rect: rect(x, boxTop, boxOf(openWidth), height), text: 'open', fontWeight: 'Light', ...common },
      { kind: 'text', name: `${name}.dash`, rect: rect(x + openWidth, boxTop, boxOf(dashWidth), height), text: 'Dash', fontWeight: 'Bold', ...common },
    ],
  };
}

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
  const labelWidth = spec.label === undefined ? 0 : Math.ceil(measureText('BarlowMedium', spec.label.toUpperCase(), labelSize)) + 2;
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
      items.push({
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
        ...withBindings({ Text: spec.bind }),
      });
      return items;
    },
  };
}

/** A right-hand group: a run of inline parts, or one proportional run of the data face. */
type HeaderGroup = { id: string; parts: InlinePart[]; run?: undefined } | { id: string; parts?: undefined; run: RunSpec };

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
            { kind: 'label', text: 'LAP' },
            { kind: 'value', sample: '12', bind: fmt(currentLap(), '0'), chars: { digits: 3, specials: 0 } },
            { kind: 'label', text: '/ 30', widest: WIDEST_LAP_DENOMINATOR, bind: concat(str('/ '), fmt(totalLaps(), '0')), visibleBind: lapTotal },
          ],
        }
      : {
          id: 'session',
          parts: [
            // Sized for "OFFLINE TESTING" and drawn from the right, so that the slack a short name
            // leaves falls to the left, into the empty middle of the header, rather than opening a
            // hole between the session name and the lap.
            { kind: 'label', text: 'RACE', widest: WIDEST_SESSION_NAME, hAlign: 'right', bind: ucase(sessionType()) },
            { kind: 'value', sample: 'L12', bind: concat(str('L'), fmt(currentLap(), '0')), chars: { digits: 4, specials: 0 } },
            { kind: 'label', text: 'OF 30', widest: WIDEST_LAP_TOTAL, bind: concat(str('OF '), fmt(totalLaps(), '0')), visibleBind: lapTotal },
          ],
        },
    {
      id: 'timeLeft',
      parts: [
        { kind: 'label', text: 'LEFT' },
        { kind: 'value', sample: '0:42:15', bind: iff(isTimedSession(), clock(sessionTimeLeft()), str('-:--:--')), chars: CHARS.clock },
      ],
    },
    {
      id: 'incidents',
      parts: [
        { kind: 'label', text: 'INC' },
        { kind: 'value', sample: '3x', bind: concat(fmt(isnull(incidents(), num(0)), '0'), str('x')), chars: { digits: 4, specials: 0 }, color: ds.purpose.alert.incident },
        ...(compact ? [] : [{ kind: 'label', text: '/ 17', widest: WIDEST_INCIDENT_LIMIT, bind: concat(str('/ '), incidentLimit()), visibleBind: hasLimit } as InlinePart]),
      ],
    },
    {
      id: 'track',
      run: { label: 'TRACK', sample: 'DRY', widest: GRIP_WIDEST, bind: trackGrip() },
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
    ...(compact
      ? []
      : [
          {
            id: 'simClock',
            parts: [
              { kind: 'label', text: 'SIM' },
              { kind: 'value', sample: '15:07', bind: simClock(), chars: { digits: 5, specials: 1 } },
            ] as InlinePart[],
          },
        ]),
    {
      id: 'localClock',
      parts: [
        { kind: 'label', text: 'LOCAL' },
        { kind: 'value', sample: '14:32', bind: localClock(), chars: { digits: 5, specials: 1 } },
      ],
    },
  ];

  const shown = compact ? groups.filter((g) => g.id !== 'wind' && g.id !== 'track') : groups;
  const readouts: Item[] = [];
  let right = frame.left + frame.width - PIT_WALL_HEADER.padX;
  for (const group of [...shown].reverse()) {
    const g = group.run ? dataRun(`${name}.${group.id}`, group.run, fs, d.labelSm) : inlineGroup(`${name}.${group.id}`, group.parts, fs, density);
    right -= g.width;
    readouts.push(...g.draw(right, top));
    right -= PIT_WALL_HEADER.groupGap;
  }

  const mark = wordmark(`${name}.wordmark`, frame.left + PIT_WALL_HEADER.padX, top - 2, 28);
  items.push(...mark.items);
  let x = frame.left + PIT_WALL_HEADER.padX + mark.width + PIT_WALL_HEADER.gap;
  const squares = spec.pages > 1 ? spec.pages * PAGE_SQUARE.size + (spec.pages - 1) * PAGE_SQUARE.gap : 0;
  const pageWidth = Math.ceil(measureText('BarlowMedium', spec.pageName.toUpperCase(), d.labelSm)) + 2;
  // The loop leaves `right` a group gap clear of everything it drew, which is the room the cluster
  // has. The squares are counted first because they say which page this is and the name only
  // repeats it, and a name that does not fit goes whole rather than cut to the room: WPF clips
  // mid-word and the strip would read "PIT WALL · PORTR".
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
