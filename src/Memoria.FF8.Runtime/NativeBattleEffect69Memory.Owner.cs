namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect69State? Effect69
    {
        get => effectState.Effect69;
        init => effectState = effectState with { Effect69 = value };
    }
}
