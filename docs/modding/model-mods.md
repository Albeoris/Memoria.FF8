# Model browser and Blender

[Documentation](../README.md) / Modding

Open **F2 → Model browser**. The catalog discovers field characters, battle actors and weapons, battle stages, world characters and static world models from the selected game installation. Discovery is incremental. Selecting an entry decodes only that model; unsupported resources show their decoder error while the rest of the catalog stays available.

Click the search field or press Tab, then type a case-insensitive substring. `*` matches any sequence and `?` matches one character. Search includes names, resource addresses, category, identifiers and installed variant names. Arrow keys select entries; Enter or the configured Confirm opens the full viewer. Clicking an already selected row also opens it.

In the viewer:

| Control | Action |
| --- | --- |
| Arrows / movement stick | Rotate the model. |
| Left or middle mouse drag | Orbit the preview freely. |
| Shift + left or middle mouse drag | Pan the preview. |
| Right mouse drag | Roll horizontally; dolly vertically. |
| World camera rotation buttons | Zoom. The mouse wheel also zooms. |
| Front / Side / Top | Select a camera direction while keeping pan and zoom. |
| Reset camera | Restore front view, centered framing and default zoom. |
| Battle escape left / right buttons | Previous / next model in the filtered list. |
| Card / Alternate or the Variant button | Cycle Original, Resolved and individual installed providers, including disabled packages. |
| Confirm or Commands | Open the command menu. |
| Cancel | Close commands, return to the catalog, then return to Debug Room. |

On-screen hints follow the current keyboard/controller bindings. Preview animation loops independently of gameplay; opening the browser pauses game simulation.

## Export

**Export to Blender** opens the asset selection panel before the Windows save dialog, where you can choose any destination folder. Objects select geometry; Clips select independent skeletal tracks. Selecting a clip does not select its mesh. Required rigs, materials and textures are dependencies. A clip-only file retains editable rigs without geometry.

The panel discovers loader-proved weapon alternatives and animation banks, including supported external banks. Field main-character movements retain their separate field/entry replacement addresses. Unsupported or unavailable sources appear as diagnostics; the status never claims complete animation coverage for unresolved banks. Exporting before discovery finishes requires confirmation.

Click a row label to preview an object or clip group, and its checkbox to select export content. Select all includes filtered-out rows; Select visible selects only matching rows; Clear all clears user selections. Group checkboxes show partial selection. Filtering leaves selection unchanged, and manual selection prevents later discoveries from becoming selected automatically. Only one weapon alternative is visible in preview, independently of export selection. Tab switches list/preview focus; arrows navigate the list or rotate the preview. Space toggles the focused checkbox. Playback provides pause, frame stepping, scrubbing, loop and speed controls. Preview speed and loop do not edit exported data.

The result is one self-contained GLB with standard meshes, skins, embedded textures and editable skeletal animations. Body and standard weapon retain independent rigs and synchronized clip components with their own durations. Reduced weapons share the owner's rig and retain the replaced body-part relationship. Selected weapon alternatives can overlap in Blender; hide unused alternatives while editing.

Use the verified [Blender settings and round-trip helper](../developers/model-linked-glb.md). Enable Custom Properties, preserve the metadata root, native joint identifiers and qualified action/NLA tokens, and use NLA Tracks without merging separate clips. Set the native sample rate before importing: field/battle clips use 30 samples per second; world character clips use 30.5. The helper derives this rate from exported timing. Blender is not required to run Memoria.

To create entirely new motion, author new keys in an exported slot's action or replace its NLA strip with your own action while keeping the qualified track identity. Follow the [authored-motion workflow](../developers/model-linked-glb.md#author-a-new-motion-for-an-existing-slot). This replaces an existing animation slot; it does not add a gameplay action or automatically coordinate a multi-character fight's events.

## Import as a mod

**Import from Blender (as mod)** accepts a self-contained GLB up to 256 MiB from any folder. It inventories actual surviving geometry and animation channels before offering a destination. Matched targets can be checked independently; unchecked means leave unchanged. Missing metadata never recreates deleted content. A single-object GLB without preserved identities requires explicit confirmation of the selected catalog target. Unidentified multi-object or multi-rig files require restored identities before import; this workflow does not merge them into one replacement. Unresolved items remain disabled with an explanation. Source revision and duration changes produce warnings. Classic and Remastered exports can be imported into either edition; the importer validates the selected model rig and clip contracts instead of blocking by the source edition. Native joint-count changes are reported; impossible rig mappings and unsupported native deformations remain blocked. Canceling the selection or a dialog leaves model files unchanged.

The package folder picker creates the default `Mods/Memoria` directory if needed and opens there. You can navigate freely and choose or create any package folder, including an external or nested folder. To use a package saved elsewhere, place it directly inside a discovered mod root such as `Mods/Memoria`. Existing replacements require overwrite confirmation. All dialogs use the current monitor's DPI context.

Selected geometry and clip slots are normalized into independent native mod outputs with embedded destination metadata. These paths also preserve compatibility with older Memoria versions, for example:

```text
Mods/Memoria/<Package>/models/battle/<original-filename>.geometry.glb
Mods/Memoria/<Package>/models/battle/<original-filename>.clips/embedded/<slot>.glb
Mods/Memoria/<Package>/models/battle/<original-filename>.clips/<external-bank>/<slot>.glb
Mods/Memoria/<Package>/models/field/<field-name>/<entry>.geometry.glb
Mods/Memoria/<Package>/models/field/<field-name>/<entry>.clips/embedded/<slot>.glb
```

Each mod is a package folder directly below `Mods/Memoria`, with `mod.ini` and its `models` directory **beside each other**. Do not place `mod.ini` inside `models`: that would make the discovered addresses `field/...` instead of `models/field/...` in older versions.

New versions find every `.glb` anywhere within a Memoria package, including renamed files. Exported linked GLBs retain their `memoria_ff8` manifest and can be placed directly in the package. Installed single-model GLBs retain `memoria_import` metadata. Embedded destinations take precedence over filenames. Multiple files in one package targeting the same geometry or clip slot are rejected. Files without destination metadata load from recognized legacy paths with a warning; an unidentified GLB at any other path rejects mod publication with a diagnostic naming the package and file. Invalid metadata is rejected even at a legacy path.

### Create or edit a destination manually

In Blender, keep **Custom Properties** enabled during import and export. On the exported metadata root, create or edit a string custom property named `memoria_import`. Its value is JSON, for example:

```json
{"Version":1,"ModelId":"field/bghoke_2/1","Mode":"geometry","Bank":"embedded","ClipSlot":null}
```

For an animation-only replacement of slot 3:

```json
{"Version":1,"ModelId":"field/bghoke_2/1","Mode":"clip","Bank":"embedded","ClipSlot":3}
```

`ModelId` is the logical model address, excluding `models/` and the filename suffix. `Mode` is `geometry` (no animations), `clip` (exactly one animation and no meshes), or `model` (legacy complete model replacement). `Bank` defaults to `embedded`; another supported bank can be named for clips. Clip slots range from 0 to 65535 and must already exist in the target bank. Geometry and complete models use the embedded bank and no clip slot. Native rig, material and coordinate metadata remain necessary for the corresponding native conversions; a destination property does not bypass those checks.

When editing GLB JSON directly, store that JSON **as a string** in `asset.extras.memoria_import` or a node's `extras.memoria_import`. Exports write both copies for editor compatibility. If both survive, update both to the same destination or remove one; conflicting destinations are rejected. Preserve the rest of the GLB and its binary chunk. The linked `memoria_ff8` manifest follows the [linked GLB schema](../developers/model-linked-glb.md); use it for multiple objects or clips instead of attaching a single-model destination to an assembly.

World targets use `models/world/<shape-id>` or `models/world/static/<shape-id>` with the same suffixes. Legacy full-model `<target>.glb` replacements remain supported. Geometry-only replacements retain the target rig and clips; per-slot replacements retain all other clips. Changing rigs, adding gameplay slots, cross-character retargeting and action/script export are outside this selection workflow. Battle stages can be exported, but stage replacement is blocked until its native stage geometry binding is supported.

Installation stages and validates all selected files, shows conflicts and rolls back touched files on failure. Unselected assets, unrelated files and package enable/priority settings are retained. Original game archives are never rewritten. A field occurrence's clip affects that exact entry, including every actor using that entry; it does not replace identical clips in other fields.

## Spring bones for field models

A Memoria package can apply spring motion to an existing field rig. The physics uses the animated pose as its target, so a single setup covers idle, movement and scripted animations. The **Rinoa Hair Physics** component is an opt-in example for Classic and Remastered. Enable it in the mod manager before entering the field that uses it.

Put a version 1 JSON document at `models/field/<field>/<entry>.springs.json` for one field model entry. Use `models/field/main_chr/<dNNN>.springs.json` to cover occurrences that load a specific installed main character model source. Both selectors use the usual package priority. An exact field entry wins over a main character selector in the same package. The winning document supplies the joint list as a whole.

```json
{
  "version": 1,
  "expectedBoneCount": 3,
  "expectedParents": [-1, 0, 1],
  "joints": [
    {
      "bone": 1,
      "localTail": [0, 0, 12],
      "stiffness": 90,
      "damping": 12,
      "gravity": [0, 0, 0]
    }
  ]
}
```

- Use zero-based indices in the selected rig. Each simulated bone must have a parent and appear only once. `expectedParents` records the rig topology in bone index order, with `-1` for roots. It is required for main character sources and optional for exact entries.
- `localTail` is a finite, nonzero offset in the bone's local axes, in native field units. For a native chain, the next joint is placed at `[0, 0, boneLength]` in those axes. Choose an explicit endpoint for a leaf. Apply weights to the relevant bones when authoring an edited mesh.
- `stiffness` is the spring strength in s⁻² and `damping` is the velocity decay rate in s⁻¹. Both must be finite and nonnegative. `gravity` is a finite field-space acceleration in field units per second squared, with Z up. Use `[0, 0, 0]` to keep the animated pose as the resting target.

Use Blender to set up the rig and weights. The [VRM add-on](https://extensions.blender.org/add-ons/vrm/) can preview the general effect. For Memoria, enter the bone indices and tune the settings in this JSON. The runtime reads the package document and updates the chains at the field callback rate. Higher presentation rates interpolate the committed samples. Each actor has its own spring state, and the field initializes it from the current pose on load. Version 1 models the motion of the bone chain with inertia, damping and gravity.


The normal mod precedence and enable settings apply. Disabled packages can be previewed without activation. Reload an already loaded scene or battle to apply a changed model to existing actors.

Supported GLB data includes triangle meshes, PNG/JPEG textures, up to four bone influences per vertex and translation/rotation/scale animation channels. External resource files, morph targets, unsupported required extensions, sheared node transforms and additional skin influences are rejected. Secondary material maps generate a compatibility warning; rendering uses base color textures and vertex color. Original eye, part and attachment data remains metadata: edited topology must preserve compatible bindings, and native per-vertex skin translations do not drive imported geometry. Game scripts continue to use their original animation and attachment identifiers.

Field geometry and texture mods can be enabled together. A material with a retained `memoria_native_texture` slot uses the active replacement for that slot, including the current eye variant when available. The embedded GLB image is used when the slot has no active replacement or the material has no slot binding. Older geometry-only files with a complete texture list matching the original slot dimensions use the original slot order.

Older whole-model packages remain available, but ambiguous battle geometry without current native part, texture and material identities requires a fresh export before import. Existing package files are preserved. Current exports retain these identities through the supported Blender workflow.

Native battle geometry additionally requires one retained joint per vertex, original part/material bindings and render flags, unchanged vertex colors, and original-size indexed eight-bit texture slots. Native coordinate, UV and index limits are validated before installation. Field/world skinning support does not expand these battle limits. Arbitrary native per-vertex control or attachment changes are unsupported.
