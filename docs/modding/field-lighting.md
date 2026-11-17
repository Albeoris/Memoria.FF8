# Field lighting documents

## Authoring in the game

Open **Mods → Create → Create lighting mod**, enter a name with the keyboard, and create the package. **F2 → Mods** opens the same mod manager when the ordinary game menu is unavailable. To continue an existing project, choose **Edit lighting mod** and select it. Managed packages offer a writable custom copy. Creation and selection enable authoring preview independently of the package's installed activation.

Visit an ordinary FFVIII field and press **Ctrl+L**. The current camera, animation sample and simulation freeze while lighting continues to render. An untouched field retains native lighting. Press **Insert** to add a spotlight above the actor or picked ground point; this switches the field to authored lighting. Select lights in the source list or by their viewport markers.

Use **G** to move a light and **R** to aim its beam. Press **X**, **Y** or **Z** to constrain the operation, then drag or enter a signed number. Enter or a left click confirms; a right click cancels. XYZ handles and rotation rings provide the same operations with the mouse. **Orbit around scene pivot** rotates position and direction together. **Pick scene pivot** changes only the editing pivot. The field camera and painted background stay fixed.

The properties panel provides RGB gradient sliders, color swatches and editable numeric values. Click a numeric value to type a replacement; Enter commits it. Intensity, range, cone half-angles, position and aim also accept keyboard input. One slider or gizmo drag is one undo step. **Ctrl+Z/Ctrl+Y** undo and redo complete edits, including additions, deletions and Primary transfers.

**Ctrl+S / Save field** validates and saves only this scene, leaving the editor open. **Cancel / Close** resumes gameplay; a dirty draft offers **Discard changes** or **Continue editing**. Successful earlier saves remain intact. Authoring stays enabled so the same shortcut opens the next field's independent light list. **Escape** hides or restores all editor panels and guides, keeps the scene frozen, and suspends editing. It does not close the editor. All these shortcuts can be changed in **Editor shortcuts**; preferences stay on the author's machine.

The first explicitly added lamp becomes the Primary shadow source. Other lamps still illuminate models. Deleting that Primary leaves no authored shadow source until another lamp is selected explicitly. Removing every source restores native lighting and shadows. While at least one source remains in the scene, only enabled lamps contribute: disabling all of them or aiming every beam away produces zero model illumination. Disable authoring to restore installed-mod selection, or choose **Enable selected mod for normal play** to use saved lighting without the editor.

The selected package and shortcuts are remembered. Authoring starts off after an application restart; enable it again to edit the remembered package. Installed activation continues independently.

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
  }
}
```

Coordinates use native field space: XY is the ground plane and positive Z is height. Position is finite and may be below ground. Direction is the normalized outward beam vector. RGB uses familiar sRGB channels divided by 255; rendering converts the channels to linear color before accumulating diffuse contributions. Intensity is nonnegative relative strength, not calibrated lumens. Range is a positive finite distance. Cone values are half-angles in degrees and satisfy `0 <= inner < outer <= 85`.

The format permits eight enabled lamps and additional disabled entries, up to 1024 total. IDs are case-sensitive stable identifiers containing 1–80 ASCII letters, digits, underscores or hyphens. Names contain 1–128 readable characters. `primaryLightId` is null or references one lamp in this field. A disabled Primary retains its designation. Deleting the Primary requires clearing its reference; no other lamp becomes Primary automatically. Ground-shadow opacity is in 0–1 and softness is a filter radius in 0–3 texels. All numerical values must be finite.

Enabling a lighting component does not by itself replace native lighting. A field with no document, or a saved scene with `"lights": []` and `"primaryLightId": null`, uses the original renderer path. At least one explicitly authored source is required to switch that field to spotlight shading, even if the source is currently disabled. This rule applies to normal play and authoring preview. There is no inherited source list or global mod light. Normal play selects the first lighting component in existing catalog precedence order for the entire supported field domain. A missing or empty scene retains native lighting and never falls through to another package. Disabling lighting also restores the original renderer path.

Scene documents require every property shown above, reject unknown and duplicate properties, and are limited to 512 KiB. Invalid lighting rejects the candidate mod publication while preserving the last committed generation. Runtime parsing occurs during resource preparation, not per frame.

Saving changes only the current field's lighting payload. It compares the exact loaded revision, stages and flushes a new file, and atomically replaces the payload. An external edit or a conflicting editor save preserves the draft and reports a conflict. Saving does not enable the mod or modify another field, package metadata or unrelated content. Opening an untouched empty field creates no file. Managed and read-only packages require a new writable custom copy; copying retains their content under a new identity and leaves the source intact.

Selected mod, authoring activation, shortcuts, selected lamp, gizmo pivot, panel layout and undo history belong to local editor state and are absent from distributed scene documents.

## Rendering contract

Ordinary opaque and cutout FFVIII field models share a per-pixel spotlight path. Both original and replacement geometry retain world position and normal through perspective-correct interpolation. Indexed native face adjacency smooths connected surfaces within a sixty-degree crease threshold without welding coincident, unrelated vertices. Valid imported normals retain their authored direction and follow the same sampled animation transforms, including inverse-transpose scale handling.

Native field faces use clockwise winding. Field export converts them to glTF counterclockwise winding, and import converts them back. Older field exports with reversed winding or inward normals need re-exporting or correction in the modeling tool before use with lighting. Imported authored normals are preserved; the engine does not guess whether an older asset's normals were intentional.

Only model albedo crosses a local sRGB-to-linear-to-sRGB boundary. Native texture, tint and alpha coverage remain inputs. For each lamp, diffuse response is `max(0, dot(normal, directionToLamp))`; attenuation is `(1 - distance / range)^2` inside the range and zero outside it. Cone weight is smoothstep between the outer and inner cone cosines. Linear RGB multiplied by relative intensity, diffuse response, attenuation and cone weight is added across the enabled lamps. There is no ambient term or minimum brightness. Backgrounds, FFVII, battles, world maps and the inactive path retain their existing color behavior. Additive field primitives retain their existing effect path.

The Primary uses one 1024-by-1024 perspective map at its actual position with the outer cone's full field of view. A capability-checked, single-sample floating-point color target stores reciprocal spotlight depth and native shadow level, with a separate ordinary depth attachment. This target uses opaque writes without alpha-preservation blending. Manual comparisons and nine-tap filtering use the authored softness radius. Receiver-plane depth gradients correct each comparison to the sampled texel centre, so a sloped surface does not shadow itself merely because neighboring samples have different depths. Primary occlusion affects only that lamp's model-lighting term. Ground opacity also follows local Primary color, intensity, range and cone contribution.

Receiving geometry starts at the actor's supported walkmesh triangle and follows native adjacency within the lamp's bounded range. Stable source triangle IDs are submitted once across all actors; overlapping casters share one map. Receivers use the native translucent ordering and opaque depth tests, preserving foreground occlusion and keeping shadows below visible models. Painted walls or furniture are not reconstructed.

Within an authored scene, native shadow suppression, zero shadow radius/level, absent or noncontributing Primary lamps and invalid below-ground projections produce no shadow. A genuinely unavailable shadow target or missing supported ground produces a diagnosed native-shadow fallback only for actors with a contributing Primary. Fallback actors are excluded from the silhouette map. Empty scenes retain native shadows. Renderer capability failure is reported as unavailable and retains legacy gameplay. Shader artifacts are provided for Direct3D 11, Direct3D 12 and Vulkan; shader compilation alone does not establish runtime hardware coverage or performance.
