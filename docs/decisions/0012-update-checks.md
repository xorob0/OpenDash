# ADR 0012: Update checks, and what leaves the user's machine

**Date:** 2026-09-12
**Status:** Accepted. Moves the "network update checks" line in [scope.md](../scope.md) and is the
record [ADR 0003](0003-plugin-settings-through-properties.md) leaves room for, since a plugin that
fetches is still a plugin that does not render.

## Context

[scope.md](../scope.md) refuses network update checks and says what would have to happen first:
a record stating exactly what is sent and how it is switched off. It also says that a pull request
falling under one of those lines is declined however well it is written, so this record comes
before the code rather than beside it.

The refusal is worth reversing. A user installs openDash once and then never learns that anything
improved, because there is no installer, no account and no channel back to them. Both comparable
dashboards solve this inside the plugin, and it costs no server of ours, since the releases are
already on GitHub.

Reversing it is nonetheless the first thing openDash does that touches the network at all, and it
sits directly against a stronger line in the same document: nothing leaves the user's machine. That
line is about telemetry rather than about HTTP, but the distinction is exactly the kind a user
feels lied to by if it is never written down.

## Investigation

We went and looked at what the releases actually publish, rather than assuming a feed exists.

**There is no machine-readable feed, and `releases/latest` does not answer.** `release.yml`
computes `prerelease` as `contains(github.ref_name, '-')`, and every version cut to date has been
a pre-release, so `https://api.github.com/repos/xorob0/OpenDash/releases/latest` returns 404 rather
than a candidate. A consumer has to read the list endpoint and choose, which it must do anyway for
the reason below.

**`build/manifest.json` is not attached to a release.** Only `build/*.simhubdash` and
`build/OpenDash-plugin.zip` are. The manifest, the fonts and the bare DLL are workflow artefacts
that no consumer can fetch, so the release itself is the only feed there is.

**The release body is GitHub's generated summary, not the changelog.** `generate_release_notes:
true` means the body is a list of commits and pull requests whose shape this repository does not
control, and `CHANGELOG.md` never reaches it.

**Asset names contain spaces**, for instance `openDash Pit wall portrait.simhubdash`, and GitHub
rewrites them on upload. A consumer must match on what the API reports rather than on the name on
disk.

**The comparison already exists and is already tested.** `Versioning.VersionCompare` implements
semver precedence including pre-release ordering, and `Versioning.Decide` turns a pair of versions
into an `InstallStatus`. One sharp edge was found while reading it: an undotted numeric identifier
is compared ordinally rather than numerically, so `0.2.0-rc10` is reported as older than
`0.2.0-rc2`. Pre-release tags must therefore be cut in the dotted form `rc.10`, or that comparison
fixed, before an eleventh candidate exists.

**net48 inside SimHub constrains how the request may be made.** `api.github.com` answers 403 to a
request with no `User-Agent`, and none of the available clients sends one by default.
`ServicePointManager.SecurityProtocol` is process-wide and shared with SimHub and every other
plugin, so it may be widened with `|=` and never assigned. An unobserved exception on a thread-pool
thread terminates the process on .NET Framework, which would make SimHub disappear without a
dialog. Nothing may join `Init`'s synchronous path, and nothing may block on a task from the thread
SimHub calls `Init` on.

## Decision

**openDash asks GitHub what the newest release is, sends nothing about the user, never installs
anything without being told to, and can be switched off before it ever asks.**

Point by point, because the point of this record is that each is written down.

**What is fetched, and from where.** An anonymous HTTPS GET to
`https://api.github.com/repos/xorob0/OpenDash/releases`, the list endpoint rather than
`releases/latest`, which does not answer while every release is a pre-release. The response gives
the tag, the body and the asset download URLs, which is everything both the check and the one-click
update need. There is no openDash server, and there will not be one for this.

A user running a stable version is not offered a pre-release. A user already running a pre-release
is, since they have opted into that by installing one.

**What is sent.** Nothing beyond what an HTTPS request unavoidably carries: the user's IP address,
reaching GitHub and not us. No identifier, no installation id, no machine fingerprint, no usage
counting, no error reporting. The mandatory `User-Agent` names the product and its version,
`openDash/<version>`, because the API rejects a request without one; it says what the software is
and nothing about who is running it. Nothing is logged anywhere but the user's own SimHub log.

This is the whole of it, and it is deliberately small enough to state in one sentence in the panel.

**How often, and how it is turned off.** At most once per SimHub start, never on the startup path,
and not at all within twenty-four hours of the last answer, which is remembered between runs. A
manual check is always available in the panel and ignores that interval, because a user who presses
a button has asked.

One setting governs it, read before the first request is constructed rather than before it is sent,
so that turning it off means nothing is fetched at all rather than fetched and discarded. It is on
by default. That is the aggressive choice of the two and it is taken deliberately: the entire
problem is that users never learn about improvements, an off-by-default check would be found by
nobody, and what the check discloses is an IP address to GitHub, which is what visiting the
repository's own page discloses.

**With no network.** The check fails to a single informational line in SimHub's log and to no
change at all in the panel, which goes on showing the installed version. No dialog, no repeated
retry, no wall of exceptions, and above all no delay to SimHub's start, since the request never
runs on the thread that starts it. A rig with no network is a normal rig rather than an error.

**Whether openDash installs by itself.** It does not, ever. The check reports, and a person
chooses. This is the line that matters most for something a driver relies on mid-season, and it is
why XOR-106 is a button rather than a background updater. An update applied without asking is
indistinguishable, from the seat, from the dashboard breaking.

## What would reopen this

**The default.** On-by-default is the one line here that a reasonable person lands on the other
side of, and it is the one to revisit first if anybody objects. Nothing else in the record depends
on it: every mechanism above works identically with the default flipped.

**A second thing to fetch.** This record authorises fetching release metadata and release assets,
and nothing else. Anything that sends information rather than requesting it, and anything that
fetches per-user rather than per-release, is a new decision and not an extension of this one.

**A rate limit that bites.** The anonymous GitHub API allows sixty requests an hour per address,
which one check a day per user cannot approach. A shared address behind a large NAT could, and the
answer would be caching a static file rather than an openDash server.

## Consequences

### Good

XOR-31 and XOR-106 become writable, and with them the cheapest distribution improvement available:
a user who already has openDash installed is exactly the user a dashboard manager does not help.
The promise that a `.simhubdash` is a complete product on its own is untouched, since a package
still installs and renders with no plugin and no network.

What is sent is small enough to state in full, which is the property that makes the reversal
defensible rather than merely convenient.

### Bad

openDash now has a network path, and with it a class of failure it did not have: a hung socket, a
proxy that intercepts TLS, a corporate network that blocks GitHub. Each has to fail quietly, and
quiet failure is harder to write and easier to get wrong than a loud one.

The changelog a user is shown is GitHub's generated summary rather than the curated `CHANGELOG.md`,
because that is what the release body holds today. It is honest but it is not good, and making the
release body the changelog is a change to `release.yml` that belongs with XOR-25.

`scope.md` loses one of its four standing refusals, which leaves three.

### Unresolved

Whether the check should also look at the plugin's own version, or only at the dashboards. The
mechanism is identical and the consequence is not: a plugin update requires the user to replace a
DLL and restart SimHub, which is a considerably larger ask than reopening a dashboard, and XOR-105
owns that question.

Whether the twenty-four hour interval is right. It is chosen for being obviously not aggressive
rather than from any evidence, and nothing depends on the exact figure.
