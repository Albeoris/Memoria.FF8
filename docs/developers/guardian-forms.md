# Guardian Force forms

[Documentation](../README.md) / Developers

GF forms are an optional per-save battle feature. The Guardian Forms package supplies Ifrit, Sacred and Minotaur. The logical player actor keeps its ID, party slot, statistics and reward attribution. The active variant supplies its presentation, commands, defenses and GF HP owner.

## Ownership

| Owner | Responsibility |
| --- | --- |
| `BattleGuardianFormDefinition` | Immutable GF identity, resource/action bindings, typed identity stat adjustments and simulation lifetime. |
| `BattleSession.GuardianForms` | Eligibility, charge admission, transitions, canonical GF HP binding, action submission and reversion. |
| `BattleGuardianFormState` | Restorable phase, invocation identity, selected variant, used once-per-invocation actions, remaining simulation ticks and completion flags. |
| `BattleSession.GuardianFormPresentation` | Owned entry context, persistent body, shortened finisher, camera and resource cleanup. |
| `BattleHudController.GuardianForms` | Navigation and explicit input requests; no gameplay state mutation. |

The canonical GF row remains the only writable GF HP pool. The combatant retains human HP independently; its effective HP access routes to that row while the form is active. Statistics still belong to the player. While the form is active, elemental defenses and the forty status resistance bytes come from its installed enemy prototype (Ifrit 61, Minotaur 62 or Sacred 63). The human defense banks remain independent and become effective again on return. Snapshots preserve those human banks and rebind the prototype defenses from the saved form resources.

The active profile selects innate commands from the GF level projected from saved experience. Tiers declare command identities, compelled actions and any automatic self-heal. Bindings specify native source, animation, target selection, stock policy and use limits. Once-per-invocation actions are recorded in the form state at admission. A private battle fork resumes the same body, uses and native invocation. A single-variant legacy Ifrit state can recover its body identity from its resources.

At activation the session binds prototype resistances, then rechecks each retained harmful status once. Existing status flags retain no original attacker or potency. This check uses ordinary status resistance with equal offensive and defensive stats and power 200, which gives full retention at the neutral resistance byte of 100. A failed check clears the status and its timer. Rebinding an existing active form during a fork preserves that decision. See the [player command tables](../players/guardian-forms.md).

The command list appends the battle commands from this GF's learned ability IDs, including item-taught abilities. The kernel maps each command ability to its battle command. The form menu hides Item, Draw, Magic, GF and MiniMog. Selection and submission identify a learned command by its GF ability ID, independently of its current grid position. The HUD uses the same grid layout, text measurement and hit testing as Draw and Magic.

`Charging -> Entering -> Active -> Finishing -> Returning` is owned by the session. Forced reversion takes precedence over expiry. Lifetime advances on the existing battle simulation clock, including full ATB. Entry and the terminal summon reset the ordinary ATB rather than awarding an extra turn. A committed action finishes before its presentation is released; uncommitted form commands are cancelled through existing command banks. Completed entry applies GF affinity once, and the finisher suppresses a second application.

A party-wide summon slot covers charging, queued and playing summons, including every form phase. Its occupancy comes from existing battle owners, so cancellation and completion release the slot with their normal cleanup. The form and its own invocation count as one owner.

Learned Darkside binds the selected body’s ordinary melee record and sequence, applies its native damage multiplier and charges the active GF HP pool. The form finisher uses the original GF formula, including Boost.

The option is captured in the battle definition and snapshot. Restore validates profile, GF ownership, queued commands, invocation IDs and presentation ownership before rebinding HP. An installed battle fork can recover missing innate and learned command bindings from the selected game resources. A logical fork can read the older ability-band field and use retained kernel actions. Actions already queued keep their original identity across a change of level table. Restoring a component with overlapping summon owners requires a checkpoint before the second summon. Old snapshots without feature state retain classic behavior. Enabled form snapshots created before prototype defenses were stored require explicit migration of the form resources before use. The mod host validates installed actions and models when preparing a candidate package catalog. Battles with the per-save option off attach an empty form resource list and allocate no form state.

## Ifrit presentation

Three invocation intents separate gameplay from presentation: `FullClassic`, `EntryOnly` and `FinisherWithoutEntry`. Ordinary confirmation on Ifrit remains the existing full summon.

Entry uses original effect 201 with a restricted positioning context, without admitting an offensive action. The source boundary is `mag200_b.00` instruction offset `0x154`, after emergence and landing and before the remaining attack. At that boundary the adapter releases the cinematic arena and hands off to original enemy model 61 in the existing player's presentation slot. It does not add an enemy combatant.

The finisher creates a fresh effect invocation. `NativeBattleIfritContinuation` validates its expected source instructions and reconstructs the required task hierarchy, transforms, model pose, texture publications and camera inputs from the current resources. Initialization excludes contact, sound, battle-clock advancement and gameplay RNG. It does not retain pointers from entry or replay the opening invisibly. Subsequent native callbacks execute the remaining roar, attack and completion normally.

Skip settles an already admitted Boost press, preserves the earned native multiplier and locks further input. It advances the same owned invocation through ordinary callbacks until the original main script sets header flag 0x10, then plays the meteor finale, contact and cleanup at normal speed. Only skipped callbacks suppress sound publication. The existing Escape battle (left) input requests Skip; there is no separate shortcut or HUD hint. Snapshot state preserves the invocation, skip request and original header flag; that flag remains the authority for resuming normal finale playback. Errors tear down the owned presentation and restore the actor before propagating the unsupported contract.

## Brothers and additional profiles

At charge completion the session selects a variant with its battle RNG, records its identity in the prepared entry and passes that identity to presentation. The decision is committed together with entry. The HUD, command table, prototype defenses and renderer all use the same selected variant.

Brotherly Love uses the installed effect 205. Mad Cow Special uses enemy action 213 and effect 258. Its companion is owned by the presentation for that action. It has no combatant record, HP pool, ATB or reward identity.

At STR 60 against VIT 30, with a neutral damage roll (random byte 16), the physical formula gives 261 damage at power 16, 326 at power 20 and 391 at power 24. Mower at power 11 deals 179 to each enemy, rising to 228 at power 14. Against two identical enemies the level-70 comparison is 391 on one target for Sacred or 456 spread across both for Minotaur. This comparison assumes ordinary hits, neutral defenses and no further modifiers.

The [GF form package format](../modding/gf-forms.md) declares variants in `gameplay/gf-forms.json`. A new presentation family needs verified entrance, body handoff and finisher contracts for its native resources. Package variants can reuse those contracts. Keep native effect details in the presentation owner and keep level thresholds, roster of forms and balance choices in the package.

Run focused lifecycle, snapshot, command/HUD and affected native-effect tests. Installed-resource presentation tests require `MEMORIA_FF8_GAME_DIR`; a skipped test does not validate the cinematic. See [building and testing](building-and-testing.md) for test selection and distribution validation.
