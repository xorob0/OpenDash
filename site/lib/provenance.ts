/**
 * The caption's note on where a picture came from: the version the capture was taken at, and when
 * that is not the version being served, that it is older. Every picture says it for itself, since
 * a reshoot of the packages leaves the pages as old as they were (#627), and a reader comparing the
 * site with the download is told which pictures predate it (#552).
 *
 * A file of its own, with no node import, because `Clip` is a client component and bundles what it
 * imports: `captures.ts` reads the sidecar from disk and cannot go to the browser.
 */
export const provenanceNote = (taken: string, served: string): string => (taken === served ? `captured at ${taken}` : `captured at ${taken}, before ${served}`);
