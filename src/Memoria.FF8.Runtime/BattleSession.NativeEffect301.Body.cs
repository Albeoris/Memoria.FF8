using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect301Frame DrawNativeEffect301Body(ref NativeBattleGraphicsState graphics, NativeBattleEffect301Frame frame, NativeBattleEffect301DrawBody66E300 operation, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs)
    {
        int actor = NativeEffect301Actor(graphics, operation.ModelAddress);
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect301 requires its current caster skeleton.");
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect301 requires its current caster world matrix.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect301 requires its current temporary memory."), 136);
        frame = frame with { Staging = allocation.Staging };
        NativeBattleEffectArena context = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 136);
        context = context.Write(0x6C, BitConverter.GetBytes(operation.ThresholdY)).Write(0x70, BitConverter.GetBytes(operation.Age)).Write(0x74, BitConverter.GetBytes(unchecked(world.Z - 800))).Write(4, BitConverter.GetBytes(operation.Scratch));
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model, world);
        context = context.Write(0x0C, new byte[4]).Write(0x10, BitConverter.GetBytes((short)320)).Write(0x12, BitConverter.GetBytes((short)216)).Write(0x14, BitConverter.GetBytes(model.Color40 ?? throw new NotSupportedException("Effect301 requires its current caster color."))).Write(0x18, BitConverter.GetBytes((model.SequenceState ?? throw new NotSupportedException("Effect301 requires its current caster object mask.")).MeshMask124)).MarkUnknown(0x7C, 4).Write(0x80, new byte[4]);
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(frame.Staging!, allocation.Address, context, 136) };
        NativeDeformedBodyBridge bridge = new(frame.Memory, frame.Effect, frame.Random, graphics, model, frame.Staging!, context, allocation.Address);
        NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(bridge, false);
        uint address = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect301 requires its actual current geometry address.");
        ImmutableArray<NativeBattlePackedMatrix32> bones = NativeBattleBodyBoneMatrices.Require(model, source).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect301 requires its current common packet process.");
        int cursor = process.ByteCursor ?? throw new NotSupportedException("Effect301 requires its current common packet cursor.");
        NativeBattleEffect301BodyStep drawn = NativeBattleEffect301BodyDrawing.DrawLoaded(frame, (offset, length) => ReadNativeDeformedGeometry(bridge, pointer, offset, length), address, bones, context, allocation.Address, cursor, resources, inputs);
        frame = drawn.Frame with { Staging = NativeBattleTemporaryMemory.PublishView(drawn.Frame.Staging!, allocation.Address, drawn.Context136, 136) };
        process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect301 lost its current common packet process.");
        NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(drawn.Batch, cursor, process.CommonPacketWriteCapacity, offset =>
        {
            (byte[] bytes, bool[] known) = process.ReadCommonPacketSnapshot(offset, 1);
            return (bytes[0], known[0]);
        });
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect301 requires its current ordering table.");
        NativeBattleCommonPacketWrite delivered = NativeBattleCommonPacketArena.Append(process, primitive.Pending, resolved);
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = delivered.Ordering } };
        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = delivered.Process } };
        NativeBattleTemporaryAllocation restore = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, 88);
        NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(model, source, NativeBattleTemporaryMemory.ReadView(restore.Staging, restore.Address, 88));
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(restore.Staging, restore.Address, prepared.Temporary58, 88);
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(NativeBattleTemporaryMemory.Release5082D0(staging, 88), 136) };
        graphics = graphics with { Models = graphics.Models.SetItem(actor, prepared.Model) };
        return frame;
    }
}
