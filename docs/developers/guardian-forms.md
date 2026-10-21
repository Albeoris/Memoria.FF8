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

The canonical GF row remains the only writable GF HP pool. The combatant retains human HP independently; its effective HP access routes to that row while the form is active. Statistics and resistance rules still belong to the player. Action bindings select existing native definitions and sequences; damage is resolved by the existing battle resolver. Innate Fire has an explicit free cost policy, leaving ordinary magic-stock validation intact.

`Charging -> Entering -> Active -> Finishing -> Returning` is owned by the session. Forced reversion takes precedence over expiry. Lifetime advances on the existing battle simulation clock, including full ATB. Entry and terminal Hell Fire reset the ordinary ATB rather than awarding an extra turn. A committed action finishes before its presentation is released; uncommitted form commands are cancelled through existing command banks. Completed entry applies GF affinity once, and terminal Hell Fire suppresses a second application.

The option is captured in the battle definition and snapshot. Restore validates profile, GF ownership, queued commands, invocation IDs and presentation ownership before rebinding HP. Old snapshots without feature state retain classic behavior. When disabled, the game does not load form-only resources or allocate form state.

## Ifrit presentation

Three invocation intents separate gameplay from presentation: `FullClassic`, `EntryOnly` and `FinisherWithoutEntry`. Ordinary confirmation on Ifrit remains the existing full summon.

Entry uses original effect 201 with a restricted positioning context, without admitting an offensive action. The source boundary is `mag200_b.00` instruction offset `0x154`, after emergence and landing and before the remaining attack. At that boundary the adapter releases the cinematic arena and hands off to original enemy model 61 in the existing player's presentation slot. It does not add an enemy combatant.

The finisher creates a fresh effect invocation. `NativeBattleIfritContinuation` validates its expected source instructions and reconstructs the required task hierarchy, transforms, model pose, texture publications and camera inputs from the current resources. Initialization excludes contact, sound, battle-clock advancement and gameplay RNG. It does not retain pointers from entry or replay the opening invisibly. Subsequent native callbacks execute the remaining roar, attack and completion normally.

Skip settles an already admitted Boost press, preserves the earned native multiplier and locks further input. It advances the same owned invocation in bounded callback batches through ordinary contact and cleanup, suppressing skipped sound publication. Snapshot state preserves the invocation and skip request. Errors tear down the owned presentation and restore the actor before propagating the unsupported contract.

## Adding another GF

Register one immutable definition and verified native action bindings in the static GF registry. Supply its original visual resources through the existing assets loader, and implement its presentation preparation, handoff and completion at the feature-local adapter boundary. Reuse session eligibility, HP ownership, simulation timing, snapshot checks and UI inputs. A GF requiring multiple visible bodies can compose them under one actor; that renderer is not implemented by the single-body Ifrit adapter.

Run focused lifecycle, snapshot, command/HUD and affected native-effect tests. Installed-resource presentation tests require `MEMORIA_FF8_GAME_DIR`; a skipped test does not validate the cinematic. See [building and testing](building-and-testing.md) for test selection and distribution validation.
