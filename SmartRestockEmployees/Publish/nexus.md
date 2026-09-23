# Nexus Mods upload copy - Smart Restock Employees

Three blocks, kept here so they are updated in the same commit as the release they describe.
The first two render as BBCode on Nexus; paste them as-is. The third is a plain-text reply.

---

## File options - "File Description"

Shown under the file name on the Files tab. **Nexus caps this field at 255 characters**, so it holds
the version, how to install, and what changed - nothing else. The full story goes on the mod page.

Counted below with CRLF line endings, which is the worst case if Nexus counts a line break as two
characters. Re-check the count after any edit; the field truncates silently rather than warning.

248 / 255 characters:

```
1.5.0 - game 1.0.6, MelonLoader 0.7.x. Extract into the game folder, load a save, press F7.

Fixed: forgotten shelf slots had no way back. New: Remember brings them back, and the panel now explains when bare slots are being left alone on purpose.
```

A shorter spare, 211 / 255, if the one above ever runs long after an edit:

```
1.5.0 - game 1.0.6, MelonLoader 0.7.x. Extract into the game folder, load a save, press F7.

Fixed: forgotten shelves had no way back. New: Remember brings them back; panel explains why bare slots are skipped.
```

---

## Mod page - "Description"

This is a changelog entry only, not a rewrite of the page. Paste it at the top of the mod page's
Changelog section, above the existing 1.4.0 entry.

```
[b]1.5.0[/b]
[list]
[*]Added: [b]Remember[/b] - the other half of Forget. Point at a shelf with forgotten slots and press F7; it offers to stock them with whatever the shelf already sells, and tells you when it cannot (stock one slot yourself first and it will know).
[*]Fixed: a forgotten shelf slot had no way back. With "Employees may fill bare shelves" off, those slots were skipped forever with nothing on the panel explaining why.
[*]The panel now says, in plain words, when a shelf's empty slots are being left alone on purpose, and names the switch that controls it.
[/list]

Thanks to [b]Drakolyte[/b] on Nexus Mods, who reported employees completely avoiding shelves with no locked slots in sight.
```

---

## Reply to Drakolyte's comment

Plain text - Nexus comments are not BBCode-heavy.

```
Hi Drakolyte, thanks for the detailed report and for sticking with it until it made sense.

What you were seeing was real, and it had nothing to do with the muscular destiny manga, pocket dragon manga, keep heart manga, ksusha figure, or kage t-shirt themselves. Those slots had been told, at some point, to sit there and not restock (a "Forget" press, maybe a while back), and there was no way to undo that and nothing on the panel explaining why. So your employees weren't avoiding those products - they were doing exactly what the mod had quietly asked them to do, indefinitely.

1.5.0 fixes this. Once you update, for the affected shelves:

1. Walk up to the shelf and press F7 to open the panel.
2. If it shows forgotten slots, press Remember - it restocks them with whatever the shelf already sells.
3. While you're there, check "Employees may fill bare shelves" at the bottom of the panel. If it's off, any slot that's never held a product will also sit empty until you turn that on or stock it yourself.

That should bring everything back to normal. Thanks again for the report - it's exactly what let me track this down.
```
