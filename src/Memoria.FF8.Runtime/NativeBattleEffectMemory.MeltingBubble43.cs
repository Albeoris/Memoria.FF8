using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMeltingBubble43State? MeltingBubble43
    {
        get => effectState.MeltingBubble43;
        init => effectState = effectState with { MeltingBubble43 = value };
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? MeltingBubble43Executable
    {
        get => effectState.MeltingBubble43Executable;
        init => effectState = effectState with { MeltingBubble43Executable = value };
    }

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
