# Existing ability behavior mods

[Documentation](../README.md) / Modding

Behavior payloads modify recognized properties of existing abilities. They do not define new abilities or execute arbitrary patches. The engine owns each parameter's integer contract and hard limits. Schema version 2 describes the menu controls in the package itself: label, description, control, unit, minimum, maximum, step and default.

A package's `gameplay/behaviors.json` declares defaults and UI metadata:

```json
{
  "schemaVersion": 2,
  "meltdown": {
    "VitalityReductionPercent": {
      "label": "Vitality reduction",
      "description": "Percentage by which Meltdown reduces effective Vitality after successfully applying Vit 0.",
      "control": "slider",
      "unit": "%",
      "min": 0,
      "max": 100,
      "step": 1,
      "default": 50
    },
    "SpiritReductionPercent": {
      "label": "Spirit reduction",
      "description": "Percentage by which Meltdown reduces effective Spirit after successfully applying Vit 0.",
      "control": "slider",
      "unit": "%",
      "min": 0,
      "max": 100,
      "step": 1,
      "default": 25
    }
  }
}
```

The writable package's `config.json` contains only user value overrides:

```json
{
  "meltdown": {
    "VitalityReductionPercent": 65,
    "SpiritReductionPercent": 35
  }
}
```

**Mods > Meltdown > Settings** displays both descriptions and their range/step beside independent percentage sliders. Save applies the draft; Cancel discards it. Returning to a package default removes that override. Both known parameters have engine hard limits 0 through 100; a descriptor can narrow the range and choose an integer step. `slider` and `%` are the supported control/unit for this contract. Nonempty labels/descriptions, ordered bounds and aligned defaults/overrides are validated before publication. Unknown or duplicate fields, fractional values and unsupported controls are rejected.

The shipped defaults reduce Vitality by 50% and Spirit by 25%. Each current resolved stat uses `floor(stat * (100 - reductionPercent) / 100)`. The two stats are independent: zero keeps that stat unchanged, and 100 reduces it to zero. Existing stat/resource modifications therefore compose before the reduction. Physical damage uses the Vitality percentage; magical damage and magical status resistance use the Spirit percentage. Smart Attack forecasts use the same rules and give no status priority when neither stat would benefit.

The percentages attach only when the existing Meltdown spell successfully applies an absent Vit 0, for enemies and players alike. Other Vit 0 producers retain their native behavior. Reapplication does not stack or replace a live effect; curing the status clears its percentages. Settings apply to new battles. Applied percentages are saved facts and survive reload. Older saves with a single percentage restore it to both stats, and saves without percentage data retain the native full reduction. Legacy schema1 payloads and scalar `defenseReductionPercent` configs remain readable; saving an option writes the separate current keys while preserving the other choice.

In **Hardcore**, the installed Meltdown package is required and uses fixed reductions of **50% Vitality and 25% Spirit**. Enable/Disable, both sliders and Save are locked with an explanation. The backend rejects disable/option writes too; global auto-enable settings and saved `enabled = 0` do not bypass the requirement. Ordinary activation and option files are preserved. Loading a non-Hardcore game or changing the Title profile restores those ordinary preferences. New Game, save reconstruction and a transition to Hardcore require successful publication of the requested mod profile; pending or conflicting changes cannot silently leave the ordinary profile in use. Deleting the package itself is not prevented or repaired.

Each behavior parameter has one active package owner. Two packages declaring the same parameter cause diagnostics naming both owners; the candidate configuration is rejected and the committed configuration remains active. Packages declaring different parameters can coexist. No hidden load-order winner or multiplication is used. A config may override only its own package's declared parameters. Unrelated packages' `config.json` files are left alone.

Built-in Memoria packages appear first with the silver Griever crest used for Memoria saves. This presentation order does not alter resource precedence. Bundled defaults live in `Data/BundledMods/Memoria`; writable settings stay under `Mods/Memoria` and survive program updates. Source builds use the same separation.
