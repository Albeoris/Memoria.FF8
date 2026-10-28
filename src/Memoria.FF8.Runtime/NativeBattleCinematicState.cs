using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicState([property: JsonRequired] int Effect, [property: JsonRequired] byte Filter145);
public sealed record NativeBattleCinematicFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCinematicState? Cinematic
    {
        get => effectState.Cinematic;
        init => effectState = effectState with { Cinematic = value };
    }
}
