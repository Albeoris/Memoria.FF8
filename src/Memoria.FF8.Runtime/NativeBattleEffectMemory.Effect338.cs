namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect338State? Effect338
    {
        get => effectState.Effect338;
        init => effectState = effectState with { Effect338 = value };
    }
}
