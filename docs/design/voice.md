# Voice in user-facing text

**Last updated:** 2026-09-21
**Status:** agreed, and applied across the settings panel.

[brand.md](brand.md) settles what openDash looks like. This document settles what it sounds like,
which had never been written down and had accordingly drifted: the panel accumulated captions
arguing for the decision behind a control instead of saying what the control does, because the
person writing a caption had just finished making that decision and the reasoning was the freshest
thing in their head.

## The governing principle

> Say what happened and what to do about it. Never say how it works.

Mechanism is the hardest of these to give up, because it is usually true, usually interesting to
whoever implemented it, and always the reason the sentence was easy to write. It is nonetheless the
first thing to cut. "openDash itself was downloaded too. It is put in place when SimHub closes"
tells a user about an assembly swap they have no part in, where "openDash itself was downloaded too.
Restart SimHub to finish updating" tells them the one thing they have to do. The second is shorter
and it is also more useful, which is the pattern every rule below is a special case of.

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

### A caption says what the control does, and stops

One sentence is the target and two are common, the second one carrying a consequence or a next
step. Three is the ceiling, and it is reserved for a caption introducing a whole section rather
than a single row.

> "One answer for every light: the box, the screens' fuel telltale and the pop-up all light when
> the laps left in the tank fall under this. Laps, not litres: litres mean nothing without the
> car." → "Warn when the fuel left drops below this many laps. Lights the box, the fuel telltale
> and the pop-up."

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

### A technical fact earns its place by being actionable

The test is whether the reader would do something differently for knowing it, and it is a stricter
test than it first looks, because a fact can be relevant, true and still not actionable. That the
car light tables are fetched when update checks are on is all three, therefore it went. That a
process cannot replace its own assembly is all three, therefore it went as well, from the dialog as
much as from the caption.

What survives the test is a consequence rather than a cause. A screen at the wrong size will not
fit, which is worth a clause because it is the reason to go and measure; how SimHub comes to draw
it that way is not, and the caption now says the first and not the second.

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

### An empty state is an instruction

A section with nothing in it says what adding one will do, rather than announcing the emptiness a
heading or a pill has already announced.

> "No bars yet. Add one and openDash installs its profile into SimHub, ready to select on your
> device."

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

### Titles are nouns and buttons are verbs

A row title names the thing being set, in sentence case: "Screen size", "Rev style", "Night mode".
A button says what pressing it does: "Add screen", "Install it again", "Put mine back". Neither is
phrased as a question, and a title does not repeat what its caption is about to say.

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
