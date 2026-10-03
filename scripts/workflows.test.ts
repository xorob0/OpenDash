/**
 * What CI builds, read from the workflows rather than restated. CI is where a change is judged, so a
 * plugin job that builds something other than the release, or a tool nothing compiles, is a check
 * that passes over a broken thing (#628).
 */
import { describe, expect, test } from 'bun:test';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
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
