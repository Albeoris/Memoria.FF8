using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMeltingBubble43State? MeltingBubble43 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? MeltingBubble43Executable { get; init; }

    public NativeBattleMeltingBubble43State RequireMeltingBubble43()
    {
        return MeltingBubble43 ?? throw new NotSupportedException("Melting Bubble requires its actual retained module memory.");
    }

    public NativeBattleEffectMemory WithMeltingBubble43(NativeBattleMeltingBubble43State state)
    {
        state.Validate();
        return this with { MeltingBubble43 = state };
    }
}
