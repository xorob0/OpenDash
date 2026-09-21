# Voice in user-facing text

**Last updated:** 2026-09-21
**Status:** agreed, and applied across the settings panel.

[brand.md](brand.md) settles what openDash looks like. This document settles what it sounds like,
which had never been written down and had accordingly drifted: the panel accumulated captions
arguing for the decision behind a control instead of saying what the control does, because the
person writing a caption had just finished making that decision and the reasoning was the freshest
thing in their head.

## The governing principle

> Say what to do. Never say how it works, and where the control already says it, say nothing.

Two failures hide behind the same sentence and both have to be looked for. The first is mechanism:
"openDash itself was downloaded. It is put in place when SimHub closes" describes an assembly swap
the user has no part in, where "Restart SimHub to finish updating" names the one thing they have to
do. The second is the harder one, because the sentence left after the mechanism goes is usually
accurate, usually readable, and still worth deleting: "Pick your screen's real resolution. The wrong
size will not fit your screen" says nothing the row title and a list of resolutions have not said
already.

**The default is no caption.** A row is a title and a control, and a caption is what gets added when
those two genuinely cannot carry something. Roughly a third of the rows on the panel now have none,
and `Ui.Row` takes a null caption for exactly that reason. The question to ask is not "is this true
and useful" but "would the row be worse without it", which most captions do not survive.

The reasoning still belongs somewhere, and that somewhere is a code comment, a decision record or
one of these design documents, all three of which are addressed to a reader who has chosen to read
them. A caption is read by somebody who wants to change a setting and stop reading, so every clause
that does not help them do that is a tax charged at the worst possible moment.

## What this covers

Everything the panel puts in front of a user: a section heading, a row title, a caption, a button,
a tooltip, an empty state, a status line, the sentence said after a press, and the module and card
descriptions the panel and the dashboards share. It also covers a message that reaches a user
through the panel rather than through the log, such as the reason an install did not finish.

It does not cover code comments, `docs/`, the decision records, commit messages or the lines
written to SimHub's log, all of which are addressed to a contributor and are free to be as long and
as argumentative as the point requires.

## The rules

### Never explain how it works

The strongest of these and the one most often broken, because a mechanism is what the writer has
just been thinking about. A user does not act on a mechanism, therefore a mechanism does not appear.

> "openDash itself was downloaded too. It is put in place when SimHub closes." → "openDash itself
> was downloaded too. Restart SimHub to finish updating."

> "Pick the device these LEDs are on: your wheel, a button plate, or the Arduino if you wired the
> strip yourself. SimHub keeps a separate profile list per device." → the same sentence without the
> second one.

This applies to openDash's own internals with no exception at all. That a second screen at one size
gets a settings group of its own is a design decision of some consequence; what a user needs from
it is that their two screens can show different pages.

### Address the user, in the present tense

A control is something the reader is about to use, therefore the caption speaks to them and about
what will happen. There is no editorial "we", and the panel does not describe its own opinions.

> "A switch you flip, not a time of day we guess at." → "Switch to the night brightness above."

### Delete the caption the control already writes

The control shows the values, so a caption listing them is the same information twice, and a
caption restating the title is the same information twice in a row. Both go, leaving the row as a
title and a control.

> "Which way round" + "The same screen, laid out for the way you have it mounted." → "Orientation",
> above two buttons reading Landscape and Portrait.

> "Night mode" + "Switch to the night brightness above." → "Night mode", above a switch.

What is left when a caption does survive is one short sentence. Two happen, the second carrying a
consequence or a next step, and three is the ceiling for a caption introducing a whole section.

> "One answer for every light: the box, the screens' fuel telltale and the pop-up all light when
> the laps left in the tank fall under this. Laps, not litres: litres mean nothing without the
> car." → "Warn below this many laps of fuel."

### Cut the history

What openDash used to do, what a setting replaced and which design came first are facts about the
project rather than about the rig in front of the reader. The one exception is a line that exists
precisely because of the history, such as the note explaining why a migrated rig holds a dozen
cards, and such a line goes when it stops being true rather than when somebody dismisses it.

> "The card face openDash shipped before the zones. Any card in any slot; the same card may be
> assigned twice and the panel says so without preventing it. A face with fewer slots uses the
> first ones." → "Put any card in any slot. A face with fewer slots uses the first ones."

### Use the words the user can see

A folder under `DashTemplates`, a namespace, a resource path and a package identifier are things
the reader never types and never meets in SimHub's own interface, where a dashboard is listed under
its title. Naming one of them sends somebody looking for a row that does not exist, so the panel
names what they can actually see instead.

> "This removes the screen from your rig, deletes openDash 850x480 from SimHub's DashTemplates, and
> forgets what you had set on it." → "This removes the screen from your rig, deletes its dashboard
> from SimHub and forgets its settings."

A repository path is never shown at all. Where a build ships nothing to install, the panel says so
and says nothing further, since a user cannot open `plugin/OpenDash/Resources/README.md` and would
have no reason to.

### A fact earns its place by being both actionable and absent from the screen

The second half of that is the one that catches most of them. That a screen at the wrong size will
not fit is true and is a consequence rather than a mechanism, and it still went, because the row is
titled "Screen size" over a list of resolutions and the user was never going to conclude anything
else. The same applies to the account of what a second screen at one size gets: if the interface is
clear, the sentence explaining it is not needed.

What survives is the fact a user could not have worked out from the screen in front of them. A name
also appearing in SimHub's own dashboard list is one, since nothing on the panel shows SimHub's
list. That a rename leaves settings and bindings alone is another, since nothing shows that either,
and it is worth three words: "Only changes the name."

### Say the step openDash does not take

Several actions finish half a job by design, and the half that is left is exactly where a user
gives up. Installing a profile adds it to SimHub without selecting it on the device; installing a
dashboard writes it without assigning it to a display; replacing a package leaves an open dash
window showing what it parsed when it opened. In each case the sentence said afterwards names the
remaining step, in the order it has to be done.

> "Added Rim. Restart SimHub, then assign "Rim" to this display in Dash Studio."

### A dialog earns its interruption with a consequence, not with a reason

A modal takes the window and demands an answer, so it does owe the reader something beyond the
demand. What it owes them is what happens if they say no, which is a fact about their rig, and not
an account of why the software is arranged as it is, which is a fact about the software. The
restart dialog says "until then you are running the old version", and that sentence is what makes
the interruption worth it, since the version number and the status pill have both already moved and
everything else on the tab reads as finished.

The order is what happened, the consequence, then the question, and the caption underneath does not
repeat any of it.

> "openDash 0.3.0-rc.5 is downloaded. It takes effect once you restart SimHub; until then you are
> running the old version."
>
> "Close SimHub now and start it again?"

### An empty state names the emptiness and points at the button

"No bars yet", beside a button reading "Add an LED bar", is the whole of it. What adding one will do
is the button's job to say, and a sentence spelling it out is the caption problem again in a
different place. The rig's own empty state is the one exception on the panel, because a rig with no
screens is somebody's first minute with openDash and the card there is the only thing on the page.

> "No bars yet."
>
> "Add the screen your rig has and openDash installs its dashboard into SimHub."

### A failure says what happened and where to look, without blame

A rig with no network is a normal rig, so the word "error" and the word "failed" are reserved for
something that actually failed, and neither is used about a condition the user is not being asked
to fix. Where there is a next move it is named, and where the detail lives in SimHub's log the line
says so rather than reproducing a stack trace.

> "Could not reach GitHub. You have 0.3.0-rc.4."
> "Install failed. See SimHub's log."

### No contractions

"Does not", "cannot", "it is". The panel has always been written this way and the consistency is
worth more than the two characters, particularly since half the sentences are read at a glance by
somebody who is about to go and drive.

### A title names the setting, and a switch names what being on does

A row title names the thing being set, in sentence case: "Screen size", "Rev light style", "Night
mode". A button says what pressing it does: "Add screen", "Install it again", "Put mine back".
Neither is phrased as a question. A title carrying no caption has to be the whole answer on its own,
which is usually a matter of naming the setting rather than gesturing at it: "How big" needed a
caption and "Screen size" does not, and "At rest" needed one where "When nothing is happening" does
not.

A switch is the case worth its own sentence, because a switch reads as a claim and the claim has to
be the thing the switch turns on, with the subject of that claim first. "A car alongside lights the
whole bar" puts the trigger where the reader expects the subject, so the sentence has to be
reassembled before it can be understood; "Whole strip lights for a car alongside" does not. The four
switches on a matrix panel are parallel for the same reason: "Show race flags", "Show pit warnings",
"Show cars alongside", "Show car warnings", where they used to be the bare nouns "Flags", "Pit",
"Spotter" and "Warnings", none of which said whether it was a switch or a chooser.

### Every "it", "that" and "them" has to point at something on the row

The caption is read on its own, without the title, by anybody scanning a page of settings, so a
pronoun whose antecedent is in the writer's head is simply broken. "Off lights only the LED at that
end" names no end anywhere in the row. Naming the thing costs a few words: "Lights the whole strip
for a car alongside, instead of just the end nearest it."

### One word per thing, everywhere

The LED strips were "bars" in a section heading, "strips" in the captions under it and "bars" again
in the buttons beside them, which reads as three features. They are strips throughout now. The same
applies to a verb: a screen is **removed**, never sometimes removed and sometimes deleted, and a
tooltip says what the control does rather than instructing the reader to do it, so it is "Removes
this screen" beside a control already labelled "Remove this screen".

## Where the words live

Copy that a test can hold lives in the pure `Panel*.cs` classes rather than inline in the WPF
files, because `plugin/OpenDash.Tests` targets net8.0 and compiles none of the panel itself. This
is why `PanelCopy`, `PanelDataTab`, `PanelLights`, `PanelLightRows`, `PanelAddScreen`,
`PanelPackageRow` and `UpdateWording` exist at all, and moving a string into one of them is the
whole of what it takes to put it under test. A sentence worth arguing about is worth pinning, and
`PanelCopyTests` and `UpdateWordingTests` pin theirs character for character.

Two constraints come from outside this document. The module descriptions are mirrored between
`Modules.cs` and `packages/dash/src/contract.ts`, and `ContractTests` fails a build in which the
two disagree, so a description is edited in both places or in neither. And where
`design/canvas/Plugin.dc.html` carries a caption, the canvas is the author's and is not edited from
code; a divergence is recorded in [plugin.md](plugin.md) rather than resolved quietly in either
direction.

## The shape of a settings row

A row is a title, a caption and a control, and the three do not overlap. The control shows the
values, therefore the caption does not list them, except where a value needs a gloss the word on
the button cannot carry. The title names the setting, therefore the caption does not begin by
naming it again. What is left for the caption is the thing neither of the other two can say, which
is what the setting does to the rig once it is changed.
