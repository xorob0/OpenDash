#!/usr/bin/env bun
/**
 * interrupted: put the VM back after `bun run dev`, `shots`, `record` or `modules` was stopped with
 * Ctrl-C, which their own `finally` never sees. `scripts/interruptible.sh` runs this from its trap,
 * once the interrupted script is gone; it is not a command anybody types.
 *
 * It does what the script's `finally` would have done: stops the emulator, takes the trace recorder
 * out of SimHub after `record`, and gives the claim back. Except when another session holds the VM
 * by then, because whatever is running on it is theirs: nothing is touched and the claim is theirs
 * to release. A claim nobody holds (none was taken yet, or this run's went stale) is not released,
 * but the rest is still put back, as the `finally` would have.
 */
import { stop as stopEmulator } from './emulator.ts';
import { putBack } from './record.ts';
import { readClaim, release, resolveHost, whoAmI, type Host, type RunResult } from './vm.ts';

/** The scripts `scripts/interruptible.sh` runs, which are the ones that claim the VM and start the emulator. */
export const INTERRUPTIBLE = ['dev', 'shots', 'record', 'modules'] as const;
export type Interruptible = (typeof INTERRUPTIBLE)[number];

export const isInterruptible = (name: string): name is Interruptible => (INTERRUPTIBLE as readonly string[]).includes(name);

/** Puts the VM back after `script` was interrupted, saying each step; 0 when every step worked. */
export function interrupted(host: Host, script: Interruptible): number {
  const held = readClaim(host);
  if (held && held.who !== whoAmI()) {
    console.log(`the VM is claimed by ${held.who} since ${held.since}${held.note ? ` (${held.note})` : ''}, so it is left as it is`);
    return 0;
  }
  const steps: [string, () => RunResult][] = [['stopping the emulator', () => stopEmulator(host)]];
  if (script === 'record') steps.push(['taking the recorder out of SimHub', () => putBack(host)]);
  if (held) steps.push(['releasing the VM', () => release(host)]);
  let code = 0;
  for (const [what, run] of steps) {
    const r = run();
    console.log(`  ${what}: ${r.ok ? r.stdout.trim() : r.stderr.trim() || r.stdout.trim()}`);
    if (!r.ok) code = 1;
  }
  return code;
}

if (import.meta.main) {
  const script = process.argv[2] ?? '';
  if (!isInterruptible(script)) {
    console.error(`interrupted: one of ${INTERRUPTIBLE.join(', ')}, not ${JSON.stringify(script)}`);
    process.exit(2);
  }
  process.exit(interrupted(resolveHost(), script));
}
