using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleResourcePointer NativeEffect308SkeletonPointer(NativeBattleGraphicsState graphics, NativeBattleEffect308Frame frame, int actor)
    {
        NativeBattleGraphicsModel model = graphics.Models[actor];
        if (model.ActiveStagedResourceRow100 is uint staged)
        {
            NativeBattleResourcePointer? pointer;
            if (!NativeBattleEffectTaskMemory.TryReadPointer(frame.Effect.Pools, staged, out pointer))
            {
                pointer = IsNativeEffectArenaAddress(staged) ? frame.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(staged, 4)) : (frame.Staging ?? throw new NotSupportedException("Effect308 requires its current addressed memory.")).ReadResourcePointer(staged);
            }
            return pointer ?? throw new NotSupportedException("Effect308 requires its actual skeleton directory pointer.");
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect308 requires its current CPU resources.");
        return new(cpu.Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("Effect308 requires its current physical resource row.")].Require(12), null);
    }

    private NativeBattleEffect308Frame WriteNativeEffect308BonePadding(NativeBattleGraphicsState graphics, NativeBattleEffect308Frame frame, int actor, int bone, ushort value)
    {
        NativeBattleResourcePointer pointer = NativeEffect308SkeletonPointer(graphics, frame, actor);
        int offset = checked(32 + bone * 48 + 18);
        byte[] bytes = BitConverter.GetBytes(value);
        if (pointer.Cpu is { Bank: < 3 } reference)
        {
            NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect308 lost its current CPU resources.");
            NativeBattleModelCpuBank bank = cpu.Cpu.Banks[reference.Bank].Write(checked(reference.Offset + offset), bytes);
            NativeModelCpuResources = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(reference.Bank, bank) } };
            return frame;
        }
        uint address = checked((pointer.Cpu?.SharedAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect308 requires its actual shared skeleton owner.")) + (uint)offset);
        return NativeBattleEffect308Controller.WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, bytes), 2);
    }

    private ImmutableArray<NativeBattlePackedMatrix32> ReadNativeEffect308PackedBones(NativeBattleGraphicsState graphics, NativeBattleEffect308Frame frame, int actor, NativeBattlePoseSource source)
    {
        NativeBattleResourcePointer pointer = NativeEffect308SkeletonPointer(graphics, frame, actor);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], source);
        var packed = ImmutableArray.CreateBuilder<NativeBattlePackedMatrix32>(bones.Length);
        for (int bone = 0; bone < bones.Length; bone++)
        {
            int offset = checked(32 + bone * 48 + 18);
            NativeBattleEffectArena padding = NativeBattleEffectArena.Clear().MarkUnknown(0, 2);
            if (pointer.Cpu is { Bank: < 3 } reference)
            {
                NativeBattleModelCpuBank bank = (NativeModelCpuResources ?? throw new NotSupportedException("Effect308 lost its current CPU resources.")).Cpu.Banks[reference.Bank];
                int first = checked(reference.Offset + offset);
                foreach (NativeBattleEffectMemoryRegion region in bank.KnownRanges)
                {
                    int start = Math.Max(first, region.Offset);
                    int end = Math.Min(first + 2, region.Offset + region.Data.Length);
                    if (start < end)
                    {
                        padding = padding.Write(start - first, region.Data.AsSpan().Slice(start - region.Offset, end - start));
                    }
                }
            }
            else
            {
                uint address = checked((pointer.Cpu?.SharedAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect308 requires its actual shared skeleton owner.")) + (uint)offset);
                padding = NativeBattleEffect308Controller.ReadBytes(frame, address, 2);
            }
            (byte[] bytes, bool[] known) = padding.ReadSnapshot(0, 2);
            NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(bones[bone], null);
            packed.Add(matrix with { Bytes = matrix.Bytes.SetItem(18, bytes[0]).SetItem(19, bytes[1]), Known = matrix.Known.SetItem(18, known[0]).SetItem(19, known[1]) });
        }
        return packed.MoveToImmutable();
    }

    private (uint Address, Func<int, int, byte[]> Read) ReadNativeEffect308Geometry(NativeBattleGraphicsState graphics, NativeBattleEffect308Frame frame, int actor)
    {
        NativeDeformedBodyBridge bridge = new(frame.Memory, frame.Effect, frame.Random, graphics, graphics.Models[actor], frame.Staging ?? throw new NotSupportedException("Effect308 requires its current addressed memory."), NativeBattleEffectArena.Clear(), 0);
        NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(bridge, false);
        uint address = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect308 requires its actual current geometry address.");
        return (address, (offset, length) => ReadNativeDeformedGeometry(bridge, pointer, offset, length));
    }

    private NativeBattleEffect308Frame PrepareNativeEffect308Pose(ref NativeBattleGraphicsState graphics, NativeBattleEffect308Frame frame, int actor, NativeBattlePoseSource source, NativeBattleEffectArena offsets)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect308 requires its current temporary memory."), 96);
        NativeBattleEffect298PosePreparation prepared = NativeBattleEffect298Pose.Prepare662C00(graphics.Models[actor], source, offsets, NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 96));
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary96, 96);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, prepared.Model) };
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 96) };
        NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(prepared.Model.SequenceState ?? throw new NotSupportedException("Effect308 requires its current decoded pose."), source);
        return PublishNativeEffect308RootPadding(graphics, frame, actor, source, decoded.HasScale);
    }

    private NativeBattleEffect308Frame RestoreNativeEffect308Pose(ref NativeBattleGraphicsState graphics, NativeBattleEffect308Frame frame, int actor, NativeBattlePoseSource source)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect308 requires its current temporary memory."), 88);
        NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[actor], source, NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88));
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, prepared.Model) };
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88) };
        NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(prepared.Model.SequenceState ?? throw new NotSupportedException("Effect308 requires its current decoded pose."), source);
        return PublishNativeEffect308RootPadding(graphics, frame, actor, source, decoded.HasScale);
    }

    private NativeBattleEffect308Frame PublishNativeEffect308RootPadding(NativeBattleGraphicsState graphics, NativeBattleEffect308Frame frame, int actor, NativeBattlePoseSource source, bool hasScale)
    {
        if (!hasScale)
        {
            for (int bone = 0; bone < source.BoneCount; bone++)
            {
                if (BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(16 + bone * 48, 2)) < 0)
                {
                    frame = WriteNativeEffect308BonePadding(graphics, frame, actor, bone, 0);
                }
            }
        }
        return frame;
    }
}
