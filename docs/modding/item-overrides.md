# Native item overrides

Place a native mod under `Mods/Memoria/<folder>/`, with `mod.json` at its root. This layout defines one item and its artwork.

```text
Mods/Memoria/my-inventory-mod/
  mod.json
  items/
    chapters/
      example.json
      illustration.png
```

The descriptor defines the mod's stable identity, title, group and descriptions.

```json
{
  "id": "my-inventory-mod",
  "name": "Inventory Illustration",
  "package": "Memoria",
  "description": {
    "en": "View an illustration from the item menu.",
    "fr": "Affiche une illustration depuis le menu des objets."
  }
}
```

Use a lowercase slug for `id`. Keep it when renaming the mod, moving its folder, or changing its `package`. The package is the group shown in **Mods**. Its **Settings** contains a toggle for each component. The description follows the published content language, including any active localization target. Japanese accepts `ja` or `jp` as aliases for the same locale. Supply `en` as the fallback.

Memoria discovers JSON documents recursively under `items/`. Each document identifies one existing inventory item by ID and declares one or both of `type` and `use`.

```json
{
  "id": 1,
  "type": "Magazine",
  "use": {
    "action": "ShowImage",
    "image": "./illustration.png"
  }
}
```

`Magazine` uses the common magazine classification and icon. `ShowImage` opens the supplied PNG from the field or world map item menu, preserves transparency and aspect ratio, and leaves item stock and progression intact. **Back** returns to the same item selection. Battle use follows the original item definition. An omitted property inherits the value from the next active package in precedence order, or from the game. Each mod may declare an item once, regardless of filenames.

A relative image path starts in the directory of its item JSON. Use `./illustration.png` or `illustration.png` for a neighboring image. A single leading slash starts at the mod root, so `/items/chapters/illustration.png` names the same file in this example. Paths use forward slashes and must match the file's case. Parent segments may resolve within the same mod. During preparation, the engine resolves each image to a canonical absolute path inside that mod and retains the validated content for the active generation. Escapes, links and native absolute paths are rejected. Invalid item documents or images prevent publication of the candidate mod generation.

For an official download, the publisher packages the same mod root and generates the install inventory and compatibility metadata. The authoring files stay the same.
