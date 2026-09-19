# Cheat for Dev

A MelonLoader cheat and debug menu for **Anime Shop Simulator**. Built for testing mods and for anyone
who wants to skip the grind. Press **F8** in game to open it.

## Features

### Money and progression
- Set money with stepper buttons, or jump straight to 0 / 10k / 100k / 1M / 10M.
- **Infinite money** — the wallet is topped back up automatically whenever it drops below half the target.
- Crystals, shop experience, shop level, day counter and tournament wins.

### Employees
- Every employee listed with profession, level, tier and experience.
- Change level, set tier, promote, boost, clear their debt, hire into a slot.
- Grant promotion experience, spawn a cashier.

### Time
- Pause and resume the clock.
- Jump to any time of day. Shop opening hours are read from the game, so the value is clamped to the
  real trading day instead of a hard-coded guess.
- Time speed from frozen to 20x.
- End the day now, or skip to the next one.

### Shelves
- List every sales shelf with its product and item count, tick the ones you want.
- **Aim mode** — look at a shelf in game and act on it directly, no list needed.
- Two ways to empty a shelf, kept deliberately separate:
  - **Delete its stock** — the items are gone.
  - **Return to delivery** — the items come back as boxes at the delivery point, ready to restock.
- Drop a single item on the floor, for testing cleaning staff.

### Orders
- Browse every box in the game and spawn 1 to 200 of them straight onto the delivery point.
- No basket, no payment, no storage limit.

### Testing helpers
- Turn customers off entirely, or send everyone home — useful for watching employee AI undisturbed.
- Spawn one customer of any type, including the thief and the shoplifter.
- Clean the whole shop.
- Teleport to the delivery point.

### Unlocks
- **Unlock every tool** — mop, trash bag, decor kit and bat, written to the save so they survive a restart.
- **Open the crystal shop** — the stand that sells special packs for crystals. Also saved.
- **Skip the whole tutorial** — finishes every tutorial step, marks every popup as seen, and grants what
  the tutorial would have handed you along the way: the tools and the crystal shop. Takes a second click
  to confirm, because it is saved and has no undo.
- **Unlock every licence** at once, or one at a time from a list.
- Buy the next store or storage upgrade.
- See outstanding bills and rent.
- If the developers' own cheat panel is present in your build, buttons appear to open all cards, spawn
  debug buildings, show their panel, and flip their session-only inventory tool cheat.

## Requirements

- [MelonLoader](https://github.com/LavaGang/MelonLoader) 0.7.x (tested on 0.7.3)

## Installation

1. Install MelonLoader into Anime Shop Simulator and launch the game once.
2. Copy `CheatForDev.dll` into the game's `Mods` folder.
3. Launch the game, load a save, and press **F8**.

## Usage notes

- The menu only opens once a save is loaded; there is nothing to cheat at the main menu.
- Drag the window by its title bar. The position is remembered between sessions.
- Numbers are changed with buttons rather than typed. This is not a style choice: the game ships with
  Unity's text field stripped out of the build, so it cannot be drawn at all. The menu checks which
  controls survived when it first opens and builds itself from those, writing the result to the
  MelonLoader console.

## Compatibility

- **Singleplayer and hosting only.** Nearly every action here is server-side. As a multiplayer client the
  menu opens but says so, and the actions do nothing.
- Safe to run alongside other employee mods.

## Warnings

- Setting level or day far outside their normal progression can corrupt a save. Back your save up first.
- Spawning hundreds of boxes at once will litter the delivery area.

## Configuration

`UserData/MelonPreferences.cfg`, created after the first launch:

```
[CheatForDev]
ToggleKey = "F8"
RunProbe = true
VerboseLogs = false
WindowX = 24.0
WindowY = 24.0
```

- `ToggleKey` — any `UnityEngine.KeyCode` name.
- `RunProbe` — logs a survey of the game's cheat-related objects once per session. Leave it on after a
  game update; it is how you find out whether something moved.
- `VerboseLogs` — logs every cheat action.
- `WindowX` / `WindowY` — updated automatically whenever you drag the window.

## Changelog

### 0.3.0
- Fixed: **Unlock inventory tools** was gone after restarting the game. It called the game's own cheat,
  which is a session-only flag that is never written to the save. The tools are really gated by four
  quest flags, and those are what the new **Unlock every tool** button sets. The session-only cheat is
  still there under the game's own cheat panel, now labelled as session-only.
- Added: **Open the crystal shop**, in the Unlocks tab. The special-pack stand is hidden until a quest
  opens it, which a skipped tutorial never reaches, leaving crystals with nowhere to be spent.
- Added: **Skip the whole tutorial**, in the Unlocks tab. It finishes the step tutorial, marks every
  one-off popup as seen, and grants what the tutorial would have handed you on the way through - the
  tools and the crystal shop - so a skipped tutorial does not leave you without a mop or a shop.
  Two clicks, because it is saved and cannot be undone.
- Fixed: every button in the **Crystals** section did nothing. Crystals are stored per player, not in the
  shared parameter table the rest of that tab uses, so the mod was writing to a place the game never reads.
  They now go through the game's own per-player crystal balance.
- Fixed: **Tournament wins** did nothing, for the same reason. It now uses the game's `SetWins`, which also
  clears any tournament rewards you had already claimed above the new number.
- Tournament wins shows a plain message instead of `0` while the game has not yet mapped your player.
- Every value the menu writes is now checked afterwards, and a warning is logged if the game did not
  actually store it. A silent mismatch is what hid the two bugs above for a whole release.
- The IMGUI capability survey no longer prints `FAIL` next to controls this game does not ship. It says
  "not in build" and explains that the menu substitutes for them. Nothing was broken; only the wording was.
- The probe now prints your real crystal and tournament-win balances next to the parameter table, and
  points out any stray value an older version of this mod left in that table. Those leftovers are inert.

### 0.2.0
- Fixed: "Delete its stock" and "Return to delivery" failed with a NullReferenceException and left the shelf untouched.
- Shelves are now emptied the same way the game removes an item when a customer takes it.
- The menu window can be dragged by its title bar, and remembers where you left it.
- MelonLoader no longer skips the mod when the game reports a different name.

### 0.1.0
- First release.
