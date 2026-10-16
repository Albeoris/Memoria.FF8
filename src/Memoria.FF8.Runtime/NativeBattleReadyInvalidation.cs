namespace Memoria.FF8.Runtime;

public sealed record NativeBattleReadyInvalidation(byte RemoveUiCommands, bool ClearReadyBusyFlags, bool RemoveQueuedCommands, bool ResetGauge);

public static class NativeBattleReadyInvalidations
{
    /// <summary>493840's readiness branches at493D80 publication, using484600's physical queue count and E30/E38's previous mirrors.</summary>
    public static NativeBattleReadyInvalidation Evaluate(byte actor, ushort saved, uint battle, int queuedCommands, byte currentActor, ushort? previousSaved, uint? previousBattle)
    {
        if (actor >= 3 || queuedCommands < 0 || queuedCommands > 22)
        {
            throw new InvalidDataException("Native493840 readiness requires a party physical index and484600's bounded queue count.");
        }
        byte removals = 0;
        bool positive = (battle & 0x80000000) == 0;
        if (positive && queuedCommands == 0 && ((saved & 4) != 0 || (battle & 9) != 0))
        {
            removals++;
        }
        if (positive && queuedCommands == 0 && currentActor != actor)
        {
            ushort oldSaved = previousSaved ?? throw new NotSupportedException("Native493840 requires retained WORD1D28E30 before comparing compelled-status transitions.");
            uint oldBattle = previousBattle ?? throw new NotSupportedException("Native493840 requires retained DWORD1D28E38 before comparing compelled-status transitions.");
            if (((saved ^ oldSaved) & 0x20) != 0 || ((battle ^ oldBattle) & 0x2004000) != 0)
            {
                removals++;
            }
        }
        bool terminal = (battle & 0x10000) != 0 || (saved & 1) != 0;
        if (terminal)
        {
            removals++;
        }
        return new(removals, removals != 0, terminal, terminal);
    }
}
