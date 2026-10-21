# Depixelation

This bundled native Memoria mod removes pixelated background characters in two scenes:

- `fields/bghall_6.json`: replaces Cid's background image during the Balamb Garden hall speech with the field's existing Cid model and idle animation.
- `fields/bgroom_1.json`: hides the seated background student in the Balamb Garden classroom. The scripted student who leaves the room keeps her original behavior.

Each fix is a separate scene file. Models, animations and background pixels come from the user's installed game; this package contains only scene configuration.

The package inherits the standard automatic enablement policy. Enable or disable `Memoria/Depixelation` through the Mods menu, or create `mod.ini` containing `enabled = 0` to disable it. No installed game files are changed. This package supersedes `Fix_CidUnpixelated`; an independently installed copy of the old package can be removed, and its enable setting does not configure this package.

See `docs/modding/field-mods.md` in the source distribution for the format and the Scene inspector in Debug Room (F2) for scene inspection. Ordinary development builds add missing bundled files without overwriting edits. Launcher updates replace untouched bundled defaults and preserve edited or pre-existing mod files.
