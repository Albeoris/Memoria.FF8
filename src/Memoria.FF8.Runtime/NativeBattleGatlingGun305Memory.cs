using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGatlingGun305State? GatlingGun305
    {
        get => effectState.GatlingGun305;
        init => effectState = effectState with { GatlingGun305 = value };
    }
}
