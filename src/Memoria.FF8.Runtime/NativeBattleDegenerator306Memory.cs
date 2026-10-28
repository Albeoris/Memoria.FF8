using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDegenerator306State? Degenerator306
    {
        get => effectState.Degenerator306;
        init => effectState = effectState with { Degenerator306 = value };
    }
}
