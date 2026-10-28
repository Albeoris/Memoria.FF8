namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect218State? Effect218
    {
        get => effectState.Effect218;
        init => effectState = effectState with { Effect218 = value };
    }
}
