using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>7059D0/7059E0's process-retained DWORD2557098, separate from UI, CRT and Quezacotl's25217A0.</summary>
public sealed record NativeBattleSharedEffectRandomState([property: JsonRequired] uint State)
{
    public static NativeBattleSharedEffectRandomState FromColdProcess() => new(0);

    public NativeBattleSharedEffectRandomState Seed7059D0(uint state)
    {
        return new(state);
    }

    public NativeBattleSharedEffectRandomState Draw7059E0(out ushort result)
    {
        result = NativeBattleRandomTransitions.Draw125(State);
        return new(result);
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSharedEffectRandomState? SharedEffectRandom { get; init; }

    public NativeBattleEffectMemory SeedSharedEffectRandom7059D0(uint state)
    {
        //The original seeder writes its full operand without reading the previous DWORD.
        return this with { SharedEffectRandom = new(state) };
    }

    public NativeBattleEffectMemory DrawSharedEffectRandom7059E0(out ushort result)
    {
        NativeBattleSharedEffectRandomState current = SharedEffectRandom ?? throw new NotSupportedException("Native7059E0 requires the retained shared effect RNG DWORD2557098; explicitly migrate a proved named standalone source.");
        return this with { SharedEffectRandom = current.Draw7059E0(out result) };
    }
}
