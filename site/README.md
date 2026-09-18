# The openDash website

The showcase site: what the dashes look like, which screens they fit, and where to download them.
Next.js, deployed as a container.

```bash
bun install
bun run dev              # http://localhost:3000
bun run build            # generate, then next build
bun run typecheck
bun run generate         # just the three generators
```

`dev`, `build` and `typecheck` all run `generate` first, because what it writes is gitignored and
a fresh clone has none of it.

## Nothing here restates a fact the repository already holds

Three scripts read the repository at build time, and their output is generated and gitignored.

| Script | Reads | Writes |
|---|---|---|
| `scripts/tokens.ts` | `design/tokens.json` | `app/tokens.css` |
| `scripts/content.ts` | `build/manifest.json`, `packages/dash/src/contract.ts`, `VERSION`, `CHANGELOG.md`, `build/*.simhubdash` | `lib/content.generated.ts` |
| `scripts/fonts.ts` | `packages/dash/fonts/*.ttf` | `public/fonts/*.woff2` |

So which packages exist, how big each one is, what the twenty-one modules are called, which three
ship switched off, what version this is and what each release changed are all read rather than
retyped. A colour comes from the token file through three layers of `var()`, exactly as the design
canvas describes them.

What is **not** generated is the prose, and one editorial file: `lib/packages.ts` holds the sentence
that says what each screen size is *for*, which is in no build output. If a size is added, add its
note there; a package with no note still renders.

`content.ts` reads `build/`, which only exists after `bun run build` at the repository root. Without
it the downloads come back empty and the Downloads page says so rather than inventing a file.

## The screenshots are real

Every picture is a capture of the package through SimHub's own renderer on the Windows VM. None is a
mock-up. Two commands make them, both from the repository root:

```bash
bun run shots --packages 'openDash,openDash 850x480' --scenarios green   # whole packages
bun scripts/modules.ts --scenario green                                   # each module alone
```

Then look at the captures, and copy the ones worth keeping in:

```bash
bun scripts/sync-shots.ts ../build/shots/green
```

`sync-shots` strips the ordinal from the file name, because inserting a package into the capture
list would otherwise renumber every file after it and break every reference at once. It is run by
hand rather than as part of a build: a capture that caught SimHub mid-reconnect is a photograph of a
bug, and the only thing that catches one is an eye.

`components/Shot.tsx` names the files it expects; `lib/packages.ts` maps a package folder to its
capture.

## Deploying

The image builds everything it serves. Its first stage runs the repository's own `bun run package`,
so the `.simhubdash` files and `OpenDash-plugin.zip` on the Downloads page are the files this commit
produces — there is no second place holding an artifact that can go stale, and no token to keep.
The consequence is that the site can only offer one version, which is why Downloads pairs the
current build with the changelog rather than with an archive.

**Build from the repository root, not from `site/`:**

```bash
docker build -f site/Dockerfile -t opendash-site .
docker run -p 3000:3000 -e NEXT_PUBLIC_SITE_URL=https://your.domain opendash-site
```

In Dokploy, set the build context to the repository root and the Dockerfile path to
`site/Dockerfile`. Redeploy to publish a release: the image rebuilds the packages from source.

### Environment

| Variable | |
|---|---|
| `NEXT_PUBLIC_SITE_URL` | The canonical origin, no trailing slash. There is no default — unset, the metadata carries no absolute URL and the sitemap comes back empty rather than pointing at a domain this repository would have had to guess. |
| `PORT` | Defaults to 3000. |

## Design

The design system is the dash's — `design/tokens.json` and the canvas — and most of it carries over
unchanged: the surface ladder, Barlow and Barlow Condensed, hard edges, and separation by 1 px rules
rather than by boxes.

Two things differ, both deliberate and both explained at the top of `app/globals.css`. The type ramp
is the site's own, because the dash's is pixels on a 480 px panel and the plugin's is 96 dpi inside
SimHub. And motion is allowed: rule 11 holds the dash to motion that carries meaning because a face
is read at speed, while a web page is documentation — the surface brand cyan already lives on.

`design/` is not edited from code. When the build and the canvas disagree, say so rather than
quietly changing either.
