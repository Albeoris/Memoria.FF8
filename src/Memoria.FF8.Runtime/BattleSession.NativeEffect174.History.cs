namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleEffect174Frame Frame, NativeBattleGraphicsState Graphics) DrawNativeEffect174History(NativeBattleEffect174Frame frame, NativeBattleGraphicsState graphics, int actor)
    {
        var drawn = DrawNativePrimaryHistory76(frame.Memory, frame.Effect, frame.Staging, graphics, actor);
        return (frame with { Memory = drawn.Memory, Staging = drawn.Staging }, drawn.Graphics);
    }
}
