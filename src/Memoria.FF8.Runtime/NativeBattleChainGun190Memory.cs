using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleChainGun190State? ChainGun190
    {
        get => effectState.ChainGun190;
        init => effectState = effectState with { ChainGun190 = value };
    }
}
