using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A completed display draw retained for deterministic lazy scene-copy rendering.</summary>
public sealed record NativeBattleSceneReplaySource(NativeBattlePreparedPrimitiveFrame Frame, NativeBattleEffectTextureState Textures, BattleSceneVisibleArea VisibleArea)
{
    public NativeBattlePcUiFrame? UiFrame { get; init; }
    public long Callback { get; init; }
    public byte Buffer { get; init; }
    public ImmutableArray<byte> DrawingEnvironment { get; init; } = [];
}
