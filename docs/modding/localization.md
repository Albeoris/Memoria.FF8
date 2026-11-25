# Translating a localization package

[Documentation](../README.md) / Modding

Use [the creation and activation guide](../players/localization.md) to create a disabled project. This page describes its files, identities and editing rules. New-project source text and artwork are read from the user's original installed archives, independently of installed mods and the active profile; original game assets are not distributed with Memoria.

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

English exports declare their `sourceEdition` and retain the same canonical identities across Classic and Remastered. The engine-owned correspondence catalogue contains the multilingual field groups and the verified English edition pairs. Memoria builds the active bindings from the installed edition's original assets. Shared `Target` text stays in the regular JSON or XLSX payload. When a row needs different wording or controls, its optional `Edition Targets` object holds text keyed by profile, such as `Classic:en`. JSON exposes the object in the row, and XLSX retains it in its standard metadata sidecar. The original edition's ordinary native metadata remains part of the normal export. Existing packages with `editionBindingPaths` can still be read for migration.

`sourceReferenceLanguage` explicitly designates the translation source and is independent of `fieldScriptBaseLanguage`, the Target identifier and reference column order. Older projects recover an unambiguous `sourceDatasets` primary declaration, then legacy Source metadata, then the first reference/base default. Project review identifies inferred choices. Contradictory explicit and primary declarations are rejected. Encoding profiles retain their separate IDs and self-contained `Encoding/<name>/profile.json` dependency closures when a project is converted or used to create a patch.

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

Workbench saves update the selected package in place. They patch one JSON Target property or workbook Target cell and retain unrelated rows, unknown metadata, workbook formatting, comments, annotation columns and nonpayload sheets. Canonical row identities, rather than remembered Excel row numbers, locate the destination. A semantic external change to any part of the same row requires Base/Ours/Theirs review; unrelated-row changes can be retained automatically. File formatting alone does not create a row conflict. A locked workbook produces a retryable File busy result and retains the draft.

A source-seeded origin save writes one complete nonempty Target string. It copies the designated source body, retains verified base choice/size metadata where supplied, and writes XY through the existing leading prefix grammar. It does not create a separate coordinate store. Clearing the complete Target also clears its inline presentation overrides. User drafts and journals belong in user authoring state, outside the distributed package.

References do not become runtime Target overrides. Native dictionary controls use language-qualified tags such as `{TermEN Status}` and `{TermDE AttackOneEnemy}`. Their operands are positional references into that language's dictionary. Unqualified `{Term ...}` is rejected; retained Target controls must match the native base language. Exported references expand these terms into readable text; hidden native Source text retains the original controls. A Target may translate dictionary text literally instead of retaining a Term control; any retained Term control must still be valid. Other dynamic placeholders remain visible and must be preserved when translating.

## Stable keys

The base game language supplies the complete row inventory and every key. Field keys use that language's original zero-based MSD table ordinal:

```text
File: bgroom_6
Key:  $en_bgroom_6_0000
```

The ordinal is padded to at least four digits. It is not a byte offset, a source-code line, or a row number after sorting. With base `en`, every exported key retains `$en_`, even with references `jp,en`; rows found only in Japanese are excluded. Each base keeps its original slots, including empty slots and same-language aliases. Proven field correspondence supplies reference text and mapped identities in `ReferenceKeys`; identical wording does not establish correspondence. Other records are matched by their complete common key after removing only the leading language prefix: `$en_c0m003_dialogue_battle_0000` matches `$jp_c0m003_dialogue_battle_0000`. This is one rule for all categories, with no category whitelist or language-specific index shifts. Matching keys identify original table slots; they do not assert identical wording or paragraph boundaries in every localization. Missing source slots stay absent. Engine-authored System messages have English originals unless an actual translated dataset supplies counterparts; English is not relabeled as another language. Entirely unavailable reference columns show an input notice when selected in Excel.

Where a source slot has distinct proven call contexts, a context-specific key adds `_ctx_<stable-hash>`. Preserve that key and its bindings. It is not a second ordinal to renumber by hand.

Other original resources use their registered table/entity identities, such as a kernel collection, role and row index. Engine-owned System strings use registered semantic IDs, for example `$en_interface_system_main.junction`. Unknown identities and contradictory/case-colliding filenames are diagnosed. Do not manufacture new original-game rows or use translated names as keys.

Use `$en_interface_system_title.wordmark` to translate `FINAL FANTASY VIII` on the title screen. Supply one line using glyphs with outlines in Times New Roman. The renderer keeps the original metallic vector treatment, fits wider text to the existing space and uses the English wordmark for blank text, strings longer than 64 UTF-16 code units, or text with missing glyphs.

System messages can be templates containing numbered arguments such as `{0}` and `{1}`. Preserve these arguments while translating the surrounding prose; their values include current player names, quantities and paths. The English base reference retains the authored English template. Selecting another reference does not claim an authored translation exists for it.

## Field controls and choices

Preserve required dynamic placeholders such as `{Var 2}`, `{Char Squall}` or key/name tokens, including their occurrence counts. Their contents come from runtime owners, including player-assigned names. Unicode translation text does not need to fit the original game's byte encoding.

Use `{Empty}` as the entire target to suppress a string in the game. A blank target inherits the next available translation or the original text. Explicit suppression must still satisfy the original string's argument and choice contracts.

Use `{Invalid}` for internal strings that were never intended for players. The game displays a visible diagnostic in place of the marker. Keep any required native control tokens around the marker when the source consumer expects them.

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

dotnet run --project tools/Memoria.FF8.Localization -c Release -- export --game-dir "$env:MEMORIA_FF8_GAME_DIR" --script-base en --source-reference jp --references jp,en --target "en (Alternate translation)" --name "Alternate translation" --format json --layout split --output "$env:MEMORIA_FF8_LOCALIZATION_OUTPUT"

dotnet run --project tools/Memoria.FF8.Localization -c Release -- validate --input "$env:MEMORIA_FF8_LOCALIZATION_INPUT"

dotnet run --project tools/Memoria.FF8.Localization -c Release -- convert --input "$env:MEMORIA_FF8_LOCALIZATION_INPUT" --output "$env:MEMORIA_FF8_LOCALIZATION_OUTPUT" --format xlsx --layout single

dotnet run --project tools/Memoria.FF8.Localization -c Release -- patch --input "$env:MEMORIA_FF8_LOCALIZATION_INPUT" --output "$env:MEMORIA_FF8_LOCALIZATION_OUTPUT" --name "Translation corrections"
```

Formats are `json` and `xlsx`; layouts are `single` and `split`. Export accepts ordered comma-separated `--references jp,en,fr`; `--script-base` selects the base game language and `--source-reference jp` independently designates the translation source. The legacy `--source jp` remains an alias; contradictory source options are rejected. Missing source/base reference columns are appended without moving selected columns. Without an explicit source option, the script base is the CLI's Source Reference. Older projects recover the source role from primary or legacy metadata without reordering references; conversion preserves their row and project identities. Probe actual availability first: requesting a dataset does not install it. Validate and convert are offline operations. Ctrl+C cancels export/conversion, and completed new packages remain disabled. Existing output directories are rejected.

For distribution from a standalone project with a declared original edition, export the player files into a new directory.

```powershell
dotnet run --project tools/Memoria.FF8.Localization -c Release -- export-player --input "$env:MEMORIA_FF8_LOCALIZATION_INPUT" --output "$env:MEMORIA_FF8_LOCALIZATION_OUTPUT"
```

The player export writes UTF-8 JSON split by category and logical file. It retains Targets, edition overrides, native identities, binding fingerprints and authored choice maps. The engine reconstructs native bindings from the installed resources. A player row can omit both `Source Text SHA256` and `sourceContext` to bind by its canonical key in the selected compatible edition without source text or resource fingerprint checks. Keep any authored choice map and edition overrides. Add a package descriptor, attribution and any separately cleared distribution assets before publishing. Original and reference text, artwork, movie files and local settings stay in the authoring project. Make translation edits in that project and regenerate the player files.

### Add reading columns and review a Russian translation

Run the localization CLI from the product directory. Set the variables below to the existing mod root, the two installed game roots and a directory for private reports outside the mod.

```powershell
$cli = "tools/Memoria.FF8.Localization"
dotnet run --project $cli -- add-references --input $mod --game-dir $classic --language en --report "$reports/english.json"
dotnet run --project $cli -- add-references --input $mod --game-dir $remastered --language jp --report "$reports/japanese.json"
dotnet run --project $cli -- lint --input $mod --reference en --report "$reports/before.json"
dotnet run --project $cli -- fix --input $mod --reference en --report "$reports/repairs.json"
dotnet run --project $cli -- lint --input $mod --reference en --report "$reports/remaining.json"
dotnet run --project $cli -- validate --input $mod
```

`add-references` updates a schema 2 JSON package in place. It writes `Reference [en]` and `Reference [jp]` beside the translation with their native keys and records the source edition for each column. For field variants with a verified shared message and different window settings, a reading may identify the native base message. The coverage report lists missing, undecodable and ambiguous readings by key. Engine-authored System strings have English originals. At runtime the package supplies its Target and applicable edition override.

`lint` reads the translation and writes its findings to the requested report. `fix` applies the supported Russian editorial rules to the package. Each report identifies the file, key, target column and rule. Exit code 0 means the review has no outstanding findings, 1 means findings remain and 2 indicates an input or execution error. Use the final lint report for editorial work. A repeat fix on the same inputs should change zero targets.

The reviewer checks speaker labels, dialogue quotes, line indentation, punctuation spacing and control tags. Its reference comparison accounts for field windows, pages and choices. In a speaker label, a supported name tag displays the player's chosen name or its localized default. Within the translated speech the editor controls spelling and grammatical case. Decisions about meaning, uncertain speaker identity, conflicting translations and ambiguous control placement require a human editor.

For named NPCs, pass the same optional `--speaker-glossary <file.json>` to `lint` and `fix`. Use a JSON object mapping each reviewed English speaker label to its exact Russian spelling, for example `{"Captain Grey":"Капитан Грей"}`. To recognize a Russian spelling variant of a character, map that spelling to its canonical name tag. The original speaker must identify the same character. The tool applies the mapping to a matching speaker label and reports conflicting evidence in the field. The same-field lookup can also recover an NPC spelling from independently translated messages in that field.

Keep enriched copies local to the translator. The current loader accepts reading columns in a player package. Older loaders can reject that format. Use `export-player` with a fresh destination to generate reference-free player payloads with the corrected Targets and their binding metadata. A loader that predates support for character-name tags in originally literal speaker labels can also require an engine update.

The export summary reports **Resource/coverage diagnostics** from the manifest's `sourceGaps` array across selected datasets. These can describe unsupported sections, unavailable banks, failed decoders or missing correspondence; they are not a missing-translation count. Review their category/resource/reason entries when a reference is blank.

Some original European field resources contain untranslated archival Japanese bytes. Japanese character escapes in these resources are reported as unsupported source encoding; the original key and source identity remain, with an empty reading and a decode diagnostic. The exporter does not infer a Japanese codepage from the resource suffix or substitute text from another language. Archival Japanese codepages can also differ from the supported Japanese release table, and strings without distinctive escapes can remain ambiguous. A successful byte decode alone does not establish that archival text is linguistically correct.

Validation checks package/schema, identities, role columns, workbook structure and retained data. Activation additionally checks current mapping/resource/script compatibility and field presentation bindings. Successful conversion does not prove that an unsupported display consumer or gameplay language has become playable. See [current coverage](../players/localization.md#coverage-and-current-limits) and [the field audit command](../developers/localization-audit.md) for the separate evidence workflow.
