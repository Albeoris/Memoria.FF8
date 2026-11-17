namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private const uint NativeEnemyRewardedFlag = 0x40000;
    private readonly List<BattleEnemyDefeat> enemyDefeats = [];
    private readonly List<BattleStock> nativeRewardItems = [];
    public IReadOnlyList<BattleStock> NativeRewardItems => nativeRewardItems.AsReadOnly();
    public IReadOnlyList<BattleEnemyDefeat> EnemyDefeats => enemyDefeats.AsReadOnly();

    private void ObserveEnemyDefeat(BattleCombatant? actor, BattleCombatant target)
    {
        BattleCombatantDefinition source = target.SourceDefinition;
        if (target.IsPlayer || source.Rewards is not { } rewards)
        {
            return;
        }
        int slot = source.RewardSlot ?? throw new InvalidDataException("Native enemy reward inputs require their actual formation slot.");
        if ((target.NativeCombatantFlags & NativeEnemyRewardedFlag) != 0)
        {
            if (source.NativeAi is not null)
            {
                // Native494410 retains actor flag0x40000 through AI revival; do not award twice.
                return;
            }
            throw new NotSupportedException("Repeated enemy revival rewards require the native per-actor rewarded flag contract.");
        }
        BattleActionPresentation? action = resolvingNativeAction ?? ActiveAction;
        byte? nativeCommand = action?.NativeAction?.Command ?? action?.NativeQueuedCommand ?? (action?.Ability == BattleAbility.Attack ? (byte)1 : null);
        byte? row = actor?.IsPlayer == true ? byte.Parse(actor.Id.AsSpan("party-".Length), System.Globalization.CultureInfo.InvariantCulture) : null;
        BattleDefeatCredit credit = row is null ? BattleDefeatCredit.Unattributed : BattleDefeatCredit.Character;
        byte? guardianForce = null;
        if (row is not null && (nativeCommand == 254 || action is { Ability: BattleAbility.GuardianForce, NativeAction: null }))
        {
            credit = BattleDefeatCredit.GuardianForce;
            guardianForce = checked((byte)(action!.NativeAction is { } native ? native.ActionId - 64 : action.ActionId));
        }
        // Native494410 grants card486750, then drop486650 before accruing EXP/AP494AF0.
        //Both chance rolls precede their bank-capacity checks. A full item bank suppresses only the rarity roll; stolen800 suppresses both item rolls.
        bool card = rewards.SelectCard(checked((byte)NextRandomByte())) && enemyDefeats.Count(defeat => defeat.CardSelected) < 8;
        sbyte selection = (target.NativeCombatantFlags & 0x800) != 0 ? (sbyte)-1 : rewards.SelectDrop(checked((byte)NextRandomByte())) && nativeRewardItems.Count < 24 ? BattleEnemyRewardDefinition.SelectDropSlot(checked((byte)NextRandomByte()), definition.RareItem, definition.Tuning.RareItem) : (sbyte)-1;
        if (selection >= 0)
        {
            nativeRewardItems.Add(rewards.Drop(target.Level, selection));
        }
        BattleEnemyDefeat fact = new(slot, checked((byte)target.Level), credit, row, guardianForce, card, selection) { AllocationId = target.Id, NativeCommand = nativeCommand };
        fact.Validate();
        if (guardianForce is byte id)
        {
            IncrementNativeGuardianForceKill(id);
        }
        enemyDefeats.Add(fact);
        target.NativeCombatantFlags |= NativeEnemyRewardedFlag;
        if (credit == BattleDefeatCredit.Character && actor is not null)
        {
            actor.NativeKillCount = unchecked((ushort)(actor.NativeKillCount + 1));
        }
    }

    private void RestoreEnemyDefeats(BattleEnemyDefeat[] defeats)
    {
        if (defeats is null || defeats.Any(defeat => defeat is null))
        {
            throw new InvalidDataException("Battle snapshot lacks its observed enemy defeat facts.");
        }
        HashSet<string> rewardedAllocations = new(StringComparer.Ordinal);
        foreach (BattleEnemyDefeat defeat in defeats)
        {
            defeat.Validate();
            BattleCombatantDefinition source = definition.Combatants.SingleOrDefault(actor => actor.RewardSlot == defeat.Slot && actor.Rewards is not null) ?? throw new InvalidDataException("Observed enemy defeat has no installed reward owner.");
            if (!byId.TryGetValue(defeat.AllocationId ?? source.Id, out BattleCombatant? defeated) || defeated.IsPlayer || defeated.SceneSlot != source.SceneSlot || defeated.SourceDefinition.RewardSlot != source.RewardSlot)
            {
                throw new InvalidDataException("Observed enemy defeat has no matching combatant allocation.");
            }
            if (!rewardedAllocations.Add(defeated.Id))
            {
                throw new InvalidDataException("Battle snapshot repeats an observed enemy defeat allocation.");
            }
            // A revived expanded enemy can change level while retaining its first defeat's reward operands.
            bool hasHistoricalLevel = source.NativeAi is not null && defeated.ResolvedNativeLevel is not null && (defeated.NativeCombatantFlags & NativeEnemyRewardedFlag) != 0;
            if (defeated.IsAlive && source.NativeAi is null || defeated.Level != defeat.Level && !hasHistoricalLevel)
            {
                throw new InvalidDataException("Observed enemy defeat contradicts its actual combatant state.");
            }
        }
        enemyDefeats.AddRange(defeats);
    }
}
