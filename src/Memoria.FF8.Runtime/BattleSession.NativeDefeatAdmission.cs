namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void BeginNativeDefeat486450()
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        if (graphics.NativeOutcome?.ResultByte6E7 is > 0)
        {
            return;
        }
        NativeGraphics = BuildNativeTerminalGraphics(graphics, BattleOutcome.Defeat);
    }
}
