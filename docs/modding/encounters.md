# Semantic encounter documents

[Documentation](../README.md) / Modding

Classic native Memoria packages can contribute UTF-8 JSON files beneath `gameplay/encounters/`. Each file is an independent document. Two packages may both contain `gameplay/encounters/encounter.json`; both contributions are retained. Package discovery, enablement, and edition compatibility use the existing Mods flow. Native encounter documents do not add Remastered support.

The document/composition contract is available for authoring and automated validation. Playing an authored encounter additionally requires an installed-resource binding and a runtime publication provider for every requested capability. If that provider is unavailable, enabling the package reports `Memoria.UnsupportedEncounterRuntime` and rejects publication. Parsing a document alone does not establish playable support.

## Document identity and selection

Every document requires `schemaVersion: 1`, a globally stable `documentId`, an `encounter` semantic selector, `atomic: true`, and a nonempty `operations` array. Optional fields are `title`, `sourceProfile`, `requiredActorCapacity`, `placements`, and descriptive `metadata`. Unknown executable properties, duplicate JSON properties, unsupported versions, and duplicate document or operation IDs are errors. Use `metadata` for descriptive extensions.

An encounter alias must resolve to a verified selected-resource baseline. It is not a model name or a wildcard for every battle using that model. `sourceProfile`, when supplied, must match the binding's compatibility identity. Semantic changes compose over selected decoded resources, including compatible binary replacements. Unsupported profiles and missing anchors fail preparation.

`requiredActorCapacity` ranges from 1 to 4. These are simultaneously active enemy slots, separate from the eight source formation records. A summon reserves every required slot before publication and never evicts another active actor. Each placement is `{ "id": "left", "x": -300, "y": 0, "z": 0 }`, with finite bounded coordinates interpreted by the encounter binding. Authors must validate positions against the intended stage.

Created actor, group, policy, rule, route, presentation, and placement names belong to the declaring document. Native baseline actors such as `original` come from the verified binding. Array indices, participant slots, localized text, and executable addresses are not author identities.

## Granular changes and dependencies

This synthetic example adjusts one original actor property without replacing its resource or AI:

```json
{
  "schemaVersion": 1,
  "documentId": "example.original-hp",
  "encounter": "example.verified-encounter",
  "atomic": true,
  "operations": [
    {
      "id": "increase-hp",
      "kind": "SetActorProperty",
      "actor": "original",
      "property": "stats.maximumHpMultiplier",
      "expectedValue": 1.0,
      "value": 1.25
    }
  ]
}
```

The alias above is illustrative. The multiplier is finite and bounded to 0.01–100. The runtime must apply it once to the original's resolved baseline maximum HP before initializing current HP; copies inherit the final resolved maximum. Composition stores the semantic multiplier rather than flattening an enemy resource. The product rounds down after multiplication and clamps the result to 1–2,147,483,647. The original multiplier is retained so restoring a resolved native level reproduces the same maximum without multiplying twice. Modifiers on actors introduced by templates are rejected until that allocation contract supports them.

| Operation | Authored responsibility |
| --- | --- |
| `AddActorTemplate` | Define a fresh full-HP, zero-ATB summoned actor inheriting resolved level/stats and an existing model. Declare its policy, group, arrival, ordinary summoned lifecycle, and no rewards. |
| `EnsureGroup` | Add explicit named members while retaining unrelated members. |
| `AddActionPolicy` / `AddRule` | Define named normal-action rules. `AddRule` carries a `ruleDefinition` object. |
| `SetRuleProperty` / `RemoveRule` | Change or remove one rule by stable policy/rule IDs. |
| `SetNormalActionPolicy` | Assign normal-action responsibility with an expected owner and preserved native lifecycle. |
| `SetActorProperty` | Guard and change `stats.maximumHpMultiplier`. |
| `AddDamageRoute` / `SetDamageRouteProperty` / `RemoveDamageRoute` | Add typed positive-HP damage routing, edit recipients, or remove an owned route. |
| `AddPresentation` / `SetPresentationProperty` | Define a finite clip/opacity track or guard an endpoint edit. |
| `GateNativeEvent` | Gate the bound native taunt channel by own-turn count while preserving native message order. |

Editable rule properties are `action.spell`, `action.spells`, `when.ownTurn`, `when.ownTurnAtLeast`, `when.ownTurnAtMost`, and `priority`. A guard uses `expectedValue`; the desired value uses `value`. Removing a rule or route requires its full definition as `expectedValue` and an explicit producer. Presentation edits support `startFrame`, `endFrame`, `opacity.from`, and `opacity.to`.

For intentional refinement, identify the actual producer:

```json
{
  "id": "change-support-spell",
  "kind": "SetRuleProperty",
  "producer": {
    "documentId": "example.support",
    "operationId": "define-support-policy"
  },
  "policy": "support-policy",
  "rule": "regen",
  "property": "action.spell",
  "expectedValue": "Regen",
  "value": "Haste"
}
```

Producer identities refer to successful entity creation. An unrelated entity with the same local name cannot satisfy a failed producer. Missing producers and dependency cycles reject composition. Stable topological ordering schedules producers before consumers; among ready contributions it retains committed catalog/document order. Files within a package use catalog path order. This affects encounter composition only, and does not reorder the common catalog or other mod formats.

Disjoint property edits compose. Identical ensures may coalesce without fabricating producer ownership. Incompatible writes, missing guards, competing exclusive policies, overlapping rules without distinct explicit priorities, incompatible route ownership, and insufficient capacity reject the candidate atomically. Diagnostics retain package, payload, document, operation, encounter, and relevant conflicting ownership context.

## Actions, damage, and presentation

Rules use positive own-turn equality or inclusive ranges. Overlapping ranges require distinct explicit priorities. Actor counters are independent; they do not use a global battle turn or an eight-bit opening counter.

Canonical spell names are English semantic identities rather than localized display labels. Target selectors, Scan-based knowledge policies, Reflection decisions, and repeated casts are typed contracts. Enemy-side knowledge is specific to the battle and target actor; player Scan history and shared model IDs do not grant it. A declared action must have supported native execution and presentation before the package can be published for gameplay.

The supported redirect contract uses living activated recipients, already resolved positive numeric HP damage, equal integer shares with discarded remainder, discarded overkill, and normal source damage when no recipients remain. It does not route healing, HP assignment/cost, absorption healing, or instant-KO status. Recipient mitigation must not run twice. A runtime provider must integrate this contract with native contacts, reactions, damage feedback, and drain ownership.

Presentation frames use `frameNumbering: "zeroBased"`, a finite `startFrame`/`endFrame`, `loop: false`, `after: "nativeIdle"`, and linear opacity endpoints in [0, 1]. At source frames 8 and 29, a track from 0 to 1 has opacity 0 and 1 respectively, with interpolation based on source time. Clip IDs remain asset identities and require validation against installed resources. Both summoned actors remain outside scheduling and targeting until arrival completes.

## Enablement and refresh

Put the package beneath a configured `Mods/Memoria/<Package>` directory. Ship optional showcases with `mod.ini` containing `enabled = 0`. Enable through the existing Mods controls, refresh after editing, and inspect that package's diagnostics. Enter or restart the intended encounter after a successful publication; active captured encounters retain their accepted definitions.

Preparation reads all documents and composes a complete candidate generation. Publication updates resource selection and encounter definitions at the existing commit boundary. A malformed or incompatible refresh retains the previous accepted generation. Disabling a package rebuilds the next encounter from the selected baseline and remaining enabled documents. Saves requiring an unavailable definition identity must fail with a compatibility diagnostic rather than attach new rules to old counters.
