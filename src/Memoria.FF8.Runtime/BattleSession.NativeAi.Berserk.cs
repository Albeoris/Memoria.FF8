namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryRunNativeEnemyBerserk(BattleCombatant actor, int callback)
    {
        if (callback != 1 || (NativeSavedStatusBits(actor) & 0x20) == 0)
        {
            return false;
        }
        //487DF0's callback1 entry bypasses the script and selects ability0 from the current level band.
        byte command = 0;
        ushort id = 0;
        byte sequence = 0;
        PrepareNativeAiAbility(actor, 0, ref command, ref id, ref sequence);
        QueueNativeCommand(actor, command, id, sequence, ResolveNativeAiTargetMask(actor, 201));
        return true;
    }
}
