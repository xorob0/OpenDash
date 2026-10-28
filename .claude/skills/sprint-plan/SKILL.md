---
name: sprint-plan
description: Step 1 of the sprint. Choose a few tickets from the backlog for the next sprint, or show where the open sprint stands. Use when Tim says "plan a sprint", "what should we do next", "sprint status", or names tickets for a sprint.
---

# Sprint planning

The whole process is in [docs/process.md](../../../docs/process.md). This step picks the tickets;
it builds nothing and touches no file in the tree. A ticket is in the sprint when it carries the
`sprint` label on `xorob0/OpenDash`, and that label is the only record of the sprint.

## If a sprint is open, show it

```bash
gh issue list --repo xorob0/OpenDash --state open --label sprint --limit 50 --json number,title,labels -q '.[] | "#\(.number)\t\(.labels|map(.name)|map(select(. == "needs-answer" or . == "analysed" or . == "in-progress"))|join(","))\t\(.title)"'
gh pr list --repo xorob0/OpenDash --state open --limit 50 --json number,title,labels,isDraft -q '.[] | "#\(.number)\tdraft=\(.isDraft)\t\(.labels|map(.name)|join(","))\t\(.title)"'
```

Print one line per sprint ticket saying which stage it is at (chosen, waiting on an answer, analysed,
in progress, verified on the VM, waiting on Tim) and which PR carries it. A ticket with `needs-answer`
is blocked on Tim: say so first. If Tim only asked for status, stop here.

Before planning a new sprint, clear the label from what the last one finished:

```bash
gh issue list --repo xorob0/OpenDash --state closed --label sprint --limit 100 --json number -q '.[].number' | xargs -r -I{} gh issue edit {} --repo xorob0/OpenDash --remove-label sprint
```

An open ticket still labelled `sprint` carries over unless Tim drops it; list it as carried over.

## Choosing

If Tim named the tickets, skip to labelling. Otherwise propose a set, five tickets unless he said a
number. Candidates are open issues without `do-not-do-yet`; read the bodies of the twenty or so
most likely, not just the titles.

```bash
gh issue list --repo xorob0/OpenDash --state open --limit 400 --json number,title,labels,milestone -q '.[] | select(.labels|map(.name)|index("do-not-do-yet")|not) | "#\(.number)\t[\(.labels|map(.name)|join(","))]\t\(.milestone.title)\t\(.title)"'
gh issue view <n> --repo xorob0/OpenDash --comments
```

Rank by, in this order: `first-release` and `v1.0` before `v2.0`; a `bug` that shows a wrong value or
a clipped glyph before a feature; a low wave number before a high one, since the waves are the
dependency order; tickets that share a milestone, so the sprint finishes something; and a size the
sprint can carry, since every ticket ends in a VM pass that takes about twenty minutes of the single
VM. Leave out anything that needs an ADR nobody has written (`adr` label) unless the ADR is the
ticket, and anything the scope document refuses.

Present the proposal as a short table: ticket, why now, what it depends on, and whether the analysis
will probably need to ask Tim something. Then stop and let Tim decide; a sprint starts on his word,
never on a proposal. When he confirms, label:

```bash
gh issue edit <n> --repo xorob0/OpenDash --add-label sprint
```

The next step for every ticket in the sprint is `/analyse-ticket <n>`; several can run at once, as
subagents, since analysis touches no files. Say that in your last line, with the ticket numbers.
