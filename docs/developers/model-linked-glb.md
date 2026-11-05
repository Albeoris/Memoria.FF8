# Linked model GLB editing in Blender

Export geometry checkboxes and clip-component checkboxes independently. A clip-only file retains the native rigs. Weapon alternatives can overlap in Blender; hide the alternatives you are not editing. Hiding an alternative in the viewport does not change its replacement identity.

Use Blender's built-in glTF importer/exporter. Enable **Custom Properties** on export, keep the `Memoria linked assets` metadata root, and retain `memoria_*` identities on objects and bones. Keep the qualified action/NLA names. Display labels after those tokens can be edited. Do not merge different clips into one scene animation.

Exports use upright Y-up glTF coordinates; Blender converts these to its Z-up coordinates. The metadata root's `memoria_basis = 2` records the authoring conversion. Keep that custom property: importing reverses the conversion before applying engine contracts. Older files without this property keep their original coordinate interpretation; unsupported future versions are rejected. The engine's retained `EditableModel` coordinate basis is unchanged. Battle/stage and field/world resources have different native vertical axes, so the conversion follows each rig's kind, including linked files.

Select an imported action to inspect its posed character. The native skeleton's unanimated bind configuration is not necessarily a standing pose. In Blender, view the chosen animation from Front to inspect the character's face; saved `.blend` working scenes can retain that action and frame for immediate preview.

## Author a new motion for an existing slot

1. Export the intended existing clip slot with its body, weapon and any synchronized components. Keep an untouched copy for comparison. Set the scene sample rate before importing, then save a working `.blend` file.
2. Select the imported armature and the slot's action in the Action Editor. Edit its keys, or make a single-user copy of the action and replace all of its keys with your new choreography. Preserve its bone identities, action slots and the qualified `memoria_...` NLA track name. Put the authored action in that track's strip and remove the old strip from that track so exactly one replacement is exported. A friendly suffix such as ` Sword fight` may follow the identity token.
3. For Squall/Seifer choreography, author each actor in its own rig. Use the matching qualified track name for synchronized body/weapon components belonging to one exported clip. Two actors' separate gameplay slots remain separate replacement targets; staging them together in Blender does not create a runtime cutscene or schedule their attacks, camera, damage or sound. Keep scene placement separate from exported root motion unless that movement is intended in the game.
4. Bake constraints or IK to supported bone transforms, and export GLB with custom properties, **NLA Tracks**, **Always Sample Animations**, and **Slide Animation to Zero**. Retain all synchronized components you intend to replace. The strip's exported length determines the authored duration; changed durations are reported and are not stretched to fit other components. Native battle clips permit at most 255 frames.
5. Inspect the exported file in Memoria, select the intended replacement components, review timing/hierarchy warnings, preview first/middle/last frames and import. Validate the existing gameplay action that invokes the slot, including its weapon alignment and event timing. Authored movement changes the slot's animation, while the action's gameplay events still follow their existing scripts.

Creating a new Blender action is supported when it replaces an exported slot through that slot's track identity. An arbitrary new action without that identity produces an explicit unmapped-animation warning and is omitted. Adding animation identifiers or gameplay actions beyond the target resource's existing slots remains unsupported.

Blender's [glTF documentation](https://docs.blender.org/manual/en/latest/addons/import_export/scene_gltf2.html) describes action slots, NLA export and custom properties. Retain the `.blend` source for further editing; runtime imports use the exported GLB.

The verified Blender 5.2 workflow uses **NLA Tracks**, **Always Sample Animations**, **Slide Animation to Zero**, GLB output and custom properties. Set the scene frame rate to the native sample rate **before importing**. The default Blender frame rate can otherwise change sample positions. For synchronized rigs, retain matching NLA track names so their channels remain one clip group. Retain imported actions and their action slots/NLA strips; deleting an action deletes its imported clip content.

The focused background round-trip helper derives the sample rate from the embedded native timing, imports and exports through Blender's built-in add-on:

```powershell
& $env:BLENDER_EXECUTABLE --background --python tools/blender/model_roundtrip.py -- input.glb output.glb
```

Set `BLENDER_EXECUTABLE` to your selected Blender installation. Blender is an authoring tool, not a Memoria runtime dependency. The helper creates no geometry and does not convert the interchange format.

Independent rigs use separate skeleton roots. Clip-only exports include unused standard glTF skins so Blender creates editable armatures even without mesh users. Blender may export synchronized shorter streams with constant hold samples through the longer stream's end. The importer recognizes a constant tail after the recorded native duration, removes that tail and reports it. Actual motion beyond that duration is preserved with a duration warning; it is never silently stretched to match the other component.

Selective import uses surviving mesh nodes and animation channels. Keeping the manifest does not restore deleted content. Unchecked items leave existing game content unchanged. Source/provider fingerprints are provenance, while logical target addresses choose replacement slots. Files with missing identities need explicit mapping and are not automatically redirected to the currently selected model.

The supported interchange includes skeletal transforms, supported glTF skin weights, triangles, UVs, base-color textures, vertex colors and native render metadata. Apply modifiers and triangulate before export. Arbitrary constraints, drivers, custom shaders, morph targets, shear, texture transforms and new gameplay slots are outside the native editing contract. A structurally invalid file cannot be approved as a warning. Edited non-linear curves or keys between native frames are sampled on the target frame grid with an explicit warning; native clips do not retain arbitrary between-frame curves.

Opaque native-bound materials use glTF `MASK` coverage when their textures contain transparency: alpha zero is cut out, while PSX STP texels (decoded alpha 128) remain opaque. The cutoff is `1/255`; the source texture pixels are retained exactly for native palette round trips. This is distinct from an explicitly blended material or fractional vertex opacity, which continues to export as `BLEND`. Imported generic `MASK` materials retain their authored cutoff; that does not expand the native resource's material capabilities.

For reproducible synthetic verification, set `MEMORIA_MODEL_EXCHANGE_TEST_GLB` and `MEMORIA_MODEL_EXCHANGE_CLIP_GLB` while running `ModelAssetExchangeTests` to emit mixed and clip-only fixtures. Run the helper on each. Set `MEMORIA_MODEL_EXCHANGE_BLENDER_GLB` and `MEMORIA_MODEL_EXCHANGE_CLIP_BLENDER_GLB` to those output files when rerunning the tests. Mixed-output validation compares first/middle/last native-frame skinned positions with a tolerance of `0.0001` glTF units. Optional installed-model checks additionally allow `0.00001` times the position magnitude for float32 skin conversion. Blender can change interpolation between native samples when transforming bone bases; native sample poses and root tracks are the compatibility contract. The default fixtures contain generated geometry only. Optional installed exports require configured local resources and must remain outside distributions.

Native sample rates belong to the source model: the current Battle/Field base contract uses 30 samples per second, while World avatars use 30.5. Script speed changes and preview speed do not alter this contract. The rate survives GLB metadata and per-object manifest records.

The linked manifest includes only rig/control metadata and selected attachment descriptors. It never embeds original geometry, texture pixels, unselected clips, compressed animation banks, action sequences or camera resources inside provenance metadata. Clip-only outputs omit geometry attachment descriptors as well.

Battle stage export is supported. Stage import is explicitly blocked because the native stage renderer has a separate geometry binding; a viewer-only replacement is not installed as a working gameplay mod.
