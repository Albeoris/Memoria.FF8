namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect64State? Effect64
    {
        get => effectState.Effect64;
        init => effectState = effectState with { Effect64 = value };
    }
}
