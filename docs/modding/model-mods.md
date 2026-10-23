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
| Card / Alternate or the Variant button | Cycle Original, Resolved and individual installed providers, including disabled packages. |
| Confirm or Commands | Open the command menu. |
| Cancel | Close commands, return to the catalog, then return to Debug Room. |

On-screen hints follow the current keyboard/controller bindings. Preview animation loops independently of gameplay; opening the browser pauses game simulation.

## Export

**Export to Blender** opens the asset selection panel before the Windows save dialog. Objects select geometry; Clips select independent skeletal tracks. Selecting a clip does not select its mesh. Required rigs, materials and textures are dependencies. A clip-only file retains editable rigs without geometry.

The panel discovers loader-proved weapon alternatives and animation banks, including supported external banks. Field main-character movements retain their separate field/entry replacement addresses. Unsupported or unavailable sources appear as diagnostics; the status never claims complete animation coverage for unresolved banks. Exporting before discovery finishes requires confirmation.

Click a row label to preview an object or clip group, and its checkbox to select export content. Select all includes filtered-out rows; Select visible selects only matching rows; Clear all clears user selections. Group checkboxes show partial selection. Filtering leaves selection unchanged, and manual selection prevents later discoveries from becoming selected automatically. Only one weapon alternative is visible in preview, independently of export selection. Tab switches list/preview focus; arrows navigate the list or rotate the preview. Space toggles the focused checkbox. Playback provides pause, frame stepping, scrubbing, loop and speed controls. Preview speed and loop do not edit exported data.

The result is one self-contained GLB with standard meshes, skins, embedded textures and editable skeletal animations. Body and standard weapon retain independent rigs and synchronized clip components with their own durations. Reduced weapons share the owner's rig and retain the replaced body-part relationship. Selected weapon alternatives can overlap in Blender; hide unused alternatives while editing.

Use the verified [Blender settings and round-trip helper](../developers/model-linked-glb.md). Enable Custom Properties, preserve the metadata root, native joint identifiers and qualified action/NLA tokens, and use NLA Tracks without merging separate clips. Set the native sample rate before importing: field/battle clips use 30 samples per second; world character clips use 30.5. The helper derives this rate from exported timing. Blender is not required to run Memoria.

## Import as a mod

**Import from Blender (as mod)** accepts a self-contained GLB up to 256 MiB. It inventories actual surviving geometry and animation channels before offering a destination. Matched targets can be checked independently; unchecked means leave unchanged. Missing metadata never recreates deleted content. A single-object GLB without preserved identities requires explicit confirmation of the selected catalog target. Unidentified multi-object or multi-rig files require restored identities before import; this workflow does not merge them into one replacement. Unresolved items remain disabled with an explanation. Source revision and duration changes produce warnings. Native joint-count changes are reported; impossible rig mappings and unsupported native deformations remain blocked. Canceling the selection or a dialog leaves model files unchanged.

Choose or create a package folder directly inside the displayed `Mods/Memoria` directory. The folder picker starts there; paths outside it, nested package roots and directory links are rejected, and the picker offers another attempt. Existing replacements require overwrite confirmation. All dialogs use the current monitor's DPI context.

Selected geometry and clip slots are normalized into independent native mod outputs, for example:

```text
Mods/Memoria/<Package>/models/battle/<original-filename>.geometry.glb
Mods/Memoria/<Package>/models/battle/<original-filename>.clips/embedded/<slot>.glb
Mods/Memoria/<Package>/models/battle/<original-filename>.clips/<external-bank>/<slot>.glb
Mods/Memoria/<Package>/models/field/<field-name>/<entry>.geometry.glb
Mods/Memoria/<Package>/models/field/<field-name>/<entry>.clips/embedded/<slot>.glb
```

World targets use `models/world/<shape-id>` or `models/world/static/<shape-id>` with the same suffixes. Legacy full-model `<target>.glb` replacements remain supported. Geometry-only replacements retain the target rig and clips; per-slot replacements retain all other clips. Changing rigs, adding gameplay slots, cross-character retargeting and action/script export are outside this selection workflow. Battle stages can be exported, but stage replacement is blocked until its native stage geometry binding is supported.

Installation stages and validates all selected files, shows conflicts and rolls back touched files on failure. Unselected assets, unrelated files and package enable/priority settings are retained. Original game archives are never rewritten. A field occurrence's clip affects that exact entry, including every actor using that entry; it does not replace identical clips in other fields.


The normal mod precedence and enable settings apply. Disabled packages can be previewed without activation. Reload an already loaded scene or battle to apply a changed model to existing actors.

Supported GLB data includes triangle meshes, PNG/JPEG textures, up to four bone influences per vertex and translation/rotation/scale animation channels. External resource files, morph targets, unsupported required extensions, sheared node transforms and additional skin influences are rejected. Secondary material maps generate a compatibility warning; rendering uses base color textures and vertex color. Original eye, part and attachment data remains metadata: edited topology must preserve compatible bindings, and native per-vertex skin translations do not drive imported geometry. Game scripts continue to use their original animation and attachment identifiers.

Older whole-model packages remain available, but ambiguous battle geometry without current native part, texture and material identities requires a fresh export before import. Existing package files are preserved. Current exports retain these identities through the supported Blender workflow.

Native battle geometry additionally requires one retained joint per vertex, original part/material bindings and render flags, unchanged vertex colors, and original-size indexed eight-bit texture slots. Native coordinate, UV and index limits are validated before installation. Field/world skinning support does not expand these battle limits. Arbitrary native per-vertex control or attachment changes are unsupported.
