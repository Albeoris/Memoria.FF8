namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect79State? Effect79
    {
        get => effectState.Effect79;
        init => effectState = effectState with { Effect79 = value };
    }
}
