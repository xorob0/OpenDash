/**
 * sims: the sims the development loop can play, and everything that differs between them.
 *
 * SimHub reads one game at a time, chosen in its window, and each game has its own reader with its own
 * shared memory, its own raw property names and its own way of saying which frame is which. So each sim
 * here has an emulator of its own under `tools/`, a game code SimHub is switched to before it starts, a
 * property its frames are keyed on, and a folder of traces of its own. Everything else in the loop, the
 * claim, the install, the clicking and the recorder, is the same for both and takes a `Sim`.
 *
 * iRacing is the default everywhere, because it is what every scenario, trace and capture was made
 * with before Assetto Corsa joined (#579), and a command that names no sim keeps meaning what it meant.
 */
import path from 'node:path';

const repoRoot = path.resolve(import.meta.dir, '..');

export type SimId = 'iracing' | 'ac';

export interface Sim {
  id: SimId;
  /** For messages. */
  label: string;
  /**
   * SimHub's code for the game, which is `DataCorePlugin.CurrentGame` while it is selected and what
   * `ContextSimhubSettings.json` names as `LastGame`.
   */
  game: string;
  /** The emulator's source, relative to the repository. */
  sourceDir: string;
  /** What `dotnet build` leaves in `bin/Release/net48`. */
  exe: string;
  /** The process name Windows lists it under, which is how a running one is found and stopped. */
  process: string;
  /** Where it runs on the guest. Local disk, not the share: it is read sixty times a second. */
  guestDir: string;
  /** Its folder on the share, on the way there. */
  shareDir: string;
  /** The folders that travel beside the exe: the scenarios, and for AC the fake install SimHub reads. */
  folders: readonly string[];
  /** The first words of a run's banner in the emulator's log, after which its laps are counted. */
  banner: string;
  /** The scenario a command that names none plays. */
  defaultScenario: string;
  /**
   * The raw property the emulator steps once a tick, which the trace recorder keys its frames on and
   * every trace carries as a column, so that a re-recording can be compared frame for frame.
   */
  tickProperty: string;
  /**
   * The raw blocks this sim's reader publishes under `DataCorePlugin.GameRawData.`. A trace of another
   * sim must hold every one of them null, which is what tells a mislabelled recording apart.
   */
  rawPrefixes: readonly string[];
  /** Where its committed traces live, relative to `traces/`: iRacing's at the top, as they always were. */
  traceSubdir: string;
  /**
   * Scenarios with no committed trace, each with the reason, so that adding one without a trace stays a
   * decision somebody made. See `UNTRACED_SCENARIOS` in `scripts/emulator.ts` for iRacing's.
   */
  untraced: readonly string[];
}

/** `DataCorePlugin.CurrentGame`: the game SimHub was reading, recorded in every trace beside the tick. */
export const GAME_PROPERTY = 'DataCorePlugin.CurrentGame';

export const IRACING: Sim = {
  id: 'iracing',
  label: 'iRacing',
  game: 'IRacing',
  sourceDir: 'tools/irsdk-emulator',
  exe: 'IrsdkEmulator.exe',
  process: 'IrsdkEmulator',
  guestDir: 'C:\\Temp\\irsdk-emulator',
  shareDir: 'irsdk-emulator',
  folders: ['scenarios'],
  banner: 'IrsdkEmulator - scenario',
  defaultScenario: 'race',
  tickProperty: 'DataCorePlugin.GameRawData.Telemetry.SessionTick',
  rawPrefixes: ['DataCorePlugin.GameRawData.Telemetry.', 'DataCorePlugin.GameRawData.SessionData.'],
  traceSubdir: '',
  untraced: [
    // flagbox drives lights, not a dashboard.
    'flagbox',
    // alerts walks the alert catalogue over 220 seconds, one condition every seven, and is watched on
    // the VM rather than replayed: a trace holds twenty seconds, which is three of its states (#762).
    'alerts',
    // gallery exists to be photographed for the website, and clip to be filmed; nothing replays them.
    'gallery',
    'clip',
    // Added for the #406 and #387 VM pass by a branch that could not claim the VM: the game running
    // with no session named, and the lap-counted race that publishes no clock. Both are worth
    // recording — a trace of either would let a state nothing else reaches be replayed headlessly.
    'nosession',
    'untimed',
    // cars changes the player's car for SimHub's per-car playlists (#199), whose switch is a device's and is
    // watched on the VM; a twenty-second trace would hold no car change at all.
    'cars',
    // steerleft holds the wheel a quarter turn to the left so the steering dial's direction can be
    // seen (#1057); it is looked at on the VM, and its trace would be green's with one value held.
    'steerleft',
    // oilpressure sets the oil-pressure bit with the engine stopped and then turning, ten seconds each,
    // so that the engine warnings can be seen to wait for the crank (#1115); it is looked at on the VM,
    // and both of its states are pinned in engineWarnings.test.ts already.
    'oilpressure',
  ],
};

export const ASSETTO_CORSA: Sim = {
  id: 'ac',
  label: 'Assetto Corsa',
  game: 'AssettoCorsa',
  sourceDir: 'tools/acsm-emulator',
  // SimHub's AC reader looks for a process named acs and takes its folder as the game's install, so the
  // emulator is acs.exe and carries content\ beside it (tools/acsm-emulator/README.md).
  exe: 'acs.exe',
  process: 'acs',
  guestDir: 'C:\\Temp\\acsm-emulator',
  shareDir: 'acsm-emulator',
  folders: ['scenarios', 'content'],
  banner: 'AcsmEmulator - scenario',
  defaultScenario: 'race',
  tickProperty: 'DataCorePlugin.GameRawData.Graphics.PacketId',
  rawPrefixes: ['DataCorePlugin.GameRawData.Physics.', 'DataCorePlugin.GameRawData.Graphics.', 'DataCorePlugin.GameRawData.StaticInfo.'],
  traceSubdir: 'ac',
  untraced: [
    // shiftlights swaps the car at forty seconds to show the lights following it, which is watched on
    // the VM against a strip; a twenty-second trace would hold the Porsche's sweep and no swap.
    'shiftlights',
    // Added by #579 on a branch that could not claim the VM. Recording them is the VM check of that
    // ticket: `bun run record --sim ac race green yellow pit quali notc noapp`, then each name comes
    // off this list in the same commit as its trace.
    'race',
    'green',
    'yellow',
    'pit',
    'quali',
    'notc',
    'noapp',
  ],
};

export const SIMS: readonly Sim[] = [IRACING, ASSETTO_CORSA];

/** The sim a `--sim` value names, by its id or its SimHub game code, in any case. */
export function simNamed(name: string): Sim | undefined {
  const wanted = name.trim().toLowerCase();
  return SIMS.find((s) => s.id === wanted || s.game.toLowerCase() === wanted);
}

/**
 * The `--sim` of a command line, iRacing when there is none. An unknown name is an error rather than
 * a fallback, since a run that quietly played iRacing when Assetto Corsa was asked for would look
 * like a pass.
 */
export function simFromArgs(argv: readonly string[]): Sim {
  const index = argv.indexOf('--sim');
  const given = argv.find((a) => a.startsWith('--sim='));
  if (index < 0 && given === undefined) return IRACING;
  const raw = index >= 0 ? (argv[index + 1] ?? '') : given!.slice('--sim='.length);
  const sim = raw.startsWith('--') ? undefined : simNamed(raw);
  if (!sim) throw new Error(`--sim ${raw}: one of ${SIMS.map((s) => s.id).join(', ')}`);
  return sim;
}

/** The emulator's source folder, absolute. */
export const sourceDir = (sim: Sim): string => path.join(repoRoot, sim.sourceDir);
