namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeReflectionGraphics(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte phase)
    {
        if (phase == 0)
        {
            if (NativeBattleActionGraphics.IsModelBusy(graphics.Models[context.ActorIndex], 0x40) || NativeHeaderTargetsBusy(graphics, context))
            {
                return (graphics, phase, false);
            }
            phase = 1;
        }
        if (phase == 1)
        {
            graphics = graphics with { Flags = graphics.Flags | 0x10 };
            graphics = InvokeNativeOriginalEffect(graphics, context, 1);
            context = context with { ReflectionWatchdog4A = 0 };
            return (graphics with { ActionGraphics = context }, 2, false);
        }
        if (phase != 2)
        {
            throw new InvalidDataException("Native50B0C0 reached an invalid descriptor phase.");
        }
        if (!NativeOriginalEffectBusy(graphics))
        {
            graphics = CompleteNativeHeaderGraphics(graphics);
            return (graphics with { Flags = graphics.Flags & ~0x10u }, phase, true);
        }
        //50B0C0 only advances its WORD watchdog after the returned module descriptor has gone away.
        if (NativeOriginalEffectDescriptorReleased(graphics))
        {
            ushort watchdog = AdvanceNativeEffectSoundWatchdog(context.ReflectionWatchdog4A ?? throw new InvalidDataException("Native50B0C0 lost its initialized watchdog WORD."));
            graphics = graphics with { ActionGraphics = context with { ReflectionWatchdog4A = watchdog } };
        }
        return (graphics, phase, false);
    }
}
