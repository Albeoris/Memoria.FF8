namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect76State? Effect76
    {
        get => effectState.Effect76;
        init => effectState = effectState with { Effect76 = value };
    }
}
