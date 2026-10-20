using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHaste125Resources(NativeBattleGfAnimationSource Clock, NativeBattleGfAnimationSource Lower, NativeBattleEffectHeapAllocation Camera, uint Sound);
public sealed record NativeBattleHaste125State([property: JsonRequired] int BufferOffset);

public sealed partial record NativeBattleOriginalEffectState
{
    public NativeBattleHaste125State? Haste125 { get; init; }
}
