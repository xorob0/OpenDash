# The showcase site, and the plugin it hands out.
#
# The site serves OpenDash-plugin.zip itself rather than linking to a release, so the file has to
# come from somewhere. It is the one file: the plugin is the only way in (docs/scope.md, #438), and
# the packages and LED profiles the build writes reach a user inside it. They come from this image: the first two
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
#   docker build -f site/Dockerfile --build-arg NEXT_PUBLIC_SITE_URL=https://example.com -t opendash-site .
#   docker run -p 3000:3000 opendash-site
#
# The origin is a build argument and not a runtime variable, for the reason given at the step that
# consumes it. docs/deploy.md is the deployment itself, Dokploy field by field.
#
# The stages below are a hand-maintained subset of the tree, so CI builds this file and runs what
# comes out of it on every push that touches anything it copies — see
# .github/workflows/site-image.yml. A stage that stops copying something the site needs fails there,
# on the branch that changed it, rather than in front of somebody pressing Redeploy.

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
# The one file packages/dash reads from outside packages/: leds/shiftPoints.ts imports
# data/shift-points.json, and site/test/content.test.ts imports the strip shapes that reach it.
# site/tsconfig.json excludes scripts/ but not test/, so `next build` type-checks that chain and
# fails on a JSON module this stage does not have, which is how it broke the first real deploy.
COPY data ./data
COPY VERSION CHANGELOG.md package.json bun.lock bunfig.toml ./
# content.ts imports the module catalogue from packages/dash/src/contract.ts, which resolves
# `@opendash/generator` through the root workspace, so the root has to be installed here as well as
# in the artifacts stage. It is six packages and the lockfile is already in this layer, so this
# costs a second rather than the copy of a node_modules whose workspace links would have to survive
# the move intact.
RUN bun install --frozen-lockfile
COPY media ./media
# The recorded telemetry the demo page replays; scripts/demo-data.ts copies one trace into public/demo/.
COPY traces ./traces
COPY site ./site
# The build output the generators read (the manifest says which screens exist), and the one file the
# site hands out, in the one place next/static serves from. Only the zip is copied into downloads:
# a .simhubdash or a .ledsprofile on disk that no page links to is a download waiting to be guessed
# at, and offering one is the route #438 closed.
COPY --from=artifacts /repo/build /repo/build
RUN mkdir -p site/public/downloads \
 && cp /repo/build/OpenDash-plugin.zip site/public/downloads/ \
 && cp /repo/build/flag-box.svg site/public/flag-box.svg

WORKDIR /repo/site
# Installed with bun, because site/bun.lock is the lockfile this repository keeps and a second one
# for npm would be a second answer to the same question. What bun writes is an ordinary
# node_modules, so the build below still runs under node.
RUN bun install --frozen-lockfile
# tokens.css from design/tokens.json, content.generated.ts from the manifest and the changelog, the
# demo's faces and trace from build/ and traces/, and the woff2 faces from the vendored TTFs. All of
# them are gitignored, so the image has to make them.
RUN bun run generate
# Next substitutes every NEXT_PUBLIC_* variable into the output as it builds rather than reading it
# when the server starts, so the canonical origin has to be given here. Passing it to `docker run`,
# or to Dokploy's Environment tab, sets a variable nothing reads: the site answers, and its sitemap
# comes back empty. In Dokploy it belongs in Build Time Arguments. The ARG sits immediately before
# the step that needs it so that changing the domain rebuilds this layer and no earlier one.
#
# The empty default is the documented degradation rather than an oversight: with no origin the
# metadata carries no absolute URL and the sitemap is empty, which lib/site.ts prefers to guessing
# at a domain.
ARG NEXT_PUBLIC_SITE_URL=""
ENV NEXT_PUBLIC_SITE_URL=$NEXT_PUBLIC_SITE_URL
# Under node rather than bun: next build is the one step where the difference has bitten before,
# and the runtime stage is node anyway, so building on the same engine keeps them honest.
RUN node node_modules/.bin/next build

# ---------------------------------------------------------------- 4. what runs
FROM node:22-slim AS runtime
ENV NODE_ENV=production
ENV PORT=3000
ENV HOSTNAME=0.0.0.0
WORKDIR /app

# Prisma's query engine links the system libssl; node itself does not need it, so the slim image
# does not carry it. What its absence actually costs was measured rather than assumed (#404): Prisma
# then cannot detect an openssl version, says so and falls back from the debian-openssl-3.0.x engine
# schema.prisma declares — left with libssl.so.3 unresolved — to its openssl-1.1.x one, which
# answered a query against a postgres with TLS and without it. So this install is what keeps the
# image running the engine it was built to run, rather than the difference between a working survey
# and a broken one; .github/workflows/site-image.yml is where it is checked.
#
# DATABASE_URL and SURVEY_ADMIN_KEY are runtime variables set in Dokploy's Environment tab;
# docs/deploy.md has the rows.
RUN apt-get update \
 && apt-get install -y --no-install-recommends openssl ca-certificates \
 && rm -rf /var/lib/apt/lists/*

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

# Without this the orchestrator has only the existence of the process to go on, and the process
# exists about a second before Next answers anything; a rolling update would hand readers over to a
# container that is not serving yet. node:22-slim carries neither curl nor wget, so the probe is
# node's own fetch against the home page, which is prerendered and costs a file read.
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD node -e "fetch('http://127.0.0.1:'+(process.env.PORT||3000)+'/').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"

CMD ["node", "server.js"]
