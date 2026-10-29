# Font and encoding profiles

Open **Tools: Encoding Editor** in the launcher. Profiles keep Unicode-to-image mapping separate from native byte encoding. A single profile can render English, Russian and Japanese from several atlas pages simultaneously. Enabling several profiles retains separate namespaces: each operation selects one profile and an edition/resource/field/dictionary context.

Use **New**, **Duplicate**, **Rename**, **Open / import** and **Save / export profile** to manage profiles. Duplicate creates a new stable ID; renaming keeps the ID. The checkbox beside the profile selector controls package export. Save all enabled documents before exporting to a mod. The desktop editor's drafts and enabled selection last for that window; saved profile files hold the durable definitions and dataset assignments. Closing with unsaved profile edits asks before discarding them.

The **Glyph images** tab searches Unicode text and image IDs. Select a row to edit its text element, image index, atlas ID, optional field context, rectangle and advance/bearings. Several Unicode aliases can select one image. Text elements can contain multiple Unicode scalars, including combining sequences and supplementary characters. Native fragments can retain an explicit `{Glyph 204}` mapping. Choose an existing atlas image by clicking its outlined rectangle; pink highlights the selection. An image need not correspond to its encoded byte value.

Configure relative atlas files in **Dependencies / roles** using the atlas declaration array. Each atlas has an ID, path, width, height, optional SHA-256, and optional palette. PNG, DDS, TEX, TIM and TDW with embedded TIM pixels use the shared decoders. All glyph rectangles must stay within the declared dimensions. Reloading an atlas with different dimensions reports incompatible image assignments rather than silently remapping characters.

The **Native bytes** tab is a separate table. Printable entries can be added or replaced using hexadecimal byte sequences. Existing control entries and reserved opcode prefixes are protected. Selecting **Canonical** makes that sequence the sole canonical reverse mapping for the same text/field. Alternative sequences remain decoding aliases. Import a reviewed profile file for another codec grammar; the basic table editor does not redefine control semantics.

The **Sample** tab uses the same `NativeFontLayout` glyph runs and metrics as the game's bitmap adapter. Enter the explicit edition, resource family and dictionary language; the glyph tab's optional field supplies the field context. It displays the first page at 2× and reports dimensions, missing glyphs, unresolved controls, native encoding failures and overflow. It never shrinks, substitutes a system font, wraps or invents pages. Name/dictionary tokens must already be expanded by the host. Native `{Line}`, `{Next}`, `{End}` and line breaks have explicit layout behavior; unsupported controls are diagnostics. Authoring controls always use ordinary desktop fonts.

## Format 1

`profile.json` uses camelCase properties. Mapping and byte tables are inline arrays, which can be generated externally and imported through **Open / import**. Large Japanese tables do not require manual row entry.

```json
{
  "formatVersion": 1,
  "id": "my-stable-profile-id",
  "displayName": "Mixed scripts",
  "lineHeight": 12,
  "logicalWidth": 320,
  "logicalHeight": 240,
  "domains": [{ "edition": "my-edition", "resourceFamily": "field", "dictionaryLanguage": "en" }],
  "atlases": [
    { "id": "latin", "path": "fonts/latin.png", "width": 256, "height": 256 },
    { "id": "cyrillic", "path": "fonts/cyrillic.png", "width": 256, "height": 256 },
    { "id": "japanese", "path": "fonts/japanese.png", "width": 512, "height": 512 }
  ],
  "images": [
    { "index": 7, "atlasId": "latin", "x": 0, "y": 0, "width": 12, "height": 12, "advance": 7 },
    { "index": 123, "atlasId": "cyrillic", "x": 24, "y": 12, "width": 12, "height": 12, "advance": 8 },
    { "index": 500, "atlasId": "japanese", "x": 0, "y": 24, "width": 12, "height": 12, "advance": 12 }
  ],
  "glyphs": [
    { "text": "A", "imageIndex": 7 },
    { "text": "Я", "imageIndex": 123 },
    { "text": "日", "imageIndex": 500 }
  ],
  "codec": [
    { "hex": "45", "text": "A", "canonical": true },
    { "hex": "80", "text": "Я", "canonical": true },
    { "hex": "1921", "text": "日", "canonical": true },
    { "hex": "01", "text": "{Next}", "canonical": true, "control": true }
  ],
  "reservedPrefixes": ["01"],
  "dependencies": [],
  "assignments": [
    { "role": "Target", "dataset": "ru", "profileId": "my-stable-profile-id", "context": { "edition": "my-edition", "resourceFamily": "field", "dictionaryLanguage": "en" } }
  ]
}
```

These indices and byte sequences are illustrative. They are not a claim about original Russian or Japanese resources. Native mapping entries can carry `field` to select a particular field character table; valid blank text elements are preserved. Each profile's `domains` declares its own native support. A profile without a native domain can supply render mappings but cannot pass strict native encoding validation. `bearingX` and `bearingY` default to zero; atlas `palette` defaults to zero.

Source script language, reference dataset, target identifier, selected font/codec profile and dictionary language are separate dimensions. Assignments use roles `Source`, `Reference` or `Target`, dataset identifiers, stable profile IDs and explicit contexts. Changing the selected reference profile does not change the Target assignment. An exported enabled selection must contain every profile referenced by its assignments.

The game keeps a validated catalog of enabled profile namespaces. Auto resolves the requested role/dataset and edition/resource/field/dictionary context. A field-specific assignment overrides a matching generic assignment; equally specific assignments to different IDs are an error. Assignments may refer to another profile in the enabled catalog. The explicit Native font profile setting overrides Target selection; reference operations continue using their own assignments. `Auto` is reserved for the selector and cannot be used as a profile's stable ID. Duplicate IDs across copied packages are allowed only for byte-identical profile/dependency closures.

Profile and dependency content is checked in the background, including changes that preserve file timestamps. A malformed or missing dependency rejects the new catalog and keeps the last validated catalog active with a reload diagnostic. Profiles are discovered only at `Encoding/Name/profile.json`; nested dependency files do not become additional profiles. Failed native selection does not choose another profile based on the text being displayed.

## Strict encoding and native support

`NativeStrictCodec.Encode` reports unrepresentable elements/controls at UTF-16 positions and returns no output bytes on failure. Pass the output resource's byte limit explicitly; output offset/directory constraints remain the responsibility of that resource's existing writer. `{Page}` is not lowered to `{Next}`. Unknown or incomplete native sequences fail with byte positions. Prefix collisions, reserved printable assignments and ambiguous canonical mappings fail profile validation.

Decode results retain each original byte sequence. `OriginalBytes()` preserves aliases for unchanged imported tokens. Encoding the decoded Unicode text instead uses the explicitly canonical sequence. These are different export policies; no automatic byte-identical claim is made for edited or canonicalized text.

`NativeInstalledFontProfile.CreateEuropean` builds the supported European PC font grid from user-supplied native atlas pixels, the actual TDW nibble-width table and the existing native text decoder. It uses the native 21-column 12×12 image grid and special glyph advances. It embeds no original font. Japanese native creation requires explicit escaped-page/field image assignments; it deliberately rejects the European factory instead of treating Japanese as an arbitrary 256-character table.

Three compatibility statements remain distinct: a bitmap sample establishes **native-style presentation**, supported resource/control/byte limits can establish **native-resource compatibility**, and only a tested supported export/install path establishes **original-game deployment**. This profile editor supplies no universal executable or archive patcher. A mixed-script Memoria profile does not establish support for switching encodings inside an unmodified original executable.

## Portable mod export and CLI

Run from a built localization CLI:

```text
Memoria.FF8.Localization encoding validate --input profile.json
Memoria.FF8.Localization encoding validate --input profile.json --text "AЯ日" --edition my-edition --resource field --dictionary en
Memoria.FF8.Localization encoding export --input profile.json --output MyMod --name Mixed
```

The launcher exports all enabled profiles through the same service. Each destination must be new: `<mod>/Encoding/<name>/profile.json`, with its full declared atlas/dependency closure copied beside it. Dependencies must be portable paths using `/`; absolute paths, `..`, case collisions and reparse points/symlinks are rejected. Optional hashes and actual atlas dimensions are checked before publication. Auxiliary table/dictionary files must be declared in `dependencies`; they do not implicitly import another profile or inherit an external cache. Each profile is self-contained. Stage validation completes before profile directories are published, and a failed selection rolls back directories created by that export.

Original assets may be loaded from a user's configured installation for local work. Do not commit or distribute original game assets without appropriate rights.
