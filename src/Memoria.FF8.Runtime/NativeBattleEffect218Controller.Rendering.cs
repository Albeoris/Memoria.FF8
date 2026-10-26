using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static NativeBattleEffect218Frame DrawOwnedModel637430(NativeBattleEffect218Frame frame, uint modelAddress, NativeBattleEffectOwnedModelResource resource, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, bool hardware, NativeBattleEffectArena primitiveFlags, uint packetCursor, Func<NativeBattleEffect218Frame, NativeBattleGfGeometryBatch, NativeBattleEffect218Frame> deliver)
    {
        if ((modelAddress != EmbeddedModelAddress || resource.Address != 0xF1DD54) && (modelAddress != StreamedModelAddress || resource.Address != frame.State.ReadDword(ArenaGlobal)))
        {
            throw new InvalidDataException("Effect218 rendering requires its actual selected model source.");
        }
        NativeBattleEffectArena model = frame.State.ReadView(modelAddress, 0x90);
        uint descriptor = BinaryPrimitives.ReadUInt32LittleEndian(model.Read(0x64, 4));
        uint skeletonAddress = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, descriptor, 4).Read(0, 4));
        uint geometryAddress = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, descriptor + 4, 4).Read(0, 4));
        if (skeletonAddress != resource.Address + resource.Section(0) || geometryAddress != resource.Address + resource.Section(1))
        {
            throw new InvalidDataException("Effect218 rendering pointers leave their current model sections.");
        }
        NativeBattleEffectArena skeleton = ReadBytes(frame, skeletonAddress, resource.PoseSource.Skeleton.Length);
        int count = skeleton.Read(0, 1)[0];
        if (count != resource.PoseSource.BoneCount)
        {
            throw new InvalidDataException("Effect218 rendering lost its current physical bone count.");
        }
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(skeleton, 0, count);
        NativeBattleActionMatrix view = NativeBattleArenaPose.ReadMatrix(frame.State.ReadView(CurrentView, 32), 0);
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Compose56C2F0(view, NativeBattleArenaPose.ReadMatrix(model, 0x40));
        (frame, uint temporary) = Allocate(frame, 76);
        NativeBattleEffectArena context76 = NativeBattleEffectOwnedModelRendering.StoreMatrix(Context(frame, temporary, 76), 0, NativeBattlePackedMatrix32.FromMatrix(basis, null));
        ImmutableArray<NativeBattlePackedMatrix32>.Builder bones = ImmutableArray.CreateBuilder<NativeBattlePackedMatrix32>(count);
        for (int index = 0; index < count; index++)
        {
            int offset = checked(32 + index * 48);
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(basis, NativeBattleArenaPose.ReadMatrix(skeleton, offset));
            NativeBattleEffectArena rotation = PackedArena(NativeBattlePackedMatrix32.FromMatrix(composed, null));
            skeleton = NativeBattleEffectNodeContextMemory.Copy(rotation, 0, skeleton, offset, 18);
            skeleton = NativeBattleEffectNodeContextMemory.Copy(rotation, 20, skeleton, offset + 20, 12);
            (byte[] bytes, bool[] known) = skeleton.ReadSnapshot(offset, 32);
            bones.Add(new(bytes.ToImmutableArray(), known.ToImmutableArray()));
        }
        frame = WriteBytes(frame, skeletonAddress, skeleton, resource.PoseSource.Skeleton.Length);
        uint projectedAddress = frame.State.ReadDword(0xE257E8);
        int projectedLength = resource.Geometry.IsEmpty ? 0 : checked(resource.Geometry.Max(mesh => mesh.Vertices.Length) * 8);
        context76 = context76.Write(0x24, BitConverter.GetBytes(projectedAddress)).Write(0x34, new byte[4]).Write(0x38, BitConverter.GetBytes((short)320)).Write(0x3A, BitConverter.GetBytes((short)216)).Write(0x3C, model.Read(0x28, 4)).Write(0x40, model.Read(0x7C, 4)).Write(0x44, new byte[2]).Write(0x48, new byte[4]);
        context76 = NativeBattleEffectNodeContextMemory.Copy(primitiveFlags, 0, context76, 0x30, 4);
        NativeBattleEffectArena context44 = NativeBattleEffectNodeContextMemory.Copy(context76, 32, NativeBattleEffectArena.Clear().MarkUnknown(0, 44), 0, 44);
        NativeBattleModelContext44Step rendered = NativeBattleModelContext44.Draw(frame.Memory, context44, bones.ToImmutable(), ReadGeometry, geometryAddress, ReadBytes(frame, projectedAddress, projectedLength), projectedLength, projection, colors, hardware, 17, 4, packetCursor);
        frame = frame with { Memory = rendered.Memory };
        context76 = NativeBattleEffectNodeContextMemory.Copy(rendered.Context44, 0, context76, 32, 44);
        frame = PublishContext(frame, temporary, context76, 76);
        frame = WriteBytes(frame, projectedAddress, rendered.ProjectedScratch, projectedLength);
        frame = deliver(frame, rendered.Batch);
        (frame, uint restore) = Allocate(frame, 88);
        skeleton = NativeBattleArenaPose.RestoreBoneMatrices(skeleton, 0, pose);
        ImmutableArray<NativeBattleActionMatrix> restoredBones = Enumerable.Range(0, count).Select(index => NativeBattleArenaPose.ReadMatrix(skeleton, 32 + index * 48)).ToImmutableArray();
        NativeBattleEffectArena restored = NativeBattleBodyBoneTemporary.Prepare508C90(resource.PoseSource, pose, restoredBones, Context(frame, restore, 88));
        frame = PublishContext(frame, restore, restored, 88);
        frame = WriteBytes(frame, skeletonAddress, skeleton, resource.PoseSource.Skeleton.Length);
        return Release(Release(frame, 88), 76);

        byte[] ReadGeometry(int offset, int length)
        {
            int extent = resource.Section(2) - resource.Section(1);
            if (offset < 0 || length < 0 || offset > extent - length)
            {
                throw new InvalidDataException("Effect218 geometry read leaves its actual selected model section.");
            }
            return resource.Bytes.AsSpan().Slice(resource.Section(1) + offset, length).ToArray();
        }
    }
}
