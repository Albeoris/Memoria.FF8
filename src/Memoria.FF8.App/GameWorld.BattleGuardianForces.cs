using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private static BattleCombatantDefinition ReadAdmittedPartyMember(BattleDefinition definition, byte row)
    {
        return definition.Combatants.SingleOrDefault(actor => actor.IsPlayer && (actor.MagicRecordId == row || actor.MagicRecordId is null && actor.Id == $"party-{row}"))
            ?? definition.PartyReplacementCandidates.GetValueOrDefault(row)
            ?? throw new InvalidDataException("The battle's entry facts do not include this party member.");
    }

    private static int[] CaptureRewardEligibleGuardianForces(NativeCharacterVitals vitals, IReadOnlyList<BattlePartyResult> party, BattleSnapshot completed, IReadOnlyList<byte> occupiedRows)
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
        return BattleResultProjection.EligibleGuardianForces(vitals with { Records = records, GuardianForces = guardianForces }, occupiedRows);
    }

    private static BattleGuardianForceCompatibilityChange[] CaptureGuardianForceCompatibilityChanges(BattleSnapshot completed, IReadOnlyList<BattlePartyResult> party)
    {
        List<BattleGuardianForceCompatibilityChange> changes = [];
        foreach (BattlePartyResult member in party)
        {
            BattleCombatantDefinition entry = ReadAdmittedPartyMember(completed.Definition, member.Row);
            string actorId = entry.Id;
            ushort[]? before = entry.GuardianForceCompatibility;
            ushort[]? after = completed.Combatants.Single(actor => actor.Id == actorId).GuardianForceCompatibility;
            if (before is not null && after is not null && !before.SequenceEqual(after))
            {
                changes.Add(new(member.Row, before.ToArray(), after.ToArray()));
            }
        }
        return changes.ToArray();
    }
}
