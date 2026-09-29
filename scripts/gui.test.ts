/**
 * What `scripts/gui.ts` decides without a VM: whether the guest's display is in the mode its
 * coordinates were measured in, and what a failed open blames. The clicking itself is a remote side
 * effect and is proved by running it.
 */
import { describe, expect, test } from 'bun:test';
import { EXPECTED_SCREEN, openFailure, screenModeProblem } from './gui.ts';

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
