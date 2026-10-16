using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
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
