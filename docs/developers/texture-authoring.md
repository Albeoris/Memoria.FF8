# Texture authoring implementation and validation

See the [author workflow](../modding/texture-authoring.md) for package layout and canonical encoding. The current implementation provides a shared catalog/picker, CPU export/import, source compatibility checks and coordinated native texture publication. Installed-game end-to-end and performance evidence is still incomplete.

## Resource contracts

`TextureTarget` carries game, canonical logical owner, subresource, palette and variant. Target identity is independent of fingerprints, image dimensions, GPU addresses and catalog order. A `TextureTargetContract` checks the installed layout and logical canvas. Tile/page/VRAM/atlas layouts require uniform integer enlargement; full images can retain their logical canvas at a different physical resolution. Native aliases additionally require exact scaled crop boundaries.

`ResourceService.CaptureTextureSources` expands derived handles into stable source dependencies. `CaptureTextureSourcesForImages` selects presented CPU images, including mip, variant and animation images, before expansion. Captures freeze the selected installed original and current resolved CPU values. `ResolveOriginalForExport` applies the selected installation provider before user mods; a native TIM fallback is not substituted for an installed Remastered HD source. Logical source dimensions remain independent of physical HD dimensions. Frozen source fingerprints survive disposal of independent authoring readers. Source-owned authoring recovery rejects transient native sampling-window identities; the installation catalog supplies complete supported sources.

`NativeTextureMods.TryPrepare` validates only native contributions under `textures/`, freezes encoded content and rejects duplicate targets within a package. Canonical PNG/DDS files need no hand-written global index. Runtime mappings and exported authoring companions are recognized explicitly. Metadata conflicts do not silently fall back to filenames. Overlapping source/pixel/semantic hashes compare without case sensitivity; native-coverage Base64 compares exactly, and native dimensions must agree. Missing legacy fields merge. Informational provenance and usage aliases need not match.

Cross-package exact-target duplicates remain valid. Each masked package/path receives a nonfatal `Memoria.TextureOverridden` diagnostic naming the first effective contribution in normal catalog precedence. Disabled packages do not contribute files. The resolver does not enable packages or modify priority to manufacture a visible result.

Source effect aliases use persistent owner/member/palette identities and a full canvas. The native resolver crops these according to the published `NativeAlias`, preserving exact boundaries. `native-effect-source` is the defined full-source counterpart of runtime `native-effect-texture`. Runtime effect crops use the existing alpha normalization (`alpha >= 127` becomes `127`; otherwise `0`); direct source exports do not apply that runtime policy. Direct-color effect sources requiring a selected device 555/565 contract are rejected until that contract is available.

Shared field model `NativeSheet` addresses normalize to source-owned `field/model/main_chr` or `field/model/second_chr` row targets on the proved logical 128x128 canvas. Field usages remain informational aliases; pixel similarity never establishes sharing. Legacy field-local and registered targets remain resolvable offline. Source-declared `eyes-open` and `eyes-closed` companions preserve native copy phases; untouched native companions can be projected from edited bank pixels without changing clocks or gameplay. Installed HD planes remain authoritative. Unknown variants are rejected.

Package priority is evaluated before target specificity. Within one package, the runtime tries the exact target, then its proved `NativeAlias`, then `NativeSheet`. Variants choose their winning package independently; an explicit sibling overrides that phase, while untouched source-declared phases remain available. Runtime composition can enlarge coherent planes to a common physical density; separate authored files keep their own sizes.

The field ORA bridge reuses Frameworks workspace readers, verifies source MIM/MAP fingerprints and preserves Pupu identities and native composition. It emits per-tile runtime replacements at uniform integral scale. It does not infer native animation, blending or ownership from a flattened painting.

## Installation and publication

`TextureModInstaller.Plan` produces only selected image/sidecar outputs. `WithNewDescriptor` adds the existing descriptor schema and new-only local `mod.ini` enablement; it refuses to overwrite existing package configuration. Distributable official packages should not include an author's local enablement file.

Use `PrepareInstall`, then `ModRuntime.ApplyTextureImport` on the runtime owner thread. Preparation exposes frozen output bytes for a candidate catalog overlay. The candidate resolver/resource generation is prepared before destination writes. A deferred or failed preparation leaves the destination and current generation intact.

`ExpectedDestinationHashes` binds selected outputs to the reviewed destination state. Preparation and the short apply boundary recheck it. Apply stages new bytes and retains original backups in `.texture-stage-<id>` with a flushed recovery journal. `Commit` finalizes the journal and invokes the already prepared publication callback; exceptions restore the prior package through disposal. Candidate lazy loaders read retained immutable/committed content, not disposable staging paths. Existing watcher callbacks request later reconciliation rather than publishing intermediate writes. `ModPreparationResult.Discard` releases abandoned candidates and must respect owning-thread requirements for GPU disposal.

The journal is recoverable after process interruption, rather than a claim that multiple ordinary file writes are atomic. Native discovery attempts recovery before reading a package descriptor or contribution. The staging lease prevents another live process from being rolled back; a busy lease requests a later retry. There is also a recovery API, but no dedicated user-facing recovery button:

```csharp
TextureModInstaller.Recover(allowedNativeRoot, packageDirectory);
```

An uncommitted journal restores hash-checked originals and removes newly created output files. A committed journal retains the new content and removes staging. A missing/corrupt journal or required backup fails explicitly and should be preserved for repair. Do not delete the staging directory to dismiss that error. This recovery contract covers interrupted processes; it is not a power-loss durability guarantee for every filesystem.

## Incremental editing and watchers

`Assets.TextureExportJournal` writes schema-version-1 `texture-export-index.json` plus `texture-export-report.txt` and `EDITING.txt` in the destination working folder. The index contains game, scope, provider, language, original/resolved mode, completion state, completed resource count, deduplicated-alias count, written bytes, relative output entries and diagnostics. Ordinary image entries include target, contract, companion path, source fingerprint, pixel/semantic baselines, usage aliases and companion roles where applicable. Layered-field entries identify the ORA and companion; their target/contract/fingerprint/baseline fields are null, and the verified field source metadata lives in the companion. One field workspace counts as one exported resource, rather than its compiled tile count.

The journal starts incomplete and becomes complete only through `Complete()`. Disposal writes an honest incomplete report after failure/cancellation, retaining completed outputs. Counts never claim that failed or unscanned archive contents form a known full-game total. An existing index prevents a second export into the same working folder, protecting its edited files and recorded baselines. Export bookkeeping is separate from the local import synchronization index below; neither belongs in a runtime native package.

The schema-3 working-folder index records semantic source/destination baselines separately per workspace input and destination package, plus reusable explicit target mappings. Semantic fingerprints include decoded pixels, logical target/contract and native coverage; encoded byte hashes protect input/destination identity at apply time. Equivalent re-encodes are omitted. The index advances only after successful publication; old state preserves explicit mappings without assigning a legacy baseline to an arbitrary destination.

Exported original and resolved semantic baselines distinguish an unedited resolved export from a new edit on first import. ORA companions hold per-tile compiled baselines. Reverting to original pixels after a prior import is a new change. Destination-only edits survive unchanged working files; two-sided changes are unchecked conflicts until explicitly selected. Missing/unchecked inputs never imply removal. Input leases and expected destination hashes reject changes between review and commit.

PNG, ORA and their companions use no-overwrite final renames, including when another writer creates a file after the initial check. Cancellation may retain a completed image with an incomplete journal; it never authorizes overwriting existing artwork. Export journal refreshes intentionally replace their own bookkeeping files.

Installed packages use the existing mod watchers and debouncing. External working-folder editing uses explicit repeat import. Stable input reads retry three times with 75 ms intervals and retain invalid/incomplete revisions as diagnostics. Explicit refresh forces content revalidation even when timestamps and sizes appear unchanged. Catalog/export sessions own independent source readers; they do not share a mutable archive reader with gameplay.

## Bounds

| Area | Current bound |
| --- | --- |
| Decoded image | 16,384 pixels per dimension; 64 million pixels total |
| Imported input file | 128 MiB |
| Native image contribution | 64 MiB encoded per file; 512 MiB total encoded images |
| Texture mapping/authoring metadata | 96 MiB per record; native mapping preparation 512 MiB total |
| Resident native CPU preparation pin | 256 MiB for decoded RGBA and mip data, retained through CPU preparation and GPU finalization, then released |
| Resolver decoded/resolved LRU | 128 MiB and 256 entries; a larger single image is not retained in the cache |
| Independent authoring source cache | Normally 64 MiB and 4 retained nested archives per provider; one current source unit may exceed that cache budget up to 256 MiB, with larger units rejected before materialization |
| Install outputs / original rollback backups | 512 MiB each; at most 100,000 outputs |
| Recovery journal | 64 MiB, enforced on creation and recovery |
| Working synchronization index | 16 MiB |
| Field workspace | 64 million compiled tile pixels and supported image canvas limits |
| Runtime contribution paths | 4,096 characters, 64 components; no traversal/reparse installation paths |

Export processes one source unit at a time. The catalog, returned path list and source-hash index grow with entry count; decoded image retention and GPU residency do not intentionally grow with every exported texture. These are implementation bounds, not measured frame-time or full-game memory claims.

Authoring opens fresh independent source providers. An encoded source context reopens its logical key after archive eviction instead of depending on a cache entry remaining resident. Read streams own their exact-length snapshot bytes, which compatible decoders reuse without sharing mutable live archive readers. These authoring cache limits do not change ordinary gameplay source caches.

A mixed game/model-source selection owns two independent provider caches. Their ordinary combined limit is eight archives and 128 MiB; two simultaneously current oversized units can total up to 512 MiB. Those are theoretical configured limits, rather than measured mixed-provider peaks.

`ResourceService.BeginPrepare` now provides an owner-thread `PreparationJob`: `Advance` accepts a time budget and unit limit, resolves complete source/transform units and yields without publishing. `Complete`, `CompletedUnits`, `Prepared` and `IsStale` expose progress and staleness. Cancellation, registration changes and transform failure discard partial candidates; only `PreparedResources.Commit` changes handles/generation. Synchronous `Prepare` drains the same implementation. A unit is indivisible, so this is not a strict millisecond pause guarantee. Metadata-only preparation capture must not invoke source image/variant callbacks on background threads. Host updates use a 4 ms budget and at most 64 source/transform units per pump; one unit can overrun that budget. Native worker preparation validates nonresident contributions individually and pins resident decoded images/mips through GPU finalization to avoid owner-thread redecoding. Stale graphs restart preparation; constructor initialization alone drains preparation before session creation.

Independent native-image worker preparation and host/UI deferred import integration are implemented. The worker uses immutable package bytes; mutable game source callbacks stay on the owner thread. Import staging, hashes and synchronization state are prepared off-thread; staged transactions and reviewed input leases survive deferred publication. Normal watcher preparation waits behind an explicit import, and a deferred normal candidate retries without restarting its debounce. Focused tests pass, including native preparation pinning and cancellation. A Classic watched-host test passed with real PNG edit/invalid/repair revisions, ready initial policy and 97 bindings spread across incremental steps. Remastered texture-only package eligibility is confirmed across discovery, enablement and explicit import. Classification requires an actual PNG/DDS contribution and restricts accompanying files to supported texture mappings, declared localization, package configuration and recognized documentation; arbitrary gameplay JSON does not become compatible. Mixed gameplay packages retain normal compatibility restrictions. The corrected Remastered watched-host edit/invalid/repair case passed. No claim of measured interactive latency follows from these APIs.

Unchanged native contributions reuse immutable entries and bounded decoded/resolved cache results across preparation. Resource preparation retains equal values and rebuilds only changed sources and their transform dependency closure. The presenter prepares affected resident GPU resources at its graphics boundary. No gameplay script replay or save mutation is part of this operation; interactive validation of all renderer consumers is still pending.

## Focused checks and remaining evidence

Run only the relevant test classes:

```powershell
dotnet run --project tests/Memoria.FF8.Mods.Tests -- --filter-class '*NativeTextureModsTests'
dotnet run --project tests/Memoria.FF8.Mods.Tests -- --filter-class '*TextureAuthoringResourceTests'
dotnet run --project tests/Memoria.FF8.Assets.Tests -- --filter-class '*TextureExchangeTests'
```

Latest focused evidence includes NativeTextureMods 40/40 (latest 1.538 s); resource/authoring 37/37 (including incremental cancellation/staleness/atomic failure); source 81/81 including 36 relevant native effect texture-source cases; and the final source companion 17/17. Strict metadata integrity 14 cases and affected exchange/journal/ORA checks passed as 34 unique cases (the one optional installed case was rerun with configured resources). The latest export-journal selection passed 5/5 in 597 ms. A later cancellation/metadata/journal selection passed 20/20 without skips in 3.129 s; its installed case cancels before the first model/field/texture enumeration step. Token-aware discovery checks between archive entries, fields and model directory members. This does not measure cancellation latency within an indivisible source read. The final broader selection used 244 distinct cases across 18 explicit classes: 242 passed, 2 justified skips, zero failures, in 1.774 s. It combined the 15-class regression selection with Native40, SourceProvider3 and ResourceAuthoring17 without double-counting earlier runs. Skips were an unavailable optional Demaster HorizonPack fixture and unavailable OS symbolic-link privilege. No unrelated battle-effect simulation suite was run. Installed cases skip without explicitly configured source resources. Follow [building and testing](building-and-testing.md) for packaging; do not use `Build.ps1 -Test` unless its entire test selection is authorized.

Still required: a cold interactive F2 export/edit/import/revert sequence against installed resources and real editor save patterns; interactive model/menu/world/battle/card/effect consumers and additional field scenarios; driver allocation-failure and in-flight resource-retirement checks; and full-game export, large ORA, catalog search, repeated reload, cancellation and idle/frame-time measurements. Record hardware, source edition, counts and measured durations/memory instead of inferring performance from asynchronous APIs.

## Installed catalog coverage and measurements

The default catalog uses one compound field-workspace node per field. Individual source tile/palette leaves expand lazily with `ExpandFieldTextureCatalog`; `includeFieldTiles: true` is available for explicit detailed enumeration. The counts below are compact catalog nodes from the measured installations, not a sum of all expanded tile targets, decoded images or exported files.

| Catalog category | Node unit | Classic | Remastered |
| --- | --- | ---: | ---: |
| Battle stage | Material/texture target | 1,358 | 1,358 |
| Battle enemy | Material/texture target | 416 | 416 |
| Battle model | Material/texture target | 34 | 50 |
| Battle weapon | Material/texture target | 32 | 32 |
| World character | Material/texture target | 16 | 16 |
| Field | Shared source row and declared variant target | 948 | 713 |
| Field background | Compound workspace, with lazy tile leaves | 851 | 851 |
| Menu and cards | Texture/palette target | 151 | 151 |
| World textures | Texture/palette target | 350 | 350 |
| World static | Material/texture target | 69 | 69 |
| Tutorial artwork | Source TIM slot/palette target | 38 | 38 |
| World terrain | Source texture/palette target | 320 | 320 |
| Startup artwork | Artwork target | 30 | 30 |
| Battle and magic textures | Supported source-owned texture/palette target | 1,991 | 2,017 |
| Guardian Forces and cinematics | Supported source-profile texture/palette target | 324 | 0 |
| Triple Triad | Native card/artwork target | 127 | 127 |
| **Compact nodes returned** | **Includes the 851 workspace parents** | **7,055** | **6,538** |

Zero entries in a category means that scan returned none; it does not establish that the edition contains no such textures. In particular, Remastered source-profile discovery reported an unsupported native executable capability. Indexed full-source effect exports cover known TIM sequences and supported scripted material profiles. Transient procedural upload/GF sampling windows and direct-color sources needing an unresolved 555/565 contract remain explicit unsupported boundaries.

These were complete runs of the configured discovery routine followed by representative category PNG round trips and one field ORA round trip. They were not full-game image exports. Hardware reported by the process was Windows build 10.0.26300, AMD64 Family 26 Model 68 Stepping 0, with 16 logical processors available. Memory values below use decimal MB; implementation cache limits above use binary MiB.

| Measurement | Classic | Remastered |
| --- | ---: | ---: |
| Compact discovery | 78.443 s | 81.197 s |
| Representative PNGs plus one field ORA | 0.771 s | 0.787 s |
| Sampled managed-memory peak during discovery | 445.38 MB (445,379,808 B) | 361.96 MB (361,957,528 B) |
| Retained authoring-archive peak during discovery | 8.31 MB (8,307,616 B) | 20.95 MB (20,948,278 B) |
| Resident resource textures after the check | 0 | 0 |
| Diagnostics classified skipped / unsupported / failed | 325 / 195 / 1 | 264 / 11 / 56 |

The reported process working-set high-water was 675.97 MB (675,966,976 B); process high-water marks are cumulative and are not independent per-edition allocation peaks. Managed memory was sampled during enumeration, not continuously. These figures show bounded source-cache retention and no resource texture residency growth in this workload; they do not establish full-game export peak memory, GPU residency, cancellation latency or frame-time impact.

The two configured installed discovery/representative cases passed without skips in 2 minutes 44.291 seconds. This is selection duration, not an interactive latency measurement. Counts supersede earlier compact reports before shared-sheet deduplication.

Skipped records include unavailable resources; unsupported records require an explicit unsupported contract, including unresolved scripted source-owner selectors and unsupported material directories. Remastered cinematic discovery lacks the required native-executable capability. An earlier category-based rule incorrectly labelled failed non-party battle reads as unsupported; it was removed. The table restores 1 Classic out-of-range failure and 56 Remastered invalid-directory/section/bone failures from the recorded diagnostic messages and their exception contracts, without repeating discovery. Source counts and timings are unchanged. These failed reads remain unresolved coverage gaps; their category does not establish an unsupported format or corrupt original data.

A separate Classic export was stopped by its 512 MiB conservative allocation threshold: 851 completed PNGs, 537,149,440 B estimated output allocation and 128.763 s including discovery. All generated imagery was removed after verification. It did not reach the entire catalog or field workspaces, so full-game export completion/peak memory remain unverified. No cancellation latency or frame-time guarantee is inferred.

### Renderer checks already run

The portable `TexturePublicationTests` selection passed all 3 cases, including 24 repeated publication cycles under the cache budget. It verifies prepared GPU images stay unpublished until commit, require no later upload, and preserve prior pixels/storage on preparation budget failure or discard.

Actual GPU checks passed:

- Field PNG round-trip and HD logical-coordinate cases for both Classic and Remastered sources, without advancing gameplay.
- Model unedited/HD cases across Classic, Remastered and both mixed model-provider directions: 8 cases, zero skips.
- SpriteBatch cached portrait, pointer and magazine-paper publication with unrelated card/host ownership retained, against Classic and Remastered: 1 installed case per edition, zero skips. Magazine paper consumes mean source RGB; authored pattern/alpha is not a full paper texture contract.
- Targeted Classic UI selection: 13 cases covering ownership 2, paper 1, installed UI publication 1 and scene eligibility 9; matching comparison rendered at 640/1280 widths in 2 additional cases. The final broader UI selection includes software-diagnostic eligibility and deferred import checks: 125 cases selected, 122 passed, 1 optional fixture skipped and 2 existing session-lifecycle failures. Those two empty-game-path failures reproduce in a retained pre-task executable. New UI/state/root/eligibility/deferred groups passed. Deferred lifecycle plus native/ModRuntime checks selected 75 cases: 74 passed and 1 optional installed fixture skipped. Texture-only package compatibility checks passed 9/9. The combined compatibility/deferred/ModRuntime selection passed 45 cases with 1 optional fixture skip; corrected watched-host acceptance and the final GPU matrix passed as recorded below.

Latest actual host/GPU regression evidence:

- Classic watched-host lifecycle: 1 passed in 3.449 s, covering real edit/invalid/repair changes, ready constructor policy and incremental handling of 97 bindings.
- Classic GPU matrix: 8 passed in 5.232 s.
- Remastered GPU matrix: 5 passed. After correcting texture-only package eligibility, its actual watched-host case and new native-font GPU case passed together: 2/2, zero skips, 3.128 s. The host covered active constructor policy, 97 incrementally handled bindings and edit/invalid/repair changes retaining the last good generation.
- Each mixed model-provider direction: 2 passed, in 1.893 s and 4.104 s.
- Classic native-font GPU: 1/1 passed in 1.258 s. Both font cases verify HD prestaging/discard/commit, changed actual pixels, unchanged logical glyph advance and the correct presented source.

The scoped implementation has no pending feature-code changes. Real editor/interactive acceptance and complete export/performance evidence remain unverified validation limits. These durations are test durations, not export or frame-time benchmarks. Renderer checks construct assets directly; they do not replace cold interactive F2 operation or a real editor save sequence, nor prove every consumer/driver failure path.

Current-scene capture is explicitly unavailable in software diagnostic rendering and unsupported presentation states. FFVII texture authoring is not claimed.

```powershell
dotnet run --project tests/Memoria.FF8.App.Tests -p:SkipMusicHost=true -- --filter-class '*TexturePublicationTests'
```

Installed cases require explicitly configured source resources and a GPU test device. An unset installation causes skips; report configured runs separately from unconfigured runs.
