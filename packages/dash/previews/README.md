# previews

One photograph per package, named for the package folder: `OpenDash 850x480.png`. The build copies
each one into its package as `<folder>.djson.png`, which is the file SimHub's dashboard list draws
as that dashboard's thumbnail.

Without it the list draws an empty box. DashStudio writes the file on every save, so every
dashboard a person drew by hand has one; a package this repository generates is the only kind that
has to bring its own, which is why OpenDash used to install as a column of grey rectangles beside
everybody else's artwork.

These are photographs and not drawings, because
[ADR 0008](../../../docs/decisions/0008-how-a-pull-request-renders-a-dash.md) is the record of
OpenDash not having a renderer of its own: the picture comes from SimHub on the Windows VM,
rendering the real package against replayed telemetry. So a file here is a picture of the build it
was taken from, and a face that has since been redrawn goes on showing the old one until somebody
takes it again.

## Refreshing them

```bash
bun run previews                                  # all of them, on the green scenario
bun run previews --packages 'OpenDash 850x480'    # the one that changed
```

It needs the VM and it needs `ffmpeg` on the machine running it. `bun run affected` prints which
packages a branch changed, and those are the ones worth retaking; say in the commit message which
faces moved, exactly as `media/readme/` asks.

A commit that refreshes them is a commit that changes the `.simhubdash` of every package it
touched, so `bun run affected` and the CI comment will list those packages as changed and ask for
captures of them. That is the circle closing rather than a fault: the captures it is asking for are
the ones that were just taken.

A capture is **replaced in place** under the same name, so the directory holds one file per package
and does not grow a file per release. A package that is renamed or removed leaves a file nothing
claims, which `packages/dash/test/previews.test.ts` fails on rather than letting it be embedded in
releases for ever.

## `run.json`

Beside the pictures, one record per package: the version, the short commit, whether that tree was
dirty, the date, the scenario and the laps SimHub had seen when the shutter fell. It is written by
`bun run previews` from the run `bun run shots` leaves beside its captures, and it is what answers
the only question these files cannot answer themselves -- which build am I looking at. The site
learned that lesson in #375, where three pages promised that none of their captures was a mock-up
and every one of them was a version old.

A refresh replaces the entries for the packages it photographed and leaves every other entry alone,
because a run's provenance is true of the pictures that run took and of no others. A picture with no
record, or a record with no picture, fails `packages/dash/test/previews.test.ts`.

## The size

Scaled to 300 px tall, which is what `EditorModel` passes to `ImageCapturer.SaveToPng` when
DashStudio writes its own, and quantised to 256 colours. A portrait screen is sized by its width
instead, to 200 px, because that is the width SimHub decodes the list's thumbnail at and a narrower
picture would be stretched back up to it.

The quantising is worth a sentence, since it is the one place these are not simply what the VM
sent. A downscaled dash is gradients where the capture was flat colour, so a truecolour thumbnail
of a face comes to about three times the bytes of the full-size capture it was made from. At this
size the difference is not visible and the files are committed here and then embedded in every
release, which is what the weight is being spent on.

## Missing ones

The build does not fail without them; it prints `warning preview/missing` per package and carries
on, because the machine that can take the picture is the VM and ADR 0008 is the record of not
making that a condition of a merge.
