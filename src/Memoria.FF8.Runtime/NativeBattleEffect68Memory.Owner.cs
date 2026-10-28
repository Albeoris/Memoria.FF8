namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect68State? Effect68
    {
        get => effectState.Effect68;
        init => effectState = effectState with { Effect68 = value };
    }
}
