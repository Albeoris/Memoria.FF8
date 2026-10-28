namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect83State? Effect83
    {
        get => effectState.Effect83;
        init => effectState = effectState with { Effect83 = value };
    }
}
