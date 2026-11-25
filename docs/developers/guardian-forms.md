# Guardian Force forms

[Documentation](../README.md) / Developers

GF forms are an optional per-save battle feature. The first registered profile is Ifrit. The logical player actor keeps its ID, party slot, character statistics, statuses and reward attribution; the form replaces its presentation, commands and active HP owner.

## Ownership

| Owner | Responsibility |
| --- | --- |
| `BattleGuardianFormDefinition` | Immutable GF identity, resource/action bindings, typed identity stat adjustments and simulation lifetime. |
| `BattleSession.GuardianForms` | Eligibility, charge admission, transitions, canonical GF HP binding, action submission and reversion. |
| `BattleGuardianFormState` | Restorable phase, invocation identity, remaining simulation ticks and completion flags. |
| `BattleSession.GuardianFormPresentation` | Owned entry context, persistent body, shortened finisher, camera and resource cleanup. |
| `BattleHudController.GuardianForms` | Navigation and explicit input requests; no gameplay state mutation. |

The canonical GF row remains the only writable GF HP pool. The combatant retains human HP independently; its effective HP access routes to that row while the form is active. Statistics still belong to the player. While the form is active, elemental defenses and the forty status resistance bytes come from its installed enemy prototype (cavern Ifrit, model 61). The human defense banks remain independent and become effective again on return. Snapshots preserve those human banks and rebind the prototype defenses from the saved form resources.

The playable Ifrit profile selects innate actions from the GF level projected from saved experience. Its progression uses levels 1, 15, 30, 70 and 100. See the [player command table](../players/guardian-forms.md). Both physical moves use the original cavern prototype's attacks and sequences. The spells bind to the installed Fire, Fira, Firaga and Flare records and use the form's innate cost policy. The profile supplies its terminal Hell Fire. Enemy Ifrit retains his own original AI and ability bands.

The command list appends the battle commands from this GF's learned ability IDs, including item-taught abilities. The kernel maps each command ability to its battle command. Draw, Item, GF and Magic are excluded. Selection and submission identify a learned command by its GF ability ID, independently of its current grid position. The HUD uses the same grid layout, text measurement and hit testing as Draw and Magic.

`Charging -> Entering -> Active -> Finishing -> Returning` is owned by the session. Forced reversion takes precedence over expiry. Lifetime advances on the existing battle simulation clock, including full ATB. Entry and terminal Hell Fire reset the ordinary ATB rather than awarding an extra turn. A committed action finishes before its presentation is released; uncommitted form commands are cancelled through existing command banks. Completed entry applies GF affinity once, and terminal Hell Fire suppresses a second application.

A party-wide summon slot covers charging, queued and playing summons, including every form phase. Its occupancy comes from existing battle owners, so cancellation and completion release the slot with their normal cleanup. The form and its own invocation count as one owner.

For learned MiniMog, admission checks the original PocketStation state, Gil and recipient before returning Ifrit and queueing MiniMog. The MiniMog action then owns the summon slot. Learned Darkside takes its weapon and HP cost from the ordinary command contract. Its presentation uses Ifrit's melee sequence, so a gunblade user resolves the untriggered strike.

The option is captured in the battle definition and snapshot. Restore validates profile, GF ownership, queued commands, invocation IDs and presentation ownership before rebinding HP. An installed battle fork can recover missing innate and learned command bindings from the selected game resources. A logical fork can read the older ability-band field and use retained kernel actions. Actions already queued keep their original identity across a change of level table. Restoring a component with overlapping summon owners requires a checkpoint before the second summon. Old snapshots without feature state retain classic behavior. Enabled form snapshots created before prototype defenses were stored require explicit migration of the form resources before use. When disabled, the game does not load form-only resources or allocate form state.

## Ifrit presentation

Three invocation intents separate gameplay from presentation: `FullClassic`, `EntryOnly` and `FinisherWithoutEntry`. Ordinary confirmation on Ifrit remains the existing full summon.

Entry uses original effect 201 with a restricted positioning context, without admitting an offensive action. The source boundary is `mag200_b.00` instruction offset `0x154`, after emergence and landing and before the remaining attack. At that boundary the adapter releases the cinematic arena and hands off to original enemy model 61 in the existing player's presentation slot. It does not add an enemy combatant.

The finisher creates a fresh effect invocation. `NativeBattleIfritContinuation` validates its expected source instructions and reconstructs the required task hierarchy, transforms, model pose, texture publications and camera inputs from the current resources. Initialization excludes contact, sound, battle-clock advancement and gameplay RNG. It does not retain pointers from entry or replay the opening invisibly. Subsequent native callbacks execute the remaining roar, attack and completion normally.

Skip settles an already admitted Boost press, preserves the earned native multiplier and locks further input. It advances the same owned invocation through ordinary callbacks until the original main script sets header flag 0x10, then plays the meteor finale, contact and cleanup at normal speed. Only skipped callbacks suppress sound publication. The existing Escape battle (left) input requests Skip; there is no separate shortcut or HUD hint. Snapshot state preserves the invocation, skip request and original header flag; that flag remains the authority for resuming normal finale playback. Errors tear down the owned presentation and restore the actor before propagating the unsupported contract.

## Adding another GF

Register one immutable definition and verified native action bindings in the static GF registry. Supply its original visual resources through the existing assets loader, and implement its presentation preparation, handoff and completion at the feature-local adapter boundary. Reuse session eligibility, HP ownership, simulation timing, snapshot checks and UI inputs. A GF requiring multiple visible bodies can compose them under one actor; that renderer is not implemented by the single-body Ifrit adapter.

Run focused lifecycle, snapshot, command/HUD and affected native-effect tests. Installed-resource presentation tests require `MEMORIA_FF8_GAME_DIR`; a skipped test does not validate the cinematic. See [building and testing](building-and-testing.md) for test selection and distribution validation.
