namespace Memoria.FF8.Runtime;

/// <summary>Keeps actor directory fixups private through the caller's task-context initialization.</summary>
internal static class NativeBattleActorBankInitialization
{
    internal static NativeBattleNodeInitialization<NativeBattleNodeSource> Prepare(NativeBattleNodeSource source, uint address, int directoryOffset = 0)
    {
        NativeBattleTaskOperation.AdmitCurrentRetainedMemoryStorage(source);
        NativeBattleNodeBankOperation operation = new(source);
        try
        {
            if (operation.ReadUInt32(directoryOffset) == 0)
            {
                NativeBattleEffectNodeBankFixups.Relocate(operation, address, directoryOffset);
                operation.WriteUInt32(directoryOffset, 1);
            }
            return new(source, operation);
        }
        catch
        {
            operation.Dispose();
            throw;
        }
    }
}
