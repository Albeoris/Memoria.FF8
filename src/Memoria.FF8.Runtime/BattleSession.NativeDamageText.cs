namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleDamageTextAnchor ProjectNativeDamageText(NativeBattleGraphicsState graphics, byte index)
    {
        NativeBattleVector point = ReadNativeGraphicsBone(graphics, index, 240, 4096);
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native5069B0 requires its actual camera projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native5069B0 requires its actual view matrix.");
        uint screen = NativeBattleModuleProjection.Project45FAA0(view, point, projection, 0, 0).Screen;
        return NativeBattleDamageText.Project5069B0(screen);
    }
}
