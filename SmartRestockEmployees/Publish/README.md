# Smart Restock Employees

A MelonLoader mod for **Anime Shop Simulator** that makes your employees restock smarter.

## Features

- **Empty shelves first** — Sorting employees fill shelf slots that are completely empty before topping up slots that still have stock. This includes shelves that have never held a product, which the game would otherwise only reach once nothing else needs restocking.
- **Emptiest shelf next** — Once nothing is empty, the slot with the lowest stock ratio wins, with the nearest one breaking ties.
- **No crowding** — Two sorting employees never walk to the same slot.
- **Balanced storage racks** — Storage employees put incoming boxes on the storage rack with the fewest boxes.
- **Slots keep their product** — When a slot runs empty, employees refill it with the same product instead of a random box.
- **Stuck watchdog** — Employees that stand still while walking to a shelf or disposing a box (for example caught on a trash can) are nudged back to work.
- **Safe fallback** — If the mod cannot find a better target, the game's original behaviour is kept.

## Requirements

- [MelonLoader](https://github.com/LavaGang/MelonLoader) 0.7.x (tested on 0.7.3)

## Installation

1. Install MelonLoader into Anime Shop Simulator and launch the game once.
2. Copy `SmartRestockEmployees.dll` into the game's `Mods` folder.
3. If you used an older build named `Anime Shop Mod.dll`, delete it from `Mods`.
4. Launch the game. The MelonLoader console should show `Smart Restock Employees`.

## Compatibility

- Singleplayer: supported.
- Multiplayer: the logic runs where employee AI runs (the host). Untested with other employee mods.

## Configuration

`UserData/MelonPreferences.cfg`, created after the first launch:

```
[SmartRestockEmployees]
EmptyShelvesFirst = true
KeepEmptySlotProduct = true
StuckWatchdog = true
StuckSeconds = 15.0
VerboseLogs = false
```

## Changelog

### 1.3.0
- Fixed: employees kept topping up a single shelf slot while other shelves stayed completely empty.
  Once the mod had picked a slot it hid every other slot from the employee's search, which also cut off
  the only code path the game uses to fill shelves that never held a product.
- Fixed: every sorting employee ran the same "which slot is emptiest" calculation and got the same answer,
  so they all walked to the same slot. Each employee now claims its target.
- Shelves that were never stocked are now valid targets: the mod picks a product from storage that fits the slot.
- Added `EmptyShelvesFirst` (default on). Turn it off to go back to pure lowest-stock-ratio ordering.
- Rebuilt against game version 1.0.4.

### 1.2.1
- Fixed: `NullReferenceException ... FindNextProductPlaceCandidate` in the MelonLoader log.
- Reworked shelf targeting to avoid patching game methods with out-parameters, which could corrupt the game's distance values and make employees behave oddly.

### 1.2.0
- Fixed: an emptied shelf slot was sometimes refilled with a different product.
- Fixed: employees could get stuck (for example on a trash can) while restocking.
- Storage employees no longer skip the game's own shelf-search logic, and two employees no longer target the same free spot.
- Added settings in `MelonPreferences.cfg`.

### 1.1.0
- Sorting employees now restock the lowest-stock shelf slot first.
- Storage employees place boxes on the least-filled storage rack.
