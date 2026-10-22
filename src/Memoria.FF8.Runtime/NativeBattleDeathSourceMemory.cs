namespace Memoria.FF8.Runtime;

public static class NativeBattleDeathSourceMemory
{
    public static (NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging) Prepare(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, NativeBattleDeathResources resources)
    {
        NativeBattleDeathState state = resources.Profile.RequireState(memory);
        if (!state.PendingColdSources)
        {
            return (memory, staging);
        }
        if (state.AuraActors is not null || state.DarkActors is not null)
        {
            throw new InvalidDataException("Native Death pending source initialization already contains mutable node bytes.");
        }
        if (resources.Model.Bytes.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Native Death cold source initialization requires its original model bytes.");
        }
        return (memory, staging.Write(resources.Model.Address, resources.Model.Bytes.AsSpan()));
    }
}
