namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect319State? Effect319
    {
        get => effectState.Effect319;
        init => effectState = effectState with { Effect319 = value };
    }
}
