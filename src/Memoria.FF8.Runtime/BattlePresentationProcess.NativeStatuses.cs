namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    internal static ushort NativeSavedStatusBits(BattleCombatant actor)
    {
        ushort bits = actor.NativeStatus0;
        foreach (BattleStatusState effect in actor.Effects)
        {
            bits |= effect.Definition.Kind switch
            {
                BattleStatusKind.Poison => (ushort)2,
                BattleStatusKind.Blind => (ushort)8,
                BattleStatusKind.Silence => (ushort)16,
                _ => (ushort)0
            };
        }
        return bits;
    }

    private static uint NativeBattleStatusBits(BattleCombatant actor)
    {
        uint bits = actor.NativeStatus1;
        foreach (BattleStatusState effect in actor.Effects)
        {
            bits |= effect.Definition.Kind switch
            {
                BattleStatusKind.Sleep => 1u,
                BattleStatusKind.Haste => 2u,
                BattleStatusKind.Slow => 4u,
                BattleStatusKind.Regen => 16u,
                BattleStatusKind.Protect => 32u,
                BattleStatusKind.Shell => 64u,
                _ => 0u
            };
        }
        return bits;
    }
}
