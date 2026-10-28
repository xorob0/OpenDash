---
name: verify-ticket
description: Step 4 of the sprint. Review a ticket's pull request against its plan, fix what the review finds, make it green and conflict-free, and see it on the Windows VM with before and after pictures. Use when Tim says "review #N", "verify #N", "test #N on the VM", or when a sprint PR is drafted and not yet verified.
---

# Verifying a ticket

Argument: the ticket or its PR number. The whole process is in
[docs/process.md](../../../docs/process.md). This step is the one a reviewer would do before
asking a colleague to look, and it ends with the PR carrying `verified-on-vm`.

```bash
gh pr view <pr> --repo xorob0/OpenDash --json number,headRefName,mergeable,isDraft,labels,body,statusCheckRollup
gh issue view <n> --repo xorob0/OpenDash --comments
```

## Review

Check out the branch and read the whole diff against the plan on the ticket and the ticket's
acceptance list, not against the PR's description of itself. Look for: a step of the plan left
out or done differently without the PR saying so; a `widest` that does not cover what the binding
draws; a text measured in the wrong weight; a token written as a number; a shed field missing from
`shedding.ts` or from the zones table; a property read that the contract or the traces do not
carry; a snapshot refreshed without its diff being explained; a test that asserts the bug. Review
as a reviewer who wants to find the fault, and say which of the findings you confirmed by running
something.

Fix what you find on the same branch, one commit per finding, and leave anything that is a new
piece of work as a ticket with a comment on the PR saying so. If `main` has moved, merge it in,
never rebase, and resolve conflicts taking `main`'s version of shared files and reapplying the
branch's own diff. CI must be green before the VM step; a red CI is a finding.

## The VM

There is one VM and other sessions use it. Claim it and chain every VM step on the claim:

```bash
bun run vm claim "#<n> verify" && bun run shots --packages "<from the plan's VM check>" --scenarios <scenario>
```

[docs/dev-loop.md](../../../docs/dev-loop.md) is the loop. Photograph the state the plan's VM check
names, before (built from `main`) and after (the branch), the same scenario and screen. A ticket
whose plan says nothing draws needs no picture and says so in the PR instead. A capture that
misses, about one in ten, is retaken rather than reported.

Publish the pictures on an orphan branch `captures/<n>-<slug>` (a scratch worktree, `git checkout
--orphan`, `git rm -rfq .`, add the PNGs, commit, push) and comment on the PR with each pair
embedded as `![caption](https://github.com/xorob0/OpenDash/blob/<sha>/<file>.png?raw=true)`, before
beside after, and one sentence per pair on what to look at. If the plan's acceptance names a
preview thumbnail, refresh it with `bun run previews --packages ...` and commit it on the PR branch.

Then tick the Done list in the PR body with a PATCH, using `-F body=@file` and not `-f`, and label:

```bash
gh pr edit <pr> --repo xorob0/OpenDash --add-label verified-on-vm
```

Release the VM with `bun run vm release` when done. Report in the PR, as one comment: what the review found and fixed,
what it left as tickets, and where the pictures are. The next step is `/hand-over <n>`.
