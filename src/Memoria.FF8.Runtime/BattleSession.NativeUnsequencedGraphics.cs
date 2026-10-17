namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeUnsequencedGraphics(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte phase)
    {
        if (phase == 0)
        {
            graphics = PublishNativeOriginalEffectTitle(graphics, context);
            graphics = BeginNativeOriginalEffect(graphics, NativeBattleActionGraphics.Word(nativeActionPackets.Headers[context.HeaderIndex], 6), destination21DFEC0: true);
            return (graphics, 1, false);
        }
        if (phase == 1)
        {
            int result = NativeEffectIoResult ?? throw new NotSupportedException("Native50BC20 requires its actual signed effect IO result.");
            return (graphics, result < 0 ? phase : (byte)2, false);
        }
        if (phase == 2)
        {
            if (graphics.ModuleEffects.PublishedCount != 0 || NativeCameraTaskMask != 0)
            {
                return (graphics, phase, false);
            }
            graphics = PrepareNativeOriginalEffectParameters(graphics, context);
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native50BC20 requires its actual camera selector owner.")).SetActionSelector(65535);
            return (InvokeNativeOriginalEffect(graphics), 3, false);
        }
        if (phase == 3)
        {
            if (NativeOriginalEffectBusy(graphics))
            {
                return (graphics, phase, false);
            }
            graphics = CompleteNativeHeaderGraphics(graphics);
            NativeEffectMemory = (NativeEffectMemory ?? throw new InvalidDataException("Native50BC20 lost its effect memory owner.")) with { Active = 0 };
            return (graphics with { Flags = graphics.Flags & ~0x10u }, phase, true);
        }
        return (graphics, phase, false);
    }
}
