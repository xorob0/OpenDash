/**
 * The thumbnail SimHub's dashboard list draws beside a package's name.
 *
 * SimHub looks for `<dashboard>.djson.png` next to the `.djson` and draws an empty box when there
 * is none, which is what every OpenDash package used to install: a list of grey rectangles, where
 * the dashboards a user downloaded by hand each show their face. DashStudio writes that file on
 * every save, so a generated package is the one kind that has to bring its own.
 *
 * What it brings is a photograph, not a drawing. [ADR 0008](../../../docs/decisions/0008-how-a-pull-request-renders-a-dash.md)
 * settled that OpenDash does not render its own dashboards, so the picture in the gallery is a
 * capture of the real face taken from SimHub on the Windows VM by `bun run previews`, committed
 * here, and copied into the package by `build.ts`. That makes these files the one part of a
 * package that is not a function of the sources: they are photographs of an earlier build, and a
 * face that is redrawn wants photographing again.
 *
 * A file is named for the package folder and nothing else, because that is the name SimHub insists
 * on -- `EditorModel.CleanDir` deletes a `<x>.djson.png` whose base name is not the folder's own.
 * {@link orphanedPreviews} is what keeps a renamed package from leaving its old picture behind, and
 * `run.json` beside them is what each one says about the build it is a picture of.
 */
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

/** Where the captures live, which is the one place a preview is read from. */
export const PREVIEWS_DIR = path.resolve(import.meta.dir, '..', 'previews');

/**
 * What a thumbnail is a picture of, recorded beside the pictures in `previews/run.json`.
 *
 * The weakness of photographing a generated package is that the photograph does not age with the
 * source: a face redrawn today keeps yesterday's picture in the gallery and nothing says so. The
 * site learned this the expensive way in #375, where three pages promised that none of their
 * captures was a mock-up and every one of them was a version old, and `scripts/shotsRun.ts` is
 * what came out of it. This is the same record, kept per package rather than per run, because
 * `bun run previews` is usually pointed at the one face that changed.
 */
export interface PreviewRecord {
  /** The version stamped on the build the picture was taken from. */
  version: string;
  /** Short commit of the tree that built it, and whether that tree had uncommitted changes. */
  commit: string;
  dirty: boolean;
  /** ISO date, as `scripts/shotsRun.ts` writes it. */
  date: string;
  /** Which telemetry was being replayed, and how many laps SimHub had seen when the shutter fell. */
  scenario: string | null;
  lapsSeen: number | null;
  /** The picture's own pixel size, which is not the dashboard's; see scripts/previews.ts. */
  width: number;
  height: number;
}

export interface PreviewRun {
  schema: 1;
  /** Keyed by package folder, which is also the file's base name. */
  previews: Record<string, PreviewRecord>;
}

export const PREVIEW_RUN_FILE = 'run.json';

/** What the committed captures say about themselves, or an empty record when nothing has been taken. */
export function readPreviewRun(dir: string = PREVIEWS_DIR): PreviewRun {
  const file = path.join(dir, PREVIEW_RUN_FILE);
  if (!existsSync(file)) return { schema: 1, previews: {} };
  return JSON.parse(readFileSync(file, 'utf8')) as PreviewRun;
}

/** What a capture is called: the package folder, which is also the main dashboard's name. */
export const previewFileName = (folder: string): string => `${folder}.png`;

/** The capture for a package folder, or undefined when nobody has photographed it yet. */
export function previewFor(folder: string, dir: string = PREVIEWS_DIR): string | undefined {
  const file = path.join(dir, previewFileName(folder));
  return existsSync(file) ? file : undefined;
}

/**
 * Captures that belong to no package any more, as file names.
 *
 * A renamed or deleted package leaves its picture here, where nothing would ever notice: the build
 * asks for a preview by folder name and simply does not find one. So the question is asked the
 * other way round as well, and `previews.test.ts` is where it is asked.
 */
export function orphanedPreviews(folders: readonly string[], dir: string = PREVIEWS_DIR): string[] {
  if (!existsSync(dir)) return [];
  const wanted = new Set(folders.map(previewFileName));
  return readdirSync(dir)
    .filter((name) => name.toLowerCase().endsWith('.png') && !wanted.has(name))
    .sort();
}

/** Package folders whose picture and `run.json` entry disagree about whether they exist. */
export function unrecordedPreviews(folders: readonly string[], dir: string = PREVIEWS_DIR): { file: string[]; record: string[] } {
  const recorded = readPreviewRun(dir).previews;
  return {
    // A picture somebody copied in by hand, which then says nothing about what it shows.
    file: folders.filter((f) => previewFor(f, dir) !== undefined && recorded[f] === undefined).sort(),
    // A record for a picture that is not there, which is a claim about a thumbnail nobody will see.
    record: Object.keys(recorded).filter((f) => previewFor(f, dir) === undefined).sort(),
  };
}
