using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMadRush273Resources(NativeBattleGfAnimationSource Animation, NativeBattleModuleResource Particle, NativeBattleEffectHeapAllocation Camera, uint Sound);
public sealed record NativeBattleMadRush273State([property: JsonRequired] int BufferOffset);

public sealed partial record NativeBattleOriginalEffectState
{
    public NativeBattleMadRush273State? MadRush273 { get; init; }
}
