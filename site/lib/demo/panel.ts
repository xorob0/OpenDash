/**
 * The fake Rig and Data panel: what the plugin's settings pages write, written into the demo's
 * property map instead, so the dashboard does the rest exactly as it does with the plugin.
 *
 * Everything it names comes from `packages/dash/src/contract.ts`, read at generate time by
 * `scripts/demo-data.ts` into a {@link PanelCatalogue}: the zone catalogues, the bar fields, the
 * value sets, the defaults and the property names themselves, each built by the contract's own
 * `zonePageSettingName` and its siblings. Nothing here spells a property. The behaviour of the
 * buttons is the plugin's (`plugin/OpenDash/FaceSettings.cs`): a wheel button steps to the next
 * enabled page in the zone's order and wraps, the back button to the previous, a zone keeps at least
 * one page enabled, and a held glance puts its page in its zone and gives the zone back on release.
 * The demo has no drag to reorder, so every zone cycles in its catalogue's order, and the position
 * it publishes is counted in that order, as `Position` in `FaceSettings.cs` counts it.
 *
 * A face of a car theme reads the same names as the default face of its size, and differs in band
 * D's catalogue alone: the theme's pages follow the house's eight, and the band opens on the first
 * of them (`pagesForZone`, `defaultZonePage`, `defaultZoneMask`).
 *
 * The companion and the pit wall are screens of their own with settings of their own
 * (`plugin/OpenDash/ScreenInstance.cs`). A companion publishes its module switches, its flag format
 * and `CompanionOpenOn`, the module the plugin forces: the start module for six seconds after it
 * opens, the glance module while the glance is held, and -2 for a second after the release, which
 * the dashboard reads as "the module the driver was on". Paging a companion is SimHub's own
 * NextScreen and PreviousScreen, which is the engine's to do, not the panel's. A pit wall publishes
 * the page it shows, a zone setting per zone of each page, class only, its flag format and the web
 * view's address.
 *
 * Pure functions over a plain state, so a React component can hold it and a test can drive it.
 */

export interface PanelPage {
  readonly number: number;
  readonly id: string;
  readonly name: string;
}

export interface PanelZone {
  readonly letter: string;
  readonly pages: readonly PanelPage[];
  readonly defaultPage: number;
  readonly defaultMask: number;
}

/** A rig-wide setting of the Data page: one of a set of words, or a switch. */
export interface PanelChoice {
  /** The property's name without the `OpenDash.` prefix, e.g. `PositionMode`. */
  readonly setting: string;
  readonly label: string;
  readonly values: readonly (string | boolean)[];
  readonly default: string | boolean;
}

/** One face's property names, each built by the contract's own function for it. */
export interface PanelFaceNames {
  readonly zonePage: Readonly<Record<string, string>>;
  readonly zoneMask: Readonly<Record<string, string>>;
  readonly zoneStart: Readonly<Record<string, string>>;
  readonly zoneClassOnly: Readonly<Record<string, string>>;
  readonly zonePosition: Readonly<Record<string, string>>;
  readonly barField: Readonly<Record<string, string>>;
  readonly quickGlance: string;
  readonly flagFormat: string;
  readonly lapReview: string;
  readonly revBar: string;
}

export interface PanelFace {
  readonly width: number;
  readonly height: number;
  readonly prefix: string;
  readonly hasBar: boolean;
  readonly barFieldsPerEnd: number;
  readonly names: PanelFaceNames;
}

/** A theme of the catalogue, and its zones: the house's, with band D's catalogue its own. */
export interface PanelTheme {
  readonly id: string;
  readonly name: string;
  readonly zones: readonly PanelZone[];
}

export interface PanelModule {
  /** Counted from one, as the companion's header counts it. */
  readonly number: number;
  readonly id: string;
  readonly name: string;
  /** Whether it is in the rotation on a fresh install. */
  readonly enabled: boolean;
}

/** The companion's settings, from `MODULE_CATALOGUE` and `companionProperties()`. */
export interface PanelCompanion {
  readonly modules: readonly PanelModule[];
  /** `CompanionModule01` and on, one per module. */
  readonly moduleNames: readonly string[];
  /** `CompanionPage`, which no package reads and the plugin still publishes. */
  readonly pageName: string;
  readonly flagFormatName: string;
  readonly openOnName: string;
  readonly flagFormats: readonly string[];
  readonly defaultFlagFormat: string;
  /** Module indices from zero, as `CompanionOpenOn` speaks them. */
  readonly defaultStart: number;
  readonly defaultGlance: number;
  /** -1, nothing forced. */
  readonly openOnNone: number;
  /** -2, the module the driver was on. */
  readonly openOnBack: number;
  /** How long the start module is forced after the companion opens, and the way back after a glance. */
  readonly openOnWindowMs: number;
  readonly backWindowMs: number;
}

export interface PanelPitWallZone {
  /** `A` to `D`, or `Wide`. */
  readonly slot: string;
  readonly kind: 'standard' | 'wide';
  readonly fallback: number;
  /** `PitWallRaceA` and its siblings. */
  readonly setting: string;
}

export interface PanelPitWallPage {
  readonly id: string;
  readonly name: string;
  readonly landscape: boolean;
  readonly zones: readonly PanelPitWallZone[];
}

/** The pit wall's settings, from `PIT_WALL_PAGES` and its neighbours in the contract. */
export interface PanelPitWall {
  readonly pages: readonly PanelPitWallPage[];
  readonly standardPages: readonly PanelPage[];
  readonly widePages: readonly PanelPage[];
  /** `PitWallPage`: the landscape page shown, an index among the landscape pages. */
  readonly pageName: string;
  readonly defaultPage: number;
  readonly classOnlyName: string;
  readonly defaultClassOnly: boolean;
  readonly flagFormatName: string;
  readonly flagFormats: readonly string[];
  readonly defaultFlagFormat: string;
  readonly webViewUrlName: string;
  readonly defaultWebViewUrl: string;
}

export interface PanelCatalogue {
  /** `OpenDash`. */
  readonly propertyPrefix: string;
  /** The default theme's zones. */
  readonly zones: readonly PanelZone[];
  /** Every theme of the catalogue, the default first, each with its zones. */
  readonly themes: readonly PanelTheme[];
  readonly barFields: readonly PanelPage[];
  /** `Left1`, `Left2`, `Right1`, `Right2`. */
  readonly barSlots: readonly string[];
  readonly defaultBarFields: Readonly<Record<string, number>>;
  readonly defaultQuickGlance: number;
  readonly flagFormats: readonly string[];
  readonly defaultFlagFormat: string;
  readonly lapReviewModes: readonly string[];
  readonly defaultLapReview: string;
  readonly revBarModes: readonly string[];
  readonly defaultRevBar: string;
  readonly rig: readonly PanelChoice[];
  /** The round faces' cards, and the default card of each slot, slot 1 first. */
  readonly cards: readonly PanelPage[];
  readonly defaultSlotCards: readonly number[];
  /** `Slot01` and on, one per slot the plugin exposes. */
  readonly slotNames: readonly string[];
  readonly faces: readonly PanelFace[];
  readonly companion: PanelCompanion;
  readonly pitWall: PanelPitWall;
}

/** Which of the plugin's screens the panel configures. */
export type PanelScreen = 'face' | 'round' | 'companion' | 'pitwall';

/** A module the plugin forces on a companion until a moment, as `ScreenInstance.ModuleForce` holds it. */
export interface CompanionForce {
  readonly module: number;
  /** In the clock `panelProperties` is handed; Infinity while a glance is held. */
  readonly until: number;
}

export interface CompanionState {
  readonly modules: readonly boolean[];
  readonly flagFormat: string;
  readonly start: number;
  readonly glance: number;
  readonly force: CompanionForce | null;
  /** Whether the glance button is down. */
  readonly held: boolean;
}

export interface PitWallState {
  /** The landscape page shown. */
  readonly page: number;
  /** Each zone's page, by its setting's name. */
  readonly zones: Readonly<Record<string, number>>;
  readonly classOnly: boolean;
  readonly flagFormat: string;
  readonly webViewUrl: string;
}

export interface PanelState {
  readonly screen: PanelScreen;
  /** The theme the face is drawn in, `default` for every screen that is not a car theme's face. */
  readonly theme: string;
  /** The face the panel configures, by prefix, or null for a screen that is not a zone face. */
  readonly face: string | null;
  readonly zones: readonly number[];
  readonly masks: readonly number[];
  readonly starts: readonly number[];
  readonly classOnly: readonly boolean[];
  readonly barFields: readonly number[];
  readonly quickGlance: number;
  readonly flagFormat: string;
  readonly lapReview: string;
  readonly revBar: string;
  /** The zone a held glance took over and the page it gives back, or null while none is held. */
  readonly glance: { readonly zone: number; readonly restore: number } | null;
  readonly slots: readonly number[];
  readonly rig: Readonly<Record<string, string | boolean>>;
  readonly companion: CompanionState;
  readonly pitWall: PitWallState;
}

export const faceByPrefix = (catalogue: PanelCatalogue, prefix: string | null): PanelFace | undefined =>
  prefix === null ? undefined : catalogue.faces.find((f) => f.prefix === prefix);

/** The prefix of the face at a size, or null when no zone face ships at that size. */
export const prefixFor = (catalogue: PanelCatalogue, width: number, height: number): string | null =>
  catalogue.faces.find((f) => f.width === width && f.height === height)?.prefix ?? null;

/** The zones of a face of a theme; the default theme's for a theme the catalogue does not hold. */
export const zonesFor = (catalogue: PanelCatalogue, theme: string): readonly PanelZone[] =>
  catalogue.themes.find((t) => t.id === theme)?.zones ?? catalogue.zones;

/** The zones of the face a panel configures. */
export const zonesOf = (catalogue: PanelCatalogue, state: PanelState): readonly PanelZone[] => zonesFor(catalogue, state.theme);

export interface PanelOptions {
  /** Which screen, when it is not a zone face; a null face with none given is a round face. */
  readonly screen?: PanelScreen;
  readonly theme?: string;
}

/** A panel as a fresh install leaves it, for one screen. */
export function initialPanel(catalogue: PanelCatalogue, face: string | null, options: PanelOptions = {}): PanelState {
  const theme = options.theme ?? 'default';
  const zones = zonesFor(catalogue, theme);
  const c = catalogue.companion;
  const w = catalogue.pitWall;
  return {
    screen: face !== null ? 'face' : (options.screen ?? 'round'),
    theme,
    face,
    zones: zones.map((z) => z.defaultPage),
    masks: zones.map((z) => z.defaultMask),
    starts: zones.map((z) => z.defaultPage),
    classOnly: zones.map(() => false),
    barFields: catalogue.barSlots.map((s) => catalogue.defaultBarFields[s] ?? 0),
    quickGlance: catalogue.defaultQuickGlance,
    flagFormat: catalogue.defaultFlagFormat,
    lapReview: catalogue.defaultLapReview,
    revBar: catalogue.defaultRevBar,
    glance: null,
    slots: catalogue.slotNames.map((_, i) => catalogue.defaultSlotCards[i] ?? 0),
    rig: Object.fromEntries(catalogue.rig.map((choice) => [choice.setting, choice.default])),
    companion: { modules: c.modules.map((m) => m.enabled), flagFormat: c.defaultFlagFormat, start: c.defaultStart, glance: c.defaultGlance, force: null, held: false },
    pitWall: {
      page: w.defaultPage,
      zones: Object.fromEntries(w.pages.flatMap((p) => p.zones.map((z) => [z.setting, z.fallback]))),
      classOnly: w.defaultClassOnly,
      flagFormat: w.defaultFlagFormat,
      webViewUrl: w.defaultWebViewUrl,
    },
  };
}

const enabled = (mask: number, page: number): boolean => Math.floor(mask / 2 ** page) % 2 === 1;
const zoneIndex = (zones: readonly PanelZone[], letter: string): number => {
  const i = zones.findIndex((z) => z.letter === letter);
  if (i < 0) throw new RangeError(`no zone ${letter}`);
  return i;
};
const replace = <T>(list: readonly T[], i: number, value: T): T[] => list.map((v, k) => (k === i ? value : v));

/**
 * The next enabled page after `page` in the catalogue's order, wrapping, or the page itself when it
 * is the only one enabled: `Contract.FirstEnabledAfter` with the order the panel did not change.
 */
export function nextEnabled(page: number, mask: number, count: number, direction: 1 | -1): number {
  for (let step = 1; step <= count; step++) {
    const candidate = (((page + direction * step) % count) + count) % count;
    if (enabled(mask, candidate)) return candidate;
  }
  return page;
}

/** Where the page showing sits in the zone's cycle, counting from one, as the plugin publishes it. */
export function cyclePosition(page: number, mask: number, count: number): number {
  let position = 1;
  for (let candidate = 0; candidate < count; candidate++) {
    if (candidate === page) break;
    if (enabled(mask, candidate)) position += 1;
  }
  return position;
}

/** A wheel button: the zone's next page, or its previous one. */
export function cycle(catalogue: PanelCatalogue, state: PanelState, letter: string, direction: 1 | -1): PanelState {
  const zones = zonesOf(catalogue, state);
  const i = zoneIndex(zones, letter);
  const count = zones[i]!.pages.length;
  return { ...state, zones: replace(state.zones, i, nextEnabled(state.zones[i]!, state.masks[i]!, count, direction)) };
}

/** Puts a zone on a page, as choosing it in the panel does. */
export function setPage(catalogue: PanelCatalogue, state: PanelState, letter: string, page: number): PanelState {
  const i = zoneIndex(zonesOf(catalogue, state), letter);
  return { ...state, zones: replace(state.zones, i, page) };
}

/**
 * Turns a page of a zone on or off. Turning off the last enabled page is refused, and a zone whose
 * page was turned off moves to the first enabled one, as `SetPageEnabled` does.
 */
export function setPageEnabled(catalogue: PanelCatalogue, state: PanelState, letter: string, page: number, on: boolean): PanelState {
  const zones = zonesOf(catalogue, state);
  const i = zoneIndex(zones, letter);
  const mask = state.masks[i]!;
  const bit = 2 ** page;
  const next = on ? (enabled(mask, page) ? mask : mask + bit) : enabled(mask, page) ? mask - bit : mask;
  if (next === 0) return state;
  const count = zones[i]!.pages.length;
  const firstEnabled = (p: number): number => (enabled(next, p) ? p : nextEnabled(p, next, count, 1));
  return {
    ...state,
    masks: replace(state.masks, i, next),
    zones: replace(state.zones, i, firstEnabled(state.zones[i]!)),
    starts: replace(state.starts, i, firstEnabled(state.starts[i]!)),
  };
}

/** Holding the glance button: its page in its zone, remembering what was there. */
export function beginGlance(state: PanelState): PanelState {
  if (state.glance) return state;
  const zone = Math.floor(state.quickGlance / 100);
  if (zone < 0 || zone >= state.zones.length) return state;
  return { ...state, glance: { zone, restore: state.zones[zone]! }, zones: replace(state.zones, zone, state.quickGlance % 100) };
}

/** Letting go: the zone goes back to the page it was on. */
export function endGlance(state: PanelState): PanelState {
  if (!state.glance) return state;
  return { ...state, zones: replace(state.zones, state.glance.zone, state.glance.restore), glance: null };
}

/** Any other field of the state, by name. */
export function update<K extends keyof PanelState>(state: PanelState, key: K, value: PanelState[K]): PanelState {
  return { ...state, [key]: value };
}

// ---------------------------------------------------------------------------------- companion

const companion = (state: PanelState, patch: Partial<CompanionState>): PanelState => ({ ...state, companion: { ...state.companion, ...patch } });

/** The modules' switches as a mask, all of them when none is on, as `ScreenInstance.ModuleMask` reads it. */
const moduleMask = (modules: readonly boolean[]): number => {
  const mask = modules.reduce((m, on, i) => (on ? m + 2 ** i : m), 0);
  return mask === 0 ? 2 ** modules.length - 1 : mask;
};

/** `Contract.FirstEnabledFrom`: the first module at or after one that the rotation leaves on. */
export function firstEnabledFrom(page: number, mask: number, count: number): number {
  if (count <= 0 || mask % 2 ** count === 0) return page;
  const from = page >= 0 && page < count ? page : 0;
  for (let step = 0; step < count; step++) {
    const candidate = (from + step) % count;
    if (enabled(mask, candidate)) return candidate;
  }
  return from;
}

/**
 * The companion opening, which is what SimHub loading it does: the start module forced for
 * `openOnWindowMs`, past any module the rotation has turned off (`OpenOnStartModule`). Choosing a
 * start module in the panel does the same.
 */
export function openCompanion(catalogue: PanelCatalogue, state: PanelState, now: number): PanelState {
  const count = catalogue.companion.modules.length;
  const module = firstEnabledFrom(state.companion.start, moduleMask(state.companion.modules), count);
  return companion(state, { force: { module, until: now + catalogue.companion.openOnWindowMs } });
}

/** Chooses the module a companion opens on, and opens on it. */
export function setCompanionStart(catalogue: PanelCatalogue, state: PanelState, start: number, now: number): PanelState {
  return openCompanion(catalogue, companion(state, { start }), now);
}

/** Puts a module in the rotation or takes it out. The last one standing stays. */
export function setModuleEnabled(state: PanelState, index: number, on: boolean): PanelState {
  const modules = replace(state.companion.modules, index, on);
  if (!modules.some(Boolean)) return state;
  return companion(state, { modules });
}

/** Holding a companion's glance: its module forced for as long as the button is down. */
export function beginCompanionGlance(state: PanelState): PanelState {
  if (state.companion.held) return state;
  return companion(state, { held: true, force: { module: state.companion.glance, until: Number.POSITIVE_INFINITY } });
}

/** Letting go: -2 for `backWindowMs`, which the dashboard reads as the module it was on. */
export function endCompanionGlance(catalogue: PanelCatalogue, state: PanelState, now: number): PanelState {
  if (!state.companion.held) return state;
  return companion(state, { held: false, force: { module: catalogue.companion.openOnBack, until: now + catalogue.companion.backWindowMs } });
}

/** `ScreenInstance.CompanionOpenOnAt`: the force in effect at a moment, or -1. */
export function companionOpenOn(catalogue: PanelCatalogue, state: PanelState, now: number): number {
  const force = state.companion.force;
  return force !== null && now < force.until ? force.module : catalogue.companion.openOnNone;
}

/** Whether a force is still to run out, so that a paused demo still ticks to see it end. */
export const forcePending = (state: PanelState, now: number): boolean => state.companion.force !== null && Number.isFinite(state.companion.force.until) && now < state.companion.force.until + 200;

// ---------------------------------------------------------------------------------- pit wall

const pitWall = (state: PanelState, patch: Partial<PitWallState>): PanelState => ({ ...state, pitWall: { ...state.pitWall, ...patch } });

/** Which landscape page a pit wall shows. */
export const setPitWallPage = (state: PanelState, page: number): PanelState => pitWall(state, { page });

/** What one zone of one page shows, by the zone's setting. */
export const setPitWallZone = (state: PanelState, setting: string, page: number): PanelState => pitWall(state, { zones: { ...state.pitWall.zones, [setting]: page } });

/** Any other pit wall setting. */
export function updatePitWall<K extends keyof PitWallState>(state: PanelState, key: K, value: PitWallState[K]): PanelState {
  return pitWall(state, { [key]: value } as Partial<PitWallState>);
}

/** The pages of one orientation of the pit wall, in the order the panel lists them. */
export const pitWallPagesFor = (catalogue: PanelCatalogue, landscape: boolean): readonly PanelPitWallPage[] => catalogue.pitWall.pages.filter((p) => p.landscape === landscape);

// --------------------------------------------------------------------------------- publishing

/**
 * Every property the panel publishes, by full name, with the values the plugin writes: an Int32 for
 * a page, a mask or a field, a boolean for a switch, a word for a mode, text for an address.
 *
 * The rig-wide settings and the slots always, and the group of the one screen the panel configures.
 * `now` is the clock a companion's force is timed in, and matters to nothing else.
 *
 * `OpenDash.PorscheCrest` is not among them: a crest is a picture file on the rig, and with none set
 * a Porsche face draws its placeholder shield, which is what the demo shows.
 */
export function panelProperties(catalogue: PanelCatalogue, state: PanelState, now = 0): Map<string, string | number | boolean> {
  const out = new Map<string, string | number | boolean>();
  const put = (name: string, value: string | number | boolean) => out.set(`${catalogue.propertyPrefix}.${name}`, value);
  for (const choice of catalogue.rig) put(choice.setting, state.rig[choice.setting] ?? choice.default);
  catalogue.slotNames.forEach((name, i) => put(name, state.slots[i] ?? catalogue.defaultSlotCards[i] ?? 0));
  if (state.screen === 'companion') {
    const c = catalogue.companion;
    c.moduleNames.forEach((name, i) => put(name, state.companion.modules[i] ?? c.modules[i]!.enabled));
    // Bookkeeping nothing reads (`COMPANION_PAGE_IS_UNREAD`): the glance while it is held, else the start.
    put(c.pageName, state.companion.held ? state.companion.glance : firstEnabledFrom(state.companion.start, moduleMask(state.companion.modules), c.modules.length));
    put(c.flagFormatName, state.companion.flagFormat);
    put(c.openOnName, companionOpenOn(catalogue, state, now));
    return out;
  }
  if (state.screen === 'pitwall') {
    const w = catalogue.pitWall;
    for (const page of w.pages) for (const z of page.zones) put(z.setting, state.pitWall.zones[z.setting] ?? z.fallback);
    put(w.pageName, state.pitWall.page);
    put(w.webViewUrlName, state.pitWall.webViewUrl);
    put(w.classOnlyName, state.pitWall.classOnly);
    put(w.flagFormatName, state.pitWall.flagFormat);
    return out;
  }
  const face = faceByPrefix(catalogue, state.face);
  if (!face) return out;
  zonesOf(catalogue, state).forEach((z, i) => {
    const count = z.pages.length;
    put(face.names.zonePage[z.letter]!, state.zones[i]!);
    put(face.names.zoneMask[z.letter]!, state.masks[i]!);
    put(face.names.zoneStart[z.letter]!, state.starts[i]!);
    put(face.names.zoneClassOnly[z.letter]!, state.classOnly[i]!);
    put(face.names.zonePosition[z.letter]!, cyclePosition(state.zones[i]!, state.masks[i]!, count));
  });
  catalogue.barSlots.forEach((slot, i) => put(face.names.barField[slot]!, state.barFields[i]!));
  put(face.names.quickGlance, state.quickGlance);
  put(face.names.flagFormat, state.flagFormat);
  put(face.names.lapReview, state.lapReview);
  put(face.names.revBar, state.revBar);
  return out;
}
