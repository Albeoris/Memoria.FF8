namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect323State? Effect323
    {
        get => effectState.Effect323;
        init => effectState = effectState with { Effect323 = value };
    }
}
