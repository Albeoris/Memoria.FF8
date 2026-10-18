namespace Memoria.FF8.Runtime;

public static class NativeBattleChoco98Director
{
    public const uint Callback = 0x722210;

    public static NativeBattleChoco97DirectorStep Advance722210(NativeBattleChoco97DirectorFrame frame, NativeBattleChoco97DirectorInputs inputs) => NativeBattleChoco97Director.AdvanceFamily(frame, inputs, true);

    public static (uint Primary, uint Secondary) SetFreeze728180(uint primary, uint secondary, uint value) => NativeBattleChoco97Director.SetFreeze730610(primary, secondary, value);
}
