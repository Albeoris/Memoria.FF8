namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect70State? Effect70
    {
        get => effectState.Effect70;
        init => effectState = effectState with { Effect70 = value };
    }
}
