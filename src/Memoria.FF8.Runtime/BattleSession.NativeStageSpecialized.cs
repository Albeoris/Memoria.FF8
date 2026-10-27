using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeSpecializedStage(NativeBattleGraphicsState graphics, NativeBattleStageState stage, NativeBattleStageTask task)
    {
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native specialized stage requires its retained camera owner.");
        short shake = camera.ScreenOffsetScratchY ?? (camera.ProjectionOffsetsCleared ? (short)0 : throw new NotSupportedException("Native specialized stage requires its screen-offset Y WORD."));
        NativeBattleStageSpecializedStep step = NativeBattleStageSpecialized.Advance(stage.Specialized ?? throw new NotSupportedException("Native specialized stage requires its retained process state."), task.Callback.Address, task.Phase, stage.Flags, stage.RotationSpeed, shake, DrawGlobal, DrawCrt);
        NativeStage = stage with { Specialized = step.State, Task = task with { Phase = step.Phase }, RotationSpeed = step.RotationSpeed };
        if (step.CameraShake != shake)
        {
            NativeCamera = camera with { ScreenOffsetScratchY = step.CameraShake, ProjectionOffsetsCleared = false };
        }
        if (step.Texture0)
        {
            AdvanceNativeStageTexture(0);
        }
        if (step.Overlay is { } overlay)
        {
            graphics = SubmitNativeSpecializedStageOverlay(graphics, overlay);
        }
        return step.StandardStage ? DrawNativeStandardStage(graphics) : graphics;

        uint DrawGlobal()
        {
            NativeGlobalRandomState random = nativeGlobalRandom ?? throw new NotSupportedException("Native515730 requires its534AA0 random owner.");
            nativeGlobalRandom = random.Draw(out ushort value);
            nativeGlobalRandomCalls = checked(nativeGlobalRandomCalls + 1);
            return value;
        }

        int DrawCrt()
        {
            NativeCrtRandomState random = nativeCrtRandom ?? throw new NotSupportedException("Native515730 requires its55CBD2 random owner.");
            nativeCrtRandom = random.Draw(out ushort value);
            return value;
        }
    }

    private NativeBattleGraphicsState SubmitNativeSpecializedStageOverlay(NativeBattleGraphicsState graphics, NativeBattleStageSpecializedOverlay overlay)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native stage color overlay requires its retained effect process.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native stage color overlay requires its primitive process.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native stage color overlay requires its physical framebuffer selector.");
        uint source = definition.NativeStageSource?.SpecializedColor ?? throw new NotSupportedException("Native56CA00 requires its original stage color record.");
        process = process.PublishFarColor45DDA0(0, 0, 0);
        NativeBattleScriptedDpcsResult color = NativeBattleScriptedDpcs.Transform45F270(memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native56CA00 requires its retained GTE color registers."), source, unchecked((uint)overlay.Cue), process.ModelRendererRegisters ?? throw new NotSupportedException("Native56CA00 requires its far-color controls."), definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native45F270 requires its authenticated color constants."));
        NativeBattleStageSpecializedState specialized = NativeStage!.Specialized!;
        NativeStage = NativeStage with { Specialized = specialized with { PublishedColor = (specialized.PublishedColor & 0xFFFFFF00u) | (byte)color.PackedRgb } };
        NativeEffectMemory = memory with { PrimitiveProcess = process, ScriptedColorRegisters = color.Registers };
        NativeBattleStageSpecializedPacketStep packets = NativeBattleStageSpecializedPackets.Build(NativeStage.Specialized!, overlay.Callback, buffer, color.PackedRgb);
        NativeBattleStageSpecializedState state = packets.State;
        NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native stage color overlay requires its initialized ordering table.");
        using NativeBattleLogicalOrdering.Builder links = new(frame.Pending);
        foreach (NativeBattleStageSpecializedPacket packet in packets.Packets)
        {
            ImmutableArray<uint> words = Enumerable.Range(0, packet.Bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(packet.Bytes.AsSpan().Slice(index * 4))).ToImmutableArray();
            NativeBattlePrimitiveReference reference = new(packet.Bank, 0, packet.Offset);
            links.InvalidatePacketRange(reference, packet.Bytes.Length);
            bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            links.Prepend(1, reference, words, packet.Flags, [0, 0, 0, 0]);
            if (linked)
            {
                state = NativeBattleStageSpecializedPackets.Write(state, overlay.Callback, packet.Offset, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink).ToImmutableArray());
            }
        }
        NativeStage = NativeStage with { Specialized = state };
        return graphics with { PrimitiveFrame = frame with { Pending = links.ToImmutable() } };
    }
}
