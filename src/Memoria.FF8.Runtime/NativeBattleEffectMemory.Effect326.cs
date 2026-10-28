namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleEffect326State? Effect326
    {
        get => effectState.Effect326;
        init => effectState = effectState with { Effect326 = value };
    }
}
