# CrawlerGuts

A 7 Days To Die mod. A crawler zombie is a half-eaten corpse dragging its guts across the ground,
so it **loses a little health for every block it drags itself after you** — 1 HP per block by
default, down to a floor of 10% of its max health, with a chance of starting a bleed on the way.

It costs the crawler nothing while it is idle, wandering, or chasing something that is not a
player. Only the chase is paid for.

## Installing

Download the zip from [Releases](https://github.com/johnrando/ul-crawlerguts/releases) and extract
it into the game's `Mods/`. The mod folder is the root of the archive, so it lands as:

```
Mods/CrawlerGuts/
├── ModInfo.xml
└── CrawlerGuts.dll
```

Load order does not matter, and nothing needs building.

## Console commands

`cg` prints the menu and changes nothing — `crawlerguts` is an alias. Every line names the command
that changes it and says what it is for, so the menu is also the reference:

```
CrawlerGuts is ON
  cg on|off           : [ >on< | off ]   - a crawler loses health for every block it drags itself after you
  cg dmg {hp}         : 1 health per block dragged
  cg floor {pct}      : never dragged below 10% of max health (a bleed can still kill)
  cg bleed {pct}      : 10% chance per block to start bleeding, if not already
  cg credit           : [ on | >off< ]   - drag damage and bleed count as your kill
  cg flavor fw        : [ >on< | off ]   - FletchWounds: a dragging crawler works your arrows deeper
  cg arrow {pct}      : 10% chance per block to work a FletchWounds arrow deeper, if not bleeding
```

`cg on` and `cg off` are the master switch — with it off the tick hook returns immediately, so
every other setting is inert. They say which state you want rather than toggling, so the command
reads the same whichever state you were in and repeating it is harmless.

A setter called with no arguments prints its usage and current value. **Changes are saved** — see
[Settings file](#settings-file).

Two more: `cg info` prints the same block with the patch state and counters added, and `cg reset`
zeroes those counters.

## What counts as a crawler

The game's own floor-crawler walk type. That is the spawned crawlers (`zombieSteveCrawler` and its
feral variant), any crawler another mod builds on the same rig, and **a walker that loses a leg
and drops to the floor mid-fight** — from then on it is dragging itself too. The wall-climbing
spider zombie is a different rig and is not affected.

## What dragging does

- **Damage per block.** Every block of ground a crawler covers while locked onto a player earns
  it `cg dmg` health of damage. The setting may be fractional — 0.5 is one point every two blocks
  — and whole points come off as they are earned. 0 switches the drag damage off but leaves the
  bleed roll running.
- **Never below the floor.** Drag damage takes at most the health the crawler has above `cg floor`
  percent of its max health, so it lands on the floor rather than through it: 20 HP on a 200 HP
  crawler, 30 on a 300 HP feral. `cg floor 0` removes the floor and lets dragging kill.
- **A chance to bleed.** Once per block, a `cg bleed` percent chance to start the game's own bleed
  — one health a second for twenty seconds, the same as one cut from a blade. Only a crawler that
  is not already bleeding gets one, and a bleed that is already running, from this mod or from a
  blade, is **never refreshed, extended or stacked** by this mod. The bleed is a buff and pays no
  attention to the floor, so a bleeding crawler can still die. 0 switches it off.
- **Nobody's kill.** By default the drag damage and the bleed are owned by nobody: being chased
  is not the same as fighting back, so a crawler that dies of either is not your kill and pays
  no XP. `cg credit` on gives the damage and the bleed the chased player's id, so such a death
  counts as that player's kill for score, quests and XP.
- **Nothing else.** No knockback, no hit reaction, no stun, no dismemberment from the drag damage
  itself.

## FletchWounds

With [FletchWounds](../ul-fletchwounds) installed, a crawler dragging itself along with one of your
arrows still stuck in it gets a **second, separate** `cg arrow` percent chance per block to work
the arrowhead deeper. That is FletchWounds' own arrow effect, exactly as if you had pulled the
arrow: its damage, its stab sound and its one stack of bleed, credited to you and tuned with `fw`.
It is rolled half a block out of step with `cg bleed`, so the two never roll on the same block, and
only a crawler that is **not already bleeding** is handed over, so a bleed that is already running,
from a blade, from dragging or from FletchWounds itself, is never added to or refreshed by it. An
arrowed crawler hurts itself more often, not harder.

`cg flavor` lists the interactions with the other mods in this family, one switch per mod, on
by default, and changes nothing; `cg flavor fw` toggles this one and `cg flavor on|off` sets them
all. The switch is a pair: `cg flavor fw` and `fw flavor cg` set each other, and touch nothing
else, so FletchWounds' interaction with DoorSlammer is unaffected. `cg arrow 0` switches just
this roll off. Without FletchWounds the switch and the setting do nothing, and `cg info` says so.

## Defaults

All settable in-game, and all written back to the settings file as soon as you set them:

| Setting | Default |
|---|---|
| damage per block | 1 HP |
| never-kill floor | 10% of max health |
| bleed chance per block | 10% |
| credit the chased player | off |
| flavor with FletchWounds | on |
| arrow chance per block | 10% (needs FletchWounds) |

## Settings file

Every setting survives a restart. A change made with `cg` is written straight out to:

```
%APPDATA%/7DaysToDie/CrawlerGuts/settings.txt
```

— the game's own user data folder, next to `Saves`, rather than `Mods/CrawlerGuts/`, so updating
the mod does not take your settings with it. `cg info` prints the full path and whether the last
read or write worked.

It is plain `key = value` text, one line per setting, each naming the command that sets it:

```
enabled             = on       # cg on|off
damage              = 1        # cg dmg {hp} - health per block dragged
floor               = 10       # cg floor {pct} - percent of max health
bleed               = 10       # cg bleed {pct} - chance per block, 0 = off
credit              = off      # cg credit
flavor.fletchwounds = on       # cg flavor fw - interaction with FletchWounds
arrow               = 10       # cg arrow {pct} - chance per block with a FletchWounds arrow in, 0 = off
```

Edit it by hand with the game closed — it is rewritten whenever a `cg` command changes something.
A line that will not parse is logged and ignored rather than fatal, and deleting the file brings
back the defaults above (which live in `Settings.cs`).

## Undead Legacy

**Not required** — the mod works fine on a plain install, and is built to sit alongside UL without
modifying anything of UL's. Tested against **UL 2.7.32**. UL does not change crawler health, so the
defaults mean the same thing with and without it.

**Rage.** UL rolls a chance to enrage a zombie on every bit of damage it takes, however small, and
drag damage is many small bits. A crawler chasing you a long way therefore has a fair chance of
raging on the way in. This is UL's rule and the mod does not suppress it.

**Bleed.** Vanilla and UL share one bleed model, and UL's bladed weapons stack it. This mod only
ever starts a bleed on a crawler that has none, at the lowest tier, and leaves any existing stack
alone.

## Limitations

- **Dedicated servers.** The drag damage and kill credit are server-side and work anywhere. With
  `cg credit` on, XP for a crawler that dies of the *bleed* comes from the game's own buff-death
  path, which only pays out on the local player — so on a dedicated server a bleed death is
  credited but grants no XP. A credited drag-damage death always grants XP.
- **Clients on a dedicated server cannot use `cg`** — the settings live on the server.

## Building

Requires the .NET SDK; there are no NuGet dependencies. The mod builds in place inside the game
install, against the game's own assemblies.

```
dotnet build src/CrawlerGuts/CrawlerGuts.csproj -c Release
```

That restages `dist/CrawlerGuts/`, ready to copy into `Mods/`. To also build the release archive:

```
dotnet build src/CrawlerGuts/CrawlerGuts.csproj -c Release -t:Package
```

That writes `release/CrawlerGuts-v<version>-<date>.zip`, taking the version from `ModInfo.xml`.
Neither `dist/` nor `release/` is tracked — the zip is published as a GitHub Release instead.

## License

MIT — see `LICENSE`.
