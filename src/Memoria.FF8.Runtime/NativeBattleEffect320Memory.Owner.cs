namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect320State? Effect320
    {
        get => effectState.Effect320;
        init => effectState = effectState with { Effect320 = value };
    }
}
