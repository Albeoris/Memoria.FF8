using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDrain39BankState? Absorb56Bank { get; init; }
}

public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDrain39State? Absorb56 { get; init; }
}
