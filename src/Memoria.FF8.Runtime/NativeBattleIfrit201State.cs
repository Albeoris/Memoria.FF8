using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleIfrit201State([property: JsonRequired] byte Filter145);

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore]
    public NativeBattleIfrit201State? Ifrit201
    {
        get => Cinematic is { Effect: 201 } state ? new(state.Filter145) : null;
        init => Cinematic = value is null ? null : new(201, value.Filter145);
    }
}

public sealed record NativeBattleIfrit201Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
