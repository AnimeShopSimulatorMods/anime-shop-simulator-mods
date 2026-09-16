# Anime Shop Simulator Mods

**MelonLoader mods for Anime Shop Simulator** — smarter employee restocking, employees who keep working
after closing time, and a full in-game cheat and debug menu. Open source, written in C# for the game's
Unity IL2CPP build.

[![MelonLoader](https://img.shields.io/badge/MelonLoader-0.7.x-ff5c8a)](https://github.com/LavaGang/MelonLoader)
[![Game version](https://img.shields.io/badge/Anime%20Shop%20Simulator-1.0.4-7c5cff)](https://store.steampowered.com/search/?term=Anime+Shop+Simulator)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

> Unofficial fan project. Not affiliated with or endorsed by OneMoreTime, the developers of Anime Shop Simulator.

## The mods

| Mod | What it does | Version |
|---|---|---|
| [**Smart Restock Employees**](SmartRestockEmployees/Publish/README.md) | Employees fill completely empty shelves first, never crowd the same slot, recover when stuck on a trash can, and store boxes on the least-filled storage rack. | 1.3.0 |
| [**Cheat for Dev**](CheatForDev/Publish/README.md) | In-game cheat menu on **F8**: money and infinite money, shop and employee levels, pause and speed up time, clear shelves, free deliveries, unlock every licence, and testing tools. | 0.1.0 |
| [**Employee Overtime**](EmployeeOvertime/Publish/README.md) | Keeps employees restocking, sorting storage and cleaning after the shop closes, until you end the day. | 0.1.0 |

Each mod is a single DLL and works on its own. Install only the ones you want.

## Installation

1. Install [MelonLoader 0.7.x](https://github.com/LavaGang/MelonLoader/releases) into Anime Shop Simulator.
2. Launch the game once so MelonLoader can generate its files, then close it.
3. Download a mod and extract it into the game folder, so the DLL ends up in `Anime Shop Simulator\Mods`.
4. Launch the game. The MelonLoader console lists every mod it loaded.

Settings for each mod appear in `UserData\MelonPreferences.cfg` after the first launch.

## Smart Restock Employees

Out of the box, the sorting employee restocks the **nearest** shelf that needs stock. That keeps a single
product topped up while shelves on the other side of the shop sit empty, and every employee makes the same
choice, so they crowd together.

This mod changes the choice, not the employee:

- **Empty shelves first** — a shelf slot with nothing on it always wins over one that still has stock,
  including shelves that have never been stocked at all.
- **Then the emptiest** — among the rest, the lowest stock ratio wins, with distance breaking ties.
- **No crowding** — each employee claims its target, so two of them never head for the same slot.
- **Slots keep their product** — an emptied slot is refilled with what it held, not a random box.
- **Stuck watchdog** — an employee standing still mid-task is nudged, then released from the task.
- **Balanced storage** — storage employees put boxes on the rack with the fewest boxes.

## Cheat for Dev

A developer menu, but useful to anyone who wants to skip the grind or set up a specific situation.

- **Money** — set it, or turn on infinite money. Crystals, experience, level, day and wins too.
- **Staff** — edit any employee's level and tier, promote, boost, clear debt, hire.
- **Time** — pause, jump to any hour within shop hours, speed up to 20x, end the day.
- **Shelves** — delete stock, or send it back to the delivery point as boxes. Pick from a list or just look at a shelf.
- **Orders** — spawn any box, up to 200 at a time, free.
- **Testing** — turn customers off, spawn a thief or any other customer type, clean the shop, teleport.
- **Unlocks** — every licence, store and storage upgrades.

## Building from source

Requirements: the .NET SDK, and Anime Shop Simulator with MelonLoader installed and launched once
(the build references the assemblies MelonLoader generates from your copy of the game — nothing from the
game is stored in this repository).

```bash
git clone https://github.com/AnimeShopSimulatorMods/anime-shop-simulator-mods.git
cd anime-shop-simulator-mods
dotnet build AnimeShopMods.slnx -c Release
```

The build looks for the game at `E:\SteamLibrary\steamapps\common\Anime Shop Simulator`. If yours is
elsewhere, create `Directory.Build.local.props` next to `Directory.Build.props` (it is ignored by git):

```xml
<Project>
  <PropertyGroup>
    <GamePath>D:\Games\Anime Shop Simulator</GamePath>
  </PropertyGroup>
</Project>
```

Or pass it once on the command line: `dotnet build -p:GamePath="D:\Games\Anime Shop Simulator"`.

To produce upload-ready zips:

```powershell
powershell -ExecutionPolicy Bypass -File tools\pack.ps1 -Mod SmartRestockEmployees
```

### Project layout

```
AnimeShopMods.slnx          one solution, one project per mod
Directory.Build.props       shared references and build settings
SmartRestockEmployees/      Harmony patches on the sorting and storage employee AI
EmployeeOvertime/           day-end hooks that keep employees working
CheatForDev/                IMGUI cheat menu and the game-facing cheat actions
tools/pack.ps1              builds a mod and zips it for Nexus Mods / Thunderstore
```

## Notes for modders

A few things about this game that cost real time to find out, written down so they do not have to be
found out again:

- **Game logic is IL2CPP.** Read method bodies with Cpp2IL; the MelonLoader-generated assemblies only
  contain stubs.
- **Do not Harmony-patch methods with `ref float` parameters.** Il2CppInterop's trampoline reads the
  `float&` with a pointer-sized load and writes it back, corrupting the game's value even if the patch does
  nothing. Smart Restock steers the game through the parameterless entry points and a `HavePoints` getter
  instead.
- **`GUILayout.TextField` is stripped from the build**, and calling it throws every frame. Everything else
  in IMGUI tested so far works. Cheat for Dev surveys the controls at runtime and logs the result.
- **Licences are called brands in code.** `ProductsService.BuyBrandSystem(id)` unlocks one without charging.
- **`TimeController.SetServerMinutes` counts from opening time**, not midnight.

## Contributing

Bug reports and pull requests are welcome. When reporting a bug, attach `MelonLoader\Latest.log` and say
which game version you are on — a game update is the most common reason a mod stops working.

## License

[MIT](LICENSE). Anime Shop Simulator and its assets belong to their respective owners; this repository
contains only original mod code.
