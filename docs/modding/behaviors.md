# Existing ability behavior mods

[Documentation](../README.md) / Modding

Behavior payloads modify recognized properties of existing abilities. They do not define new abilities or execute arbitrary patches. The engine owns each parameter's integer contract and hard limits. Schema version 2 describes the menu controls in the package itself: label, description, control, unit, minimum, maximum, step and default.

A package's `gameplay/behaviors.json` declares defaults and UI metadata:

```json
{
  "schemaVersion": 2,
  "meltdown": {
    "StatusName_{LANG}": "${LANG}_kernel_magicspells_name_0049",
    "VitalityReductionPercent": {
      "label": "Vitality reduction",
      "description": "Percentage by which Meltdown reduces effective Vitality after successfully applying Meltdown.",
      "control": "slider",
      "unit": "%",
      "min": 0,
      "max": 100,
      "step": 1,
      "default": 50
    },
    "SpiritReductionPercent": {
      "label": "Spirit reduction",
      "description": "Percentage by which Meltdown reduces effective Spirit after successfully applying Meltdown.",
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

The percentages attach only when the existing Meltdown spell successfully applies the absent Meltdown status (the native Vit 0 bit), for enemies and players alike. Other Vit 0 producers retain their native behavior. Reapplication does not stack or replace a live effect; curing the status clears its percentages. Settings apply to new battles. Applied percentages are saved facts and survive reload. Older saves with a single percentage restore it to both stats, and saves without percentage data retain the native full reduction. Legacy schema1 payloads and scalar `defenseReductionPercent` configs remain readable; saving an option writes the separate current keys while preserving the other choice.

In **Hardcore**, the installed Meltdown package is required and uses fixed reductions of **50% Vitality and 25% Spirit**. Enable/Disable, both sliders and Save are locked with an explanation. The backend rejects disable/option writes too; global auto-enable settings and saved `enabled = 0` do not bypass the requirement. Ordinary activation and option files are preserved. Loading a non-Hardcore game or changing the Title profile restores those ordinary preferences. New Game, save reconstruction and a transition to Hardcore require successful publication of the requested mod profile; pending or conflicting changes cannot silently leave the ordinary profile in use. Deleting the package itself is not prevented or repaired.

Status-name declarations redirect the existing status localization identity to another existing kernel or authored interface key. The bundled declaration uses `"StatusName_{LANG}": "${LANG}_kernel_magicspells_name_0049"`, the Meltdown spell-name entry in the shared kernel schema. `{LANG}` means the original-resource language, independently of the selected translation's output language. English resources with a Russian translation still use `$en_kernel_magicspells_name_0049`; French resources use `$fr_kernel_magicspells_name_0049`. The engine does not create a new `$status` namespace or store a fixed English display name.

A package may provide explicit bindings when languages need different entries:

```json
{
  "schemaVersion": 2,
  "meltdown": {
    "StatusName_EN": "$en_kernel_magicspells_name_0049",
    "StatusName_FR": "$fr_kernel_magicspells_name_0049",
    "StatusName_{LANG}": "${LANG}_kernel_magicspells_name_0049"
  }
}
```

Within one package, the explicit source-language binding takes precedence over `StatusName_{LANG}`, then the legacy unqualified `StatusName`. An explicit binding may use a matching absolute prefix or `${LANG}`. An unqualified absolute key stays pinned to its declared language; it is never silently rewritten. Supported source languages are `en`, `fr`, `de`, `es`, `it` and `jp`, with `ja` accepted as an alias of `jp`. Suffixes and absolute language prefixes are case insensitive; duplicate canonical bindings and unknown placeholders are rejected. The remaining key identity keeps its case. Only the selected binding must exist in the active resource namespace, but all declarations must be syntactically valid.

The native fallback is read from the selected kernel, including accepted resource replacements. Active translations are resolved when displaying the name, so localization changes take effect without altering a save. Omitting an applicable binding or disabling its owner restores the original status identity. Unknown targets and cycles reject the candidate change and preserve the committed name. The localization workbench sees the resolved target spell-name identity. Name declarations belong in the payload and cannot be overridden in `config.json`.

Each numeric behavior parameter and the selected status-name binding has one active package owner. Two packages claiming the same parameter cause diagnostics naming both owners; the candidate configuration is rejected and the committed configuration remains active. Explicit and generic status bindings in different packages conflict if both apply to the current source language; specificity never overrides another package. Packages providing names only for different source languages can coexist. Packages declaring different numeric parameters can also coexist. No hidden load-order winner or multiplication is used. A config may override only its own package's declared numeric parameters. Unrelated packages' `config.json` files are left alone.

Built-in Memoria packages appear first with the silver Griever crest used for Memoria saves. This presentation order does not alter resource precedence. Bundled defaults live in `Data/BundledMods/Memoria`; writable settings stay under `Mods/Memoria` and survive program updates. Source builds use the same separation.
