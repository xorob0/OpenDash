/**
 * What `scripts/clips.ts` decides without a VM: its arguments, the rate it picks, how it reads the
 * recorder's report and the frame timestamps, the ffmpeg commands it builds, and when a recording
 * counts as frozen.
 */
import { describe, expect, test } from 'bun:test';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { LIST_ORDER } from './dev.ts';
import { CLIP_PACKAGES, clipSlug, encode, fileReader, fpsFor, frozen, gifArgs, measuredFps, mp4Args, parseArgs, posterArgs, RECORD_FILE, webmArgs, type EncodeInput, type ReadAt } from './clips.ts';
import { parseRecordReport } from './gui.ts';

describe('the arguments', () => {
  test('default to the three clips the site shows, on the clip scenario, six seconds, auto rate', () => {
    expect(parseArgs([])).toMatchObject({ packages: [...CLIP_PACKAGES], scenario: 'clip', seconds: 6, fps: 'auto', preroll: 1, encodeOnly: false });
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
    expect(measuredFps(`${ticks}\n`)).toBeCloseTo(20, 5);
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

  // The GIF once filtered the shared list by value, dropping `-r` and every all-digit token except
  // the ones equal to the seconds or the preroll. A rate equal to either survived without its `-r`,
  // and ffmpeg took the stray number for an output file: "Unable to choose an output format for '15'".
  const gifFilter = 'fps=12,scale=640:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3';
  const expectedGif = (i: EncodeInput, out: string): string[] => [
    '-y', '-f', 'rawvideo', '-pix_fmt', 'bgra', '-video_size', `${i.width}x${i.height}`, '-framerate', i.inputFps.toFixed(3), '-i', i.raw,
    '-ss', String(i.preroll), '-t', String(i.seconds), '-an', '-vf', gifFilter, '-loop', '0', out,
  ];

  for (const [what, i] of [
    ['a rate of its own', input],
    ['a rate equal to the seconds', { ...input, outputFps: 15, seconds: 15 }],
    ['a rate equal to the preroll', { ...input, outputFps: 2, preroll: 2 }],
  ] as const) {
    test(`the GIF has one filter, no -r and no stray number, with ${what}`, () => {
      const args = gifArgs(i, '/x/out.gif');
      expect(args).toEqual(expectedGif(i, '/x/out.gif'));
      expect(args.filter((a) => a === '-vf')).toHaveLength(1);
      expect(args).not.toContain('-r');
    });
  }
});

describe('a frozen recording', () => {
  const w = 2, h = 2, bytes = w * h * 4;
  const inMemory = (raw: Uint8Array): ReadAt => (offset, length) => raw.subarray(offset, offset + length);

  test('is one whose first and last kept frames are the same picture', () => {
    const still = new Uint8Array(bytes * 3).fill(7);
    expect(frozen(inMemory(still), w, h, 3, 1)).toBe(true);
    const moving = new Uint8Array(bytes * 3).fill(7);
    moving[bytes * 2] = 9;
    expect(frozen(inMemory(moving), w, h, 3, 1)).toBe(false);
  });

  test('too short to tell is not frozen', () => {
    expect(frozen(inMemory(new Uint8Array(bytes)), w, h, 1, 0)).toBe(false);
  });

  test('a file shorter than the frames it claims is not frozen', () => {
    expect(frozen(inMemory(new Uint8Array(bytes * 2).fill(7)), w, h, 3, 1)).toBe(false);
  });

  // A raw recording is hundreds of megabytes, about 870 at 1080p, and encode once read all of it
  // to compare two frames.
  test('encode reads the two frames it compares and nothing else of the recording', () => {
    const dir = mkdtempSync(path.join(tmpdir(), 'clips-'));
    try {
      const width = 4, height = 3, frameBytes = width * height * 4, frames = 50;
      writeFileSync(path.join(dir, 'frames.raw'), new Uint8Array(frameBytes * frames).fill(3));
      writeFileSync(path.join(dir, RECORD_FILE), JSON.stringify({ slug: 'x', fps: 10, seconds: 4, preroll: 1, recording: { width, height, frames, measuredFps: 10 } }));
      const reads: { file: string; offset: number; length: number }[] = [];
      const counting = (file: string): ReadAt => {
        const read = fileReader(file);
        return (offset, length) => {
          reads.push({ file: path.basename(file), offset, length });
          return read(offset, length);
        };
      };
      // Every frame alike, so encode stops at the check and never reaches ffmpeg.
      expect(encode(dir, { gif: false }, counting)).toMatchObject({ ok: false, why: expect.stringMatching(/nothing moved/) });
      expect(reads).toEqual([
        { file: 'frames.raw', offset: 10 * frameBytes, length: frameBytes },
        { file: 'frames.raw', offset: 49 * frameBytes, length: frameBytes },
      ]);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });

  test('the file reader reads at an offset and stops where the file ends', () => {
    const dir = mkdtempSync(path.join(tmpdir(), 'clips-'));
    try {
      const file = path.join(dir, 'f');
      writeFileSync(file, new Uint8Array([0, 1, 2, 3, 4, 5]));
      expect([...fileReader(file)(2, 3)]).toEqual([2, 3, 4]);
      expect([...fileReader(file)(4, 10)]).toEqual([4, 5]);
    } finally {
      rmSync(dir, { recursive: true, force: true });
    }
  });
});
