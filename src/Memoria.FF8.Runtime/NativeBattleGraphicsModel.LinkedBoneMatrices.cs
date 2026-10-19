using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleGraphicsModel
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBodyBoneMatrices? CurrentLinkedBoneMatrices { get; init; }
}
