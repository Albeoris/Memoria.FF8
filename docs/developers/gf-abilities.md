# GF ability ownership and original-save conversion

The supported gameplay-payload path is Classic. The ability catalog remains the installed game's existing catalog; this feature does not add global ability IDs, effects, junctionable GFs, character command slots or character passive slots.

## Managed definitions and progression

`NativeCharacterVitals` owns gameplay GF state. `MenuDefinitions.Templates` contains sixteen ordered arrays of `GuardianForceAbilityTemplate`. References to prerequisites and exclusions use ability identity. `LearningPriority` retains automatic learning-selection priority independently of display order. `SupportedCondition` preserves source entries whose condition is not implemented without offering an invented unlock rule.

`NativeGuardianForceVital.Progression` contains the identity-keyed `GuardianForceProgression` ledger. Entries retain learned state, earned AP, explicit forgetting, item-teaching status and activity. Removing a template makes its ordinary earned state dormant; it must not grant effects while absent. Reinstating the same ability can recover that state. Item-taught abilities remain usable without an authored learning row. `InitialLearned` describes creation defaults, not permission to overwrite existing earned progress.

Use `ResolveGuardianForceTemplates`, `ApplyGuardianForceTemplates` and `GuardianForceAbilities` rather than assuming the native learning-array index is the current authored row. Applying templates returns a copied, reconciled owner. Gameplay consumers, item teaching, forgetting, AP awards and junction checks use this managed state. The native learned bank and twenty-one AP bytes are compatibility mirrors; they do not bound the managed collection. Supported additions are catalog IDs 1 through 115. Source sentinel and unsupported entries are not additions offered by the editor.

## Source initialization and migration

`NativeCharacterInitialization.Read` combines kernel learning rules and AP costs with the independent `init.out` learned bank, progress, current learning and forgotten flags. It freezes `MenuDefinitions.SourceTemplates` before gameplay can change the learned bank, and records sixteen complete `InitialStates`. Do not reconstruct pristine defaults from an earned midgame learned bank.

The extra template and progression properties are additive to the existing Memoria JSON representation. Older saves can omit them. On first managed reconciliation, `ImportGuardianForceProgression` maps their native slots by ability identity and preserves already learned and item-taught abilities. With a complete initialization baseline, an untouched unacquired GF can defer default initialization until acquisition. Without that baseline, the importer conservatively treats the observed record as initialized rather than granting new defaults over an ambiguous save. Opaque learned-bank sentinel/high bits are retained in the compatibility mirror.

Each managed GF records `LastProjection`, including the compatibility learning-slot mapping. `NormalizeGuardianForceCompatibility` compares external edits against that projection and adopts unambiguous learned/AP/forgotten changes by ability identity. Ambiguous structure or an inconsistent representation without a projection becomes a `GuardianForceOpaqueBaseline`; the old profile moves to read-only `GuardianForceRecoveryProfile`, and bounded diagnostics record the loss of attribution. Recovery cannot establish which historical package caused an observed change. Changes needing this recovery during an active or pending battle are rejected with an instruction to use a standalone checkpoint outside battle. Normalization updates root, event and departing-field owners together.

`GameSession.PreviewGuardianForceAbilities` computes progression and junction consequences without publishing them. `PrepareGuardianForceAbilities` checks the reviewed source revision, mod generation and character state, checks the mutation boundary, and prepares root, event and departing-field character owners together. `CommitPreparedGuardianForceAbilities` publishes that prepared state synchronously. Applying while a battle is active or pending is rejected, preserving its captured definitions.

`GuardianForceAbilityProfile` schema 1 carries source identity, applied layers, separately owned local documents, excluded document identities and local ownership aliases for independent exported copies. Preparing an independent export stages its document exclusion/alias without mutating the session; successful file writing precedes the prepared metadata commit. An unsafe session boundary rejects that ownership-changing copy before writing. A native original save cannot carry that attribution or the managed dormant ledger. A flattened save does not prove which historical mod produced it; integrations must not infer an inverse patch from that observation.

## Intentional binary limits

The original codec still writes sixteen 68-byte GF records, a 128-bit learned bank, twenty-one AP bytes, a byte learning target and a 24-bit forgotten mask. The target original game's effective-list algorithm has its separate twenty-two-entry limit. No native structure is enlarged.

`NativeGuardianForceLegacyCompatibility.Analyze(state, targetSource)` produces the shared `NativeGuardianForceLegacyExportPlan`. The target must be the declared original game's initialized source definitions. `Losses` lists configuration changes, identity-based AP/forgotten remaps or losses, dormant history, capacity and availability differences, and invalid current-learning or equipped references. `RequiresConfirmation` also covers remapping notices; it is deliberately conservative. Reports use numeric identities so presentation can add localized labels without changing codec semantics.

The plan contains a copied `ProjectedState`. It restores the target learning definitions, maps progress and forgetting by ability identity, keeps representable learned item-taught abilities, and deterministically retains the first twenty-two learned IDs in ascending order. Other learned IDs are explicitly reported as dropped. It evaluates target availability before preserving the current learning selection and clears unavailable equipment and magic-junction references in the copy. Uninitialized unacquired GFs use the target's initial state. The projection drops the managed ledger, attribution profile and authored template view from the exported character owner; the live owner is untouched.

`NativePcSaveConverter.Export(state, targetSource, allowGuardianForceLoss)` consumes the same analysis. Without explicit loss acceptance it rejects a plan needing confirmation. The ordinary `Export(state)` uses frozen source templates when available and rejects unreviewed GF losses or a managed state without declared source definitions. The GF AP copy is checked and bounded to exactly twenty-one destination bytes. Existing original bytes outside the converter's owned fields remain in the copied original block.

The player save and copy paths show a scrollable, game-window-owned review before accepting GF losses. A copied Memoria source is held as the reviewed snapshot through conversion rather than reread after confirmation. The normal original-save continuation validation and atomic destination replacement still run. GF acceptance does not override unrelated blockers such as unsupported gameplay preferences or magic history; those continue to require Memoria format. Cancellation keeps the menu open and preserves source and destination files.

## Focused checks

From the product directory:

```powershell
dotnet run --project tests/Memoria.FF8.Assets.Tests -c Release -- --filter-class '*GuardianForceSourceInitializationTests' --filter-class '*NativePcSaveConverterTests'
dotnet run --project tests/Memoria.FF8.App.Tests -c Release -- --filter-class '*OriginalSaveBankTests' --filter-class '*PlayerSaveMenuTests'
```

These synthetic checks cover separate source defaults, identity remapping, expanded projection, item-taught preservation, unacquired defaults, same-sized configuration changes, live-state preservation, file safety, other-feature blockers and review cancellation. Installed-game and original-save fixture checks remain optional and report skips when their configured inputs are absent. This selection does not run battle-effect tests. Runtime learning, composition and mod-publication tests are separate focused selections.

All files needed by this public implementation live in the product source tree. Proprietary resources and saves are supplied by the user or explicit optional test configuration; they are not distribution inputs committed with the feature.

## Host and editor integration

`GameResourceHost` prepares GF documents alongside model/field/texture adapters. Its gameplay callback prepares a session candidate before generation publication; commits contain prepared assignment operations. A concrete progression/junction review keeps the previous generation active until accepted. Source revision, committed mod generation and the complete source vitals guard a review against staleness. The application explains unsafe boundaries rather than retaining authorization for a later, possibly different loss.

`GuardianForceAbilityProfileCoordinator` keeps a missing package or GF payload's pinned contribution in its saved position, distinguishes explicit disabling/incompatible editions, composes final local contributions and exposes missing-payload diagnostics. Current setup substitutes a disabled editor draft at its catalog position only when Preview as enabled is selected. Local ownership excludes its matching native document and can be explicitly converted back. Independently exported copies use private save-profile aliases; these are not exported mod payload data.

`GuardianForceAbilityDraft` owns stable logical row handles and raw undo commands only in memory. Its exported document contains canonical net operations: starting states fold into newly produced rows and stable subset permutations replace interaction history. Undo removes the most recent command for a row, keeps unrelated commands and groups dependent transformations. A saved or applied checkpoint resets session undo. The custom UI retains pane/cursors/scroll positions through rebuilds and resolves the visible row by identity; Source and without-layer comparisons refuse editing.

The authoring folder picker uses the standard Windows folder dialog with exception-safe DPI restoration. It creates and opens the default mod directory while allowing unrestricted navigation and package selection. The authoring service rechecks ownership and document revisions around its targeted atomic write. Saving a file and publishing a runtime generation are separately reported outcomes.
