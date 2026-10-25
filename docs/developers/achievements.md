# Achievements

Gameplay publishes immutable facts through `IAchievementService.Record<TEvent>` or `RecordBatch<TEvent>`. A successful return means the local profile durably contains the event receipts, progress, unlocks and account contributions. It does not mean the platform accepted or synchronized anything. The gameplay owner must retain its saved pending facts until that return, then acknowledge them. Replaying pending facts after interruption is safe. Tools and tests use separate profiles and the no-external backend by default.

`AchievementService` belongs to the application lifetime. Pump it on the application thread before pause or scene-transition returns. Loading a save does not recreate it. `Flush` makes one bounded attempt; `Dispose` makes one final bounded attempt before releasing the backend and repository. Native adapters must keep every call nonblocking and pump native callbacks on that same thread.

## First rule and save observations

The stable local ID is `ff8.combat.enemies_defeated.100`. `AchievementRegistry.CreateCombatRegistry(bool remastered)` registers its `SaveHighWater` handler on `BattleKillTotalObservedEvent`, a saved-counter observation at accepted battle completion. It retains the highest observed absolute total, including totals above 100, and clamps only `DisplayProgress`. Loading older or branching saves cannot subtract progress or repeat an additive lifetime contribution. Missing historical counters remain unknown (`ProgressKnown == false`) until an eligible observation arrives; they are never reconstructed from reward-roll flags. `CardSelected` is a reward selection and cannot establish which removal command was used.

`EnemyDefeatedEvent.FromBattle` requires a stable encounter occurrence and allocation ID. Its event ID is the allocation ID within that occurrence. A new allocation in the same formation slot has a different identity. Rejected candidates must never reach `Record`. The accepted owner stores facts in its save outbox, including observations and the platform owner captured at acceptance. An unknown owner remains unbound even if an account becomes available before the outbox drains.

The official completion gate differs by resource edition. Original evaluates all completed outcomes, including defeat and scripted exit, independently of formation flag `0x10`. Remastered evaluates escape regardless of that flag, and victory only when the formation's own flag `0x10` is clear; it skips defeat and scripted exits. Active battles and debug/research reward policies never qualify. Do not use merged battle flags for the Remastered formation check. A completed battle with no newly defeated enemy still publishes the saved total, so imported progress can be observed without inventing a defeat event.

Live enemy facts remain available to independent custom rules and are staged with `AcceptedOutcome == null` until the accepted completion finalizes them. They do not drive the official save-total rule. Save rollback discards abandoned staged facts while retaining finalized durable-outbox work. Each event retains the semantic owner captured at its own authoritative acceptance boundary; replay never substitutes the account currently signed in.

Official account/save progress has separate local buckets for unbound state and each semantic provider/product/user/profile. Local custom rules may use `LocalProfile` scope. A previously unbound total does not move into a newly signed-in account. Selecting another account changes the displayed official bucket and delivery queue, preserving the previous queue for that account's return.

## Add a rule over an existing event

Register at composition time before creating the service. The handler returns a nonnegative contribution, an absolute observation for `SaveHighWater`, or `null` when the event does not qualify. Handlers must be deterministic and must not perform I/O, native calls or presentation work.

```csharp
AchievementRegistry registry = AchievementRegistry.CreateCombatRegistry(remastered: true);
registry.Register<EnemyDefeatedEvent>(
    new(new("memoria.example.defeats"), "Example", "A local custom rule.", 5, AchievementScope.LocalProfile),
    fact => fact.Facts.RewardPolicy == BattleRewardPolicy.NativeCombat ? 1 : null);
```

The example is instructional and is not part of the shipped catalogue. Duplicate IDs, invalid thresholds, late registrations and conflicting bindings fail at composition. Every registration includes its typed handler, so a definition cannot silently have no handler. There is no global achievement-condition switch.

To add a genuinely new condition, define one immutable struct implementing `IAchievementEvent`, with an event ID, authoritative context ID and optional captured `AchievementOwner`. Add the emission to its accepted gameplay mutation and extend the saved outbox format if needed. Register the handler; no platform adapter change is needed. IDs must be stable, namespaced and independent of native API keys.

Subscribe to `Committed` for progress and unlock notifications. `NewlyUnlocked` identifies the single local transition. Notifications use the same reconciled owner view as `Get`: a lower local save observation cannot display a regression below confirmed remote progress or repeat an already-observed unlock. Notifications run after durable local commit, independently of platform availability. Subscriber failures are isolated and reported in `Diagnostic`; they cannot cause another increment. UI must distinguish local unlock, queued batch, SDK acceptance and observed remote state.

## Add a verified platform mapping

An `AchievementBinding` associates the local ID with a verified native achievement key and, only when actually configured, an optional native counter key. Bindings are composed for the selected edition/provider. They do not register a publisher-side achievement. Custom rules without bindings stay local.

An `IAchievementBackend` owns initialization, callbacks, shutdown, verified identity and key translation. `Identity` may become non-null only after the provider, product and real user are confirmed. It must remain null for an unauthenticated wrapper identity. Preserve the last verified binding only while the SDK establishes that cached/offline access still belongs to that same user. When a selected product changes, dispose its adapter and construct one with the matching mappings; do not reuse SDK handles or mappings across products.

`Read` returns counters and unlock IDs keyed by local achievement IDs, translating through the supplied mappings. Return null for unavailable/failed reads; never manufacture zero or locked results. A binding without a persistent counter does not require a counter value. `Submit` receives an immutable-in-practice durable absolute target batch. Do not mutate its dictionaries. Store completion must include the exact batch revision and captured owner. Progress-popup callbacks must not raise `StoreCompleted`. SDK success alone retains the pending batch until readback observes its targets. Set `PersistenceConfirmed` false for reads that expose only a locally modified or offline SDK cache: these cannot retire a durable batch. Only confirmed persisted readback may acknowledge it; cached success is not evidence of server synchronization.

Represent unsupported keys/types/configuration as `Unsupported` with a precise diagnostic, avoiding endless retry. `TransientFailure` and unavailable stats keep local tracking usable. The default `NoAchievementBackend` never initializes a native library and never writes an account. An adapter implementation or DLL presence alone does not establish installation compatibility: actual selected-build initialization, authenticated identity, reads and explicitly authorized integration writes remain separate validation requirements.

## Durability and reconciliation

`FileAchievementRepository` uses a single-writer lock and a flushed temporary file replaced over the committed profile. A leftover `.pending` file is uncommitted and ignored; the gameplay outbox supplies replay. A corrupt committed profile fails closed instead of resetting replay protection. Progress and receipt storage failures propagate from `Record`, so gameplay must retain the fact. External submission happens only after the delivery batch itself has been persisted.

`RecordBatch` coalesces a bounded pending gameplay batch into one synchronous file replacement. It evaluates and deduplicates each fact, clones only touched encounter receipt sets, and copies the small counter/delivery dictionaries. Invalid facts, overflow, handler failure or persistence failure leave the entire batch uncommitted. Presentation receives one committed transition per changed achievement/owner from its prebatch state to final state, including one unlock notification if the threshold was crossed. `Record` delegates a singleton batch. The application may coalesce its outbox on a bounded cadence and prioritize threshold/shutdown boundaries, but must keep all facts recoverable until the batch returns successfully; an in-memory enqueue is never durable acknowledgement.

Receipts are grouped by encounter and contain only allocation IDs, without complete gameplay events or action histories. They must be retained while arbitrary older saves may be loaded. Globally bounded receipts and unlimited old-save replay protection cannot both be guaranteed without a trusted campaign watermark that rejects stale saves; this implementation does not silently prune receipts. Profile storage and per-event serialization therefore grow with the number of accepted encounter allocations. A later bounded campaign-retirement protocol must preserve the save handoff contract before pruning.

Each pending batch records semantic destination, revision, baseline, absolute targets and covered contribution totals. `AccountCumulative` rules use a confirmed remote baseline plus genuinely new contributions captured for that owner. Facts accepted while an initial read is unavailable remain included. A durable ambiguous batch is read back or retried at exactly the same absolute target before newer contributions form another batch. `SaveHighWater` rules instead reconcile comparable absolute observations using their maximum; they never add a remote baseline to a save total. Unlock-only mappings retain local progress and submit only when the owner-specific threshold is reached.

Confirmed remote state is read and persisted for the verified owner even before any local contribution exists. Remote counters become baselines, never synthetic local contributions; remote unlocks have a separate monotonic ledger. Account and unbound buckets remain separate. Unlock-only remote unlocks do not invent a historical progress total: the snapshot can be unlocked while `ProgressKnown` remains false. Successful confirmed observations cover comparable save progress or an already-unlocked unlock-only contribution without a redundant platform write. Failed and locally cached reads cannot import a baseline or unlock, and monotonic confirmed observations survive process restart or later stale reads.

Native counter writes coalesce on a five-second cadence; a newly unlocked local transition or `Flush` requests an immediate attempt. Failures use bounded exponential retry delay. A revision/owner mismatch cannot acknowledge another batch. One process holds an exclusive local destination lock for provider/product/user even when two profiles are open. This prevents simultaneous Memoria writers on one machine, but cannot coordinate another device or the stock game. Concurrent external account-cumulative writers can still overwrite or merge ambiguously; absolute APIs cannot provide distributed exactly-once increments. Do not claim otherwise.

Run focused fake tests with the runtime test runner's `--filter-class '*Achievement*'`. No platform client or real account is used. Native checks must be opt-in and must report unavailable resources as unverified rather than silently succeeding.

## Steam runtime and launch

The application uses one Steam adapter and the pinned x64 Steamworks SDK 1.65 runtime in `Data/Steam/steam_api64.dll`. See [the runtime notice](../../vendor/Steamworks/README.md) for provenance, hash and terms. The installed game's older native DLLs are not loaded or replaced. GOG provider/transport types and local metadata detection are extension points; a GOG backend is not implemented.

| Selected Steam product | AppID | Native achievement | Persistent progress |
| --- | --- | --- | --- |
| Original | `39150` | `TOTAL_KILLS_100` | `nmy_kill`, nonnegative increment-only Int32; publisher progress range 0–100. |
| Remastered | `1026680` | `NEW_ACHIEVEMENT_1_10` | No publisher progress stat; progress remains local until the unlock is submitted. |

`SteamAchievementMappings` holds edition mappings. Adding a verified mapping does not require changing native adapter code. The adapter supports integer counters and monotonic unlocks; another native stat type would require an explicit capability extension.

Installation identity comes from the selected directory and validated storefront metadata, including remembered/manual selections and update resumes. A DLL's presence does not select a storefront. The adapter sets process-local `SteamAppId` and `SteamGameId` before loading the explicit bundled runtime, then verifies the SDK's AppID and actual individual user. It uses UserStats013's preloaded-state contract and successful typed reads to establish readiness. Initialization success alone is insufficient. It neither creates `steam_appid.txt` nor starts the stock executable through `RestartAppIfNecessary`.

The launcher provides a local-only setting, also available as `--achievements-local-only`. Local profiles are stored under `%LOCALAPPDATA%/Memoria/FF8/achievements/`, separated by product and semantic account within the profile. Diagnostic/capture/control/timed sessions and direct test construction use isolated temporary profiles and no external backend. Only the normal player entry point enables external composition. Ordinary modded gameplay follows the same rule API.

Starting automatic exploration or replaying a diagnostic reproduction from the player UI first flushes accepted player receipts, closes the player SDK/profile, and selects an isolated local profile for the rest of that process. Restart returns to ordinary player composition. An unavailable player-profile write prevents this switch until its recoverable receipts can be stored; it does not acknowledge them into the diagnostic profile.

To explicitly check an installed Steam product through the actual Memoria process without changing achievements or local progress:

```powershell
./Data/Memoria.FF8.exe --probe-steam-achievements --game-dir "$env:MEMORIA_FF8_GAME_DIR"
```

Run separately with each selected installation. The probe validates product/user/readiness, pumps callbacks and reads the mapped keys; `writes=0` is part of its result. Keep Steam running under the intended owning account. Native initialization and reads have been demonstrated for both AppIDs with the bundled runtime. Genuine stat/achievement writes remain a separate explicitly selected integration check; fake backend and adapter tests are the default. Read success does not establish server-side write synchronization or complete Remastered gameplay support.

Store submissions are serialized. Progress callback 1103 never acknowledges persistence. Only stats-store callback 1102 can confirm SDK acceptance for the current pending batch, followed by readback. Repeating the same in-flight batch does not issue another native store. A missing store callback times out after 30 seconds and disables external delivery for that process with a restart diagnostic, preserving durable pending work. This avoids attributing a late uncorrelated callback to a newer batch. SDK cached/offline acceptance is reported separately from independently observed server synchronization.
