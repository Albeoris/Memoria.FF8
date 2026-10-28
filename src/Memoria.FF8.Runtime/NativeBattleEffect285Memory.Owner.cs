namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect285State? Effect285
    {
        get => effectState.Effect285;
        init => effectState = effectState with { Effect285 = value };
    }
}
