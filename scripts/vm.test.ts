/**
 * The parts of `scripts/vm.ts` that are decidable without a VM: quoting, the CLIXML that
 * PowerShell writes over SSH, how the host is chosen, when a claim on the VM has gone stale and
 * how a run notices that its own claim changed hands, and what a step makes of a guest that says it
 * failed, which is asked of a fake host (`fakeHost.ts`). Everything else in that file is a remote
 * side effect and is proved by running it.
 */
import { describe, expect, test } from 'bun:test';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { withFakeHost, type Answer } from './fakeHost.ts';
import {
  bindPairs,
  claim,
  claimLost,
  cleanClixml,
  inputMapping,
  install,
  parseActivation,
  parseInputSettings,
  PRESS,
  psq,
  release,
  resolveHost,
  shq,
  simhubStart,
  transport,
  whoAmI,
  withBindings,
  withKeyboardReader,
  withoutBindings,
  withPluginActivated,
  type Claim,
  type PluginActivation,
} from './vm.ts';

describe('quoting', () => {
  test('a shell argument survives a quote in a path', () => {
    expect(shq("/tmp/it's here")).toBe(`'/tmp/it'\\''s here'`);
  });

  test('a shell argument survives a space', () => {
    expect(shq('/opt/winvm/shared/OpenDash 1280x480.simhubdash')).toBe(`'/opt/winvm/shared/OpenDash 1280x480.simhubdash'`);
  });

  test('a PowerShell string doubles its quotes', () => {
    expect(psq("C:\\it's\\here")).toBe("'C:\\it''s\\here'");
  });

  test('a Windows path keeps its backslashes', () => {
    expect(psq('C:\\Program Files (x86)\\SimHub')).toBe("'C:\\Program Files (x86)\\SimHub'");
  });
});

describe('the CLIXML PowerShell writes on stderr', () => {
  test('plain stderr is left alone', () => {
    expect(cleanClixml('  ssh: connect failed  ')).toBe('ssh: connect failed');
  });

  test('a progress record alone produces nothing', () => {
    const progress = '#< CLIXML\r\n<Objs Version="1.1.0.1" xmlns="http://schemas.microsoft.com/powershell/2004/04"><Obj S="progress" RefId="0"><TN RefId="0"><T>System.Management.Automation.PSCustomObject</T></TN></Obj></Objs>';
    expect(cleanClixml(progress)).toBe('');
  });

  test('an error record is unescaped and its line breaks restored', () => {
    const clixml = '#< CLIXML\r\n<Objs Version="1.1.0.1"><S S="Error">Remove-Item : Cannot find path_x000D__x000A_ &lt;C:\\nope&gt;</S></Objs>';
    expect(cleanClixml(clixml)).toBe('Remove-Item : Cannot find path\n <C:\\nope>');
  });

  test('several error records are joined', () => {
    const clixml = '#< CLIXML\r\n<Objs><S S="Error">first</S><S S="Error">second</S></Objs>';
    expect(cleanClixml(clixml)).toBe('first\nsecond');
  });
});

describe('choosing the host', () => {
  test('a machine without /opt/winvm tunnels to the configured host', () => {
    const before = process.env.OPENDASH_VM_HOST;
    process.env.OPENDASH_VM_HOST = 'elsewhere';
    try {
      const host = resolveHost();
      // The test machine is whichever one runs the suite, so only the remote branch is asserted.
      if (!host.local) expect(host.name).toBe('root@elsewhere');
    } finally {
      if (before === undefined) delete process.env.OPENDASH_VM_HOST;
      else process.env.OPENDASH_VM_HOST = before;
    }
  });

  test('the user can be overridden as well', () => {
    const beforeHost = process.env.OPENDASH_VM_HOST;
    const beforeUser = process.env.OPENDASH_VM_USER;
    process.env.OPENDASH_VM_HOST = 'elsewhere';
    process.env.OPENDASH_VM_USER = 'tester';
    try {
      const host = resolveHost();
      if (!host.local) expect(host.name).toBe('tester@elsewhere');
    } finally {
      if (beforeHost === undefined) delete process.env.OPENDASH_VM_HOST;
      else process.env.OPENDASH_VM_HOST = beforeHost;
      if (beforeUser === undefined) delete process.env.OPENDASH_VM_USER;
      else process.env.OPENDASH_VM_USER = beforeUser;
    }
  });
});

/**
 * `readClaim` reads the lock through the host, so the staleness rule is restated here against the
 * same threshold: a session that dies never releases its claim, and the VM would be locked for
 * good if an old one counted.
 */
describe('when a claim on the VM has gone stale', () => {
  const STALE_MINUTES = 90;
  const isStale = (claim: Claim, now: Date): boolean => {
    const age = (now.getTime() - Date.parse(claim.since)) / 60_000;
    return !Number.isFinite(age) || age > STALE_MINUTES;
  };
  const now = new Date('2026-09-11T12:00:00.000Z');

  test('a claim made a minute ago holds', () => {
    expect(isStale({ who: 'a', since: '2026-09-11T11:59:00.000Z', note: '' }, now)).toBe(false);
  });

  test('a claim just under the threshold holds', () => {
    expect(isStale({ who: 'a', since: '2026-09-11T10:31:00.000Z', note: '' }, now)).toBe(false);
  });

  test('a claim older than the threshold is abandoned', () => {
    expect(isStale({ who: 'a', since: '2026-09-11T10:00:00.000Z', note: '' }, now)).toBe(true);
  });

  test('an unparseable date is abandoned rather than holding the VM for ever', () => {
    expect(isStale({ who: 'a', since: 'not a date', note: '' }, now)).toBe(true);
  });
});

/**
 * `claimLost` is what turns #218 into a sentence: the opener clicks over VNC, so a second session
 * taking the lock mid-run moves the mouse under it, and the failure has to name that rather than
 * the coordinates it was aiming at.
 */
describe('when the claim changes hands under a run', () => {
  const mine = '2026-09-13T13:39:00.000Z';
  const me = 'tim@laptop';
  const now = new Date('2026-09-13T13:48:30.000Z');

  test('our own lock, untouched, says nothing', () => {
    expect(claimLost({ who: me, since: mine, note: 'shots 5x1' }, mine, me, now)).toBeNull();
  });

  test('somebody else holding it is named, with the note they left', () => {
    const held: Claim = { who: 'root@cumulus', since: '2026-09-13T13:48:09.000Z', note: '#124 binding probe' };
    const why = claimLost(held, mine, me, now);
    expect(why).toContain('root@cumulus');
    expect(why).toContain('#124 binding probe');
  });

  test('our own name over a different claim is still not our claim: re-claiming loses the run', () => {
    // Same machine, second session. The lock says us, but the run that wrote this one no longer
    // holds it, and the other session is driving the same guest.
    expect(claimLost({ who: me, since: '2026-09-13T13:48:09.000Z', note: 'dev' }, mine, me, now)).not.toBeNull();
  });

  test('a lock that is simply gone is reported, since nothing was guarding the guest', () => {
    expect(claimLost(null, mine, me, now)).toContain('gone');
  });

  test('a run that outlives the staleness threshold is not told its claim was taken', () => {
    // Its own claim expired rather than being taken, and a batch of twenty faces can last that
    // long; saying somebody took the VM would send the reader after a session that never existed.
    expect(claimLost(null, '2026-09-13T11:00:00.000Z', me, now)).toBeNull();
  });
});

/**
 * Who a session is, for the lock. One machine runs several sessions at once, a worktree each, so the
 * machine and the user are not enough; the checkout is what tells them apart. Not the process: a
 * session claims in one command and works in the next (`bun run vm claim && bun run dev`), and
 * releases from a third, and all three have to be the same owner.
 */
describe('who holds the VM', () => {
  const env = { USER: 'root', HOSTNAME: 'cumulus', HOME: '/root' };

  test('two checkouts on one machine are two sessions', () => {
    const a = whoAmI(env, '/root/dev/OpenDash/.claude/worktrees/agent-a');
    const b = whoAmI(env, '/root/dev/OpenDash/.claude/worktrees/agent-b');
    expect(a).not.toBe(b);
    expect(a.startsWith('root@cumulus')).toBe(true);
  });

  test('the same checkout is the same session, whichever process asks', () => {
    expect(whoAmI(env, '/root/dev/OpenDash')).toBe(whoAmI({ ...env }, '/root/dev/OpenDash'));
  });

  test('says where the checkout is, so a reader can go and find the session', () => {
    expect(whoAmI(env, '/root/dev/OpenDash')).toBe('root@cumulus:~/dev/OpenDash');
    expect(whoAmI({ USER: 'tim', HOSTNAME: 'laptop', HOME: '/Users/tim' }, '/opt/src/OpenDash')).toBe('tim@laptop:/opt/src/OpenDash');
  });

  test('OPENDASH_VM_WHO still overrides it', () => {
    expect(whoAmI({ ...env, OPENDASH_VM_WHO: 'root@cumulus-633' }, '/root/dev/OpenDash')).toBe('root@cumulus-633');
  });
});

/**
 * The lock itself, against a host whose share is a temporary directory. The race is the one two
 * sessions starting together run into: both read the lock free, and both used to write it.
 */
describe('the claim on the VM', () => {
  const now = new Date('2026-10-03T08:00:00.000Z');
  const lockIn = (share: string) => path.join(share, 'vm.lock');
  const write = (share: string, c: Claim) => writeFileSync(lockIn(share), `${JSON.stringify(c)}\n`);
  const read = (share: string): Claim | null => (existsSync(lockIn(share)) ? (JSON.parse(readFileSync(lockIn(share), 'utf8')) as Claim) : null);
  const theirs: Claim = { who: 'root@cumulus:~/dev/OpenDash/.claude/worktrees/agent-b', since: '2026-10-03T07:59:00.000Z', note: '#218 shots' };

  /** Runs `body` as the session `who`, on a fake host with no guest behind it. */
  function as<T>(who: string, body: (host: Parameters<typeof claim>[0], share: string) => T): T {
    const before = process.env.OPENDASH_VM_WHO;
    process.env.OPENDASH_VM_WHO = who;
    try {
      return withFakeHost(() => ({}), (host, _calls, share) => body(host, share));
    } finally {
      if (before === undefined) delete process.env.OPENDASH_VM_WHO;
      else process.env.OPENDASH_VM_WHO = before;
    }
  }

  /** Lets `step` run on the host just after the first command that reads the lock, as a second session would. */
  function afterFirstRead(step: () => void): void {
    const run = transport.run;
    let done = false;
    transport.run = (argv, timeoutMs) => {
      const r = run(argv, timeoutMs);
      if (!done && /^cat /.test(argv[argv.length - 1]!)) {
        done = true;
        step();
      }
      return r;
    };
  }

  test('a free VM is taken, and the lock names who took it', () => {
    as('me', (host, share) => {
      expect(claim(host, '#633', now)).toMatchObject({ ok: true, stdout: 'claimed by me' });
      expect(read(share)).toEqual({ who: 'me', since: now.toISOString(), note: '#633' });
    });
  });

  test('a fresh claim by another session is refused and left as it was', () => {
    as('me', (host, share) => {
      write(share, theirs);
      const r = claim(host, '#633', now);
      expect(r.ok).toBe(false);
      expect(r.stderr).toContain(theirs.who);
      expect(read(share)).toEqual(theirs);
    });
  });

  test('a claim older than ninety minutes is abandoned, and taken over', () => {
    as('me', (host, share) => {
      write(share, { ...theirs, since: '2026-10-03T06:29:00.000Z' });
      expect(claim(host, '#633', now).ok).toBe(true);
      expect(read(share)?.who).toBe('me');
    });
  });

  test('our own claim is taken again, so a claim can be chained into a command that claims', () => {
    as('me', (host, share) => {
      write(share, { who: 'me', since: '2026-10-03T07:30:00.000Z', note: 'vm claim' });
      expect(claim(host, 'dev', now).ok).toBe(true);
      expect(read(share)).toEqual({ who: 'me', since: now.toISOString(), note: 'dev' });
    });
  });

  test('a session that takes the lock between our read and our write keeps it, and we are refused', () => {
    as('me', (host, share) => {
      afterFirstRead(() => write(share, theirs));
      const r = claim(host, '#633', now);
      expect(r.ok).toBe(false);
      expect(r.stderr).toContain(theirs.who);
      expect(read(share)).toEqual(theirs);
    });
  });

  test('release gives back our own claim', () => {
    as('me', (host, share) => {
      write(share, { who: 'me', since: '2026-10-03T07:30:00.000Z', note: '' });
      expect(release(host, now)).toMatchObject({ ok: true, stdout: 'released' });
      expect(read(share)).toBeNull();
    });
  });

  test("release refuses another session's claim and leaves it", () => {
    as('me', (host, share) => {
      write(share, theirs);
      expect(release(host, now).ok).toBe(false);
      expect(read(share)).toEqual(theirs);
    });
  });

  test('release does not delete a claim another session took after ours went stale', () => {
    as('me', (host, share) => {
      write(share, { who: 'me', since: '2026-10-03T06:00:00.000Z', note: '' });
      afterFirstRead(() => write(share, theirs));
      expect(release(host, now).ok).toBe(false);
      expect(read(share)).toEqual(theirs);
    });
  });
});

describe('binding a key to a SimHub action', () => {
  test('the mapping is what SimHub writes when the dialog is used', () => {
    // Read back from PluginsData/PluginManagerSettings.json after binding zone C by hand, which is
    // the only way to know the shape: nothing documents it.
    expect(inputMapping('OpenDash.CycleZoneC', 'F9')).toEqual({
      Target: 'OpenDash.CycleZoneC',
      Trigger: 'KeyboardReaderPlugin.F9',
      PressType: 4,
      GameRestriction: { SupportedGames: [] },
    });
  });

  test('a key is upper-cased, because that is how the keyboard reader names its triggers', () => {
    expect(inputMapping('OpenDash.HoldQuickGlance', 'f8').Trigger).toBe('KeyboardReaderPlugin.F8');
  });

  test('an action that is held is bound as During, which is the only type that can hold', () => {
    // TriggerInputPress calls an action's start only for During mappings; every other type goes
    // through TriggerAction, which fires start and end back to back. A glance bound any other way
    // appears and vanishes in the same frame, which is what the first attempt on the VM did.
    expect(inputMapping('OpenDash.HoldQuickGlance', 'F8').PressType).toBe(PRESS.during);
    // The screen's namespace sits between the dot and the verb since the glance went per face.
    expect(inputMapping('OpenDash.RimHoldQuickGlance', 'F8').PressType).toBe(PRESS.during);
    expect(inputMapping('OpenDash.Face1280x480HoldQuickGlance', 'F8').PressType).toBe(PRESS.during);
    expect(inputMapping('OpenDash.CycleZoneB', 'F7').PressType).toBe(PRESS.shortAndLong);
    // Even when the caller asks for something else: an action named Hold has a release.
    expect(inputMapping('OpenDash.HoldQuickGlance', 'F8', PRESS.shortAndLong).PressType).toBe(PRESS.during);
  });

  test('an action name without a plugin prefix is refused', () => {
    // SimHub names an action pluginType.Name + "." + action, so a bare name binds nothing and says
    // nothing about it.
    expect(() => inputMapping('CycleZoneC', 'F9')).toThrow();
    expect(() => inputMapping('OpenDash.CycleZoneC', 'F 9')).toThrow();
    expect(() => inputMapping('OpenDash.CycleZoneC', '')).toThrow();
  });
});


describe("SimHub's record of which plugins are enabled", () => {
  const entries: PluginActivation[] = [
    { ClassName: 'SimHub.Plugins.Motion.MotionPlugin', IsEnabled: false, ShowInMainMenu: true, ShowInMainMenuPosition: 0 },
    { ClassName: 'OpenDashPlugin.OpenDash', IsEnabled: false, ShowInMainMenu: true, ShowInMainMenuPosition: 0 },
  ];

  test('a plugin SimHub already knows is enabled in place, with the rest left alone', () => {
    expect(withPluginActivated(entries, 'OpenDashPlugin.OpenDash')).toEqual([
      entries[0]!,
      { ClassName: 'OpenDashPlugin.OpenDash', IsEnabled: true, ShowInMainMenu: true, ShowInMainMenuPosition: 0 },
    ]);
  });

  test('a plugin it has never seen is appended, enabled, and out of the left menu unless asked for', () => {
    expect(withPluginActivated(entries, 'OpenDashTraceRecorder.TraceRecorderPlugin')[2]).toEqual({
      ClassName: 'OpenDashTraceRecorder.TraceRecorderPlugin',
      IsEnabled: true,
      ShowInMainMenu: false,
      ShowInMainMenuPosition: 0,
    });
  });

  test('the file SimHub writes reads back as itself, byte order mark included', () => {
    const text = `\uFEFF${JSON.stringify(entries)}`;
    expect(parseActivation(text)).toEqual(entries);
  });

  test('anything but a flat array of plugins is refused', () => {
    // PowerShell 5.1 turns the array into this when it is handed to ConvertTo-Json, and SimHub then
    // dies at startup on a plugin with no class name. Refusing it here is what keeps that shape
    // from being written back a second time.
    expect(() => parseActivation(JSON.stringify({ value: [entries], Count: 1 }))).toThrow(/not an array/);
    expect(() => parseActivation(JSON.stringify([{ IsEnabled: true }]))).toThrow(/names no plugin class/);
  });
});

describe('the menu entry a capture clicks', () => {
  const hidden: PluginActivation[] = [{ ClassName: 'OpenDashPlugin.OpenDash', IsEnabled: false, ShowInMainMenu: false, ShowInMainMenuPosition: 3 }];

  test('asking for it puts a plugin SimHub already knows into the left menu, where it keeps its place', () => {
    expect(withPluginActivated(hidden, 'OpenDashPlugin.OpenDash', true)).toEqual([
      { ClassName: 'OpenDashPlugin.OpenDash', IsEnabled: true, ShowInMainMenu: true, ShowInMainMenuPosition: 3 },
    ]);
  });

  test('not asking for it never takes one out', () => {
    const shown = [{ ...hidden[0]!, ShowInMainMenu: true }];
    expect(withPluginActivated(shown, 'OpenDashPlugin.OpenDash')[0]!.ShowInMainMenu).toBe(true);
    expect(withPluginActivated(hidden, 'OpenDashPlugin.OpenDash')[0]!.ShowInMainMenu).toBe(false);
  });
});

describe('`bun run vm bind` on the command line', () => {
  test('a bare action is OpenDash', () => {
    expect(bindPairs(['CycleZoneC', 'F9'])).toEqual([{ action: 'OpenDash.CycleZoneC', key: 'F9' }]);
  });

  test('a named plugin is kept, and pairs repeat', () => {
    expect(bindPairs(['OpenDash.HoldQuickGlance', 'F8', 'Rim.CycleZoneB', 'F7'])).toEqual([
      { action: 'OpenDash.HoldQuickGlance', key: 'F8' },
      { action: 'Rim.CycleZoneB', key: 'F7' },
    ]);
  });

  test('an action without its key is refused rather than half bound', () => {
    expect(bindPairs([])).toBeNull();
    expect(bindPairs(['CycleZoneC'])).toBeNull();
    expect(bindPairs(['CycleZoneC', 'F9', 'CycleZoneB'])).toBeNull();
  });
});

describe("SimHub's record of what each input does", () => {
  // The shape of PluginsData/PluginManagerSettings.json on the VM, read off the #475 backup on the
  // share: two event tables carried through untouched, and the bindings, a serial dash's among them.
  const file = {
    EventMessageSettings: { 'Pit limiter': { Duration: 5, Enabled: false } },
    EventActionMapping: [{ GameRestriction: { SupportedGames: [] }, Target: 'SerialDashPlugin.DisplayScreenFor1s_RPMStartChanged', Trigger: 'SerialDashPlugin.RPMStartOffsetChanged' }],
    InputActionMapping: [
      { Target: 'SerialDashPlugin.NextScreen', PressType: 1, GameRestriction: { SupportedGames: [] }, Trigger: 'SerialDashPlugin.SCREEN1_BUTTON2' },
      { Target: 'OpenDash.CycleZoneB', PressType: 4, GameRestriction: { SupportedGames: [] }, Trigger: 'KeyboardReaderPlugin.F7' },
      { Target: 'OpenDash.CycleZoneC', PressType: 4, GameRestriction: { SupportedGames: [] }, Trigger: 'SomeWheel.Button12' },
    ],
  };
  const text = `﻿${JSON.stringify(file, null, 2)}`;

  test('reads back as itself, byte order mark included', () => {
    expect(parseInputSettings(text)).toEqual(file);
  });

  test('a file with no bindings yet reads as an empty list', () => {
    expect(parseInputSettings('{}').InputActionMapping).toEqual([]);
    expect(parseInputSettings('{"InputActionMapping":null}').InputActionMapping).toEqual([]);
  });

  test('anything but the object SimHub writes is refused', () => {
    expect(() => parseInputSettings('[]')).toThrow(/not an object/);
    expect(() => parseInputSettings('{"InputActionMapping":{"value":[],"Count":0}}')).toThrow(/not a list/);
    expect(() => parseInputSettings('{"InputActionMapping":[{"Trigger":"x"}]}')).toThrow(/names no target/);
  });

  test('binding replaces what the action was bound to and leaves the rest where it was', () => {
    const bound = withBindings(parseInputSettings(text), [inputMapping('OpenDash.CycleZoneB', 'F9'), inputMapping('OpenDash.HoldQuickGlance', 'F8')]);
    expect(bound.EventMessageSettings).toEqual(file.EventMessageSettings);
    expect(bound.EventActionMapping).toEqual(file.EventActionMapping);
    expect(bound.InputActionMapping.map((m) => `${m.Target}<-${m.Trigger}/${m.PressType}`)).toEqual([
      'SerialDashPlugin.NextScreen<-SerialDashPlugin.SCREEN1_BUTTON2/1',
      'OpenDash.CycleZoneC<-SomeWheel.Button12/4',
      'OpenDash.CycleZoneB<-KeyboardReaderPlugin.F9/4',
      'OpenDash.HoldQuickGlance<-KeyboardReaderPlugin.F8/3',
    ]);
  });

  test('binding twice leaves one binding per action', () => {
    const once = withBindings(parseInputSettings(text), [inputMapping('OpenDash.CycleZoneB', 'F9')]);
    expect(withBindings(once, [inputMapping('OpenDash.CycleZoneB', 'F9')])).toEqual(once);
  });

  test("unbinding takes the plugin's key bindings and leaves a wheel button somebody bound by hand", () => {
    const { settings, removed } = withoutBindings(withBindings(parseInputSettings(text), [inputMapping('OpenDash.HoldQuickGlance', 'F8')]), 'OpenDash');
    expect(removed).toBe(2);
    expect(settings.InputActionMapping.map((m) => m.Target)).toEqual(['SerialDashPlugin.NextScreen', 'OpenDash.CycleZoneC']);
    expect(settings.EventActionMapping).toEqual(file.EventActionMapping);
  });

  test('the keyboard reader is switched on in place, or added when SimHub has never listed it', () => {
    const off: PluginActivation[] = [{ ClassName: 'SimHub.Plugins.InputPlugins.KeyboardReaderPlugin', IsEnabled: false, ShowInMainMenu: false, ShowInMainMenuPosition: 0 }];
    expect(withKeyboardReader(off)).toEqual([{ ...off[0]!, IsEnabled: true }]);
    expect(withKeyboardReader([])).toEqual([{ ...off[0]!, IsEnabled: true }]);
  });
});

describe('a SimHub that does not start', () => {
  // What the guest printed, word for word, when SimHub was kept from starting on the VM (#629).
  const NEVER_APPEARED = 'SimHub did not appear; check a screenshot';
  const isStart = (script: string) => script.includes("Start-ScheduledTask -TaskName 'SimHub'");
  const isStop = (script: string) => script.includes('Stop-Process');
  const isExpand = (script: string) => script.includes('ExtractToDirectory');

  /** A build/ holding one package, which is all `install` reads of it before the guest takes over. */
  function withBuild<T>(body: (dir: string) => T): T {
    const dir = mkdtempSync(path.join(os.tmpdir(), 'opendash-build-'));
    writeFileSync(path.join(dir, 'OpenDash Test.simhubdash'), 'not a real package');
    try {
      return body(dir);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  }

  test.each([0, 1])('is a failed start, whatever the guest exits with (%i)', (status) => {
    const r = withFakeHost(
      (script) => (isStart(script) ? { status, stdout: NEVER_APPEARED } : {}),
      (host) => simhubStart(host),
    );
    expect(r.ok).toBe(false);
    expect(r.stderr).toContain('SimHub did not start');
  });

  test('a start the guest saw is a success, and so is one already running', () => {
    for (const stdout of ['started (pid 4242)', 'already running']) {
      const r = withFakeHost((script) => (isStart(script) ? { stdout } : {}), (host) => simhubStart(host));
      expect(r).toMatchObject({ ok: true, stdout });
    }
  });

  test('fails `vm install`, after saying what it installed', () => {
    const guest = (script: string): Answer => {
      if (isStop(script)) return { stdout: 'stopped' };
      if (isExpand(script)) return { stdout: 'installed: OpenDash Test' };
      if (isStart(script)) return { stdout: NEVER_APPEARED };
      return {};
    };
    const r = withBuild((dir) => withFakeHost(guest, (host) => install(host, ['OpenDash Test'], dir)));
    expect(r.ok).toBe(false);
    expect(r.stdout).toContain('installed: OpenDash Test');
    expect(r.stderr).toContain('SimHub did not start');
  });

  test('a stop that fails ends `vm install` before anything is expanded under a running SimHub', () => {
    const guest = (script: string): Answer => (isStop(script) ? { status: 1, stdout: 'SimHub is still running (pid 4242)' } : { stdout: 'ok' });
    const { r, expanded } = withBuild((dir) =>
      withFakeHost(guest, (host, calls) => ({ r: install(host, ['OpenDash Test'], dir), expanded: calls.some((c) => c.script !== null && isExpand(c.script)) })),
    );
    expect(r.ok).toBe(false);
    expect(expanded).toBe(false);
  });
});
