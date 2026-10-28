namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect213State? Effect213
    {
        get => effectState.Effect213;
        init => effectState = effectState with { Effect213 = value };
    }
}
