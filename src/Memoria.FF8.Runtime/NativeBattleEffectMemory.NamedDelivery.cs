namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    /// <summary>Publish one selected battle archive file to its current native destination and filename context.</summary>
    public NativeBattleEffectMemory DeliverBattleFile(NativeBattleEffectFileAsset file, int offset, bool useWorkBuffer = false)
    {
        if (file.FileId < 0 || string.IsNullOrWhiteSpace(file.Name) || file.Data.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native battle file delivery requires its selected original archive identity and bytes.");
        }
        NativeBattleTextureFileIdentity identity = new(file.Name, NativeBattleTextureDomain.Battle);
        NativeBattleEffectMemory result = this with { Textures = Textures?.OpenTextureFile(identity) };
        if (useWorkBuffer)
        {
            NativeBattleGuardianWorkBuffer destination = GuardianWorkBuffer ?? throw new NotSupportedException("Native battle file delivery requires its current auxiliary destination owner.");
            return result with { GuardianWorkBuffer = destination.Write(offset, file.Data.AsSpan()) };
        }
        NativeBattleEffectArena arena = Arena ?? throw new NotSupportedException("Native battle file delivery requires its current arena destination owner.");
        arena = arena.Write(offset, file.Data.AsSpan()).RememberTextureFile(offset, file.Data.AsSpan(), identity);
        return result with { Arena = arena };
    }
}
