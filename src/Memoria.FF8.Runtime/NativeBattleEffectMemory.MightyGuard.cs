using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMightyGuardState? MightyGuard73 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMightyGuardState? MightyGuard78 { get; init; }

    public NativeBattleMightyGuardState RequireMightyGuard(ushort effect)
    {
        return (effect switch { 73 => MightyGuard73, 78 => MightyGuard78, _ => throw new NotSupportedException("The selected Mighty Guard module is unsupported.") }) ?? throw new NotSupportedException("Mighty Guard requires its actual retained module state.");
    }

    public NativeBattleEffectMemory WithMightyGuard(NativeBattleMightyGuardState state)
    {
        state.Validate();
        return state.Effect switch { 73 => this with { MightyGuard73 = state }, 78 => this with { MightyGuard78 = state }, _ => throw new NotSupportedException("The selected Mighty Guard module is unsupported.") };
    }
}
