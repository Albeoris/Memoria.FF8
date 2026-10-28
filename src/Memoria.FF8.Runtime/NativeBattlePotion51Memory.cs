namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public short? Potion51ContextF4
    {
        get => effectState.Potion51ContextF4;
        init => effectState = effectState with { Potion51ContextF4 = value };
    }
    public int? Potion51SceneFade
    {
        get => effectState.Potion51SceneFade;
        init => effectState = effectState with { Potion51SceneFade = value };
    }
}
