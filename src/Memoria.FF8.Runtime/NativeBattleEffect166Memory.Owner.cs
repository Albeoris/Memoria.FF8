namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect166State? Effect166
    {
        get => effectState.Effect166;
        init => effectState = effectState with { Effect166 = value };
    }
}
