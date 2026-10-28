namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect322State? Effect322
    {
        get => effectState.Effect322;
        init => effectState = effectState with { Effect322 = value };
    }
}
