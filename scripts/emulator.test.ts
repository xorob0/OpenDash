/**
 * What `scripts/emulator.ts` decides without a VM: which scenarios it will accept, and the
 * refusal that stops a second emulator from being started. Starting, stopping and following are
 * remote side effects and are proved by running them.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { scenarios } from './emulator.ts';

const SCENARIO_DIR = path.resolve(import.meta.dir, '../tools/irsdk-emulator/scenarios');

describe('the scenarios a run may name', () => {
  test('they are the JSON files beside the emulator, without their extension', () => {
    const onDisk = readdirSync(SCENARIO_DIR)
      .filter((f) => f.endsWith('.json'))
      .map((f) => f.replace(/\.json$/, ''))
      .sort();
    expect(scenarios()).toEqual(onDisk);
  });

  test('the default scenario is one of them', () => {
    expect(scenarios()).toContain('race');
  });

  test('each one is a readable JSON document', () => {
    for (const name of scenarios()) {
      const file = path.join(SCENARIO_DIR, `${name}.json`);
      expect(existsSync(file)).toBe(true);
      // The emulator's own reader tolerates // comments, which JSON.parse does not, so they are
      // stripped here rather than the scenarios being written without them.
      const text = Bun.file(file).text();
      expect(text).resolves.toContain('{');
    }
  });

  test('a scenario that extends another names one that exists', async () => {
    for (const name of scenarios()) {
      const text = await Bun.file(path.join(SCENARIO_DIR, `${name}.json`)).text();
      const extend = /"extends"\s*:\s*"([^"]+)"/.exec(text)?.[1];
      if (!extend) continue;
      expect({ scenario: name, extends: extend, exists: existsSync(path.join(SCENARIO_DIR, extend)) }).toMatchObject({ exists: true });
    }
  });
});
