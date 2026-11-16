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

Open **Mods > Memoria** and expand **Battle** to enable Meltdown and adjust both percentages. Changes are saved and applied as you edit. Back returns to the Mods catalog. User-installed behavior packages expose their own settings through the Mods catalog. Both parameters have engine hard limits of 0 through 100. A descriptor can narrow the range and choose an integer step. `slider` and `%` are the supported control and unit for percentage settings. Nonempty labels and descriptions, ordered bounds, and aligned defaults and overrides are validated before publication. Unknown or duplicate fields, fractional values and unsupported controls are rejected.

The shipped defaults reduce Vitality by 50% and Spirit by 25%. Each current resolved stat uses `floor(stat * (100 - reductionPercent) / 100)`. The two stats are independent: zero keeps that stat unchanged, and 100 reduces it to zero. Existing stat/resource modifications therefore compose before the reduction. Physical damage uses the Vitality percentage; magical damage and magical status resistance use the Spirit percentage. Smart Attack forecasts use the same rules and give no status priority when neither stat would benefit.

The percentages attach only when the existing Meltdown spell successfully applies the absent Meltdown status (the native Vit 0 bit), for enemies and players alike. Other Vit 0 producers retain their native behavior. Reapplication does not stack or replace a live effect; curing the status clears its percentages. Settings apply to new battles. Applied percentages are saved facts and survive reload. Older saves with a single percentage restore it to both stats, and saves without percentage data retain the native full reduction. Legacy schema1 payloads and scalar `defenseReductionPercent` configs remain readable; saving an option writes the separate current keys while preserving the other choice.

In **Hardcore**, Meltdown is required and uses fixed reductions of **50% Vitality and 25% Spirit**. The Meltdown toggle and both sliders display the enforced values and give the reason for the lock. You can save changes to the other unlocked components. Loading a non-Hardcore game or changing the Title profile restores the saved Meltdown preferences. New Game, save reconstruction and a transition to Hardcore require successful publication of the requested mod profile.

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

The built-in components appear under one **Memoria** entry with the silver Griever crest. Its settings group the components into collapsible categories. Bundled defaults live in `Data/BundledMods/Memoria` with the same resource paths and precedence. Choices saved through the Memoria settings live in `Mods/Memoria/memoria-settings.json` and take precedence over the corresponding older package preferences. Existing package preferences still supply values for settings you have yet to change in Memoria.

**Girl Next Door** is an optional official download. Open **Mods > Memoria > Settings** to download and enable it or change its existing setting. See [official downloads](../players/official-mods.md) for installation and compatibility. Opening the magazine from the FFVIII Item menu displays the cover at its original aspect ratio on a black background and preserves the item quantity. Cancel returns to inventory.

Under **FFVII**, the FFVII Enemy Integration toggle controls availability of the bundled enemy extensions. The campaign's **Automatic** preference requires the extensions after a transition from FFVIII and locks this component for that segment. Outside that rule, the saved component toggle and campaign preference determine availability for the next encounter.

## Level-Based Stat Bonuses

**Level-Based Stat Bonuses** is disabled by default and required while Hardcore is active. Its toggle in **Memoria > Battle** is locked in Hardcore. Leaving Hardcore restores the saved activation preference. The engine recognizes the active bundled package and applies `GameplayTuning.LevelBasedStatBonuses`.

While equipped, HP Bonus contributes `min(2970, 30 * (level - 1))` HP; Str, Vit, Mag and Spr Bonus each contribute `min(99, level - 1)` to their respective stat. Unequipping removes this contribution. Accumulated permanent Bonus growth is ignored while active. Level-ups continue recording permanent Bonus growth, which becomes effective again when the mod is disabled. Stat gains from items and Devour remain. There are no added Speed or Luck Bonus abilities. Disabling the package restores the ordinary permanent-growth rule and any recorded historical Bonus growth.

The Mods details description includes built-in English, Russian, Japanese, German, French, Italian and Spanish text selected by the active interface language. Translation packages can override `$en_interface_system_mods.level-based-stat-bonuses.description` through the ordinary interface localization contract.

See [Permanent stat bonus sources](stat-bonuses.md) for save compatibility, source groups and original-save export.
