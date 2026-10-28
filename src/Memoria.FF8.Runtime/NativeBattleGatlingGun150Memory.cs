using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGatlingGun150State? GatlingGun150
    {
        get => effectState.GatlingGun150;
        init => effectState = effectState with { GatlingGun150 = value };
    }
}
