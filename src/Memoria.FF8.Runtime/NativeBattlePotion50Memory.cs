namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public short? Potion50ContextF4
    {
        get => effectState.Potion50ContextF4;
        init => effectState = effectState with { Potion50ContextF4 = value };
    }
}
