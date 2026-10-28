namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect66State? Effect66
    {
        get => effectState.Effect66;
        init => effectState = effectState with { Effect66 = value };
    }
}
