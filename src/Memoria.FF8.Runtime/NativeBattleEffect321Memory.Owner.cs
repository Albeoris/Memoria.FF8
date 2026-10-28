namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect321State? Effect321
    {
        get => effectState.Effect321;
        init => effectState = effectState with { Effect321 = value };
    }
}
