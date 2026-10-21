using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleWindBlast29State? WindBlast29 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? WindBlast29Executable { get; init; }

    public NativeBattleWindBlast29State RequireWindBlast29()
    {
        return WindBlast29 ?? throw new NotSupportedException("Wind Blast requires its actual retained module memory.");
    }

    public NativeBattleEffectMemory WithWindBlast29(NativeBattleWindBlast29State state)
    {
        state.Validate();
        return this with { WindBlast29 = state };
    }
}
