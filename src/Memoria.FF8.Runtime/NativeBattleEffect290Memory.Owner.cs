namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect290State? Effect290
    {
        get => effectState.Effect290;
        init => effectState = effectState with { Effect290 = value };
    }
}
