namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect315State? Effect315
    {
        get => effectState.Effect315;
        init => effectState = effectState with { Effect315 = value };
    }
}
