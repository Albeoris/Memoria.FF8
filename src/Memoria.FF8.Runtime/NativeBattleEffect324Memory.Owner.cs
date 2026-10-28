namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect324State? Effect324
    {
        get => effectState.Effect324;
        init => effectState = effectState with { Effect324 = value };
    }
}
