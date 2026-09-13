# binding-probe

Two throwaway packages that answer, on the VM, the questions
[ADR 0011](../../docs/decisions/0011-personalisation.md) had to settle: which properties SimHub
applies a binding to, and what a package full of bindings costs per frame.

Neither is a dashboard. Neither ships. They exist so the record cites a measurement rather than an
expectation, and so the measurement can be repeated when SimHub changes.

## `probe.ts`: which properties bind

```bash
bun tools/binding-probe/probe.ts build
bun run vm install 'openDash Probe'
```

Then open `openDash Probe` in SimHub and look at it. Twelve rows, each drawing a literal that says
FAIL and a binding that says PASS, so the answer needs no telemetry and no plugin:

| Row | Question |
|---|---|
| 1 | `TextColor`, formula mode |
| 2 | `TextColor`, gradient mode (`Mode` 4) |
| 3 | `FontSize` |
| 4 | `Font`, which SimHub marks `[NoBinding]` |
| 5 | `CharWidth`, also `[NoBinding]` |
| 6 | `BorderColor` on the `BorderStyle` sub-object |
| 7 | `BorderColor` at item level, which cannot resolve. Expected to stay red |
| 8 | `BorderTop` on the same sub-object, an `int` |
| 9 | `Left` |
| 10 | `Opacity` |
| 11 | A target no item has. Expected to draw normally: an unknown name is a silent no-op |
| 12 | `PaddingLeft` on the `TextPadding` sub-object |

It is written as plain JSON rather than through `packages/generator`, because the generator's
validator permits only the targets openDash already relies on and the point is the ones it does not.

## `themedTwin.ts`: what the bindings cost

```bash
bun tools/binding-probe/themedTwin.ts 'build/openDash zones 1920x480'
```

Rewrites a built package so that every non-transparent colour becomes
`isnull([OpenDash.Theme<n>], '<the literal it replaces>')`, which is the shape the colour tickets
would emit. It renders identically to the original with no plugin installed, so the difference
between the two is the bindings and nothing else.

Delete both from `DashTemplates` when you are done; they are not products and a stray package in
the list is confusing.
