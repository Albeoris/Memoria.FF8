namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    internal static ushort NativeSavedStatusBits(BattleCombatant actor) => actor.NativeStatus0;

    private static uint NativeBattleStatusBits(BattleCombatant actor) => actor.NativeStatus1;

}
