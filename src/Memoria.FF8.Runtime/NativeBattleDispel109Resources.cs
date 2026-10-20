using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDispel109Resources(NativeBattleGfAnimationSource Animation, NativeBattleEffectHeapAllocation Camera, NativeBattleModuleResource Swirl, NativeBattleModuleResource Spark, uint Sound);
public sealed record NativeBattleDispel109State([property: JsonRequired] int BufferOffset);
public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDispel109State? Dispel109 { get; init; }
}
public sealed record NativeBattleDispel109Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
