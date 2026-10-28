namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect140State? Effect140
    {
        get => effectState.Effect140;
        init => effectState = effectState with { Effect140 = value };
    }
}
