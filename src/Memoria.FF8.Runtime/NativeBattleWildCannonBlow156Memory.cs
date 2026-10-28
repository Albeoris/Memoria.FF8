using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleWildCannonBlow156State? WildCannonBlow156
    {
        get => effectState.WildCannonBlow156;
        init => effectState = effectState with { WildCannonBlow156 = value };
    }
}
