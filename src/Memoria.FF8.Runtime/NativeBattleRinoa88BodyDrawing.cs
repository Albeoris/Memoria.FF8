using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa88BodyDraw(NativeBattleRinoa88Frame Frame, NativeBattleLogicalOrdering Ordering);

/// <summary>7719A0's ordinary posed body, bounds and shadow, followed by508C90's retained matrix publication.</summary>
public static class NativeBattleRinoa88BodyDrawing
{
    public static NativeBattleRinoa88BodyDraw Draw7719A0(NativeBattleRinoa88Frame frame, uint address, NativeBattleEffectOwnedModelResource resource, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleShadowSource shadow, NativeBattlePrimitiveCapabilities capabilities, NativeBattleSummonColors colors, NativeBattleLogicalOrdering ordering)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if ((task.ReadByte(38) & 4) != 0)
        {
            return new(frame, ordering);
        }
        NativeBattleEffectOwnedModelState model = frame.State.OwnedModels.GetValueOrDefault(address) ?? throw new NotSupportedException("Native7719A0 requires its actual effect-owned body.");
        ImmutableArray<byte> projected = frame.State.BodyProjected;
        if (projected.IsDefault || projected.Length != 2416 || resource.Geometry.Max(mesh => mesh.Vertices.Length) * 8 > projected.Length)
        {
            throw new NotSupportedException("Native7719A0 requires its retained original projected-vertex workspace.");
        }
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native7719A0 requires its actual renderer process owner.");
        var gate = process.Enter5099D0(capabilities.HardwareEnabled ?? throw new NotSupportedException("Native7719A0 requires its current hardware capability."));
        int start = frame.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native7719A0 requires its current private packet cursor.");
        NativeBattleEffectOwnedModelDraw drawn = NativeBattleEffectOwnedModelDrawing.Draw7558A0(task, model, resource, view, projection, shadow, capabilities, colors, projected, () => gate.Draw);
        process = gate.Process;
        if (gate.Draw)
        {
            process = process.PublishModelColor(task.ReadDword(88)).Publish50FDF0(checked(0x20DFAB8u + (uint)(start + drawn.BodyAllocationBytes)), null);
        }
        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process }, Effect = frame.Effect with { Pools = hierarchy.Write(address, drawn.Task).Pools }, State = frame.State with { BodyProjected = drawn.ProjectedScratch, BodyBones = frame.State.BodyBones.SetItem(address, NativeBattlePoseMatrices.StageBoneMatrices(resource.PoseSource, model.DecodedPose!)) } };
        var appended = NativeBattleEffectPacketArena.Append(frame.Memory, start, ordering, drawn.Packets);
        frame = frame with { Memory = appended.Memory, State = frame.State with { PrimaryDrawCursor = appended.Cursor } };
        return new(frame, appended.Ordering);
    }

    public static NativeBattleVector ReadBone502170(NativeBattleRinoa88Frame frame, uint bodyAddress, NativeBattleEffectOwnedModelResource resource, byte selector)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(bodyAddress);
        ImmutableArray<NativeBattleActionMatrix> bones = frame.State.BodyBones.GetValueOrDefault(bodyAddress) is { IsDefault: false } actualBones ? actualBones : throw new NotSupportedException("Native502170 requires its actual retained body bone matrices.");
        int bone = selector >= 240 ? resource.PoseSource.Skeleton[selector - 236] : selector;
        int fraction = (bone & 128) != 0 ? 4096 : 0;
        bone &= 127;
        if (bone >= bones.Length)
        {
            throw new InvalidDataException("Native502170 Rinoa88 selector leaves its original rig.");
        }
        NativeBattleActionMatrix world = new(Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(112 + index * 2))).ToImmutableArray(), unchecked((int)task.ReadDword(132)), unchecked((int)task.ReadDword(136)), unchecked((int)task.ReadDword(140)), 4096);
        NativeBattleActionMatrix selected = NativeBattlePoseMatrices.Compose56C2F0(world, bones[bone]);
        short parent = BinaryPrimitives.ReadInt16LittleEndian(resource.PoseSource.Skeleton.AsSpan().Slice(16 + bone * 48));
        int distance = parent < 0 ? 0 : fraction * BinaryPrimitives.ReadInt16LittleEndian(resource.PoseSource.Skeleton.AsSpan().Slice(18 + bone * 48)) >> 12;
        return new(unchecked((short)(selected.X + ((long)selected.Rotation[2] * distance >> 12))), unchecked((short)(selected.Y + ((long)selected.Rotation[5] * distance >> 12))), unchecked((short)(selected.Z + ((long)selected.Rotation[8] * distance >> 12))));
    }
}
