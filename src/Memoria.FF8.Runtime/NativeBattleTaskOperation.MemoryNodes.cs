namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleTaskOperation
{
    internal static NativeBattleNodeSource AdmitCurrentRetainedMemoryStorage(NativeBattleNodeSource source)
    {
        if (source.IsStaging)
        {
            current?.AdmitStaging(source.StagingAuthority);
        }
        else
        {
            AdmitCurrentRetainedArena(source.Arena ?? throw new InvalidDataException("Retained node storage has no physical arena authority."));
        }
        return source;
    }
}
