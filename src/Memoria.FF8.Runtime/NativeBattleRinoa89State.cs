using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89State([property: JsonRequired] bool GlobalsPublished)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRinoa89Resources? Resources { get; init; }
    public static NativeBattleRinoa89State FromColdProcess() => new(false);

    public void Validate()
    {
        if (!GlobalsPublished && Resources is not null)
        {
            throw new InvalidDataException("Native Wishing Star resources require their entered process owner.");
        }
        Resources?.Validate();
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRinoa89State? Rinoa89
    {
        get => effectState.Rinoa89;
        init => effectState = effectState with { Rinoa89 = value };
    }
}

public sealed record NativeBattleRinoa89Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleFileStaging Staging, NativeCrtRandomState Random);
public sealed record NativeBattleRinoa89TaskStep(NativeBattleRinoa89Frame Frame, NativeBattleEffectTaskPool Pool, int Result);
