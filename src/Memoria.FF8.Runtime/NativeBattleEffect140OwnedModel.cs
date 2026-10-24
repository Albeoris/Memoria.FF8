using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect140Controller;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect140OwnedModelSnapshot(NativeBattleEffectOwnedModelResource Resource, NativeBattleEffectArena Skeleton, NativeBattleDecodedPose Pose, ImmutableArray<NativeBattlePackedMatrix32> Bones, ImmutableArray<NativeBattleRawMesh> Geometry);

public static class NativeBattleEffect140OwnedModel
{
    public static NativeBattleEffect140Frame Initialize6EC060(NativeBattleEffect140Frame frame, uint model, uint descriptor, NativeBattleEffect140Resources resources)
    {
        NativeBattleEffectOwnedModelResource source = resources.Model;
        frame = PutWord(frame, model, 2);
        frame = PutDword(frame, model + 0x7C, uint.MaxValue);
        frame = PutDword(frame, model + 0x8C, 0);
        frame = PutDword(frame, model + 0x78, 0);
        frame = PutByte(frame, model + 0x61, 0);
        frame = PutByte(frame, model + 0x60, 0);
        frame = PutDword(frame, model + 0x68, descriptor);
        frame = PutDword(frame, model + 0x64, descriptor);
        frame = PutByte(frame, model + 0x6D, 0);
        frame = PutDword(frame, model + 0x28, 0x808080);
        for (int index = 0; index < 4; index++)
        {
            frame = PutDword(frame, descriptor + (uint)(index * 4), checked(source.Address + (uint)source.Section(index)));
        }
        return frame;
    }

    public static NativeBattleEffect140Frame Select6574D0(NativeBattleEffect140Frame frame, uint model, byte selector, NativeBattleEffect140Resources resources)
    {
        SourcePointers(frame, model, resources.Model);
        int count = ReadBytes(frame, Skeleton(resources.Model), 1).Read(0, 1)[0];
        byte[] animations = ReadBytes(frame, Animation(resources.Model), resources.Model.Section(3) - resources.Model.Section(2)).Read(0, resources.Model.Section(3) - resources.Model.Section(2));
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(animations, count, selector);
        byte flags = Byte(frame, model + 0x6D);
        frame = PutByte(frame, model + 0x6C, selector);
        frame = PutByte(frame, model + 0x6D, (byte)((flags & 0xF3) | 4));
        frame = PutByte(frame, model + 0x72, 1);
        frame = PutByte(frame, model + 0x73, NativeBattlePoseDecoder.ReadFrameCounts(animations)[selector]);
        frame = PutWord(frame, model + 0x6E, first.ByteOffset);
        frame = PutWord(frame, model + 0x70, first.BitOffset);
        return PublishPose(frame, resources.Model, first.Pose);
    }

    public static NativeBattleEffect140Frame Advance6FBDB0(NativeBattleEffect140Frame frame, uint model, NativeBattleEffect140Resources resources)
    {
        SourcePointers(frame, model, resources.Model);
        byte flags = Byte(frame, model + 0x6D);
        if ((flags & 3) != 0)
        {
            throw new NotSupportedException("Effect140 requires its selected normal pose speed.");
        }
        byte selector = Byte(frame, model + 0x6C);
        if (Byte(frame, model + 0x72) >= Byte(frame, model + 0x73))
        {
            return Select6574D0(frame, model, selector, resources);
        }
        NativeBattleEffectArena skeleton = ReadBytes(frame, Skeleton(resources.Model), resources.Model.PoseSource.Skeleton.Length);
        NativeBattleDecodedPose current = NativeBattleArenaPose.ReadCurrentPose(skeleton, 0, resources.Model.PoseSource.BoneCount);
        int length = resources.Model.Section(3) - resources.Model.Section(2);
        byte[] animations = ReadBytes(frame, Animation(resources.Model), length).Read(0, length);
        NativeBattlePoseFirstFrame next = NativeBattlePoseDecoder.AdvanceRegular508F90(animations, current.Bones.Length, selector, current, Word(frame, model + 0x6E), Word(frame, model + 0x70));
        frame = PutWord(frame, model + 0x6E, next.ByteOffset);
        frame = PutWord(frame, model + 0x70, next.BitOffset);
        frame = PutByte(frame, model + 0x72, unchecked((byte)(Byte(frame, model + 0x72) + 1)));
        frame = PutByte(frame, model + 0x6D, (byte)((flags & 0xF3) | ((((flags >> 2) + 1) & 1) << 2)));
        return PublishPose(frame, resources.Model, next.Pose);
    }

    public static NativeBattleEffect140OwnedModelSnapshot ReadCurrent(NativeBattleEffect140Frame frame, uint model, NativeBattleEffect140Resources resources)
    {
        SourcePointers(frame, model, resources.Model);
        NativeBattleEffectOwnedModelResource resource = resources.Model;
        NativeBattleEffectArena skeleton = ReadBytes(frame, Skeleton(resource), resource.PoseSource.Skeleton.Length);
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(skeleton, 0, resource.PoseSource.BoneCount);
        ImmutableArray<NativeBattlePackedMatrix32>.Builder bones = ImmutableArray.CreateBuilder<NativeBattlePackedMatrix32>(pose.Bones.Length);
        for (int index = 0; index < pose.Bones.Length; index++)
        {
            (byte[] bytes, bool[] known) = skeleton.ReadSnapshot(32 + index * 48, 32);
            bones.Add(new(bytes.ToImmutableArray(), known.ToImmutableArray()));
        }
        int geometryLength = resource.Section(2) - resource.Section(1);
        byte[] geometry = ReadBytes(frame, resource.Address + (uint)resource.Section(1), geometryLength).Read(0, geometryLength);
        return new(resource, skeleton, pose, bones.MoveToImmutable(), NativeBattleRawGeometryReader.Read(geometry, pose.Bones.Length));
    }

    public static NativeBattleEffect140Frame PublishPose(NativeBattleEffect140Frame frame, NativeBattleEffectOwnedModelResource resource, NativeBattleDecodedPose pose)
    {
        NativeBattleEffectArena current = ReadBytes(frame, Skeleton(resource), resource.PoseSource.Skeleton.Length);
        current = NativeBattleArenaPose.WritePose(current, 0, pose);
        current = NativeBattleArenaPose.RestoreBoneMatrices(current, 0, pose);
        return WriteBytes(frame, Skeleton(resource), current, resource.PoseSource.Skeleton.Length);
    }

    private static void SourcePointers(NativeBattleEffect140Frame frame, uint model, NativeBattleEffectOwnedModelResource resource)
    {
        uint directory = Dword(frame, model + 0x64);
        if (Dword(frame, model + 0x68) != directory)
        {
            throw new InvalidDataException("Effect140's current model descriptor aliases differ.");
        }
        for (int index = 0; index < 4; index++)
        {
            if (Dword(frame, directory + (uint)(index * 4)) != resource.Address + (uint)resource.Section(index))
            {
                throw new InvalidDataException("Effect140's current model descriptor leaves its source.");
            }
        }
    }

    private static uint Skeleton(NativeBattleEffectOwnedModelResource resource) => resource.Address + (uint)resource.Section(0);
    private static uint Animation(NativeBattleEffectOwnedModelResource resource) => resource.Address + (uint)resource.Section(2);
    private static byte Byte(NativeBattleEffect140Frame frame, uint address) => ReadBytes(frame, address, 1).Read(0, 1)[0];
    private static ushort Word(NativeBattleEffect140Frame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(frame, address, 2).Read(0, 2));
    private static uint Dword(NativeBattleEffect140Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, address, 4).Read(0, 4));
    private static NativeBattleEffect140Frame PutByte(NativeBattleEffect140Frame frame, uint address, byte value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);
    private static NativeBattleEffect140Frame PutWord(NativeBattleEffect140Frame frame, uint address, ushort value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);
    private static NativeBattleEffect140Frame PutDword(NativeBattleEffect140Frame frame, uint address, uint value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 4);
}
