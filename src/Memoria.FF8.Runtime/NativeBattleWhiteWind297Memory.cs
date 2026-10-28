using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleWhiteWind297State? WhiteWind297
    {
        get => effectState.WhiteWind297;
        init => effectState = effectState with { WhiteWind297 = value };
    }
}
