# Field lighting documents

## Authoring in the game

Open **Mods → Create → Create lighting mod**, enter a name with the keyboard, and create the package. **F2 → Mods** opens the same mod manager when the ordinary game menu is unavailable. To continue an existing project, choose **Edit lighting mod** and select it. Managed packages offer a writable custom copy. Creation and selection enable authoring preview independently of the package's installed activation.

Visit an ordinary FFVIII field and press **Ctrl+L**. The current camera, animation sample and simulation freeze while lighting continues to render. An untouched field retains native lighting. Press **Insert** or **Add light** to place a spotlight above the actor or picked ground point. **Add fill** places a broad, dim spotlight aimed toward the same area. Both switch the field to authored lighting.

Select lights in the source list or click their markers. Drag a marker to move its light in the camera plane. The panels hide during the drag so the marker, beam and handles remain visible. Release to commit or right-click to cancel. **G** moves a light and **R** aims its beam. Press **X**, **Y** or **Z** to constrain the operation, then move the pointer or enter a signed number. Enter or a left click confirms. The axis handles and rotation rings offer the same controls with the mouse. **Rotate light around pivot** rotates the light's position and direction together. Use **Pick scene pivot** to choose a new pivot on the walkmesh.

Scroll over the scene to zoom from 0.5? to 4? around the pointer. Middle-drag to pan and use **Reset view** to restore framing. Zoom keeps the painted background, models, and editor guides registered. **3D inspection** shows models and the walkmesh without the painted background. Alt + middle-drag orbits the inspection camera around the pivot. **Backdrop preview** limits the orbit angle and projects the visible painted tiles using their original drawing depth. Surfaces can stretch or separate because the stage contains only the painted view and approximate depth. **Game camera** restores the exact painted composition. **Gray material** shows model shading with neutral albedo, and **Wrap preview** softens the lighting falloff around surfaces for inspection. These view controls only affect the editor preview.

The properties panel provides RGB gradient sliders, color swatches and editable numeric values. Click a value and press Enter after typing a replacement. One slider, marker or handle drag is one undo step. **Ctrl+Z/Ctrl+Y** undo and redo complete edits, including additions, deletions and Primary transfers.

**Ctrl+S / Save field** validates and saves only this scene, leaving the editor open. **Cancel / Close** resumes gameplay; a dirty draft offers **Discard changes** or **Continue editing**. Successful earlier saves remain intact. Authoring stays enabled so the same shortcut opens the next field's independent light list. **Escape** hides or restores all editor panels and guides, keeps the scene frozen, and suspends editing. It does not close the editor. All these shortcuts can be changed in **Editor shortcuts**; preferences stay on the author's machine.

**Add light** assigns the Primary shadow source when the scene has no lamps. **Add fill** preserves the current Primary assignment. Deleting the Primary leaves no authored shadow source until another lamp is selected explicitly. Removing every source restores native field lighting. **Config → Graphics → Native field shadows** is on by default and controls native circular shadows, including the native fallback under authored lighting. While the scene has sources, enabled lamps contribute where their beams reach models. Disable authoring to restore installed-mod selection, or choose **Enable selected mod for normal play** to use saved lighting without the editor.

The selected package and shortcuts are remembered. Authoring starts off after an application restart; enable it again to edit the remembered package. Installed activation continues independently.

Use **Receivers overlay** to see the walkmesh triangles currently selected for silhouette shadows and the authored shadow patches. **Add floor** places a horizontal rectangle using two opposite corners. The first click uses the picked walkmesh height or the scene pivot height. **Add wall** uses two endpoints along the bottom edge and gives the wall an initial height. Use the arrow buttons to select a patch, then edit its world-space center, width, depth or height, and heading. A wall's bottom stays fixed while you edit its height. Changes to patches are part of the field's undo and save history. The overlay is local to the editor.

## Package format

A lighting mod is a normal Memoria package with `mod.json`, its existing activation preferences, and the marker `lighting/component.json`:

```json
{
  "schemaVersion": 1
}
```

The marker activates the lighting component even when the package contains no scene documents. Creating a mod starts it disabled and creates no lamps. Authoring preview selects that package independently of installed-mod activation and order.

Each authored field has one complete document at `lighting/fields/<fieldName>.json`. The selector is the native field resource name, shared by all cameras in that field. It contains ASCII letters, digits or underscores, with a maximum length of 64. The path and document selector must match. The `fields/` directory at the package root belongs to scene patches and is not used for lighting.

```json
{
  "schemaVersion": 1,
  "fieldName": "example_field",
  "lights": [
    {
      "id": "ceiling-01",
      "name": "Ceiling lamp",
      "enabled": true,
      "type": "spot",
      "position": [0, 0, 300],
      "direction": [0, 0, -1],
      "color": [1, 0.8, 0.6],
      "intensity": 1,
      "range": 900,
      "innerConeDegrees": 30,
      "outerConeDegrees": 60
    }
  ],
  "primaryLightId": "ceiling-01",
  "groundShadow": {
    "opacity": 0.5,
    "softness": 1
  },
  "shadowReceivers": [
    {
      "id": "wall-01",
      "kind": "wall",
      "enabled": true,
      "center": [20, 60, 100],
      "width": 150,
      "extent": 200,
      "headingDegrees": 0
    }
  ]
}
```

Coordinates use native field space: XY is the ground plane and positive Z is height. Position is finite and may be below ground. Direction is the normalized outward beam vector. RGB uses familiar sRGB channels divided by 255; rendering converts the channels to linear color before accumulating diffuse contributions. Intensity is nonnegative relative strength, not calibrated lumens. Range is a positive finite distance. Cone values are half-angles in degrees and satisfy `0 <= inner < outer <= 85`.

The format permits eight enabled lamps and additional disabled entries, up to 1024 total. IDs are case-sensitive stable identifiers containing 1–80 ASCII letters, digits, underscores or hyphens. Names contain 1–128 readable characters. `primaryLightId` is null or references one lamp in this field. A disabled Primary retains its designation. Deleting the Primary requires clearing its reference; no other lamp becomes Primary automatically. Ground-shadow opacity is in 0–1 and softness is a filter radius in 0–3 texels. All numerical values must be finite.

Enabling a lighting component does not by itself replace native lighting. A field with no document, or a saved scene with `"lights": []` and `"primaryLightId": null`, uses the original renderer path. At least one explicitly authored source is required to switch that field to spotlight shading, even if the source is currently disabled. This rule applies to normal play and authoring preview. There is no inherited source list or global mod light. Normal play selects the first lighting component in existing catalog precedence order for the entire supported field domain. A missing or empty scene retains native lighting and never falls through to another package. Disabling lighting also restores the original renderer path.

Scene documents may omit `shadowReceivers`, which defaults to an empty array. Other scene properties and every property in each receiver entry are required. Documents reject unknown and duplicate properties and are limited to 512 KiB. Invalid lighting rejects the candidate mod publication while preserving the last committed generation. Runtime parsing occurs during resource preparation, not per frame.

Each field supports up to 128 rectangular patches with unique IDs. The kind is `floor` or `wall`. Width and extent are positive world-space distances. Extent is depth for a floor and height for a wall. Heading ranges from -180 to 180 degrees and rotates the width axis from +X toward +Y.

Saving changes only the current field's lighting payload. It compares the exact loaded revision, stages and flushes a new file, and atomically replaces the payload. An external edit or a conflicting editor save preserves the draft and reports a conflict. Saving does not enable the mod or modify another field, package metadata or unrelated content. Opening an untouched empty field creates no file. Managed and read-only packages require a new writable custom copy; copying retains their content under a new identity and leaves the source intact.

Selected mod, authoring activation, shortcuts, selected lamp, gizmo pivot, panel layout and undo history belong to local editor state and are absent from distributed scene documents.

## Rendering contract

Ordinary opaque and cutout FFVIII field models share a per-pixel spotlight path. Both original and replacement geometry retain world position and normal through perspective-correct interpolation. Indexed native face adjacency smooths connected surfaces within a sixty-degree crease threshold without welding coincident, unrelated vertices. Valid imported normals retain their authored direction and follow the same sampled animation transforms, including inverse-transpose scale handling.

Native field faces use clockwise winding. Field export converts them to glTF counterclockwise winding, and import converts them back. Older field exports with reversed winding or inward normals need re-exporting or correction in the modeling tool before use with lighting. Imported authored normals are preserved; the engine does not guess whether an older asset's normals were intentional.

Only model albedo crosses a local sRGB-to-linear-to-sRGB boundary. Native texture, tint and alpha coverage remain inputs. For each lamp, diffuse response is `max(0, dot(normal, directionToLamp))`; attenuation is `(1 - distance / range)^2` inside the range and zero outside it. Cone weight is smoothstep between the outer and inner cone cosines. Linear RGB multiplied by relative intensity, diffuse response, attenuation and cone weight is added across the enabled lamps. There is no ambient term or minimum brightness. Backgrounds, FFVII, battles, world maps and the inactive path retain their existing color behavior. Additive field primitives retain their existing effect path.

The Primary uses one 1024-by-1024 perspective map at its actual position with the outer cone's full field of view. A capability-checked, single-sample floating-point color target stores reciprocal spotlight depth and native shadow level, with a separate ordinary depth attachment. This target uses opaque writes without alpha-preservation blending. Manual comparisons and nine-tap filtering use the authored softness radius. Receiver-plane depth gradients correct each comparison to the sampled texel centre, so a sloped surface does not shadow itself merely because neighboring samples have different depths. Primary occlusion affects only that lamp's model-lighting term. Ground opacity also follows local Primary color, intensity, range and cone contribution.

Native receiving geometry starts at the actor's supported walkmesh triangle and follows native adjacency within the lamp's bounded range. Authored floor and wall patches extend shadow coverage independently of the walkmesh and have no effect on actor movement. A wall can receive a shadow from either side. Coplanar overlaps between authored patches and active walkmesh triangles are drawn once. Receivers use native translucent ordering and opaque depth tests. Patches receive shadows and add no new occluders or visible geometry to the scene.

Within an authored scene, native shadow suppression, zero shadow radius or level, and an absent or noncontributing Primary lamp suppress casting. An unavailable shadow target can use a diagnosed native-shadow fallback when the actor has a native receiver path. A missing support triangle can also use the native fallback if no authored patch can receive the actor's shadow and the Primary is above the native shadow center. Fallback actors are excluded from the silhouette map. Empty scenes retain native shadow behavior. Renderer capability failure is reported as unavailable and retains legacy gameplay. Shader artifacts are provided for Direct3D 11, Direct3D 12 and Vulkan; shader compilation alone does not establish runtime hardware coverage or performance.
