namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeDirectGraphics(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte phase)
    {
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[context.HeaderIndex];
        bool control = context.Callback == 0x50BDC0;
        ushort action = NativeBattleActionGraphics.Word(header, 4);
        if (phase == 0)
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native direct effect service requires its actual retained process memory.");
            _ = memory.RequireDirectEntry21DFEC4(graphics.OriginalEffect);
            if (control)
            {
                graphics = PublishNativeOriginalEffectTitle(graphics, context);
                context = graphics.ActionGraphics ?? context;
            }
            graphics = PrepareNativeOriginalEffectParameters(graphics, context);
            memory = memory.SetDirectParameters(context.Callback, header.ReadByte(1), action, header.ReadByte(6));
            NativeEffectMemory = memory;
            NativeBattleOriginalEffectState current = graphics.OriginalEffect!;
            uint? descriptor = current.ReturnedDescriptor;
            graphics = graphics with { OriginalEffect = current with { Parameters = current.Parameters! with { Parameter80 = memory.Parameter80, Parameter82 = memory.Parameter82 } } };
            graphics = InvokeNativeOriginalEffect(graphics);
            graphics = graphics with { OriginalEffect = graphics.OriginalEffect! with { ReturnedDescriptor = descriptor } };
            return (graphics with { Flags = graphics.Flags | 0x10 }, 1, false);
        }
        if (phase != 1)
        {
            return (graphics, phase, false);
        }
        bool waitForEffect = control ? action is 0xFFFC or 0xFFFA : header.ReadByte(1) != 0xED;
        if (waitForEffect)
        {
            if (NativeOriginalEffectBusy(graphics))
            {
                return (graphics, phase, false);
            }
            graphics = CompleteNativeHeaderGraphics(graphics);
            if (!control || action == 0xFFFC)
            {
                graphics = graphics with { Flags = graphics.Flags & ~0x10u };
            }
            return (graphics, phase, true);
        }
        byte ready = graphics.OriginalEffect?.Parameters?.Parameter81 ?? throw new NotSupportedException("Native direct service completion requires its current module parameter BYTE81.");
        return (graphics, phase, ready == 1 && NativeCameraTaskMask == 0);
    }
}
