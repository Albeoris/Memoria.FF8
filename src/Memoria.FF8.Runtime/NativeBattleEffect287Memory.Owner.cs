namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect287State? Effect287
    {
        get => effectState.Effect287;
        init => effectState = effectState with { Effect287 = value };
    }
}
