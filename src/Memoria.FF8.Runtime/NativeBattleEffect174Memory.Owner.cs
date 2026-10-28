namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect174State? Effect174
    {
        get => effectState.Effect174;
        init => effectState = effectState with { Effect174 = value };
    }
}
