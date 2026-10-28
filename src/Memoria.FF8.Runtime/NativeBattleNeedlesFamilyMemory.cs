using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleNeedlesFamilyState? ThousandNeedles177
    {
        get => effectState.ThousandNeedles177;
        init => effectState = effectState with { ThousandNeedles177 = value };
    }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleNeedlesFamilyState? TenThousandNeedles178
    {
        get => effectState.TenThousandNeedles178;
        init => effectState = effectState with { TenThousandNeedles178 = value };
    }
}
