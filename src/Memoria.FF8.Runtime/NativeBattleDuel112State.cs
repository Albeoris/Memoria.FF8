using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel112State([property: JsonRequired] bool GlobalsInitialized, [property: JsonRequired] NativeBattleEffectParameters? Parameters, [property: JsonRequired] NativeBattleDuel112Resources? Resources)
{
    public static NativeBattleDuel112State FromColdProcess() => new(false, null, null);

    public void Validate()
    {
        if (GlobalsInitialized)
        {
            if (Parameters is null || Parameters.Actor78 >= 7 || Resources is null)
            {
                throw new InvalidDataException("Native Duel requires its actual entered parameters and original resources.");
            }
            Resources.Validate();
        }
        else if (Parameters is not null || Resources is not null)
        {
            throw new InvalidDataException("Native Duel cannot retain entered data before its actual native entry.");
        }
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDuel112State? Duel112 { get; init; }
}

public sealed record NativeBattleDuel112Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleFileStaging Staging, NativeCrtRandomState Random);
public sealed record NativeBattleDuel112TaskStep(NativeBattleDuel112Frame Frame, NativeBattleEffectTaskPool Pool, int Result);
