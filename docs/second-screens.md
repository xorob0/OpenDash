# The companion and the pit wall

OpenDash draws three kinds of screen. The **face** is the one on the wheel: a rev bar, a bar of
settled values, three zones across the body and a band at the foot, each zone cycling its own
catalogue from a wheel button, described in [design/zones.md](design/zones.md) and summarised in
[scope.md](scope.md). The **companion** is a phone or tablet beside it showing one module at a
time. The **pit wall** is a big screen for someone who is not driving.

All three are built from the same source and installed by the same plugin. This document is what
the second screens are, how they are put together, and what they deliberately do not show.

## The module

A module is a function of a rectangle, a density and a shape:

```ts
(ctx: { frame: Rect; density: Density; prefix: string; shape?: Shape }) => Item[]
```

`Density` is `'companion' | 'zone' | 'compact' | 'wide'`, in
`packages/dash/src/second/density.ts`. `compact` is the ramp a zone gets when it is too small for
the zone ramp, and `densityForBox` is what chooses. `Shape` is a pair of bands, width and height,
in `packages/dash/src/second/shape.ts`; it is derived from the frame when the caller does not pass
one, which is every caller today. Density says how large the type is, shape says how much of the
page fits.

That is the whole design. Nothing in a module knows which screen it is on: the density carries the
type ramp (116 / 64 / 46 / 34 / 24 on a companion page, 64 / 46 / 34 / 24 / 16 in a zone), the
gaps, the row heights and the trace length.

The boxes themselves are not written down, here or anywhere else. They are computed: a companion
page is `companionGeometry` less the padding `contentRect` takes, and a pit wall zone is the
widget rectangle the page placed less what `zoneFrame` takes for its title and its gutters.
`moduleBoxes` in `packages/dash/test/secondScreens.test.ts` derives all seven the packages produce
and builds every module into each of them, so running the test is how to see the list. Those sizes
were once literals in that test and every one had drifted taller than the real box, which made the
test read as a stronger guarantee than it was; a size copied into this document would be the same
mistake with nothing to catch it.

Rows of fields shrink their gaps and then wrap, so a row of three lap times is one line on the
850 px companion and two on the 480 px portrait one. No module has a portrait variant.

There are twenty-one modules; `packages/dash/src/contract.ts` lists them and the plugin mirrors
the list. Module 17 draws the gear alone: a module shows one thing, which is the rule the dash
face follows too, and the speed has the speedo module.

## The companion

One dashboard, twenty-one screens, one per module in catalogue order. Each screen carries a
header (module name, page counter, position, lap), the module, a row of page dots and a compact
flag band.

Which screens exist is a plugin setting. Each screen's `ScreenEnabledExpression` reads its own
property, `isnull([OpenDash.CompanionModule07], 1)`; SimHub treats a screen as enabled when the
expression is above zero and removes a disabled one from its Next/Previous ring. So turning eight
modules off means paging through thirteen.

Every screen carries the same roles: in game and idle, not pit. SimHub only filters screens by
role when the roles differ between them, so identical roles keep every enabled screen navigable
whatever the game is doing, which is what a companion is for. Paging is a wheel button bound to
the device's own `NextScreen` action in SimHub, not something OpenDash can do from the dashboard.

## The pit wall

Three landscape pages and one portrait page:

| Page | What it is |
|---|---|
| Race | The field on the left with sectors, stints and stops; the driver's own session, delta, lap data and the track on the right; two zones under them |
| Tower | A compact field list, a large track map, one wide zone and two standard zones |
| Telemetry | Speed, RPM, pedals and steering traced over the last minute, with three zones beside them |
| Portrait | The field above, session and lap data in the middle, four zones below |

### The header, and the flag

Every page draws one 64 px strip: the wordmark and the page name on the left, and on the right,
laid out from the right edge inwards, the two clocks, the wind, the track state, the incident
count, the time left and the session and lap. Each group names itself before its value, which is
not decoration: the strip once wrote its clocks the other way round, as `14:32 LOCAL 15:07 SIM`,
and a rig reported being unable to tell which was the wall clock and which was the sim's.

**The flag is not on the strip.** It was, as a colour block and a word built from the six flags
SimHub normalises, and it did not light on a rig; a 24 px block in the corner of a 1920 px header
would not have been where anyone looked for a flag even when it did. `OpenDash.PitWallFlagFormat`
replaces it with the companion's three answers -- `off`, `band` or `full` -- drawn from the full
fifteen-condition catalogue the face uses. A band is the header's own height, directly under it;
`full` takes the body. Neither covers the header, because "which page is this and how long is
left" is the question somebody asks immediately after seeing a flag.

The default is `band` and not the companion's `full`. A companion is a phone showing one module,
so a full-screen flag costs one list; a pit wall is a board, a track map and four zones that
somebody is watching *because* of the flag, and covering them the moment a yellow comes out hides
the cars the yellow is about.

### Zones

A zone is a widget over a small dashboard that holds every zone page as a screen, with the
widget's screen index bound to the zone's plugin property. It is the same mechanism the dash face
uses for its own zones, and it works for the same reason: a widget's `InitialScreenIndex` can be
bound, so a setting change moves a zone to another page without touching a file. The difference is
who moves it, and it is the reason the two catalogues have stayed apart: a pit wall zone is chosen
with a mouse by somebody who is not driving, and a face zone is cycled with a thumb mid-lap.

**A wide zone names what the extra width buys.** Three of its six pages draw more than the standard
zone's rather than the same thing larger, so the catalogue calls them "Lap history · delta to best",
"Opponents · best and last" and "Tyres · psi and kPa", where the standard zone keeps the bare module
name. A page that gains nothing from the width, the inputs trace and the web view, keeps its own
name in both catalogues.

One zone dashboard exists per distinct zone rectangle a package uses, because a widget scaled to a
box it was not drawn for would scale its type with it. The zone dashboards are derived from the
widgets the pages actually placed, so a page that moves a zone cannot leave a dangling file.

### Tables

The leaderboard, the relative and the lap history are one row definition that SimHub stamps N
times: a `Layer` with `Repetitions` set, whose children address their own row through
`repeatindex()`.

The "is there a car on this row" test lives on a child layer, not on the repeated layer itself.
SimHub evaluates a child of a repeated layer inside that copy's repeat context, whereas the
repeated layer's own `Visible` is evaluated once, for row one, and would hide or show every row
together.

Rows are one continuous list in leaderboard order with a class chip on each. SimHub exposes
per-class rows only for the player's own class, so a block per class would be a picture of data
that is not there.

#### What `class` means on a list page

`OpenDash.PositionMode` set to `class` filters the rows as well as numbering them. Every page that
lists other cars therefore lists the player's own class: the leaderboard, the relative and the
opponents page, on a face zone as on the companion and on the pit wall, and band D's relative
alongside them. One could think that the mode is a readout setting and that the field should stay
visible under class numbers. In reality a column of class positions over the whole field draws
three cars called P1 in an order that is not the order of any of the numbers, which is not a
leaderboard; the numbers a column shows and the cars it shows them against are one question and
are answered together.

A zone carries a filter of its own, which is a different question and stays one. `ZoneBClassOnly`,
`ZoneCClassOnly` and the pit wall's `PitWallClassOnly` say who is in the list without saying how
they are numbered, so a zone filtered to one class while the rig counts overall lists that class by
its overall places, which on a multi-class grid is a legitimate thing to want. A list is filtered
when either answer is yes, and `rowsInClass` in `second/values.ts` is where the two meet.

The filter is a lookup swap rather than a row set built somewhere else: SimHub has a class-only
twin of each of the two functions a table addresses its rows through, so the same rows are drawn
either way and only the car each one carries moves. The rows a short class leaves over are hidden
by the "is there a car on this row" test above rather than drawn empty.

The Gap and the Int columns move with the rows, because both are measured against a car above the
row and that car has to be one the list draws. On a filtered list the Gap is to the leader of the
player's class, whose own row reads `Lead`, and the Int is to the row above it on the list rather
than to whatever car the leaderboard puts in between. Measured the other way a class running a lap
behind the overall leader reads `+1L` on every row and `Lead` on none, which is a column carrying
no gap at all. SimHub publishes neither figure for a class, so both are differences of the two gaps
to the overall leader that it does publish.

The ± column follows the numbering for the same reason, a places-gained figure being the movement
of the place the column beside it shows. Counting in class it reads SimHub's `PositionGainClass`
instead of its `PositionGain`, so that a car which has climbed three places overall and one within
its own class does not draw the one figure against the other number.

The round faces read the same setting from `cards/position.ts` and are unaffected, there being no
rows on a card to filter: the position and the count it is shown out of are both in class, which is
the reading that setting has always given.

`packages/dash/test/positionMode.test.ts` holds the two to each other. It evaluates the formulas
the build writes against a six-car, three-class grid and reads the position column downwards, and
it reads the race board's own ± cell beside it on a grid whose starting order is not its running
order. The Gap and the Int are read the same way in `packages/dash/test/pitwallValues.test.ts`,
against a board whose class is interleaved with another and a lap behind it.

## What is not drawn, and why

Nothing here is a placeholder for work that is pending. Each is a value the sim does not publish.

| Not drawn | Why |
|---|---|
| Virtual energy (module 6) | A Le Mans Ultimate feature. SimHub's ERS members are never filled by the iRacing reader. |
| Damage (module 20) | iRacing publishes no damage values. SimHub's damage members are filled with zeros, so a body diagram drawn from them would show an undamaged car after a crash. |
| Track rivals (module 21) | SimHub times sectors, not segments, and has no notion of a rival's time over the stretch of track you are on. |
| Mini-sectors | SimHub has no subdivision below a sector. The sector strip has one cell per real sector. |
| Class header bands | Per-class rows exist only for the player's class (above). |
| The gain-and-loss bar on the opponents module | It needs a history of the gap, which neither SimHub nor a generated dashboard keeps. The gap itself, refreshed every frame, says the same thing. |
| Incidents per car | iRacing carries them only in the session YAML, per entry rather than per leaderboard row. Your own count is in the pit wall header. |
| Strength of field | It does not exist as a SimHub property from iRacing. The catalogue draws it in the session module's third rank, which is therefore built two fields wide rather than three. Track state, listed here until the track module drew it, does exist: `TrackGripStatus` is what band D and the track page's header both bind. |
| Square car markers, and the two coloured cars beside the grey ones (module 13) | SimHub's `StaticMapItem` carries one player style and one opponent style, each a dot with a radius and a colour, and nothing about shape. Every opponent is therefore the same grey dot: the catalogue's blue and purple markers would need a per-car colour the item does not expose, and class colours, the one per-car colour it does expose, are deliberately off. |
| Car rectangles, the lane grid and a red outline on the threatening car (module 12) | SimHub's `RadarItem` exposes a scale, a player dot style and an opponent dot style. It draws no grid of its own, draws every opponent alike, and has no notion of a threat, so the cars are dots and the grid lines beneath them are rectangles the module draws itself. |
| Spotter arrows (module 12) | SimHub has no path, so an arrow can only be a picture, and a picture carries no colour: a flank that is dim when the spotter is quiet and red when it calls would be two files a side. The flanks are rectangles that turn red on the side the spotter is calling, which reads better at a glance than a 46 px arrow would and carries the same meaning without the shape. |
| The steering dial (module 10) | The catalogue draws a 96 px arc with a dot on its rim, turned by the wheel angle. SimHub draws no arc, and `Rotation` is a number written into the package rather than one of the properties a formula can drive, so nothing on the page can turn. The angle is drawn as a marker running along a track of the same width instead, since `Left` does bind. |
| Fuel used this stint (module 18) | SimHub publishes the last lap's consumption and the current lap's, and no figure at all for what the tank has given since the stop. Laps since the stop multiplied by the rolling average is an estimate wearing a measurement's label, so the field is left out rather than approximated. |
| Per-lap fuel, and the fuel target drawn over it (module 19) | The previous-lap family carries ten lap times and their deltas to the session best, and no consumption beside them. Keeping one per lap would mean remembering between frames, which [decisions/0009-does-the-plugin-compute.md](decisions/0009-does-the-plugin-compute.md) refuses; the lap history therefore draws that delta where the catalogue draws fuel. |
| Round caps and round joins on a trace | `ChartItem` carries a colour, a thickness and a sample count, and nothing about how a line ends or how it turns, so every polyline the canvas draws round is drawn square and mitred here. It is the format rather than a setting left unset, and the only place it shows is a pedal at full application, where the canvas rounds the plateau and the build corners it. |
| The licence badge and its safety rating, on a list row and on the opponents identity row | iRacing carries the licence in the session YAML and no reader for it has been verified, so the table declares the column and draws nothing in it rather than inventing a letter. The catalogue's 12 px `B` at the `tall` opponents shape is that badge, which is why the shedding table keeps the last lap there instead. |
| The nationality flag beside a driver, 20 by 14 | A picture rather than text, so it waits on the image assets the flag box is waiting on: one file per country, each with the licence that has to travel with it. |

Three of those (energy, damage, track rivals) ship as modules that say so, off by default, because
the data exists in other sims and the module should be there when someone runs one.

## Where the code lives

```
packages/dash/src/second/     density, fields, chips, gauges, traces, tables, wheels, sectors, headers
packages/dash/src/modules/    the 21 modules, plus the web view and the wide car-telemetry page
packages/dash/src/screens/    the four packages: companion, companion portrait, pit wall, pit wall portrait
```

`packages/dash/test/secondScreens.test.ts` measures every text of every package against its box,
checks that no item leaves its canvas, and proves the screens, the zone widgets, the repeat layers
and the settings contract hold.
