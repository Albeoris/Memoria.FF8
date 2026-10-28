namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect187State? Effect187
    {
        get => effectState.Effect187;
        init => effectState = effectState with { Effect187 = value };
    }
}
