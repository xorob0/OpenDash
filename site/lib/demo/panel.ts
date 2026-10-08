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

export interface PanelCatalogue {
  /** `OpenDash`. */
  readonly propertyPrefix: string;
  readonly zones: readonly PanelZone[];
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
}

export interface PanelState {
  /** The face the panel configures, by prefix, or null for a round face, which has slots instead. */
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
}

export const faceByPrefix = (catalogue: PanelCatalogue, prefix: string | null): PanelFace | undefined =>
  prefix === null ? undefined : catalogue.faces.find((f) => f.prefix === prefix);

/** The prefix of the face at a size, or null when no zone face ships at that size. */
export const prefixFor = (catalogue: PanelCatalogue, width: number, height: number): string | null =>
  catalogue.faces.find((f) => f.width === width && f.height === height)?.prefix ?? null;

/** A panel as a fresh install leaves it, for one face. */
export function initialPanel(catalogue: PanelCatalogue, face: string | null): PanelState {
  return {
    face,
    zones: catalogue.zones.map((z) => z.defaultPage),
    masks: catalogue.zones.map((z) => z.defaultMask),
    starts: catalogue.zones.map((z) => z.defaultPage),
    classOnly: catalogue.zones.map(() => false),
    barFields: catalogue.barSlots.map((s) => catalogue.defaultBarFields[s] ?? 0),
    quickGlance: catalogue.defaultQuickGlance,
    flagFormat: catalogue.defaultFlagFormat,
    lapReview: catalogue.defaultLapReview,
    revBar: catalogue.defaultRevBar,
    glance: null,
    slots: catalogue.slotNames.map((_, i) => catalogue.defaultSlotCards[i] ?? 0),
    rig: Object.fromEntries(catalogue.rig.map((c) => [c.setting, c.default])),
  };
}

const enabled = (mask: number, page: number): boolean => Math.floor(mask / 2 ** page) % 2 === 1;
const zoneIndex = (catalogue: PanelCatalogue, letter: string): number => {
  const i = catalogue.zones.findIndex((z) => z.letter === letter);
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
  const i = zoneIndex(catalogue, letter);
  const count = catalogue.zones[i]!.pages.length;
  return { ...state, zones: replace(state.zones, i, nextEnabled(state.zones[i]!, state.masks[i]!, count, direction)) };
}

/** Puts a zone on a page, as choosing it in the panel does. */
export function setPage(catalogue: PanelCatalogue, state: PanelState, letter: string, page: number): PanelState {
  const i = zoneIndex(catalogue, letter);
  return { ...state, zones: replace(state.zones, i, page) };
}

/**
 * Turns a page of a zone on or off. Turning off the last enabled page is refused, and a zone whose
 * page was turned off moves to the first enabled one, as `SetPageEnabled` does.
 */
export function setPageEnabled(catalogue: PanelCatalogue, state: PanelState, letter: string, page: number, on: boolean): PanelState {
  const i = zoneIndex(catalogue, letter);
  const mask = state.masks[i]!;
  const bit = 2 ** page;
  const next = on ? (enabled(mask, page) ? mask : mask + bit) : enabled(mask, page) ? mask - bit : mask;
  if (next === 0) return state;
  const count = catalogue.zones[i]!.pages.length;
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

/**
 * Every property the panel publishes, by full name, with the values the plugin writes: an Int32 for
 * a page, a mask or a field, a boolean for a switch, a word for a mode.
 *
 * `OpenDash.PorscheCrest` is not among them: the demo draws the default theme, and a crest is a
 * file on the rig.
 */
export function panelProperties(catalogue: PanelCatalogue, state: PanelState): Map<string, string | number | boolean> {
  const out = new Map<string, string | number | boolean>();
  const put = (name: string, value: string | number | boolean) => out.set(`${catalogue.propertyPrefix}.${name}`, value);
  for (const choice of catalogue.rig) put(choice.setting, state.rig[choice.setting] ?? choice.default);
  catalogue.slotNames.forEach((name, i) => put(name, state.slots[i] ?? catalogue.defaultSlotCards[i] ?? 0));
  const face = faceByPrefix(catalogue, state.face);
  if (!face) return out;
  catalogue.zones.forEach((z, i) => {
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
