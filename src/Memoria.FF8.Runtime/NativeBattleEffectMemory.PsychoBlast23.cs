using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePsychoBlast23State? PsychoBlast23
    {
        get => effectState.PsychoBlast23;
        init => effectState = effectState with { PsychoBlast23 = value };
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? PsychoBlast23Executable
    {
        get => effectState.PsychoBlast23Executable;
        init => effectState = effectState with { PsychoBlast23Executable = value };
    }

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
