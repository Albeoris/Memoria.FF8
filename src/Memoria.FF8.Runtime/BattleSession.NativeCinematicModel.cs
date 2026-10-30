using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicModel(NativeBattleWaterExecutionFrame frame, int secondFileLength, NativeBattleCinematicModelServices services, NativeBattleGraphicsState graphics, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame>? prepare = null)
    {
        NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, secondFileLength);
        int index = NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, bank);
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native cinematic model requires its current camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native cinematic model requires its current projection registers.");
        if (prepare is null)
        {
            ushort focal = projection.FocalLength ?? throw new NotSupportedException("Native cinematic model requires its current GTE focal register.");
            NativeBattleWaterMeshPrepared prepared = NativeBattleWaterMeshPreparation.PrepareWrapper(frame, bank, index, 2, unchecked((short)focal));
            projection = projection with { FocalLength = unchecked((ushort)prepared.Focal) };
            NativeCamera = camera with { Projection = projection };
            frame = NativeBattleCinematicModel.PrepareDraw(prepared.Frame, services);
        }
        else
        {
            frame = prepare(frame);
        }
        NativeBattleEffectMemory memory = RequireWaterMemory();
        NativeBattlePrimitiveProcess common = memory.PrimitiveProcess ?? throw new NotSupportedException("Native cinematic model requires its retained primitive process.");
        bool hardware = definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native cinematic model requires its selected hardware capability.");
        var gate = common.Enter5099D0(hardware);
        common = gate.Process;
        memory = memory with { Arena = frame.Arena, ScriptedEffects = frame.Core, PrimitiveProcess = common };
        if (!gate.Draw)
        {
            NativeEffectMemory = memory;
            return (frame, graphics);
        }
        uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        uint color = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(model + 0x5C), 4), 4));
        NativeBattleModelRendererRegisters renderer = (common.ModelRendererRegisters ?? new(null, null, null, null, null)).PublishModelColor(color);
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native cinematic model requires its original color arithmetic tables.");
        NativeBattleCinematicModelDraw drawn = NativeBattleCinematicModel.Render(frame, model, projection, colors, OtBucket, NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, bank, index) with { ResourceGeneration = graphics.OriginalEffect.Binding.Effect is 18 or 208 or 211 ? model : 0 });
        frame = drawn.Frame;
        uint manager = CoreDword(frame, 0x27973EC);
        uint start = CoreDword(frame, unchecked(manager + 0x7C));
        uint end = unchecked(start + (uint)drawn.Packets.AllocationBytes);
        frame = WriteCoreDword(frame, unchecked(manager + 0x7C), end);
        if (drawn.SourceCursor is uint sourceCursor)
        {
            renderer = renderer.Publish50FDF0(end, sourceCursor);
        }
        common = common with { ModelRendererRegisters = renderer };
        memory = memory with { Arena = frame.Arena, ScriptedEffects = frame.Core, PrimitiveProcess = common };
        if (!drawn.Packets.Attempts.IsEmpty)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native cinematic model requires its current ordering table.");
            if ((ReadSlot(frame, 0x4C, 1)[0] & 1) != 0)
            {
                if (graphics.OriginalEffect is { Binding.Effect: 208 or 211 } selected)
                {
                    graphics = graphics with { OriginalEffect = selected with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(selected.PresentationPackets, "20DFAB8", 0, ArenaOffset(start, 0), drawn.Packets) } };
                }
                NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, ArenaOffset(start, 0), primitive.Pending, drawn.Packets);
                memory = written.Memory;
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            }
            else
            {
                if (start != common.ByteCursor)
                {
                    throw new InvalidDataException("Native cinematic model COMMON cursor disagrees with its current physical owner.");
                }
                if (graphics.OriginalEffect is { Binding.Effect: 208 or 211 } selected)
                {
                    graphics = graphics with { OriginalEffect = selected with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(selected.PresentationPackets, common.PacketBank!, common.PacketBuffer, common.ByteCursor!.Value, drawn.Packets) } };
                }
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(common, primitive.Pending, drawn.Packets);
                memory = memory with { PrimitiveProcess = written.Process };
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            }
        }
        NativeEffectMemory = memory;
        return (frame with { Arena = memory.Arena!, Core = memory.ScriptedEffects! }, graphics);
    }
}
