#!/usr/bin/env bun
/**
 * previews: refresh the thumbnails SimHub's dashboard list draws beside each package.
 *
 * A generated package is the one kind that has to bring its own gallery picture. DashStudio writes
 * `<dashboard>.djson.png` on every save, so a dashboard somebody drew by hand always has one;
 * nothing writes it for a package this repository zips, and SimHub then lists OpenDash as a column
 * of grey boxes. `packages/dash/previews/<folder>.png` is where the pictures live and `build.ts`
 * is what copies them in; this is what takes them.
 *
 * It is `bun run shots` with two things done to the result. The capture itself is unchanged --
 * the same claim, the same single install, the same emulator, the same `PrintWindow` at the
 * dashboard's own size -- because ADR 0008 is what says a picture of a dash comes from SimHub on
 * the VM and not from a renderer of our own. What this adds is the scaling, to the size DashStudio
 * writes, and the file name SimHub insists on.
 *
 *   bun run previews                                   # every package, on the green scenario
 *   bun run previews --packages 'OpenDash 850x480'
 *   bun run previews --scenario race --keep
 *
 * Refreshing one is a commit of its own kind: the picture is a photograph of an earlier build, so
 * a face that has been redrawn shows the old face until somebody runs this. Say in the commit
 * message which faces moved, exactly as `media/readme/` asks.
 *
 * It needs the VM, and it needs `ffmpeg` on this machine to do the scaling. The VM is the larger
 * ask of the two; see docs/dev-loop.md.
 */
import { copyFileSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { pngSize } from '../packages/dash/src/generator.ts';
import { PREVIEW_RUN_FILE, PREVIEWS_DIR, previewFileName, readPreviewRun, type PreviewRecord, type PreviewRun } from '../packages/dash/src/previews.ts';
import { LIST_ORDER } from './dev.ts';
import { shotName, shots } from './shots.ts';
import { readRun } from './shotsRun.ts';
import { resolveHost } from './vm.ts';

const repoRoot = path.resolve(import.meta.dir, '..');

/**
 * How big a thumbnail is.
 *
 * SimHub decodes the file at `DecodePixelWidth` 200 for the list, and DashStudio saves its own at
 * `maxheight` 300, so 300 tall is the size the format is used at and the size a hand-saved
 * dashboard's picture already is. The width floor is the one place this differs: a portrait
 * companion 300 tall is 180 wide, which SimHub would then stretch back up to 200 and blur, so a
 * package that is taller than it is wide is sized by its width instead. Nothing is ever enlarged.
 */
export const PREVIEW_HEIGHT = 300;
export const PREVIEW_MIN_WIDTH = 200;

/** The size a capture is scaled to, given the size it was taken at. */
export function previewSize(width: number, height: number): { width: number; height: number } {
  const scale = Math.min(1, Math.max(PREVIEW_HEIGHT / height, PREVIEW_MIN_WIDTH / width));
  return { width: Math.max(1, Math.round(width * scale)), height: Math.max(1, Math.round(height * scale)) };
}

export interface Options {
  packages: readonly string[];
  scenario: string;
  noBuild: boolean;
  keep: boolean;
  /** Passed through to `bun run shots`; see its own ShotsOptions for why two is the default. */
  warmLaps: number;
}

const list = (value: string | undefined): string[] | undefined =>
  value === undefined ? undefined : value.split(',').map((s) => s.trim()).filter((s) => s.length > 0);

export function parseArgs(argv: readonly string[]): Options | { help: true } {
  if (argv.includes('--help') || argv.includes('-h')) return { help: true };
  const flagValue = (name: string): string | undefined => {
    const index = argv.indexOf(`--${name}`);
    if (index >= 0 && argv[index + 1] && !argv[index + 1]!.startsWith('--')) return argv[index + 1];
    return argv.find((a) => a.startsWith(`--${name}=`))?.split('=').slice(1).join('=');
  };
  return {
    packages: list(flagValue('packages')) ?? LIST_ORDER,
    scenario: flagValue('scenario') ?? 'green',
    noBuild: argv.includes('--no-build'),
    keep: argv.includes('--keep'),
    warmLaps: Number(flagValue('warm-laps') ?? 2),
  };
}

const USAGE = `previews: photograph every package for SimHub's dashboard list.

  bun run previews [--packages a,b] [--scenario green] [--no-build] [--keep]

  --packages   comma separated; default every package
               ${LIST_ORDER.join(', ')}
  --scenario   which telemetry to replay; default "green", which holds the flag and the field still
  --no-build   skip the dashboard build, when only the scenario changed
  --keep       leave the emulator running and the VM claimed when this returns
  --warm-laps  laps to let the emulator complete before the first picture; default 2

It takes the captures with \`bun run shots\`, scales each one to ${PREVIEW_HEIGHT} px tall and writes it to
packages/dash/previews/<folder>.png, where the build picks it up. Needs the VM and ffmpeg.
`;

/**
 * The record after a refresh: what was just taken replaces what that package said before, and every
 * other package's entry is left alone.
 *
 * Left alone rather than rewritten, because `bun run previews` is usually pointed at the one face
 * that changed, and a run's provenance is only true of the pictures that run took. This is the one
 * place this differs from the website's sidecar, which carries one provenance for the whole set.
 */
export function mergePreviewRun(existing: PreviewRun, taken: Record<string, PreviewRecord>): PreviewRun {
  const previews = { ...existing.previews, ...taken };
  return { schema: 1, previews: Object.fromEntries(Object.keys(previews).sort().map((k) => [k, previews[k]!])) };
}

/** Scales one capture into the previews directory. Returns what went wrong, or null. */
export function scaleInto(source: string, target: string): string | null {
  const taken = pngSize(new Uint8Array(readFileSync(source)), source);
  const size = previewSize(taken.width, taken.height);
  mkdirSync(path.dirname(target), { recursive: true });
  if (size.width === taken.width && size.height === taken.height) {
    copyFileSync(source, target);
    return null;
  }
  // Quantised, and deliberately. A downscaled dash is gradients where the capture was flat colour,
  // so a truecolour thumbnail compresses to more bytes than the full-size capture it came from --
  // three times more, measured. Two hundred and fifty-six colours at this size is indistinguishable
  // and a fifth of the weight, and these files are committed and then embedded in every release.
  const filter =
    `scale=${size.width}:${size.height}:flags=lanczos,` +
    'split[a][b];[a]palettegen=max_colors=256:stats_mode=single[p];[b][p]paletteuse=dither=none';
  const run = Bun.spawnSync(['ffmpeg', '-loglevel', 'error', '-y', '-i', source, '-vf', filter, '-compression_level', '100', target], {
    stdout: 'pipe',
    stderr: 'pipe',
  });
  if (run.exitCode !== 0) return new TextDecoder().decode(run.stderr).trim() || `ffmpeg exited ${run.exitCode}`;
  return null;
}

export async function previews(opts: Options): Promise<number> {
  if (Bun.spawnSync(['ffmpeg', '-version'], { stdout: 'pipe', stderr: 'pipe' }).exitCode !== 0) {
    console.error('ffmpeg is not on this machine, and the scaling needs it: apt install ffmpeg');
    return 1;
  }
  // Under build/, which is gitignored, and emptied first so that a capture this run failed to take
  // cannot be scaled from the one the last run left behind.
  const rawDir = path.join(repoRoot, 'build/previews');
  rmSync(rawDir, { recursive: true, force: true });

  const code = await shots(resolveHost(), {
    packages: opts.packages,
    scenarios: [opts.scenario],
    outDir: rawDir,
    noBuild: opts.noBuild,
    keep: opts.keep,
    warmLaps: opts.warmLaps,
  });

  // The run `shots` wrote beside the captures: the version, the commit, the tree's cleanliness and
  // the laps SimHub had seen. A thumbnail that cannot say which build it shows is the failure this
  // whole approach is exposed to, so it travels with the pictures.
  const run = readRun(rawDir);

  console.log('');
  let written = 0;
  const taken: Record<string, PreviewRecord> = {};
  for (const [index, name] of opts.packages.entries()) {
    const shot = shotName(index + 1, name, opts.scenario);
    const source = path.join(rawDir, shot);
    const target = path.join(PREVIEWS_DIR, previewFileName(name));
    if (!existsSync(source)) {
      console.log(`  kept     ${previewFileName(name)} (nothing was photographed)`);
      continue;
    }
    const failed = scaleInto(source, target);
    if (failed) {
      console.log(`  failed   ${previewFileName(name)}: ${failed}`);
      continue;
    }
    written += 1;
    const size = pngSize(new Uint8Array(readFileSync(target)), target);
    taken[name] = {
      version: run?.version ?? '',
      commit: run?.commit ?? '',
      dirty: run?.dirty ?? true,
      date: run?.date ?? new Date().toISOString().slice(0, 10),
      scenario: run?.scenario ?? opts.scenario,
      lapsSeen: run?.captures[shot]?.lapsSeen ?? null,
      ...size,
    };
    console.log(`  wrote    ${path.relative(repoRoot, target)}`);
  }

  if (written > 0) {
    const merged = mergePreviewRun(readPreviewRun(), taken);
    const file = path.join(PREVIEWS_DIR, PREVIEW_RUN_FILE);
    writeFileSync(file, `${JSON.stringify(merged, null, 2)}\n`);
    console.log(`  wrote    ${path.relative(repoRoot, file)}`);
  }

  console.log('');
  console.log(`  ${written} of ${opts.packages.length} refreshed; say in the commit message which faces moved`);
  return written === opts.packages.length ? code : 1;
}

if (import.meta.main) {
  const opts = parseArgs(process.argv.slice(2));
  if ('help' in opts) {
    console.log(USAGE);
    process.exit(0);
  }
  process.exit(await previews(opts));
}
