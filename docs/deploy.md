# Deploying the website

The website is one container, built from `site/Dockerfile` and run on [Dokploy](https://dokploy.com).
This page is the deployment itself: the settings an application needs, the first deploy, and what a
redeploy actually publishes. [site/README.md](../site/README.md) is the site as a piece of software,
which is a different question.

## The one thing to understand first

The image builds everything it serves. Its first stage runs the repository's own `bun run package`,
so the `.simhubdash` files and `OpenDash-plugin.zip` behind the Downloads page are the files this
commit produces rather than artifacts uploaded from somewhere else. There is consequently no token
to keep and no second place that can go stale, and a redeploy is a release of the downloads as much
as of the pages. It also means the site can offer exactly one version at a time, which is why
Downloads pairs the current build with the changelog rather than with an archive.

The cost is the build: two toolchains, the whole dashboard, the plugin and then Next, which is
several minutes rather than several seconds. That is the trade the container was designed around,
and it is not worth optimising until a release is held up by it.

## The application in Dokploy

Create an application, point it at this repository and the branch to publish, then set the
following. The field names are Dokploy's own.

| Tab | Field | Value |
|---|---|---|
| Build | Build Type | `Dockerfile` |
| Build | Dockerfile Path | `site/Dockerfile` |
| Build | Docker Context Path | `.`, typed in rather than left to the placeholder |
| Build | Docker Build Stage | leave empty |
| Environment | Build Time Arguments | `NEXT_PUBLIC_SITE_URL=https://your.domain` |
| Domains | Host | `your.domain` |
| Domains | Container Port | `3000` |
| Domains | HTTPS | on, with Let's Encrypt as the certificate provider |

The context is the repository root and not `site/`, because the stages that build the packages and
run the site's generators read `packages/`, `design/`, `scripts/`, `plugin/`, `VERSION` and
`CHANGELOG.md`, none of which live under `site/`. A context set to `site/` fails on the first
`COPY`.

**Type the dot.** Dokploy shows `Path of your docker context (default: .)` in that field as grey
placeholder text, which reads exactly like a value already in place, and it is not one. An empty
field is stored as the empty string, and the builder treats the empty string as no answer at all:

```js
const defaultContextPath =
  dockerFilePath.substring(0, dockerFilePath.lastIndexOf("/") + 1) || ".";
const dockerContextPath = getDockerContextPath(application) || defaultContextPath;
```

The fallback is therefore the directory holding the Dockerfile, which here is `site/`, and not the
repository root the placeholder promises. A value that is typed, on the other hand, is joined to
the clone root rather than to the Dockerfile, so `.` means the repository. Both lines are from
Dokploy 0.29; should a later version make the placeholder true, typing the dot remains correct.

### The origin is a build argument, not an environment variable

`NEXT_PUBLIC_SITE_URL` goes in **Build Time Arguments** and nowhere else. Next substitutes every
`NEXT_PUBLIC_*` variable into its output while building rather than reading it when the server
starts, so a value placed in the Environment tab is read by nothing at all. The failure is quiet:
the site answers every request correctly, and meanwhile `/sitemap.xml` comes back with no entries
and `robots.txt` carries neither a `Sitemap:` nor a `Host:` line. The metadata loses its
`metadataBase` at the same time, which today changes nothing visible, because no page declares a
canonical or an Open Graph image; it would matter the moment one did. Should the domain change
later, the value has to change here and the application has to be rebuilt, since a restart will not
do it.

Give the origin with its scheme and without a trailing slash. Unset, the metadata simply carries no
absolute URL, which [site/lib/site.ts](../site/lib/site.ts) prefers to guessing at a domain this
repository does not know.

### What runs, and what the platform watches

The runtime stage listens on `PORT`, which defaults to `3000`, as `HOSTNAME=0.0.0.0`, as the
unprivileged `node` user. Nothing else is configurable and nothing else needs to be.

The image declares a `HEALTHCHECK` that fetches the home page, so Dokploy has a real answer rather
than the existence of the process, which precedes the first served request by about a second. That
is worth pairing with a rolling update, under Advanced, Cluster Settings, Swarm Settings, Update
Config:

```json
{ "Parallelism": 1, "Order": "start-first", "FailureAction": "rollback" }
```

With `start-first` the new container has to pass its health check before the old one is taken down,
and a build that starts but cannot serve rolls back instead of taking the site with it.

## The first deploy

Deploy, and watch the build log rather than the deployment status: the long silences are
`bun run package` compiling the plugin and `next build`, both of which are expected. When it
finishes, three things are worth checking, in this order, because each one fails differently.

```bash
curl -sI https://your.domain/ | head -1
curl -s https://your.domain/sitemap.xml
curl -sI https://your.domain/downloads/OpenDash-plugin.zip | head -1
```

The first says the container is serving and the certificate is in place. The second says the build
argument arrived, and should list the seven pages rather than come back as an empty `<urlset>`. The
third says the artifacts stage ran and its output reached the public folder, which is the part with the
most moving pieces behind it.

## Publishing a release

Redeploy. The image rebuilds the packages from source, so whatever `main` produces at that moment
becomes both the pages and the downloads. There is no separate step for the artifacts and no
artifact to upload.

The corollary is that a redeploy taken from a tree mid-change publishes that tree. Deploy from a
branch you are willing to hand to a driver.

## When it goes wrong

**The sitemap is empty and `robots.txt` names no host.** The origin was set as an environment
variable rather than as a build argument, or it was set before the domain changed and the
application has not been rebuilt since. Move it to Build Time Arguments and rebuild.

**The build fails on a `COPY` with `"/site": not found`,** along with `/packages`, `/design`,
`/plugin` and most of the rest of the tree. The context is `site/` rather than the repository root,
which is what an empty Docker Context Path gives you. The other tell is in the log a few lines
earlier: `load .dockerignore` transferring two bytes means it found none, and the repository's own
`.dockerignore` is at the root. Type `.` into the field and redeploy.

**The site answers with no styling and no pictures.** The static and public folders did not reach
the runtime stage. Next leaves them out of the standalone output deliberately, expecting a CDN, and
the Dockerfile places them by hand; this is a change to the Dockerfile rather than to the
deployment.

**The downloads are missing or stale.** `.dockerignore` excludes `build/` and
`site/public/downloads` precisely so that a local build cannot be copied into the image and served
as this version's files. If the downloads are wrong, the artifacts stage is wrong, and the
`bun run package` output in the build log is where to look.

**The build runs out of memory or disk.** It carries the .NET SDK, bun, node and the whole
dependency tree of all four. A small instance will not do it, and the remedy is either a larger
builder or building the image elsewhere and deploying it by digest.

## Doing it without Dokploy

Nothing here is specific to the platform beyond the field names.

```bash
docker build -f site/Dockerfile --build-arg NEXT_PUBLIC_SITE_URL=https://your.domain -t opendash-site .
docker run -p 3000:3000 opendash-site
```

Built from the repository root, with the origin as a build argument. Any host that can run an image
and terminate TLS in front of it will serve this one.
