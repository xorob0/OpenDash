# ADR 0013: openDash lights hardware, and the flag box is where it starts

**Date:** 2026-09-13
**Status:** Accepted. Adds a fourth kind of artefact to "What ships" in [scope.md](../scope.md)
and is the record [#276](https://github.com/xorob0/OpenDash/issues/276) asked for. Depends on
[the format research](../research/simhub-leds-format.md), which is what made it answerable.

## Context

openDash ships fourteen packages and a plugin, and every one of them is a screen. Nothing in the
refusal list forbids shipping something else: the list covers our own renderer, DashStudio
authoring, computed telemetry, theming, idle screens, licensing, user telemetry, Lovely's design
and sims other than iRacing, and an LED profile is none of those. But "What ships" says three
kinds of screen, and that sentence is the product. It has to gain a clause before a
`.ledsprofile` lands in `build/`, for the same reason the zone face changed this document first.

The case for doing it at all is that openDash already owns the content. The flag colours are in
`design/tokens.json`, decided once and deliberately — `purpose.flag.yellow` is not `caution`
amber because the two sit side by side on a yellow-flag lap, `flag.black` is drawn outlined
because black on black is nothing, `flag.debris` is yellow with danger stripes. The alert
catalogue is being written as one ordered list for the face, the companion and the pit wall. A
64-pixel box is that same list with a different renderer, and a project that has already decided
what yellow means and which flag wins is most of the way to lighting a box with it.

The case against is that a screen and a device are not the same kind of thing, and the difference
is the whole of the third decision below.

## What is decided

### The flag box goes first, and nothing else is promised

The hardware is an **8x8 WS2812b matrix in a printed box** — 64 addressable LEDs on one data pin,
sold ready-made by several small makers, printed by plenty of people from the free models, and
driven natively by SimHub. It is the cheapest piece of sim hardware a dashboard project can
meaningfully light up.

Daniel Newman Racing ships twelve profiles across RPM strips, wheels, button boxes, brows, matrix
flag boxes and ambient lighting. openDash starts with one of those six and says so. The rest —
the strips, the brows, the wheel buttons, the ambient lighting — are real and are on the backlog,
and this record does not claim them. Each one is a different device with a different container
vocabulary, and the honest position is that we have shipped none of them.

### A profile is build output, not a file anybody edits

[The format research](../research/simhub-leds-format.md) settles that a matrix profile is plain
indented JSON with no `$id`, `$ref` or `$type`, that its condition door is the same
`ExpressionValue` class every `.djson` binding uses, and that a 64-pixel picture is one string per
frame. All of that is generatable.

So [ADR 0002](0002-djson-generated-from-source.md)'s reasoning applies unchanged and this record
adds nothing to it: the profile is emitted by `bun run build`, colour comes from
`design/tokens.json` resolved to a literal at build time, every property read goes through
`contract.ts` so the validator can reject a profile that reads a property the plugin does not
attach, and **a profile edited in SimHub's LED editor is not a contribution**. The alternative —
a hand-authored file with hex values in it, reviewed by reading JSON — is exactly what
`design/tokens.json` exists to prevent, and it was rejected for that reason rather than for
effort.

### The plugin does not install it. The user imports it.

This is the decision that is not inherited, and it goes the other way from the packages.

A package installs itself: `PackageInstaller` compares versions and extracts into
`DashTemplates/`, a folder SimHub reads and nothing else writes. A profile has no such folder. On
the Arduino path every profile the user owns lives *inside*
`PluginsData/Common/ArduinoRGBMatrixSettings.json`, a single file that `RGBMatrixDriver` reads
when it is constructed and **writes back whenever anything changes**. Installing a profile means
merging into that file, and the file belongs to another component that will overwrite it.

That is enough on its own — a write we cannot sequence against is a write that loses somebody's
other profiles — but the consent argument would decide it even if the file were safe. A profile
attaches to a device the user owns and paints it. Silently changing what somebody's hardware does,
because they installed a dashboard, is a larger liberty than installing a dashboard, and
[ADR 0012](0012-update-checks.md) already argues that openDash asks rather than assumes.

So:

* `bun run build` writes the profile into `build/` beside the fourteen packages, and
  `bun run package` embeds it in the plugin the same way.
* The plugin **extracts it to a folder and stops there.** It never writes
  `ArduinoRGBMatrixSettings.json`, never merges into a profile list, and never touches a profile
  the user has edited.
* The lights page in the settings panel says where the file is and what to do with it: SimHub's
  own import, one click, on a device the user has already had to configure by hand.
* Updating is the same act. The plugin refreshes the extracted file and says the version changed;
  it does not reach into SimHub's settings to update a profile in place.

The cost is a manual step openDash does not impose anywhere else, and it is a real cost: some
people will never take it, and the box will sit dark for them. That is accepted. The alternative
was writing into a file SimHub owns, on hardware the user did not ask us to touch.

This is narrower than [#198](https://github.com/xorob0/OpenDash/issues/198), which turns the installer
into a picker for themed packages. A picker chooses among things openDash installs; the profile is
not one of them, so it does not appear in the picker and #198 does not need amending.

### A profile works without the plugin

The standing rule holds: every property read is wrapped in `isnull()` with its default, so a user
who imports the profile and never installs the plugin gets a working flag box with the default
settings. The research confirms this is expressible — the condition door takes NCalc and
`isnull([OpenDash.FlagBoxCriticalOnly], false)` evaluates in a profile exactly as in a dashboard.

A package has to be a complete product on its own. A profile is held to the same standard or it is
not shippable.

### iRacing, and flags are not universal

Flags are more tempting to over-claim than a dash, because they look like something every sim has.
They are not. The per-game handling that competitors carry for a dozen sims is precisely the work
this project has refused to advertise without driving it, and a flag box that lights the wrong
colour in a sim we never tested is worse than one that stays dark.

The profile reads iRacing, the same as everything else here. Where iRacing does not publish a
condition, **it is not drawn**, and it is written down as undrawable rather than approximated.

## What this costs

**A fourth artefact in the build, with no VM check behind it.** The definition of done says a
change is done when what it draws has been seen on the Windows VM in real SimHub. No 8x8 panel is
plugged into the VM, and CI owns no hardware at all. SimHub's own matrix preview is the substitute
and it is a weaker one: it proves the profile loads and the glyphs are right, not that a real
panel lights. The definition of done gains that exception explicitly, because an unstated
exception is how a claim becomes untrue.

**A device vocabulary that the generator did not have.** The matrix containers are not the strip
containers and not the dashboard items; a third serialiser is a third thing to keep correct, and
the research file records the two places it is easiest to get wrong.

**A manual install step**, above.

## What is not decided

**Whether the other five device families follow, and in what order.** This record deliberately
covers one. If the flag box works and people ask, that is the argument for the next one; it is not
made here.

**Whether the profile should ever be installed automatically**, if SimHub later gains a folder for
matrix profiles the way it has one for dashboards. The reasoning above is about the file that
exists today, and it would be worth reopening rather than inheriting if that changes.

**Whether a plugin-supplied effect is better than a generated profile.** SimHub's LED SDK lets a
plugin register its own container, which would put openDash's drawing code in C# and make the
profile a thin reference to it. It is rejected for now because the profile then only works where
the plugin is installed, which contradicts the rule above — but it is the obvious answer if the
generated tree ever gets too large to read.

---

## Amended, 2026-09-13: the plugin installs it after all, from a button

**What moved.** The decision above says "the plugin **extracts it to a folder and stops there**",
and the reason given is that a matrix profile lives inside
`PluginsData/Common/ArduinoRGBMatrixSettings.json`, which `RGBMatrixDriver` rewrites whenever
anything changes, so a write we cannot sequence against would lose the user's other profiles.

That reasoning was correct **about the file** and wrong about the conclusion, because it assumed the
only way in was the file. It is not. SimHub exposes the whole chain publicly, and every type is in
`SimHub.Plugins.dll`, which the plugin already references:

```csharp
PluginManager.GetInstance()                     // PluginManager.cs:1274, public static
  .GetPlugin<SerialDashPlugin>()                // PluginManager.cs:2579, public
  .Settings.RGBMatrixDriver                     // SerialDashPluginSettings.cs:271, public
  .Settings                                     // RGBMatrixDriver.cs:137, public MatrixSettings
  .AddProfile(profile)                          // ProfileSettingsBase.cs:825, public
driver.SaveSettings()                           // RGBMatrixDriver.cs:312, public
```

No reflection, no internals, and **openDash never opens the settings file**. It hands SimHub a
profile object; SimHub serialises its own in-memory collection to its own file, exactly as it does
when the user imports one through its UI. The clobbering problem does not arise, because there is no
second writer.

The claim that this compiles is not an argument from reading decompiled source: the chain above was
written into the plugin and built against `plugin/lib/SimHub.Plugins.dll` before this amendment was
written.

**What did not move.** The consent half of the original decision stands, and it was always the better
half of it. A profile paints hardware the user owns, and installing one because they installed a
dashboard is a larger liberty than installing a dashboard. So:

* the plugin still does **not** install anything at startup;
* it installs when the user presses **Install into SimHub** on the Lights page, and not before;
* it matches its own profile by `ProfileId` and touches nothing else in the list, so a profile the
  user made is never at risk;
* an update is offered only when the version differs, is labelled **Update in SimHub**, and the panel
  says in words that updating replaces the copy in SimHub including any changes made to it there.

**What this costs.** Matching by `ProfileId` means a copy of ours that the user has since edited in
SimHub keeps our id, so pressing Update discards their edits. The dashboards can tell an edited
folder from an untouched one and hold it back; a profile inside a settings blob gives us nothing to
fingerprint, so the panel warns instead. That is weaker, and it is the reason the button never
presses itself.

**The file is still written.** Extraction to `SimHub/OpenDash/` stays, for three reasons: it is the
fallback when the matrix driver cannot be reached (an older SimHub, or the serial dash plugin absent),
it is what a user copies to a second machine, and it is what somebody inspects when they want to see
what openDash is asking their hardware to do.

**The strongest argument against this amendment** is that it puts openDash inside another plugin's
object graph, which is a larger surface to break on a SimHub update than a file whose format we had
already reverse-engineered. That is true. It is mitigated by every call being null-checked and
wrapped, by the failure mode being "the button says SimHub's matrix settings are not available, here
is the file" rather than an exception, and by the file path remaining the documented fallback.

**Two things the investigation turned up that the amendment above depends on.**

*The file is not rewritten "whenever anything changes".* It is read once, when `SerialDashPlugin`
constructs its driver, and written at four call sites — teardown, `Dispose`, and two unrelated
Arduino text-screen handlers. So the original ADR overstated the mechanism as well as drawing the
wrong conclusion from it. The real failure of a file merge would not have been losing the user's
profiles; it would have been openDash's write being silently reverted at the next teardown. The
in-process route avoids both, because SimHub stays the only writer.

*Installing is not selecting.* `AddProfile` appends to the list. Which profile is live comes from
SimHub's own persisted `activeProfileId`, so after installing, the box still runs whatever it was
running. The panel and the guide both say to pick it on the device; openDash does not switch it,
because which profile a user's hardware runs is theirs to choose.

## Amended, 2026-09-18: there is no such thing as "SimHub's LED profiles"

The amendment above says the plugin hands a profile to `SerialDashPlugin.Settings.RGBLedsDriver` and
lets SimHub save its own settings. That is right about the mechanism and wrong about the target,
because it names one device and calls it SimHub.

`SerialDashPluginSettings.RGBLedsDriver` is `new RGBLedsDriver("PluginsData\Common\ArduinoRGBLedsSettings.json")`:
the **Arduino RGB LEDs** device, and nothing else. Every other set of LEDs SimHub drives has a
driver of its own — each wheel, button plate or brow that SimHub knows as a device is a
`LedModuleDevice` holding a `LedModuleSettings` with its own `RGBLedsDriver`, its own `LedsSettings`,
its own profile list and its own file. A profile in one list is invisible in every other.

So a bar added for a wheel was parsed, added, saved and verified correctly — into a list the wheel
does not read. Reported from a rig as a 3-9-3 bar named "wheel" simply not being in the wheel's
profile list, which is exactly what it was. The install reported success, because it was successful;
the mistake was upstream of it, in believing there was one list to succeed into.

**A bar therefore names its device, and the panel asks.** `LedBar.Device` holds the answer,
`LedTargets` resolves it against SimHub, and `LedTargets.All()` is the list the picker offers:

```
PluginManager.GetInstance().GetPlugin<DevicesPlugin>()
  .GetDevices<LedModuleDevice>()            // flattens composites: a wheel that is an LCD *and*
                                            // a LED module yields both
  .ledModuleSettings.LedsDriver.Settings    // that device's own profile list
```

all public, no reflection, the same shape as the chain the first amendment established. Saving is
two calls, because SimHub stores these two ways: a module built with a file name has a
`SettingsFileName` and `SaveSettings()` writes it, and one built without is serialised into its
device's own settings by `DevicesPlugin.SaveSettings()`. Which of the two a device is cannot be read
off the public surface, and both are what SimHub does itself, so both are called.

A bar written before this existed reads as the Arduino's, because that is where those bars actually
went. It is a statement about the past rather than a default anybody chose; a new bar takes what the
picker offered, which is the one LED device when there is one and a choice when there is not.

**What this does not fix.** The flag box matrix has the same shape of problem: `LedModuleSettings`
carries a `MatrixDriver` as well, so a matrix built into a wheel is a list openDash still cannot
reach, and the Install tab's matrix row still means the Arduino's. It is not the reported bug and
the machinery here is what it will be built on. [#363](https://github.com/xorob0/OpenDash/issues/363).

A device's LEDs are also not the only ones out of reach: a bitmap display device carries a
`LedsDriver` of its own (`BitmapDisplaySettings.LedsDriver`), and `BitmapDisplayDevice<T>` is
generic with no non-generic interface exposing its settings, so those are reached by naming every
concrete settings type or not at all. None is offered today.
