---
name: hand-over
description: Step 5 of the sprint. Hand a verified pull request to Tim for review and merge by marking it ready and labelling it needs-human-review, after checking it is green, conflict-free and complete. Use when Tim says "hand over #N", "ready for me", or when a sprint PR carries verified-on-vm and is still a draft. Also the step to return to after Tim's review comments.
---

# Handing over

Argument: the ticket or its PR number. The whole process is in
[docs/process.md](../../../docs/process.md). Tim reviews and merges by hand; this step only says
that it is his turn, and it must not say so too early.

```bash
gh pr view <pr> --repo xorob0/OpenDash --json number,isDraft,mergeable,labels,body,statusCheckRollup,reviewDecision
```

## The checks

All of these, or the PR is not handed over and the missing one is fixed or sent back a step:

- CI is green on the current head, and `mergeable` is not `CONFLICTING`. If `main` moved, merge it
  in, never rebase, and wait for CI again.
- The PR carries `verified-on-vm`, or the body says plainly why nothing was photographed.
- The body's Done list is ticked only where true, the snapshot line says what moved, and
  "Anything a reviewer should know" lists every departure from the plan on the ticket and every
  thing left to another ticket.
- The ticket's acceptance list is covered line by line; write the mapping into the PR comment if it
  is not obvious.
- `Closes #<n>` is in the body, so the merge closes the ticket.

## The hand-over

```bash
gh pr ready <pr> --repo xorob0/OpenDash
gh pr edit <pr> --repo xorob0/OpenDash --add-label needs-human-review
```

Tell Tim in one line per PR: the number, the title, and the one thing you would look at first.
Never merge it, never enable auto-merge, and never add `to merge`: that label is his to give.

## After his review

When Tim leaves review comments or requests changes, remove `needs-human-review`, address every
comment on the same branch, answer each thread with what changed, run `/verify-ticket` again if a
face changed, and hand over again. When he merges, the `Closes` line closes the ticket; check that
it did and that the `in-progress` label is gone (remove it if not). A ticket he does not merge is
still in the sprint until he says otherwise.
