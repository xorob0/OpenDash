/** The 21 modules in page order, plus the two zone pages that are not modules. */
import { MODULE_CATALOGUE } from '../contract.ts';
import { carSettings } from './carSettings.ts';
import { damage } from './damage.ts';
import { delta } from './delta.ts';
import { energy } from './energy.ts';
import { fuel } from './fuel.ts';
import { gear } from './gear.ts';
import { inputs } from './inputs.ts';
import { lapHistory } from './lapHistory.ts';
import { lapTimes } from './lapTimes.ts';
import { leaderboard } from './leaderboard.ts';
import type { Module, ModuleBuilder, ModuleContext } from './module.ts';
import { opponents } from './opponents.ts';
import { carTelemetry, webView } from './pages.ts';
import { pitView } from './pitView.ts';
import { radar } from './radar.ts';
import { relative } from './relative.ts';
import { sectors } from './sectors.ts';
import { session } from './session.ts';
import { speedo } from './speedo.ts';
import { stint } from './stint.ts';
import { track } from './track.ts';
import { trackRivals } from './trackRivals.ts';
import { tyres } from './tyres.ts';

export type { Module, ModuleBuilder, ModuleContext } from './module.ts';
export { defineModule, fieldsRow, fld, blockRow } from './module.ts';

/** Every module, in the catalogue's page order. */
export const MODULES: readonly Module[] = [
  lapTimes,
  delta,
  sectors,
  speedo,
  fuel,
  energy,
  tyres,
  pitView,
  carSettings,
  inputs,
  session,
  radar,
  track,
  leaderboard,
  relative,
  opponents,
  gear,
  stint,
  lapHistory,
  damage,
  trackRivals,
];

/** The module with this id. */
export function moduleById(id: string): Module {
  const module = MODULES.find((m) => m.id === id);
  if (!module) throw new Error(`modules: no module with id ${id}`);
  return module;
}

/**
 * What a zone page draws: a module, or one of the two pages that are not modules. The page ids
 * come from the contract, so an id that is neither is a build-time error rather than a blank zone.
 */
export function pageBuilder(id: string): ModuleBuilder {
  if (id === 'web') return webView;
  if (id === 'carTelemetry') return carTelemetry;
  return (ctx: ModuleContext) => moduleById(id).build(ctx);
}

/** The catalogue and the builders must stay in step; the test reads this. */
export const MODULE_IDS: readonly string[] = MODULE_CATALOGUE.map((m) => m.id);
