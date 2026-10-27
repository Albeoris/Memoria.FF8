namespace Memoria.FF8.Runtime;

public partial record NativeBattleEffectArena
{
    internal NativeBattleStorageReference? StorageReference { get; private set; }
    internal NativeBattleWorkingMemorySnapshot? CapturedAuthority => capturedAuthority;
    internal int CapturedOffset => capturedOffset;
    internal bool CapturedBorrowed { get; private set; }

    internal NativeBattleEffectArenaSnapshot WithStorageReference(NativeBattleStorageReference? reference)
    {
        if (!snapshot)
        {
            throw new InvalidOperationException("Storage references require detached arena snapshots.");
        }
        NativeBattleEffectArenaSnapshot result = this is NativeBattleEffectArenaSnapshot existing ? existing with { } : new(initialRegions, initialUnknown, initialPointers, bound) { TextureFiles = initialTextureFiles };
        result.StorageReference = reference;
        return result;
    }

    internal void AttachStorageReference(NativeBattleStorageReference reference)
    {
        if (!snapshot || capturedAuthority is null)
        {
            throw new InvalidOperationException("Only freshly captured physical views can acquire graph references.");
        }
        StorageReference = reference;
    }

    internal static NativeBattleEffectArena RestoreGraphView(NativeBattleWorkingMemory owner, NativeBattleStorageReference reference) => new(owner, reference.Offset, reference.Length, reference.Borrowed);
}
