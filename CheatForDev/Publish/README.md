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
- End the day now, or skip to the next one. Skipping advances the day counter, which the quest system
  schedules against, so anything dated still comes due.

### Shelves
- List every sales shelf with its product and item count, tick the ones you want.
- **Aim mode** — look at a shelf in game and act on it directly, no list needed.
- Two ways to empty a shelf, kept deliberately separate:
  - **Delete its stock** — the items are gone.
  - **Return to delivery** — the items come back as boxes at the delivery point, ready to restock.
- Drop a single item on the floor, for testing cleaning staff.

### Orders
- Browse every box in the game and spawn 1 to 200 of them straight onto the delivery point.
- **Genuinely free.** Boxes are created directly rather than ordered, so nothing is validated against
  what the shop currently sells and nothing is charged.
- Every entry shows its internal id, which is what to quote if one of them misbehaves.
- **Clear up** — remove the selected product, every loose box, or unopened furniture crates. Quest
  deliveries, boxes already on shelves, and anything built into your shop are never touched.

### Testing helpers
- Turn customers off entirely, or send everyone home — useful for watching employee AI undisturbed.
- Spawn one customer of any type, including the thief and the shoplifter.
- Clean the whole shop.
- Teleport to the delivery point.

### Unlocks
- **Unlock every tool** — mop, trash bag, decor kit and bat, written to the save so they survive a restart.
- **Open the crystal shop** — the stand that sells special packs for crystals.
- **Open the clothes shop** — the outfit stand, which the game holds shut until a set day rather than
  by a flag, so this brings that day forward.
- **Open the second floor** — the upstairs room. This one moves quest progress past the quest that
  grants it, so every quest before that counts as done. All three are written to the save.
- **Skip the whole tutorial** — finishes every tutorial step, marks every popup as seen, and opens up
  everything the tutorial would have given you along the way: the tools, the crystal shop, the clothes
  shop and the second floor. Takes a second click to confirm, because it is saved and has no undo.
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

### 0.5.0
- Fixed: the spawn buttons never appeared for some products, so they could be selected but never
  ordered. The menu decided whether anything was selected by testing the product id for being zero or
  above, and the game's ids are not all positive. Reported against Bald Justice Manga, which has a
  negative one, while Booster Big Box Common does not.
- Fixed: free delivery was neither free nor reliable. It went through the game's own ordering path,
  which validates the order against what the shop currently sells - rejecting anything else, silently -
  and debits the wallet on the way through. Boxes are created straight onto the delivery point now,
  which skips both. Nobody walked to the computer and paid for these.
- Fixed: the log claimed every spawn succeeded, because it was written after the request rather than
  after checking the result. It now reports how many boxes were actually placed, and says so when that
  is fewer than asked for.
- Added: **Clear up**, in the Orders tab. Two hundred boxes arrive on one click and used to have to be
  carried away one at a time. Clear the selected product, every loose box, or unopened furniture crates.
- Every entry in the Orders list now shows its internal id, so a product that misbehaves can be named
  exactly rather than described.

### 0.4.0
- Fixed: **skip to the next day** never moved the day counter. It winds the clock round to the next
  morning, but the counter is raised by the end-of-day sequence that the button skips, so ten presses
  still left you on day zero. It now advances the counter too.
  This was not only a wrong number on screen. The quest system schedules by that counter, so with it
  stuck at zero nothing dated could ever come due - which is why the clothes shop below could not be
  opened at all until this was fixed.
- Added: **Open the clothes shop**, in the Unlocks tab. The outfit stand is held shut until a set day
  rather than by a flag, so this brings that day forward, and moves the day counter to 1 first on a save
  still sitting on day zero.
- Added: **Open the second floor**, in the Unlocks tab. The upstairs room opens on finishing the quest
  that grants it, so this moves quest progress past that quest. It is the blunt one: every quest before
  it then counts as done. The menu and the log both say so before you use it.
- **Skip the whole tutorial** now opens the clothes shop and the second floor as well, so a skipped
  tutorial no longer leaves you locked out of half the building.

### 0.3.0
- Fixed: **Unlock inventory tools** was gone after restarting the game. It called the game's own cheat,
  which is a session-only flag that is never written to the save. The tools are really gated by four
  quest flags, and those are what the new **Unlock every tool** button sets. The session-only cheat is
  still there under the game's own cheat panel, now labelled as session-only.
- Added: **Open the crystal shop**, in the Unlocks tab. The special-pack stand is hidden until a quest
  opens it, which a skipped tutorial never reaches, leaving crystals with nowhere to be spent.
- Added: **Skip the whole tutorial**, in the Unlocks tab. It finishes the step tutorial, marks every
  one-off popup as seen, and grants the tools and the crystal shop the tutorial would have handed you
  on the way through. Two clicks, because it is saved and cannot be undone.
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
