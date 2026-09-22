#!/usr/bin/env bun
/**
 * clips: record a few seconds of each dashboard on the VM and encode them for the website.
 *
 * A still cannot show a rev bar sweeping or a delta ticking, and the website's readers decide by
 * watching. So this is `bun run shots` with a recorder in place of the camera: one claim of the VM,
 * every package installed in one pass, the emulator started once, and per package an open, a place
 * and a recording of raw frames through SimHub's own renderer at the dashboard's own size. The
 * frames come back to the host and ffmpeg turns them into a webm, an mp4 and a poster; the guest
 * never encodes anything.
 *
 * Six seconds by default, which is one period of the emulator's rev sweep, so the bar and the
 * shift lights end at the phase they began and the loop seam is quiet. Every clip carries a
 * sidecar with the version, commit, scenario and rate it was taken at; `site/scripts/sync-clips.ts`
 * reads it.
 *
 *   bun run clips                                    # the three the site shows
 *   bun run clips --packages 'OpenDash 1280x480'     # one more
 *   bun run clips --encode-only                      # re-encode what build/clips holds
 */
import { existsSync, mkdirSync, readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { LIST_ORDER, packageSize } from './dev.ts';
import { build as buildEmulator, scenarios, start as startEmulator, stop as stopEmulator, upload as uploadEmulator, waitForLaps } from './emulator.ts';
import { closeDashboards, guiAvailable, openDashboard, placeDashboards, recordDashboard, type Recording } from './gui.ts';
import { provenance } from './shotsRun.ts';
import { claim, install, readClaim, release, resolveHost, sleep, status, up, waitReady, whoAmI, type Host } from './vm.ts';

const repoRoot = path.resolve(import.meta.dir, '..');

/** The clips the site shows: the base face, the companion and the pit wall. */
export const CLIP_PACKAGES: readonly string[] = ['OpenDash 850x480', 'OpenDash Companion', 'OpenDash Pit wall'];

export interface ClipsOptions {
  packages: readonly string[];
  scenario: string;
  seconds: number;
  /** `auto` picks from the package's size. */
  fps: number | 'auto';
  preroll: number;
  outDir: string;
  noBuild: boolean;
  keep: boolean;
  noEncode: boolean;
  encodeOnly: boolean;
  gif: boolean;
}

const list = (value: string | undefined): string[] | undefined =>
  value === undefined ? undefined : value.split(',').map((s) => s.trim()).filter((s) => s.length > 0);

export function parseArgs(argv: readonly string[]): ClipsOptions | { help: true } {
  if (argv.includes('--help') || argv.includes('-h')) return { help: true };
  const flagValue = (name: string): string | undefined => {
    const index = argv.indexOf(`--${name}`);
    if (index >= 0 && argv[index + 1] && !argv[index + 1]!.startsWith('--')) return argv[index + 1];
    return argv.find((a) => a.startsWith(`--${name}=`))?.split('=').slice(1).join('=');
  };
  const fps = flagValue('fps');
  return {
    packages: list(flagValue('packages')) ?? [...CLIP_PACKAGES],
    scenario: flagValue('scenario') ?? 'gallery',
    seconds: Number(flagValue('seconds') ?? 6),
    fps: fps === undefined || fps === 'auto' ? 'auto' : Number(fps),
    preroll: Number(flagValue('preroll') ?? 1),
    outDir: flagValue('out') ?? path.join(repoRoot, 'build/clips'),
    noBuild: argv.includes('--no-build'),
    keep: argv.includes('--keep'),
    noEncode: argv.includes('--no-encode'),
    encodeOnly: argv.includes('--encode-only'),
    gif: argv.includes('--gif'),
  };
}

/** `OpenDash Pit wall` -> `opendash-pit-wall`, the same rule as the site's. */
export const clipSlug = (packageName: string): string =>
  packageName
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');

/** 20 fps for a face, 15 for anything past 1280 x 720: the guest disk is the ceiling at 1080p. */
export const fpsFor = (size: { width: number; height: number }): number => (size.width * size.height > 1280 * 720 ? 15 : 20);

/** Frames per second actually achieved, from the timestamps of the frames that were written. */
export function measuredFps(ticksText: string): number {
  const t = ticksText
    .split('\n')
    .map((l) => Number(l.trim()))
    .filter((n) => Number.isFinite(n));
  if (t.length < 2) return 0;
  return ((t.length - 1) * 1000) / (t[t.length - 1]! - t[0]!);
}

/** What one recording produced, written beside its frames. */
export interface ClipRecord {
  schema: 1;
  slug: string;
  package: string;
  scenario: string;
  version: string;
  commit: string;
  simHubVersion: string;
  takenAt: string;
  seconds: number;
  fps: number;
  preroll: number;
  recording: Recording;
}

export const RECORD_FILE = 'record.json';

export interface EncodeInput {
  raw: string;
  width: number;
  height: number;
  /** The input rate, which is the measured one, so a clip recorded at 18.7 fps plays in real time. */
  inputFps: number;
  outputFps: number;
  seconds: number;
  preroll: number;
}

const common = (i: EncodeInput): string[] => [
  '-y',
  '-f', 'rawvideo',
  '-pix_fmt', 'bgra',
  '-video_size', `${i.width}x${i.height}`,
  '-framerate', i.inputFps.toFixed(3),
  '-i', i.raw,
  '-ss', String(i.preroll),
  '-t', String(i.seconds),
  '-vf', 'pad=ceil(iw/2)*2:ceil(ih/2)*2',
  '-r', String(i.outputFps),
  '-an',
];

export const webmArgs = (i: EncodeInput, out: string, crf = 32): string[] => [
  ...common(i),
  '-c:v', 'libvpx-vp9',
  '-crf', String(crf),
  '-b:v', '0',
  '-deadline', 'good',
  '-cpu-used', '1',
  '-row-mt', '1',
  '-tile-columns', '1',
  '-g', String(Math.round(i.outputFps * i.seconds)),
  '-pix_fmt', 'yuv420p',
  '-f', 'webm',
  out,
];

export const mp4Args = (i: EncodeInput, out: string, crf = 22): string[] => [
  ...common(i),
  '-c:v', 'libx264',
  '-preset', 'slow',
  '-crf', String(crf),
  '-profile:v', 'high',
  '-level', '4.1',
  '-pix_fmt', 'yuv420p',
  '-g', String(Math.round(i.outputFps * i.seconds)),
  '-movflags', '+faststart',
  out,
];

/** The first kept frame, so the poster and the clip's first frame are the same pixels. */
export const posterArgs = (i: EncodeInput, out: string): string[] => [
  '-y',
  '-f', 'rawvideo',
  '-pix_fmt', 'bgra',
  '-video_size', `${i.width}x${i.height}`,
  '-framerate', i.inputFps.toFixed(3),
  '-i', i.raw,
  '-ss', String(i.preroll),
  '-frames:v', '1',
  '-pix_fmt', 'rgb24',
  out,
];

/** For a release body, which renders a GIF and not a video. Smaller and slower than the site's clip. */
export const gifArgs = (i: EncodeInput, out: string): string[] => [
  ...common(i).filter((a) => a !== '-r' && !/^\d+$/.test(a) || a === String(i.seconds) || a === String(i.preroll)),
  '-vf', 'fps=12,scale=640:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3',
  '-loop', '0',
  out,
];

/** Whether nothing moved: the first and last kept frames are byte for byte the same picture. */
export function frozen(raw: Uint8Array, width: number, height: number, frames: number, skip: number): boolean {
  const frameBytes = width * height * 4;
  if (frames - skip < 2 || raw.byteLength < frames * frameBytes) return false;
  const first = raw.subarray(skip * frameBytes, (skip + 1) * frameBytes);
  const last = raw.subarray((frames - 1) * frameBytes, frames * frameBytes);
  return Bun.hash(first) === Bun.hash(last);
}

export function ffmpegPath(): string | null {
  return Bun.which('ffmpeg');
}

const run = (args: string[]): { ok: boolean; err: string } => {
  const r = Bun.spawnSync(['ffmpeg', ...args], { stdout: 'pipe', stderr: 'pipe' });
  return { ok: r.exitCode === 0, err: new TextDecoder().decode(r.stderr).trim().split('\n').slice(-4).join('\n') };
};

/** Encodes one recorded clip directory in place. */
export function encode(dir: string, opts: { gif: boolean }): { ok: boolean; why?: string; bytes?: { webm: number; mp4: number; poster: number } } {
  const recordPath = path.join(dir, RECORD_FILE);
  const raw = path.join(dir, 'frames.raw');
  const ticks = path.join(dir, 'frames.ticks');
  if (!existsSync(recordPath) || !existsSync(raw)) return { ok: false, why: 'no recording here' };
  const record = JSON.parse(readFileSync(recordPath, 'utf8')) as ClipRecord;
  const inputFps = existsSync(ticks) ? measuredFps(readFileSync(ticks, 'utf8')) || record.recording.measuredFps : record.recording.measuredFps;
  const input: EncodeInput = { raw, width: record.recording.width, height: record.recording.height, inputFps, outputFps: record.fps, seconds: record.seconds, preroll: record.preroll };
  const skip = Math.round(inputFps * record.preroll);
  if (frozen(new Uint8Array(readFileSync(raw)), input.width, input.height, record.recording.frames, skip)) {
    return { ok: false, why: 'nothing moved during the clip: SimHub was probably not connected to the emulator (bun run vm logs 40)' };
  }
  const base = path.join(dir, record.slug);
  for (const [what, args] of [
    ['webm', webmArgs(input, `${base}.webm`)],
    ['mp4', mp4Args(input, `${base}.mp4`)],
    ['poster', posterArgs(input, `${base}.png`)],
    ...(opts.gif ? [['gif', gifArgs(input, `${base}.gif`)]] : []),
  ] as const) {
    const r = run(args as string[]);
    if (!r.ok) return { ok: false, why: `${what}: ${r.err}` };
  }
  return { ok: true, bytes: { webm: statSync(`${base}.webm`).size, mp4: statSync(`${base}.mp4`).size, poster: statSync(`${base}.png`).size } };
}

const USAGE = `clips: record a few seconds of each dashboard on the VM and encode them for the website.

  bun run clips [--packages a,b] [--scenario gallery] [--seconds 6] [--fps auto|20|15] [--preroll 1]
                [--out build/clips] [--no-build] [--keep] [--no-encode] [--encode-only] [--gif]

  --packages     comma separated; default ${CLIP_PACKAGES.join(', ')}
  --scenario     default gallery; one of ${scenarios().join(', ') || '(none built)'}
  --seconds      length kept; default 6, one period of the rev sweep
  --fps          default auto: 20, or 15 past 1280 x 720
  --no-build     skip the dashboard build
  --keep         leave the emulator running and the VM claimed, so bun run shots --no-build can follow
  --no-encode    record only; encode later with --encode-only
  --encode-only  no VM: encode every recording under --out again
  --gif          also write a GIF, for a release body

Needs ffmpeg on this host: apt-get install ffmpeg.
`;

export async function clips(host: Host, opts: ClipsOptions): Promise<number> {
  if (!opts.noEncode && !ffmpegPath()) {
    console.error('ffmpeg is not installed on this host: apt-get install ffmpeg (Debian 13 carries 7.1). Or pass --no-encode and encode later with --encode-only.');
    return 1;
  }
  if (opts.encodeOnly) return encodeAll(opts);

  const unknown = opts.packages.filter((p) => !LIST_ORDER.includes(p as (typeof LIST_ORDER)[number]));
  if (unknown.length > 0) {
    console.error(`unknown package${unknown.length > 1 ? 's' : ''}: ${unknown.join(', ')}\none of ${LIST_ORDER.join(', ')}`);
    return 1;
  }
  const built = scenarios();
  if (built.length > 0 && !built.includes(opts.scenario)) {
    console.error(`unknown scenario ${opts.scenario}; one of ${built.join(', ')}`);
    return 1;
  }
  const held = readClaim(host);
  if (held && held.who !== whoAmI()) {
    console.error(`the VM is claimed by ${held.who} since ${held.since}${held.note ? ` (${held.note})` : ''}\nThere is one VM. Wait, or ask them to run \`bun run vm release\`.`);
    return 1;
  }
  const claimed = claim(host, `clips ${opts.packages.length}`);
  if (!claimed.ok) {
    console.error(claimed.stderr);
    return 1;
  }

  const recorded: string[] = [];
  const failed: string[] = [];
  try {
    if (!status(host).stdout.includes('guest-ssh: up')) {
      console.log('starting the VM');
      up(host);
      if (!waitReady(host, 300)) {
        console.error('the VM did not answer within five minutes; try `bun run vm status`');
        return 1;
      }
    }
    if (!opts.noBuild) {
      console.log('building the packages');
      const b = Bun.spawnSync(['bun', 'run', 'build'], { cwd: repoRoot, stdout: 'pipe', stderr: 'pipe' });
      if (b.exitCode !== 0) {
        console.error(new TextDecoder().decode(b.stderr) || new TextDecoder().decode(b.stdout));
        return 1;
      }
    }
    for (const p of opts.packages) {
      if (!packageSize(p)) {
        console.error(`build/manifest.json has no size for ${p}; run bun run build`);
        return 1;
      }
    }
    console.log(`installing ${opts.packages.length} package${opts.packages.length > 1 ? 's' : ''} and restarting SimHub`);
    const installed = install(host, opts.packages);
    if (!installed.ok) {
      console.error(installed.stderr || installed.stdout);
      return 1;
    }
    if (!guiAvailable(host)) {
      console.error('the VNC tooling is not on the VM host, so a dash cannot be opened from here');
      return 1;
    }
    const emulator = buildEmulator();
    if (!emulator.ok) {
      console.error(emulator.stderr || emulator.stdout);
      return 1;
    }
    const uploaded = uploadEmulator(host);
    if (!uploaded.ok) {
      console.error(uploaded.stderr);
      return 1;
    }
    console.log(`\nscenario ${opts.scenario}`);
    const running = startEmulator(host, { scenario: opts.scenario, replace: true });
    if (!running.ok) {
      console.error(running.stderr);
      return 1;
    }
    sleep(8);
    console.log('  letting 2 laps go by first');
    if (!waitForLaps(host, 2)) console.error('  the laps did not come; recording anyway');

    mkdirSync(opts.outDir, { recursive: true });
    for (const [index, packageName] of opts.packages.entries()) {
      const slug = clipSlug(packageName);
      const dir = path.join(opts.outDir, slug);
      const size = packageSize(packageName)!;
      const fps = opts.fps === 'auto' ? fpsFor(size) : opts.fps;
      process.stdout.write(`  [${index + 1}/${opts.packages.length}] ${packageName} at ${fps} fps for ${opts.seconds} s `);
      const opened = openDashboard(host, { name: packageName });
      if (!opened.ok) {
        console.log(`could not be opened (${opened.stderr.split('\n')[0]})`);
        failed.push(packageName);
        continue;
      }
      sleep(5);
      placeDashboards(host, 0, 0, { name: packageName, ...size });
      sleep(2);
      let rec = recordDashboard(host, packageName, { seconds: opts.seconds, fps, preroll: opts.preroll, localDir: dir });
      if (!rec.ok && /off-screen/.test(rec.stderr + rec.stdout)) {
        placeDashboards(host, 0, 0, { name: packageName, ...size });
        sleep(2);
        rec = recordDashboard(host, packageName, { seconds: opts.seconds, fps, preroll: opts.preroll, localDir: dir });
      }
      closeDashboards(host);
      if (!rec.ok || !rec.recording) {
        console.log(`recording failed (${(rec.stderr || rec.stdout).split('\n')[0]})`);
        failed.push(packageName);
        continue;
      }
      const record: ClipRecord = {
        schema: 1,
        slug,
        package: packageName,
        scenario: opts.scenario,
        ...(({ version, commit, simHubVersion }) => ({ version, commit, simHubVersion }))(provenance(opts.scenario)),
        takenAt: new Date().toISOString(),
        seconds: opts.seconds,
        fps,
        preroll: opts.preroll,
        recording: rec.recording,
      };
      writeFileSync(path.join(dir, RECORD_FILE), `${JSON.stringify(record, null, 2)}\n`);
      const r = rec.recording;
      console.log(`recorded ${r.frames} frames at ${r.measuredFps.toFixed(2)} fps, ${r.dropped} dropped, ${r.captureMeanMs.toFixed(1)} ms mean`);
      if (r.dropped > 0.02 * r.frames) console.log(`    more than 2% dropped; retry with --fps ${Math.max(10, fps - 5)}`);
      recorded.push(packageName);
    }
  } finally {
    if (!opts.keep) {
      closeDashboards(host);
      stopEmulator(host);
      release(host);
    }
  }

  console.log(`\n  ${recorded.length} of ${opts.packages.length} recorded into ${path.relative(repoRoot, opts.outDir)}/`);
  for (const p of failed) console.log(`  missing  ${p}`);
  if (opts.noEncode) return failed.length === 0 ? 0 : 1;
  const code = encodeAll(opts);
  return failed.length === 0 && code === 0 ? code : 1;
}

function encodeAll(opts: ClipsOptions): number {
  if (!existsSync(opts.outDir)) {
    console.error(`${opts.outDir} does not exist`);
    return 1;
  }
  const dirs = readdirSync(opts.outDir).filter((d) => existsSync(path.join(opts.outDir, d, RECORD_FILE)));
  if (dirs.length === 0) {
    console.error(`nothing to encode under ${opts.outDir}`);
    return 1;
  }
  let bad = 0;
  console.log('\nencoding');
  for (const d of dirs) {
    process.stdout.write(`  ${d} `);
    const r = encode(path.join(opts.outDir, d), { gif: opts.gif });
    if (r.ok && r.bytes) console.log(`webm ${Math.round(r.bytes.webm / 1024)} KB, mp4 ${Math.round(r.bytes.mp4 / 1024)} KB, poster ${Math.round(r.bytes.poster / 1024)} KB`);
    else {
      console.log(`failed: ${r.why}`);
      bad += 1;
    }
  }
  console.log(`\n  then: cd site && bun scripts/sync-clips.ts ../${path.relative(repoRoot, opts.outDir)}\n`);
  return bad === 0 ? 0 : 1;
}

if (import.meta.main) {
  const opts = parseArgs(process.argv.slice(2));
  if ('help' in opts) console.log(USAGE);
  else process.exit(await clips(resolveHost(), opts));
}
