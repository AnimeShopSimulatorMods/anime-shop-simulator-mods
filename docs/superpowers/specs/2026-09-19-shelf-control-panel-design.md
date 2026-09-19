# Shelf control panel — design

Smart Restock Employees 1.4.0

## Why

Two Nexus Mods users asked for the same thing from opposite directions:

- **Limitlessandre** keeps shelves deliberately empty to organise a growing store. They put
  stock on one temporarily, took it off, and employees now refill it forever. They want a way
  to clear a shelf's memory.
- **Th3DarkC1aw** wants employees blocked from filling slots that never held anything, plus a
  button to clear the remembered product from an empty shelf.

Both hit the same mechanism. When a slot runs empty the mod falls back to
`ProductPricePlace.LastDefinitionId` — the product the slot held before — and keeps refilling
with it. A slot that has never held anything has no memory, so today it is treated as free and
gets filled with whatever box is nearby.

That produces two empty-looking slots that behave differently, and the player has no way to move
a slot between the two states.

| Slot state | `LastDefinitionId` | Today | Wanted |
| --- | --- | --- | --- |
| Held stock, ran out | set | refilled with the same product | keep, but allow clearing |
| Never held stock | 0 | filled with anything | leave alone, player's choice |

## Scope

In:

- A store-wide setting for whether never-filled slots may be used.
- An in-game panel: look at a shelf, press a key, act on that shelf.
- A list of every shelf, with the hovered shelf highlighted in the world.
- Returning a shelf's stock to boxes at delivery zone 1 or 2, player's choice.
- Clearing a shelf's remembered product.

Out:

- Per-shelf overrides of the never-filled-slot rule. One store-wide rule, decided during design.
  This keeps the mod stateless: everything reads and writes game state, so there is no mod-side
  save file and no way for mod state to drift out of sync with a reloaded save.
- Renaming shelves.
- Any change to the existing restock ranking beyond the new rule.

## Restocking rule

Add to `SlotRules`:

```csharp
// A slot that has never held anything. The player may want it left alone.
public static bool IsFreeSlot(ProductPricePlace place) => !HasProduct(GetSlotProductId(place));
public static bool AllowsFill(ProductPricePlace place) => Main.FillFreeSlots || !IsFreeSlot(place);
```

Two call sites, both in `SortingPatches.cs`:

1. `HavePointsPatch.Allows` — the single hook every slot search passes through. Returning false
   here removes the slot from the game's own search.
2. `ResolveProductForSlot` — returns `NoProduct` for a free slot when the rule is off, so the
   shelf ranking in `ChooseTarget` skips it through the existing
   `if (!SlotRules.HasProduct(productId)) continue;`.

Both are needed. The first alone would let a shelf of free slots rank as "emptiest", sending an
employee on a trip with nothing to do at the end of it.

New preference `FillFreeSlots`, **default true**. True preserves today's behaviour. A default of
false would mean a newly bought shelf is never stocked by employees again, which breaks the base
game loop for anyone who updates without reading the release notes.

## Clearing a shelf's memory

`ProductPricePlace.LastDefinitionId` is get-only, but the backing field is exposed by
Il2CppInterop as a settable property:

```csharp
place._lastDefinitionId = 0;
place.EnsureProductSaveDataServer(true);
```

The second call pushes the change into the slot's `ProductSaveData` so it survives a save/reload.
Verify this in game — if the save path ignores it, fall back to `RestoreStateServer` with a save
data whose definition id is zero.

Server only. Guard with the existing `GameAccess.IsServer` check.

## Returning stock to a delivery zone

Lift `ShelfCheats.ReturnToDelivery` into shared code and add zone selection.

The game keeps two spawn point lists on `OrderController`: `_points` (zone 1) and
`_secondaryPoints` (zone 2). `ResolveSpawnPoints(bool useSecondarySpawnPoints)` picks between them.

- **Zone 1** — keep using `TryGetRecoveryPose(out position, out rotation, true)`. This is the path
  already proven to work in CheatForDev.
- **Zone 2** — no game API reaches it. Read `_secondaryPoints`, take the transform of the point at
  a rotating index, add a per-box vertical offset from the controller's own
  `DefaultSpawnStackHeight` and `SpawnStackClearance` floats, then pass the result through
  `NormalizeSpawnPose(ref position, ref rotation, definitionId)` so the game snaps the box into a
  legal pose.

`BuildSpawnAnchors` and `TryGetSpawnPose` would be the tidier route, but they take a `SpawnAnchor`
struct and an `Il2CppStructArray<float>`. Struct marshalling through Il2CppInterop is exactly what
broke `SetCountServer` in this codebase (see the comment on `ShelfCheats.EmptySlot`), so the risk
is not worth it while a hand-rolled pose works.

If `_secondaryPoints` is null or empty the zone is not unlocked yet. Warn in the panel and use
zone 1.

Emptying a shelf to a delivery zone also clears each slot's memory, in the same action. This is
the case Limitlessandre described: they want the shelf genuinely free, not just temporarily bare.

## The panel

Rendered with IMGUI through a new `Ui/Skin.cs` that builds `Texture2D` and `GUIStyle` objects once
at load. Dark card, rounded corners via a 9-slice border texture, pink accent `#F58CC4` matching
the mod's `MelonColor`. The product icon is the game's own `Sprite`, read from
`ProductPricePlace._currentIcon` and drawn with `GUI.DrawTexture`. No other icons — the game ships
none and hand-drawn textures are not worth the bytes.

Opened with F7 by default, configurable. CheatForDev uses F8; the two are deliberately different.

### Aim view

Shown when the player is looking at a shelf. Reuses `ShelfCheats.ShelfUnderCrosshair`.

```
Shelf                                    F7 to close
  [icon]  Sakura figurine
          12 of 24 items - 4 of 6 slots used

  Empty this shelf
  [ Delivery 1 ]  [ Delivery 2 ]
  Items go back into boxes at the zone you pick, and
  employees stop refilling this shelf.

  Keep this shelf empty                        [ Forget ]
  Forget what used to be here

  Use never-filled slots                          (on/off)
  Applies to every shelf in the store
```

Destructive actions go through a confirmation that states the outcome in numbers
("12 items become 2 boxes at Delivery 1").

### List view

Every shelf in the store, scrollable, each row showing product icon, product name and fill count.
Hovering a row calls `ProductPricePlace.EnableLook(true)` on that shelf's places every frame, which
is the game's own look-highlight — the player sees the real shelf glow. Rows are checkable for bulk
actions.

Re-applying every frame matters: the game's own look raycast clears the flag, so a single call
would flicker off.

Shelves have no player-visible names, which is why highlighting replaces naming.

### Wording

Player-facing, no engine vocabulary. `slot`, `definition id`, `memory` and GameObject names never
reach the screen.

## Code layout

A `Shared/` folder at the repo root, compiled into both mods:

```xml
<Compile Include="..\Shared\**\*.cs" />
```

One copy of the source, two DLLs, nothing extra for players to install. Moving to `Shared/`:
`Ui/GuiCaps.cs`, `Ui/Controls.cs`, `CursorControl.cs`, shelf scanning and crosshair lookup, and the
delivery return.

A separate shared DLL was rejected: Nexus users would have to install a second file, which is a
reliable source of broken installs and support comments.

## Failure handling

| Case | Behaviour |
| --- | --- |
| Not the host | Actions unavailable, panel says why. These write networked state. |
| Delivery zone full | Stop, report how many items were returned. The rest stay on the shelf. |
| Product has no box definition | Skip that slot, report it. Never delete stock silently. |
| Zone 2 not unlocked | Warn, fall back to zone 1. |
| Any exception | Log, keep the panel alive, leave game state as found. |

Every action reports a number back into the panel, not just into the MelonLoader console.

## Capability probe

`GuiCaps` already surveys 13 IMGUI controls and found only `TextField` stripped. Extend it with:

- a custom-built `GUIStyle`
- `GUI.DrawTexture`
- a game `Font` via `Resources.FindObjectsOfTypeAll<Font>()`

If the font probe fails, fall back to the built-in font at a larger `fontSize`. The panel must
render with every one of these missing.

## Testing

No test harness exists for Il2Cpp mods. In-game checklist:

1. Shelf that held stock, then cleared — employees stop refilling it.
2. Shelf that never held stock, rule on — employees fill it.
3. Shelf that never held stock, rule off — employees leave it alone and do not walk to it.
4. Return stock to zone 1, boxes appear and stack.
5. Return stock to zone 2, boxes appear at the second zone.
6. Return stock with the zone full — partial return reported, remaining stock intact.
7. Save, reload, confirm a cleared shelf is still cleared.
8. Join as a client — actions unavailable, no exceptions.
9. Panel renders with each probed capability forced off.
