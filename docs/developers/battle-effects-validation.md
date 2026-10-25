# Battle-effects validation

This external tool discovers installed battle action/effect relationships and runs an explicit scenario manifest through fresh bounded graphical hosts. Its runner accepts the complete selection without an artificial effect-count limit. Discovery records unsupported owners, fixtures and invocation paths as blockers; these produce individual ERROR results and never count as playable coverage. The runner implements no effects and does not certify a complete campaign. Normal source projects contain the reusable harness; no copied engine source or overlay build is needed. Original resources, authored campaign inputs and generated evidence remain external.

## Build and verify

From the product root, with Windows, PowerShell 7 and .NET 10:

```powershell
dotnet build tests/Memoria.FF8.BattleEffects.Tests -c Release --artifacts-path artifacts/validation
dotnet ./artifacts/validation/bin/Memoria.FF8.BattleEffects.Tests/release/Memoria.FF8.BattleEffects.Tests.dll
$tool = (Resolve-Path ./artifacts/validation/bin/Memoria.FF8.BattleEffects/release/Memoria.FF8.BattleEffects.dll).Path
dotnet $tool capabilities
```

The tests use synthetic records and owned disposable Windows child processes, never installed game resources. Build the normal App project using [building and testing](building-and-testing.md). Focused App tests are `DiagnosticBattlePreparationTests` and `DiagnosticBattleDriverTests`; do not select unrelated battle-effect tests for harness changes. Build once and invoke the resulting executable rather than rebuilding for each case.

An optional ahead-of-time build reduces managed startup compilation while preserving a fresh process and the cold first focal invocation:

```powershell
dotnet publish src/Memoria.FF8.App -c Release -r win-x64 --self-contained false -p:PublishReadyToRun=true -o $immutableBuild
```

Keep this published directory unchanged for the entire pass. Record source commit and dirty-patch identity with the authored inputs; the runner records the exact file hashes. Publish before acquiring the measurement slot. Do not add a focal warm-up cast or rebuild between cases.

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

## Installed discovery and unattended scenario execution

Discover the installed action catalog and effect bindings into a fresh external directory:

```powershell
dotnet $tool discover --game-dir $env:MEMORIA_FF8_GAME_DIR --output $discoveryDirectory
```

Optional `--fixture $canonicalTemplatePath` supplies one explicitly selected authored canonical bootstrap template; discovery does not scan directories or infer a fixture from a filename. Supply `--source-commit $sourceCommit` and, for dirty source, `--patch-hash $patchHash` to retain caller-declared source identity alongside the tool binary identity and sealed manifest hashes. Discovery writes the installed inventory, scenario catalog, per-effect scenario records and `cases-manifest.json`. Inspect their evidence and blockers before execution. Discovery does not invent missing native identities or phase initialization contracts, and a candidate recipe alone does not establish the required canonical encounter. Supply the discovered case array as `cases` in the execution manifest alongside the explicit build, game, output and budget configuration.

`implementation-evidence.json` reconciles installed binding entries with the current compiled dispatcher. It retains dispatcher, target-method and assembly hashes plus safely decoded direct equality branches. Per-binding links report `direct_equality_branch_observed` or `unknown`; generic and range dispatch calls remain opaque when no exact entry mapping is proved. `ImplementationSummary` in the installed inventory and `coverage-reconciliation.json` retain reconciliation counts. This is registration evidence, with no claim of playable support, effect completeness or campaign acceptance.

Author an external manifest matching [pilot.v1](../../tools/Memoria.FF8.BattleEffects/schemas/pilot.v1.schema.json), then run:

```powershell
dotnet $tool validate-record --input $pilotManifest --schema pilot
dotnet $tool run --input $pilotManifest
```

The manifest requires `schema_version: 1`, `executable`, `working_directory`, `game_dir`, a fresh `output_root`, `budgets` and a nonempty explicit `cases` array. All filesystem paths are explicit absolute paths. Evidence must be outside repositories and disjoint from the supplied build and game directories. Each case contains a unique safe `id`, `recipe` (an authored bootstrap path, or null for a blocked scenario), optional `effect_id`, and optional `status` and `blockers`. Optional `recipe_sha256` pins the recipe's exact bytes using 64 lowercase hexadecimal characters; generated recipes include this digest. The runner verifies it before staging or launching, and a mismatch produces a case ERROR while later independent cases continue. Status defaults to `RUNNABLE`. `PLANNED_BLOCKED` and `UNRESOLVED` require explicit nonempty blockers and emit a distinct ERROR record without launching a host. Runnable cases cannot retain blockers. Missing or invalid runnable recipe paths produce a case ERROR while independent later cases continue. `pilot --input` remains an alias of `run --input` using the same schema and behavior. No unsupported effect is substituted automatically.

`budgets` requires finite positive `initialization_ms`, `action_one_ms`, `cleanup_rearm_ms`, `action_two_ms`, `teardown_ms`, `total_ms` and `no_progress_ms`, each at most one day. Unknown fields, duplicate JSON properties, missing fields and incorrect types are rejected before a host starts. Runtime checks also reject duplicate case identities, unsafe evidence locations and reused evidence. Every runnable case retains its own finite deadlines, regardless of the manifest length. Optional `build_fingerprint` pins the lowercase SHA256 of the canonical snake_case `BuildFile[]` manifest returned by `EvidenceStore.DescribeFiles(working_directory)`; the runner always retains its actual build manifest and fingerprint.

The runner holds an OS-exclusive measurement file for the batch and starts fresh graphical hosts sequentially. Each host receives isolated settings, control and disposable-save paths, the actual installed game directory and a 1920×1080 GPU window. Each case performs two commands in one uninterrupted battle. Unsupported prerequisites, errors and performance failures are recorded before proceeding to the next case; there are no automatic retries. An unconfirmed owned-process exit prevents further launches and quarantines the measurement slot. Do not run builds, profilers, captures or competing graphical hosts during a batch.

Each case retains its copied bootstrap, `output/telemetry-report.json`, authenticated candidate, supervisor result and immutable `pilot-result.json`. The batch retains `manifest.json`, `build-fingerprint.json` and `batch-summary.json`. Console output contains compact case counts, verdicts and timing; full observations remain in JSON. Original attempts are preserved.

The timing budget uses actual native effect entry through first verified full drain, separately for both invocations. The validator recomputes `effect_duration_ms` from `effect_started_ticks`, `effect_completed_ticks` and the timestamp frequency, and checks those markers against the retained presentation window. `effect_duration_ms` in the pilot summary sums these two durations. `execution_window_ms` instead includes command admission, casting windup, effect work, cleanup and presented tails. They are different measurements.

`total_ms` runs from bootstrap staging through publication of the validated `output/result.json`, including process startup, entry preparation, readiness/baseline, both commands, rearm, tails, teardown and validation. The later timing envelope (`pilot-result.json`), progress callback and batch inventory/summary are supervisor bookkeeping outside this per-case clock; `batch-summary.json.total_ms` includes the per-case envelopes, callbacks and initial inventory, ending before publication of that summary itself. `overhead_ms = total_ms - effect_duration_ms`; it must be **at most 10,000 ms**. A larger overhead fails even if both commands complete. Disjoint `stages` report staging, supervision, validation and validated-result publication costs, whose sum must reconcile to the per-case `total_ms`. An execution can also fail the separate strict 30 FPS gate. Completing both commands for every runnable case does not establish campaign acceptance.

## Direct graphical host

`--battle-test` reuses installed cold-process memory, hardcoded/resource-derived New Game owners, shared encounter construction and lazy effect resources before field allocation. Diagnostic clocks, field-derived inputs and teardown ownership are explicit; no fake suspended field is created. Missing owners fail. Initialization receipts are preparation evidence, not native-parity certification.

The snake_case recipe requires `encounter` (formation WORD), `seed` (BYTE), `flags` (WORD), `field_battle_mode` (WORD), `elapsed_counter` (DWORD). Optional `battle_music`, `primary_music_channel`, `music_field_word` supply music inputs through the shared native contract. Resolve values from evidence; zero cannot replace unknown state. Optional `driver` requires `input` (ordinary BattleInput, snake_case fields/string enums) and `expected_effect` (native effect ID). Bounds are `baseline_seconds`, `readiness_timeout_seconds`, `action_timeout_seconds`, `overall_timeout_seconds`, `tail_frames` (at least two). Replace finite defaults with known case-specific budgets. Unknown recipe/driver fields are rejected.

The driver supports ordinary Action/Attack and the typed enemy, Limit and repair adapters below. It waits for normal entry/readiness, submits semantic commands, checks requested/executed binding, observes conservative drain plus presented tails, then repeats in the same battle without restore/reset. Wrong fallback, missing dispatch, replaced/rewound owners, terminal state and missing prerequisites block completion. Legitimate persistent owners may need future typed drain contracts. Natural terminal-path certification remains separately required.

Optional `expected_native_command` and `expected_native_action` form an explicit native invocation pair, separate from the semantic input's ability/action identity. This permits a normal GF or item input to retain the original packet identity without treating its display/action index as a native effect ID. Optional `supporting_effects` lists distinct known supporting dispatches; they do not replace either of the two required focal executions or authorize fallback resolution.

An `enemy_ability` selector declares the actual `model`, `scene_slot`, `level_band`, `ability_slot`, `command`, `action_id`, `sequence` and native `target_selector`. The fixture must hold unrelated enemy command selection. The selector verifies the live original DAT ability row and uses normal AI preparation, targeting and command queuing. Initialization, reactions and other mandatory callbacks remain active. Resource-table membership alone does not prove a trigger or phase prerequisite.

A `limit_selection` declares `minimum_crisis_level`, `maximum_crisis_level` (`1..4`) and `maximum_selections` (`1..256`). It uses bounded ordinary menu selections and records the crisis results and RNG state; once-per-turn rules, later invocation and finisher selection remain native owners. Gunblade triggers use the ordinary successful-input window. Unsupported timed-input paths still fail explicitly.

An optional `fixture` declares `guardian_forces` (`id`, explicit `acquire`), `characters` (saved `row`, `character_id`, `stocks` with native magic `action` and `count`, assigned `guardian_forces`, optional positive `hit_points`) and `hold_enemy_commands`. Initial magic stocks use installed kernel identities `1..63`, at most32 distinct entries of2..100 units. Each character may declare `commands: [{slot, ability}]` for up to three distinct slots `0..2` and native command abilities `20..38`. Existing junction APIs require the ability to be learned by an assigned GF. Legacy `magic_command: {slot, ability}` still requires the installed Magic command and cannot overlap a declared generic command slot. GF rows may optionally declare `learning_ability` and positive `ability_points`; the existing native eligibility, learning selection and AP-award APIs apply, with no invented learned bits or GF HP. Optional `items: [{id, count}]` uses native item IDs `1..198` and exact quantities `2..100`, acquired through the existing economy owner before encounter allocation. Preparation copies actual New Game records and retains immutable character/GF/economy before/after receipts. No preparation runs between executions. Holding enemy commands suppresses autonomous full-gauge selection, while initialization, reactions, counters, allocations and deaths continue normally.

Optional `fixture.limits` supplies initial saved prerequisites through `learn_blue_magic`, `learn_duel`, `learn_angelo` and `learn_special_magic` masks, plus optional `options978`. Existing initialized owners are required; known learned flags are added to their saved banks and changes are recorded before encounter construction. This preparation does not change character identity, skeleton, native command admission, RNG or finisher selection, and cannot create an absent special-magic/options owner.

Optional character `level` (`1..100`) increases the actual saved party member's level through ordinary native battle-experience awards and the installed ability flags. Preparation uses the existing native level function, preserves equipped level-growth bonuses, rebuilds derived statistics, then applies an explicitly requested `hit_points` value against the new maximum. Lowering a saved level is unsupported. Experience and stat changes remain in the initial character receipt; no raw experience field or encounter level is overwritten. Enemy level-band selection still comes from normal encounter initialization.

`fixture.limits.borrow_blue_magic_for_squall` explicitly adapts the initial diagnostic definition to Quistis's original Blue Magic command owner and its installed crisis/kernel records. Squall retains his actor identity, body and weapon. The capability receipt records this intervention; effect resources and sequence requirements still resolve normally, and incompatible owner/skeleton requirements remain blockers. This does not make Blue Magic naturally available to Squall during ordinary play.

Optional `driver.repairs` contains at most three distinct party-actor entries with `actor_id`, exact positive `hit_points`, optional `revive`, `remove_statuses0` and `remove_statuses1`. Repairs run after the first execution's full normal drain and presented tail, before readiness for the second execution; their wall time remains in total time and overhead. They use current HP/status owners, retain permanent statuses and synchronize the existing party presentation cache. Receipts retain compact before/after HP, statuses and stocks, callback/frame/lifecycle identity, unchanged RNG owners and restore/reset counts. Repairs cannot reverse a terminal/pending outcome, discard tasks, reset stock, restore snapshots, create a new party member or force KO. A legal revival scenario still requires an existing KO ally and another surviving party member; unsupported initial party composition remains an explicit blocker.

An optional `target` declares `actor_id`, `stage`, `model`, `scene_slot`, `physical_index`, `flags_mask`, `flags_value`, `opening_action` and `opening_effect`. The driver waits for the original opening dispatch, normal completion and full drain, then verifies actual live identity, initialized statistics, active/visible/targetable flags and original initialization-script hash. This certifies the declared direct participant contract; full story progression requires separate provenance. Never infer a phase from a model screenshot. Original resource-only companions retain their own information/AI and the existing native shared-model contract.

An authored `prepare_entry_before_presentation: true` executes ordinary native entry callbacks before the real-time baseline. `entry_callback_budget` is finite (`1..18000`, default 1024). Preparation must reach native entry completion, declared target verification, full drain and command readiness within that budget. Actual callbacks advance the existing clock and owners; no save restore or fabricated field is used. A focal-effect dispatch during preparation is rejected. This explicitly accelerated setup is outside the scored presentation window, but its full wall time remains in the per-case total and overhead. Both focal invocations and their presentation remain normally paced. A case may explicitly select a short steady baseline such as `baseline_seconds: 0.1`; this never authorizes a focal warm-up invocation.

For standalone preparation diagnostics:

```powershell
& $gameExecutable --game-dir $env:MEMORIA_FF8_GAME_DIR --battle-test $bootstrapRecipe --settings-dir $isolatedSettings --control-dir $isolatedControl --save-state $disposableSave --resolution 1920x1080
```

Diagnostic recipes default to `atb_mode: "TurnBased"` and `instant_guardian_force_charge: true`. Turn-based scheduling uses the existing logical battle-time advancement instead of waiting for real-time ATB growth. With a driver, automatic readiness waits for its declared actor rather than stopping for another ready participant. This bounded advancement updates the live battle's ordinary gauges and status timers, preserves pending-action and presentation pause gates, and uses no snapshot restoration. Instant GF charge retains the original computed initial ticks, sets only the remaining charge to zero at ordinary command admission, and records `DiagnosticGuardianForceChargeSkipped` with the skipped ticks. The original GF task, admission gates, model callbacks and effect presentation still run normally. Both settings are recorded in the initialization receipt. Select `atb_mode: "Normal"` and `instant_guardian_force_charge: false` for an explicit real-time charge comparison. These diagnostic defaults do not change normal gameplay.

Saved-state startup, software rendering, control stepping and incompatible UI startup are rejected. Entry preparation and the declared scheduling settings above accelerate preparation or command waiting; they do not accelerate focal-effect presentation. The active driver owns commands; status/quit remain available. Without a driver, ordinary battle-input is available. battle-close releases isolation without persistent rewards. Exploration-save serialization is rejected. Global user settings are not selected.

Diagnostic hosts also use an isolated player-save directory and ignore physical keyboard/controller/battle-pointer input; commands come through the driver or control queue. The window keeps the requested dimensions instead of automatically maximizing. This prevents unrelated hotkeys and save menus from changing the measured scene.

## Telemetry and remaining boundary

Bounded collectors retain events/raw frame intervals without snapshots. The preceding presentation boundary includes first-use stalls. Each execution separately rejects intervals strictly above `1000 / 30` ms. Median FPS is calculated directly (average two central samples when even); percentiles use nearest rank. Lost samples cannot pass. Metrics describe successful host presentation, not physical scan-out.

External evidence includes presentation, process, semantic action and lifecycle observations for every supported scenario. Each execution retains `process_before` and `process_after` samples for process CPU time and memory; these process-wide observations include runtime and driver work and do not identify individual copy operations. Optional common counters use the `shared-payload-v3` registry to observe shared arena, task, resource and GPU operations across all effects. Effect-local hooks have been removed. These counters are partial lower bounds: they omit effect-local allocations/copies and CLR, driver and OS internals, and never certify complete payload coverage. Missing optional counters do not fail external smoke validation; malformed claimed observations remain invalid. When supplied, copy/fill/GPU/diagnostic categories use checked 64-bit payload totals and numeric sites, and the validator checks their declared scope and site inventory. Execution totals include cleanup; never add child phase totals to an inclusive parent. At least 1 GiB copied produces a separate copy-review diagnostic without changing the smoke verdict. `diagnostic_issues` and `copy_traffic_scope` remain separate from `errors` and `verdict` in validated results, per-case summaries and the batch case array. The registry lists exact sites and exclusions. Arena-writer rentals expose actual live count/capacity and lifetime peaks. Hooks are optional during ordinary play.

`telemetry-report.json` retains every successful-present interval from the boundary preceding admission through cleanup/tails, dispatches, live semantic events and resolved-versus-applied HP contacts, compact HP/status/stock observations, allocation/GC changes, pool observations and actual lifecycle/renderer identities. Collection is bounded and loss blocks acceptance. No full snapshot is serialized per frame. Scored drivers reject screenshot capture; perform captures in a separate evidence replay. Audio uses the normal entry receipt/dispatch path even with diagnostic control transport; `--mute-audio` remains explicit and cannot certify the audio route.

Validate retained evidence independently:

```powershell
dotnet $tool validate-live --input $telemetryReport
dotnet $tool validate-live --input $telemetryReport --case $sealedCaseRecipe
```

The validator recomputes raw intervals from ticks/frequency, checks all presented indexes, both dispatches and costs, HP reconciliation, continuity, cleanup, renderer participation, coverage and reference environment. A structurally complete run below the 30 FPS threshold is `FAIL`; missing or contradictory evidence is `ERROR`. A telemetry `PASS` still has `campaign_acceptance=false` until fixture provenance and independent review are supplied.

Schemas cover implemented records. The validator supports their checked-in vocabulary and rejects unknown keywords/types. Breaking changes need a new version and separately named migration output; immutable records never change. Full result/review/fix contracts are prerequisites for acceptance. Byte counts remain integers, `_ms` means milliseconds, timestamps are UTC.

Exit codes: 0 success, 1 input/I/O/coordination error, 2 sealed-plan blockers or a live performance/overhead failure, 3 no claim, 4 unavailable/incomplete execution evidence (including blocked scenarios in an executed manifest). General `worker`, `replay`, `retry`, `validate-results`, `compare` and accepted-coverage `report` remain unavailable; use `run`, `supervise` and `validate-live` for execution and evidence validation. `capabilities` lists these boundaries. Preparation never displays a green campaign.
