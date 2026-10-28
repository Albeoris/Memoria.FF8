namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect318State? Effect318
    {
        get => effectState.Effect318;
        init => effectState = effectState with { Effect318 = value };
    }
}
