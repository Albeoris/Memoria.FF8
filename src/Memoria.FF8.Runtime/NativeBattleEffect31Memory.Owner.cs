namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect31State? Effect31
    {
        get => effectState.Effect31;
        init => effectState = effectState with { Effect31 = value };
    }
}
