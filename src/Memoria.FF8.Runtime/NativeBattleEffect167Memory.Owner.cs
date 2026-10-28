namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect167State? Effect167
    {
        get => effectState.Effect167;
        init => effectState = effectState with { Effect167 = value };
    }
}
