# Existing ability behavior mods

[Documentation](../README.md) / Modding

Memoria behavior payloads modify explicitly supported properties of existing abilities. They do not define new abilities, run patch bytes or replace an entire kernel resource. The engine owns the type, allowed range and UI for each parameter.

A package's `gameplay/behaviors.json` declares its defaults:

```json
{
  "schemaVersion": 1,
  "meltdown": {
    "defenseReductionPercent": 50
  }
}
```

The writable package's `config.json` contains only user overrides:

```json
{
  "meltdown": {
    "defenseReductionPercent": 65
  }
}
```

The bundled Meltdown package uses 50 by default. **Mods ? Meltdown ? Settings** exposes a percentage slider from 0 to 100 with a step of 1, and Save/Cancel. Zero leaves Vit/Spr unchanged; 100 retains the original full reduction. Resetting to a package default removes that parameter's override. Enablement remains the ordinary Mods preference, separate from the parameter document.

The reduction applies once to each current resolved stat: `floor(stat * (100 - percent) / 100)`. Thus it composes with resource/stat modifications without overwriting their values. Damage and status resistance use the same effective stats; Smart Attack forecasts do too. The percentage is attached only when the existing Meltdown spell successfully applies an absent Vit 0, for enemies and players alike. Other producers of Vit 0 retain their original effect. Repeated application does not stack or replace a live effect. Curing the status clears its percentage.

Settings changes follow normal battle tuning boundaries: they affect new battles. Already applied percentages are saved facts and survive reload, including after the mod is disabled. Old saves without a recorded percentage preserve the original full reduction. Damage, status application chance, targeting and reflection retain their ordinary contracts.

Each active behavior parameter has one package owner. Two active packages declaring `meltdown.defenseReductionPercent` cause diagnostics naming both owners; the candidate configuration is rejected and the previous committed configuration stays active. Disable one owner to resolve it. No silent load-order winner or multiplication is used. Unrelated mods, including resource replacements, remain independent. Unknown, duplicate, fractional or out-of-range fields fail validation. An override may only configure a parameter declared by its own package. A `config.json` in a package without this behavior payload is outside this format and is left alone.

Built-in Memoria packages appear first with the same silver Griever crest used for Memoria saves. That display grouping does not alter resource precedence. Bundled files remain in `Data/BundledMods/Memoria`; user settings are written under the writable `Mods/Memoria` overlay and survive replacing the program files. Source builds use the same separation.
