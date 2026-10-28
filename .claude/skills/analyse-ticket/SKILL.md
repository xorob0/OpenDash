---
name: analyse-ticket
description: Step 2 of the sprint. Analyse one ticket functionally and technically and put the result on the ticket as a comment, either a plan or questions for Tim. Use when Tim says "analyse #N", "plan #N", when a sprint ticket has no analysis yet, or when Tim has answered the questions on a ticket.
---

# Analysing a ticket

Argument: the ticket number. The whole process is in [docs/process.md](../../../docs/process.md).
This step writes a comment on the ticket and moves a label; it writes no code and no file in the
tree. The point is that Tim can say no on the ticket before anything is built, so the plan has to be
concrete enough to disagree with.

## Read before writing

```bash
gh issue view <n> --repo xorob0/OpenDash --comments
```

Then read what the ticket leans on: the ADRs it cites under `docs/decisions/`, the design document for
its area (`docs/design/zones.md` for anything on a face, `docs/second-screens.md` for the companion
and the pit wall, `docs/sim-support.md` for a per-sim question), the tickets it links and the two or
three nearest in its milestone, and the code it will touch, found with grep rather than guessed. For a
format or a property question, [docs/research/simhub-dash-format.md](../../../docs/research/simhub-dash-format.md)
and the traces under `traces/` are the evidence; a property no trace carries is a fact to put in the
analysis. `CLAUDE.md` and `CONTRIBUTING.md` carry the constraints every plan has to respect: the
text-fit tests, `widest`, the shedding table, tokens only from `design/tokens.json`.

If the ticket already has an analysis comment and Tim has answered its questions, this invocation
turns the answers into the plan: read them, do not ask again.

## Decide which of two comments to write

**Questions**, when the functional behaviour is not settled: two readings of the ticket lead to
different work, a value or a wording is missing, the ticket contradicts a document or an ADR, the
canvas and the ticket disagree, or the ticket turns out to be already done or impossible on the
sims it names. Ask only what you cannot settle from the repository, each question with the two
answers it chooses between and the one you would take. Post it and label:

```bash
gh issue comment <n> --repo xorob0/OpenDash --body-file questions.md
gh issue edit <n> --repo xorob0/OpenDash --add-label needs-answer --remove-label analysed
```

The comment starts `## Questions before the plan, YYYY-MM-DD`. Nothing is built on a ticket with
`needs-answer`; Tim answers in a comment and invokes this skill again.

**The plan**, when the behaviour is clear. The comment starts `## Technical analysis, YYYY-MM-DD`
and has these sections, each a short paragraph or a list:

- **Understanding.** What the ticket asks, in your own words, including what it does not ask. If
  the ticket's acceptance list is incomplete, say what you would add.
- **Where it lands.** The files and functions that change, named, and the ones deliberately left
  alone. For the plugin, whether the panel changes, since the panel is built by CI and not by the
  tests.
- **Steps.** Numbered, in the order they will be committed, each a green checkpoint.
- **Tests.** Which tests change or are added, and which snapshots are expected to move and why.
- **VM check.** The package, the scenario and the screen to photograph, or the stated reason none
  is needed. A flag box profile is read in SimHub's matrix preview instead.
- **Risks.** What could turn out wrong, what is a guess, and the number taken from the canvas.

Post it and label:

```bash
gh issue comment <n> --repo xorob0/OpenDash --body-file analysis.md
gh issue edit <n> --repo xorob0/OpenDash --add-label analysed --remove-label needs-answer
```

Write both comments in the voice of the documents: sentences, no headings deeper than the ones
above, and the ticket's own words for its things. Draft them in the scratchpad, never in the tree.

Tim may answer a plan with a change; an invocation after that appends `## Amended, YYYY-MM-DD` to a
new comment rather than editing the old one, so the record reads in order.

The next step is `/work-ticket <n>`, and it refuses a ticket without `analysed`. End with the
ticket number and which comment you posted.
