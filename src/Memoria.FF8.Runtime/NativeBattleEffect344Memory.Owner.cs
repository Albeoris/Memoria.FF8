namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect344State? Effect344
    {
        get => effectState.Effect344;
        init => effectState = effectState with { Effect344 = value };
    }
}
