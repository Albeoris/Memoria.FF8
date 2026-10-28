using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBlaster186State? Blaster186
    {
        get => effectState.Blaster186;
        init => effectState = effectState with { Blaster186 = value };
    }
}
