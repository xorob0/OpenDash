import type { NextConfig } from 'next';

const config: NextConfig = {
  // Dokploy runs the site as a container, so the build has to produce a server that can be copied
  // out of the build stage with its own minimal node_modules rather than the whole workspace.
  output: 'standalone',
  // The generators read design/tokens.json, the module catalogue and CHANGELOG.md from the
  // repository root, which is outside site/. Next has to be told the tracing root is the repo.
  outputFileTracingRoot: process.cwd() + '/..',
  images: {
    // Every image on the site is a PNG capture of a dash face: flat colour, hard edges and text.
    // AVIF is worth the encode time on exactly this kind of picture.
    formats: ['image/avif', 'image/webp'],
  },
  eslint: { ignoreDuringBuilds: true },
};

export default config;
