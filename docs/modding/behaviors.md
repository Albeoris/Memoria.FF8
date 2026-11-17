# Status mods and Memoria settings

[Documentation](../README.md) / Modding

## Define a status override

Create a native mod with a root `mod.json` and one JSON document per status under `statuses/`. For example, the bundled Meltdown mod has these two files.

```text
Meltdown/
  mod.json
  statuses/
    vit0.json
```

`mod.json` defines the mod's identity and menu grouping.

```json
{
  "id": "memoria-meltdown",
  "name": "Meltdown",
  "package": "Memoria",
  "description": {
    "en": "Adjusts the Vitality and Spirit reductions from Vit 0."
  }
}
```

Use a globally unique lowercase slug for `id`. Keep it when changing the display name, settings group or folder. The loader discovers `statuses/**/*.json` recursively. Each document's `id` identifies the native status, so moving or renaming its file preserves the corresponding settings. Bundled and downloaded mods use the same descriptor and payload format.

```json
{
  "id": "Vit0",
  "name": "${LANG}_kernel_magicspells_name_0049",
  "statModifiers": {
    "Vitality": {
      "operation": "ReducePercent",
      "value": {
        "default": 50,
        "description": {
          "en": "Reduces Vitality while this status is active.",
          "ru": "Снижает физическую защиту, пока действует статус."
        }
      }
    },
    "Spirit": {
      "operation": "ReducePercent",
      "value": { "default": 25 }
    }
  }
}
```

The `ReducePercent` operation takes an integer from 0 through 100. It computes `floor(stat * (100 - percent) / 100)` from the current effective value at the status contribution stage. Zero preserves the incoming value and 100 reduces it to zero. Values above 100 would produce negative stats. Native Scan stores these effective stats in unsigned bytes, and the crossover damage bridge uses unsigned defenses.

A numeric `value`, such as `50`, sets a fixed reduction. An object with `default` generates a slider. Optional `min` and `max` narrow the operation's range. They must satisfy `0 <= min <= default <= max <= 100`, and the stored value must lie in the same inclusive range. The control steps by one percentage point. An optional `label` overrides the stat's localized label. An optional `description` adds gray help text below the slider. These fields and the descriptor's description use language dictionaries with a required English entry. The active localization target selects another entry when present. For example, a Russian entry appears when a Russian localization is active.

The current stat names are `Strength`, `Vitality` and `Spirit`. An override for another status uses the same format. For example, `statuses/poison.json` can add a fixed reduction while Poison is active.

```json
{
  "id": "Poison",
  "statModifiers": {
    "Strength": { "operation": "ReducePercent", "value": 20 }
  }
}
```

Status and stat IDs and operation names are case-sensitive. The accepted status names are `Ko`, `Poison`, `Petrify`, `Blind`, `Silence`, `Berserk`, `Zombie`, `Sleep`, `Haste`, `Slow`, `Stop`, `Regen`, `Protect`, `Shell`, `Reflect`, `Aura`, `Curse`, `Doom`, `Invincible`, `Petrifying`, `Float`, `Confuse`, `Drain`, `Double`, `Triple`, `Vit0` and `AngelWing`. A status document changes the contributions of an existing status. The native battle rules still determine when that status can take effect.

## Priority, battle state and names

For each status and stat, ordinary mod priority selects one complete operation and value. A local mod can set `priority` in `mod.ini`. Higher numbers load first, with equal priorities sorted by mod ID. An omitted field inherits the lower-priority value, including native defaults. Native Vit 0 reduces Vitality and Spirit by 100%. Different active statuses can contribute to the same stat in native status-bit order. Reapplying a status does not compound its contribution. A cure removes it, and any source that sets the status uses the same contribution.

A battle captures its resolved rules at entry and keeps them over a save and restore. Editing or disabling a mod changes the next battle. Settings are keyed by mod ID, status ID and stat ID. A mod may provide one document for each status. Duplicate status identities within a mod, unknown IDs or fields, and invalid bounds reject the candidate publication. The last successful publication stays active.

The optional `name` refers to an existing localization key. In the example, `${LANG}` means the original-resource language. An English installation with an active Russian translation still uses the English kernel key for the Meltdown spell, and the localization layer supplies its Russian text. A literal source prefix such as `$en_` pins the key. Omitting `name` inherits the lower-priority or native label. Name references are display text and have no effect on status application or damage. Invalid or cyclic references reject the candidate publication.

In **Mods > Memoria > Settings**, expand **Meltdown** to adjust the bundled options. They default to a 50% Vitality reduction and a 25% Spirit reduction. Hardcore enables the mod and locks the options to its declared defaults. Leaving Hardcore restores the saved activation and option preferences.

## Other Memoria settings

The built-in components appear under one **Memoria** entry with the silver Griever crest. Its settings group the components into collapsible categories. Bundled defaults live in `Data/BundledMods/Memoria` with the same resource paths and precedence. Choices saved through the Memoria settings live in `Mods/Memoria/memoria-settings.json` and take precedence over the corresponding older package preferences. Existing package preferences still supply values for settings you have yet to change in Memoria.

**Girl Next Door** is an optional official download. Open **Mods > Memoria > Settings** to download and enable it or change its existing setting. See [official downloads](../players/official-mods.md) for installation and compatibility. See [native item overrides](item-overrides.md) to add image actions or change item types. Opening the magazine from the FFVIII Item menu displays the cover at its original aspect ratio on a black background and preserves the item quantity. Cancel returns to inventory.

Under **FFVII**, the FFVII Enemy Integration toggle controls availability of the bundled enemy extensions. The campaign's **Automatic** preference requires the extensions after a transition from FFVIII and locks this component for that segment. Outside that rule, the saved component toggle and campaign preference determine availability for the next encounter.

## Level-Based Stat Bonuses

**Level-Based Stat Bonuses** is disabled by default and required while Hardcore is active. Its toggle in **Memoria > Battle** is locked in Hardcore. Leaving Hardcore restores the saved activation preference. The engine recognizes the active bundled package and applies `GameplayTuning.LevelBasedStatBonuses`.

While equipped, HP Bonus contributes `min(2970, 30 * (level - 1))` HP; Str, Vit, Mag and Spr Bonus each contribute `min(99, level - 1)` to their respective stat. Unequipping removes this contribution. Accumulated permanent Bonus growth is ignored while active. Level-ups continue recording permanent Bonus growth, which becomes effective again when the mod is disabled. Stat gains from items and Devour remain. There are no added Speed or Luck Bonus abilities. Disabling the package restores the ordinary permanent-growth rule and any recorded historical Bonus growth.

The Mods details description includes built-in English, Russian, Japanese, German, French, Italian and Spanish text selected by the active interface language. Translation packages can override `$en_interface_system_mods.level-based-stat-bonuses.description` through the ordinary interface localization contract.

See [Permanent stat bonus sources](stat-bonuses.md) for save compatibility, source groups and original-save export.
