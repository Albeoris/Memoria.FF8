namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect313State? Effect313
    {
        get => effectState.Effect313;
        init => effectState = effectState with { Effect313 = value };
    }
}
