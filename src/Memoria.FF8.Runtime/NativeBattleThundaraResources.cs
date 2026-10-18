using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleThundaraResources(NativeBattleEffectHeapAllocation Animation, ImmutableArray<NativeBattleModuleResource> Sprites, NativeBattleStaticMeshResource Mesh0, NativeBattleStaticMeshResource Mesh1, NativeBattleStaticMeshResource Mesh2, uint Sound, uint FlashColor)
{
    public NativeBattleModuleResource Sprite(uint address)
    {
        NativeBattleModuleResource[] matches = Sprites.Where(sprite => sprite.Address == address).ToArray();
        return matches.Length == 1 ? matches[0] : throw new NotSupportedException("Native Thundara requires the exact original sprite resource address.");
    }
}
