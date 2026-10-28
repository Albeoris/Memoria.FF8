using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? QuezacotlCompletion17A8
    {
        get => effectState.QuezacotlCompletion17A8;
        init => effectState = effectState with { QuezacotlCompletion17A8 = value };
    }
}
