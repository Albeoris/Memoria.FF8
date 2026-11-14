# Permanent stat bonus sources

Character growth records retain HP and six statistics in the original order: Str, Vit, Mag, Spr, Spd and Luck. HP and stat bonuses remain separate from ordinary level curves, weapons, magic junctions and percentage abilities.

`BonusSources.Groups` stores permanent additions by a stable string identifier:

| Source | Meaning |
| --- | --- |
| `initial` | Bonus values supplied by New Game initialization |
| `level-up` | Growth earned with HP/Str/Vit/Mag/Spr Bonus equipped |
| `items` | Permanent stat items and the remainder of inferred legacy bonuses |
| `devour` | Permanent gains from Devour |

Additional identifiers are preserved and contribute as ordinary permanent growth. A group contains HP and all six statistics; groups have distinct identifiers and bounded nonnegative values. The original HP/byte fields are capped projections of all groups, validated against them. Source totals can exceed the original combined caps without losing provenance; export caps the combined values at 9999 HP and 255 per statistic.

## Existing saves

Missing sources are supported automatically. For an existing engine save, up to 99 points in each of Str/Vit/Mag/Spr are attributed to `level-up`; HP uses up to 2970. The remainder, including all Spd and Luck growth, is attributed to `items`. There are no original Spd Bonus or Luck Bonus abilities. This is an inference because the old record does not retain the actual source.

An original FFVIII save uses the same inference separately for each Bonus ability, but only when at least one acquired GF has learned that ability. Without a matching learned ability, the entire corresponding bonus is attributed to `items`. GF ownership by the character, current junctions and current GF HP do not affect this historical inference. Import starts with ordinary native rules; the active built-in package is applied by the game host afterwards.

New saves preserve their categories, so subsequent loads do not repeat this inference. Loading does not replay scripts, battles or rewards.

## Level-Based Stat Bonuses

The optional built-in package is off by default and required in Hardcore. It excludes the `level-up` group from all effective stats and substitutes a contribution from each currently equipped Bonus ability: `level - 1` for Str/Vit/Mag/Spr, or `30 * (level - 1)` for HP, capped at 99 or 2970 respectively. Duplicate equipped copies do not multiply a contribution. Bonuses enter the ordinary calculation before percentage abilities and final stat caps.

Level-ups continue recording their permanent additions while the package is active. Hidden level growth does not consume the room available for item or Devour growth. Disabling the package restores all accumulated permanent growth and removes the calculated equipped contribution. Preference changes during a battle wait until that battle ends.

Original-save export combines every permanent source, including hidden level growth. The calculated equipped contribution is not an additional permanent source and is not added again on export. Native saves cannot preserve the source breakdown; importing an exported native save applies the original-save inference again.
