using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLanding17Resources(NativeBattleEffectHeapAllocation Tim, NativeBattleEffectHeapAllocation Camera, NativeBattleModuleResource Spark, NativeBattleGfAnimationSource Animation, ImmutableArray<uint> NodeFlags, NativeVibrationBank Vibration, uint FirstSound, uint SecondSound, uint ShadeSource, uint ShadeDestination);
public sealed record NativeBattleLanding17State([property: JsonRequired] int BufferOffset);
public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleLanding17State? Landing17 { get; init; }
}
