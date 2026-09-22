# The showcase site, and the packages it hands out.
#
# The site serves the .simhubdash files and OpenDash-plugin.zip itself rather than linking to a
# release, so the files have to come from somewhere. They come from this image: the first two
# stages are the repository's own build commands, run against the source the site is being built
# from, and the third copies what they produced into the site's public folder. That means the
# download button always hands over exactly what this commit produces — there is no second place
# holding a stale artifact, nothing to upload by hand, and no token to keep.
#
# It also means the site can only ever offer one version, which is the whole reason the Downloads
# page pairs the current build with the changelog rather than with an archive.
#
# Build from the REPOSITORY ROOT, not from site/, because stages 1 and 2 need the whole tree:
#
#   docker build -f site/Dockerfile -t opendash-site .
#   docker run -p 3000:3000 -e NEXT_PUBLIC_SITE_URL=https://example.com opendash-site
#
# Dokploy: set the build context to the repository root and the Dockerfile path to site/Dockerfile.

# ---------------------------------------------------------------- 1. everything the site hands out
# `bun run package` is the repository's own release command: it builds every .simhubdash and LED
# profile, copies them and the fonts into the plugin's resources, compiles the plugin against the
# reference assemblies committed under plugin/lib, and zips the DLL with INSTALL.md and the font
# licence. It is what CI runs to cut a release, so running it here is what makes the site's
# downloads the same files a release would carry — rather than a second recipe that can drift from
# it, which is exactly what the first draft of this file was.
#
# It needs both toolchains, so bun is copied into the .NET SDK image rather than either being
# reimplemented. That fuses what would be two cacheable layers into one; the alternative was two
# stages that between them restate package.sh, and a stale download is worse than a slow build.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS artifacts
COPY --from=oven/bun:1.4-debian /usr/local/bin/bun /usr/local/bin/bun
RUN apt-get update \
 && apt-get install -y --no-install-recommends zip unzip \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /repo
COPY package.json bun.lock bunfig.toml tsconfig.base.json VERSION ./
COPY packages ./packages
COPY design ./design
COPY scripts ./scripts
COPY tools ./tools
COPY data ./data
COPY plugin ./plugin
RUN bun install --frozen-lockfile
# Writes build/*.simhubdash, build/*.ledsprofile, build/flag-box.svg, build/manifest.json and
# build/OpenDash-plugin.zip.
RUN bun run package

# ---------------------------------------------------------------- 3. the site
FROM node:22-slim AS build
WORKDIR /repo
# The site's generators read design/tokens.json, packages/dash/src/contract.ts, scripts/changelog.ts,
# VERSION and CHANGELOG.md, so those parts of the tree have to be here too.
COPY --from=oven/bun:1.4-debian /usr/local/bin/bun /usr/local/bin/bun
COPY design ./design
COPY packages ./packages
COPY scripts ./scripts
COPY VERSION CHANGELOG.md package.json bun.lock bunfig.toml ./
# content.ts imports the module catalogue from packages/dash/src/contract.ts, which resolves
# `@opendash/generator` through the root workspace, so the root has to be installed here as well as
# in the artifacts stage. It is six packages and the lockfile is already in this layer, so this
# costs a second rather than the copy of a node_modules whose workspace links would have to survive
# the move intact.
RUN bun install --frozen-lockfile
COPY media ./media
COPY site ./site
# Everything the site hands out, in the one place next/static serves from.
COPY --from=artifacts /repo/build /repo/build
# The superseded card faces are built for comparison on a rig and are not a size anybody should
# install, so they are built but never offered; lib/packages.ts filters them out of the listing for
# the same reason, and a file on disk that no page links to is a download waiting to be guessed at.
RUN mkdir -p site/public/downloads \
 && cp /repo/build/*.simhubdash site/public/downloads/ \
 && rm -f 'site/public/downloads/OpenDash slots '*.simhubdash \
 && cp /repo/build/OpenDash-plugin.zip site/public/downloads/ \
 && cp /repo/build/flag-box.svg site/public/flag-box.svg

WORKDIR /repo/site
# Installed with bun, because site/bun.lock is the lockfile this repository keeps and a second one
# for npm would be a second answer to the same question. What bun writes is an ordinary
# node_modules, so the build below still runs under node.
RUN bun install --frozen-lockfile
# tokens.css from design/tokens.json, content.generated.ts from the manifest and the changelog, and
# the woff2 faces from the vendored TTFs. All three are gitignored, so the image has to make them.
RUN bun run generate
# Under node rather than bun: next build is the one step where the difference has bitten before,
# and the runtime stage is node anyway, so building on the same engine keeps them honest.
RUN node node_modules/.bin/next build

# ---------------------------------------------------------------- 4. what runs
FROM node:22-slim AS runtime
ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME=0.0.0.0
WORKDIR /app

# `output: standalone` writes a server carrying only the modules it actually imports. Because
# outputFileTracingRoot is the repository, the tree it writes is rooted there too, and the server
# and its node_modules sit under `standalone/site/` rather than at `standalone/`.
#
# The static and public folders are deliberately not part of that tree — Next expects them to be
# served from a CDN in the deployments this output was designed for — so they are placed by hand.
# Miss either and the site answers 200 with no CSS and no pictures, which is the failure mode this
# comment exists to prevent.
COPY --from=build --chown=node:node /repo/site/.next/standalone/site ./
COPY --from=build --chown=node:node /repo/site/.next/static ./.next/static
COPY --from=build --chown=node:node /repo/site/public ./public

# The one thing the site does write. next/image optimises a capture on first request and caches the
# result here; without a writable directory it re-optimises every picture on every request and logs
# an EACCES for each one. COPY lands files as root, so the ownership above and this directory are
# both needed — the first draft had neither and the container ran hot serving the same pages.
RUN mkdir -p .next/cache && chown -R node:node .next

USER node
EXPOSE 3000
CMD ["node", "server.js"]
