namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect210State? Effect210
    {
        get => effectState.Effect210;
        init => effectState = effectState with { Effect210 = value };
    }
}
