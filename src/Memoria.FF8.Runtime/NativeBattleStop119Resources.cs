using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStop119Resources(NativeBattleGfAnimationSource Clock, NativeBattleEffectHeapAllocation Camera, uint Sound);
public sealed record NativeBattleStop119State([property: JsonRequired] int BufferOffset);

public sealed partial record NativeBattleOriginalEffectState
{
    public NativeBattleStop119State? Stop119 { get; init; }
}
