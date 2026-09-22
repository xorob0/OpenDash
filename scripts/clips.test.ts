/**
 * What `scripts/clips.ts` decides without a VM: its arguments, the rate it picks, how it reads the
 * recorder's report and the frame timestamps, the ffmpeg commands it builds, and when a recording
 * counts as frozen.
 */
import { describe, expect, test } from 'bun:test';
import { LIST_ORDER } from './dev.ts';
import { CLIP_PACKAGES, clipSlug, fpsFor, frozen, measuredFps, mp4Args, parseArgs, posterArgs, webmArgs, type EncodeInput } from './clips.ts';
import { parseRecordReport } from './gui.ts';

describe('the arguments', () => {
  test('default to the three clips the site shows, on gallery, six seconds, auto rate', () => {
    expect(parseArgs([])).toMatchObject({ packages: [...CLIP_PACKAGES], scenario: 'gallery', seconds: 6, fps: 'auto', preroll: 1, encodeOnly: false });
  });

  test('take a package list and a numeric rate', () => {
    expect(parseArgs(['--packages', 'OpenDash 1280x480, OpenDash Companion', '--fps=15', '--seconds', '12'])).toMatchObject({
      packages: ['OpenDash 1280x480', 'OpenDash Companion'],
      fps: 15,
      seconds: 12,
    });
  });

  test('help wins', () => {
    expect(parseArgs(['--packages', 'x', '--help'])).toEqual({ help: true });
  });
});

describe('the packages', () => {
  test('every default clip is a package the opener knows', () => {
    for (const p of CLIP_PACKAGES) expect((LIST_ORDER as readonly string[]).includes(p)).toBe(true);
  });

  test('the slug is the site’s', () => {
    expect(clipSlug('OpenDash Pit wall')).toBe('opendash-pit-wall');
    expect(clipSlug('OpenDash 850x480')).toBe('opendash-850x480');
  });

  test('the rate is 20 for a face and 15 for a pit wall', () => {
    expect(fpsFor({ width: 850, height: 480 })).toBe(20);
    expect(fpsFor({ width: 1280, height: 480 })).toBe(20);
    expect(fpsFor({ width: 1920, height: 1080 })).toBe(15);
  });
});

describe('the recorder’s report', () => {
  test('is parsed into numbers', () => {
    expect(parseRecordReport('launched\nrecorded 140 frames 0 dropped 19.94 fps mean 9.8 ms max 21.0 ms 850x480\n')).toEqual({
      frames: 140,
      dropped: 0,
      measuredFps: 19.94,
      captureMeanMs: 9.8,
      captureMaxMs: 21,
      width: 850,
      height: 480,
    });
  });

  test('anything else is an error naming what came back', () => {
    expect(() => parseRecordReport('no window titled OpenDash 850x480 (WPF Renderer)')).toThrow(/no window titled/);
  });

  test('the measured rate comes from the timestamps', () => {
    const ticks = Array.from({ length: 21 }, (_, i) => (i * 50).toFixed(3)).join('\n');
    expect(measuredFps(ticks)).toBeCloseTo(20, 5);
    expect(measuredFps('')).toBe(0);
  });
});

describe('the ffmpeg commands', () => {
  const input: EncodeInput = { raw: '/x/frames.raw', width: 850, height: 480, inputFps: 19.94, outputFps: 20, seconds: 6, preroll: 1 };

  test('the webm is VP9 at constant quality with one keyframe', () => {
    const args = webmArgs(input, '/x/out.webm');
    expect(args).toEqual(expect.arrayContaining(['-c:v', 'libvpx-vp9', '-crf', '32', '-b:v', '0', '-row-mt', '1', '-pix_fmt', 'yuv420p', '-g', '120', '-ss', '1', '-t', '6']));
    expect(args[args.indexOf('-framerate') + 1]).toBe('19.940');
    expect(args[args.length - 1]).toBe('/x/out.webm');
  });

  test('the mp4 is H.264 yuv420p with faststart', () => {
    expect(mp4Args(input, '/x/out.mp4')).toEqual(expect.arrayContaining(['-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', '-g', '120']));
  });

  test('the poster is the first kept frame', () => {
    const args = posterArgs(input, '/x/out.png');
    expect(args).toEqual(expect.arrayContaining(['-ss', '1', '-frames:v', '1']));
    expect(args).not.toContain('-t');
  });
});

describe('a frozen recording', () => {
  const w = 2, h = 2, bytes = w * h * 4;
  test('is one whose first and last kept frames are the same picture', () => {
    const still = new Uint8Array(bytes * 3).fill(7);
    expect(frozen(still, w, h, 3, 1)).toBe(true);
    const moving = new Uint8Array(bytes * 3).fill(7);
    moving[bytes * 2] = 9;
    expect(frozen(moving, w, h, 3, 1)).toBe(false);
  });

  test('too short to tell is not frozen', () => {
    expect(frozen(new Uint8Array(bytes), w, h, 1, 0)).toBe(false);
  });
});
