# Voice in user-facing text

**Last updated:** 2026-09-21
**Status:** agreed, and applied across the settings panel.

[brand.md](brand.md) settles what openDash looks like. This document settles what it sounds like,
which had never been written down and had accordingly drifted a long way: the panel had accumulated
captions arguing for the decision behind a control instead of naming the control, because whoever
wrote a caption had just finished making that decision and the reasoning was the freshest thing in
their head.

## The governing principle

> Name the setting. Say what to do. Never say how it works, and where the control already says it,
> say nothing.

Two failures hide behind the same sentence and both have to be looked for. The first is mechanism:
"openDash itself was downloaded. It is put in place when SimHub closes" describes an assembly swap
the reader has no part in, where "Restart SimHub to finish updating" names the one thing they have
to do. The second is harder, because the sentence left after the mechanism goes is usually accurate,
usually readable, and still worth deleting: "Pick your screen's real resolution. The wrong size will
not fit your screen" says nothing that a row titled "Screen size" over a list of resolutions has not
said already.

A third failure waits on the other side of both, and it is the one that comes of correcting them
carelessly. Plain is not the same as simple-minded. "What the middle shows" and "When nothing is
happening" are perfectly clear and read as though the panel were addressing a child, where "Centre
display" and "Idle display" are the same facts written as a product writes them.

## What this covers

Everything the panel puts in front of a user: a section heading, a row label, a caption, a button, a
tooltip, an empty state, a status line, the sentence said after a press, and the module and card
descriptions the panel shares with the dashboards. It also covers a message that reaches a user
through the panel rather than through the log, such as the reason an install did not finish.

It does not cover code comments, `docs/`, the decision records, commit messages or the lines written
to SimHub's log, all of which are addressed to a contributor and are free to be as long and as
argumentative as the point requires.

## The shape of a row

A row is a label, a control and, only sometimes, a caption. The three do not overlap: the control
shows the values, so the caption does not list them, and the label names the setting, so the caption
does not name it again. What is left for a caption is the thing neither of the other two can say.

**The default is no caption.** Roughly a third of the rows on the panel have none, and `Ui.Row`
takes a null caption for exactly that reason. The question to ask is not whether a caption is true
and useful, which most of them are, but whether the row would be worse without it, which most of
them do not survive.

## The label

A label is a noun phrase, written the way a specification sheet labels a figure: "Screen size",
"Rev light style", "Idle display", "Mounting side", "Centre display", "Flag display". A button is
the exception and takes a verb, since a button is pressed rather than named: "Add screen", "Install
it again", "Put mine back". Neither is ever phrased as a question.

A label carrying no caption has to be the whole answer on its own, which is usually a matter of
naming the setting rather than gesturing at it. "How big" needed a caption where "Screen size" does
not, and "At rest" needed one where "Idle display" does not.

A switch still has to make clear which way is on, and it does so by naming the thing rather than by
making a claim about it. "A car alongside lights the whole bar" puts the trigger where the reader
expects the subject, so the sentence has to be reassembled before it can be read; "Full-strip
spotter" is what the setting is, and the caption underneath says what it does.

## The values in a control

A value in a chooser is read without its label, alongside the values beside it, and it therefore has
to be a name rather than a fragment. "The car's own" is a possessive with no head noun, so the
reader has to work out what it owns, and the three values beside it were "Left to right", "Meet in
middle" and "F1", which are patterns rather than owners and so give nothing to work it out from. It
is "Car-specific" now, which is a name, and which is also what Lovely Sim Racing calls the tables
behind it, so a driver who met them there recognises the word.

Where one value needs unpacking and the others do not, the caption names it and explains that one
rather than describing the set.

> Rev light style, over ( Car-specific ) ( Left to right ) ( Meet in middle ) ( F1 )
>
> "Car-specific uses the shift lights measured for the car you are driving."

**The audience is a sim racer, and the vocabulary that audience already uses is not jargon.**
Spotter, redline, delta, stint, limiter and pit lane all stay, since the reader has met every one of
them in iRacing and in SimHub before meeting it here. What gets translated is openDash's own
invented vocabulary, which nobody has met anywhere: "band D", "the ladder", "the lamps" and "at
rest" are the panel talking to itself.

## The caption

One short sentence. Two happen where the second carries a consequence or a next step, and three is
the ceiling, reserved for a caption introducing a whole section rather than a single row.

A fact earns its place by being both actionable and absent from the screen, and the second half of
that is what catches most of them. That a screen at the wrong size will not fit is true, and it is a
consequence rather than a mechanism, and it still went, because nobody reading "Screen size" over a
list of resolutions was going to conclude anything else. What survives is the fact the reader could
not have worked out from what is in front of them: that a name also appears in SimHub's own
dashboard list, since nothing on the panel shows that list, or that a rename leaves the settings and
the bindings alone, which is worth exactly three words.

> "Only changes the name."

Every "it", "that" and "them" has to point at something the reader can see on the row, because a
caption is read on its own by anybody scanning a page of settings. "Off lights only the LED at that
end" names no end anywhere in the row, and naming it costs a few words.

> "Lights the whole strip for a car alongside, instead of just the end nearest it."

## What never appears

**Mechanism**, with no exception, and openDash's own internals as much as SimHub's. That a second
screen at one size gets a settings group of its own is a design decision of some consequence, and
what the reader needs from it is that their two screens can show different pages.

**History.** What openDash used to do, what a setting replaced and which design came first are facts
about the project rather than about the rig in front of the reader. The one exception is a line that
exists because of the history, such as the note about a migrated rig holding a dozen dashboards, and
such a line goes when it stops being true rather than when somebody dismisses it.

**Names from inside the code.** A folder under `DashTemplates`, a namespace, a resource path and a
package identifier are things the reader never types and never meets in SimHub, where a dashboard is
listed under its title, so naming one sends them looking for a row that does not exist. A repository
path is never shown at all: where a build ships nothing to install, the panel says so and says
nothing further, since nobody can open `plugin/OpenDash/Resources/README.md` and would have no
reason to.

**An editorial voice.** There is no "we", and the panel does not describe its own opinions. "A
switch you flip, not a time of day we guess at" became "Night mode" over a switch, with no caption
at all.

**Contractions.** "Does not", "cannot", "it is". The panel has always been written this way and the
consistency is worth more than the two characters.

## Messages

A message is what the panel says after something happens, and it is the one place where a sentence
is owed rather than merely allowed.

**Say the step openDash does not take.** Several actions finish half a job by design, and that half
is exactly where a user gives up: installing a profile adds it without selecting it on the device,
installing a dashboard writes it without assigning it to a display, and replacing a package leaves
an open dash window showing what it parsed when it opened. The sentence afterwards names the
remaining step, in the order it has to be done.

> "Added Rim. Restart SimHub, then assign "Rim" to this display in Dash Studio."

**A failure says what happened and where to look, without blame.** A rig with no network is a normal
rig, so "error" and "failed" are reserved for something that actually failed and are never used
about a condition the reader is not being asked to fix. Where the detail lives in SimHub's log, the
line says so rather than reproducing it.

> "Could not reach GitHub. You have 0.3.0-rc.4."
>
> "Install failed. See SimHub's log."

**An empty state names the emptiness and points at the button.** "No strips yet", beside a button
reading "Add an LED strip", is the whole of it. The rig's own empty state is the single exception,
since a rig with no screens is somebody's first minute with openDash and the card there is the only
thing on the page.

**A dialog earns its interruption with a consequence, not with a reason.** A modal takes the window
and demands an answer, so it does owe the reader something beyond the demand, and what it owes them
is what happens if they say no. The restart dialog says "until then you are running the old
version", which is what makes the interruption worth it, since the version number and the status
pill have both already moved and everything else on the tab reads as finished.

## One word per thing

The LED strips were "bars" in a section heading, "strips" in the captions under it and "bars" again
in the buttons beside them, which reads as three separate features. They are strips throughout.

The same discipline applies to verbs. A screen is **removed**, never removed in one place and
deleted in another. A tooltip says what the control does rather than instructing the reader to do
it, so it reads "Removes this screen" beside a control already labelled "Remove this screen".

## Where the words live

Copy a test can hold lives in the pure `Panel*.cs` classes rather than inline in the WPF files,
because `plugin/OpenDash.Tests` targets net8.0 and compiles none of the panel itself. This is why
`PanelCopy`, `PanelDataTab`, `PanelLights`, `PanelLightRows`, `PanelAddScreen`, `PanelPackageRow`
and `UpdateWording` exist at all, and moving a string into one of them is the whole of what it takes
to put it under test. A sentence worth arguing about is worth pinning, and `PanelCopyTests` and
`UpdateWordingTests` pin theirs character for character.

Two constraints come from outside this document. The module descriptions are mirrored between
`Modules.cs` and `packages/dash/src/contract.ts`, and `ContractTests` fails a build in which the two
disagree, so a description is edited in both places or in neither. And where
`design/canvas/Plugin.dc.html` carries a caption, the canvas is the author's and is not edited from
code; a divergence is recorded in [plugin.md](plugin.md) rather than resolved quietly in either
direction.
