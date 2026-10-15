namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private readonly List<ReplayBattleEnemyDefeat> enemyDefeats = [];
    public IReadOnlyList<ReplayBattleEnemyDefeat> EnemyDefeats => enemyDefeats.AsReadOnly();

    private void ObserveEnemyDefeat(BattleCombatant? actor, BattleCombatant target)
    {
        BattleCombatantDefinition source = definition.Combatants.Single(candidate => candidate.Id == target.Id);
        if (target.IsPlayer || source.Rewards is not { } rewards)
        {
            return;
        }
        int slot = source.RewardSlot ?? throw new InvalidDataException("Native enemy reward inputs require their actual formation slot.");
        if (enemyDefeats.Any(defeat => defeat.Slot == slot))
        {
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
        sbyte selection = rewards.SelectDrop(checked((byte)NextRandomByte())) ? BattleEnemyRewardDefinition.SelectDropSlot(checked((byte)NextRandomByte()), definition.RareItem) : (sbyte)-1;
        ReplayBattleEnemyDefeat fact = new(slot, checked((byte)target.Level), credit, row, guardianForce, card, selection);
        fact.Validate();
        enemyDefeats.Add(fact);
    }
}
