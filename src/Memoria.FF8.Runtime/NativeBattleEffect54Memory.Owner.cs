namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect54State? Effect54
    {
        get => effectState.Effect54;
        init => effectState = effectState with { Effect54 = value };
    }
}
