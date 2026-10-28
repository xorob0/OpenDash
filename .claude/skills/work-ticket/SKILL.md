---
name: work-ticket
description: Step 3 of the sprint. Implement an analysed ticket on its own branch, following the plan on the ticket, and open a draft pull request. Use when Tim says "work #N", "implement #N", "do the sprint", or when sprint tickets are analysed and nothing is in progress.
---

# Working a ticket

Argument: one ticket number, or several. The whole process is in
[docs/process.md](../../../docs/process.md); the engineering rules are `CLAUDE.md` and
`CONTRIBUTING.md`, and they are not repeated here.

## The gate

```bash
gh issue view <n> --repo xorob0/OpenDash --json labels,comments -q '{labels: [.labels[].name], analysis: [.comments[].body | select(startswith("## Technical analysis"))] | length}'
```

Without the `analysed` label and a `## Technical analysis` comment, stop and say that
`/analyse-ticket <n>` comes first. With `needs-answer`, stop: it is waiting on Tim. Read the plan
and every comment after it; the latest answer from Tim wins over the plan.

## One ticket

Branch from `origin/main`, named `<n>-<three-or-four-words>`; never on `main`. Check first that no
branch or PR for the ticket exists:

```bash
gh pr list --repo xorob0/OpenDash --state open --search "#<n> in:title" --json number,headRefName
git fetch origin && git checkout -b <n>-<slug> origin/main
```

Follow the plan's steps in its order, each as its own commit when the tree is green: `bun run
check`, `dotnet test plugin/OpenDash.Tests` when the plugin changed, and `dotnet build
plugin/OpenDash/OpenDash.csproj` when the panel changed, because CI compiles it and the tests do
not. Read a snapshot diff before refreshing it and say in the commit message what moved and why.
Where the code forces a departure from the plan, take it and write it down for the PR; where the
departure changes behaviour Tim agreed to, stop and ask on the ticket instead.

Push after each commit. Open the pull request as a draft as soon as the first commit is pushed, so
the PR exists while the work goes on, with the house body from
[.github/pull_request_template.md](../../../.github/pull_request_template.md): what changed and why
in one paragraph, `Closes #<n>`, the Done list unticked except what is true, and under "Anything a
reviewer should know" every departure from the plan. The title is one sentence in the voice of the
commits on `main`, ending in `(#<n>)`.

```bash
gh pr create --repo xorob0/OpenDash --draft --base main --title "<sentence> (#<n>)" --body-file pr.md
gh issue edit <n> --repo xorob0/OpenDash --add-label in-progress
```

Keep the VM out of this step; it belongs to `/verify-ticket`. The step is done when the plan is
implemented, the branch is green and pushed, and the draft PR says what departed from the plan.

## Several tickets

Fan them out with the Workflow tool, one Opus agent per ticket in its own worktree, each given the
ticket number, its plan comment verbatim, this file's rules and the branch name. Two tickets that
the plans say touch the same files run one after the other, not at once. Never more than four `bun
run check` at a time: the host runs out of memory and `bun test` is killed, which reads as a
failure that is not one. An agent that could not get a green run but pushed its branch reports
that; only an unpushed branch is a failed lane. Say up front which model the agents run on.

The next step for each PR is `/verify-ticket <n>`, one at a time, since there is one VM.
