namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectHeapLoad(NativeBattleEffectMemory Memory, int AllocationIndex);

public sealed partial record NativeBattleEffectMemory
{
    /// <summary>Load one complete5718E0 file into its tracked heap slot without consuming the effect arena.</summary>
    public NativeBattleEffectHeapLoad LoadHeap5718E0(NativeBattleEffectHeapAllocation asset)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(asset);
        if (string.IsNullOrEmpty(asset.Identity) || asset.Data.IsDefault)
        {
            throw new InvalidDataException("Native5718E0 requires an explicit file identity and complete file bytes.");
        }
        if (AllocationCount == 256)
        {
            throw new NotSupportedException("Native5718E0 has exhausted its256 physical heap allocation slots.");
        }
        int index = AllocationCount;
        NativeBattleEffectHeapAllocation loaded = asset with { TextureIdentity = new(asset.Identity, NativeBattleTextureDomain.Magic) };
        return new(this with { Allocations = Allocations.SetItem(index, loaded), AllocationCount = index + 1, Textures = Textures?.OpenTextureFile(loaded.TextureIdentity) }, index);
    }
}
