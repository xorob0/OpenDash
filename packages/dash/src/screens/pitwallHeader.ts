/**
 * The pit wall header: what an engineer needs on every page, in one 64 px strip.
 *
 * The right-hand groups are laid out from the right edge in a fixed order, each measured from its
 * own text, so a longer session name or a three-digit incident count never pushes another group
 * off the screen. Track state is not drawn: SimHub has no wetness or rubber value from iRacing,
 * and a "Dry" that is always "Dry" is worse than an empty space.
 */
import type { Item, Rect } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { measureText } from '../design/advances.ts';
import { rect } from '../design/geometry.ts';
import { canvasBaseline, canvasYForBaseline } from '../design/metrics.ts';
import { band } from '../elements/band.ts';
import { label } from '../elements/label.ts';
import { rule } from '../elements/rule.ts';
import { FLAG_PRIORITY, flagVisible } from '../components/flagStrip.ts';
import { densityOf } from '../second/density.ts';
import { inlineGroup, type InlinePart } from '../second/header.ts';
import { CHARS, clock, currentLap, incidentLimit, incidents, isTimedSession, localClock, sessionTimeLeft, sessionType, simClock, totalLaps, windKmh } from '../second/values.ts';
import { ds } from '../tokens.ts';

const { concat, str, fmt, iff, gt, num, isnull, isNull, not, ucase } = ncalc;

/** Height of the pit wall header and the padding either side of it. */
export const PIT_WALL_HEADER = { height: 64, padX: 32, gap: 20, groupGap: 28 } as const;
/** The three page squares of the landscape dashboard. */
export const PAGE_SQUARE = { size: 8, gap: 6 } as const;
/** The flag block beside the flag's name. */
export const FLAG_BLOCK = { width: 24, height: 12 } as const;

/** Each flag's colour and name, in the priority order the strip uses. */
const FLAG_LOOK: Record<string, { color: `#${string}`; name: string }> = {
  Flag_Black: { color: ds.purpose.flag.black, name: 'Black' },
  Flag_Checkered: { color: ds.purpose.flag.chequer, name: 'Chequered' },
  Flag_Yellow: { color: ds.purpose.flag.yellow, name: 'Yellow' },
  Flag_Blue: { color: ds.purpose.flag.blue, name: 'Blue' },
  Flag_White: { color: ds.purpose.flag.white, name: 'White' },
  Flag_Green: { color: ds.purpose.flag.green, name: 'Green' },
};

/** The flag colour and name as one expression each, in priority order, defaulting to no flag. */
const flagColour = (): string => FLAG_PRIORITY.reduce<string>((fallback, flag) => iff(flagVisible(flag), str(FLAG_LOOK[flag]?.color ?? ds.color.text.dim), fallback), str(ds.color.text.dim));
const flagName = (): string => FLAG_PRIORITY.reduce<string>((fallback, flag) => iff(flagVisible(flag), str((FLAG_LOOK[flag]?.name ?? '').toUpperCase()), fallback), str('NO FLAG'));

/** The wordmark, in the two weights the brand uses. Barlow Condensed Light and Bold are bundled. */
export function wordmark(name: string, x: number, top: number, fs: number): { items: Item[]; width: number } {
  // The advance table covers SemiBold; Light is narrower and Bold wider, so each half is measured
  // from SemiBold and given a tenth of slack, which is enough for Bold and invisible for Light.
  const openWidth = Math.ceil(measureText('BarlowCondensedSemiBold', 'open', fs) * 1.1);
  const dashWidth = Math.ceil(measureText('BarlowCondensedSemiBold', 'Dash', fs) * 1.1);
  const common = { font: ds.font.data, fontSize: fs, textColor: ds.color.text.primary, hAlign: 'left', vAlign: 'top', backgroundColor: '#00FFFFFF' } as const;
  const boxTop = Math.round(top - 0.1 * fs);
  const height = Math.ceil(1.2 * fs) + 1;
  return {
    width: openWidth + dashWidth,
    items: [
      { kind: 'text', name: `${name}.open`, rect: rect(x, boxTop, openWidth, height), text: 'open', fontWeight: 'Light', ...common },
      { kind: 'text', name: `${name}.dash`, rect: rect(x + openWidth, boxTop, dashWidth, height), text: 'Dash', fontWeight: 'Bold', ...common },
    ],
  };
}

export interface PitWallHeaderSpec {
  frame: Rect;
  /** "Pit wall · race" and the like. */
  pageName: string;
  /** 1-based page and page count; a portrait dashboard has one page and draws no squares. */
  page: number;
  pages: number;
  /** A narrow header drops the wind and the sim clock, which is what the portrait page needs. */
  compact?: boolean;
}

/**
 * The header. Left: wordmark, page name, page squares. Right, from the right edge inwards: the two
 * clocks, the wind, the incident count, the flag, the time left and the session and lap.
 */
export function pitWallHeader(name: string, spec: PitWallHeaderSpec, density: 'zone' = 'zone'): Item[] {
  const d = densityOf(density);
  const frame = spec.frame;
  const fs = d.small;
  const top = Math.round(frame.top + (frame.height - fs) / 2);
  const labelY = canvasYForBaseline(canvasBaseline(top, fs), d.labelSm);
  const items: Item[] = [];

  const mark = wordmark(`${name}.wordmark`, frame.left + PIT_WALL_HEADER.padX, top - 2, 28);
  items.push(...mark.items);
  let x = frame.left + PIT_WALL_HEADER.padX + mark.width + PIT_WALL_HEADER.gap;
  const pageWidth = Math.ceil(measureText('BarlowMedium', spec.pageName.toUpperCase(), d.labelSm)) + 2;
  items.push(label(`${name}.page`, spec.pageName, x, labelY, pageWidth, { size: d.labelSm, color: ds.color.text.secondary }));
  x += pageWidth + PIT_WALL_HEADER.gap;
  if (spec.pages > 1) {
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

  const hasLimit = not(isNull(incidentLimit()));
  const groups: { id: string; parts: InlinePart[] }[] = [
    {
      id: 'session',
      parts: [
        { kind: 'label', text: 'RACE', bind: ucase(sessionType()) },
        { kind: 'value', sample: 'L12', bind: concat(str('L'), fmt(currentLap(), '0')), chars: { digits: 4, specials: 0 } },
        { kind: 'label', text: 'OF 30', bind: concat(str('OF '), fmt(totalLaps(), '0')), visibleBind: gt(totalLaps(), num(0)) },
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
      id: 'flag',
      parts: [
        { kind: 'block', width: FLAG_BLOCK.width, height: FLAG_BLOCK.height, color: ds.color.text.dim, colorBind: flagColour() },
        { kind: 'label', text: 'GREEN', bind: flagName(), color: ds.color.text.primary },
      ],
    },
    {
      id: 'incidents',
      parts: [
        { kind: 'label', text: 'INC' },
        { kind: 'value', sample: '3x', bind: concat(fmt(isnull(incidents(), num(0)), '0'), str('x')), chars: { digits: 4, specials: 0 }, color: ds.purpose.fuel.low },
        { kind: 'label', text: '/ 17', bind: concat(str('/ '), incidentLimit()), visibleBind: hasLimit },
      ],
    },
    {
      id: 'wind',
      parts: [
        { kind: 'label', text: 'WIND' },
        { kind: 'value', sample: '12', bind: fmt(windKmh(), '0'), chars: { digits: 3, specials: 0 } },
        { kind: 'label', text: 'KM/H' },
      ],
    },
    {
      id: 'clocks',
      parts: [
        { kind: 'value', sample: '14:32', bind: localClock(), chars: { digits: 5, specials: 1 } },
        { kind: 'label', text: 'LOCAL' },
        { kind: 'value', sample: '15:07', bind: simClock(), chars: { digits: 5, specials: 1 } },
        { kind: 'label', text: 'SIM' },
      ],
    },
  ];

  const shown = spec.compact ? groups.filter((g) => g.id !== 'wind' && g.id !== 'clocks').concat([{ id: 'clock', parts: [{ kind: 'value', sample: '14:32', bind: localClock(), chars: { digits: 5, specials: 1 } }, { kind: 'label', text: 'LOCAL' }] }]) : groups;
  let right = frame.left + frame.width - PIT_WALL_HEADER.padX;
  for (const group of [...shown].reverse()) {
    const g = inlineGroup(`${name}.${group.id}`, group.parts, fs, density);
    right -= g.width;
    items.push(...g.draw(right, top));
    right -= PIT_WALL_HEADER.groupGap;
  }
  items.push(rule(`${name}.rule`, frame.left, frame.top + frame.height - 1, frame.width, 1));
  return items;
}
