using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMeltEye158State? MeltEye158
    {
        get => effectState.MeltEye158;
        init => effectState = effectState with { MeltEye158 = value };
    }
}
