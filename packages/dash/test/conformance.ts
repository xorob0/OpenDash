/**
 * The three properties every theme is held to (#200), as functions of one built face: nothing
 * clips, nothing escapes its frame, nothing disappears. Each returns what is wrong as sentences
 * naming the theme and the size, so that a failure reads as the fault rather than as a diff of two
 * objects; an empty list is a face that conforms.
 *
 * They are the house face's checks with the quantifier changed. Clipping is `zoneFace.test.ts`'s
 * "nothing the face draws is clipped", escaping is `moduleBoxes()` in `secondScreens.test.ts` asked
 * of the rectangles a theme hands its modules rather than of the house faces', and disappearing is
 * the catalogue check of `anatomy.test.ts` carried down to the fields of each page.
 */
import { FACE_ZONE_LETTERS, pagesForZone, type FaceZone } from '../src/contract.ts';
import { measureText } from '../src/design/advances.ts';
import { rect, type Rect } from '../src/design/geometry.ts';
import { LINE_SPACING } from '../src/design/metrics.ts';
import type { Dashboard, Item, TextItem, WidgetItem } from '../src/generator.ts';
import { pageBuilder } from '../src/modules/index.ts';
import { PARTS, SHEDDING, archetypeFor, type Archetype } from '../src/modules/shedding.ts';
import { densityForBox } from '../src/second/density.ts';
import { shapeOf } from '../src/second/shape.ts';
import type { ThemeFace } from '../src/themes/faces.ts';
import { themesWithCode, touchedThemes, type Selection } from '../src/themes/touched.ts';
import { walkItems } from '../src/walk.ts';
import { BAND_PAGES, bandPageItems } from '../src/zones/bandPages.ts';
import { FACE_SCREEN_NAME, FACE_SCREEN_NAME_NO_REV_BAR } from '../src/zones/index.ts';
import { moduleBodyOf } from '../src/zones/pages.ts';
import { THEME_DRAWINGS } from '../src/themes/drawings.ts';
import { withHouseLayout } from '../src/themes/moduleRegister.ts';
import { cellOverruns, faceOf } from './monoGlyphs.ts';

/** Set to `full` to check every theme the build knows, whatever the branch touched. */
export const MATRIX_ENV = 'OPENDASH_THEME_MATRIX';

/**
 * The themes this run checks: every theme with code with {@link MATRIX_ENV} set to `full`, and
 * otherwise the default and the themes the branch touched, by the rule `bun run build
 * --touched-themes` uses too (`src/themes/touched.ts`), so that CI builds the faces it checked.
 */
export function themesToCheck(env: Record<string, string | undefined> = process.env): Selection {
  if (env[MATRIX_ENV] === 'full') return { ids: themesWithCode(), why: `${MATRIX_ENV}=full` };
  return touchedThemes();
}

const named = (face: ThemeFace): string => `${face.layout.width}x${face.layout.height}`;

/** What a text item draws: its widest where it is bound, its sample where it is not. */
const drawnText = (item: TextItem): string => item.widest ?? item.text;

function drawnWidth(item: TextItem): number {
  const drawn = drawnText(item);
  const mono = item.monospace;
  if (!mono) return measureText(faceOf(item), drawn, item.fontSize);
  const specials = [...drawn].filter((c) => mono.specialChars?.includes(c) ?? false).length;
  return (drawn.length - specials) * mono.charWidth + specials * mono.specialCharsWidth;
}

const screensOf = (face: ThemeFace): { dashboard: Dashboard; screen: string; items: Item[] }[] =>
  [face.built.main, ...face.built.zones].flatMap((dashboard) => dashboard.screens.map((s) => ({ dashboard, screen: s.name, items: [...walkItems(s.items)] })));

/**
 * Nothing clips: every text fits the box SimHub hands WPF, measured in the face and the weight it
 * is drawn in, and every glyph a monospaced value can draw fits its cell.
 */
export function clipped(themeId: string, face: ThemeFace): string[] {
  const at = `${themeId} ${named(face)}`;
  const problems: string[] = [];
  for (const { dashboard, screen, items } of screensOf(face)) {
    for (const item of items) {
      if (item.kind !== 'text') continue;
      const where = `${at} ${dashboard.name} ${screen}: ${item.name}`;
      const width = drawnWidth(item);
      if (width > item.rect.width) {
        problems.push(`${where} draws ${JSON.stringify(drawnText(item))} ${width.toFixed(1)} px wide in ${faceOf(item)} ${item.fontSize}, in a box ${item.rect.width} px wide`);
      }
      const line = LINE_SPACING * item.fontSize;
      if (line > item.rect.height) problems.push(`${where} needs a ${line.toFixed(1)} px line at ${item.fontSize}, in a box ${item.rect.height} px tall`);
      for (const o of cellOverruns(item)) problems.push(`${where} draws ${JSON.stringify(o.glyph)} ${o.advance} px wide in a ${o.cell} px cell`);
    }
  }
  return problems;
}

/**
 * Whether a drawn item stays in the box it was given.
 *
 * A text box is a WPF line box and it is taller than its ink at both ends. `textBox` puts its top a
 * tenth of the font size above the line it is given, so the baseline lands where the layout asked
 * for it, and the box runs about a fifth of the size below that baseline. Both tails are
 * transparent, so both are slack -- and granting it only at the bottom is why these boxes were once
 * quietly written 16 px taller than the real ones instead of being derived.
 */
export function insideBox(item: Exclude<Item, { kind: 'layer' }>, frame: Rect): boolean {
  const r = item.rect;
  const below = item.kind === 'text' ? Math.ceil(0.25 * item.fontSize) + 2 : 1;
  const above = item.kind === 'text' ? Math.ceil(0.1 * item.fontSize) + 2 : 1;
  return r.left >= frame.left - 1 && r.top >= frame.top - above && r.left + r.width <= frame.left + frame.width + 1 && r.top + r.height <= frame.top + frame.height + below;
}

const MODULE_DASHBOARD = /^zoneface-module-/;

/**
 * The body a module page is drawn in, in a zone dashboard of this size: what `zonePageScreen` cuts
 * from the frame under the zone's header, or what the theme's drawing gives the page instead, read
 * from the same function the face is built with. Neither the title nor the counter moves it.
 */
const moduleBody = (themeId: string, dashboard: Dashboard, page: string): Rect =>
  moduleBodyOf({ id: page, name: page }, { width: dashboard.width, height: dashboard.height }, THEME_DRAWINGS[themeId]);

/**
 * Nothing escapes its frame: every item stays on the dashboard it is drawn on, and every item a
 * module page draws stays in the body of the zone the theme gave it, the header being the zone's.
 * A zone dashboard carries every page of the module catalogue, so this is every module in every
 * rectangle the theme hands one.
 */
export function escaped(themeId: string, face: ThemeFace): string[] {
  const at = `${themeId} ${named(face)}`;
  const problems: string[] = [];
  for (const { dashboard, screen, items } of screensOf(face)) {
    const body = MODULE_DASHBOARD.test(dashboard.name) ? moduleBody(themeId, dashboard, screen) : undefined;
    for (const item of items) {
      if (item.kind === 'layer') continue;
      const r = item.rect;
      const where = `${at} ${dashboard.name} ${screen}: ${item.name} at ${JSON.stringify(r)}`;
      if (r.left < 0 || r.top < 0 || r.left + r.width > dashboard.width || r.top + r.height > dashboard.height) {
        problems.push(`${where} leaves the ${dashboard.width} x ${dashboard.height} dashboard`);
      } else if (body && !item.name.startsWith(`${screen}.zone.`) && !insideBox(item, body)) {
        problems.push(`${where} leaves the ${body.width} x ${body.height} body the ${screen} page is given`);
      }
    }
  }
  return problems;
}

export interface CatalogueChange {
  screen: string;
  zone: FaceZone;
  /** Pages of the contract's catalogue the zone's dashboard does not carry. */
  removed: string[];
  /** Pages it carries, but not at the index the contract gives them. */
  moved: string[];
  /** Pages it carries that the contract's catalogue does not have. */
  added: string[];
}

/** What each zone of each arrangement of a face carries, against the contract's catalogue for that zone. */
export function catalogueChanges({ built }: { built: { main: Dashboard; zones: Dashboard[] } }): CatalogueChange[] {
  const byFile = new Map(built.zones.map((d) => [`${d.name}.djson`, d]));
  return built.main.screens
    .filter((screen) => screen.name === FACE_SCREEN_NAME || screen.name === FACE_SCREEN_NAME_NO_REV_BAR)
    .flatMap((screen) =>
      FACE_ZONE_LETTERS.map((zone) => {
        const widget = screen.items.find((i): i is WidgetItem => i.kind === 'widget' && i.name === `zone${zone}`);
        if (!widget) throw new Error(`${screen.name} has no widget for zone ${zone}`);
        const dashboard = byFile.get(widget.fileName);
        if (!dashboard) throw new Error(`${screen.name} zone ${zone} points at ${widget.fileName}, which the face does not carry`);
        const carried = dashboard.screens.map((s) => s.name);
        const contract = pagesForZone(zone).map((p) => p.id);
        return {
          screen: screen.name,
          zone,
          removed: contract.filter((id) => !carried.includes(id)),
          moved: contract.filter((id, i) => carried.includes(id) && carried.indexOf(id) !== i),
          added: carried.filter((id) => !contract.includes(id)),
        };
      }),
    );
}

/** The ids a page drew: an item is `<id>.label`, `<id>.value`, `<id>.text` and so on. */
export const drewId = (names: readonly string[], id: string): boolean => names.some((name) => name === id || name.startsWith(`${id}.`));

/** A table's cells are named after the row rather than after the page, so a column reads as a suffix. */
export const drewColumn = (names: readonly string[], id: string): boolean => names.some((name) => name.endsWith(`.row.${id}`) || name.includes(`.row.${id}.`));

/**
 * What proves a part was drawn, where the page does not draw it under its own name.
 *
 * Pit view's tyre service is the one: the catalogue writes it as the single line `Tyres · RIGHTS`
 * and the module draws that summary and one toggle per corner beside it, the summary saying which
 * pair and the toggles which corner, so the part is declared as the drawing names it and proved by
 * everything the module draws for it.
 */
const PART_ITEMS: Record<string, readonly string[]> = { 'pitView.tyres': ['tyres', 'FrontLeft', 'FrontRight', 'RearLeft', 'RearRight'] };

export const drewPart = (names: readonly string[], page: string, id: string): boolean => (PART_ITEMS[`${page}.${id}`] ?? [id]).every((item) => drewId(names, item));

/** The ids a page declares at a drawing but does not draw in this box. */
export const undrawn = (page: string, names: readonly string[], archetype: Archetype): string[] => {
  const entry = SHEDDING[page]!;
  const missing: string[] = [];
  if (entry.kind === 'fields') missing.push(...entry.keeps[archetype].filter((id) => !drewId(names, id)));
  if (entry.kind === 'columns') missing.push(...entry.keeps[archetype].filter((id) => !drewColumn(names, id)));
  const parts = PARTS[page];
  if (parts) missing.push(...parts[archetype].filter((id) => !drewPart(names, page, id)));
  return missing;
};

const namesUnder = (items: readonly Item[], prefix: string): string[] =>
  items.flatMap((item) => [...walkItems([item])]).flatMap((item) => (item.name.startsWith(prefix) ? [item.name.slice(prefix.length)] : []));

/**
 * Nothing disappears: every zone of both arrangements carries every page of its catalogue at its
 * place, and every page draws every field it carries or has shed it.
 *
 * A field a page carries and a box does not draw has been shed one of two ways, and both are the
 * page's own decision rather than the theme's. The shedding table names what a page keeps at each
 * of the four drawings, so a field the table does not keep at this drawing is shed by declaration;
 * and `fitFields`, `rowsThatFit` and a rank too wide for its row shed what the box has no room for,
 * so the record of that is the page built alone into the same body, which is what it draws when
 * nothing but the box decides. A field missing from the theme's page and drawn by that one has been
 * left out by the theme, and that is the failure.
 *
 * Band D's pages are held to the same rule with the band's own fields. Zone A's are a drawing each,
 * the gear and its kin, and carry no field to lose, so the catalogue is what holds them.
 *
 * A page whose body draws nothing at all has disappeared whatever its fields say, which a page with
 * no fields to declare, a drawing, would otherwise pass: the Porsche's tyres page did. So a module
 * page that draws no item of its own, its header aside, fails where the same page built in the house's
 * layout, the theme's register standing aside, draws some in the same body.
 */
export function disappeared(themeId: string, face: ThemeFace): string[] {
  const at = `${themeId} ${named(face)}`;
  const problems: string[] = [];
  for (const change of catalogueChanges(face)) {
    const where = `${at} ${change.screen} zone ${change.zone}`;
    for (const page of change.removed) problems.push(`${where} does not carry the ${page} page`);
    for (const page of change.moved) problems.push(`${where} carries the ${page} page away from its place in the catalogue`);
  }
  for (const dashboard of face.built.zones) {
    const frame = rect(0, 0, dashboard.width, dashboard.height);
    for (const screen of dashboard.screens) {
      const page = screen.name;
      const drawn = namesUnder(screen.items, `${page}.`);
      if (MODULE_DASHBOARD.test(dashboard.name) && drawn.every((name) => name.startsWith('zone.'))) {
        const body = moduleBody(themeId, dashboard, page);
        const house = withHouseLayout(() => pageBuilder(page)({ frame: body, density: densityForBox(dashboard), prefix: `${page}.`, shape: shapeOf(body) }));
        if (house.length > 0) problems.push(`${at} ${dashboard.name}: the ${page} page draws nothing in its ${body.width} x ${body.height} body, where the page in the house's layout draws ${house.length} items`);
      }
      if (MODULE_DASHBOARD.test(dashboard.name) && SHEDDING[page]) {
        const body = moduleBody(themeId, dashboard, page);
        const density = densityForBox(dashboard);
        const archetype = archetypeFor(page, shapeOf(body), body);
        const alone = namesUnder(pageBuilder(page)({ frame: body, density, prefix: `${page}.`, shape: shapeOf(body) }), `${page}.`);
        const shed = new Set(undrawn(page, alone, archetype));
        for (const field of undrawn(page, drawn, archetype)) {
          if (!shed.has(field)) problems.push(`${at} ${dashboard.name}: the ${page} page draws no ${field} in its ${body.width} x ${body.height} body (${archetype}), where the page alone draws it`);
        }
      } else if (BAND_PAGES[page] && dashboard.name.startsWith('zoneface-band-')) {
        const corners = face.layout.bandCorners;
        const alone = namesUnder(bandPageItems(page, frame, `${page}.`, corners), `${page}.`);
        for (const { id } of BAND_PAGES[page]!) {
          if (!drewId(drawn, id) && drewId(alone, id)) problems.push(`${at} ${dashboard.name}: the ${page} page draws no ${id} in its ${frame.width} x ${frame.height} band, where the page alone draws it`);
        }
      }
    }
  }
  return problems;
}
