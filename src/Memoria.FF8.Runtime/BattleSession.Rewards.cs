namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private readonly List<BattleEnemyDefeat> enemyDefeats = [];
    public IReadOnlyList<BattleEnemyDefeat> EnemyDefeats => enemyDefeats.AsReadOnly();

    private void ObserveEnemyDefeat(BattleCombatant? actor, BattleCombatant target)
    {
        BattleCombatantDefinition source = target.SourceDefinition;
        if (target.IsPlayer || source.Rewards is not { } rewards)
        {
            return;
        }
        int slot = source.RewardSlot ?? throw new InvalidDataException("Native enemy reward inputs require their actual formation slot.");
        if ((target.NativeCombatantFlags & 0x40000) != 0)
        {
            if (source.NativeAi is not null)
            {
                // Native494410 retains actor flag0x40000 through AI revival; do not award twice.
                return;
            }
            throw new NotSupportedException("Repeated enemy revival rewards require the native per-actor rewarded flag contract.");
        }
        byte? row = actor?.IsPlayer == true ? byte.Parse(actor.Id.AsSpan("party-".Length), System.Globalization.CultureInfo.InvariantCulture) : null;
        BattleDefeatCredit credit = row is null ? BattleDefeatCredit.Unattributed : BattleDefeatCredit.Character;
        byte? guardianForce = null;
        if (row is not null && ActiveAction is { Ability: BattleAbility.GuardianForce } action)
        {
            credit = BattleDefeatCredit.GuardianForce;
            guardianForce = checked((byte)action.ActionId);
        }
        // Native494410 grants card486750, then drop486650 before accruing EXP/AP494AF0.
        bool card = rewards.SelectCard(checked((byte)NextRandomByte()));
        sbyte selection = (target.NativeCombatantFlags & 0x800) != 0 ? (sbyte)-1 : rewards.SelectDrop(checked((byte)NextRandomByte())) ? BattleEnemyRewardDefinition.SelectDropSlot(checked((byte)NextRandomByte()), definition.RareItem) : (sbyte)-1;
        BattleEnemyDefeat fact = new(slot, checked((byte)target.Level), credit, row, guardianForce, card, selection) { AllocationId = target.Id, NativeCommand = ActiveAction?.NativeQueuedCommand ?? ActiveAction?.NativeAction?.Command ?? (ActiveAction?.Ability == BattleAbility.Attack ? (byte)1 : null) };
        fact.Validate();
        enemyDefeats.Add(fact);
        target.NativeCombatantFlags |= 0x40000;
        if (credit == BattleDefeatCredit.Character && actor is not null)
        {
            actor.NativeKillCount = unchecked((ushort)(actor.NativeKillCount + 1));
        }
    }
}
