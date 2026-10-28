namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect173State? Effect173
    {
        get => effectState.Effect173;
        init => effectState = effectState with { Effect173 = value };
    }
}
