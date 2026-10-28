using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleHeroState? Hero309
    {
        get => effectState.Hero309;
        init => effectState = effectState with { Hero309 = value };
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleHeroState? Hero310
    {
        get => effectState.Hero310;
        init => effectState = effectState with { Hero310 = value };
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleHeroCrtStackContract? HeroCrtStack
    {
        get => effectState.HeroCrtStack;
        init => effectState = effectState with { HeroCrtStack = value };
    }

    public NativeBattleHeroState RequireHero(ushort effect) => (effect switch { 309 => Hero309, 310 => Hero310, _ => throw new NotSupportedException("The Hero retained module is unsupported.") }) ?? throw new NotSupportedException("The Hero module requires its executed setup state.");

    public NativeBattleEffectMemory WithHero(NativeBattleHeroState state)
    {
        state.Validate();
        return state.Effect switch { 309 => this with { Hero309 = state }, 310 => this with { Hero310 = state }, _ => throw new NotSupportedException("The Hero retained module is unsupported.") };
    }
}
