/**
 * What CI builds, read from the workflows rather than restated. CI is where a change is judged, so a
 * plugin job that builds something other than the release, or a tool nothing compiles, is a check
 * that passes over a broken thing (#628).
 */
import { describe, expect, test } from 'bun:test';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, utimesSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { gunzipSync } from 'node:zlib';

const root = join(import.meta.dir, '..');
const read = (path: string) => readFileSync(join(root, path), 'utf8');
const ci = read('.github/workflows/ci.yml');
const release = read('.github/workflows/release.yml');

/** One job of a workflow, from its key to the next job's, without comments or blank lines. */
function job(workflow: string, name: string): string {
  const lines = workflow.split('\n');
  const start = lines.indexOf(`  ${name}:`);
  if (start < 0) return '';
  const end = lines.findIndex((line, i) => i > start && /^ {2}\S/.test(line));
  return lines
    .slice(start, end < 0 ? undefined : end)
    .filter((line) => line.trim() !== '' && !line.trim().startsWith('#'))
    .join('\n');
}

describe('the plugin CI builds', () => {
  test('is the plugin a release builds: the two jobs run the same steps', () => {
    const plugin = job(ci, 'plugin');
    expect(plugin).toContain('dotnet build plugin/OpenDash -c Release');
    expect(plugin).toBe(job(release, 'plugin'));
  });

  test('embeds the LED profiles gzipped, by the one script every packaging path runs', () => {
    for (const [name, workflow] of [['ci.yml', ci], ['release.yml', release]] as const) {
      const plugin = job(workflow, 'plugin');
      const compress = plugin.indexOf('bash plugin/scripts/compress-profiles.sh');
      expect([name, compress > 0]).toEqual([name, true]);
      // Before the tests, so FlagBoxProfile's gzip path is what they exercise.
      expect([name, compress < plugin.indexOf('dotnet test plugin/OpenDash.Tests')]).toEqual([name, true]);
      expect([name, /\bgzip\b/.test(plugin)]).toEqual([name, false]);
    }
    expect(read('scripts/package.sh')).toContain('bash plugin/scripts/compress-profiles.sh');
  });

  test('is zipped by the script, with no hand-rolled zip to fall back on', () => {
    const plugin = job(ci, 'plugin');
    expect(plugin).toContain('bash plugin/scripts/package-plugin.sh');
    expect(plugin).not.toMatch(/\bzip -j\b/);
    expect(plugin).not.toContain('if [ -f plugin/scripts/package-plugin.sh ]');
  });
});

describe('the dash job', () => {
  // A test that fails once and passes on the rerun leaves only what the run kept, and a timeout
  // prints no assertion diff, so the run keeps a report with the failure text in it (#547).
  test('keeps a JUnit report of its tests, uploaded when they fail as when they pass', () => {
    const dash = job(ci, 'dash');
    const outfile = dash.match(/run: bun test --reporter=junit --reporter-outfile="\$RUNNER_TEMP\/([^"]+)"/)?.[1];
    expect(outfile).toBeDefined();
    const upload = dash.slice(dash.indexOf('- name: Upload the test report'));
    expect(upload).toMatch(/^- name: Upload the test report\n\s+if: \$\{\{ !cancelled\(\) \}\}\n\s+uses: actions\/upload-artifact@v4/);
    expect(upload).toContain(`path: \${{ runner.temp }}/${outfile}`);
  });
});

describe('the development tools', () => {
  // What `bun run dev` and `bun run record` build on their way to the VM, and the Linux build of the
  // emulator that runs its selfcheck, which docs/dev-loop.md says CI checks.
  test('are built in Release on every pull request, and the emulator checks itself', () => {
    const tools = job(ci, 'tools');
    expect(tools).toContain('dotnet build tools/irsdk-emulator -c Release');
    expect(tools).toContain('dotnet build tools/irsdk-emulator/devcheck -c Release');
    expect(tools).toContain('dotnet build tools/trace-recorder -c Release');
    expect(tools).toContain('dotnet devcheck/bin/Release/net8.0/IrsdkEmulator.dll --selfcheck flagbox');
    expect(tools).not.toContain('continue-on-error');
  });
});

describe('the site image', () => {
  // The site hands out the zip this image compiles, so it is a release build in all but name, and
  // the toolchain that builds it is held to the pins the other builds read (#915).
  const dockerfile = read('site/Dockerfile');
  const siteImage = read('.github/workflows/site-image.yml');

  /** The Dockerfile's instructions, one to an entry, with comments dropped and continuations joined. */
  const instructions = dockerfile
    .split('\n')
    .filter((line) => !line.trim().startsWith('#'))
    .join('\n')
    .replace(/\\\n/g, ' ')
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line !== '');
  /** Each FROM's image and the stage it names, with the line it was read from. */
  const froms = instructions
    .filter((line) => line.startsWith('FROM '))
    .map((line) => {
      const [, image = '', as, stage] = line.split(/\s+/);
      return { line, image, stage: as?.toUpperCase() === 'AS' ? stage : undefined };
    });
  const stages = froms.flatMap(({ stage }) => (stage === undefined ? [] : [stage]));
  /** Each COPY's sources, its options and destination left out. */
  const copies = instructions
    .filter((line) => line.startsWith('COPY '))
    .map((line) => {
      const words = line.split(/\s+/).slice(1);
      const from = words.find((word) => word.startsWith('--from='))?.slice('--from='.length);
      const paths = words.filter((word) => !word.startsWith('--'));
      return { from, sources: paths.slice(0, -1) };
    });

  test('names the line of every image it uses, and the Debian release of every stage', () => {
    // A tag floats on patches, as the rest of the repository's toolchain does, but never on its line:
    // no `latest`, no bare name, and a stage says which Debian it is, so that the system under the
    // build changes when someone changes the tag rather than when a publisher moves its default. The
    // stages name the same release: the build and runtime stages share a node_modules, and the SDK
    // stage follows them for consistency. A digest is the strongest pin there is and passes both,
    // though the SDK's keeps its tag beside it, because the next test reads the version from the tag.
    const digest = /@sha256:[0-9a-f]{64}$/;
    const images = [
      ...froms.map(({ image }) => image),
      ...copies.flatMap(({ from }) => (from !== undefined && !stages.includes(from) ? [from] : [])),
    ];
    expect(images.length).toBeGreaterThan(0);
    for (const image of images) {
      expect([image, digest.test(image) || /:\d+(\.\d+)*(-[a-z0-9.-]+)?$/.test(image)]).toEqual([image, true]);
    }
    const releases = froms.map(({ image }) => (digest.test(image) ? 'digest' : image.match(/[:-](bullseye|bookworm|trixie)(-|$)/)?.[1]));
    for (const [i, release] of releases.entries()) expect([froms[i]?.image, release]).not.toEqual([froms[i]?.image, undefined]);
    expect(new Set(releases.filter((release) => release !== 'digest')).size).toBeLessThanOrEqual(1);
  });

  test('compiles the plugin with an SDK global.json accepts, and with global.json beside it', () => {
    const artifacts = froms.find(({ image }) => /^mcr\.microsoft\.com\/dotnet\/sdk[:@]/.test(image));
    if (artifacts === undefined) throw new Error('no stage builds from the .NET SDK image');
    // The stage that runs the release's own packaging copies the pin into the root it runs from, so
    // the dotnet inside it reads the same global.json a local build and the runner do.
    const stage = instructions.slice(instructions.indexOf(artifacts.line));
    const next = stage.findIndex((line, i) => i > 0 && line.startsWith('FROM '));
    const body = stage.slice(0, next < 0 ? undefined : next);
    expect(body).toContain('WORKDIR /repo');
    expect(body).toContain('RUN bun run package');
    expect(body.some((line) => line.startsWith('COPY ') && line.split(/\s+/).includes('global.json'))).toBe(true);

    // And the SDK the image carries is one that pin lets through, so the build cannot stop there
    // either. The roll-forward policies are dotnet's own: each widens what may differ from the pin.
    const { sdk } = JSON.parse(read('global.json')) as { sdk: { version: string; rollForward?: string } };
    const parts = (version: string): number[] => {
      const [major = NaN, minor = NaN, patch = NaN] = version.split('.').map(Number);
      return [major, minor, Math.floor(patch / 100), patch % 100];
    };
    const tag = artifacts.image.match(/:(\d+\.\d+(?:\.\d+)?)/)?.[1];
    if (tag === undefined) {
      throw new Error(`${artifacts.image} names no SDK version: keep the tag beside a digest, as in sdk:8.0-bookworm-slim@sha256:...`);
    }
    // A floating tag such as `8.0` names the major and minor and leaves the feature band and patch to
    // the publisher, so those count as unknown: the policy must not fix them, and the pin must ask for
    // no more than the lowest of them, or only an exact tag can show the image satisfies it.
    const named = tag.split('.').length === 2 ? 2 : 4;
    const have = parts(tag).slice(0, named);
    const want = parts(sdk.version);
    const same = { disable: 4, patch: 3, latestPatch: 3, feature: 2, latestFeature: 2, minor: 1, latestMinor: 1, major: 0, latestMajor: 0 }[
      sdk.rollForward ?? 'latestPatch'
    ];
    expect(same).toBeDefined();
    expect(named).toBeGreaterThanOrEqual(same ?? 4);
    expect(have.slice(0, same)).toEqual(want.slice(0, same));
    const floor = [...have, ...want.slice(named).map(() => 0)];
    expect(floor.map((n, i) => n - (want[i] ?? NaN)).find((d) => d !== 0) ?? 0).toBeGreaterThanOrEqual(0);
  });

  test('is rebuilt by CI on every push that changes something it copies', () => {
    const paths = [...siteImage.slice(siteImage.indexOf('    paths:'), siteImage.indexOf('  workflow_dispatch:')).matchAll(/^\s+- "([^"]+)"$/gm)].map(
      (m) => m[1],
    );
    expect(paths.length).toBeGreaterThan(0);
    const sources = copies.filter(({ from }) => from === undefined).flatMap(({ sources }) => sources);
    for (const source of sources) {
      const watched = paths.includes(source) || paths.includes(`${source.replace(/\/$/, '')}/**`);
      expect([source, watched]).toEqual([source, true]);
    }
  });
});

describe('compress-profiles.sh', () => {
  const run = (dir: string) => spawnSync('bash', [join(root, 'plugin', 'scripts', 'compress-profiles.sh'), dir], { encoding: 'utf8' });

  test('gzips every profile in place, keeping its name, and leaves a gzipped one alone', () => {
    const dir = mkdtempSync(join(tmpdir(), 'opendash-profiles-'));
    try {
      const text = '{"Name":"OpenDash 0-10-0"}\n';
      writeFileSync(join(dir, 'OpenDash 0-10-0.ledsprofile'), text);
      writeFileSync(join(dir, 'flag-box-glyphs.json'), '{}');
      expect(run(dir).status).toBe(0);
      const once = readFileSync(join(dir, 'OpenDash 0-10-0.ledsprofile'));
      expect([once[0], once[1]]).toEqual([0x1f, 0x8b]);
      expect(gunzipSync(once).toString('utf8')).toBe(text);
      expect(readFileSync(join(dir, 'flag-box-glyphs.json'), 'utf8')).toBe('{}');
      // A second run, as when package.sh is run twice over one Resources folder, is not a gzip of a gzip.
      expect(run(dir).status).toBe(0);
      expect(readFileSync(join(dir, 'OpenDash 0-10-0.ledsprofile'))).toEqual(once);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  test('gzips a profile to the same bytes whenever it was written, so a rebuild embeds the same file', () => {
    // The build rewrites every profile, so a gzip header with the file's name and mtime in it made
    // every build's assembly a new one (#605).
    const dir = mkdtempSync(join(tmpdir(), 'opendash-profiles-'));
    try {
      const profile = join(dir, 'OpenDash 0-10-0.ledsprofile');
      const gzipped = (mtime: Date) => {
        writeFileSync(profile, '{"Name":"OpenDash 0-10-0"}\n');
        utimesSync(profile, mtime, mtime);
        expect(run(dir).status).toBe(0);
        return readFileSync(profile);
      };
      const first = gzipped(new Date('2026-10-01T12:00:00Z'));
      expect(gzipped(new Date('2026-10-02T12:00:00Z'))).toEqual(first);
      // FLG (byte 3) carries no FNAME, and MTIME (bytes 4..7) is nought.
      expect([first[3], first.readUInt32LE(4)]).toEqual([0, 0]);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  test('refuses a folder with no profile in it, which is a plugin about to ship without its LEDs', () => {
    const dir = mkdtempSync(join(tmpdir(), 'opendash-profiles-'));
    try {
      const r = run(dir);
      expect(r.status).not.toBe(0);
      expect(r.stderr).toContain('no .ledsprofile');
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });
});
