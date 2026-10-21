using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePsychoBlast23State? PsychoBlast23 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? PsychoBlast23Executable { get; init; }

    public NativeBattlePsychoBlast23State RequirePsychoBlast23()
    {
        return PsychoBlast23 ?? throw new NotSupportedException("Psycho Blast requires its actual retained module memory.");
    }

    public NativeBattleEffectMemory WithPsychoBlast23(NativeBattlePsychoBlast23State state)
    {
        state.Validate();
        return this with { PsychoBlast23 = state };
    }
}
