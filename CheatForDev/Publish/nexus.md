# Nexus Mods upload copy - Cheat for Dev

Two blocks, kept here so they are updated in the same commit as the release they describe.
Nexus renders both as BBCode. Paste them as-is.

---

## File options - "File Description"

Shown under the file name on the Files tab. Keep it to what changed and how to install.

```
Version 0.4.0 - built against Anime Shop Simulator 1.0.5, needs MelonLoader 0.7.x.

Extract into your game folder. The zip already contains Mods\CheatForDev.dll, so it lands in the right place. Launch the game, load a save, press F8.

New in this version:
- Skip the whole tutorial in one click, and get everything it would have handed you along the way: the tools, the crystal shop, the clothes shop and the second floor.
- Open the clothes shop and the second floor on their own, if you would rather not skip the tutorial.
- Unlock every tool (mop, trash bag, decor kit, bat) and have it survive a restart.

Fixed in this version:
- Skip to the next day never moved the day counter, so ten presses still left you on day one. Worth knowing: the quest system schedules by that counter, so with it stuck nothing dated ever came due.
- The Crystals buttons did nothing. They work now.
- Tournament wins did nothing. It works now.
- Unlocking the inventory tools was forgotten when you quit the game. It is saved now.

Upgrading from an earlier version: replace the old DLL. Your settings and saves are kept. If an older version left a stray crystal or tournament-win value behind, the mod now points it out in the log; it is inert and can be ignored.
```

---

## Mod page - "Description"

```
[size=5][b]Cheat for Dev[/b][/size]

A cheat and debug menu for Anime Shop Simulator. Press [b]F8[/b] in game to open it.

It was written to test other mods, so it leans towards things a developer needs - spawn this customer, empty that shelf, freeze the clock - but it does the ordinary cheats too. Take what you want from it.

[size=4][b]What it does[/b][/size]

[b]Money and progression[/b]
[list]
[*]Set money with buttons, or jump straight to 0 / 10k / 100k / 1M / 10M.
[*][b]Infinite money[/b] - the wallet tops itself back up once it drops below half your target, so ordinary spending still shows on screen instead of the number snapping back.
[*]Crystals, shop experience, shop level, the day counter and tournament wins.
[/list]

[b]Employees[/b]
[list]
[*]Every employee listed with profession, level, tier and experience.
[*]Change level, set tier, promote, boost, clear their debt, hire into a slot.
[*]Grant promotion experience, spawn a cashier.
[/list]

[b]Time[/b]
[list]
[*]Pause and resume the clock.
[*]Jump to any time of day. Opening hours are read from the game, so the value is clamped to the real trading day rather than a guess.
[*]Time speed from frozen to 20x.
[*]End the day now, or skip to the next one. Skipping advances the day counter, which the quest system schedules against.
[/list]

[b]Shelves[/b]
[list]
[*]Every sales shelf listed with its product and item count. Tick the ones you want.
[*][b]Aim mode[/b] - look at a shelf in game and act on it directly, no list needed.
[*]Two ways to empty a shelf, kept deliberately apart: [b]delete its stock[/b], or [b]return it to delivery[/b] as boxes you can restock from.
[*]Drop a single item on the floor, for testing cleaning staff.
[/list]

[b]Orders[/b]
[list]
[*]Browse every box in the game and spawn 1 to 200 of them straight onto the delivery point. No basket, no payment, no storage limit.
[/list]

[b]Testing helpers[/b]
[list]
[*]Turn customers off entirely, or send everyone home - useful for watching employee AI undisturbed.
[*]Spawn one customer of any type, including the thief and the shoplifter.
[*]Clean the whole shop.
[*]Teleport to the delivery point.
[/list]

[b]Unlocks[/b]
[list]
[*][b]Skip the whole tutorial[/b] - finishes every step, marks every popup as seen, and opens everything the tutorial hands out along the way, so you are not left without a mop, with crystals you cannot spend, or locked out of half the building.
[*][b]Unlock every tool[/b] - mop, trash bag, decor kit and bat.
[*][b]Open the crystal shop[/b] - the stand that sells special packs for crystals.
[*][b]Open the clothes shop[/b] - the outfit stand, which the game holds shut until a set day.
[*][b]Open the second floor[/b] - the upstairs room. This one moves quest progress past the quest that grants it, so every quest before that counts as done.
[*]Unlock every licence at once, or one at a time.
[*]Buy the next store or storage upgrade, and see what you owe in bills and rent.
[*]If the developers' own cheat panel is still in your build, buttons appear to open all cards, spawn debug buildings and show their panel.
[/list]

[size=4][b]Requirements[/b][/size]

[list]
[*]MelonLoader 0.7.x
[*]Built against Anime Shop Simulator 1.0.5
[/list]

[size=4][b]Installation[/b][/size]

[list=1]
[*]Install MelonLoader into Anime Shop Simulator and launch the game once.
[*]Extract this mod into your game folder. The zip already contains [b]Mods\CheatForDev.dll[/b].
[*]Launch the game, load a save, press [b]F8[/b].
[/list]

[size=4][b]Things worth knowing[/b][/size]

[b]Numbers are changed with buttons, not typed.[/b] That is not a style choice. This game ships with Unity's text field stripped out of the build, so it cannot be drawn at all. When the menu first opens it checks which controls survived and builds itself from those, then writes the result to the MelonLoader console. A line there saying a control is "not in build" is the expected result, not an error.

[b]Singleplayer and hosting only.[/b] Nearly everything here is server-side. As a multiplayer client the menu still opens, but it tells you so and the buttons do nothing.

[b]The menu only opens once a save is loaded.[/b] There is nothing to cheat at the main menu.

Drag the window by its title bar. It remembers where you left it.

[size=4][b]Warnings[/b][/size]

[list]
[*]Setting level or day far outside their normal progression can corrupt a save. [b]Back your save up first.[/b]
[*]Skipping the tutorial is written to the save and cannot be undone. It asks for a second click before it does anything.
[*]Opening the second floor moves quest progress forward, so every quest before the one that grants it counts as done.
[*]Spawning hundreds of boxes at once will litter the delivery area.
[/list]

[size=4][b]Configuration[/b][/size]

[b]UserData/MelonPreferences.cfg[/b], created after the first launch:

[code]
[CheatForDev]
ToggleKey = "F8"
RunProbe = true
VerboseLogs = false
[/code]

[list]
[*][b]ToggleKey[/b] - any UnityEngine.KeyCode name.
[*][b]RunProbe[/b] - logs a survey of the game's cheat-related objects once per session. Worth leaving on after a game update; it is how you find out whether something moved.
[*][b]VerboseLogs[/b] - logs every action the menu takes, and what the game actually stored afterwards. [b]Turn this on before reporting anything that looks like it does nothing[/b] - it answers the question in one line.
[/list]

[size=4][b]Changelog[/b][/size]

[b]0.4.0[/b]
[list]
[*]Fixed: skip to the next day never moved the day counter. The clock wound round to the next morning, but the counter is raised by the end-of-day sequence the button skips, so ten presses still left you where you started. It matters more than the number on screen: the quest system schedules by that counter, so with it stuck nothing dated could come due.
[*]Added: open the clothes shop. The outfit stand is held shut until a set day rather than by a flag, so this brings that day forward.
[*]Added: open the second floor. The upstairs room opens on finishing the quest that grants it, so this moves quest progress past that quest - which means every quest before it then counts as done. The menu and the log both say so first.
[*]Skipping the tutorial now opens the clothes shop and the second floor too.
[/list]

[b]0.3.0[/b]
[list]
[*]Added: skip the whole tutorial, in the Unlocks tab. It also unlocks the tools and the crystal shop, which the tutorial would otherwise have handed you on the way through.
[*]Added: open the crystal shop on its own. The special-pack stand stays hidden until a quest opens it, leaving crystals with nowhere to go.
[*]Fixed: every button in the Crystals section did nothing. Crystals are kept per player rather than in the table the rest of that tab uses, so the mod was writing somewhere the game never reads.
[*]Fixed: tournament wins did nothing, for the same reason.
[*]Fixed: unlocking the inventory tools was gone after restarting the game. It used the game's own cheat, which is a session-only flag nothing saves. The real unlock is saved, and that is what the button does now.
[*]Every value the menu writes is now read back, and the log warns when the game did not store it. A silent mismatch is what hid the three bugs above.
[*]The startup survey no longer prints "FAIL" next to controls this game does not ship. Nothing was broken; only the wording was alarming.
[/list]

[b]0.2.0[/b]
[list]
[*]Fixed: emptying a shelf threw an error and left the shelf untouched.
[*]The menu window can be dragged by its title bar, and remembers where you left it.
[*]MelonLoader no longer skips the mod when the game reports a different name.
[/list]

[b]0.1.0[/b]
[list]
[*]First release.
[/list]
```
