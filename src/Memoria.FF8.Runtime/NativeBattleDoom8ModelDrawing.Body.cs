using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDoom8ModelDrawing
{
    private static ImmutableArray<NativeBattleRawMesh> ReadGeometry(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelResource resource, Dictionary<int, (ImmutableArray<ushort> Counts, int First, int End)>? metadata = null) => NativeBattleEffectOwnedModelRendering.ReadGeometry(staging, resource, metadata);
}
