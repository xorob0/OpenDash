# media

Screenshots and clips referenced from pull request descriptions, one directory per issue:
`media/xor-82/header-before.png`. Nothing here is built, shipped or imported; it exists so that a
reviewer can see what a change looks like without installing a package.

These are committed to `main` along with the change they illustrate, which is why the directory is
small and deliberate. A capture earns a place here by being referenced from a pull request body.

`bun run shots` writes to `build/shots/`, which is scratch and gitignored. Copy the one worth
showing into `media/<issue>/` and leave the rest behind.

## `media/readme/`

One directory is not a record of a pull request: `media/readme/` holds the four captures
`README.md` shows, and it is the current state of the product rather than the state at the time of
some issue. A capture there is therefore **replaced in place** under the same name when the face
changes, so that the README never points at a picture of something that has been redrawn, and so
that the directory does not grow a file per release.

Which four they are is a product decision rather than this directory's: the base face and the large
one, which `docs/scope.md` names as 850 x 480 and 1280 x 480, plus the companion and the pit wall.
A file here is named for its size, so changing which sizes the README shows does rename files, and
that is the one time the replace-in-place rule does not apply.

They live in the repository rather than being attached to a release for two reasons. A relative
path is rendered by GitHub at whatever commit the reader is looking at, so a reader of an old tag
sees the dashboard of that tag, whereas a link to a release asset shows today's picture whatever
the reader checked out, and it breaks outright if the release is deleted. The cost is history
weight, since a replaced PNG leaves the old one in the object store for ever; that cost is bounded
because there are four of them, they come to about four hundred kilobytes together, and a face is
not redrawn often.

Regenerating them is one command, and the scenario matters: `green` holds the flag, the session and
the field still, so what differs between one capture and the next is the change being shown rather
than the lap. The revs, the gear and the speed do keep moving, which is worth knowing before
photographing a face: take several frames and keep the one where the rev bar is lit, since a bar
photographed at part throttle is dark and says nothing.

```bash
bun run shots --packages "OpenDash 850x480,OpenDash 1280x480,OpenDash Companion,OpenDash Pit wall" --scenarios green
```

Then copy the four PNGs over the ones in `media/readme/`, keeping the names, and say in the commit
message what moved.

Three of those four cannot be opened by that command today, which is #303. Dash Studio draws a
"Last used" band above the list once a dashboard has been run, the band pushes every row down by
about 220 px, and `openDashboard` measures its rows from a constant, so the click lands on a
neighbour: filtering by `OpenDash` opened the face one row below it. Until the opener measures the
list rather than assuming it, open those three in Dash Studio by hand (Start, then Windowed) and
run `bun run dev '<package>' --no-build --keep`, which finds the window already open, skips the
clicking and photographs it into `build/dev.png` at its own size.
