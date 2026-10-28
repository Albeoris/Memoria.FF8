namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect345State? Effect345
    {
        get => effectState.Effect345;
        init => effectState = effectState with { Effect345 = value };
    }
}
