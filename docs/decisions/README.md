# Decision records

One file per decision that would otherwise have to be re-derived from the code. A record says what
was decided, what it was decided against, and what it costs; the code says what it is.

A record wins over any summary of it. [scope.md](../scope.md) summarises several of these and is
the thing that is wrong when the two disagree.

## Accepted

| | | |
|---|---|---|
| 0001 | [SimHub native rendering](0001-simhub-native-rendering.md) | openDash renders through SimHub and will continue to |
| 0002 | [The `.djson` is generated from source](0002-djson-generated-from-source.md) | The scene graph is build output; TypeScript and tokens are the source |
| 0003 | [Plugin settings through properties](0003-plugin-settings-through-properties.md) | The plugin attaches settings and does nothing else; a package alone is a complete product |
| 0004 | [The rev bar model](0004-rev-bar-model.md) | SimHub's per-car band values are not bar percentages, so the bar is segments |
| 0005 | [The plugin builds on Linux](0005-plugin-builds-on-linux.md) | net48, code-only WPF, no Windows in the build |
| 0006 | [The face is zones, not slots](0006-the-zone-face.md) | Five parts, a catalogue and a wheel button per zone; amends 0003 |
| 0008 | [How a pull request renders a dash](0008-how-a-pull-request-renders-a-dash.md) | The author attaches a native SimHub capture; there is no preview renderer |
| 0009 | [Does the plugin compute?](0009-does-the-plugin-compute.md) | No, and it turned out not to need to: SimHub already publishes almost all of it |
| 0011 | [How far personalisation reaches](0011-personalisation.md) | Colour is a runtime setting, geometry is a build input, and nothing regenerates a package locally |
| 0012 | [Update checks, and what leaves the user's machine](0012-update-checks.md) | openDash asks GitHub what the newest release is, sends nothing about the user, and never installs without being told |
| 0013 | [openDash lights hardware, and the flag box is where it starts](0013-lighting-hardware.md) | An 8x8 matrix profile is build output like a package, but the user imports it rather than the plugin installing it |
| 0014 | [The shift model is the car's own](0014-the-shift-model.md) | iRacing publishes the car's shift-light RPMs and SimHub ignores them; one definition drives the bar, the arc, the flag box and the strip. Amends 0004 |
| 0017 | [The car's own lights, from a table openDash does not carry](0017-the-cars-own-lights.md) | The pattern and the colours are mirrored too, from a table the plugin fetches and openDash never ships. Amends 0014, reopens 0009, extends 0012 |

## Reserved

Three tickets claimed 0006 at once, which is how these came to be assigned in one place. A number
is taken when the ticket is opened, not when the record is written, so that two records cannot
collide.

It happened again on 2026-09-13, and worse: four tickets, two numbers. XOR-142 and XOR-225 both
claimed 0013, and XOR-146 and XOR-230 both claimed 0014, all four opened within an afternoon of
each other. The two lighting records keep the numbers, because they were the pair actually being
written when the collision was found; the two Car themes records move to 0015 and 0016. Any ticket
body still naming the old number is amended rather than rewritten, which is why a reader may find
"ADR 0014" in XOR-146 and XOR-147 meaning the record now numbered 0016.

| | | |
|---|---|---|
| 0007 | The second screens, written down after the fact | XOR-67 |
| 0010 | Where alert priority is decided | XOR-56 |
| 0015 | What a car theme is, and the scope line it has to move | XOR-142 |
| 0016 | How several hundred themed packages reach the user | XOR-146 |

## Writing one

Context, then the decision, then what it costs. Alternatives considered belong in it: a record
that lists only the option taken is a summary of the code rather than a decision.

Say plainly what is still unresolved. ADR 0006 ends with the round faces because the answer is
genuinely not known, and a record that pretends otherwise is worse than one that admits it.
