namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public byte? NativeEntryAdvantage { get; private set; }
    public ushort ResolvedNativeFlags => definition.NativeEntry is { } entry ? NativeBattleEntryRules.MergeFlags(definition.NativeFlags, entry.FormationFlags) : definition.NativeFlags;

    private void InitializeNativeEntry()
    {
        if (definition.NativeEntry is not { } entry)
        {
            return;
        }
        uint battleFlags = (uint)ResolvedNativeFlags << 16 | (ushort)(definition.EncounterId ?? 0);
        if (UsesExpandedRoster)
        {
            NativeBattleEntryEnemy[] activeEnemies = enemies.Where(actor => actor.IsActive).Select(actor => new NativeBattleEntryEnemy(NativeSavedStatusBits(actor), actor.SourceDefinition.Information254 ?? actor.SourceDefinition.NativeTemplate?.Information254)).ToArray();
            NativeEntryAdvantage = NativeBattleEntryRules.ResolveExpanded(battleFlags, entry.EncounterAbilityFlags, activeEnemies, ref random);
        }
        else
        {
            NativeBattleEntryEnemy[] enemySlots = new NativeBattleEntryEnemy[NativeBattleSlots.EnemyCount];
            for (int slot = 0; slot < enemySlots.Length; slot++)
            {
                BattleCombatant? actor = FindAllocatedNativeActor(slot + NativeBattleSlots.AllyCount);
                //48C5C0 marks every unallocated physical slot KO; no DAT pointer is read for it.
                enemySlots[slot] = actor is null ? new(1, null) : new(NativeSavedStatusBits(actor), actor.SourceDefinition.NativeTemplate?.Information254);
            }
            NativeEntryAdvantage = NativeBattleEntryRules.Resolve(battleFlags, entry.EncounterAbilityFlags, enemySlots, ref random);
        }
        foreach (BattleCombatant actor in combatants.Where(actor => UsesExpandedRoster ? actor.IsActive : actor.NativeActorIndex is >= 0 and < NativeBattleSlots.Count))
        {
            ushort saved = NativeSavedStatusBits(actor);
            if (NativeEntryAdvantage is 1 or 2)
            {
                if (actor.IsPlayer)
                {
                    uint passive = actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? throw new NotSupportedException("Native enemy advantage requires the party's actual saved passive flags.");
                    if ((passive & 0x10000) == 0)
                    {
                        actor.Atb = 0;
                    }
                }
                else
                {
                    actor.Atb = (saved & 5) == 0 ? AtbMaximum : 0;
                }
            }
            else if (NativeEntryAdvantage is 3 or 4)
            {
                actor.Atb = actor.IsPlayer && (saved & 5) == 0 ? AtbMaximum : 0;
            }
            if ((saved & 1) == 0 && (NativeEntryAdvantage == 2 && actor.IsPlayer || NativeEntryAdvantage == 4 && !actor.IsPlayer))
            {
                actor.NativeStatus1 |= 0x800000;
            }
        }
    }

    private void RestoreNativeEntry(BattleSnapshot snapshot)
    {
        if ((snapshot.NativeEntryAdvantage is null) != (definition.NativeEntry is null) || snapshot.NativeEntryAdvantage > 4)
        {
            throw new InvalidDataException("Native encounter advantage is missing or outside its original BYTE0..4 contract.");
        }
        NativeEntryAdvantage = snapshot.NativeEntryAdvantage;
    }
}
