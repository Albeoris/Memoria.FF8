# Battle-effects validation preparation

This tool prepares a future two-execution smoke/performance campaign. It does not discover installed coverage, analyze effects or certify existing effects. Normal source projects contain the reusable harness; no copied engine source or overlay build is needed. Original resources, authored campaign inputs and generated evidence remain external.

## Build and verify

From the product root, with Windows, PowerShell 7 and .NET 10:

```powershell
dotnet build tests/Memoria.FF8.BattleEffects.Tests -c Release --artifacts-path artifacts/validation
dotnet ./artifacts/validation/bin/Memoria.FF8.BattleEffects.Tests/release/Memoria.FF8.BattleEffects.Tests.dll
$tool = (Resolve-Path ./artifacts/validation/bin/Memoria.FF8.BattleEffects/release/Memoria.FF8.BattleEffects.dll).Path
dotnet $tool capabilities
```

The tests use synthetic records and owned disposable Windows child processes, never installed game resources. Build the normal App project using [building and testing](building-and-testing.md). Focused App tests are `DiagnosticBattlePreparationTests` and `DiagnosticBattleDriverTests`; do not select unrelated battle-effect tests for harness changes. Build once and invoke the resulting executable rather than rebuilding for each case.

## JSON storage and ownership

Select an absolute local `$store` outside repositories/worktrees. Every campaign agent/worktree must use this exact location. All permanent records/artifacts use JSON envelopes in `records/<logical-key>.record.json`: schema version, SHA256 and exactly one UTF-8 text or base64 binary payload. This preserves original bytes, including integer precision, encoding and whitespace. `show` displays decoded JSON and `export` writes exact original bytes. Reading verifies SHA256; publication uses flush plus atomic no-replace rename. Failed attempts are never overwritten.

`coordination.json` contains claims, generations, pass binding, process slots, allocations and committed receipt metadata. Every read/update holds an OS-exclusive `FileStream` lock on `coordination.lock`; updates serialize a complete new state and atomically replace the old file only after success. A failed transaction leaves the previous state intact. This is not an existence-check lock. Network paths are unsupported; lock acquisition has a finite bound. No database dependency is required by the engine or tool.

Temporary settings/control/output files serve existing process transport. On confirmed exit, the CLI imports their bytes into immutable records and removes temporary files. Interrupted import preserves recovery material. Use one authoritative store per measurement machine: different stores do not coordinate. Do not run competing campaigns, builds, profilers or captures while the slot is held.

## Import and seal declared inputs

Supply real planner-authored `inventory`, `resources`, `profile` and `cases` inputs matching [schemas](../../tools/Memoria.FF8.BattleEffects/schemas). No executable sample plans or guessed game identities are provided. `inventory --input` validates authored input without inspecting the installation. Resource manifests identify selected files by relative path, byte size and SHA256; `resource_hash` is `EvidenceStore.HashRecord(resources)` using normalized snake_case serialization. Preserve enabled-mod order in both manifests/profiles.

With variables pointing to existing inputs and an immutable Release build:

```powershell
dotnet $tool inventory --input $inventory
dotnet $tool plan --store $store --campaign $campaignId --inventory $inventory --resources $resources --profile $profile --cases $cases --build $build --source-commit $sourceCommit
dotnet $tool validate-plan --store $store
dotnet $tool seal --store $store
dotnet $tool pass-create --store $store --pass baseline --kind baseline --build $build --source-commit $sourceCommit
dotnet $tool status --store $store --pass baseline
```

Each corrected revision uses a new store directory with `--revision` and `--parent-campaign`. Dirty source requires `--patch-hash` for the exact patch; the planner authenticates provenance. Validation checks schemas, identities, coverage references, mappings, explicit blockers, declared Squall owner, resource/mod compatibility and finite budgets. It checks declared evidence, **not independent native target legality, final-Ultimecia phase or unknown owners**. Unresolved entries block sealing; exclusions require evidence/reasons. Seal freezes inputs, separately from readiness and reviewer acceptance.

Changed builds need new passes. `pass-create --kind final` always includes every sealed case, independently of baseline claims. Nonfinal passes may select `--cases case-a,case-b`. Pass reads recheck build-manifest/content hashes and selection completeness; supervision rehashes the actual build directory before launch.

## Records and claims

```powershell
dotnet $tool records --store $store --prefix effects/
dotnet $tool show --store $store --key campaign.json
dotnet $tool export --store $store --key campaign.json --output $exportFile
$claim = dotnet $tool claim-next --store $store --pass baseline --agent worker-01 | ConvertFrom-Json
dotnet $tool heartbeat --store $store --claim $claim.token
dotnet $tool release --store $store --claim $claim.token
dotnet $tool recover-stale --store $store --pass baseline
```

`claim-specific` adds `--effect <effect-key>`. Tokens locate current leases. An effect group is claimed per explicit pass. Default lease expiry is 60 seconds; normal heartbeat is every five seconds. Tokens/generations fence heartbeat/completion/release. Old workers cannot release newer slots. Recovery checks stored PID **and start time**, not expiry alone; orphan evidence remains visible. Scheduling completion, execution verdict and review acceptance are separate.

## Supervise a preparation diagnostic

The [launch input](../../tools/Memoria.FF8.BattleEffects/schemas/supervisor-launch.v1.schema.json) declares the pinned executable, arguments, working directory, no-progress bound and optional child environment. Arguments select an authored `--battle-test` recipe and installed resources. Do not supply settings/control/save switches: unique isolated locations are injected. Phase budgets come from the sealed case. Launch/bootstrap inputs and started metadata are durably recorded before execution.

```powershell
$claim = dotnet $tool claim-specific --store $store --pass baseline --effect $effectKey --agent worker-01 | ConvertFrom-Json
dotnet $tool supervise --store $store --claim $claim.token --case $caseId --request $launchInput
```

The supervisor creates a suspended child, attaches a kill-on-close Windows job, acquires the measurement slot, then resumes it. Independent heartbeats maintain leases. Initialization/action/cleanup/teardown/process and no-progress bounds are finite. Crash/cancellation/missing candidate/skipped stages produce immutable ERROR evidence. Only the owned job is terminated. Slot release requires confirmed exit; otherwise ownership remains for recovery. Finished preparation diagnostics return their claim to pending without an accepted campaign receipt.

The host supplies a candidate; the supervisor alone finalizes results. Current candidates become `ERROR/TELEMETRY_UNAVAILABLE` until complete live instrumentation and case assertions exist. Coordination APIs support transactional receipt commits and idempotent immutable receipt projections; independent reviewer acceptance is a later contract.

## Direct graphical host

`--battle-test` reuses installed cold-process memory, hardcoded/resource-derived New Game owners, shared encounter construction and lazy effect resources before field allocation. Diagnostic clocks, field-derived inputs and teardown ownership are explicit; no fake suspended field is created. Missing owners fail. Initialization receipts are preparation evidence, not native-parity certification.

The snake_case recipe requires `encounter` (formation WORD), `seed` (BYTE), `flags` (WORD), `field_battle_mode` (WORD), `elapsed_counter` (DWORD). Optional `battle_music`, `primary_music_channel`, `music_field_word` supply music inputs through the shared native contract. Resolve values from evidence; zero cannot replace unknown state. Optional `driver` requires `input` (ordinary BattleInput, snake_case fields/string enums) and `expected_effect` (native effect ID). Bounds are `baseline_seconds`, `readiness_timeout_seconds`, `action_timeout_seconds`, `overall_timeout_seconds`, `tail_frames` (at least two). Replace finite defaults with known case-specific budgets. Unknown recipe/driver fields are rejected.

The first driver supports ordinary Action/Attack; timed limits, borrowed capabilities and repairs fail explicitly. It waits for normal entry/readiness, submits semantic commands, checks requested/executed binding, observes conservative drain plus presented tails, then repeats in the same battle without restore/reset. Wrong fallback, missing dispatch, replaced/rewound owners, terminal state and missing prerequisites block completion. Legitimate persistent owners may need future typed drain contracts. AI selection/holding, progression/capability/repair adapters, phase verification and natural terminal-path certification remain unavailable. Receipts never produce PASS.

For standalone preparation diagnostics:

```powershell
& $gameExecutable --game-dir $env:MEMORIA_FF8_GAME_DIR --battle-test $bootstrapRecipe --settings-dir $isolatedSettings --control-dir $isolatedControl --save-state $disposableSave --resolution 1920x1080
```

Saved-state startup, software rendering, accelerated stepping and incompatible UI startup are rejected. The active driver owns commands; status/quit remain available. Without a driver, ordinary battle-input is available. battle-close releases isolation without persistent rewards. Exploration-save serialization is rejected. Global user settings are not selected.

## Telemetry and remaining boundary

Bounded collectors retain events/raw frame intervals without snapshots. The preceding presentation boundary includes first-use stalls. Each execution separately rejects intervals strictly above `1000 / 30` ms. Median FPS is calculated directly (average two central samples when even); percentiles use nearest rank. Lost samples cannot pass. Metrics describe successful host presentation, not physical scan-out.

Copy/fill/GPU/diagnostic categories use checked 64-bit payload totals and numeric sites. Execution totals include cleanup; never add child phase totals to an inclusive parent. At least 1 GiB copied requires review. The manifest explicitly reports missing runtime/renderer hooks with null totals, not measured zero. Live event/traffic/GC/process sampling, renderer coupling, allocation attribution and pilot calibration remain to be connected. No effect was measured during preparation.

Schemas cover implemented records. The validator supports their checked-in vocabulary and rejects unknown keywords/types. Breaking changes need a new version and separately named migration output; immutable records never change. Full result/review/fix contracts are prerequisites for acceptance. Byte counts remain integers, `_ms` means milliseconds, timestamps are UTC.

Exit codes: 0 success, 1 input/I/O/coordination error, 2 planning blockers, 3 no claim, 4 preparation-only/unavailable execution. Scored `worker`, `run`, `replay`, `retry`, `validate-results`, `compare` and accepted-coverage `report` return 4 until native adapters, live telemetry and independent acceptance are connected. `capabilities` lists these boundaries. Preparation never displays a green campaign.
