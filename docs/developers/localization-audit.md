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

Static operand inference currently recognizes contiguous literal pushes with no intervening branch entry. Other expressions are retained as unresolved, with a whole-bank bound when known. Context diagnostics retain ownership and explicitly unproved control-flow/window-state observations. Dynamic candidate ranges use a first ordinal and count, referencing the indexed bank instead of duplicating its strings. Japanese escapes are checked before decoding to catch missing tables, invalid indices and truncation; valid square characters are not mistaken for substitution. Legitimate character aliases do not require byte-identical re-encoding.

Review cases are mechanically identified unmatched runs, choice/context groups and repeated-message presentation conflicts. Repeated identical operand sets share a case. Their count is a work queue, not a finding about semantic equivalence. Effective stateful window layout, dynamic expressions, control-flow analysis, cross-edition mappings, non-field text families and translation runtime behavior require further work. Incomplete counts and unsupported resources must be reported before approving semantic review.

Focused portable checks:

```powershell
dotnet run --project tests/Memoria.FF8.Assets.Tests -c Release -- --filter-class '*FieldCompatibilityAuditTests' --filter-class '*FieldAuditDecoderTests'
dotnet run --project tests/Memoria.FF8.Assets.Tests -c Release -- --filter-class '*FieldLocationGraphTests'
```

Fixtures are synthetic; these tests do not require an installed game and do not run battle-effect tests.
