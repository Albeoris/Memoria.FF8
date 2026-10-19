using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSlow123Resources(NativeBattleGfAnimationSource Clock, NativeBattleGfAnimationSource Lower, NativeBattleEffectHeapAllocation Camera, uint Sound);
public sealed record NativeBattleSlow123State([property: JsonRequired] int BufferOffset);

public sealed partial record NativeBattleOriginalEffectState
{
    public NativeBattleSlow123State? Slow123 { get; init; }
}
