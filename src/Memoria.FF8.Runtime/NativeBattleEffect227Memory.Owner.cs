namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect227State? Effect227
    {
        get => effectState.Effect227;
        init => effectState = effectState with { Effect227 = value };
    }
}
