# Texture authoring implementation and validation

See the [author workflow](../modding/texture-authoring.md) for package layout and canonical encoding. The current implementation provides a shared catalog/picker, CPU export/import, source compatibility checks and coordinated native texture publication. Installed-game end-to-end and performance evidence is still incomplete.

## Resource contracts

`TextureTarget` carries game, canonical logical owner, subresource, palette and variant. Target identity is independent of fingerprints, image dimensions, GPU addresses and catalog order. A `TextureTargetContract` checks the installed layout and logical canvas. Tile/page/VRAM/atlas layouts require uniform integer enlargement; full images can retain their logical canvas at a different physical resolution. Native aliases additionally require exact scaled crop boundaries.

`ResourceService.CaptureTextureSources` expands derived handles into stable source dependencies. `CaptureTextureSourcesForImages` selects presented CPU images, including mip, variant and animation images, before expansion. Captures contain CPU source contexts and the current resolved values. Source-owned authoring recovery rejects transient native sampling-window identities; the installation catalog supplies complete supported sources.

`NativeTextureMods.TryPrepare` validates only native contributions under `textures/`, freezes encoded content and rejects duplicate targets within a package. Canonical PNG/DDS files need no hand-written global index. Runtime mappings and exported authoring companions are recognized explicitly. Metadata conflicts do not silently fall back to filenames.

Source effect aliases use persistent owner/member/palette identities and a full canvas. The native resolver crops these according to the published `NativeAlias`, preserving exact boundaries. `native-effect-source` is the defined full-source counterpart of runtime `native-effect-texture`. Runtime effect crops use the existing alpha normalization (`alpha >= 127` becomes `127`; otherwise `0`); direct source exports do not apply that runtime policy. Direct-color effect sources requiring a selected device 555/565 contract are rejected until that contract is available.

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

The working-folder index records the last successfully installed source pixels and destination bytes per mapped input. It advances after publication succeeds. Current source pixels are compared with this imported snapshot on subsequent imports, preserving revert-to-original edits. Destination-only edits are retained when the working image is unchanged; two-sided changes require explicit selection. Missing/unchecked inputs never imply removal. Comparisons of destination bytes are conservative and can report a conflict for semantically identical re-encodes.

Installed packages use the existing mod watchers and debouncing. External working-folder editing uses explicit repeat import. Stable input reads retry three times with 75 ms intervals and retain invalid/incomplete revisions as diagnostics. Explicit refresh forces content revalidation even when timestamps and sizes appear unchanged. Catalog/export sessions own independent source readers; they do not share a mutable archive reader with gameplay.

## Bounds

| Area | Current bound |
| --- | --- |
| Decoded image | 16,384 pixels per dimension; 64 million pixels total |
| Imported input file | 128 MiB |
| Native image contribution | 64 MiB encoded per file; 512 MiB total encoded images |
| Texture mapping/authoring metadata | 96 MiB per record; native mapping preparation 512 MiB total |
| Resolver decoded/resolved LRU | 128 MiB and 256 entries; a larger single image is not retained in the cache |
| Install outputs / original rollback backups | 512 MiB each; at most 100,000 outputs |
| Recovery journal | 64 MiB, enforced on creation and recovery |
| Working synchronization index | 16 MiB |
| Field workspace | 64 million compiled tile pixels and supported image canvas limits |
| Runtime contribution paths | 4,096 characters, 64 components; no traversal/reparse installation paths |

Export processes one source unit at a time. The catalog, returned path list and source-hash index grow with entry count; decoded image retention and GPU residency do not intentionally grow with every exported texture. These are implementation bounds, not measured frame-time or full-game memory claims.

Unchanged native contributions reuse immutable entries and bounded decoded/resolved cache results across preparation. Resource preparation retains equal values and rebuilds only changed sources and their transform dependency closure. The presenter prepares affected resident GPU resources at its graphics boundary. No gameplay script replay or save mutation is part of this operation; interactive validation of all renderer consumers is still pending.

## Focused checks and remaining evidence

Run only the relevant test classes:

```powershell
dotnet run --project tests/Memoria.FF8.Mods.Tests -- --filter-class '*NativeTextureModsTests'
dotnet run --project tests/Memoria.FF8.Mods.Tests -- --filter-class '*TextureAuthoringResourceTests'
dotnet run --project tests/Memoria.FF8.Assets.Tests -- --filter-class '*TextureExchangeTests'
```

The first two portable selections passed with 22 and 14 cases respectively, including embedded-claim conflicts, live-lease rejection, startup recovery and presented mip/variant/animation capture. Assets exchange checks include an optional installed-resource case that is skipped without its configured resources. These commands do not run unrelated battle-effect tests. Follow [building and testing](building-and-testing.md) for packaging; do not use `Build.ps1 -Test` unless its entire test selection is authorized.

Still required: a cold interactive F2 export/edit/import/revert sequence against installed resources; interactive model/menu/world/battle/card/effect consumers and additional field scenarios; driver allocation-failure and in-flight resource-retirement checks; and full-game export, large ORA, catalog search, repeated reload, cancellation and idle/frame-time measurements. Record hardware, source edition, counts and measured durations/memory instead of inferring performance from asynchronous APIs.



### Renderer checks already run

The App `TexturePublicationTests` checks prepared GPU images remain unpublished until commit, require no later upload, and preserve previous GPU pixels/storage on budget failure or discarded preparation. Its two portable cases passed.

`InstalledTexturePublicationTests` ran both cases with actual GPU rendering against Classic and Remastered installations, with zero skips in each run:

- `NativeFieldPngRoundTripPublishesThroughActualBackgroundAtlasesWithoutChangingRenderedPixels`.
- `HdFieldTileUsesNativeLogicalCoordinatesAndChangesTheActualGpuFrame`.

These checks instantiate field assets/rendering directly. They verify actual background atlas publication and HD logical coordinates, without loading or advancing gameplay. They do not replace an interactive F2 acceptance run or prove every non-field renderer consumer. The installed run durations were 2.534 seconds for Classic and 6.108 seconds for Remastered; these are test durations, not export or frame-time benchmarks.

```powershell
dotnet run --project tests/Memoria.FF8.App.Tests -p:SkipMusicHost=true -- --filter-class '*TexturePublicationTests'
```

Installed cases require `MEMORIA_FF8_GAME_DIR`; without it the installed cases skip. An initial unconfigured combined run had two portable passes and two installed skips. The configured runs above exercised both installed cases without skips.

