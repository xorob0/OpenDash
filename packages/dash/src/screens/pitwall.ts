/**
 * The pit wall: a big screen for someone who is not driving. Three landscape pages (the race, the
 * tower and the telemetry traces) and one portrait page, each built from the same modules the
 * companion uses plus four configurable zones.
 *
 * The leaderboard is one continuous list rather than a set of per-class blocks: SimHub exposes
 * per-class rows only for the player's own class, so class headings would be a picture of data
 * that is not there. The class is a chip on every row instead.
 */
import type { Dashboard, DashboardMetadata, Item, Rect, Screen } from '../generator.ts';
import { ncalc } from '../generator.ts';
import { rect } from '../design/geometry.ts';
import { rule } from '../elements/rule.ts';
import { label } from '../elements/label.ts';
import { densityOf } from '../second/density.ts';
import { drawFieldBlock, fitFields, type FieldSpec } from '../second/field.ts';
import { panel } from '../second/header.ts';
import { centreZeroGauge } from '../second/gauge.ts';
import { sectorFields } from '../second/sectors.ts';
import { table, type ColumnId } from '../second/table.ts';
import { trace, type Series } from '../second/trace.ts';
import { track } from '../modules/track.ts';
import { fld, type ModuleContext } from '../modules/module.ts';
import {
  CHARS,
  airTemperature,
  bestLap,
  brake,
  carPosition,
  classOpponentCount,
  clock,
  clutch,
  currentLap,
  deltaColour,
  estimatedLap,
  fieldSize,
  isTimedSession,
  lapTime,
  lastLap,
  player,
  playerClass,
  referenceDelta,
  referenceLabel,
  roadTemperature,
  rpm,
  sessionBestLap,
  sessionTimeLeft,
  sessionName,
  speed,
  steering,
  throttle,
  totalLaps,
} from '../second/values.ts';
import { ds } from '../tokens.ts';
import { PIT_WALL_HEADER, pitWallHeader } from './pitwallHeader.ts';
import { zoneWidget } from './zones.ts';

const { fmt, concat, str, iff, gt, num, isnull, driver, game } = ncalc;

/** A pit wall panel draws at zone density: 24 px numerals, 13 px labels. */
const DENSITY = 'zone' as const;

/** A context for a module drawn inside a pit wall panel. */
const ctxOf = (frame: Rect, prefix: string): ModuleContext => ({ frame, density: DENSITY, prefix });

/** A block of fields filling a panel body: it wraps when the panel is narrow and shrinks when it is short. */
function fieldsIn(prefix: string, body: Rect, specs: readonly FieldSpec[]): Item[] {
  return fitFields(specs, body, DENSITY);
}

/** The session panel: which race, how long is left, where you are in it. */
export function sessionPanel(name: string, frame: Rect): Item[] {
  const d = densityOf(DENSITY);
  const { items, body } = panel(name, { frame, title: 'Session' });
  const classPosition = isnull(driver('classposition', player()), num(0));
  return [
    ...items,
    ...fieldsIn(name, body, [
      fld(ctxOf(body, `${name}.`), 'type', 'Session', { sample: 'Race', bind: sessionName(), chars: CHARS.word, fs: d.big }),
      fld(ctxOf(body, `${name}.`), 'left', 'Time left', { sample: '0:42:15', bind: iff(isTimedSession(), clock(sessionTimeLeft()), str('-:--:--')), chars: CHARS.clock, fs: d.big }),
      fld(ctxOf(body, `${name}.`), 'lap', 'Lap', {
        sample: '12',
        bind: fmt(currentLap(), '0'),
        chars: CHARS.position,
        fs: d.big,
        follower: { text: '/ 30', bind: concat(str('/ '), fmt(totalLaps(), '0')), visibleBind: gt(totalLaps(), num(0)) },
      }),
      fld(ctxOf(body, `${name}.`), 'position', 'Position', {
        sample: '4',
        bind: fmt(carPosition(player()), '0'),
        chars: CHARS.position,
        fs: d.big,
        follower: { text: '/ 24', bind: concat(str('/ '), fmt(fieldSize(), '0')) },
      }),
      fld(
        ctxOf(body, `${name}.`),
        'class',
        'Class',
        { sample: 'GT3 · P4', bind: concat(playerClass(), str(' · P'), fmt(classPosition, '0')), chars: CHARS.classPosition, fs: d.mid },
        { visibleBind: gt(classOpponentCount(), num(0)) },
      ),
    ]),
  ];
}

/** The lap delta panel: the live delta, its bar, and the three sectors of the last lap. */
export function lapDeltaPanel(name: string, frame: Rect): Item[] {
  const d = densityOf(DENSITY);
  const { items, body } = panel(name, { frame, title: 'Lap delta' });
  const value = referenceDelta();
  const deltaField = fld(ctxOf(body, `${name}.`), 'delta', 'VS SESSION BEST', { sample: '-0.21', bind: fmt(value, '0.00', true), chars: CHARS.delta, fs: d.big, colorBind: deltaColour(value) }, {
    labelBind: referenceLabel(),
    labelWidest: 'VS ALL-TIME BEST',
  });
  const deltaWidth = 160;
  const barHeight = 10;
  const topHeight = d.label + d.fieldGap + d.big;
  const barWidth = Math.max(0, body.width - deltaWidth - d.gapX);
  const sectorTop = body.top + topHeight + Math.round(d.gapY / 2);
  const sectorHeight = Math.max(0, body.top + body.height - sectorTop);
  return [
    ...items,
    ...fitFields([deltaField], rect(body.left, body.top, deltaWidth, topHeight), DENSITY),
    ...centreZeroGauge(`${name}.bar`, rect(body.left + deltaWidth + d.gapX, body.top + topHeight - barHeight - 6, barWidth, barHeight), value, { range: 2 }),
    ...sectorFields(`${name}.sector`, rect(body.left, sectorTop, body.width, sectorHeight), DENSITY, d.small),
  ];
}

/** The lap data panel: what the last lap was, what your best is, what this one is heading for. */
export function lapDataPanel(name: string, frame: Rect): Item[] {
  const d = densityOf(DENSITY);
  const { items, body } = panel(name, { frame, title: 'Lap data' });
  const ctx = ctxOf(body, `${name}.`);
  return [
    ...items,
    ...fieldsIn(name, body, [
      fld(ctx, 'estimated', 'Estimated', { sample: '1:42.1', bind: lapTime(estimatedLap(), 1), chars: CHARS.lapTime, fs: d.mid }),
      fld(ctx, 'yourBest', 'Your best', { sample: '1:42.311', bind: lapTime(bestLap()), chars: CHARS.lapTime, fs: d.mid }),
      fld(ctx, 'last', 'Last', { sample: '1:42.905', bind: lapTime(lastLap()), chars: CHARS.lapTime, fs: d.mid }),
      fld(ctx, 'sessionBest', 'Session best', { sample: '1:41.877', bind: lapTime(sessionBestLap()), chars: CHARS.lapTime, fs: d.mid, color: ds.purpose.lap.sessionBest }),
    ]),
  ];
}

/** The track panel: the map, with the conditions and the car's assists beside it. */
export function trackPanel(name: string, frame: Rect): Item[] {
  const d = densityOf(DENSITY);
  const { items, body } = panel(name, { frame, title: 'Track' });
  const mapWidth = Math.round(body.width * 0.48);
  const right = rect(body.left + mapWidth + d.gapX, body.top, Math.max(0, body.width - mapWidth - d.gapX), body.height);
  const ctx = ctxOf(right, `${name}.`);
  return [
    ...items,
    ...track.build({ frame: rect(body.left, body.top, mapWidth, body.height), density: DENSITY, prefix: `${name}.map.` }),
    ...fieldsIn(name, right, [
      fld(ctx, 'air', 'Air', { sample: '24', bind: fmt(airTemperature(), '0'), chars: CHARS.temperature, fs: d.small, follower: { text: '°' } }),
      fld(ctx, 'road', 'Road', { sample: '31', bind: fmt(roadTemperature(), '0'), chars: CHARS.temperature, fs: d.small, follower: { text: '°' } }),
      fld(ctx, 'tc', 'TC', { sample: '3', bind: fmt(isnull(game('TCLevel'), num(0)), '0'), chars: CHARS.setting, fs: d.small }),
      fld(ctx, 'abs', 'ABS', { sample: '2', bind: fmt(isnull(game('ABSLevel'), num(0)), '0'), chars: CHARS.setting, fs: d.small }),
      fld(ctx, 'bb', 'BB', { sample: '54.2', bind: fmt(isnull(game('BrakeBias'), num(0)), '0.0'), chars: CHARS.setting, fs: d.small }),
    ]),
  ];
}

/** The five traces of the telemetry page, top to bottom. */
export const TELEMETRY_TRACES: { id: string; title: string; series: () => Series[]; weight: number }[] = [
  { id: 'speed', title: 'Speed', weight: 1.3, series: () => [{ name: 'Speed', color: ds.color.text.primary, bind: speed(), min: 0, max: 300 }] },
  { id: 'rpm', title: 'RPM', weight: 1, series: () => [{ name: 'RPM', color: ds.color.text.primary, bind: rpm(), min: 0, useMaximum: false }] },
  {
    id: 'pedals',
    title: 'Throttle, brake and clutch',
    weight: 1.3,
    series: () => [
      { name: 'Throttle', color: ds.purpose.delta.faster, bind: throttle(), min: 0, max: 100 },
      { name: 'Brake', color: ds.purpose.delta.slower, bind: brake(), min: 0, max: 100 },
      { name: 'Clutch', color: ds.color.text.secondary, bind: clutch(), min: 0, max: 100 },
    ],
  },
  { id: 'steering', title: 'Steering', weight: 1, series: () => [{ name: 'Steering', color: ds.color.text.primary, bind: steering(), min: -3.5, max: 3.5 }] },
];

/** A trace panel: its title and legend, then the plot. */
export function tracePanel(name: string, frame: Rect, spec: (typeof TELEMETRY_TRACES)[number]): Item[] {
  const { items, body } = panel(name, { frame, title: spec.title, padY: 10 });
  return [...items, ...trace(`${name}.trace`, body, spec.series(), DENSITY, { legend: spec.series().length > 1 })];
}

/** Columns of each page's leaderboard, in importance order. */
export const RACE_COLUMNS: readonly ColumnId[] = ['pos', 'rank', 'num', 'name', 'class', 'gap', 'int', 'last', 'best', 's1', 's2', 's3', 'stint', 'pit', 'tyre'];
export const TOWER_COLUMNS: readonly ColumnId[] = ['pos', 'rank', 'num', 'name', 'class', 'gap', 'int', 'last', 'best', 'pit'];
export const PORTRAIT_COLUMNS: readonly ColumnId[] = ['pos', 'rank', 'num', 'name', 'class', 'gap', 'int', 'last', 'best', 's1', 's2', 's3', 'pit', 'tyre'];

/** Rows the pit wall tables stamp: a full grid of cars. */
export const PIT_WALL_ROWS = 24;

const vRule = (name: string, x: number, top: number, height: number): Item => rule(name, x, top, 1, height);

/** The race page: the field on the left, the driver's own numbers and two zones on the right. */
export function racePage(width: number, height: number): Screen {
  const header = rect(0, 0, width, PIT_WALL_HEADER.height);
  const bodyTop = PIT_WALL_HEADER.height;
  const bodyHeight = height - bodyTop;
  const boardWidth = 1280;
  const columnLeft = boardWidth + 1;
  const columnWidth = width - columnLeft;
  const panels = [
    { id: 'session', height: 104, draw: sessionPanel },
    { id: 'lapDelta', height: 150, draw: lapDeltaPanel },
    { id: 'lapData', height: 92, draw: lapDataPanel },
    { id: 'track', height: 260, draw: trackPanel },
  ];
  const items: Item[] = [
    ...pitWallHeader('race.header', { frame: header, pageName: 'Pit wall · race', page: 1, pages: 3 }),
    ...table({ name: 'race.board', frame: rect(0, bodyTop, boardWidth, bodyHeight), columns: RACE_COLUMNS, mode: 'full', density: DENSITY, rows: PIT_WALL_ROWS, rowHeight: 34 }),
    vRule('race.columnRule', boardWidth, bodyTop, bodyHeight),
  ];
  let y = bodyTop;
  for (const p of panels) {
    items.push(...p.draw(`race.${p.id}`, rect(columnLeft, y, columnWidth, p.height)));
    y += p.height;
    items.push(rule(`race.${p.id}.rule`, columnLeft, y, columnWidth, 1));
    y += 1;
  }
  const zoneHeight = Math.floor((bodyTop + bodyHeight - y - 1) / 2);
  items.push(zoneWidget('race.zoneA', rect(columnLeft, y, columnWidth, zoneHeight), 'standard', 'A'));
  items.push(rule('race.zoneRule', columnLeft, y + zoneHeight, columnWidth, 1));
  items.push(zoneWidget('race.zoneB', rect(columnLeft, y + zoneHeight + 1, columnWidth, zoneHeight), 'standard', 'B'));
  return { name: 'race', inGame: true, idle: true, pit: false, backgroundColor: ds.color.surface.base, items };
}

/** The tower page: a compact field list, the track, and three zones. */
export function towerPage(width: number, height: number): Screen {
  const bodyTop = PIT_WALL_HEADER.height;
  const bodyHeight = height - bodyTop;
  const boardWidth = 880;
  const columnLeft = boardWidth + 1;
  const columnWidth = width - columnLeft;
  const trackHeight = 400;
  const wideHeight = 255;
  const zonesTop = bodyTop + trackHeight + 1 + wideHeight + 1;
  const zoneHeight = height - zonesTop;
  const zoneWidth = Math.floor((columnWidth - 1) / 2);
  const items: Item[] = [
    ...pitWallHeader('tower.header', { frame: rect(0, 0, width, PIT_WALL_HEADER.height), pageName: 'Pit wall · tower', page: 2, pages: 3 }),
    ...table({ name: 'tower.board', frame: rect(0, bodyTop, boardWidth, bodyHeight), columns: TOWER_COLUMNS, mode: 'full', density: DENSITY, rows: PIT_WALL_ROWS, rowHeight: 28 }),
    vRule('tower.columnRule', boardWidth, bodyTop, bodyHeight),
    ...trackPanel('tower.track', rect(columnLeft, bodyTop, columnWidth, trackHeight)),
    rule('tower.trackRule', columnLeft, bodyTop + trackHeight, columnWidth, 1),
    zoneWidget('tower.wide', rect(columnLeft, bodyTop + trackHeight + 1, columnWidth, wideHeight), 'wide', 'wide'),
    rule('tower.wideRule', columnLeft, bodyTop + trackHeight + 1 + wideHeight, columnWidth, 1),
    zoneWidget('tower.zoneC', rect(columnLeft, zonesTop, zoneWidth, zoneHeight), 'standard', 'C'),
    vRule('tower.zoneRule', columnLeft + zoneWidth, zonesTop, zoneHeight),
    zoneWidget('tower.zoneD', rect(columnLeft + zoneWidth + 1, zonesTop, zoneWidth, zoneHeight), 'standard', 'D'),
  ];
  return { name: 'tower', inGame: true, idle: true, pit: false, backgroundColor: ds.color.surface.base, items };
}

/** The telemetry page: the traces of the last minute of driving, and three zones beside them. */
export function telemetryPage(width: number, height: number): Screen {
  const d = densityOf(DENSITY);
  const bodyTop = PIT_WALL_HEADER.height;
  const bodyHeight = height - bodyTop;
  const plotWidth = 1279;
  const zoneLeft = plotWidth + 1;
  const zoneWidth = width - zoneLeft;
  const footerHeight = 25;
  const weights = TELEMETRY_TRACES.reduce((sum, t) => sum + t.weight, 0);
  const available = bodyHeight - footerHeight - TELEMETRY_TRACES.length;
  const items: Item[] = [...pitWallHeader('telemetry.header', { frame: rect(0, 0, width, PIT_WALL_HEADER.height), pageName: 'Pit wall · telemetry', page: 3, pages: 3 })];
  let y = bodyTop;
  TELEMETRY_TRACES.forEach((spec) => {
    const h = Math.floor((available * spec.weight) / weights);
    items.push(...tracePanel(`telemetry.${spec.id}`, rect(0, y, plotWidth, h), spec));
    y += h;
    items.push(rule(`telemetry.${spec.id}.rule`, 0, y, plotWidth, 1));
    y += 1;
  });
  items.push(
    label('telemetry.axisStart', '0 %', 20, y + (footerHeight - d.labelSm) / 2, 60, { size: d.labelSm }),
    label('telemetry.axisName', 'TIME', 0, y + (footerHeight - d.labelSm) / 2, plotWidth, { size: d.labelSm, hAlign: 'center' }),
    label('telemetry.axisEnd', 'NOW', plotWidth - 80, y + (footerHeight - d.labelSm) / 2, 60, { size: d.labelSm, hAlign: 'right' }),
    vRule('telemetry.columnRule', plotWidth, bodyTop, bodyHeight),
  );
  const zoneHeight = Math.floor((bodyHeight - 2) / 3);
  (['A', 'B', 'C'] as const).forEach((letter, i) => {
    const top = bodyTop + i * (zoneHeight + 1);
    items.push(zoneWidget(`telemetry.zone${letter}`, rect(zoneLeft, top, zoneWidth, zoneHeight), 'standard', letter));
    if (i < 2) items.push(rule(`telemetry.zone${letter}.rule`, zoneLeft, top + zoneHeight, zoneWidth, 1));
  });
  return { name: 'telemetry', inGame: true, idle: true, pit: false, backgroundColor: ds.color.surface.base, items };
}

/** The portrait page: the field above, the driver's numbers in the middle, four zones below. */
export function portraitPage(width: number, height: number): Screen {
  const bodyTop = PIT_WALL_HEADER.height;
  const boardHeight = 856;
  const panelTop = bodyTop + boardHeight + 1;
  const panelHeight = 110;
  const zonesTop = panelTop + panelHeight + 1;
  const zoneHeight = Math.floor((height - zonesTop - 1) / 2);
  const half = Math.floor((width - 1) / 2);
  const items: Item[] = [
    ...pitWallHeader('portrait.header', { frame: rect(0, 0, width, PIT_WALL_HEADER.height), pageName: 'Pit wall · portrait', page: 1, pages: 1, compact: true }),
    ...table({ name: 'portrait.board', frame: rect(0, bodyTop, width, boardHeight), columns: PORTRAIT_COLUMNS, mode: 'full', density: DENSITY, rows: PIT_WALL_ROWS, rowHeight: 32 }),
    rule('portrait.boardRule', 0, bodyTop + boardHeight, width, 1),
    ...sessionPanel('portrait.session', rect(0, panelTop, half, panelHeight)),
    vRule('portrait.panelRule', half, panelTop, panelHeight),
    ...lapDataPanel('portrait.lapData', rect(half + 1, panelTop, width - half - 1, panelHeight)),
    rule('portrait.panelBottomRule', 0, panelTop + panelHeight, width, 1),
    zoneWidget('portrait.zoneA', rect(0, zonesTop, half, zoneHeight), 'standard', 'A'),
    vRule('portrait.zoneRuleTop', half, zonesTop, zoneHeight),
    zoneWidget('portrait.zoneB', rect(half + 1, zonesTop, half, zoneHeight), 'standard', 'B'),
    rule('portrait.zoneRuleMiddle', 0, zonesTop + zoneHeight, width, 1),
    zoneWidget('portrait.zoneC', rect(0, zonesTop + zoneHeight + 1, half, zoneHeight), 'standard', 'C'),
    vRule('portrait.zoneRuleBottom', half, zonesTop + zoneHeight + 1, zoneHeight),
    zoneWidget('portrait.zoneD', rect(half + 1, zonesTop + zoneHeight + 1, half, zoneHeight), 'standard', 'D'),
  ];
  return { name: 'portrait', inGame: true, idle: true, pit: false, backgroundColor: ds.color.surface.base, items };
}

export interface PitWallSize {
  folder: string;
  width: number;
  height: number;
  description: string;
  portrait: boolean;
}

export const PIT_WALL_SIZES: readonly PitWallSize[] = [
  { folder: 'openDash Pit wall', width: 1920, height: 1080, description: '1920 x 1080, three pages', portrait: false },
  { folder: 'openDash Pit wall portrait', width: 1080, height: 1920, description: '1080 x 1920, one page', portrait: true },
];

/** The pit wall dashboard: three landscape pages, or the one portrait page. */
export function pitWallDashboard(size: PitWallSize, metadata: DashboardMetadata): Dashboard {
  const screens = size.portrait ? [portraitPage(size.width, size.height)] : [racePage(size.width, size.height), towerPage(size.width, size.height), telemetryPage(size.width, size.height)];
  return { name: size.folder, width: size.width, height: size.height, backgroundColor: ds.color.surface.base, screens, metadata };
}
