/**
 * What `scripts/gui.ts` decides without a VM: whether the guest's display is in the mode its
 * coordinates were measured in, where in SimHub's window each click lands, and what a failed open
 * blames. The clicking itself is a remote side effect and is proved by running it.
 */
import { describe, expect, test } from 'bun:test';
import { aimAt, EXPECTED_SCREEN, openFailure, parseClientArea, screenModeProblem } from './gui.ts';

describe('the display mode the coordinates need', () => {
  test('the expected mode is the one the file documents and the VM is set up with', () => {
    expect(EXPECTED_SCREEN).toEqual({ width: 3840, height: 2160 });
  });

  test('that mode is no problem at all', () => {
    expect(screenModeProblem({ ...EXPECTED_SCREEN })).toBeNull();
  });

  test('the mode a restarted container comes back at is refused, by name', () => {
    const problem = screenModeProblem({ width: 1280, height: 800 });
    expect(problem).toContain('1280x800');
    expect(problem).toContain('3840x2160');
  });

  test('the refusal says what to run, since naming the mode alone leaves the reader stuck', () => {
    const problem = screenModeProblem({ width: 2560, height: 1600 }) ?? '';
    expect(problem).toContain('/opt/winvm/shared/setres.ps1');
    expect(problem).toContain('-Width 3840 -Height 2160');
    // The mode belongs to the interactive session, so a command run over SSH changes nothing.
    expect(problem).toContain('interactive session');
  });

  test('a mode that is only wrong in one dimension is still wrong', () => {
    expect(screenModeProblem({ width: EXPECTED_SCREEN.width, height: 1600 })).not.toBeNull();
    expect(screenModeProblem({ width: 2560, height: EXPECTED_SCREEN.height })).not.toBeNull();
  });
});

/**
 * #303: SimHub exiting under the clicks and the clicks missing both end in a dashboard that did not
 * open, and the advice for one is useless for the other.
 */
describe('what a failed open blames', () => {
  test('a SimHub that is gone is named on the first line, which is all shots keeps', () => {
    const [first] = openFailure('OpenDash 850x480', false).split('\n');
    expect(first).toContain('OpenDash 850x480');
    expect(first).toContain('SimHub exited');
  });

  test('a SimHub that is gone sends the reader to its log, not to Dash Studio', () => {
    const message = openFailure('OpenDash 850x480', false);
    expect(message).toContain('bun run vm logs');
    expect(message).not.toContain('Open it by hand');
  });

  test('a SimHub still running leaves the clicking as the thing to look at', () => {
    const message = openFailure('OpenDash 850x480', true);
    expect(message).toContain('after two attempts');
    expect(message).toContain('Open it by hand');
    expect(message).not.toContain('exited');
  });

  test('a SimHub that could not be asked about is not declared dead', () => {
    expect(openFailure('OpenDash 850x480', null)).toBe(openFailure('OpenDash 850x480', true));
  });
});

/**
 * #303: the clicks are measured from SimHub's client area rather than from the display. On the rig
 * the maximised client area is the working area, so every click has to land exactly where it did
 * when the coordinates were measured against the screen.
 */
describe("where the clicks land in SimHub's window", () => {
  const maximised = { left: 0, top: 0, width: 3840, height: 2120 };

  test('the client area is read from what the maximise prints', () => {
    expect(parseClientArea('client 0,0 3840x2120 (unclipped -8,-8 3856x2136)')).toEqual(maximised);
  });

  test('a maximise that never arrived has no client area to aim at', () => {
    expect(parseClientArea('SimHub is not running after 120s')).toBeNull();
    expect(
      parseClientArea("SimHub's window is 1300x760 at 78,0, which does not cover the 3840x2120 working area; still starting after 120s"),
    ).toBeNull();
    expect(parseClientArea('')).toBeNull();
  });

  test('a maximised SimHub is clicked exactly where the coordinates were measured', () => {
    expect(aimAt(maximised, 336)).toEqual({
      dashStudio: { x: 100, y: 248 },
      trackLayoutOffer: { x: 2669, y: 182 },
      search: { x: 2004, y: 213 },
      row: { x: 1471, y: 336 },
      windowed: { x: 1520, y: 422 },
    });
  });

  test('a client area away from the origin moves every click with it', () => {
    const moved = aimAt({ ...maximised, left: 60, top: 40 }, 336);
    const home = aimAt(maximised, 336);
    for (const key of Object.keys(home) as (keyof typeof home)[]) {
      expect(moved[key]).toEqual({ x: home[key].x + 60, y: home[key].y + 40 });
    }
  });
});
