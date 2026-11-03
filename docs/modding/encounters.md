# Semantic encounter documents

[Documentation](../README.md) / Modding

Classic native Memoria packages can contribute UTF-8 JSON files beneath `gameplay/encounters/`. Each file is an independent document. Two packages may both contain `gameplay/encounters/encounter.json`; both contributions are retained. Package discovery, enablement, and edition compatibility use the existing Mods flow. Native encounter documents do not add Remastered support.

The runtime prepares these documents through the normal resource-generation publication boundary and captures their compiled definitions when a battle starts. The supplied [Edea — Mirrors package](../../Mods/Memoria/EdeaMirrors/README.md) uses the same format in [its shipped payload](../../Mods/Memoria/EdeaMirrors/gameplay/encounters/edea-disc1.json). Its supported binding is `ff8.disc1.edea` with `sourceProfile: "Classic"`; later Edea encounters do not match. Selected native resources must satisfy the binding and presentation contracts. Preparation and start diagnostics report incompatible resources or an unavailable accepted definition.

## Document identity and selection

Every document requires `schemaVersion: 1`, a globally stable `documentId`, an `encounter` semantic selector, `atomic: true`, and a nonempty `operations` array. Optional fields are `title`, `sourceProfile`, `requiredActorCapacity`, `placements`, and descriptive `metadata`. Unknown executable properties, duplicate JSON properties, unsupported versions, and duplicate document or operation IDs are errors. Use `metadata` for descriptive extensions.

An encounter alias must resolve to a verified selected-resource baseline. It is not a model name or a wildcard for every battle using that model. `sourceProfile`, when supplied, must match the binding's compatibility identity. Semantic changes compose over selected decoded resources, including compatible binary replacements. Unsupported profiles and missing anchors fail preparation.

`requiredActorCapacity` ranges from 1 to 4. These are simultaneously active enemy slots, separate from the eight source formation records. A summon reserves every required slot before publication and never evicts another active actor. Each placement is `{ "id": "left", "x": -300, "y": 0, "z": 0 }`, with finite bounded coordinates interpreted by the encounter binding. Authors must validate positions against the intended stage.

Created actor, group, policy, rule, route, presentation, and placement names belong to the declaring document. Native baseline actors such as `original` come from the verified binding. Array indices, participant slots, localized text, and executable addresses are not author identities.

## Granular changes and dependencies

This independent compatibility example increases the Disc 1 original's maximum HP without replacing its resource or AI. The 25% increase is separate from the shipped showcase balance:

```json
{
  "schemaVersion": 1,
  "documentId": "example.edea-hp",
  "encounter": "ff8.disc1.edea",
  "sourceProfile": "Classic",
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

The multiplier is finite and bounded to 0.01–100. The runtime applies it once to the original's level-resolved baseline maximum HP before initializing current HP; copies inherit the final resolved maximum. Composition stores the semantic multiplier rather than flattening an enemy resource. The product rounds down after multiplication and clamps the result to 1–2,147,483,647. The original multiplier is retained so restoring a resolved native level reproduces the same maximum without multiplying twice. A template can use the same guarded property: its factor scales the inherited composed original maximum once at spawn, before initializing the copy at full HP. The default template factor is 1. Copies retain their already resolved maximum in the allocation snapshot, so restoration does not apply either factor again. For a baseline maximum of 1,000, an original factor of 1.25 and template factor of 1.5 produce a copy maximum of 1,875 even if the original is wounded. Each stage uses the rounding and bounds above.

| Operation | Authored responsibility |
| --- | --- |
| `AddActorTemplate` | Define a fresh full-HP, zero-ATB summoned actor inheriting resolved level/stats and an existing model. Declare its policy, group, arrival, ordinary summoned lifecycle, and no rewards. |
| `EnsureGroup` | Add explicit named members while retaining unrelated members. |
| `AddActionPolicy` / `AddRule` | Define named normal-action rules. `AddRule` carries a `ruleDefinition` object. |
| `SetRuleProperty` / `RemoveRule` | Change or remove one rule by stable policy/rule IDs. |
| `SetNormalActionPolicy` | Assign normal-action responsibility with an expected owner and preserved native lifecycle. |
| `SetActorProperty` | Guard and change `stats.maximumHpMultiplier` or `statusResistances.reflectMultiplier`. |
| `SetCameraProperty` | Guard and change battle scene `elevationDegrees` in [-45, 45]. |
| `AddDamageRoute` / `SetDamageRouteProperty` / `RemoveDamageRoute` | Add typed positive-HP damage routing, edit recipients, or remove an owned route. |
| `AddPresentation` / `SetPresentationProperty` | Define a finite clip/opacity track or guard an endpoint edit. |
| `GateNativeEvent` | Gate the bound native taunt channel by own-turn count while preserving native message order. |

Editable rule properties are `action.spell`, `action.spells`, `action.spellWeights.<canonicalSpell>`, `when.ownTurn`, `when.ownTurnAtLeast`, `when.ownTurnAtMost`, and `priority`. A guard uses `expectedValue`; the desired value uses `value`. Removing a rule or route requires its full definition as `expectedValue` and an explicit producer. Presentation edits support `startFrame`, `endFrame`, `opacity.from`, and `opacity.to`.

An optional `action.spellWeights` object assigns each pool spell an integer weight from 1 to 255. Omitted spells have weight 1; the complete pool total must not exceed 256. For example, an action containing `"spells": ["Firaga", "Blizzaga", "Thundaga"]` and `"spellWeights": { "Firaga": 3 }` uses weights 3:1:1. Ordinary random pools and Scan-uninformed fallback use these weights. Ties among known strongest elemental pairs and explicit all-Reflect bounce selection remain uniform.

For intentional refinement, identify the actual producer:

```json
{
  "id": "change-support-spell",
  "kind": "SetRuleProperty",
  "producer": {
    "documentId": "memoria.edea-mirrors.disc1",
    "operationId": "define-support-policy"
  },
  "policy": "support-policy",
  "rule": "regen",
  "property": "action.spell",
  "expectedValue": "Regen",
  "value": "Haste"
}
```

To edit only the showcase's Fire weight, use the same producer with `"rule": "offense"`, `"property": "action.spellWeights.Firaga"`, `"expectedValue": 1`, and `"value": 3`. Distinct spell weights are independent properties. A further override must reference the actual previous writer and guard its value; the entity keeps its original document scope.

Producer identities refer to successful entity creation. An unrelated entity with the same local name cannot satisfy a failed producer. Missing producers and dependency cycles reject composition. Stable topological ordering schedules producers before consumers; among ready contributions it retains committed catalog/document order. Files within a package use catalog path order. This affects encounter composition only, and does not reorder the common catalog or other mod formats.

Disjoint property edits compose. Identical ensures may coalesce without fabricating producer ownership. Incompatible writes, missing guards, competing exclusive policies, overlapping rules without distinct explicit priorities, incompatible route ownership, and insufficient capacity reject the candidate atomically. Diagnostics retain package, payload, document, operation, encounter, and relevant conflicting ownership context.

## Actions, damage, and presentation

Rules use positive own-turn equality or inclusive ranges. Overlapping ranges require distinct explicit priorities. Actor counters are independent; they do not use a global battle turn or an eight-bit opening counter.

Canonical spell names are English semantic identities rather than localized display labels. Accepted aliases are `Blizaga` → `Blizzaga`, `Darkness` → `Blind`, and `Reflection` → `Reflect`. Actions, pool entries, weight keys, per-spell property paths and guards normalize before validation and conflict ownership; duplicate aliases for one pool/weight identity are rejected. `Cast`, `CastFromPool` and atomic `SummonGroup` actions compile before battle construction. `casts: "nativeDouble"` uses the actor's current native Double or Triple state for up to two or three casts and one admitted main turn, without player stock or passive requirements. Template instances also consult their living, active source actor and use the stronger state. Single-target and authored group spells prefer distinct eligible recipients, then repeat within the same intended side or named group when distinct targets run out; native all-target spells retain their native cardinality. Fewer available allies never turn a buff into an opposing-party cast, and fewer opponents never turn an attack or debuff into an allied cast. Deliberate delivery through an actually reflected ally remains an explicit reflection policy, not a target-shortage fallback. Scan prefers unobserved opponents, and status spells prefer recipients missing the status. Allocation-owned target history survives snapshots and favors different recipients on later main turns; older snapshots begin with empty history. Native status admission, interruption and reflection still apply.

Successful enemy-side Scan records observed affinities for that battle, observer side and target allocation. Player Scan history and another actor sharing a model do not grant knowledge. `scanKnownWeakestElseRandom` selects the highest eligible known elemental multiplier, with uniform deterministic ties. Unknown, resistant, immune and absorbing candidates supply no invented weakness; when no eligible known pair remains, selection falls back to the declared pool and living targets.

`statusResistances.reflectMultiplier` scales the selected native Reflect resistance byte at encounter initialization. Its guard baseline is 1; values range from 0 through 100, with truncation and byte saturation. Zero removes resistance without forcing the Reflect status itself or changing other resistance entries. Fresh templates inherit the source's modified resistance; a template may apply its own multiplier at allocation. Original installed resources remain unchanged.

The Scan pair selector requires single-element, single-target native spells. Native all-target spells such as Quake must retain `nativeOpponentTargeting`; compilation rejects using them as single-target weakness pairs.

The optional reflection policy first prefers unreflected opponents. If every opponent reflects, it selects a living reflected ally and a reflectable spell uniformly; the native bounce chooses the final recipient. Without an allied reflector, it spends the main action on Dispel against a random reflected opponent. It does not append an offensive cast to that Dispel action.

The supported redirect contract uses living activated recipients and already resolved positive numeric HP damage. With two mirrors, each receives `floor(damage / 2)` and the original receives zero; damage 101 produces 50/50, and damage 1 produces 0/0. One surviving mirror receives the full amount. With no recipient, the original takes normal damage. Remainders and overkill are discarded, and recipient mitigation does not run twice. Direct hits against a mirror still use that mirror's own defenses. Healing, HP assignment/cost, absorption healing and instant-KO status retain their native semantics. Routing participates in the native HP/contact transaction, damage feedback, reactions and drain attribution.

Presentation frames use `frameNumbering: "zeroBased"`, a finite `startFrame`/`endFrame`, `loop: false`, `after: "nativeIdle"`, and linear opacity endpoints in [0, 1]. The shipped arrival uses clip `002`, source frames **8–28**, corresponding to native pose counters **9–29**. Opacity is 0 at frame 8 and 1 at frame 28, interpolated in source time. Clip IDs remain asset identities and are checked against selected resources. Both summoned actors remain outside scheduling and targeting until their atomic arrival completes.

## Edea showcase and native story ownership

Each actor has an independent own-turn cursor. The first four main actions are:

| Actor | Turn 1 | Turn 2 | Turn 3 | Turn 4 | From turn 5 |
| --- | --- | --- | --- | --- | --- |
| Original | Summon both mirrors | Triple on self | Scan a random opponent | Haste a random ally | Drain, Quake or Tornado with native Double/Triple |
| Support mirror | Protect the family | Shell the family | Regen the family | Reflect the family | Firaga, Blizzaga or Thundaga using Scan knowledge |
| Assault mirror | Blind opponents | Silence opponents | Confuse opponents | Slow opponents | Aero, Water or Bio using Scan knowledge and the reflection policy |

Mirrors inherit the original's resolved level, maximum HP, model and draw definitions, start at full HP with zero ATB, and receive fresh transient state plus their resource's innate statuses. They retain separate placement, targeting, animation and action cursors. Their ordinary summoned lifecycle grants no boss rewards, Mug loot or ownership of the original's story exit.

The original retains its native lifecycle. Native sequences **13/14** are the opening/party taunt messages: the package delays those channels until the original's fifth admitted main turn, preserves eligibility and order, and releases at most one queued line per turn. A deferred party taunt still consumes its non-attacking native callback. Sequences **15/17** contain native terminal/story control and remain outside the taunt gate. The original's native low-HP/party and defeat conditions can therefore end the encounter before later authored phases; waiting for every opening or taunt is not a victory requirement. If the added actors exhaust the party before the original's next callback, this optional encounter admits the genuine terminal sequence 15 at that boundary. Only its actual completed terminal witness permits the native story victory with an incapacitated party; ordinary encounters retain their defeat rules. Terminal cleanup removes the summoned actors while the original owns the single native result and story-return transaction.

## Enablement and refresh

The supplied package lives at `Mods/Memoria/EdeaMirrors/` and ships without an explicit `enabled` setting. It inherits the Mods menu's global automatic enablement policy until the player explicitly enables or disables the package. Saved user choices survive package updates. Refresh after editing and inspect the package's diagnostics. Enter or restart the intended encounter after a successful publication; active captured encounters retain their accepted definitions.

Preparation reads all documents and composes a complete candidate generation. Publication updates resource selection and encounter definitions at the existing commit boundary. A malformed or incompatible refresh retains the previous accepted generation. Disabling a package rebuilds the next encounter from the selected baseline and remaining enabled documents. Saves requiring an unavailable definition identity must fail with a compatibility diagnostic rather than attach new rules to old counters.

Schema validation is performed by the strict parser and semantic composer, including operation-specific fields, canonical identities, producer guards and effective-rule checks. No separate external JSON Schema file is required. The Classic showcase was also checked through real field entry, CPU/GPU arrival frames, each actor's first six actions, linked damage and its native story terminal through the movie and following field load. A constructed fourth actor separately exercises installed native counter completion, snapshot restore, heap retirement and slot reuse; it is not an original four-enemy story formation.

## Battle camera elevation

`SetCameraProperty` adjusts the composed encounter's battle scene view without changing installed camera data:

```json
{
  "id": "raise-camera",
  "kind": "SetCameraProperty",
  "property": "elevationDegrees",
  "expectedValue": 0,
  "value": 12
}
```

`elevationDegrees` is a finite number between -45 and 45, with an unmodified baseline of 0. Positive values raise the scene view. Native effect cameras retain their tracks and lens; the authored elevation resumes after each effect completes. Encounters with an authored camera also keep the caster and all loaded spell recipients inside the stage viewport once the effect begins, pulling the presentation camera back only when their actual body bounds require it. The initial caster close-up and Scan's per-target modal viewer retain their native presentation. Omission preserves the normal view; an explicit zero uses zero elevation while enabling recipient framing. The shipped Edea package starts at 12 degrees.

The camera is one encounter-wide semantic target. Independent documents writing its elevation conflict unless the later operation references the actual previous writer through `producer` and guards its current value with `expectedValue`. For example, a refinement of the shipped package uses `producer: { "documentId": "memoria.edea-mirrors.disc1", "operationId": "raise-camera" }`, `expectedValue: 12`, and the desired `value`. Camera operations retain package/document/operation provenance and reject the entire candidate on a conflict or failed guard. The compiled setting is captured with the active battle, so changing enabled packages affects the next battle or explicit restart.
