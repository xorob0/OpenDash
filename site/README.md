# The openDash website

The showcase site: what the dashboards look like, which screens they fit, what they cost, and where
to download them. Next.js, deployed as a container.

```bash
bun install
bun run dev              # http://localhost:3000
bun run build            # generate, then next build
bun run typecheck
bun run generate         # just the three generators
```

`dev`, `build` and `typecheck` all run `generate` first, because what it writes is gitignored and a
fresh clone has none of it.

## Nothing here restates a fact the repository already holds

Three scripts read the repository at build time, and their output is generated and gitignored.

| Script | Reads | Writes |
|---|---|---|
| `scripts/tokens.ts` | `design/tokens.json` | `app/tokens.css` |
| `scripts/content.ts` | `build/manifest.json`, `packages/dash/src/contract.ts`, `flags.ts`, `leds/strip.ts`, `zones/index.ts`, `VERSION`, `CHANGELOG.md`, `build/OpenDash-plugin.zip` | `lib/content.generated.ts` |
| `scripts/fonts.ts` | `packages/dash/fonts/*.ttf` | `public/fonts/*.woff2` |

So which packages exist, which of them are a car theme's and what the themes are, how big the plugin
zip is, what the pages are called, which ship off, the strip shapes, how many LED profiles and flag
box glyphs there are, the flags in ranked order, the rectangles of the base face, what version this
is and what each release changed are all read rather than retyped. A colour comes from the token
file through three layers of `var()`. A count in a sentence comes through `lib/counts.ts`, which
`lib/liveCounts.ts` fills from the generated file, and `test/copy.test.ts` refuses a digit typed
before one of the nouns the site counts.

What is **not** generated is the prose. `lib/site.ts` holds the sentences every page reuses: the
free-forever promise and the sentence that puts the car themes inside it, the sim claim, the
no-tracking line, the car data attribution and the three differentiators. `lib/packages.ts` holds
the sentence that says what each size is for. `lib/compare.ts` holds the comparison with Lovely Sim
Racing and Daniel Newman Racing, every competitor cell read from their pages on the date in
`CHECKED_ON`, which the page prints. `lib/anatomy.ts` holds the words for the parts of the face;
their rectangles are generated.

`content.ts` reads `build/`, which only exists after `bun run build` at the repository root, and the
plugin zip is in it only after `bun run package`. Without it the download comes back empty and the
pages say so rather than inventing a file.

## The pages

| Route | What it answers |
|---|---|
| `/` | which sim, which host, what it costs; find your screen; download |
| `/screens` | every size, to scale; the anatomy; the companion; the pit wall |
| `/themes` | the Porsche face beside the plain one; what a theme is; what follows; free like the rest |
| `/pages` | the 21 pages, captured; the face's other catalogues |
| `/lights` | the car's own shift lights, the strip shapes, the flag box, In the plugin (which page holds what) |
| `/plugin` | the panel's eight pages, each captured; a screen set up on a picture of itself; the rig; updates that keep what you changed; what leaves your machine |
| `/compare` | openDash beside the two competitors, dated |
| `/install` | the plugin, the unblock step, nothing showing, and why there is no other way in |
| `/download` | the plugin, what it carries, the release notes |

`lib/routes.ts` lists them with their anchors. The sitemap is generated from it and
`test/links.test.ts` checks every `href` against it.

## The pictures are real, and they say when they were taken

Every picture is a capture of the package through SimHub's own renderer on the Windows VM. None is
a mock-up. `public/shots/captures.json` records the version, commit, date and emulator scenario
behind every file, and every caption says which version took the picture through
`provenanceNote()`; when it is not the version being served the caption says it is older.
`test/captures.test.ts` fails on a missing picture and warns on a stale one
(`OPENDASH_SHOTS_STRICT=1` makes it fail).

Files are `<slug>.png` for a package, `page-<id>.png` for a page and `panel-<page>.png` for a page
of the plugin. Two commands make them, both from the repository root:

```bash
bun run shots --scenarios gallery          # whole packages
bun run modules --scenario gallery  # each page alone
```

Then look at the captures, and copy the ones worth keeping in:

```bash
bun scripts/sync-shots.ts ../build/shots/gallery
```

It is run by hand rather than as part of a build: a capture that caught SimHub mid-reconnect is a
photograph of a bug, and the only thing that catches one is an eye.

The pictures of the plugin's panel are `bun run panel-shots` at the repository root, one per page of
the sidebar, synced one at a time with `bun scripts/sync-shots.ts --panel <page> <file>`.

The clips under `public/clips/` are the same idea in motion: raw frames of a dash window recorded
by `bun run clips`, encoded on the host, with `clips.json` saying what was taken. A page shows a
clip where one exists and the still otherwise; a reader who asked for reduced motion sees the still.

## Tests

`test/` runs under the repository's root `bun test` and imports nothing generated. `content.test.ts`
holds the generator's functions against the modules they read; `captures.test.ts`, `links.test.ts`,
`copy.test.ts` and `compare.test.ts` hold the site to its own rules: every picture exists, every
link resolves, the promise is where it has to be, no em dash, no host beyond GitHub, the car data
source and SimHub, and the comparison is complete and dated.

## Deploying

[docs/deploy.md](../docs/deploy.md) is the deployment: the Dokploy application field by field, the
first deploy, what a redeploy publishes, and the failures worth recognising. Two facts from it are
worth having here, because both are easy to get wrong from inside this directory.

The image builds everything it serves. Its first stage runs the repository's own `bun run package`,
so the `OpenDash-plugin.zip` on the Downloads page is the file this commit produces, and the only
one the site offers, and a redeploy is a release of the downloads as much as of the pages. **Build from the
repository root, not from `site/`**, because that first stage needs the whole tree:

```bash
docker build -f site/Dockerfile --build-arg NEXT_PUBLIC_SITE_URL=https://your.domain -t opendash-site .
docker run -p 3000:3000 opendash-site
```

And the canonical origin is a build argument rather than a runtime variable. Next substitutes
`NEXT_PUBLIC_*` into its output while building, so a value given to `docker run`, or to Dokploy's
Environment tab, is read by nothing: the site answers, and its sitemap comes back empty.

### Settings

| | |
|---|---|
| `NEXT_PUBLIC_SITE_URL` (build argument) | The canonical origin, no trailing slash. There is no default: unset, the metadata carries no absolute URL and the sitemap comes back empty rather than pointing at a domain this repository would have had to guess. |
| `PORT` (environment) | Defaults to 3000. |
| `DATABASE_URL` (environment) | PostgreSQL, for the sim racer survey — the one thing the site stores. Unset, a submission answers 503 and every other page is unaffected. |
| `SURVEY_ADMIN_KEY` (environment) | Gates the survey results page. Unset, the results answer 503 rather than letting an empty key through. |

## The survey

`lib/survey.ts` holds the questions once for all three consumers: the form at `/survey-k4qf9v`, the
validation in `app/api/survey/route.ts`, and the tallies at `/survey-admin-w7ne3p` behind the key.
Neither page is in `lib/routes.ts`, deliberately: they are handed out, not found, so the nav, the
sitemap and the link test do not know them, and both carry `robots: noindex`. Changing an address is
renaming its folder. Storage is one PostgreSQL table through Prisma
(`prisma/schema.prisma`), created on first use; `prisma generate` runs as part of `bun run generate`.

## Design

The design system is the dash's — `design/tokens.json` and the canvas — and most of it carries over
unchanged: the surface ladder, Barlow and Barlow Condensed, hard edges, and separation by 1 px rules
rather than by boxes.

Two things differ, both deliberate and both explained at the top of `app/globals.css`. The type ramp
is the site's own, because the dash's is pixels on a 480 px panel and the plugin's is 96 dpi inside
SimHub. And motion is kept to what carries meaning, as on the dash: a clip moves because the
dashboard moves, and a reader who asked for reduced motion sees the still.

`design/` is not edited from code. When the build and the canvas disagree, say so rather than
quietly changing either.
