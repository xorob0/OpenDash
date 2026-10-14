/**
 * Every request the plugin makes, which is the whole of what OpenDash sends off the machine.
 *
 * The comparison's "What leaves your machine" row and the table under the telemetry refusal in
 * `docs/scope.md` both say this, and both once said less than the plugin did: the row named the
 * update check and the car tables, and the crest, fetched at a start without a press, was in
 * neither (#1096). So the list is written once, here, and `test/wire.test.ts` holds three things
 * to it: the plugin's code, where every fetch goes through `IReleaseSource` and only
 * `ReleaseClient.cs` opens a connection; the table in scope.md; and the comparison's cell.
 *
 * A request added to the plugin fails the test until it is listed here, and one listed here fails
 * it until scope.md and the cell name it. What is not here is not the plugin's: SimHub's own
 * requests, and the page a driver types into the pit wall's web view, which SimHub's browser item
 * fetches the way any browser would.
 */

export interface WireRequest {
  /** The request as the first column of scope.md's table names it. */
  name: string;
  /** The words the comparison's cell names it by. */
  cell: string;
  /** Whether it waits for a press. The two that do not are the two scope.md argues for. */
  pressed: boolean;
  /**
   * Where the plugin makes it: a file under `plugin/OpenDash` and the `IReleaseSource` method it
   * calls, once per call site, so a second call in the same file is a second entry.
   */
  calls: readonly string[];
  /**
   * The address it goes to when the plugin carries one: the constant's file and name, and the host
   * the constant has to name. A release's files come from the addresses the listing gives, so the
   * download carries none.
   */
  address?: { file: string; constant: string; host: string };
}

export const WIRE: readonly WireRequest[] = [
  {
    name: 'The update check',
    cell: 'An update check to GitHub',
    pressed: false,
    calls: ['UpdateService.cs GetString'],
    address: { file: 'UpdateCheck.cs', constant: 'ReleasesUrl', host: 'api.github.com' },
  },
  {
    name: 'The crest',
    cell: 'the Porsche crest',
    pressed: false,
    calls: ['CarCrest.cs GetBytes'],
    address: { file: 'CarCrest.cs', constant: 'DefaultUrl', host: 'upload.wikimedia.org' },
  },
  {
    name: 'The car tables',
    cell: 'The car tables',
    pressed: true,
    calls: ['CarLightLibrary.cs GetBytes'],
    address: { file: 'CarLightLibrary.cs', constant: 'ArchiveUrl', host: 'codeload.github.com' },
  },
  {
    name: 'A new release',
    cell: 'a new release',
    pressed: true,
    // The plugin, then each package the release carries beside it.
    calls: ['UpdateService.cs GetBytes', 'UpdateService.cs GetBytes'],
  },
];
