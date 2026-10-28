namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect312State? Effect312
    {
        get => effectState.Effect312;
        init => effectState = effectState with { Effect312 = value };
    }
}
