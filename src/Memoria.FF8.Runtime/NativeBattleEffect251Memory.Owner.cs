namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect251State? Effect251
    {
        get => effectState.Effect251;
        init => effectState = effectState with { Effect251 = value };
    }
}
