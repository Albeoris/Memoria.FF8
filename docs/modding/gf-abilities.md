# Native GF ability payload

[Documentation](../README.md) / Modding

A Classic native Memoria package may contain `gameplay/gf-abilities.json`. It uses the existing package discovery roots, enablement policy, duplicate-package ownership and catalog order. It neither changes global ability IDs/effects nor changes texture/model precedence.

```json
{
  "schemaVersion": 1,
  "documentId": "2be0846e-aa14-47b2-943e-1bb532b103d7",
  "operations": [
    {
      "id": "add-existing-ability",
      "guardianForce": 0,
      "kind": "EnsureAbility",
      "ability": 38,
      "initialLearned": false
    },
    {
      "id": "change-starting-state",
      "guardianForce": 0,
      "kind": "SetInitialState",
      "ability": 1,
      "expectedInitialLearned": false,
      "initialLearned": true
    },
    {
      "id": "exchange-display-positions",
      "guardianForce": 0,
      "kind": "OrderSubset",
      "order": [2, 1],
      "expectedOrder": [1, 2]
    }
  ]
}
```

The IDs and operations above are synthetic examples, not a promise that those targets/guards match every installation. GF IDs are 0–15. Supported ability IDs are the existing catalog's non-sentinel IDs 1–115. Localized names are never identities. The source is the game's loaded definitions plus init.out defaults, rather than an assumed pristine retail version.

| Operation | Exact behavior |
| --- | --- |
| `EnsureAbility` | Append if absent, using the catalog AP cost, level-one availability and the specified initial state. Existing rows are never moved or reset. A contradictory redundant default is diagnosed; a redundant ensure grants no ownership. |
| `ReplaceAbility` | `ability` names the expected old identity; `replacement` names the destination. Missing targets or duplicate destinations skip the entire edit. Retain position, initial state, learning priority and conditions, retarget dependent prerequisite/exclusion references, and use the new catalog AP cost. Cyclic or unsupported conditions reject the operation. |
| `SetInitialState` | Change a template default. When `expectedInitialLearned` is supplied, apply only when it matches or the desired state is already satisfied. The editor guards inherited defaults; hand-authored documents may omit that guard. It never resets earned playthrough progress on activation. |
| `OrderSubset` | Place `order` participants in their current subset positions in the desired sequence. For `[A,X,B,Y]`, order `[B,A]` produces `[B,X,A,Y]`. Unrelated entries keep their positions. Missing participants skip the entire permutation. `expectedOrder` supplies comparison/diagnostic context; later overlapping permutations compose sequentially. |

Operations retain stable IDs and order. Packages compose in committed catalog order; the first successful replacement of a concrete identity wins because later edits cannot target its former row position. Save-local contributions follow the native stack. Outcomes and provenance report applied, already satisfied, missing target, conflicting value and unsupported condition results. Malformed schemas, duplicate document identities and globally invalid definitions reject preparation without partially publishing a generation.

A dependent edit must identify the producer it relies on. `producers` is an array of `{ "documentId": "...", "operationId": "...", "ability": 38 }` references. Every supplied producer must have actually created the named current row; an already existing same-ID row cannot satisfy a failed producer. The earlier single `producerDocumentId` / `producerOperationId` fields remain readable. The editor emits dependencies for produced participants, including both sides of an ordering operation.

The document has an optional `sourceFingerprint` diagnostic hint. An unrelated source change does not invalidate every guarded operation. Supported unknown JSON metadata is retained at document and operation level; unknown schema versions are refused. Canonical serialization sorts object properties, preserves operation ordering and writes only selected-layer intent, with no current AP, flattened combined tables or undo history. Editing retains document identity; an independent copy receives a new identity and its internal producer references are updated.

See the [player guide](../players/gf-ability-editor.md) for authoring/activation and the [developer contract](../developers/gf-abilities.md) for progression, migration and original-save projection.

## Global ability rules

Starting with engine compatibility version 0.8.15, a native package with a `mod.json` descriptor can also supply `gameplay/gf-rules.json`. Use it to change the cost or description of an existing ability, set the default cap for FFVIII summons, or grant a different cap to GFs that learn an ability. The learn-list document above controls which GFs can learn an ability.

```json
{
  "schemaVersion": 1,
  "defaultSummonDamageLimit": 9999,
  "abilities": [
    {
      "id": 91,
      "apCost": 200,
      "description": {
        "en": "Boost damage during summoning and remove this GF's summon damage limit.",
        "ru": "Усиливает урон во время призыва и снимает ограничение урона от призыва этого стража."
      },
      "summonDamageLimit": "unlimited"
    }
  ]
}
```

- Use existing ability IDs 1–115 and AP costs 1–255. AP changes affect learning and its menu display. Earned AP, learned abilities and selected learning targets retain their saved state. The current cost is checked when AP is awarded.
- A description is plain text keyed by language code. Supply `en` as the fallback. Memoria selects the active content language, including a localization override. Descriptions for unmodified abilities follow the normal text pipeline.
- Both limit properties accept a positive 32-bit integer or `"unlimited"`. The greatest limit declared by an ability learned by the summoning GF wins. With no applicable learned ability, use the package default, or the existing engine limit when the default is omitted. Positive FFVIII summon damage uses the resolved cap for that battle. Healing, other FFVIII actions and attacks resolved by the FFVII host use their own rules.
- Supply at least one contribution and at most one entry per ability in each document. Disjoint properties from enabled packages compose. Multiple packages assigning the same property reject the candidate generation with a conflict diagnostic. Editing a document uses the ordinary mod refresh path. An active battle retains its captured summon rules.
