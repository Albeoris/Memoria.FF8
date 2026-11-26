# Guardian Force form payloads

A native Memoria package can declare playable Guardian Force battle forms in `gameplay/gf-forms.json`. It uses the normal [package descriptor and enablement](behaviors.md). The package must be active, and the player's **GF Transformations** setting must be enabled for a new battle to use its profiles.

The file has `version: 1` and a `variants` array. Each entry has a `profile` and an `actions` array. See the [bundled package](../../Mods/Memoria/Guardian%20Forms/gameplay/gf-forms.json) for a complete file. The source game supplies models, animation sequences, action records and the GF summon cinematic.

## Profile and progression

| Property | Meaning |
| --- | --- |
| `profileId`, `variantId` | Stable lowercase identifiers of up to 64 characters using letters, digits and hyphens. All variants of one GF share its `profileId`. Use a distinct `variantId` for each selectable body across enabled packages. A GF supports up to 256 variants. |
| `guardianForceId`, `modelId` | Native GF index 0–15 and installed enemy-model index 0–239. The model must provide supported graphics, defenses and sequences. |
| `name` | Display name. Set `useInstalledModelName` for the installed model name or `useGuardianForceName` for the player's saved GF name. |
| `lifetimeTicks` | Active lifetime in battle-clock ticks, from 1 to 65,535. |
| `adjustments` | `strengthPercent` and `magicPercent`, from 1 through 1,000, each defaulting to 100. |
| `supportSequence` | Model sequence used for learned commands with a generic casting pose. Defaults to 11. The selected model must contain it. |
| `finisher` | A terminal binding with the `GuardianForce` source, `Finisher` role and the same `resourceActionId` as `guardianForceId`. It uses the GF's native action, formula and presentation. |
| `tiers` | Entries in ascending order from level 1 through at most 100. The highest unlocked tier supplies the current ordered `actions` list, which includes the finisher. |

A tier's `compelledPhysical` names a listed single-enemy physical attack. A tier may also name a listed spell in `compelledSpell`. The optional `healBeforeAttack` names an unlisted self-recovery action that requires grounding and has a once-per-invocation limit. The same recovery can appear as a manual command in an earlier tier. Learned commands come from the GF's acquired abilities and do not occupy IDs in the authored action list.

A package can supply multiple body variants for the same GF. Each entrant owns one variant and uses that variant's command list, model and defenses. Save-stable IDs should stay attached to the same variant and action when revising a package.

## Action bindings

| Property | Meaning |
| --- | --- |
| `action`, `name` | Stable action ID from 0 through 255 and command label. The bundled package also demonstrates named IDs. Pick unused numeric IDs for new actions. |
| `source`, `resourceActionId` | `Physical` selects an enemy physical attack record, `Magic` selects a spell, `GuardianForce` selects the corresponding GF summon, and `Native` selects a native action record by command and action ID. |
| `nativeCommand`, `nativeCrisisLevel` | For the `Native` source, command 8 selects an enemy action and is the default. Command 15 selects Blue Magic with an explicit crisis row from 1 through 4. |
| `nativeIdentity` | Required installed record identity with `attackType`, `power`, `effectId` and `element`. The selected row must match these numeric fields. |
| `sequence`, `requirePrototypeBinding` | Nonzero native sequence index. An optional prototype check requires that the same command, ID and sequence appear in the model's native ability slots. |
| `role` | `Attack`, `Spell`, `Recovery`, `Special` or `Finisher`. |
| `target` | `Native` uses the row's native targeting. `SingleEnemy`, `Self` and `AllEnemies` are relative to the actor. |
| `cost` | `Native` is the default. `Innate` makes a form spell independent of stocked magic. The actor still spends its turn. |
| `labelKey` | Optional battle UI localization key for an innate command. Its authored name is the fallback. |
| `power` | Optional replacement power from 0 through 255 for a physical or native action. `nativeIdentity.power` still checks the original installed value. |
| `oncePerInvocation`, `requiresGrounded`, `requiresFullAtb` | Admission conditions, each off by default. |
| `presentedModels` | For native paired effect 258, two variant IDs belonging to the same GF, including the active body. The pair must bind the two native body roles, enemy models 62 and 63. |
| `terminal` | True for the GF finisher. The finisher ends the form. |

Model indices and action rows are validated against the active resources. A custom profile must also use an engine-supported entry, finisher and action presentation. In Remastered, a native package with GF form content can include `mod.json`, `mod.ini`, `README.md` and `LICENSE` or `LICENSE.md` alongside the payload. Other native gameplay contributions remain subject to their own edition rules.

Variant IDs have one owner. Duplicate definitions in enabled packages and missing paired variants are reported with package and payload details. Invalid edits to an active package retain the previous valid publication. Changes affect new battles after publication, while a captured battle keeps its selected profiles.
