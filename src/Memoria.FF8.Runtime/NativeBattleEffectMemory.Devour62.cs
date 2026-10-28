using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDevour62State? Devour62
    {
        get => effectState.Devour62;
        init => effectState = effectState with { Devour62 = value };
    }

    public NativeBattleDevour62State RequireDevour62()
    {
        return Devour62 ?? throw new NotSupportedException("Devour requires its actual retained module memory.");
    }

    public NativeBattleEffectMemory WithDevour62(NativeBattleDevour62State state)
    {
        state.Validate();
        return this with { Devour62 = state };
    }
}
