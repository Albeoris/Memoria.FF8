using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFiraResources(NativeBattleEffectHeapAllocation Animation, NativeBattleModuleResource Sprite0, NativeBattleModuleResource AnimatedSprite, NativeBattleStaticMeshResource Mesh0, NativeBattleStaticMeshResource Mesh1, NativeBattleStaticMeshResource Mesh2, uint Sound, ImmutableArray<uint> UvOffsets);
