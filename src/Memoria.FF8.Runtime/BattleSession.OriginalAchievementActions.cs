namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private readonly List<BattleOriginalStockDrawOperation> originalStockDrawOperations = [];

    private void ValidateOriginalMaximumHitPoints(BattleSnapshot snapshot)
    {
        foreach (BattleCombatantSnapshot saved in snapshot.Combatants)
        {
            if (saved.NativeParticipant?.MaximumHitPoints is not int maximum)
            {
                continue;
            }
            BattleCombatant actor = GetActor(saved.Id);
            if (!actor.IsPlayer || actor.NativePlayerStatRules is null || actor.GuardianFormVitals is not null || maximum != actor.MaximumHp)
            {
                throw new InvalidDataException("Saved native maximum HP differs from its actual playable statistic owner.");
            }
        }
    }

    private void RecordOriginalStockDraw(BattleCombatant actor, BattleAttackCommand command, BattleNativeDrawContext result)
    {
        if (!actor.IsPlayer || command.DrawCast || result.Selection >= 64)
        {
            return;
        }
        BattleOriginalStockDrawOperation operation = new(checked(originalStockDrawOperations.Count + 1), actor.Id, command.TargetId, result);
        operation.Validate();
        originalStockDrawOperations.Add(operation);
    }

    private void RestoreOriginalStockDrawOperations(BattleOriginalStockDrawOperation[]? operations)
    {
        originalStockDrawOperations.Clear();
        // Older snapshots contain no action history; their inventory cannot establish past operations.
        if (operations is null)
        {
            return;
        }
        for (int index = 0; index < operations.Length; index++)
        {
            BattleOriginalStockDrawOperation operation = operations[index] ?? throw new InvalidDataException("Stock Draw history contains an empty operation.");
            operation.Validate();
            if (operation.Ordinal != index + 1 || !GetActor(operation.ActorId).IsPlayer || GetActor(operation.SourceId).IsPlayer)
            {
                throw new InvalidDataException("Stock Draw history contradicts its encounter ordinal or combatant owners.");
            }
            originalStockDrawOperations.Add(operation);
        }
    }
}
