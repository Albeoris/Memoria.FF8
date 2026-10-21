using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleHeroState? Hero309 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleHeroState? Hero310 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleHeroCrtStackContract? HeroCrtStack { get; init; }

    public NativeBattleHeroState RequireHero(ushort effect) => (effect switch { 309 => Hero309, 310 => Hero310, _ => throw new NotSupportedException("The Hero retained module is unsupported.") }) ?? throw new NotSupportedException("The Hero module requires its executed setup state.");

    public NativeBattleEffectMemory WithHero(NativeBattleHeroState state)
    {
        state.Validate();
        return state.Effect switch { 309 => this with { Hero309 = state }, 310 => this with { Hero310 = state }, _ => throw new NotSupportedException("The Hero retained module is unsupported.") };
    }
}
