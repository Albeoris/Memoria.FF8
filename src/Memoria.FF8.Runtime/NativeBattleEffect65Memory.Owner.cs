namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect65State? Effect65
    {
        get => effectState.Effect65;
        init => effectState = effectState with { Effect65 = value };
    }
}
