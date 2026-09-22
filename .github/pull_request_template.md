<!--
The definition of done lives in docs/scope.md and is not repeated here. This asks for the things
a reviewer cannot get for themselves.
-->

## What changed, and why

<!-- One paragraph. The why matters more than the what; the diff already says the what. -->

Closes XOR-

## Done

- [ ] `bun run check` passes
- [ ] `dotnet test plugin/OpenDash.Tests` passes, or the plugin did not change
- [ ] The snapshot diff was **read** rather than refreshed, and the message says what moved and why
- [ ] Seen on the Windows VM in real SimHub, or this says plainly that it was not

## What it looks like

<!--
`bun run shots --packages "OpenDash" --scenarios green` photographs the dash window at its own
size through SimHub's own renderer. Put the capture worth showing in media/<issue>/ and link it
here. A change nobody can see does not need one; say so instead of leaving this empty.
-->

## Anything a reviewer should know

<!--
A decision you are not sure about, a number taken from the canvas, a test you could not write,
a thing you left for another ticket. This is the most useful section in the template and the
one most often left blank.
-->
