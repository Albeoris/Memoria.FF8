using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRayBomb307State? RayBomb307
    {
        get => effectState.RayBomb307;
        init => effectState = effectState with { RayBomb307 = value };
    }
}
