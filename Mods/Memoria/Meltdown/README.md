# Meltdown

Changes the existing Meltdown spell: its successfully applied Meltdown status reduces effective Vitality by 50% and Spirit by 25% by default. Adjust these independently from 0% (no reduction) to 100% (original zero defense) in **Mods > Meltdown > Settings**. The package JSON declares each option's label, description, control, unit, limits, step and default; the engine accepts only the recognized percentage parameters. Damage, status resistance, targeting and reflection keep their ordinary rules.

Options apply to future Meltdown statuses in new battles. An already applied status retains its recorded percentages until cured or the battle ends. Older saves without this optional status detail retain their original full reduction. Previously recorded single percentages still apply to both stats. Other sources of Vit 0 keep their behavior. Repeated application never compounds the reduction. Each effective stat is `floor(currentStat * (100 - reductionPercent) / 100)`, after ordinary stat calculation and other resource mods.

The bundled `gameplay/behaviors.json` declares `"StatusName_{LANG}": "${LANG}_kernel_magicspells_name_0049"` inside `meltdown`. While active, the status name follows the existing Meltdown spell-name localization entry for the source resource language, with the selected translation applied to that entry. For example, English game resources with a Russian translation still resolve `$en_kernel_magicspells_name_0049`. This presentation declaration is not a slider and cannot be overridden in `config.json`. Older packages without it retain the native status name.

A package may instead declare an explicit binding such as `"StatusName_EN": "$en_kernel_magicspells_name_0049"`. Language suffixes are case insensitive; duplicate aliases are rejected. An explicit language binding takes precedence over `StatusName_{LANG}`, which takes precedence over the legacy unqualified `StatusName`. `${LANG}` in a reference resolves to the source resource language. Explicit bindings must use a matching absolute language prefix or `${LANG}`. A binding for another source language does not claim a status name or require that foreign entry to exist. Different languages may reference different entries; no shared entry index is assumed. Legacy `StatusName` absolute references keep their original meaning and must exist in the active source namespace.

The bundled `gameplay/behaviors.json` uses schema version 2, with descriptors for `meltdown.VitalityReductionPercent` and `meltdown.SpiritReductionPercent`. Both use the `slider` control, `%` unit, integer limits 0 through 100 and step 1. A descriptor has this form:

```json
{
  "label": "Vitality reduction",
  "description": "Percentage by which Meltdown reduces effective Vitality after successfully applying Meltdown.",
  "control": "slider",
  "unit": "%",
  "min": 0,
  "max": 100,
  "step": 1,
  "default": 50
}
```

The Mods menu writes only value overrides to the writable package's `config.json`, for example:

```json
{
  "meltdown": {
    "VitalityReductionPercent": 65,
    "SpiritReductionPercent": 35
  }
}
```

Restoring an option to its package default removes that override. Legacy schema version 1 packages and `defenseReductionPercent` configuration are read as the same value for both stats. Editing an option writes the separate keys and preserves the other option's saved value.

In Hardcore mode Meltdown is always active with Vitality reduction 50% and Spirit reduction 25%; its Settings controls are locked. Loading a non-Hardcore game restores the user's activation and option preferences. Outside Hardcore, activation follows the normal Mods policy and Enable / Disable control. No game resource or kernel file is replaced. A second active package claiming the same behavior parameter is rejected with diagnostics for both owners, preserving the previous committed configuration. Packages declaring different parameters can coexist; `StatusName` has its own owner for the active source language and conflicts independently of the numeric parameters. Selected explicit and generic bindings in different active packages conflict, while declarations for different source languages can coexist. Built-in list ordering is presentation only and does not change resource precedence.
