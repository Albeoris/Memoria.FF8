using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleShotgun281State? Shotgun281
    {
        get => effectState.Shotgun281;
        init => effectState = effectState with { Shotgun281 = value };
    }
}
