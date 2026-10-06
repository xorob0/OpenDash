/** Every `.djson` of the given zone faces, keyed `<folder>/<dashboard>.djson`, exactly as the build writes them. */
import { composePackages } from '../../src/build.ts';
import { serializeDashboard } from '../../src/generator.ts';
import { ZONE_FACES } from '../../src/zones/index.ts';
import type { ZoneLayout } from '../../src/zones/layout.ts';

export function faceDjson(faces: readonly ZoneLayout[] = ZONE_FACES): Record<string, string> {
  const out: Record<string, string> = {};
  const composed = composePackages({ version: '0.0.0-test', layouts: [], screens: [], zoneFaces: faces, log: () => {} });
  for (const { pkg } of composed) {
    for (const dashboard of pkg.dashboards) out[`${pkg.folderName}/${dashboard.name}.djson`] = serializeDashboard(dashboard, { packageName: pkg.folderName });
  }
  return out;
}
