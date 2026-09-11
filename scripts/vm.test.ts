/**
 * The parts of `scripts/vm.ts` that are decidable without a VM: quoting, the CLIXML that
 * PowerShell writes over SSH, how the host is chosen, and when a claim on the VM has gone stale.
 * Everything else in that file is a remote side effect and is proved by running it.
 */
import { describe, expect, test } from 'bun:test';
import { cleanClixml, inputMapping, PRESS, psq, resolveHost, shq, type Claim } from './vm.ts';

describe('quoting', () => {
  test('a shell argument survives a quote in a path', () => {
    expect(shq("/tmp/it's here")).toBe(`'/tmp/it'\\''s here'`);
  });

  test('a shell argument survives a space', () => {
    expect(shq('/opt/winvm/shared/openDash 1280x480.simhubdash')).toBe(`'/opt/winvm/shared/openDash 1280x480.simhubdash'`);
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
