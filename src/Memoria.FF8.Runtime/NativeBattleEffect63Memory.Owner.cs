namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect63State? Effect63
    {
        get => effectState.Effect63;
        init => effectState = effectState with { Effect63 = value };
    }
}
