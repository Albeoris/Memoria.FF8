namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect316State? Effect316
    {
        get => effectState.Effect316;
        init => effectState = effectState with { Effect316 = value };
    }
}
