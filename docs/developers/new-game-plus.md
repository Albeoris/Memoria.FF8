# New Game+ ownership and persistence

[Documentation](../README.md) / Developers

`CompletedGameData` is a non-resumable FFVIII source purpose in the existing bounded, checksummed gameplay container. Component schema 3 accepts historical ordinary schemas 1/2 with their existing coverage rules. A completed document has `Resume.Kind = Completed`, no field/world position, and an independently validated transfer payload instead of an ending VM. Unknown required policies remain errors. Snapshot schema 10 adds optional completion/NG+ owners; older saves remain ordinary and retain their recorded progress.

`Ff8CompletionState` records the idempotent terminal event. The Events adapter recognizes the reviewed original program's terminal callback after the final ending movie and credits have completed, including legitimate skipping. The finish achievement, a generic HALT, ENDING wait, last-boss victory or a movie request is insufficient. The callback does not replace character, GF, magic or economy owners.

`NewGamePlusDraft` edits detached owners. It receives a completed source, a pristine resource-initialized new game and installation-owned UI preferences. Target settings changes convert the current magic draft and revalidate Native defaults; they do not reread source stock or discard Custom selections. New-game resource preparation uses a detached host. The existing live session remains suspended until a fully validated candidate replaces it.

The immutable `NewGamePlusDefinition` stores policy/schema versions, source completion/content/revision identities, selected settings/presets and resolved initial owners. `StartingChoicesApplied` and per-entity initialization receipts distinguish that recipe from mutable current progress. Definitions are applied at the cold resource-initialized boundary before the first field transaction. Later loads restore current owners directly; they never apply the recipe over newly earned progress.

`NewGamePlusRewardRules` owns effective EXP/AP rates in integer tenths. Reward aggregation policy 1 scales each native recipient/category subtotal once with `(subtotal * tenths + 5) / 10`, using wide arithmetic and the existing award caps. Character and GF native subtotals remain independent. A present NG+ policy replaces the corresponding integer tuning multiplier; it does not multiply an already scaled award. Absent NG+ policy preserves historical integer tuning, including rates above the new slider range. Save restoration, setup grants and imported starting AP/EXP are not new earned rewards.

Additional roster availability is save-local. `NewGamePlusPermanentGuests` distinguishes the selected guests from the global expanded-roster policy, which composes with the run dependency. Native story removal preserves selected guests' independent stock/junction owners. Dream EXP is transferred between the actual temporary rows and independent guest records with a saved original-owner backup. World switching uses the same effective policy in presentation and action validation.

GF definition restoration composes in this order:

1. Read current installed stat tables and ability catalogs.
2. Bind the embedded resolved initial GF definitions as a self-contained source, retaining explicit baked-document exclusions.
3. Apply saved local editing intent, including later player edits.
4. Restore current identity-based learned/AP progression.

Current progress is never reconstructed from initial learned flags. Embedded source documents are excluded from package reapplication; missing original authoring packages do not make an embedded definition an external dependency. Resource refreshes and postbattle owner reconstruction retain that source and its ownership marker. Normal non-NG+ saves retain their existing composition behavior. Completed sources embed their effective definitions so the next cycle reads current progression and can survive source package removal without nesting prior saves.

Manual completed writes validate a temporary file before atomic publication. Overwrite approval binds to the destination's content digest, is consumed once, and is checked again immediately before publication. Ordinary save, transfer/import and combined writes use the same guard. Automatic writers skip completed cleanup targets, including a second completion check after external writer/persistence callbacks. Completed saves are never authored as automatic slots.

`GameplaySaveCodec`, player-save summaries, direct/configured loading, diagnostic snapshots and FFVIII payloads in combined envelopes preserve the relevant owners. Completion purpose and NG+ rules require Memoria format; original FFVIII export rejects lossy conversion. Campaign identity is explicit; FFVII completion cannot create FFVIII completion.

## Focused checks

Run the native Runtime test executable with filters for `NewGamePlusRewardTests`, `NewGamePlusPartyTests`, `NewGamePlusGuardianForceTests`, `Ff8CompletionTests` and `CompletedGameContractTests`. App tests cover `CompletedGameStoreTests`, `NewGamePlusDraftTests` and `NewGamePlusNavigationTests`. The optional `NewGamePlusInstalledResourceTests` reads `MEMORIA_FF8_GAME_DIR` and `MEMORIA_FF8_TEST_ENDING_SNAPSHOT`; it resumes the supplied standalone terminal, does not replay earlier travel, and checks source creation and fresh-session ownership. Missing external inputs report a skip. An optional absolute `MEMORIA_FF8_TEST_COMPLETED_OUTPUT` retains one explicitly requested validated completed source and refuses to overwrite an existing output.

Keep tests of this feature separate from unrelated battle-effect tests. Logical terminal continuation is not evidence of continuous audiovisual credits playback or a whole new playthrough.
