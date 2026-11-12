# FFVII enemy extension documents

Native Memoria packages may contribute UTF-8 JSON beneath `ff7/gameplay/enemy-extensions/`. This namespace is specific to original FFVII encounters. It is separate from the FFVIII semantic encounter format.

A document declares `schemaVersion: 1`, a unique `documentId`, and an `enemies` array. Each entry identifies an `enemyId` and supplies only the properties that it changes. Do not copy native levels, HP, attacks, item probabilities, or entire source records into an extension.

```json
{
  "schemaVersion": 1,
  "documentId": "example.ff7.inspection",
  "enemies": [
    {
      "enemyId": 105,
      "scanDescription": "A small, furious ember has taken on a life of its own. It swells with the conviction that every insult deserves a spectacular reply."
    }
  ]
}
```

This Scan-only contribution does not promote the target's level or alter its statistics. It illustrates the format; installing it alongside another contribution to the same Scan property produces a conflict diagnostic.

## Changed properties

| Property | Meaning |
| --- | --- |
| `scanDescription` | Authored English prose displayed independently of name and statistics. |
| `growth` | Changed multiplier rows for `hp`, `attack`, `magic`, or `mp`; omit unchanged statistics. |
| `rewardGrowth` | Separate native-unit FFVII multiplier rows for `exp`, `ap`, and `gil`. These are not FFVIII EXP or GF AP amounts. |
| `drawTiers` | Increasing `minimumDelta` boundaries, each with at most four distinct `{magicId,resistance}` slots. IDs refer to stocked FFVIII magic, not FFVII attacks or GF identities. |
| `itemReplacements` | Explicit `{minimumDelta,channel,fromItemId,toItemId}` replacements; the bundled content uses only native `drop` and `steal` channels. |

Growth coefficients are four signed integers with a fixed scale of 1,000,000, in `[A,B,C,D]` order. The initial coefficient is 1,000,000 and the remaining coefficients are nonnegative. For `t=(L-L0)/max(1,100-L0)`, the multiplier is `A+B*t+C*t*t+D*t*t*t`, divided by the coefficient scale. The implementation uses checked wide arithmetic and rounds down. At `L<=L0`, it returns the unchanged native anchor. Constant rows need not be declared.

The anchor comes from the selected, resolved native data, including compatible source changes; it is not a baseline copied into the mod. Defenses, speed, luck and other undeclared properties retain their native ownership. Authored Draw resistance is a slot modifier; the spell's kernel Draw resistance remains independently authoritative.

An item replacement starts only at a positive level delta. It selects an existing native item identity in the stated channel and retains the native slot's rate and ordering. Where several tiers replace the same source item, the highest eligible threshold is resolved against the original selected slot. It is not a chain of repeated inventory grants. The bundled package does not add Morph commands or replace protected progression items.

## Composition

The catalog composes disjoint changes to the same enemy ID. A Scan contribution can coexist with a separate HP contribution; different growth or reward row keys can coexist. Two writers of the same description, Draw table, growth row, reward row, or item selector/threshold fail with a diagnostic identifying the documents. Package order does not silently choose a winner.

Unknown executable properties, duplicate JSON properties, duplicate document or enemy IDs, unsupported coefficient rows, invalid magic identities and unordered Draw boundaries fail preparation. A failed candidate is not partially applied. Resolved encounter state is captured once, separately from the immutable source records and the authored declarations.

The built-in [FFVII Enemy Integration package](../../Mods/Memoria/FF7EnemyIntegration/README.md) is a complete example of the declaration structure. Its activation preference and the existing source-data compatibility checks remain host responsibilities.
