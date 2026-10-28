namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect30State? Effect30
    {
        get => effectState.Effect30;
        init => effectState = effectState with { Effect30 = value };
    }
}
