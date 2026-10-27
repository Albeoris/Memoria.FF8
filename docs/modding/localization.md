# Translating a localization package

[Documentation](../README.md) / Modding

Use [the creation and activation guide](../players/localization.md) to create a disabled project. This page describes its files, identities and editing rules. Source text is read from the user's installation or selected translation projects; original game assets are not distributed with Memoria.

## Package files

```text
<package>/
  mod.ini
  Localization/
    localization.manifest.json
    Fields/<logical-file>.json
    Magic/<logical-file>.json
```

Split layout groups payloads by **category/file**, without recreating archive nesting. A JSON or XLSX payload is produced for each logical file. All-in-one layout instead uses `Localization/localization.json` or `Localization/localization.xlsx`; an all-in-one workbook has category worksheets. Worksheet names may have explicit safe mappings in the manifest when a category is not an Excel-safe name.

JSON/XLSX documents and technical sidecars have a 1.5 GiB file ceiling; XLSX also limits total decompressed ZIP data to 1.5 GiB. Individual PNG files retain a separate 128 MiB ceiling. Split layout also allows unchanged payloads to be reused during live reload.

The manifest records schema/project identity, optional base-project relationship, base game language and Target, ordered references, mapping version, compatible resource variants and payload layout. Paths are relative to the package. Localization files belong under `Localization/`; they are separate from native scene patches under `fields/`.

XLSX exports also contain `Localization/localization.extensions.json`. This sidecar retains structured technical context, typed metadata and missing-versus-empty states that cannot be represented by an ordinary blank cell. Keep it with the workbook. Known technical JSON such as call contexts, context bindings and place-ID arrays uses structured metadata; it is not squeezed into oversized Excel text cells.

## Rows and language roles

| Column/property | Editing rule |
| --- | --- |
| `File` | Stable globally unique logical filename stem. Keep it unchanged. |
| `Key` | Stable source identity. Keep it unchanged when sorting, translating or changing format. |
| `Target` | Your translated text. A missing, null or empty value contributes no override. |
| `Reference [dataset]` | Ordered prose references, including the base language. Labels and dataset IDs are declared by the manifest. Missing or ambiguous correspondence stays blank. |
| `ReferenceKeys` | Mapped native identities for the reference columns. Keep this correspondence information unchanged. |
| `Field Name` | Human-readable field context when established; it can be blank with an explicit status. It does not determine identity. |
| Other metadata | Retain field names/statuses, source/script fingerprints, call bindings, correspondence alternatives and unknown translator metadata. |

JSON groups records as `category → File → Key → role/metadata properties`. XLSX shows `File`, `Key`, `Target`, the ordered reference columns, then `ReferenceKeys`, with frozen identity columns, wrapped text and filters. Technical columns are hidden and retained for conversion and validation; keep them and the sidecar intact. There is no separate Source prose column. Edit text as literal strings; formulas in role cells or headers are rejected. A translator string longer than Excel's cell limit must be edited in JSON or deliberately shortened. It is not silently moved into a technical sidecar. Save and close an editor before conversion or validation so the input is a consistent file snapshot.

References do not become runtime Target overrides. If a selected project has no translated Target for a row, its source/reference dataset has a gap rather than a copy of native text. Project datasets have stable `project:<project-id>` identities distinct from their arbitrary Target labels. The effective profile dataset uses the explicit selected stack; own-contribution export and effective-profile export are different operations.

## Stable keys

The base game language supplies the complete row inventory and every key. Field keys use that language's original zero-based MSD table ordinal:

```text
File: bgroom_6
Key:  $en_bgroom_6_0000
```

The ordinal is padded to at least four digits. It is not a byte offset, a source-code line, or a row number after sorting. With base `en`, every exported key retains `$en_`, even with references `jp,en`; rows found only in Japanese are excluded. Each base keeps its original slots, including empty slots and same-language aliases. Proven field correspondence supplies reference text and mapped identities in `ReferenceKeys`; identical wording does not establish correspondence. Other categories use only their implemented identity/mapping contracts. Ambiguous or unsupported counterparts do not supply guessed text.

Where a source slot has distinct proven call contexts, a context-specific key adds `_ctx_<stable-hash>`. Preserve that key and its bindings. It is not a second ordinal to renumber by hand.

Other original resources use their registered table/entity identities, such as a kernel collection, role and row index. Engine-owned System strings use registered semantic IDs, for example `$en_interface_system_main.junction`. Unknown identities and contradictory/case-colliding filenames are diagnosed. Do not manufacture new original-game rows or use translated names as keys.

System messages can be templates containing numbered arguments such as `{0}` and `{1}`. Preserve these arguments while translating the surrounding prose; their values include current player names, quantities and paths. The English base reference retains the authored English template. Selecting another reference does not claim an authored translation exists for it.

## Field controls and choices

Preserve required dynamic placeholders such as `{Var 2}`, `{Char Squall}` or key/name tokens, including their occurrence counts. Their contents come from runtime owners, including player-assigned names. Unicode translation text does not need to fit the original game's byte encoding.

Field presentation metadata, when present, is a leading tag with complete blocks in this order: `XY` position, `WH` size, then `FLBC` choices. For example, `{F1L2B1C2}` describes choices on zero-based lines 1–2, initially selecting line 1 and cancelling to line 2. This is a syntax illustration, not a new native choice contract.

For `ASK`/`AASK`, **F** and **L** identify the first and last selectable lines; **B** identifies the initial line and **C** the native cancellation result. A translated heading may move selectable lines, but the number/order of semantic options and the relative initial/cancel identities must remain unchanged. An out-of-range native cancel result must also remain unchanged. The translated body must contain the declared lines, and window sizes must be positive.

Keep the original script fingerprint and call bindings. A key reused by incompatible calls needs its exported context variants; do not solve it by adding a guessed global offset. For dynamic choices, edit the structured **Choice Line Map**, mapping each selectable native `SourceLine` to its translated `TargetLine`. An empty map means unchanged line positions. At presentation time every evaluated native option must map completely, contiguously and in the original order. Initial and cancel identities are preserved, including native out-of-range sentinels. These checks change presentation only; they do not patch JSM or change branch results.

## Structured Information and artwork

Information bodies preserve `{Info Gate XX}`, `{Info Number XX}` and `{Info Link XXXX}` tokens. Translate prose around them while retaining the original control stream and its structure. Activation checks the translation against installed native controls, rather than trusting edited reference cells.

The manifest's `artwork` declarations bind native image identities, source hashes and canvas sizes to files under `Localization/Artwork/`. Each declaration has a required Source PNG and an optional Target PNG path. Create the Target file at that declared path to override an image; an absent Target retains the lower-priority image or installed original. Use the original canvas or a uniform integer enlargement. Do not edit native identity, hash or geometry metadata to force compatibility. PNG overrides participate in stack priority and live publication; malformed updates retain the last valid presentation.

Conversion and patch creation preserve Source PNG files and artwork declarations. A new patch starts without Target images. Image lettering is edited in the PNG, not extracted automatically as prose. Native European `{Glyph N}` wordmark fragments retain byte identities and source-font rectangles; they are not ordinary Unicode letters.

## Validate and convert from the command line

Run these source-build examples from the product root containing `Build.ps1`. Set the environment variables to user-selected installation/input/output locations; output must be a new package directory.

```powershell
dotnet run --project tools/Memoria.FF8.Localization -c Release -- languages --game-dir "$env:MEMORIA_FF8_GAME_DIR"

dotnet run --project tools/Memoria.FF8.Localization -c Release -- export --game-dir "$env:MEMORIA_FF8_GAME_DIR" --script-base en --references jp,en --target "en (Alternate translation)" --name "Alternate translation" --format json --layout split --output "$env:MEMORIA_FF8_LOCALIZATION_OUTPUT"

dotnet run --project tools/Memoria.FF8.Localization -c Release -- validate --input "$env:MEMORIA_FF8_LOCALIZATION_INPUT"

dotnet run --project tools/Memoria.FF8.Localization -c Release -- convert --input "$env:MEMORIA_FF8_LOCALIZATION_INPUT" --output "$env:MEMORIA_FF8_LOCALIZATION_OUTPUT" --format xlsx --layout single

dotnet run --project tools/Memoria.FF8.Localization -c Release -- patch --input "$env:MEMORIA_FF8_LOCALIZATION_INPUT" --output "$env:MEMORIA_FF8_LOCALIZATION_OUTPUT" --name "Translation corrections"
```

Formats are `json` and `xlsx`; layouts are `single` and `split`. Export accepts ordered comma-separated `--references jp,en,fr`; `--script-base` selects the base game language. The legacy `--source jp` option remains accepted and prepends `jp` to the references. Older projects with a Source column are read with that dataset as the first reference; conversion preserves their row and project identities. Probe actual availability first: requesting a dataset does not install it. Validate and convert are offline operations. Ctrl+C cancels export/conversion, and completed new packages remain disabled. Existing output directories are rejected.

The export summary reports **Resource/coverage diagnostics** from the manifest's `sourceGaps` array across selected datasets. These can describe unsupported sections, unavailable banks, failed decoders or missing correspondence; they are not a missing-translation count. Review their category/resource/reason entries when a reference is blank.

Validation checks package/schema, identities, role columns, workbook structure and retained data. Activation additionally checks current mapping/resource/script compatibility and field presentation bindings. Successful conversion does not prove that an unsupported display consumer or gameplay language has become playable. See [current coverage](../players/localization.md#coverage-and-current-limits) and [the field audit command](../developers/localization-audit.md) for the separate evidence workflow.
