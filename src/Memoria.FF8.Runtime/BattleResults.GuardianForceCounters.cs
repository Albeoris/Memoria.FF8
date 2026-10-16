namespace Memoria.FF8.Runtime;

public sealed partial record BattleResult
{
    public BattleGuardianForceCounterChange[] GuardianForceCounterChanges { get; init; } = [];

    private void ValidateNativeGuardianForceCounters()
    {
        if (GuardianForceCounterChanges is null || GuardianForceCounterChanges.Any(change => change is null) || GuardianForceCounterChanges.Select(change => change.Id).Distinct().Count() != GuardianForceCounterChanges.Length)
        {
            throw new InvalidDataException("Native GF counter receipts are missing or duplicated.");
        }
        foreach (BattleGuardianForceCounterChange change in GuardianForceCounterChanges)
        {
            change.Validate();
        }
        if (RewardPolicy != BattleRewardPolicy.NativeCombat || EnemyDefeats is null)
        {
            return;
        }
        for (int id = 0; id < 16; id++)
        {
            int kills = EnemyDefeats.Count(defeat => defeat.Credit == BattleDefeatCredit.GuardianForce && defeat.GuardianForceId == id);
            BattleGuardianForceCounterChange? change = GuardianForceCounterChanges.SingleOrDefault(change => change.Id == id);
            if (kills != 0 && change?.KillsBefore is null || change?.KillsBefore is ushort before && unchecked((ushort)(change.KillsAfter!.Value - before)) != unchecked((ushort)kills))
            {
                throw new InvalidDataException("Native GF kill counter differs from its observed rewarded defeats.");
            }
        }
    }
}

public static partial class BattleResultProjection
{
    private static void ApplyNativeGuardianForceCounters(BattleResult result, NativeGuardianForceVital[] guardianForces)
    {
        foreach (BattleGuardianForceCounterChange change in result.GuardianForceCounterChanges)
        {
            NativeGuardianForceVital before = guardianForces[change.Id];
            if (before.KillCount != change.KillsBefore || before.DeathCount != change.DeathsBefore)
            {
                throw new InvalidDataException("Native GF counter receipt differs from the current canonical saved row.");
            }
            guardianForces[change.Id] = before with { KillCount = change.KillsAfter, DeathCount = change.DeathsAfter };
        }
    }
}
