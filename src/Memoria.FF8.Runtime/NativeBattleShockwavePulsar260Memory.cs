using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleShockwavePulsar260State? ShockwavePulsar260
    {
        get => effectState.ShockwavePulsar260;
        init => effectState = effectState with { ShockwavePulsar260 = value };
    }
}
