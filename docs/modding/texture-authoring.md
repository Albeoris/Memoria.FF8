# Texture authoring

Open **F2 ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â€šÂ¬Ã‚Â ÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢ Textures** while the game is running. Choose **Current scene**, **All game textures**, or **Selected textures**, then choose **Original** or **Resolved**. Original reads the selected installation provider, including its installed HD artwork; Resolved includes the active replacement chain. The scene list follows presented images and expands derived atlases into their authored dependencies. Shared field-model rows have one source-owned identity across their field usages; required eye-phase and background-editing companions are labelled in the export report. Current scene is unavailable for unsupported presentation states, movies, FFVII scenes and software diagnostic rendering. FFVII texture authoring is not claimed.

Shared field-model rows keep one identity across field usages. Source-declared eye phases export as companions; keep these when editing animated models. Package priority applies separately to each phase, and unspecified phases retain the source contract.

Use the search and category/owner/member/palette details to select an exact source. Checkboxes survive filtering. The comparison view shows the imported image beside the installed original and active resolved result. Friendly names and owner/member/palette details share the same picker for export and manual import matching.

## Export and edit

Export to a working folder. Ordinary textures are PNG files under `textures/`, accompanied by `.texture.json` metadata and redundant embedded identity. Keep the relative paths. PNG metadata may be stripped by an image editor without losing identity when the canonical path remains intact. Keep the companions for source fingerprints, baseline hashes and native coverage information. DDS is accepted on import; the export command currently writes PNG.

Each export also writes `texture-export-index.json`, `texture-export-report.txt` and `EDITING.txt`. The index records the selected scope and completed relative outputs, while the report lists counts, duplicate aliases and discovery/processing diagnostics. An interrupted export is marked **INCOMPLETE**; its completed files remain editable and importable. These counts describe the scanned selection, not a promise that unsupported or unscanned archive contents were exported. Choose a fresh export folder: existing indexes, images and companions are preserved, including files created while export is running.

Fields export as layered `fields/<field>.ora` documents with source companions instead of duplicate loose tile images. Preserve all Pupu layer identities, animation/blend composition and the `.ora.texture.json` companion. The importer checks the original MIM and MAP fingerprints. Painting and uniform integer enlargement are supported; changing native composition, deleting required layers or flattening the document is rejected. Paint a layer transparently instead of deleting its identity. Runtime output is a coherent set of canonical tile images; the authoring ORA stays in the working folder.

Native STP coverage is separate from editor opacity. For supported native layouts, export makes STP pixels visibly opaque and retains their native flags. Import restores those flags when an edited pixel is opaque, while retaining authored transparency. Ordinary desktop RGBA artwork keeps ordinary alpha. Full native effect sources are exported rather than transient VRAM sampling windows; the runtime crops the source-owned replacement and applies its existing effect alpha contract.

## Import and review

Use **Import folder** or **Import files** for PNG, DDS and supported ORA inputs. Select a new or existing native package inside the discovered writable `Mods/Memoria` root. Unknown identities and conflicting metadata require the same catalog picker used for export, or must be left unchecked. Dimensions alone never assign a target. Duplicate targets require selecting one input.

The review selects meaningful changed images by default. After a successful import, schema version 3 of `.memoria-texture-authoring.json` records target matches and semantic source/destination snapshots separately for each destination package. Exported resolved artwork supplies its own baseline, so an unedited resolved export is omitted on first import. Keep this local file out of the distributed package.

- An unchanged working image leaves a destination-only edit intact.
- Changes on both sides are unchecked until the author explicitly selects replacement.
- Restoring the original pixels after importing an edit is a new change and can be imported.
- Missing files and unchecked rows leave existing replacements intact; they do not delete them.
- Destination comparisons use decoded semantic appearance, including target/layout and native coverage, so an equivalent re-encode is omitted. Encoded hashes still protect the reviewed files from concurrent replacement.

Reviewed inputs and destination files are revalidated and held with leases through installation; changed inputs require review again. New packages receive a stable descriptor and local enablement for **Import and apply**. Existing IDs, descriptor text, settings, priority and unrelated contributions are preserved. An existing disabled package stays disabled. The result distinguishes saved content from resident targets using it; nonresident or overridden targets are not all labelled applied. Use the normal Mods controls for enablement and priority. Saved content and effective package ownership are reported separately; normal Mods diagnostics identify overrides.

Repeat the import action to reread the working folder after editing. Installed package files use the existing debounced mod watchers; incomplete or invalid revisions retain the last committed generation. External working folders are reread explicitly. **Refresh** requests content revalidation rather than relying only on file size or timestamp.

## Native package paths

A native package contains the existing root `mod.json`, optional local `mod.ini`, and texture contributions under `textures/`. Images elsewhere, such as item icons or README illustrations, are not texture contributions. Do not add texture fields or a speculative schema version to `mod.json`.

The canonical grammar is:

```text
textures/<game>/<encoded owner segments>/members/<encoded subresource>/palette-<integer>/<encoded variant>.png
```

`.dds` is also accepted. Each encoded component starts with `r`. Lowercase ASCII letters, digits and `-` remain literal; every other UTF-8 byte becomes uppercase `%HH`. Owner separators stay directory separators. The empty variant becomes `r`. For the synthetic identity `ff8`, `synthetic/checkerboard.tex`, `image`, palette `0`, empty variant:

```text
textures/ff8/rsynthetic/rcheckerboard%2Etex/members/rimage/palette-0/r.png
```

The full canonical path identifies the target. The final variant filename alone is generally not unique. Renamed or moved images remain supported through valid explicit metadata or a confirmed picker mapping. The native resolver compares embedded PNG identity with its sidecar and canonical path. Conflicting overlapping source, semantic, native-coverage or dimension claims fail; missing legacy fields can be supplied by the other metadata copy. Malformed metadata is an error even when the path looks recognizable; contradictory claims require correction.

Exceptional mappings use a versioned `.texture.json` sidecar relative to its image directory. The importer writes these when needed. Schema version 1 requires a target and an image; an optional contract records source layout and logical dimensions. Unknown required semantics and duplicate properties fail; optional `extensions` data survives parsing. Exported authoring companions are also recognized beside their canonical PNGs.

Resolution order is explicit localization artwork, native Memoria textures, the existing legacy resolver chain, then provider replacement/original fallback. Native packages retain existing identity, priority and enablement rules. Two definitions of an exact target inside one package are errors. Changing a target from PNG to DDS while leaving the previous canonical file or exceptional mapping may create such a duplicate; remove the obsolete definition deliberately before retrying.

When two enabled native packages define the same target, the lower-precedence package receives a `Memoria.TextureOverridden` diagnostic naming the effective winning package and path. This notice does not reject the generation or change priority. Use the normal Mods diagnostics and priority/enablement controls to choose the owner.

## Limits and validation status

The catalog includes supported field/model/menu/world/card resources and source-owned indexed effect resources. Coverage depends on the selected provider. Some procedural/direct-color effect sources cannot yet be exported, and some effect formats outside the known source profiles are not cataloged. A source edition label alone does not establish compatibility: the selected logical target, layout and canvas must agree.

The compact installed scans returned 7,055 Classic nodes and 6,538 Remastered nodes, including 851 compound field-workspace parents in each. A parent can expand into many tile targets, so these are not full-game texture totals. The scans reported skipped, unsupported and failed resource reads. After correcting an unproved category-based classification, Classic has 1 failed read and Remastered has 56; these remain coverage gaps. See the [coverage matrix and measured limits](../developers/texture-authoring.md#installed-catalog-coverage-and-measurements) before treating an export as complete coverage of an installation.

Portable tests cover identity, metadata, source snapshots, selective dependency refresh, alpha preservation, native alias cropping, ORA structure, and disk/publication rollback. Actual GPU field, model and UI publication checks passed with Classic and Remastered resources; model checks also exercised both mixed provider directions. Remastered texture-only package eligibility through discovery, enablement and explicit import is currently being corrected; its watched-host acceptance case has not passed. A complete interactive installed-game acceptance run and full-game export/frame-time benchmark also remain required before claiming the entire specification is complete. See [developer details](../developers/texture-authoring.md).

Run [Create-TextureExample.ps1](../../tools/Create-TextureExample.ps1) to create a redistributable procedural package fixture. Its identity is deliberately synthetic and does not replace any installed game texture.
