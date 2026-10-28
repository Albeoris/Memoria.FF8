namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect209State? Effect209
    {
        get => effectState.Effect209;
        init => effectState = effectState with { Effect209 = value };
    }
}
