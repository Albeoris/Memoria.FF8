using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private static int[] CaptureRewardEligibleGuardianForces(NativeCharacterVitals vitals, IReadOnlyList<BattlePartyResult> party, BattleSnapshot completed)
    {
        NativeCharacterVital[] records = vitals.Records.ToArray();
        foreach (BattlePartyResult member in party)
        {
            records[member.Row] = records[member.Row] with { Status = member.StatusAfter };
        }
        NativeGuardianForceVital[] guardianForces = vitals.GuardianForces.ToArray();
        foreach (BattleGuardianForceVital gf in completed.GuardianForces)
        {
            guardianForces[gf.Id] = guardianForces[gf.Id] with { HitPoints = gf.HitPoints };
        }
        return BattleResultProjection.EligibleGuardianForces(vitals with { Records = records, GuardianForces = guardianForces });
    }

    private static BattleGuardianForceCompatibilityChange[] CaptureGuardianForceCompatibilityChanges(BattleSnapshot completed, IReadOnlyList<BattlePartyResult> party)
    {
        List<BattleGuardianForceCompatibilityChange> changes = [];
        foreach (BattlePartyResult member in party)
        {
            string actorId = $"party-{member.Row}";
            ushort[]? before = completed.Definition.Combatants.Single(actor => actor.Id == actorId).GuardianForceCompatibility;
            ushort[]? after = completed.Combatants.Single(actor => actor.Id == actorId).GuardianForceCompatibility;
            if (before is not null && after is not null && !before.SequenceEqual(after))
            {
                changes.Add(new(member.Row, before.ToArray(), after.ToArray()));
            }
        }
        return changes.ToArray();
    }
}
