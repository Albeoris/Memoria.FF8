using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class BattleHudTestPresentation
{
    internal static BattleSession Victory(BattleDefinition definition, BattleAtbMode mode)
    {
        BattleCombatantDefinition[] actors = definition.Combatants.Select(actor => actor.IsPlayer ? actor : actor with { CurrentHp = 0, InitialAtb = 0, NativeStatus0 = 1 }).ToArray();
        return new(definition with { Combatants = actors }, mode);
    }

    internal static BattlePresentationUiState Visible()
    {
        return new(commandMenuHidden: false, vitalsVisible: true, guardianBoostOwnsInput: false, paused: false);
    }
}
