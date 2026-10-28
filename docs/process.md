# How the work runs

The work runs as a sprint, the way a team would run it, and each step is a skill under
[`.claude/skills/`](../.claude/skills/) that an agent invokes by name. This page is the lifecycle;
the skills carry the commands. The engineering rules a step has to respect are in
[CLAUDE.md](../CLAUDE.md) and [CONTRIBUTING.md](../CONTRIBUTING.md), and the definition of done
is the one at the end of [scope.md](scope.md).

## The five steps

1. **Plan the sprint** (`/sprint-plan`). A few tickets are chosen from the backlog, five unless
   Tim says otherwise, ranked by release label, by bugs that show a wrong value or a clipped glyph,
   by wave order and by what finishes a milestone. The agent proposes; the sprint starts when Tim
   confirms, and the chosen tickets take the `sprint` label. Nothing else records the sprint.

2. **Analyse the ticket** (`/analyse-ticket <n>`). Before any code, each ticket is read against the
   documents, the ADRs, the traces and the code it touches, and the result goes on the ticket as a
   comment. It is one of two things. A **technical analysis** is the plan: what the ticket asks in
   the agent's own words, where the change lands, the steps in commit order, the tests and the
   snapshots expected to move, the VM check, and the risks; the ticket takes `analysed`. Or
   **questions**, when the functional behaviour is not settled: each with the readings it chooses
   between and the one the agent would take; the ticket takes `needs-answer` and waits for Tim to
   answer on the ticket. The point of this step is that Tim can say no on the ticket before anything
   is built, so a plan is concrete enough to disagree with.

3. **Work the ticket** (`/work-ticket <n>`). Only an `analysed` ticket is worked. The work follows
   the plan on its own branch named `<n>-<slug>` from `main`, committed as green checkpoints and
   pushed as it goes. A draft pull request is opened on the first push, with the house body, and the
   ticket takes `in-progress`. Where the code forces a departure from the plan the PR says so; where
   the departure changes agreed behaviour, the agent asks on the ticket instead. Several tickets at
   once are fanned out as a workflow, one agent per ticket, with the VM kept out of it.

4. **Verify the ticket** (`/verify-ticket <n>`). The PR is reviewed against the plan and the
   ticket's acceptance list by an agent trying to find the fault, and what it finds is fixed on the
   branch or filed as a ticket. `main` is merged in if it moved, never rebased. Then the VM: the
   state the plan's VM check names is photographed before and after, the pictures are published on
   a `captures/<n>-<slug>` branch and embedded in a PR comment, the Done list is ticked, and the PR
   takes `verified-on-vm`. There is one VM, so this step runs one ticket at a time under
   `bun run vm claim`.

5. **Hand over** (`/hand-over <n>`). When CI is green, the PR is conflict-free, verified on the VM
   and its body is complete, the draft is marked ready and the PR takes `needs-human-review`. Tim
   reviews and merges by hand. An agent never merges a sprint PR and never gives it `to merge`.
   Review comments are addressed on the same branch, the label is removed meanwhile and put back
   when the PR is handed over again.

## The labels, and who moves them

| Label | On | Means | Set by | Cleared by |
|---|---|---|---|---|
| `sprint` | issue | Chosen for the current sprint | sprint-plan, on Tim's word | the next sprint-plan, once closed |
| `needs-answer` | issue | A question is open on the ticket | analyse-ticket | analyse-ticket, after Tim answers |
| `analysed` | issue | A plan is on the ticket and nothing is open | analyse-ticket | work-ticket is not required to; it stays as the record |
| `in-progress` | issue | A branch and a draft PR exist | work-ticket | the merge, checked by hand-over |
| `verified-on-vm` | PR | Seen on the VM, pictures in the PR | verify-ticket | nobody |
| `needs-human-review` | PR | Tim's turn | hand-over | hand-over, while review comments are addressed |
| `to merge` | PR | Tim delegates the merge to an agent | Tim only | the merge |

A ticket is cited as `#123`; everything the steps write goes on GitHub, as a comment on the ticket
or the PR, so the record reads in order without opening a session. A comment is never edited into
something else: a change of plan is a new comment headed `## Amended, YYYY-MM-DD`, as a ticket is
amended.

## Where it stands

`/sprint-plan` with no arguments prints the open sprint: each ticket's stage and its PR. A ticket
with `needs-answer` is waiting on Tim and is listed first.
