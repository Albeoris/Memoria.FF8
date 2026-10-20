using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBreak117Resources(NativeBattleGfAnimationSource Shell, ImmutableArray<byte> Debris, NativeBattleModuleResource Spark, NativeBattleEffectHeapAllocation Camera, uint Sound);
public sealed record NativeBattleBreak117State([property: JsonRequired] int BufferOffset);

public sealed partial record NativeBattleOriginalEffectState
{
    public NativeBattleBreak117State? Break117 { get; init; }
}
