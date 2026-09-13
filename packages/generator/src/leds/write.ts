/**
 * Writing a `.ledsprofile`. Deliberately not part of `writePackage`: a profile is not a dashboard,
 * does not belong inside a `.simhubdash`, and `writePackage` begins by removing the folder it is
 * about to fill. `zipPackage` refuses a folder with no `<folderName>.djson` for the same reason.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { serializeProfile } from './serialize.ts';
import type { LedProfile } from './model.ts';

/** What a profile file is called. SimHub offers `Leds effects profile (*.ledsprofile)|*.ledsprofile`. */
export const LEDS_PROFILE_EXTENSION = '.ledsprofile';

/** Writes one profile into `outDir` and returns its absolute path. */
export function writeLedsProfile(profile: LedProfile, outDir: string, fileName = profile.name): string {
  mkdirSync(outDir, { recursive: true });
  const path = join(outDir, `${fileName}${LEDS_PROFILE_EXTENSION}`);
  writeFileSync(path, serializeProfile(profile));
  return path;
}
