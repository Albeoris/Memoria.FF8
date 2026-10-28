using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDrain39BankState? Absorb56Bank
    {
        get => effectState.Absorb56Bank;
        init => effectState = effectState with { Absorb56Bank = value };
    }
}

public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDrain39State? Absorb56 { get; init; }
}
