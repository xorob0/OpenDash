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
| 0009 | [Does the plugin compute?](0009-does-the-plugin-compute.md) | No, and it turned out not to need to: SimHub already publishes almost all of it |
| 0011 | [How far personalisation reaches](0011-personalisation.md) | Colour is a runtime setting, geometry is a build input, and nothing regenerates a package locally |

## Reserved

Three tickets claimed 0006 at once, which is how these came to be assigned in one place. A number
is taken when the ticket is opened, not when the record is written, so that two records cannot
collide.

| | | |
|---|---|---|
| 0007 | The second screens, written down after the fact | XOR-67 |
| 0008 | How a pull request renders a dash | XOR-18 |
| 0010 | Where alert priority is decided | XOR-56 |
| 0012 | Update checks, and what leaves the user's machine | XOR-29 |

## Writing one

Context, then the decision, then what it costs. Alternatives considered belong in it: a record
that lists only the option taken is a summary of the code rather than a decision.

Say plainly what is still unresolved. ADR 0006 ends with the round faces because the answer is
genuinely not known, and a record that pretends otherwise is worse than one that admits it.
