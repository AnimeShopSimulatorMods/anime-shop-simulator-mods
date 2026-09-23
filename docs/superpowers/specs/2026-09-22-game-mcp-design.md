# Game MCP — design

A dev-only way for Claude Code to see and drive a running Anime Shop Simulator, so mod bugs can be
reproduced and verified in the game without the user pressing anything.

## Why

Bug reports like the Smart Restock "employees ignore a forgotten shelf" one cannot be settled from
the interop stubs: the method bodies are native, so the only way to know what the game does is to
watch it. Today that means asking the user to set a scene up, press keys, and paste logs back.

## Scope

In:

- Reading game state: shelves and their slots, employees, loose boxes, time, money, loaded mods.
- Reading `MelonLoader/Latest.log`, including while the game is closed.
- Cheat-style actions (money, boxes, time, employees, shelves) without depending on CheatForDev
  being installed.
- Calling any mod's static methods and reading/writing any mod's MelonPreferences.
- Generic reflection: find objects by type, get/set members, call methods.
- Screenshots, teleporting the player, pointing the camera at a shelf.
- Sending keyboard (and basic mouse) input to the game.

Out:

- Launching, closing or loading saves. The user starts the game and enters the store.
- Running arbitrary C# source (no Roslyn in the game).
- Shipping any of this to players. GameBridge is never packed by `tools/pack.ps1`.
- Multiplayer clients. The bridge acts as the host.

## Architecture

```
Claude Code --stdio--> GameMcp (.NET 10 console)  --TCP 127.0.0.1:47831-->  GameBridge (MelonLoader mod)
                        reads Latest.log from disk                           runs commands on Unity's main thread
```

Two pieces, so the MCP server stays up while the game is closed and can say so plainly instead of
Claude Code seeing a dead server.

### New and moved folders

| Folder | What | Compiled by |
| --- | --- | --- |
| `GameBridge/` | MelonLoader mod, net6, in `AnimeShopMods.slnx` | itself |
| `GameMcp/` | .NET 10 console, `ModelContextProtocol` SDK, stdio transport | itself |
| `GameMcp.Tests/` | unit tests for the MCP side | itself |
| `DevShared/` | cheat code moved out of `CheatForDev/` | CheatForDev, GameBridge |

`DevShared/` holds what is now `CheatForDev/Cheats/*.cs` and `CheatForDev/GameAccess.cs`, under the
namespace `AnimeShopMods.Dev`. Their calls to `CheatForDev.Main.Log` go through a small
`DevLog` class whose sink each host sets at start-up. CheatForDev must behave exactly as before; this
is a move, not a rewrite. It is not put in `Shared/` because Smart Restock compiles `Shared/**` and
would then ship cheat code to players.

### Wire protocol

Newline-delimited JSON over one TCP connection, loopback only.

```
-> {"id":7,"cmd":"shelves","args":{"includeEmpty":true}}
<- {"id":7,"ok":true,"result":{...}}
<- {"id":8,"ok":false,"error":"NullReferenceException: ... (at ...)"}
```

- The listener thread only parses and enqueues. Every command runs in `OnUpdate` on the main thread,
  because Il2Cpp objects must not be touched from other threads. At most a few commands run per
  frame.
- Each command has a timeout (10 s default; `wait` sets its own). A hung frame returns a timeout
  error instead of hanging Claude.
- Every command is wrapped in try/catch; an exception becomes an error reply, never a crash.
- Port `47831`, overridable in MelonPreferences (`GameBridge.Port`) and in `GameMcp` through the
  `GAME_BRIDGE_PORT` environment variable. The port is documented in `GameBridge/README.md`.

### Handles

Reflection results that are Unity/Il2Cpp objects are returned as handles (`"h:12"`) plus a short
description. Handles live in a table in the bridge and are cleared on scene unload; using a stale
handle returns an error naming the scene change.

## Tools

| Group | Tool | Does |
| --- | --- | --- |
| State | `game_status` | connected, scene, in store, host, day/time, money, loaded mods and versions |
| | `shelves` | every slot: persistent id, product, count/max, `LastDefinitionId`, product binding, `HavePoints`, Smart Restock lock state, position |
| | `employees` | role, position, carried box product, target slot, AI state |
| | `boxes` | loose and stored boxes grouped by product |
| | `read_log` | tail N lines or grep `Latest.log`; works with the game closed |
| Cheats | `set_money`, `spawn_boxes`, `box_catalog`, `clear_boxes`, `set_time`, `time_scale`, `hire_employee`, `employee_set`, `empty_shelf`, `buyers` | thin wrappers over `DevShared` |
| | `wait` | wait N real seconds or N in-game minutes, then return |
| Mods | `mod_call` | call a static method by `Assembly:Namespace.Type.Method` with JSON args |
| | `mod_prefs` | read or set any MelonPreferences entry |
| Reflection | `find_objects` | `FindObjectsOfType` by type name, returns handles |
| | `get`, `set`, `call` | member access on a handle or a static type; args are primitives, strings, enums by name, or handles |
| Visual | `screenshot` | capture the game view to a PNG; `GameMcp` returns it as an image |
| | `teleport` | move the player to a position or in front of a shelf/slot |
| | `look_at` | turn the camera to a shelf/slot/position |
| Input | `key_press`, `key_down`, `key_up`, `type_text` | keyboard input |
| | `mouse_move`, `mouse_click` | relative mouse move (camera look) and clicks |

Tool descriptions state which ones change the save, so Claude treats them with care.

## Keyboard and mouse input

The game ships Rewired, Unity InputSystem and the legacy Input module, and which one reads player
controls is not known from the stubs. Input is therefore sent through Win32 `SendInput`, which
looks like real hardware to all three.

- The bridge brings the game window to the foreground before sending. If Windows refuses (focus
  stealing rules), the tool returns an error saying so rather than typing into another window.
- Known limitation: input sent while the user is actively using another window can land there.
  The tools are for when the user is away from the keyboard.
- First implementation step is a spike: hold W and confirm the player moves. If `SendInput` cannot
  be made reliable, fall back to queueing `InputSystem` state events on `Keyboard.current`, and
  record which path works in `GameBridge/README.md`.

## Screenshots

`ScreenCapture.CaptureScreenshot(path)` into a scratch folder under `UserData/GameBridge/`, then
wait for the file to appear (it is written at end of frame). The reply carries the path; `GameMcp`
reads the file and returns image content, then deletes it. If the capture module is stripped from
this build, fall back to reading the backbuffer at end of frame through a coroutine.

## Errors when the game is not running

`GameMcp` tries to connect per call (with a short timeout) and keeps no long-lived state. If the
connection fails, every game tool returns: game not running, or GameBridge not installed, with the
expected port. `read_log` still works.

## Registration

`.mcp.json` at the repo root registers `game` as a stdio server running the built `GameMcp`
executable. Deploying GameBridge into the game's `Mods/` folder is a build step on the GameBridge
project, same as the other mods.

## Testing

- `GameMcp.Tests`: protocol encoding/decoding, timeout and not-running replies, tool argument
  validation, against a fake bridge on a local socket.
- In the game, the acceptance run: `game_status` → `shelves` → `key_press` W moves the player →
  `screenshot` shows the store → reproduce the Smart Restock forgotten-shelf report with
  `mod_call` + `wait` + `shelves`, and see whether employees refill it.
- CheatForDev after the move: build, load in game, open the menu, use one cheat per tab.
