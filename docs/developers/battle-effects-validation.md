# Battle-effects validation

This tool supports preparation plus a bounded live pilot for existing native effects 1, 2 and 3. It does not certify a complete effect campaign or discover its inventory automatically. Normal source projects contain the reusable harness; no copied engine source or overlay build is needed. Original resources, authored campaign inputs and generated evidence remain external.

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

The host supplies a candidate; the supervisor alone finalizes results. It verifies the fixed telemetry-report reference, SHA256, battle identity and sealed action/effect before independently validating the raw report. Telemetry verdicts are separate from campaign acceptance (`publication_accepted=false`). Coordination APIs support transactional receipt commits and idempotent immutable receipt projections; independent reviewer acceptance is a later contract.

## Direct graphical host

`--battle-test` reuses installed cold-process memory, hardcoded/resource-derived New Game owners, shared encounter construction and lazy effect resources before field allocation. Diagnostic clocks, field-derived inputs and teardown ownership are explicit; no fake suspended field is created. Missing owners fail. Initialization receipts are preparation evidence, not native-parity certification.

The snake_case recipe requires `encounter` (formation WORD), `seed` (BYTE), `flags` (WORD), `field_battle_mode` (WORD), `elapsed_counter` (DWORD). Optional `battle_music`, `primary_music_channel`, `music_field_word` supply music inputs through the shared native contract. Resolve values from evidence; zero cannot replace unknown state. Optional `driver` requires `input` (ordinary BattleInput, snake_case fields/string enums) and `expected_effect` (native effect ID). Bounds are `baseline_seconds`, `readiness_timeout_seconds`, `action_timeout_seconds`, `overall_timeout_seconds`, `tail_frames` (at least two). Replace finite defaults with known case-specific budgets. Unknown recipe/driver fields are rejected.

The driver supports ordinary Action/Attack; timed limits, arbitrary borrowed capabilities and between-command repairs fail explicitly. It waits for normal entry/readiness, submits semantic commands, checks requested/executed binding, observes conservative drain plus presented tails, then repeats in the same battle without restore/reset. Wrong fallback, missing dispatch, replaced/rewound owners, terminal state and missing prerequisites block completion. Legitimate persistent owners may need future typed drain contracts. Natural terminal-path certification remains separately required.

An optional `fixture` declares `guardian_forces` (`id`, explicit `acquire`), `characters` (saved `row`, `character_id`, `stocks` with `action` and `count`, assigned `guardian_forces`, optional `magic_command` with `slot` and kernel `ability`, optional positive `hit_points`) and `hold_enemy_commands`. Initial stock preparation accepts only actions 21, 1 and 7, with 2..100 units. Preparation copies the actual New Game records and invokes existing acquisition/junction/command/HP APIs before encounter allocation. Immutable before/after receipts identify every change; no preparation runs between executions. Holding enemy commands suppresses autonomous full-gauge selection, while initialization, reactions, counters, allocations and deaths continue normally.

An optional `target` declares `actor_id`, `stage`, `model`, `scene_slot`, `physical_index`, `flags_mask`, `flags_value`, `opening_action` and `opening_effect`. The driver waits for the original opening dispatch, normal completion and full drain, then verifies actual live identity, initialized statistics, active/visible/targetable flags and original initialization-script hash. This certifies the declared direct participant contract; full story progression requires separate provenance. Never infer a phase from a model screenshot. Original resource-only companions retain their own information/AI and the existing native shared-model contract.

For standalone preparation diagnostics:

```powershell
& $gameExecutable --game-dir $env:MEMORIA_FF8_GAME_DIR --battle-test $bootstrapRecipe --settings-dir $isolatedSettings --control-dir $isolatedControl --save-state $disposableSave --resolution 1920x1080
```

Saved-state startup, software rendering, accelerated stepping and incompatible UI startup are rejected. The active driver owns commands; status/quit remain available. Without a driver, ordinary battle-input is available. battle-close releases isolation without persistent rewards. Exploration-save serialization is rejected. Global user settings are not selected.

Diagnostic hosts also use an isolated player-save directory and ignore physical keyboard/controller/battle-pointer input; commands come through the driver or control queue. The window keeps the requested dimensions instead of automatically maximizing. This prevents unrelated hotkeys and save menus from changing the measured scene.

## Telemetry and remaining boundary

Bounded collectors retain events/raw frame intervals without snapshots. The preceding presentation boundary includes first-use stalls. Each execution separately rejects intervals strictly above `1000 / 30` ms. Median FPS is calculated directly (average two central samples when even); percentiles use nearest rank. Lost samples cannot pass. Metrics describe successful host presentation, not physical scan-out.

Copy/fill/GPU/diagnostic categories use checked 64-bit payload totals and numeric sites. Execution totals include cleanup; never add child phase totals to an inclusive parent. At least 1 GiB copied requires review. Scoped hooks observe managed arena/writer/known-range/staging/resource/first-three payload copies, fills, GPU vertex/texture uploads and framebuffer transfers. The manifest lists exact sites and exclusions; effects outside 1..3 have unavailable mandatory payload coverage. CLR/driver/OS internal copying and collection bookkeeping are outside these payload counters. Arena-writer rentals expose actual live count/capacity and lifetime peaks. Hooks are optional during ordinary play.

`telemetry-report.json` retains every successful-present interval from the boundary preceding admission through cleanup/tails, dispatches, live semantic events and resolved-versus-applied HP contacts, compact HP/status/stock observations, allocation/GC changes, pool observations and actual lifecycle/renderer identities. Collection is bounded and loss blocks acceptance. No full snapshot is serialized per frame. Scored drivers reject screenshot capture; perform captures in a separate evidence replay. Audio uses the normal entry receipt/dispatch path even with diagnostic control transport; `--mute-audio` remains explicit and cannot certify the audio route.

Validate retained evidence independently:

```powershell
dotnet $tool validate-live --input $telemetryReport
dotnet $tool validate-live --input $telemetryReport --case $sealedCaseRecipe
```

The validator recomputes raw intervals from ticks/frequency, checks all presented indexes, both dispatches and costs, HP reconciliation, continuity, cleanup, renderer participation, coverage and reference environment. A structurally complete run below the 30 FPS threshold is `FAIL`; missing or contradictory evidence is `ERROR`. A telemetry `PASS` still has `campaign_acceptance=false` until fixture provenance and independent review are supplied.

Schemas cover implemented records. The validator supports their checked-in vocabulary and rejects unknown keywords/types. Breaking changes need a new version and separately named migration output; immutable records never change. Full result/review/fix contracts are prerequisites for acceptance. Byte counts remain integers, `_ms` means milliseconds, timestamps are UTC.

Exit codes: 0 success, 1 input/I/O/coordination error, 2 planning blockers or a live performance failure, 3 no claim, 4 unavailable/incomplete execution evidence. General `worker`, `run`, `replay`, `retry`, `validate-results`, `compare` and accepted-coverage `report` remain unavailable; use `supervise` and `validate-live` for the bounded pilot. `capabilities` lists these boundaries. Preparation never displays a green campaign.
