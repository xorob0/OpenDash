/**
 * The shapes `scripts/demo-data.ts` writes into `lib/demo.generated.ts`, kept here so that the page
 * and the tests can name them without importing the generated file.
 */

/** One `.djson` the demo fetches, with what it weighs as served and as the build wrote it. */
export interface DemoFile {
  readonly file: string;
  /** Minified, as served from `public/demo/`. */
  readonly bytes: number;
  /** As the build wrote it, indented. */
  readonly builtBytes: number;
}

/** A picture out of a `.djson.ressources` sidecar, as the demo serves it. */
export interface DemoImage {
  /** The dashboard file whose `Images` list names it. */
  readonly dashboard: string;
  /** The `Name` an image item references. */
  readonly name: string;
  /** Where it is served, under `public/`. */
  readonly src: string;
}

export interface DemoFace {
  readonly slug: string;
  readonly folder: string;
  readonly width: number;
  readonly height: number;
  readonly round: boolean;
  /** The main dashboard, named after the folder. */
  readonly main: string;
  /** Where the files are served, e.g. `/demo/opendash-850x480/`. */
  readonly base: string;
  readonly files: readonly DemoFile[];
  readonly images: readonly DemoImage[];
  /** The zone face's settings prefix, e.g. `Face850x480`; null for a round face, which has slots. */
  readonly prefix: string | null;
  /** How many slot settings a round face reads; zero on a zone face. */
  readonly slots: number;
}

export interface DemoTrace {
  readonly src: string;
  readonly scenario: string;
  readonly frames: number;
  readonly hz: number;
  readonly recorded: string;
  readonly simHub: string;
  readonly bytes: number;
}
