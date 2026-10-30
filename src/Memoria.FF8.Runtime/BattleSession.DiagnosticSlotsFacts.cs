namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticSlotsFacts(byte? SavedSpecialMagicFlags, bool? NativeLimitsDisabled, bool LimitOncePerTurn, byte? LimitTurnCrisis, NativeBattleRandomState Random);

public sealed partial class BattleSession
{
    public BattleDiagnosticSlotsFacts ObserveDiagnosticSlotsFacts(string actorId)
    {
        BattleCombatant actor = combatants.Single(candidate => candidate.Id == actorId);
        return new(definition.LimitProgress?.SpecialMagicFlags, definition.NativeLimitsDisabled, definition.LimitOncePerTurn, actor.LimitTurnCrisis, random);
    }
}
