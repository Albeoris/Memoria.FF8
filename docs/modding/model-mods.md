# Model browser and Blender

[Documentation](../README.md) / Modding

Open **F2 → Model browser**. The catalog discovers field characters, battle actors and weapons, battle stages, world characters and static world models from the selected game installation. Discovery is incremental. Selecting an entry decodes only that model; unsupported resources show their decoder error while the rest of the catalog stays available.

Click the search field or press Tab, then type a case-insensitive substring. `*` matches any sequence and `?` matches one character. Search includes names, resource addresses, category, identifiers and installed variant names. Arrow keys select entries; Enter or the configured Confirm opens the full viewer. Clicking an already selected row also opens it.

In the viewer:

| Control | Action |
| --- | --- |
| Arrows / movement stick | Rotate the model. |
| World camera rotation buttons | Zoom. The mouse wheel also zooms. |
| Battle escape left / right buttons | Previous / next model in the filtered list. |
| Card / Alternate or the Variant button | Cycle the original and installed mod files, including disabled packages. |
| Confirm or Commands | Open the command menu. |
| Cancel | Close commands, return to the catalog, then return to Debug Room. |

On-screen hints follow the current keyboard/controller bindings. Preview animation loops independently of gameplay; opening the browser pauses game simulation.

## Export

**Export to Blender** opens the standard Windows save dialog and writes a self-contained `.glb`. It contains triangulated geometry, UVs, embedded textures, vertex colors, skeleton, skin weights and available animation clips. Native model data is retained as metadata for round trips; this does not convert every original rendering or gameplay contract into a Blender feature.

In Blender, set the scene frame rate to **30 FPS**, import **glTF 2.0**, edit the model, then export **glTF Binary (.glb)**. Export all animation actions and enable **Custom Properties** to preserve `FF8_bone_index` joint identifiers. Retain `bone_<index>` bone names and `animation_<index>` action names where possible. Original quads are triangulated for exchange. Skeleton changes may break original attachments even when the number of bones stays the same.

## Import as a mod

**Import from Blender (as mod)** accepts a self-contained GLB up to 256 MiB. It validates geometry, buffers/accessors, textures, skeleton, transforms and animation data before offering a destination. A changed bone count requires explicit confirmation. Other compatibility warnings are also shown before writing. Canceling a dialog leaves model files unchanged.

Choose or create a package folder directly inside the displayed `Mods/Memoria` directory. The folder picker starts there; paths outside it, nested package roots and directory links are rejected, and the picker offers another attempt. Existing replacements require overwrite confirmation. All dialogs use the current monitor's DPI context.

Imported GLBs are normalized into the engine's editable representation and stored as:

```text
Mods/Memoria/<Package>/models/field/<field-name>/<model-index>.glb
Mods/Memoria/<Package>/models/battle/<original-filename>.glb
Mods/Memoria/<Package>/models/world/<shape-id>.glb
Mods/Memoria/<Package>/models/world/static/<shape-id>.glb
```

The normal mod precedence and enable settings apply. Disabled packages can be previewed without activation. Reload an already loaded scene or battle to apply a changed model to existing actors.

Supported GLB data includes triangle meshes, PNG/JPEG textures, up to four bone influences per vertex and translation/rotation/scale animation channels. External resource files, morph targets, unsupported required extensions, sheared node transforms and additional skin influences are rejected. Secondary material maps generate a compatibility warning; rendering uses base color textures and vertex color. Original eye, part and attachment data remains metadata: edited topology must preserve compatible bindings, and native per-vertex skin translations do not drive imported geometry. Game scripts continue to use their original animation and attachment identifiers.
