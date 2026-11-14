namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState DrawNativeForeignEffectModel5099D0(NativeBattleGraphicsState graphics, int index, NativeBattleForeignModelGeometry geometry, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int shadowTableBase, byte shadowDepthShift, byte bodyDepthShift, uint? privateOrderingAddress, short? depthCue, bool drawShadow, NativeBattlePilotVisualIdentity? pilotIdentity, bool pilotScreenSpace, bool pilotContinuous)
    {
        NativeBattleGraphicsModel model = graphics.Models[index];
        if (drawShadow)
        {
            graphics = SubmitNativeShadowPackets5088A0(graphics, model, projection, shadowTableBase, false, shadowDepthShift, privateOrderingAddress, pilotScreenSpace, pilotContinuous, pilotIdentity);
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Host body drawing requires its retained packet memory.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Host body drawing requires its current renderer process.");
        NativeBattleGraphicsDefinition rules = definition.NativeGraphics ?? throw new NotSupportedException("Host body drawing requires its actual graphics definition.");
        var gate = process.Enter5099D0(rules.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Host body drawing requires its actual renderer capability."));
        NativeEffectMemory = memory with { PrimitiveProcess = gate.Process };
        if (!gate.Draw)
        {
            return graphics;
        }
        uint color = model.Color40 ?? throw new NotSupportedException("Host body drawing requires its retained model color.");
        process = gate.Process.PublishModelColor(color);
        NativeBattlePilotVisualIdentity identity = pilotIdentity ?? NativePilotSceneIdentity(0x5099D0, index) ?? throw new NotSupportedException("Host body drawing requires its current visual occurrence.");
        NativeBattleRawModelBodyDraw drawn = NativeBattleForeignModelDrawing.DrawBody(geometry, matrix, projection, color, rules.Rules.SummonColors ?? throw new NotSupportedException("Host body drawing requires its authenticated color arithmetic."), identity, bodyDepthShift, new(0, 0, 320, 216), 17, depthCue, pilotScreenSpace, pilotContinuous);
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Host body drawing requires its initialized ordering table.");
        if (privateOrderingAddress is uint table)
        {
            NativeBattleRinoa89Frame capture = new(memory with { PrimitiveProcess = process }, graphics.OriginalEffect ?? throw new NotSupportedException("Private host body drawing requires its effect owner."), NativeFileStaging ?? throw new NotSupportedException("Private host body drawing requires its staging owner."), nativeCrtRandom ?? throw new NotSupportedException("Private host body drawing requires its CRT owner."));
            NativeBattleRinoa89CaptureStep written = NativeBattleRinoa89Capture.AppendCommon(capture, primitive.Pending, drawn.Packets, table);
            NativeEffectMemory = written.Frame.Memory with { PrimitiveProcess = PublishCursor(written.Frame.Memory.PrimitiveProcess!) };
            return graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        }
        NativeBattleCommonPacketWrite appended = NativeBattleCommonPacketArena.Append(process, primitive.Pending, drawn.Packets);
        NativeEffectMemory = memory with { PrimitiveProcess = PublishCursor(appended.Process) };
        return graphics with { PrimitiveFrame = primitive with { Pending = appended.Ordering } };

        static NativeBattlePrimitiveProcess PublishCursor(NativeBattlePrimitiveProcess current)
        {
            uint bank = current.PacketBank switch { "off_B6D070" => 0x1901018u, "off_B6D094" => 0x199B018u, _ => throw new NotSupportedException("Host body drawing requires its actual common packet bank.") };
            return current.Publish50FDF0(checked(bank + (uint)current.PacketBuffer * (uint)current.CommonPacketNominalCapacity + (uint)(current.ByteCursor ?? throw new NotSupportedException("Host body drawing requires its actual common cursor."))), null);
        }
    }
}
