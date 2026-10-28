namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect301State? Effect301
    {
        get => effectState.Effect301;
        init => effectState = effectState with { Effect301 = value };
    }
}
