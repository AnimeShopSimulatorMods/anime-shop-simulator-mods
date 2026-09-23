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
FillFreeSlots = true
StuckWatchdog = true
StuckSeconds = 15.0
VerboseLogs = false
DiagnosticLogs = false
PanelKey = "F7"
```

| Setting | Default | What it does |
| --- | --- | --- |
| `FillFreeSlots` | `true` | Let employees stock shelf slots that have never held anything. Turn it off to keep bare shelves bare across the whole store. |
| `PanelKey` | `F7` | Opens the shelf panel. Any UnityEngine KeyCode name. |
| `DiagnosticLogs` | `false` | Traces the game's own restock search step by step. Very noisy; for bug reports only. |

Slots you set aside from the panel are listed in
`UserData/SmartRestockEmployees/locked-shelves.txt`, one id per line. Delete a line to hand that slot
back to your employees.

## Changelog

### 1.5.0

**Fixed: a forgotten shelf stayed forgotten, with no way back and nothing on screen explaining
why.** Forget clears a slot's memory and sets it aside so employees leave it alone -- working as
intended, but silent about it, and a slot a player later released from the panel came back
unlocked yet still unremembered, so it stayed skipped for a reason the panel never mentioned. With
"Employees may fill bare shelves" off, that slot was never getting stock again on its own.

The panel now says so, and gives you the other half of Forget: **Remember**. Point at a shelf with
forgotten slots on it and press F7 -- it offers to stock them with whatever the shelf already
sells, and tells you when it cannot (stock one slot yourself first and it will know). When bare
shelves are turned off, the panel also spells out, in plain words, that those slots are being left
alone on purpose and names the switch that controls it.

Thanks to **Drakolyte** on Nexus Mods, who reported employees completely avoiding shelves with no
locked slots in sight.

### 1.4.0

**Fixed: employees stopped restocking the whole store.** The mod judged whether a bare slot could
take a product by its type alone, which is not the question the game asks — the game also checks
whether the product physically fits. So the mod kept nominating slots the game then refused, and
blacklisted each one for its own mistake until nothing was left to restock and every employee went
idle. Bare slots are now left to the game, which knows how to fill them; the mod only steers slots
that already hold a known product. It also can no longer starve the store: if its own filtering
leaves nothing, it stands aside instead of waiting out a timer.

**New: a shelf panel on F7.** Look at a shelf and press F7. It shows what is on the shelf, and gives
you three things:

- **Empty this shelf** — packs the stock back into boxes at delivery zone 1 or 2, your choice, and
  sets the emptied slots aside so employees leave them alone.
- **Forget** — for slots that are already empty. Clears the price tag and sets the slot aside. Your
  price is not lost; prices belong to the product, so putting it back brings the price with it.
- **Employees may fill bare shelves** — the store-wide switch, on by default, which is how the mod
  has always behaved.

Slots you set aside stay empty regardless of that switch. Stock one yourself and it is released
automatically — no second trip to the panel. "See all" lists every shelf, and hovering a row lights
up the real shelf, since shelves have no name you would recognise. The panel drags by its title bar
and remembers where you left it.

Thanks to **Limitlessandre** and **Th3DarkC1aw** on Nexus Mods, who asked for exactly this.

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
