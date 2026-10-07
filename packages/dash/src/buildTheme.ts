/**
 * One theme's faces, composed in a process of its own and printed as JSON, for `build.ts`.
 *
 * A theme's colours are fixed when `ds` is built, once per process, so a theme with colours of its
 * own cannot be drawn in the process that draws the default. `build.ts` starts this file with
 * `OPENDASH_THEME` naming the theme, hands it a `ThemeRequest` as its one argument, and writes what it
 * prints exactly as it writes a face composed in place, after validating every other package first.
 */
import { writeSync } from 'node:fs';
import { composeThemeHere, type ThemeRequest } from './build.ts';

/** Prints the composed faces on stdout; a failure goes to stderr and exits 1, which the build reports. */
export function main(argv: readonly string[] = process.argv.slice(2)): number {
  try {
    const request = JSON.parse(argv[0] ?? '') as ThemeRequest;
    // Written synchronously and to the end, because the process exits on return and a pipe takes
    // megabytes in pieces: process.stdout.write left the parent half a JSON document.
    const bytes = Buffer.from(JSON.stringify(composeThemeHere(request)), 'utf8');
    for (let at = 0; at < bytes.length; ) at += writeSync(1, bytes, at);
    return 0;
  } catch (e) {
    console.error(e instanceof Error ? e.message : String(e));
    return 1;
  }
}

if (import.meta.main) process.exit(main());
