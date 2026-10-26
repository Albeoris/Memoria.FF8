# Localization compatibility audit

The field audit is a read-only preparation tool. It does not create or load localization mods, approve semantic mappings, or establish playable support for a language. Resource-language availability and gameplay support are separate capabilities.

Run from the product root with a locally selected Remastered installation and a new report directory **outside the source checkout**:

```powershell
dotnet run --project tools/Memoria.FF8.Localization -c Release -- audit --game-dir $env:MEMORIA_FF8_REMASTERED_DIR --output $env:MEMORIA_FF8_LOCALIZATION_AUDIT_OUTPUT
```

Both arguments are required. Existing reports are never overwritten. Ctrl+C cancels; an `INCOMPLETE` marker remains if execution does not finish. Exit status 0 means complete coverage, 1 means a completed report with explicit coverage gaps, and 2 means the operation failed or was cancelled. Completion of the tool does not mean that the corpus is compatible.

The tool probes the six supported resource suffixes, including `jp` for canonical language `ja`, using localized-archive precedence and shared fallback. It inventories each nested field archive once and uses sequential reads and field-scoped encodings. No game archives are modified. A resource member is limited to 128 MiB. Unknown language suffixes, missing counterparts and ambiguous identities are reported rather than silently accepted.

Outputs include:

- `mapping.csv`: programmatically generated keys for every known ordinal of every field, with English in the first column, per-language proof status and graph-derived Field Name. Empty/unknown fields have explicit blank-key placeholder records.
- `report.json` and `summary.md`: coverage, per-language counts, corpus/provider fingerprints, resource metadata and limitations.
- `banks/<field>/<language>/bank.json`: original ordinals, decoded text/control tokens, byte ranges, decoder/table identity and decoding errors.
- `banks/<field>/<language>/inventory.json`: indexed executable operations, message/window/choice/result contexts, referenced/unreferenced/empty entries and unresolved operands.
- `pairs/<field>.<language>.json`: first raw/structured divergence, preceding context, all subsequent text contexts, mechanically proved prefix identities, conservative candidates and individual review cases.
- Original JSM/MSD bytes alongside indexed banks, for private diagnostics.

The tool also builds a directed location graph from field-transition instructions and `SETPLACE`, using the native `maplist` field IDs. A reverse multi-source breadth-first search finds the nearest name-setting node reachable by outgoing transitions. Equal-distance alternatives retain all candidate place IDs; no manual location mapping is used. This is a graph of potential transitions, without story-condition execution.

`graphs/<language>.json` stores transitions, setters, distances, names, ambiguity and uncertainty. A sibling `localization-graph-cache/` reuses graphs only when the graph rule, implementation assembly, field IDs, script bytes and decoded name bank still match. Both the reports and this cache belong outside Git checkouts and game installations; the tool rejects those destinations before writing.

`banks/<field>/<language>/field-name.json` supplies the **Field Name** authoring property for Fields export. It is the single nearest candidate name when unique, or null with explicit candidates when ambiguous/unavailable. Resolution status and incomplete/dynamic evidence remain alongside it. This annotation is not part of the canonical key and cannot affect runtime identity or choice results. The later JSON/XLSX project exporter must preserve it as a property/column; that exporter is not implemented by the audit tool.

**Reports contain proprietary game text and scripts. Keep every output private and outside the public source tree.** Do not distribute generated reports with the product.

Each source language has its own unique keys: `$en_sample_1_0000`, `$de_sample_1_0000`, `$jp_sample_1_0000`. The numeric suffix is the zero-based original bank ordinal, padded to at least four digits without truncation. Internal canonical language `ja` uses `jp` in exported keys. Empty and unused slots retain their keys; language keys are never merged into an English key.

The complete CSV starts with `en,fr,de,es,it,jp`, followed by field, ordinal, Field Name, five correspondence-status columns and all-language unused status. Its initial rows align numeric positions programmatically. `Proven` indicates mechanical correspondence; `RequiresReview`, technical uncertainty, absent positions and unknown banks stay explicit. Positional placement alone does not assert semantic equivalence. Extra localized ordinals have a blank English key. Subsequent corrections can be made in the CSV; generated reports never overwrite existing output directories. Universally unused rows remain in this complete key inventory even when excluded from review. CSV contents are private audit output, not redistributable product data.

English Remastered bank ordinals are the comparison baseline. Text correspondence uses a message-identity projection: MES, MESW, AMES, AMESW, RAMESW, ASK and AASK retain their opcode kind and MessageId, ignoring their window, coordinate and choice presentation arguments. Complete contiguous pure argument expressions may collapse to one message token; unknown MessageId, incomplete expressions, potentially faulting calculations and interior control-flow entries cannot establish that shortcut. Branch targets and script pointers are compared at projected boundaries, preserving owners, categories, group order and pointer flags. All other instructions remain significant. The original executable inventory, raw divergence and runtime readiness classification remain separate diagnostics.

Equal validated field projections preserve all shared bank positions, including empty/unused slots. A nonempty bank-only suffix has no counterpart. For divergent fields, correspondence is also proved independently within each native object/script identity. Matching requires the object index and label, script ID, native category and category position, physical pointer-table index and pointer flags. Branch destinations compare script-local projected boundaries; cross-script outgoing branches and external entries into a script interior prevent this independent shortcut. An unrelated changed script or later added object does not invalidate a separately proved script.

A genuine projected difference stops correspondence within that script: later equal-looking messages in it remain candidates, without suffix resynchronization. Each language has its own original-operation boundary, since argument expressions can have different lengths. An ordinal leaves the divergent-field queue only when it has proved uses on both sides and no unproved reuse on either side. Unknown message IDs prevent this shortcut. The field-wide prefix remains additional evidence; native validation and runtime support do not follow from text correspondence. `MessageIdentityEvidence` records the original field-wide comparison; `ScriptIdentityEvidence` records each independent script's proof, boundaries and uncertainty.

After correspondence, the audit excludes candidate ordinals proved unreferenced in **all six** supported languages, including English. This uses the union of statically known references across validated banks and scripts; absence of an ordinal from a valid bank counts as no use in that language. Missing resources, invalid banks/scripts or unknown MessageId prevent universal exclusion for the field. Being unused only in the current pair is insufficient. `UniversallyUnusedEvidence` records the rule, required languages, excluded ordinal set and uncertainty. Original banks and canonical keys remain intact; this only removes review candidates and empty alignment cases. Dynamic presentation diagnostics remain available independently of row pruning.

Static operand inference maintains an abstract stack suffix within each script. It preserves independent constants beside unknown operands and folds exact CAL expressions through the native scalar contract. Unknown memory/results remain unknown. Script changes, branch targets and commands/stores reset the suffix; no control-flow merge or observed gameplay value is assumed. A dynamic coordinate or choice operand does not erase a known message ID. Only unresolved message IDs expand the reference bound to the whole bank. Context diagnostics retain ownership and explicitly unproved control-flow/window-state observations. Dynamic candidate ranges use a first ordinal and count, referencing the indexed bank instead of duplicating its strings. Japanese escapes are checked before decoding to catch missing tables, invalid indices and truncation; valid square characters are not mistaken for substitution. Legitimate character aliases do not require byte-identical re-encoding.

Ordinal correspondence and runtime presentation readiness are separate evidence. Equal validated message-identity projections with valid decoded banks and statically bounded references prove shared ordinals even when a presentation operand remains dynamic. Such pairs can remain `Incomplete` for runtime readiness and retain their original context evidence. For divergent projections, only proven prefix-exclusive references can leave the candidate set; every later text-bearing suffix and unused row remains conservative. Invalid resources, undecoded banks and unknown text IDs do not obtain this shortcut.

To refine an existing original audit without reopening game archives or duplicating its captured assets:

```powershell
dotnet run --project tools/Memoria.FF8.Localization -c Release -- refine --input $env:MEMORIA_FF8_LOCALIZATION_AUDIT_INPUT --output $env:MEMORIA_FF8_LOCALIZATION_AUDIT_OUTPUT
```

The input must be a finished schema-1 Remastered audit, including its original resources and manifest. Incomplete coverage is allowed; an unfinished-run marker is rejected. The tool verifies the corpus fingerprint, binds each bank's metadata to the physical manifest, checks original resource lengths/SHA256 and decodes them again. It prepares each field/language inventory once and reuses it across comparisons. The new output references original evidence, field names and graphs instead of copying them. Keep the input report available.

Optional `--compare <prior-refined-directory>` compares counts with a finished schema-2 refinement from the same original report and corpus. It verifies source hashes and the exact field/language pair set. The original schema-1 audit still supplies physical resources and determines membership of the originally incomplete subset. Without this option, counts compare with the original audit.

The refined `summary.md`/`report.json` compare counts across the full corpus and the originally incomplete subset. `grouped-review.json` separates shared technical repairs, blocked alignments and actionable alignment/context groups, with explicit language-specific members. Grouping identical structural evidence does not merge canonical rows or decide that translated prose means the same thing. A more precise context inventory can contain additional cases even while fewer rows need alignment review.

Candidate totals distinguish technically unresolved rows, referenced script-suffix rows, remaining unused bank rows and other candidates. These categories are exclusive; technical uncertainty takes priority. Reports separately record candidate counts before the all-language unused filter, removed counts and remaining counts. Review cases are mechanically identified unmatched runs and choice/context groups. Message groups use only owner, opcode kind and MessageId; presentation variants remain indexed evidence without becoming separate text-conflict cases. Their count is a work queue, not a finding about semantic equivalence. Effective stateful window layout, dynamic expressions, control-flow analysis, cross-edition mappings, non-field text families and translation runtime behavior require further work. Incomplete counts and unsupported resources must be reported before approving semantic review.

Focused portable checks:

```powershell
dotnet run --project tests/Memoria.FF8.Assets.Tests -c Release -- --filter-class '*FieldCompatibilityAuditTests' --filter-class '*FieldAuditDecoderTests'
dotnet run --project tests/Memoria.FF8.Assets.Tests -c Release -- --filter-class '*FieldLocationGraphTests'
dotnet run --project tests/Memoria.FF8.Assets.Tests -c Release -- --filter-class '*FieldCompatibilityOperandStateTests' --filter-class '*FieldCompatibilityReductionTests'
dotnet run --project tests/Memoria.FF8.Assets.Tests -c Release -- --filter-class '*FieldCompatibilityProjectionTests'
dotnet run --project tests/Memoria.FF8.Assets.Tests -c Release -- --filter-class '*FieldScriptCompatibilityTests' --filter-class '*FieldUniversallyUnusedTests'
dotnet run --project tests/Memoria.FF8.Assets.Tests -c Release -- --filter-class '*FieldLocalizationMappingTests'
```

Fixtures are synthetic; these tests do not require an installed game and do not run battle-effect tests.
