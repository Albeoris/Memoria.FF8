# Localization compatibility audit

The field audit is a read-only preparation tool. It does not create or load localization mods, approve semantic mappings, or establish playable support for a language. Resource-language availability and gameplay support are separate capabilities.

Run from the product root with a locally selected Remastered installation and a new report directory **outside the source checkout**:

```powershell
dotnet run --project tools/Memoria.FF8.Localization -c Release -- audit --game-dir $env:MEMORIA_FF8_REMASTERED_DIR --output $env:MEMORIA_FF8_LOCALIZATION_AUDIT_OUTPUT
```

Both arguments are required. Existing reports are never overwritten. Ctrl+C cancels; an `INCOMPLETE` marker remains if execution does not finish. Exit status 0 means complete coverage, 1 means a completed report with explicit coverage gaps, and 2 means the operation failed or was cancelled. Completion of the tool does not mean that the corpus is compatible.

The tool probes the six supported resource suffixes, including `jp` for canonical language `ja`, using localized-archive precedence and shared fallback. It inventories each nested field archive once and uses sequential reads and field-scoped encodings. No game archives are modified. A resource member is limited to 128 MiB. Unknown language suffixes, missing counterparts and ambiguous identities are reported rather than silently accepted.

Outputs include:

- `report.json` and `summary.md`: coverage, per-language counts, corpus/provider fingerprints, resource metadata and limitations.
- `banks/<field>/<language>/bank.json`: original ordinals, decoded text/control tokens, byte ranges, decoder/table identity and decoding errors.
- `banks/<field>/<language>/inventory.json`: indexed executable operations, message/window/choice/result contexts, referenced/unreferenced/empty entries and unresolved operands.
- `pairs/<field>.<language>.json`: first raw/structured divergence, preceding context, all subsequent text contexts, mechanically proved prefix identities, conservative candidates and individual review cases.
- Original JSM/MSD bytes alongside indexed banks, for private diagnostics.

The tool also builds a directed location graph from field-transition instructions and `SETPLACE`, using the native `maplist` field IDs. A reverse multi-source breadth-first search finds the nearest name-setting node reachable by outgoing transitions. Equal-distance alternatives retain all candidate place IDs; no manual location mapping is used. This is a graph of potential transitions, without story-condition execution.

`graphs/<language>.json` stores transitions, setters, distances, names, ambiguity and uncertainty. A sibling `localization-graph-cache/` reuses graphs only when the graph rule, implementation assembly, field IDs, script bytes and decoded name bank still match. Both the reports and this cache belong outside Git checkouts and game installations; the tool rejects those destinations before writing.

`banks/<field>/<language>/field-name.json` supplies the **Field Name** authoring property for Fields export. It is the single nearest candidate name when unique, or null with explicit candidates when ambiguous/unavailable. Resolution status and incomplete/dynamic evidence remain alongside it. This annotation is not part of the canonical key and cannot affect runtime identity or choice results. The later JSON/XLSX project exporter must preserve it as a property/column; that exporter is not implemented by the audit tool.

**Reports contain proprietary game text and scripts. Keep every output private and outside the public source tree.** Do not distribute generated reports with the product.

English Remastered bank ordinals are the identity baseline. Identical executable streams preserve all shared bank positions, including empty/unused slots. A nonempty bank-only suffix has no counterpart. Any real executable difference prevents automatic suffix resynchronization: later equal-looking messages remain candidates. Unreferenced rows remain conservative candidates; rows used exclusively in a proved prefix can be excluded. Structural-only differences with unproved object/script layout retain the complete bank bound.

Static operand inference maintains an abstract stack suffix within each script. It preserves independent constants beside unknown operands and folds exact CAL expressions through the native scalar contract. Unknown memory/results remain unknown. Script changes, branch targets and commands/stores reset the suffix; no control-flow merge or observed gameplay value is assumed. A dynamic coordinate or choice operand does not erase a known message ID. Only unresolved message IDs expand the reference bound to the whole bank. Context diagnostics retain ownership and explicitly unproved control-flow/window-state observations. Dynamic candidate ranges use a first ordinal and count, referencing the indexed bank instead of duplicating its strings. Japanese escapes are checked before decoding to catch missing tables, invalid indices and truncation; valid square characters are not mistaken for substitution. Legitimate character aliases do not require byte-identical re-encoding.

Ordinal correspondence and runtime presentation readiness are separate evidence. Fully identical validated executable streams with valid decoded banks and statically bounded references prove shared ordinals even when a presentation operand remains dynamic. Such pairs remain `Incomplete` for runtime readiness and retain their context diagnostics. For divergent scripts, only proven prefix-exclusive references can leave the candidate set; every later text-bearing suffix and unused row remains conservative. Invalid resources, undecoded banks and unknown text IDs do not obtain this shortcut.

To refine an existing original audit without reopening game archives or duplicating its captured assets:

```powershell
dotnet run --project tools/Memoria.FF8.Localization -c Release -- refine --input $env:MEMORIA_FF8_LOCALIZATION_AUDIT_INPUT --output $env:MEMORIA_FF8_LOCALIZATION_AUDIT_OUTPUT
```

The input must be a finished schema-1 Remastered audit, including its original resources and manifest. Incomplete coverage is allowed; an unfinished-run marker is rejected. The tool verifies the corpus fingerprint, binds each bank's metadata to the physical manifest, checks original resource lengths/SHA256 and decodes them again. It prepares each field/language inventory once and reuses it across comparisons. The new output references original evidence, field names and graphs instead of copying them. Keep the input report available.

The refined `summary.md`/`report.json` compare counts across the full corpus and the originally incomplete subset. `grouped-review.json` separates shared technical repairs, blocked alignments and actionable alignment/context groups, with explicit language-specific members. Grouping identical structural evidence does not merge canonical rows or decide that translated prose means the same thing. A more precise context inventory can contain additional cases even while fewer rows need alignment review.

Review cases are mechanically identified unmatched runs, choice/context groups and repeated-message presentation conflicts. Repeated identical operand sets share a case. Their count is a work queue, not a finding about semantic equivalence. Effective stateful window layout, dynamic expressions, control-flow analysis, cross-edition mappings, non-field text families and translation runtime behavior require further work. Incomplete counts and unsupported resources must be reported before approving semantic review.

Focused portable checks:

```powershell
dotnet run --project tests/Memoria.FF8.Assets.Tests -c Release -- --filter-class '*FieldCompatibilityAuditTests' --filter-class '*FieldAuditDecoderTests'
dotnet run --project tests/Memoria.FF8.Assets.Tests -c Release -- --filter-class '*FieldLocationGraphTests'
dotnet run --project tests/Memoria.FF8.Assets.Tests -c Release -- --filter-class '*FieldCompatibilityOperandStateTests' --filter-class '*FieldCompatibilityReductionTests'
```

Fixtures are synthetic; these tests do not require an installed game and do not run battle-effect tests.
