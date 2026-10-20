using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89BodyDraw(NativeBattleRinoa89Frame Frame, NativeBattleGfGeometryBatch Packets);
public sealed record NativeBattleRinoa89BoneStep(NativeBattleRinoa89Frame Frame, NativeBattleVector Position);

public static class NativeBattleRinoa89BodyDrawing
{
    public static NativeBattleRinoa89BodyDraw Draw6A79B0(NativeBattleRinoa89Frame frame, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelResource resource, NativeBattleCameraProjection projection, bool hardware)
    {
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Wishing Star body requires its current camera matrix.");
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Wishing Star body requires its actual projected workspace.");
        NativeBattleEffectArena skeleton = NativeBattleRinoa89Model.ReadSkeleton(frame.Staging, resource);
        NativeBattleActionMatrix modelView = NativeBattlePoseMatrices.ComposeStageCamera([World(task)], view, 0)[0];
        int count = skeleton.Read(0, 1)[0];
        for (int index = 0; index < count; index++)
        {
            NativeBattleActionMatrix bone = NativeBattleArenaPose.ReadMatrix(skeleton, 32 + index * 48);
            skeleton = NativeBattleArenaPose.WriteMatrix(skeleton, 32 + index * 48, NativeBattlePoseMatrices.ComposeStageCamera([bone], modelView, 0)[0], true);
        }
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Wishing Star body requires its retained renderer process.");
        var gate = process.Enter5099D0(hardware);
        process = gate.Process;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        if (gate.Draw)
        {
            uint rgb = task.ReadDword(92);
            process = process.PublishModelColor(rgb);
            int geometryStart = resource.Section(1);
            ImmutableArray<byte> geometry = resource.Bytes.Slice(geometryStart, resource.Section(2) - geometryStart);
            NativeBattleQuezacotlPathModelBatch batch = NativeBattleQuezacotlPathModelPackets.Emit6A79B0(geometry, selector => NativeBattleArenaPose.ReadMatrix(skeleton, checked(32 + selector * 48)), projection, rgb, task.ReadDword(176));
            foreach (NativeBattleGfGeometryAttempt write in batch.VertexScratchWrites)
            {
                arena = arena.Write(checked(100492 + write.ByteOffsetRelative), write.Data.AsSpan());
            }
            foreach (NativeBattleGeometryPrimitive primitive in batch.Primitives)
            {
                byte[] bytes = new byte[primitive.Words.Length * 4];
                for (int index = 0; index < primitive.Words.Length; index++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), primitive.Words[index]);
                }
                attempts.Add(new(allocated, bytes.ToImmutableArray(), primitive.PhysicalWriteMask ?? Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray(), primitive));
                allocated = checked(allocated + bytes.Length);
            }
            if (batch.SourceEnd is int end)
            {
                uint cursor = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x25565EC, 4));
                process = process.Publish50FDF0(checked(cursor + (uint)allocated), checked(resource.Address + (uint)geometryStart + (uint)end));
            }
        }
        NativeBattleDecodedPose current = NativeBattleArenaPose.ReadCurrentPose(skeleton, 0, count);
        skeleton = NativeBattleArenaPose.RestoreBoneMatrices(skeleton, 0, current);
        frame = frame with { Memory = frame.Memory with { Arena = arena, PrimitiveProcess = process }, Staging = NativeBattleRinoa89Model.PublishSkeleton(frame.Staging, resource, skeleton) };
        return new(frame, new(attempts.ToImmutable(), allocated));
    }

    public static NativeBattleRinoa89BoneStep ReadBone6FD2D0(NativeBattleRinoa89Frame frame, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelResource resource, ReadOnlySpan<byte> reference)
    {
        if (reference.Length != 8)
        {
            throw new InvalidDataException("Native6FD2D0 requires its actual eight-byte bone reference.");
        }
        NativeBattleEffectArena skeleton = NativeBattleRinoa89Model.ReadSkeleton(frame.Staging, resource);
        int count = skeleton.Read(0, 1)[0];
        NativeBattleDecodedPose current = NativeBattleArenaPose.ReadCurrentPose(skeleton, 0, count);
        skeleton = NativeBattleArenaPose.RestoreBoneMatrices(skeleton, 0, current);
        short selector = BinaryPrimitives.ReadInt16LittleEndian(reference[6..]);
        if (selector < 0 || selector >= count)
        {
            throw new InvalidDataException("Native6FD2D0 bone selector leaves its actual skeleton.");
        }
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(World(task), NativeBattleArenaPose.ReadMatrix(skeleton, 32 + selector * 48));
        NativeBattleVector source = new(BinaryPrimitives.ReadInt16LittleEndian(reference), BinaryPrimitives.ReadInt16LittleEndian(reference[2..]), BinaryPrimitives.ReadInt16LittleEndian(reference[4..]));
        return new(frame with { Staging = NativeBattleRinoa89Model.PublishSkeleton(frame.Staging, resource, skeleton) }, NativeBattleGfMatrices.Transform460840(composed, source));
    }

    private static NativeBattleActionMatrix World(NativeBattleModulePoolTask task)
    {
        return new(Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(116 + index * 2))).ToImmutableArray(), unchecked((int)task.ReadDword(136)), unchecked((int)task.ReadDword(140)), unchecked((int)task.ReadDword(144)), 4096);
    }
}
