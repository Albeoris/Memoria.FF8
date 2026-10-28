namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect283State? Effect283
    {
        get => effectState.Effect283;
        init => effectState = effectState with { Effect283 = value };
    }
}
