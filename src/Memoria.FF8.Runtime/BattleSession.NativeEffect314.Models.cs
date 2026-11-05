using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleResourcePointer NativeEffect314SkeletonPointer(NativeBattleGraphicsState graphics, NativeBattleEffect314Frame frame, int actor)
    {
        NativeBattleGraphicsModel model = graphics.Models[actor];
        if (model.ActiveStagedResourceRow100 is uint staged)
        {
            NativeBattleResourcePointer? pointer;
            if (!NativeBattleEffectTaskMemory.TryReadPointer(frame.Effect.Pools, staged, out pointer))
            {
                pointer = IsNativeEffectArenaAddress(staged) ? frame.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(staged, 4)) : frame.Staging!.ReadResourcePointer(staged);
            }
            return pointer ?? throw new NotSupportedException("Effect314 requires its selected skeleton owner.");
        }
        NativeBattleModelCpuResourceRow row = (NativeModelCpuResources ?? throw new NotSupportedException("Effect314 requires its current CPU owner.")).Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("Effect314 requires its selected resource row.")];
        return new(row.Require(12), null, row.Lengths.TryGetValue(12, out int length) ? length : null);
    }

    private NativeBattlePackedMatrix32 ReadNativeEffect314BoneMatrix(NativeBattleGraphicsState graphics, NativeBattleEffect314Frame frame, uint model, int bone)
    {
        int actor = NativeEffect314Actor(graphics, model);
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect314 requires its current rig.");
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], source);
        if ((uint)bone >= (uint)matrices.Length)
        {
            throw new InvalidDataException("Effect314 matrix selector leaves its current rig.");
        }
        NativeBattleResourcePointer pointer = NativeEffect314SkeletonPointer(graphics, frame, actor);
        (byte[] data, bool[] known) = NativeBattleModelSectionSnapshot.Read(NativeModelCpuResources, frame.Staging!, pointer, checked(32 + bone * 48 + 18), 2, (address, length) => NativeBattleEffect314Controller.ReadBytes(frame, address, length).ReadSnapshot(0, length));
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrices[bone], null);
        return packed with { Bytes = packed.Bytes.SetItem(18, data[0]).SetItem(19, data[1]), Known = packed.Known.SetItem(18, known[0]).SetItem(19, known[1]) };
    }

    private NativeBattleModelGeometrySource ReadNativeEffect314Geometry(NativeBattleGraphicsState graphics, NativeBattleEffect314Frame frame, uint model, bool mutable = false)
    {
        int actor = NativeEffect314Actor(graphics, model);
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect314 requires its current CPU owner.");
        NativeDeformedBodyBridge bridge = new(frame.Memory, frame.Effect, frame.Random, graphics, graphics.Models[actor], frame.Staging!, NativeBattleEffectArena.Clear(), 0);
        NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(bridge, false);
        if (mutable && pointer.Cpu is { IsOwnedResource: true })
        {
            return NativeBattleModelGeometrySource.OpenEditor(cpu, frame.Staging!, pointer, (offset, length) => NativeBattleModelResourceView.Open(NativeModelCpuResources!, frame.Staging!, pointer, checked(offset + length)).Read(offset, length));
        }
        return NativeBattleModelGeometrySource.Open(cpu, frame.Staging!, pointer.Pin(cpu), (offset, length) => ReadNativeDeformedGeometry(bridge, pointer, offset, length));
    }

    private NativeBattleEffect314Frame WriteNativeEffect314Geometry(NativeBattleGraphicsState graphics, NativeBattleEffect314Frame frame, uint model, int offset, byte[] bytes)
    {
        int actor = NativeEffect314Actor(graphics, model);
        NativeDeformedBodyBridge bridge = new(frame.Memory, frame.Effect, frame.Random, graphics, graphics.Models[actor], frame.Staging!, NativeBattleEffectArena.Clear(), 0);
        NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(bridge, false);
        if (offset < 0 || pointer.Length is int length && offset > length - bytes.Length)
        {
            throw new InvalidDataException("Effect314 geometry write leaves its selected section.");
        }
        if (pointer.Cpu is { IsOwnedResource: true })
        {
            NativeModelCpuResources = NativeBattleModelResourceView.WritePartySection(NativeModelCpuResources!, frame.Staging!, pointer, offset, bytes);
            return frame;
        }
        uint address = checked((pointer.Cpu?.TryPhysicalAddress() ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect314 shared geometry requires its actual addressed owner.")) + (uint)offset);
        return WriteNativeEffect314Arena(frame, address, NativeBattleEffectArena.Clear().Write(0, bytes), bytes.Length);
    }

    private NativeBattleEffect314Frame WriteNativeEffect314BoneMatrix(ref NativeBattleGraphicsState graphics, NativeBattleEffect314Frame frame, uint model, int bone, NativeBattlePackedMatrix32 packed)
    {
        int actor = NativeEffect314Actor(graphics, model);
        _ = ReadNativeEffect314BoneMatrix(graphics, frame, model, bone);
        NativeBattleGraphicsModel current = graphics.Models[actor];
        NativeBattleBodyBoneMatrices bones = current.CurrentBodyBoneMatrices!;
        graphics = graphics with { Models = graphics.Models.SetItem(actor, current with { CurrentBodyBoneMatrices = bones with { Matrices = bones.Matrices.SetItem(bone, packed.ReadMatrix()) } }) };
        return frame;
    }
}
