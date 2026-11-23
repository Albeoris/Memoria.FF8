# Authoring 3D field scenes

An FFVIII 3D field scene mod adds GLB scenery to an active field or replaces selected parts of its painted background. Native scripts still choose the field and camera slot. The walkmesh, actor positions, triggers, and save format are shared with the original presentation. Players enable the package under **Mods → Installed**. Blender is an authoring dependency.

## Export and edit in Blender

1. Enter the field and wait until it is visible. Open **Mods → Create Mods → Edit 3D field scene mod → Export native reference to Blender**. Choose a destination folder. The export contains `reference.glb`, `reference.json`, and a rendered `background.png`. Keep native captures private.
2. Open the reference with the companion script. It imports the walkmesh, native cameras and actor guides, marks reference collections and saves a Blender project. The background is a camera-space reference image. Guide meshes and cameras are excluded from ordinary scene export.
3. Model using the reference origin and axes. One Blender unit corresponds to 128 native field units. Blender Z up becomes glTF Y up. In native `System.Numerics` row-vector order, the reference mapping is `(x, y, z) → (128x, −128z, 128y)`. Use the camera projection and capture metadata in the JSON when checking alignment. Background tiles have compositing order and screen-space animation, so positioning them as physical geometry would require additional authored depth.
4. Export a scene using Blender's glTF exporter or the companion script. Use embedded PNG/JPEG, UV0, triangulated geometry, an ordinary unlit or base-color material, and up to four skin influences per vertex. Export cameras when they are used for calibration or an authored view. Keep scene, camera, material, node, and clip custom property `memoria_scene_id` stable when renaming or reorganizing objects.

```powershell
blender --background --factory-startup --disable-autoexec --python tools/blender/field_scene.py -- reference reference.json field-reference.blend
blender --background authored.blend --disable-autoexec --python tools/blender/field_scene.py -- export field.glb --scene Scene
```

See `blender --background --python tools/blender/field_scene.py -- --help` for the installed script's commands. Its optional painted preset uses straight alpha, double-sided unlit base color, and preserves the active action's exported key range. The GLB includes supplemental projection metadata for Blender lens shift and effective framing.

## Import, preview, and save

1. Choose **Create 3D field scene mod** under **Mods → Create Mods**, or select an existing scene package under **Edit 3D field scene mod**. Managed packages are copied to a writable identity for editing. New packages start disabled. The shortcut to open a selected package in the current field is **Ctrl+K** by default and can be rebound from the scene package menu.
2. Choose **Import GLB from Blender**. It uses the same file dialog and review flow as model import. Review counts, clips, dependency warnings, and approximate texture memory. Import uses the GLB's default scene. Preview is specific to the native camera slot captured when opening the editor.
3. Choose **Adjust placement**, **Align author camera pose**, or **Fit 3D landmarks**. In the editor, G moves along X, Y or Z, R rotates around an axis, and S scales uniformly. Type the amount, then use Enter to apply or Esc to cancel. The preview follows numeric edits before they are applied. Pose alignment supplies an initial camera orientation and translation at the current scale. It gives no floor or pixel fit by itself. Landmark files contain an array of corresponding `asset` and `field` XYZ points in glTF units and native units respectively. Put three distinct non-collinear landmarks first, then add independent checks. The first three set rotation. All pairs contribute to uniform scale and translation. The editor reports RMS and maximum residual in field units and native pixels at the current viewport.
4. Choose the painted material preset when fractional alpha and unlit shadow receiving are needed. A primary shadow-capable field light must be active for dynamic receiving. The alpha preset writes depth on fully opaque pixels, discards zero alpha, and blends fractional pixels without writing depth. The source `BLEND` mode uses this split by default. Set explicit `blend` for an all-blended pass.
5. Select a clip, preview it, and review the backdrop. **Overlay** keeps native tiles. **SelectedModules** hides chosen modules. **Replace** hides the whole painted background for that binding. Full replacement requires coverage of every animated module for each claimed state. Compare against the original view before claiming states. Art for a static room may need extra geometry for a scripted door, screen, or foreground effect.
6. **Save scene** writes the current field binding and content-addressed GLBs to the chosen package. Reimport retains placement and exact stable-ID bindings. Review removed IDs and select replacement bindings before Save. Undo and Redo are local to the editor. A revision conflict leaves the draft open. Use **Compare native field** or **Full-screen field preview** to check the complete raster. The preview camera can use the game camera, an imported camera, or an inspection orbit without changing the saved camera binding.
7. Close the editor and enable the package through **Mods → Installed**. Test entering and leaving the field, its scripts and each camera covered by the mod. Test a missing or unmatched binding to verify native fallback.

To preview a window-filling composition, import a GLB with a camera, choose **Frame fit: Crop**, then open **Full-screen field preview**. **Frame fit** switches the preview to the imported camera when gameplay still uses the native camera. To use the same framing in gameplay, select **Camera: Authored** and save the view. **Contain** preserves the whole authored frame and can show bars. **Expand** widens the visible area for scenes with geometry beyond the authored frame.

## Package layout and coordinate contract

```text
mod.json
mod.ini
scene3d/component.json
scene3d/fields/<native-field-name>.json
scene3d/assets/<lowercase-sha256>.glb
```

The component file is `{"schemaVersion":1}`. The ordinary mod descriptor supplies the package ID, display name and English description. A field document uses the canonical resource name. Its `assets` reference the hash of exact GLB bytes, with an optional scene name in `scene`. Each view maps to a native `cameraSlot` and has bounded instance, asset and material IDs. A placement is a finite invertible affine matrix with 16 row-major values. Translation occupies cells 12–14, using zero-based indexing. The one transform maps asset world coordinates to native field coordinates after the node pose or skinning.

The synthetic fixture generator writes a complete example package with multiple meshes, two authored views, a clip, and material roles. Export a reference from each of two native camera slots in one field before generating the package.

```powershell
blender --background --python tools/blender/field_scene_fixture.py -- room.glb --reference slot0/reference.json --reference slot1/reference.json --package Mods/Memoria/field-scene-fixture
```

| Field binding setting | Contract |
| --- | --- |
| `background` | `overlay`, `selectedModules`, or `replace`. Selected replacement requires `replacedModules`. Module 255 selects static tiles. |
| `nativeStates` / `nativeStateSets` | Exact module states or sets of explicit states. Use one form per view. State -1 is the disabled/absent sentinel. Full replacement needs a selector for each animated module. |
| `projection` | `native` shares the field's presentation projection. `authored` uses the named `cameraId` on `cameraInstance` and requires full background replacement. Unmapped slots, uncovered states and incompatible native screen-space effects use native presentation. |
| `frameFit` | `contain` fits the authored camera frame with bars. `crop` fills the output and crops the artwork. `expand` exposes more room at the output aspect. |
| `cameraMotion` | `static` keeps the authored framing fixed. `captureRelative` requires `scrollBaseline` with `x` and `y` in native drawing units, then applies native screen motion relative to that capture. |
| `playback` | Stable `clipId`, `loop`, `autoplay`, `speed` (0–16), and `phaseSeconds`. The presentation clock has no script side effects. Each instance owns its own clip sample. |
| `materials` | Material ID plus optional shading (`source`, `unlit`, `lit`, `unlitShadowReceiver`), alpha (`source`, `opaque`, `mask`, `blend`, `opaqueCoreAndBlend`), culling, casting, receiving and render role. |
| `role` | `surface`, `occluder` or `shadowReceiver`. Proxies are controlled by their material bindings. |

Replacements compete by installed package priority for overlapping field/camera/state selectors. Independent overlays use package-scoped identities. A failed preparation keeps the last published generation. If none exists, the engine presents native scenery. Saving an asset first publishes its immutable hash path and only then publishes the field document that references it. Old GLBs remain available to fields that still reference them.

The runtime accepts self-contained glTF 2.0 GLBs with embedded PNG/JPEG, independent TRS clips (STEP, LINEAR and CUBICSPLINE), standard node transforms, and perspective or orthographic cameras. It rejects unsupported required extensions and external URIs. Field rendering requires a GPU triangle backend. Unsupported optional appearance properties produce diagnostics and mipmapped filters sample the base texture level. Fractional transparency is triangle sorted and may require asset changes when surfaces intersect. Native movie compositing uses native presentation.

See [field lighting](field-lighting.md) for primary shadow sources and writable lighting packages.
