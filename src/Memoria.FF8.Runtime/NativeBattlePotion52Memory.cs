namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleCuragaExecutableState? Potion52Executable
    {
        get => effectState.Potion52Executable;
        init => effectState = effectState with { Potion52Executable = value };
    }
}
