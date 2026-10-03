/**
 * A container host with no VM behind it, for the tests of the scripts that drive the VM.
 *
 * Those scripts are a sequence of remote steps, and the faults worth pinning are in what they make
 * of a step's answer: a SimHub that never appeared, an SSH call that failed, a desktop script that
 * timed out. So the guest is a function the test writes, handed each PowerShell script decoded and
 * answering as the real guest did, while every command the host runs itself is run in a real shell
 * with the share moved into a temporary directory. The host's own loops are exercised that way
 * rather than imitated.
 */
import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { transport, type Host } from './vm.ts';

/** What the fake guest answers a PowerShell script with. Anything left out is a silent success. */
export interface Answer {
  status?: number;
  stdout?: string;
  stderr?: string;
}

/** One command the host was asked to run. */
export interface HostCall {
  command: string;
  /** The PowerShell it carried to the guest, decoded, or null for a command the host ran itself. */
  script: string | null;
}

/**
 * Runs `body` against a fake host. `guest` answers every PowerShell script and is told where the
 * share is, so it can write what a scheduled task on the desktop would have written there.
 */
export function withFakeHost<T>(
  guest: (script: string, share: string) => Answer,
  body: (host: Host, calls: HostCall[], share: string) => T,
): T {
  const share = mkdtempSync(path.join(os.tmpdir(), 'opendash-share-'));
  const calls: HostCall[] = [];
  const real = transport.run;
  transport.run = (argv) => {
    const command = argv[argv.length - 1]!;
    const encoded = /-EncodedCommand (\S+)/.exec(command)?.[1];
    const script = encoded ? Buffer.from(encoded, 'base64').toString('utf16le') : null;
    calls.push({ command, script });
    if (script !== null) {
      const a = guest(script, share);
      return { status: a.status ?? 0, stdout: a.stdout ?? '', stderr: a.stderr ?? '' };
    }
    return spawnSync('bash', ['-c', command.replaceAll('/opt/winvm/shared', share)], { encoding: 'utf8', timeout: 60_000 });
  };
  try {
    return body({ local: true, name: 'fake' }, calls, share);
  } finally {
    transport.run = real;
    rmSync(share, { recursive: true, force: true });
  }
}
