namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect308State? Effect308
    {
        get => effectState.Effect308;
        init => effectState = effectState with { Effect308 = value };
    }
}
