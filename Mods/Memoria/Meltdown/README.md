# Meltdown

Changes the existing Meltdown spell: its successfully applied Vit 0 reduces effective Vitality and Spirit by 50% by default. Set the reduction from 0% (no reduction) to 100% (original zero defense) in **Mods ? Meltdown ? Settings**. Damage, status resistance, targeting and reflection keep their ordinary rules. The spell remains the original ability.

The setting applies to future Meltdown statuses in new battles. An already applied status retains its recorded percentage until cured or the battle ends. Older saves without this optional status detail retain their original full reduction. Other sources of Vit 0 keep their behavior. Repeated application never compounds the reduction. Each effective stat is `floor(currentStat * (100 - reductionPercent) / 100)`, after ordinary stat calculation and other resource mods.

The bundled default is `gameplay/behaviors.json`:

```json
{
  "schemaVersion": 1,
  "meltdown": {
    "defenseReductionPercent": 50
  }
}
```

The Mods menu writes only overrides to the writable package's `config.json`, for example:

```json
{
  "meltdown": {
    "defenseReductionPercent": 65
  }
}
```

Removing an override restores the package default. Activation follows the normal Mods policy and Enable / Disable control. No game resource or kernel file is replaced. A second active package claiming the same behavior parameter is rejected with diagnostics for both owners, preserving the previous committed configuration. Built-in list ordering is presentation only and does not change resource precedence.
