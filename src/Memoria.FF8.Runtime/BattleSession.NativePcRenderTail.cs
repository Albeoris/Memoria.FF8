namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState PublishNativePcRenderTail47CF60(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory? memory = NativeEffectMemory;
        if (memory?.PcRenderRequest is not { } request || graphics.PrimitiveFrame is not { } frame)
        {
            return graphics;
        }
        bool pause = request.Requested != 0 && (graphics.UiServices ?? throw new NotSupportedException("Native PC render tail requires its current UI pause owner.")).PauseDE9;
        NativeBattlePcRenderViewport? viewport = null;
        if (request.Requested != 0 && !pause)
        {
            NativeSoftwareViewportInputs inputs = memory.PrimitiveProcess?.Software?.ViewportInputs ?? throw new NotSupportedException("Native PC render tail requires its current selected device viewport.");
            viewport = new(inputs.OffsetX, inputs.OffsetY, inputs.Width, inputs.Height);
            NativeBattlePcFramebufferCaptureProfile profile = definition.NativeGraphics?.PrimitiveCapabilities?.PcFramebufferCaptureProfile ?? throw new NotSupportedException("Native PC render tail requires its explicitly selected framebuffer capture profile.");
            profile.ResolveMaterial();
        }
        request = request.PublishTail47CF60(pause, viewport);
        NativeEffectMemory = memory with { PcRenderRequest = request };
        return graphics with { PrimitiveFrame = frame with { LastPcRenderTail = request.PublishedTail } };
    }
}
